using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 로그라이크에만 있는 액티브 스킬과 조합 효과. 기획서 3-3 "액티브 카드 7종"·"조합 카드(프리즘) 5종".
///
///   본편 스킬 재사용(RunEffectApplier가 스탯으로 켬): 회전베기, 충격파, 부메랑, 드론
///   여기서 구현하는 신규 스킬: 오프로드, 스텔라, 포자폭탄
///   여기서 구현하는 조합: 회전베기+충격파(360도 충격파), 부메랑+충격파, 회전베기+부메랑, 스텔라+포자폭탄, 스텔라+드론
///
/// 판정과 피해만 맡는다. 외형은 이펙트 담당이 RoguelikeSkillVfxSignal(및 별 프리팹)로 입힌다.
/// 웨이브 전투 중에만 돈다(BeginCombat ~ EndCombat). 웨이브가 끝나면 별·연막·표식을 모두 치운다.
///
/// 카드 수치 적용 규칙(기획서 3-3 공통 패시브):
///   피해 = 기본 × (1 + 피해%(전역+스킬)/100), 도트 = 기본 × (1 + 피해%/100) × (1 + 도트%/100)
///   범위·지속 = 기본 × (1 + %/100), 주기 = 기본 ÷ (1 + 공격속도%/100) × (1 - 재사용 대기 감소%/100)
///   발동 수 = 기본 + 발동 수(전역+스킬)
/// 기획서 미정 항목(공격력과 도트의 중복 적용, 공격속도의 적용 범위)은 위처럼 "모두 적용"으로 두었다.
/// </summary>
public class RoguelikeSkillController : MonoBehaviour
{
    private struct MarkedTree
    {
        public TreeObj tree;
        public float expireTime;
        public bool bDot;
        public float nextDotTime;
    }

    private struct SporeZone
    {
        public Vector3 position;
        public float radius;
        public float endTime;
        public float nextTickTime;
        public float tickDamage;
    }

    private struct OffroadDash
    {
        public Vector3 from;
        public Vector3 to;
        public float startTime;
        public float dashStartTime;
        public float dashDuration;
        public Vector3 lastPosition;
        public float damage;
        public float radius;
    }

    [SerializeField] private RoguelikeSkillTuningSO tuning;
    [SerializeField] private RoguelikeStar starPrefab;
    [Tooltip("오프로드 외형(선택). 비어 있으면 판정만 움직인다.")]
    [SerializeField] private GameObject offroadVisualPrefab;
    [SerializeField] private LayerMask treeLayer;

    // // 외부 의존성
    private SignalHub signalHub;
    private RunModifiers modifiers;
    private Character character;
    private AxeExtraAttackCreator axeExtraAttackCreator;

    // // 상태
    private bool bActive;
    private Vector3 fieldCenter;
    private float fieldRadiusX = 8f;
    private float fieldRadiusY = 4.5f;

    private readonly List<IStaticCollidable> scanBuffer = new List<IStaticCollidable>(128);
    private readonly List<TreeObj> hitBuffer = new List<TreeObj>(64);

    private readonly List<RoguelikeStar> activeStars = new List<RoguelikeStar>(16);
    private readonly Stack<RoguelikeStar> starPool = new Stack<RoguelikeStar>(16);
    private readonly List<RoguelikeStar> pendingStarHits = new List<RoguelikeStar>(4);
    private int nextStarId = 1;

    private readonly List<MarkedTree> markedTrees = new List<MarkedTree>(128);
    private readonly List<SporeZone> sporeZones = new List<SporeZone>(8);
    private readonly List<OffroadDash> offroadDashes = new List<OffroadDash>(4);
    private readonly List<GameObject> offroadVisuals = new List<GameObject>(4);
    private readonly HashSet<TreeObj> offroadHitTrees = new HashSet<TreeObj>();

    private readonly List<Vector3> pendingBoomerangHits = new List<Vector3>(16);
    private readonly List<Vector3> pendingSporeExplosions = new List<Vector3>(16);
    private int pendingAxeSwings;
    private int spinShockwaveSwingCount;

    // 스텔라+드론 이음선(별 인덱스 쌍). 별이 바뀔 때마다 최소 신장 트리로 다시 잇는다 - 더 가까운 새 별이 생기면
    // 기존 긴 이음선이 끊기고 새 별로 이어지는 규칙(기획서 "이음선 재구성")이 그대로 성립한다.
    private readonly List<Vector2Int> starLinks = new List<Vector2Int>(16);
    // 빔을 쏘는 동안 쓰는 이음선 사본. 빔에 나무가 죽으면 그 자리에 별이 생기며 starLinks가 다시 만들어지므로(TrySpawnStar) 원본을 돌면 안 된다.
    private readonly List<Vector2Int> beamLinkBuffer = new List<Vector2Int>(16);
    private readonly List<bool> mstInTree = new List<bool>(16);
    private readonly List<float> mstBestDist = new List<float>(16);
    private readonly List<int> mstBestFrom = new List<int>(16);

    private float nextOffroadTime;
    private float nextSporeTime;
    private float nextBeamTime;
    private float nextLonePulseTime;
    private float nextBoomerangShockwaveTime;
    private float nextSpinBoomerangTime;

    private System.Action<Vector3> axeSwungHandler;
    private System.Action<Vector3> boomerangHitHandler;
    private System.Action<RoguelikeStar> starHitHandler;

    /// <summary>스텔라+드론 이음선. 이펙트 담당이 선을 그릴 때 읽는다(StarLinksChanged 시그널 뒤).</summary>
    public IReadOnlyList<Vector2Int> StarLinks => starLinks;
    public IReadOnlyList<RoguelikeStar> ActiveStars => activeStars;

    public void Initialize(SignalHub _signalHub, RunModifiers _modifiers)
    {
        signalHub = _signalHub;
        modifiers = _modifiers;

        axeSwungHandler = OnAxeSwung;
        boomerangHitHandler = OnBoomerangHitTree;
        starHitHandler = OnStarHit;

        RunCombatHooks.AxeSwungEvent -= axeSwungHandler;
        RunCombatHooks.AxeSwungEvent += axeSwungHandler;
        RunCombatHooks.BoomerangHitTreeEvent -= boomerangHitHandler;
        RunCombatHooks.BoomerangHitTreeEvent += boomerangHitHandler;
    }

    public void Release()
    {
        RunCombatHooks.AxeSwungEvent -= axeSwungHandler;
        RunCombatHooks.BoomerangHitTreeEvent -= boomerangHitHandler;
        EndCombat();
    }

    public void SetCharacter(Character _character)
    {
        character = _character;
        axeExtraAttackCreator = null != _character ? _character.GetComponentInChildren<AxeExtraAttackCreator>(true) : null;
    }

    /// <summary>섬(타원)의 중심과 반지름(월드). 오프로드가 섬 가장자리에서 반대편 가장자리로 돌진하는 데 쓴다.</summary>
    public void SetField(Vector3 _center, float _radiusX, float _radiusY)
    {
        fieldCenter = _center;
        fieldRadiusX = Mathf.Max(1f, _radiusX);
        fieldRadiusY = Mathf.Max(1f, _radiusY);
    }

    public void BeginCombat()
    {
        bActive = true;
        float now = Time.time;
        nextOffroadTime = now + Interval(tuning.offroadInterval, ERoguelikeSkill.Offroad);
        nextSporeTime = now + Interval(tuning.sporeInterval, ERoguelikeSkill.SporeBomb) * 0.5f;
        nextBeamTime = now + tuning.stellaDroneBeamInterval;
        nextLonePulseTime = now + tuning.stellaDroneLoneStarInterval;
        pendingAxeSwings = 0;
        pendingBoomerangHits.Clear();
        pendingSporeExplosions.Clear();
    }

    /// <summary>웨이브 종료. 별·연막·표식·돌진을 모두 치운다.</summary>
    public void EndCombat()
    {
        bActive = false;

        for (int i = activeStars.Count - 1; i >= 0; i--) DespawnStar(i, false);
        starLinks.Clear();

        for (int i = 0; i < markedTrees.Count; i++)
        {
            TreeObj tree = markedTrees[i].tree;
            if (null != tree && false == tree.IsPooled) tree.SetStarMarked(false);
        }

        markedTrees.Clear();
        sporeZones.Clear();
        offroadDashes.Clear();
        offroadHitTrees.Clear();
        for (int i = 0; i < offroadVisuals.Count; i++) if (null != offroadVisuals[i]) offroadVisuals[i].SetActive(false);

        pendingStarHits.Clear();
        pendingBoomerangHits.Clear();
        pendingSporeExplosions.Clear();
        pendingAxeSwings = 0;
        spinShockwaveSwingCount = 0;
    }

    /// <summary>웨이브 전투 중 나무가 쓰러졌다(RoguelikeSystem이 알려 준다).</summary>
    public void OnTreeKilled(TreeObj _tree, Vector3 _position)
    {
        if (false == bActive) return;

        // 표식·도트 목록에서 뺀다. 이 나무는 곧 풀로 돌아가 다른 나무로 재사용된다.
        for (int i = markedTrees.Count - 1; i >= 0; i--)
        {
            if (markedTrees[i].tree != _tree) continue;

            if (markedTrees[i].bDot && modifiers.IsComboActive(ERoguelikeCombo.StellaSpore))
            {
                pendingSporeExplosions.Add(_position);
            }

            markedTrees.RemoveAt(i);
        }

        TrySpawnStar(_position);
    }

    // // 유니티

    private void Update()
    {
        if (false == bActive || null == tuning || null == character || character.bDead) return;

        float now = Time.time;

        UpdateCombos(now);
        UpdateStars(now);
        UpdateMarks(now);
        UpdateSpore(now);
        UpdateOffroad(now);
        UpdateStellaDrone(now);
    }

    // // 조합: 회전베기+충격파 / 부메랑+충격파 / 회전베기+부메랑

    private void OnAxeSwung(Vector3 _center)
    {
        if (bActive) pendingAxeSwings++;
    }

    private void OnBoomerangHitTree(Vector3 _position)
    {
        if (bActive && pendingBoomerangHits.Count < 64) pendingBoomerangHits.Add(_position);
    }

    private void UpdateCombos(float _now)
    {
        // 회전베기 + 충격파: 회전베기·충격파 기본 능력은 꺼지고, n번 휘두를 때마다 360도 충격파.
        if (modifiers.IsComboActive(ERoguelikeCombo.SpinShockwave))
        {
            int interval = Mathf.Max(1, tuning.spinShockwaveSwingInterval + Mathf.RoundToInt(modifiers.GetForSkill(ERunEffect.TriggerHitCountAdd, ERoguelikeSkill.Spin)));
            for (int i = 0; i < pendingAxeSwings; i++)
            {
                spinShockwaveSwingCount++;
                if (spinShockwaveSwingCount < interval) continue;

                spinShockwaveSwingCount = 0;
                Fire360ShockWave(character.transform.position);
            }
        }
        pendingAxeSwings = 0;

        if (pendingBoomerangHits.Count > 0)
        {
            bool bBoomerangShockwave = modifiers.IsComboActive(ERoguelikeCombo.BoomerangShockwave);
            bool bSpinBoomerang = modifiers.IsComboActive(ERoguelikeCombo.SpinBoomerang);

            for (int i = 0; i < pendingBoomerangHits.Count; i++)
            {
                // 부메랑 + 충격파: 맞은 나무 주변으로 충격파가 추가로 터진다(부메랑·충격파 기본 능력은 유지).
                if (bBoomerangShockwave && _now >= nextBoomerangShockwaveTime)
                {
                    nextBoomerangShockwaveTime = _now + tuning.boomerangShockwaveCooldown;
                    Fire360ShockWave(pendingBoomerangHits[i]);
                }

                // 회전베기 + 부메랑: 부메랑 타격 1회당 회전베기 1회(회전베기의 도끼 n타 발동은 꺼짐).
                if (bSpinBoomerang && _now >= nextSpinBoomerangTime)
                {
                    nextSpinBoomerangTime = _now + tuning.spinBoomerangCooldown;
                    SpinStrikeAroundCharacter();
                }
            }

            pendingBoomerangHits.Clear();
        }
    }

    private void Fire360ShockWave(Vector3 _position)
    {
        if (null == axeExtraAttackCreator) return;

        ShockWave sw = axeExtraAttackCreator.CreateShockWave(_position);
        if (null == sw) return;

        sw.SetDirection(Vector3.right);
        // 부채꼴 반각 180도 = 전 범위. 다음 생성 때 Reset이 기본 각도로 되돌린다.
        sw.angle = 180f;
        axeExtraAttackCreator.PlayShockWaveVisual(sw);

        Publish(ERoguelikeSkillVfx.Shockwave360, _position, _position, 0f, sw.LifeTime, 0);
    }

    private void SpinStrikeAroundCharacter()
    {
        StatComponent stat = character.statComponent;
        Vector3 center = character.transform.position;
        float radius = tuning.spinBoomerangRadius * stat.axeAttackRangeMultiplier;
        float damage = stat.axeDamage * tuning.spinBoomerangDamageMultiplier;

        DamageTreesInRadius(center, radius, damage, false, false);
        Publish(ERoguelikeSkillVfx.SpinBoomerangStrike, center, center, radius, 0f, 0);
    }

    // // 스텔라

    private void TrySpawnStar(Vector3 _position)
    {
        if (null == starPrefab || false == modifiers.IsSkillUnlocked(ERoguelikeSkill.Stella)) return;

        int maxStars = MaxStars();
        if (activeStars.Count >= maxStars) return;

        float chance = tuning.stellaBaseChance + modifiers.GetForSkill(ERunEffect.ChancePercent, ERoguelikeSkill.Stella);
        if (Random.value * 100f >= chance) return;

        bool bStellaDrone = modifiers.IsComboActive(ERoguelikeCombo.StellaDrone);

        RoguelikeStar star = starPool.Count > 0 ? starPool.Pop() : CreateStar();
        star.Place(nextStarId++, _position, GetFirstLayer(treeLayer), false == bStellaDrone);
        star.ExpireTime = Time.time + tuning.stellaStarLifetime * DurationMul(ERoguelikeSkill.Stella);
        activeStars.Add(star);

        Publish(ERoguelikeSkillVfx.StarSpawned, _position, _position, 0f, 0f, star.StarId);

        if (bStellaDrone) RebuildStarLinks();
    }

    private int MaxStars()
    {
        int max = tuning.stellaBaseMaxStars + Mathf.RoundToInt(modifiers.GetForSkill(ERunEffect.CountAdd, ERoguelikeSkill.Stella));

        // 스텔라+드론은 노바 조건(별 9개)까지 모여야 하므로 상한을 그 수 이상으로 연다.
        if (modifiers.IsComboActive(ERoguelikeCombo.StellaDrone)) max = Mathf.Max(max, tuning.stellaDroneNovaStars);

        return Mathf.Max(1, max);
    }

    private RoguelikeStar CreateStar()
    {
        RoguelikeStar star = Instantiate(starPrefab, transform);
        star.HitEvent += starHitHandler;
        return star;
    }

    private void OnStarHit(RoguelikeStar _star)
    {
        // 도끼 판정 루프 안에서 불린다. 바로 터뜨리지 않고 다음 Update에서 처리한다.
        if (false == pendingStarHits.Contains(_star)) pendingStarHits.Add(_star);
    }

    private void UpdateStars(float _now)
    {
        for (int i = 0; i < pendingStarHits.Count; i++)
        {
            int idx = activeStars.IndexOf(pendingStarHits[i]);
            if (idx < 0) continue;

            RoguelikeStar star = activeStars[idx];
            StarBlast(star.transform.position, tuning.stellaBlastRadius, tuning.stellaBlastDamage);
            Publish(ERoguelikeSkillVfx.StarBlast, star.transform.position, star.transform.position, tuning.stellaBlastRadius * RangeMul(ERoguelikeSkill.Stella), 0f, star.StarId);
            DespawnStar(idx, false);
        }
        pendingStarHits.Clear();

        // 스텔라+드론의 별은 노바 때만 사라진다.
        if (modifiers.IsComboActive(ERoguelikeCombo.StellaDrone)) return;

        for (int i = activeStars.Count - 1; i >= 0; i--)
        {
            if (_now < activeStars[i].ExpireTime) continue;

            Publish(ERoguelikeSkillVfx.StarExpired, activeStars[i].transform.position, activeStars[i].transform.position, 0f, 0f, activeStars[i].StarId);
            DespawnStar(i, false);
        }
    }

    /// <summary>별의 단발 공격. 주변 나무에 피해를 주고 별 표식을 남긴다(스텔라+포자폭탄이면 표식에 도트가 붙는다).</summary>
    private void StarBlast(Vector3 _center, float _baseRadius, float _baseDamage)
    {
        float radius = _baseRadius * RangeMul(ERoguelikeSkill.Stella);
        float damage = _baseDamage * DamageMul(ERoguelikeSkill.Stella);
        bool bDot = modifiers.IsComboActive(ERoguelikeCombo.StellaSpore);

        DamageTreesInRadius(_center, radius, damage, true, bDot);
    }

    private void DespawnStar(int _index, bool _bRebuildLinks)
    {
        RoguelikeStar star = activeStars[_index];
        activeStars.RemoveAt(_index);
        star.Remove();
        starPool.Push(star);

        if (_bRebuildLinks) RebuildStarLinks();
    }

    // // 표식·도트

    private void MarkTree(TreeObj _tree, bool _bDot)
    {
        float expire = Time.time + tuning.stellaMarkDuration * DurationMul(ERoguelikeSkill.Stella);

        for (int i = 0; i < markedTrees.Count; i++)
        {
            if (markedTrees[i].tree != _tree) continue;

            MarkedTree existing = markedTrees[i];
            existing.expireTime = expire;
            existing.bDot |= _bDot;
            markedTrees[i] = existing;
            return;
        }

        _tree.SetStarMarked(true);
        markedTrees.Add(new MarkedTree
        {
            tree = _tree,
            expireTime = expire,
            bDot = _bDot,
            nextDotTime = Time.time + tuning.stellaSporeDotInterval,
        });
    }

    private void UpdateMarks(float _now)
    {
        float dotDamage = tuning.stellaSporeDotDamage * DotMul(ERoguelikeSkill.Stella);

        TreeTileWriteBatch.Begin();
        try
        {
            for (int i = markedTrees.Count - 1; i >= 0; i--)
            {
                if (i >= markedTrees.Count) continue;

                MarkedTree mark = markedTrees[i];
                TreeObj tree = mark.tree;

                if (null == tree || tree.IsPooled || tree.bDead || false == tree.bCanApplyDamage)
                {
                    markedTrees.RemoveAt(i);
                    continue;
                }

                if (_now >= mark.expireTime)
                {
                    tree.SetStarMarked(false);
                    markedTrees.RemoveAt(i);
                    continue;
                }

                if (mark.bDot && _now >= mark.nextDotTime)
                {
                    mark.nextDotTime = _now + tuning.stellaSporeDotInterval;
                    markedTrees[i] = mark;

                    // 이 타격으로 죽으면 OnTreeKilled가 목록에서 빼고 포자 폭발을 예약한다.
                    tree.TakeDamageWithoutHitSound(dotDamage);
                }
            }
        }
        finally
        {
            TreeTileWriteBatch.End();
        }

        // 스텔라 + 포자폭탄: 도트 중인 나무가 무엇으로 죽었든 포자 폭발. 연쇄는 프레임당 상한까지만.
        int explosions = Mathf.Min(pendingSporeExplosions.Count, Mathf.Max(1, tuning.stellaSporeMaxExplosionsPerFrame));
        for (int i = 0; i < explosions; i++)
        {
            Vector3 pos = pendingSporeExplosions[i];
            float radius = tuning.stellaSporeExplosionRadius * RangeMul(ERoguelikeSkill.SporeBomb);
            DamageTreesInRadius(pos, radius, tuning.stellaSporeExplosionDamage * DamageMul(ERoguelikeSkill.SporeBomb), false, false);
            Publish(ERoguelikeSkillVfx.SporeExplosion, pos, pos, radius, 0f, 0);
        }

        if (explosions > 0) pendingSporeExplosions.RemoveRange(0, explosions);
    }

    // // 포자폭탄

    private void UpdateSpore(float _now)
    {
        if (modifiers.IsSkillActive(ERoguelikeSkill.SporeBomb) && _now >= nextSporeTime)
        {
            nextSporeTime = _now + Interval(tuning.sporeInterval, ERoguelikeSkill.SporeBomb);

            int bombs = 1 + CountAdd(ERoguelikeSkill.SporeBomb);
            for (int i = 0; i < bombs; i++) ThrowSporeBomb(_now);
        }

        if (0 == sporeZones.Count) return;

        TreeTileWriteBatch.Begin();
        try
        {
            for (int i = sporeZones.Count - 1; i >= 0; i--)
            {
                SporeZone zone = sporeZones[i];

                if (_now >= zone.endTime)
                {
                    sporeZones.RemoveAt(i);
                    continue;
                }

                if (_now < zone.nextTickTime) continue;

                zone.nextTickTime = _now + tuning.sporeTickInterval;
                sporeZones[i] = zone;
                DamageTreesInRadius(zone.position, zone.radius, zone.tickDamage, false, false, false);
            }
        }
        finally
        {
            TreeTileWriteBatch.End();
        }
    }

    private void ThrowSporeBomb(float _now)
    {
        // 캐릭터 주변 나무 하나를 노린다. 없으면 이번 투하는 건너뛴다.
        CollisionSystem.Instance?.GetCollidablesInRadius(character.transform.position, tuning.sporeTargetRange, treeLayer.value, scanBuffer);

        int candidates = 0;
        for (int i = 0; i < scanBuffer.Count; i++)
        {
            if (scanBuffer[i] is TreeObj tree && IsHittable(tree)) candidates++;
        }

        if (0 == candidates) return;

        int pick = Random.Range(0, candidates);
        Vector3 target = character.transform.position;
        for (int i = 0; i < scanBuffer.Count; i++)
        {
            if (false == (scanBuffer[i] is TreeObj tree) || false == IsHittable(tree)) continue;
            if (0 == pick--)
            {
                target = tree.Position;
                break;
            }
        }

        float radius = tuning.sporeRadius * RangeMul(ERoguelikeSkill.SporeBomb);
        float duration = tuning.sporeDuration * DurationMul(ERoguelikeSkill.SporeBomb);

        sporeZones.Add(new SporeZone
        {
            position = target,
            radius = radius,
            endTime = _now + duration,
            nextTickTime = _now,
            tickDamage = tuning.sporeTickDamage * DotMul(ERoguelikeSkill.SporeBomb),
        });

        Publish(ERoguelikeSkillVfx.SporeZoneStarted, target, character.transform.position, radius, duration, 0);
    }

    // // 오프로드

    private void UpdateOffroad(float _now)
    {
        if (modifiers.IsSkillActive(ERoguelikeSkill.Offroad) && _now >= nextOffroadTime)
        {
            nextOffroadTime = _now + Interval(tuning.offroadInterval, ERoguelikeSkill.Offroad);

            int count = 1 + CountAdd(ERoguelikeSkill.Offroad);
            for (int i = 0; i < count; i++) StartOffroad(_now);
        }

        for (int i = offroadDashes.Count - 1; i >= 0; i--)
        {
            OffroadDash dash = offroadDashes[i];
            GameObject visual = i < offroadVisuals.Count ? offroadVisuals[i] : null;

            if (_now < dash.dashStartTime)
            {
                if (null != visual) visual.transform.position = dash.from;
                continue;
            }

            float t = dash.dashDuration > 0f ? Mathf.Clamp01((_now - dash.dashStartTime) / dash.dashDuration) : 1f;
            Vector3 pos = Vector3.Lerp(dash.from, dash.to, t);
            if (null != visual) visual.transform.position = pos;

            DamageTreesAlongSegment(dash.lastPosition, pos, dash.radius, dash.damage, offroadHitTrees);
            dash.lastPosition = pos;
            offroadDashes[i] = dash;

            if (t >= 1f)
            {
                offroadDashes.RemoveAt(i);
                if (null != visual)
                {
                    visual.SetActive(false);
                    offroadVisuals.RemoveAt(i);
                    offroadVisuals.Add(visual);
                }

                if (0 == offroadDashes.Count) offroadHitTrees.Clear();
            }
        }
    }

    /// <summary>필드 끝 한 점에 소환되어 반대편 끝까지 정면으로 돌진한다(기획서 "필드 끝 면 타일 위에 랜덤하게 플래시").</summary>
    private void StartOffroad(float _now)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        if (dir.sqrMagnitude < 0.01f) dir = Vector2.right;

        // 섬 타원 위의 한 점에서 중심을 지나 반대편 점까지.
        Vector3 edge = new Vector3(dir.x * fieldRadiusX, dir.y * fieldRadiusY, 0f);
        Vector3 from = fieldCenter + edge;
        Vector3 to = fieldCenter - edge;
        float duration = Vector3.Distance(from, to) / Mathf.Max(0.1f, tuning.offroadSpeed);

        OffroadDash dash = new OffroadDash
        {
            from = from,
            to = to,
            startTime = _now,
            dashStartTime = _now + tuning.offroadWarmup,
            dashDuration = duration,
            lastPosition = from,
            damage = tuning.offroadDamage * DamageMul(ERoguelikeSkill.Offroad),
            radius = tuning.offroadHitRadius * RangeMul(ERoguelikeSkill.Offroad),
        };

        offroadDashes.Add(dash);

        if (null != offroadVisualPrefab)
        {
            int visualIndex = offroadDashes.Count - 1;
            while (offroadVisuals.Count <= visualIndex) offroadVisuals.Add(Instantiate(offroadVisualPrefab, transform));
            offroadVisuals[visualIndex].transform.position = from;
            offroadVisuals[visualIndex].SetActive(true);
        }

        Publish(ERoguelikeSkillVfx.OffroadWarmup, from, to, dash.radius, tuning.offroadWarmup, 0);
        Publish(ERoguelikeSkillVfx.OffroadDash, from, to, dash.radius, duration, 0);
    }

    // // 조합: 스텔라 + 드론

    private void UpdateStellaDrone(float _now)
    {
        if (false == modifiers.IsComboActive(ERoguelikeCombo.StellaDrone))
        {
            if (starLinks.Count > 0) starLinks.Clear();
            return;
        }

        // 조합을 산 순간 이미 떠 있던 별도 더 이상 공격받지 않는다.
        for (int i = 0; i < activeStars.Count; i++)
        {
            if (activeStars[i].bCanApplyDamage) activeStars[i].SetAttackable(false);
        }

        if (activeStars.Count >= 2 && 0 == starLinks.Count) RebuildStarLinks();

        // 별이 노바 수만큼 모이면 노바. 모든 별이 사라지고 처음부터 다시 모은다.
        if (activeStars.Count >= tuning.stellaDroneNovaStars)
        {
            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < activeStars.Count; i++) centroid += activeStars[i].transform.position;
            centroid /= activeStars.Count;

            float radius = tuning.stellaDroneNovaRadius * RangeMul(ERoguelikeSkill.Stella);
            DamageTreesInRadius(centroid, radius, tuning.stellaDroneNovaDamage * DamageMul(ERoguelikeSkill.Stella), false, false);
            Publish(ERoguelikeSkillVfx.StarNova, centroid, centroid, radius, 0f, 0);

            for (int i = activeStars.Count - 1; i >= 0; i--) DespawnStar(i, false);
            RebuildStarLinks();
            return;
        }

        // 홀로 남은 별: 공격받지도 사라지지도 않고 주변에 표식 공격을 계속한다.
        if (1 == activeStars.Count && _now >= nextLonePulseTime)
        {
            nextLonePulseTime = _now + Interval(tuning.stellaDroneLoneStarInterval, ERoguelikeSkill.Drone);
            Vector3 pos = activeStars[0].transform.position;
            float radius = tuning.stellaDroneLoneStarRadius * RangeMul(ERoguelikeSkill.Stella);
            DamageTreesInRadius(pos, radius, tuning.stellaBlastDamage * DamageMul(ERoguelikeSkill.Stella), true, false);
            Publish(ERoguelikeSkillVfx.LoneStarPulse, pos, pos, radius, 0f, activeStars[0].StarId);
        }

        // 이음선 빔.
        if (starLinks.Count > 0 && _now >= nextBeamTime)
        {
            nextBeamTime = _now + Interval(tuning.stellaDroneBeamInterval, ERoguelikeSkill.Drone);
            float width = tuning.stellaDroneBeamWidth * RangeMul(ERoguelikeSkill.Drone);
            float damage = tuning.stellaDroneBeamDamage * DamageMul(ERoguelikeSkill.Drone);

            // 이번 빔은 쏘기 직전의 이음선으로 끝까지 쏜다. 도중에 생긴 별은 다음 빔부터 이어진다.
            // (별은 이 사이 늘기만 하므로 사본의 인덱스는 activeStars에서 그대로 유효하다)
            beamLinkBuffer.Clear();
            beamLinkBuffer.AddRange(starLinks);

            for (int i = 0; i < beamLinkBuffer.Count; i++)
            {
                Vector3 a = activeStars[beamLinkBuffer[i].x].transform.position;
                Vector3 b = activeStars[beamLinkBuffer[i].y].transform.position;
                DamageTreesAlongSegment(a, b, width, damage, null);
                Publish(ERoguelikeSkillVfx.StarLinkBeam, a, b, width, 0f, 0);
            }
        }
    }

    /// <summary>현재 별들로 최소 신장 트리를 만든다(프림). 별은 많아야 노바 수(9) 근처라 매번 다시 계산해도 싸다.</summary>
    private void RebuildStarLinks()
    {
        starLinks.Clear();

        int n = activeStars.Count;
        if (n >= 2)
        {
            mstInTree.Clear();
            mstBestDist.Clear();
            mstBestFrom.Clear();
            for (int i = 0; i < n; i++)
            {
                mstInTree.Add(false);
                mstBestDist.Add(float.MaxValue);
                mstBestFrom.Add(-1);
            }

            mstBestDist[0] = 0f;
            for (int step = 0; step < n; step++)
            {
                int u = -1;
                for (int i = 0; i < n; i++)
                {
                    if (mstInTree[i]) continue;
                    if (u < 0 || mstBestDist[i] < mstBestDist[u]) u = i;
                }

                mstInTree[u] = true;
                if (mstBestFrom[u] >= 0) starLinks.Add(new Vector2Int(mstBestFrom[u], u));

                Vector3 pu = activeStars[u].transform.position;
                for (int v = 0; v < n; v++)
                {
                    if (mstInTree[v]) continue;
                    float d = (activeStars[v].transform.position - pu).sqrMagnitude;
                    if (d < mstBestDist[v])
                    {
                        mstBestDist[v] = d;
                        mstBestFrom[v] = u;
                    }
                }
            }
        }

        Publish(ERoguelikeSkillVfx.StarLinksChanged, fieldCenter, fieldCenter, 0f, 0f, starLinks.Count);
    }

    // // 피해 공통

    private void DamageTreesInRadius(Vector3 _center, float _radius, float _damage, bool _bMark, bool _bDot, bool _bBatch = true)
    {
        if (null == CollisionSystem.Instance || _radius <= 0f) return;

        CollisionSystem.Instance.GetCollidablesInRadius(_center, _radius, treeLayer.value, scanBuffer);

        hitBuffer.Clear();
        float radiusSq = _radius * _radius;
        for (int i = 0; i < scanBuffer.Count; i++)
        {
            if (false == (scanBuffer[i] is TreeObj tree) || false == IsHittable(tree)) continue;
            if (((Vector2)_center - tree.Position).sqrMagnitude > radiusSq) continue;
            hitBuffer.Add(tree);
        }

        if (_bBatch) TreeTileWriteBatch.Begin();
        try
        {
            for (int i = 0; i < hitBuffer.Count; i++)
            {
                TreeObj tree = hitBuffer[i];
                if (false == IsHittable(tree)) continue;

                // 표식을 먼저 남겨 이번 피해부터 표식 배율이 적용되게 한다.
                if (_bMark) MarkTree(tree, _bDot);
                tree.TakeDamage(_damage, false, false);
            }
        }
        finally
        {
            if (_bBatch) TreeTileWriteBatch.End();
        }
    }

    private void DamageTreesAlongSegment(Vector3 _from, Vector3 _to, float _width, float _damage, HashSet<TreeObj> _alreadyHit)
    {
        if (null == CollisionSystem.Instance) return;

        Vector2 a = _from;
        Vector2 b = _to;
        Vector2 center = (a + b) * 0.5f;
        float scanRadius = Vector2.Distance(a, b) * 0.5f + _width;

        CollisionSystem.Instance.GetCollidablesInRadius(center, scanRadius, treeLayer.value, scanBuffer);

        hitBuffer.Clear();
        float widthSq = _width * _width;
        for (int i = 0; i < scanBuffer.Count; i++)
        {
            if (false == (scanBuffer[i] is TreeObj tree) || false == IsHittable(tree)) continue;
            if (null != _alreadyHit && _alreadyHit.Contains(tree)) continue;
            if (DistancePointToSegmentSq(tree.Position, a, b) > widthSq) continue;
            hitBuffer.Add(tree);
        }

        TreeTileWriteBatch.Begin();
        try
        {
            for (int i = 0; i < hitBuffer.Count; i++)
            {
                TreeObj tree = hitBuffer[i];
                if (false == IsHittable(tree)) continue;

                _alreadyHit?.Add(tree);
                tree.TakeDamage(_damage, false, false);
            }
        }
        finally
        {
            TreeTileWriteBatch.End();
        }
    }

    private static bool IsHittable(TreeObj _tree)
    {
        return null != _tree && false == _tree.bDead && false == _tree.IsPooled && _tree.bCanApplyDamage;
    }

    private static float DistancePointToSegmentSq(Vector2 _p, Vector2 _a, Vector2 _b)
    {
        Vector2 ab = _b - _a;
        float lenSq = ab.sqrMagnitude;
        if (lenSq < 0.0001f) return (_p - _a).sqrMagnitude;

        float t = Mathf.Clamp01(Vector2.Dot(_p - _a, ab) / lenSq);
        return (_p - (_a + ab * t)).sqrMagnitude;
    }

    // // 수치

    private float DamageMul(ERoguelikeSkill _skill) => Mathf.Max(0f, 1f + modifiers.GetForSkill(ERunEffect.DamagePercent, _skill) / 100f);
    private float RangeMul(ERoguelikeSkill _skill) => Mathf.Max(0.1f, 1f + modifiers.GetForSkill(ERunEffect.RangePercent, _skill) / 100f);
    private float DurationMul(ERoguelikeSkill _skill) => Mathf.Max(0.1f, 1f + modifiers.GetForSkill(ERunEffect.DurationPercent, _skill) / 100f);
    private float DotMul(ERoguelikeSkill _skill) => DamageMul(_skill) * Mathf.Max(0f, 1f + modifiers.GetForSkill(ERunEffect.DotDamagePercent, _skill) / 100f);
    private int CountAdd(ERoguelikeSkill _skill) => Mathf.Max(0, Mathf.RoundToInt(modifiers.GetForSkill(ERunEffect.CountAdd, _skill)));

    private float Interval(float _baseInterval, ERoguelikeSkill _skill)
    {
        float speed = Mathf.Max(0.1f, 1f + modifiers.GetForSkill(ERunEffect.AttackSpeedPercent, _skill) / 100f);
        float reduction = Mathf.Clamp(modifiers.GetForSkill(ERunEffect.CooldownReductionPercent, _skill), 0f, 90f) / 100f;
        return Mathf.Max(0.05f, _baseInterval / speed * (1f - reduction));
    }

    private void Publish(ERoguelikeSkillVfx _type, Vector3 _position, Vector3 _position2, float _radius, float _duration, int _id)
    {
        signalHub?.Publish(new RoguelikeSkillVfxSignal(_type, _position, _position2, _radius, _duration, _id));
    }

    private static int GetFirstLayer(LayerMask _mask)
    {
        int value = _mask.value;
        for (int i = 0; i < 32; i++)
        {
            if (0 != (value & (1 << i))) return i;
        }

        return 0;
    }

    private void OnDestroy()
    {
        RunCombatHooks.AxeSwungEvent -= axeSwungHandler;
        RunCombatHooks.BoomerangHitTreeEvent -= boomerangHitHandler;
    }
}
