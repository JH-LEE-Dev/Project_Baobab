using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>본편 연출을 그대로 쓰지 못하는(코드에서 꺼낼 수 없는) 에셋만 따로 꽂는다.</summary>
[Serializable]
public sealed class RoguelikeSkillVfxAssets
{
    [Tooltip("본편 회전베기 프레임(AttackComponent.whirlwindFrames와 같은 것). 회전베기+부메랑 타격에 쓴다.")]
    public Sprite[] whirlwindFrames;
    [Tooltip("포자 수류탄이 날아가는 동안의 그림(기획서 v2.5: Booty Items_16x의 초록 포자 주머니, 두 번째 칸).")]
    public Sprite sporeBombSprite;
    [Tooltip("포자 수류탄이 날아가는 시간(초)과 포물선 높이. 수류탄은 떨어지는 순간 대폭발을 일으킨다(판정도 이 시간 뒤다).")]
    public float sporeBombThrowSeconds = 0.45f;
    public float sporeBombArcHeight = 1.6f;
    [Tooltip("날아가는 동안 빙글빙글 도는 속도(도/초).")]
    public float sporeBombSpinSpeed = 720f;
    [Tooltip("날아가는 동안의 스쿼시/스트레치 세기. 오르내릴 때(빠를 때) 세로로 늘어나고 꼭대기에서 원래 모양으로 돌아온다.")]
    [Range(0f, 1f)] public float sporeBombFlightStretch = 0.3f;
    [Tooltip("착지 뒤 찌그러졌다 출렁이다 부풀며 터질 때까지의 시간(초). 대폭발 연출은 착지 순간 함께 시작된다.")]
    public float sporeBombLandSeconds = 0.4f;
    [Tooltip("착지 순간 찌그러지는 세기(세로가 1 - 이 값까지 납작해진다).")]
    [Range(0f, 0.9f)] public float sporeBombLandSquash = 0.45f;
    [Tooltip("착지 뒤 출렁임의 초당 진동 수와 감쇠(클수록 빨리 잦아든다).")]
    public float sporeBombWobbleFrequency = 9f;
    public float sporeBombWobbleDamping = 7f;
    [Tooltip("터지기 직전 부풀어 오르는 최대 배율.")]
    public float sporeBombPopScale = 1.35f;
    [Tooltip("본편 드론 레이저(VFX_DroneLaser.prefab). 스텔라+드론의 이음선 빔 - 기획서 \"드론 레이저가 이음선 위로 발사되는 형태\".")]
    public PresentationLayer.VFX.VFX_DroneLaser droneLaserPrefab;
}

/// <summary>
/// 로그라이크 신규 스킬·조합의 연출을 본편 연출로 재생한다(기획서 3-3 "본편 ~을 그대로 사용한다/활용한다").
///   스텔라 별: 본편 3스테이지 별 표식 나무에서 나오는 땅 별(TreeStarMark_Ground) + 등장음 / 사라질 때 본편 회전 소멸.
///              별마다 로그라이크 전용 폭발 범위 링(VFX_StellaRange)과 유지시간 게이지(VFX_StellaDurationGauge)를 붙인다.
///   별 공격: 로그라이크 전용 스텔라 폭발(VFX_StellaBurst) + 본편 별 폭발음.
///   오프로드 소환 위치: 로그라이크 전용 이동 경로 표시(VFX_OffroadLane, 발동 주기 내내 - BeginOffroadLane).
///   포자 폭발: 본편 포자 폭발(4연발 + 포자 폭발음). v2.6부터 포자 조합 3종도 대폭발을 써서 지금은 내는 곳이 없다.
///   포자 폭탄: 포물선으로 던지고, 떨어지면 본편 포자 대폭발(2스테이지 전리품 연출). 오염된 나무에는 본편 포자 디버프 표시.
///              포자 조합 3종(포자 레이저 · 방구차 · 포자 충격파)의 대폭발도 같은 연출이다(던지기 없이 그 자리에서).
///   별 이음선 빔·노바(스텔라+드론): 본편 드론 레이저(+ 드론 발사음)·별자리 노바 마법진, 이음선은 본편 별자리 점선.
///   회전베기+부메랑: 본편 회전베기 VFX + 카메라 흔들림.
/// 판정·피해는 RoguelikeSkillController가 이미 끝낸 뒤라 여기서는 그리기만 한다. 이펙트 담당은 같은 지점의
/// RoguelikeSkillVfxSignal로 덧붙이거나 바꾼다.
/// </summary>
public sealed class RoguelikeSkillVfxPlayer
{
    // 본편 별자리 그룹 번호(0부터 증가)와 겹치지 않는 구간.
    private const int StarGroupBase = -100000;
    private const int LinkGroupBase = -200000;
    private const int SporeBursts = 4;
    private const float SporeBurstInterval = 0.15f;
    private const float SporeBurstSpread = 0.25f;
    private const float SporeBigExplosionShakeInterval = 0.2f;

    private InDungeonVFXManager vfx;
    private ConstellationPixelLaserCreator laserCreator;
    private RoguelikeSkillVfxAssets assets;
    private MonoBehaviour host;
    private readonly List<Vector3> linkPoints = new List<Vector3>(2) { Vector3.zero, Vector3.zero };
    private int registeredLinkCount;
    private readonly List<GameObject> projectilePool = new List<GameObject>(4);
    // 별 표식이 찍힌 나무 -> 그 나무를 감싸고 도는 별 이펙트
    private readonly Dictionary<TreeObj, PresentationLayer.VFX.VFX_BrandStarWrap> markWraps = new Dictionary<TreeObj, PresentationLayer.VFX.VFX_BrandStarWrap>(32);

    // 드론 레이저 한 번이 다 그려지는 시간(본편 VFX_DroneLaser 0.05 + 0.10 + 0.15초)보다 조금 길게. 그 안이면 쓰는 중으로 본다.
    private const float DroneLaserBusySeconds = 0.35f;
    private readonly List<PresentationLayer.VFX.VFX_DroneLaser> droneLasers = new List<PresentationLayer.VFX.VFX_DroneLaser>(8);
    private readonly List<float> droneLaserLastPlay = new List<float>(8);
    private int lastBeamSoundFrame = -1;
    // 회전베기+부메랑: 같은 프레임에 여러 그루를 맞혀 회전베기가 여러 번 나가도 그림·흔들림은 한 번만(겹치면 같은 그림이다).
    private int lastWhirlwindFrame = -1;
    private float lastSporeShakeTime = float.NegativeInfinity;
    // 포자 연출 회차. StopCombatEffects가 올리면 진행 중이던 포자 폭발·폭탄 연출이 다음 단계에서 멈춘다.
    private int sporeGeneration;

    // 별마다 붙는 폭발 범위 링·유지시간 게이지. 별 번호로 찾아 별이 터지거나 사라질 때 End()로 거둔다.
    private struct StarOverlay
    {
        public int starId;
        public PresentationLayer.VFX.VFX_StellaRange range;
        public PresentationLayer.VFX.VFX_StellaDurationGauge gauge;
    }
    private readonly List<StarOverlay> starOverlays = new List<StarOverlay>(12);

    /// <summary>본편 땅 별을 쓸 수 있어 로그라이크 별 자체 그림을 숨겨도 되는지.</summary>
    public bool bUsesMainStarVisual => null != vfx;

    // 땅 별 그림 중심이 별 위치(판정·스폰 지점)에서 떠 있는 거리. 이음선·빔·별 폭발은 그림 중심끼리 잇는다.
    private Vector3 starCenterOffset;

    /// <summary>땅 별 그림 중심이 별 바닥 위치에서 떠 있는 거리(본편 땅 별을 못 쓰면 0).</summary>
    public Vector3 StarCenterOffset => starCenterOffset;

    public void Initialize(InDungeonObjectManager _objectManager, RoguelikeSkillVfxAssets _assets, MonoBehaviour _host)
    {
        assets = _assets ?? new RoguelikeSkillVfxAssets();
        host = _host;
        vfx = null != _objectManager ? _objectManager.GetComponentInChildren<InDungeonVFXManager>(true) : null;
        laserCreator = null != _objectManager ? _objectManager.GetComponentInChildren<ConstellationPixelLaserCreator>(true) : null;
        starCenterOffset = null != vfx ? vfx.ConstellationGroundMarkVisualOffset : Vector3.zero;
    }

    public void Play(in RoguelikeSkillVfxSignal _signal, IReadOnlyList<RoguelikeStar> _stars, IReadOnlyList<Vector2Int> _links)
    {
        switch (_signal.type)
        {
            case ERoguelikeSkillVfx.StarSpawned:
                vfx?.PlayConstellationGroundMarkVFX(_signal.position, Order(_signal.position) + 20, StarGroupBase - _signal.id);
                Sound.Play(SoundID.Starappear, _signal.position);
                PlayStarOverlay(_signal.id, _signal.position, _signal.radius, _signal.duration);
                break;

            case ERoguelikeSkillVfx.StarBlast:
                vfx?.SetConstellationGroundMarkHighlightForRun(StarGroupBase - _signal.id, false);
                vfx?.BeginConstellationGroundMarkManifest(StarGroupBase - _signal.id);
                // 범위 링을 밝게 강조한 뒤 폭발을 내고, 링·게이지를 거둔다. 폭발은 별의 바닥 위치에 놓는다(별 높이는 이펙트가 맡는다).
                ActivateStarRange(_signal.id);
                vfx?.PlayStellaBurstVFX(_signal.position, _signal.radius, Order(_signal.position) + 40);
                EndStarOverlay(_signal.id);
                Sound.Play(SoundID.StarExplosion, _signal.position + starCenterOffset);
                CameraMoveController.Instance?.ShakeCamera(3f, 0.2f);
                break;

            case ERoguelikeSkillVfx.StarBlastRepeat:
                // 같은 자리의 뒤이은 폭발. 땅 별·범위 링·게이지는 첫 폭발에서 이미 거뒀으므로 폭발 그림·소리·흔들림만 낸다.
                vfx?.PlayStellaBurstVFX(_signal.position, _signal.radius, Order(_signal.position) + 40);
                Sound.Play(SoundID.StarExplosion, _signal.position + starCenterOffset);
                CameraMoveController.Instance?.ShakeCamera(3f, 0.2f);
                break;

            case ERoguelikeSkillVfx.StarHighlightOn:
            case ERoguelikeSkillVfx.StarHighlightOff:
                // 도끼 범위에 들어온 별은 나무 아웃라인처럼 강조한다(본편 땅 별이 더 밝게 번진다).
                vfx?.SetConstellationGroundMarkHighlightForRun(StarGroupBase - _signal.id, ERoguelikeSkillVfx.StarHighlightOn == _signal.type);
                break;

            case ERoguelikeSkillVfx.StarExpired:
                vfx?.SetConstellationGroundMarkHighlightForRun(StarGroupBase - _signal.id, false);
                vfx?.BeginConstellationGroundMarkManifest(StarGroupBase - _signal.id);
                EndStarOverlay(_signal.id);
                break;

            case ERoguelikeSkillVfx.SporeExplosion:
                if (null != host && host.isActiveAndEnabled) host.StartCoroutine(SporeBurstRoutine(_signal.position, sporeGeneration));
                break;

            case ERoguelikeSkillVfx.SporeBombThrown:
                if (null != host && host.isActiveAndEnabled) host.StartCoroutine(SporeBombRoutine(_signal.position2, _signal.position, _signal.duration, sporeGeneration));
                break;

            case ERoguelikeSkillVfx.SporeBigExplosion:
                // 본편 2스테이지 전리품 "포자 대폭발" 연출(응집 → 폭발). 범위는 판정과 같은 값을 넘겨 보이는 범위를 맞춘다.
                vfx?.PlaySporeBigExplosionVFX(_signal.position, _signal.radius, Mathf.Clamp(_signal.id, 1, 3));
                Sound.Play(SoundID.SporeExplosion, _signal.position);
                // 포자 조합 3종은 맞힌 나무마다 대폭발이 일어나 연달아 터진다. 흔들림은 최소 간격을 두어 화면이 계속 떨리지 않게 한다.
                if (Time.time - lastSporeShakeTime >= SporeBigExplosionShakeInterval)
                {
                    lastSporeShakeTime = Time.time;
                    CameraMoveController.Instance?.ShakeCamera(2f, 0.15f);
                }
                break;

            case ERoguelikeSkillVfx.StarLinkBeam:
                FireLaser(_signal.position + starCenterOffset, _signal.position2 + starCenterOffset);
                break;

            // DroneStarShot: 레이저 그림은 본편 드론 연쇄 번개가 총구 → 별 그림 중심 → 체인 나무로 이미 그린다(Character.OnDronePriorityShot).

            case ERoguelikeSkillVfx.StarNova:
                vfx?.PlayConstellationNovaVFX(_signal.position, _signal.radius);
                Sound.Play(SoundID.StarExplosion, _signal.position);
                CameraMoveController.Instance?.ShakeCamera(6f, 0.35f);
                break;

            case ERoguelikeSkillVfx.StarLinksChanged:
                RebuildLinkLines(_stars, _links);
                break;

            case ERoguelikeSkillVfx.SpinBoomerangStrike:
                if (lastWhirlwindFrame == Time.frameCount) break;
                lastWhirlwindFrame = Time.frameCount;
                if (null != assets.whirlwindFrames && assets.whirlwindFrames.Length > 0) WhirlwindVFX.Spawn(_signal.position, _signal.radius, assets.whirlwindFrames);
                CameraMoveController.Instance?.ShakeCamera(2f, 0.15f);
                break;
        }
    }

    /// <summary>
    /// 남아 있는 별(스텔라+드론 - 노바 때까지 웨이브를 넘어 유지된다)의 땅 별과 이음선 점선을 다시 그린다. 본편 나무 정리
    /// (InDungeonObjectManager.ClearTrees)가 땅 별·점선을 일괄 회수하므로 그 뒤에 부른다.
    /// </summary>
    public void RestoreStars(IReadOnlyList<RoguelikeStar> _stars, IReadOnlyList<Vector2Int> _links)
    {
        if (null == vfx || null == _stars) return;

        // 남는 별은 스텔라+드론의 별뿐이고 여기에는 범위 링·게이지를 붙이지 않으므로 다시 그릴 것이 없다.
        // 본편 정리(StopAllPooledVFX)가 풀로 거둔 항목이 남아 있으면 비운다.
        EndAllStarOverlays();

        for (int i = 0; i < _stars.Count; i++)
        {
            Vector3 pos = _stars[i].transform.position;
            // 등장 연출 없이 내려앉은 모습으로 다시 놓는다(새로 생기는 별처럼 튀어나오지 않게).
            vfx.PlayConstellationGroundMarkLandedForRun(pos, Order(pos) + 20, StarGroupBase - _stars[i].StarId);
        }

        // 본편 정리가 점선을 이미 거뒀다. 지운 적 없는 번호를 다시 지우지 않게 등록 수를 비우고 새로 깐다.
        registeredLinkCount = 0;
        RebuildLinkLines(_stars, _links);
    }

    /// <summary>
    /// 전투가 끝났다(웨이브·런 종료). 웨이브와 함께 끝나는 연출(포자 폭발·폭탄)을 멈춘다. 별은 건드리지 않는다
    /// (스텔라+드론의 별은 웨이브를 넘어 남는다 - Clear는 따로 부른다).
    /// </summary>
    public void StopCombatEffects()
    {
        sporeGeneration++;
        for (int i = 0; i < projectilePool.Count; i++)
        {
            if (null != projectilePool[i]) projectilePool[i].SetActive(false);
        }
    }

    /// <summary>
    /// 포자폭탄으로 오염된 나무 표시. 본편 포자 대폭발에 맞은 나무의 디버프 표시(TreeSporeDebuff)를 그대로 쓰고,
    /// 중첩 수(1~3)를 표시 단계로 쓴다.
    /// </summary>
    public void SetContaminated(TreeObj _tree, int _stacks)
    {
        if (null == _tree) return;
        PresentationLayer.VFX.TreeSporeDebuffVfx.Apply(_tree, ContaminationDisplayAmplify(_stacks));
    }

    /// <summary>
    /// 오염이 풀렸다. _bRestoreMain이면 본편 전리품 포자 대폭발의 피해 증폭(로그라이크에도 적용된다)이 남아 있는 나무는 그 표시로 되돌린다.
    /// 전투가 끝나 나무가 곧 정리될 때는 지우기만 한다(지우기는 관리자를 새로 만들지 않는다).
    /// </summary>
    public void ClearContaminated(TreeObj _tree, bool _bRestoreMain)
    {
        if (null == _tree) return;

        // 표시는 더 큰 단계로만 올라가므로 지운 뒤 다시 건다.
        PresentationLayer.VFX.TreeSporeDebuffVfx.Clear(_tree);
        if (_bRestoreMain && _tree.SporeDamageAmplify > 0f) PresentationLayer.VFX.TreeSporeDebuffVfx.Apply(_tree, _tree.SporeDamageAmplify);
    }

    /// <summary>
    /// 오프로드 소환 위치 · 진행 방향 표시(VFX_OffroadLane)를 시작한다(기획서 v2.8: 발동 주기 시작 ~ 발동). _halfWidth는 돌진 폭의 반폭이다.
    /// 돌려받은 표시는 컨트롤러가 들고 있다가 발동 때 Fire, 웨이브 종료 때 Cancel을 부른다. 이펙트가 없으면 null.
    /// </summary>
    public PresentationLayer.VFX.VFX_OffroadLane BeginOffroadLane(Vector3 _from, Vector3 _to, float _halfWidth)
    {
        return null != vfx ? vfx.PlayOffroadLaneVFX(_from, _to, _halfWidth) : null;
    }

    /// <summary>
    /// 별 표식이 찍힌 나무에 본편 별 감싸기(VFX_BrandStarWrap, 타원 궤도를 빙글빙글 도는 별)를 시작한다.
    /// 이미 같은 나무에 감싸기가 돌고 있으면 아무 일도 하지 않는다. 풀/프리팹이 없으면 조용히 건너뛴다.
    /// </summary>
    public void BeginMarkWrap(TreeObj _tree)
    {
        if (null == vfx || null == _tree || null == _tree.treeVisualComponent) return;

        PresentationLayer.VFX.VFX_BrandStarWrap existing;
        if (true == markWraps.TryGetValue(_tree, out existing))
        {
            // 던전 정리 등으로 이미 회수된 낡은 항목이면(감싸는 나무가 비어 있음) 버리고 새로 시작한다
            if (null != existing && _tree.treeVisualComponent == existing.Visual) return;
            markWraps.Remove(_tree);
        }

        PresentationLayer.VFX.VFX_BrandStarWrap wrap = vfx.BeginBrandStarWrap(_tree.treeVisualComponent);
        if (null != wrap) markWraps[_tree] = wrap;
    }

    /// <summary>
    /// 표식이 풀렸다. _bImmediate가 거짓이면 별들이 궤도 안쪽으로 모이며 사라지는 퇴장 연출 뒤 스스로 풀에 돌아가고,
    /// 참이면(나무가 죽거나 풀로 돌아가는 중) 연출 없이 바로 회수한다.
    /// </summary>
    public void EndMarkWrap(TreeObj _tree, bool _bImmediate)
    {
        if (null == _tree) return;

        PresentationLayer.VFX.VFX_BrandStarWrap wrap;
        if (false == markWraps.TryGetValue(_tree, out wrap)) return;

        markWraps.Remove(_tree);
        if (null == wrap) return;

        if (true == _bImmediate) wrap.ForceRelease();
        else wrap.End();
    }

    /// <summary>웨이브/런 종료. 표식 별 감싸기를 연출 없이 전부 회수한다.</summary>
    public void ClearMarkWraps()
    {
        foreach (KeyValuePair<TreeObj, PresentationLayer.VFX.VFX_BrandStarWrap> pair in markWraps)
        {
            if (null != pair.Value) pair.Value.ForceRelease();
        }

        markWraps.Clear();
    }

    /// <summary>중첩 수를 디버프 표시 단계(1~3)로 바꾸는 증폭 값. 단계 경계는 TreeSporeDebuffVfx.TierFromAmplify를 따른다.</summary>
    private static float ContaminationDisplayAmplify(int _stacks)
    {
        if (_stacks >= 3) return PresentationLayer.VFX.TreeSporeDebuffVfx.Tier3Threshold;
        if (_stacks >= 2) return PresentationLayer.VFX.TreeSporeDebuffVfx.Tier2Threshold;
        return PresentationLayer.VFX.TreeSporeDebuffVfx.Tier2Threshold * 0.5f;
    }

    /// <summary>웨이브가 끝나 별·이음선이 한꺼번에 치워질 때. 남은 땅 별과 점선, 범위 링·게이지를 거둔다.</summary>
    public void Clear()
    {
        if (null == vfx) return;

        vfx.ClearAllConstellationGroundMarks();
        RemoveLinkLines();
        EndAllStarOverlays();
    }

    // // 내부

    private static int Order(Vector3 _position) => -Mathf.RoundToInt(_position.y * 100f);

    /// <summary>
    /// 새 별에 폭발 범위 링(_radius)과 유지시간 게이지(_lifetime초)를 붙인다. 0 이하인 쪽은 붙이지 않는다
    /// (스텔라+드론의 별은 쳐도 터지지 않고 사라지지도 않아 둘 다 0으로 온다).
    /// </summary>
    private void PlayStarOverlay(int _starId, Vector3 _position, float _radius, float _lifetime)
    {
        if (null == vfx) return;

        StarOverlay overlay = new StarOverlay { starId = _starId };

        if (_radius > 0f)
        {
            ParticleSystem effect = vfx.PlayStellaRangeVFX(_position, _radius);
            overlay.range = null != effect ? effect.GetComponentInChildren<PresentationLayer.VFX.VFX_StellaRange>(true) : null;
        }

        if (_lifetime > 0f)
        {
            // 땅 별(+20)·폭발(+40)보다 위에 그린다.
            ParticleSystem effect = vfx.PlayStellaGaugeVFX(_position, _lifetime, Order(_position) + 50);
            overlay.gauge = null != effect ? effect.GetComponentInChildren<PresentationLayer.VFX.VFX_StellaDurationGauge>(true) : null;
        }

        if (null == overlay.range && null == overlay.gauge) return;

        ForgetReusedOverlay(overlay.range, overlay.gauge);
        starOverlays.Add(overlay);
    }

    /// <summary>
    /// 방금 꺼낸 인스턴스를 아직 들고 있는 다른 별 항목에서 뺀다. 게이지는 수명이 다하면 스스로, 링은 루트 수명(60초)이 끝나면
    /// 풀로 돌아가므로 그 사이 다른 별이 같은 인스턴스를 꺼낼 수 있다 - 옛 별의 End()가 새 별의 그림을 끄지 않게 한다.
    /// </summary>
    private void ForgetReusedOverlay(PresentationLayer.VFX.VFX_StellaRange _range, PresentationLayer.VFX.VFX_StellaDurationGauge _gauge)
    {
        for (int i = starOverlays.Count - 1; i >= 0; i--)
        {
            StarOverlay overlay = starOverlays[i];
            if (null != _range && overlay.range == _range) overlay.range = null;
            if (null != _gauge && overlay.gauge == _gauge) overlay.gauge = null;

            if (null == overlay.range && null == overlay.gauge) starOverlays.RemoveAt(i);
            else starOverlays[i] = overlay;
        }
    }

    private int FindStarOverlay(int _starId)
    {
        for (int i = 0; i < starOverlays.Count; i++)
        {
            if (starOverlays[i].starId == _starId) return i;
        }

        return -1;
    }

    /// <summary>별이 터지기 직전 범위 링을 밝게 강조한다(VFX_StellaBurst 재생 직전).</summary>
    private void ActivateStarRange(int _starId)
    {
        int index = FindStarOverlay(_starId);
        if (index < 0) return;

        PresentationLayer.VFX.VFX_StellaRange range = starOverlays[index].range;
        if (null != range && range.isActiveAndEnabled) range.Activate();
    }

    /// <summary>별이 터지거나 사라졌다. 링은 깜빡이며, 게이지는 퇴장 연출 뒤 스스로 풀로 돌아간다.</summary>
    private void EndStarOverlay(int _starId)
    {
        int index = FindStarOverlay(_starId);
        if (index < 0) return;

        EndOverlay(starOverlays[index]);
        starOverlays.RemoveAt(index);
    }

    private void EndAllStarOverlays()
    {
        for (int i = 0; i < starOverlays.Count; i++) EndOverlay(starOverlays[i]);
        starOverlays.Clear();
    }

    // 이미 풀로 돌아간(꺼진) 인스턴스는 건드리지 않는다.
    private static void EndOverlay(StarOverlay _overlay)
    {
        if (null != _overlay.range && _overlay.range.isActiveAndEnabled) _overlay.range.End();
        if (null != _overlay.gauge && _overlay.gauge.isActiveAndEnabled) _overlay.gauge.End();
    }

    /// <summary>
    /// 이음선 하나에 본편 드론 레이저를 쏜다. 레이저 한 개는 다시 쏘면 처음부터 다시 터지므로(본편 Drone.chainZap) 이음선마다
    /// 따로 둔다. 발사음은 본편 드론 발사음(발사·타격·전기 세 겹)을 한 번의 발사(같은 프레임의 이음선 전부)에 한 번만 낸다.
    /// </summary>
    private void FireLaser(Vector3 _from, Vector3 _to)
    {
        PresentationLayer.VFX.VFX_DroneLaser laser = GetDroneLaser();
        if (null != laser)
        {
            laser.PlayZap(_from, _to);
        }
        else
        {
            // 드론 레이저 에셋이 없으면 본편 별자리 레이저로 대신 그린다.
            PresentationLayer.VFX.ConstellationPixelLaser fallback = null != laserCreator ? laserCreator.Get() : null;
            if (null != fallback) fallback.PlayLaser(_from, _to);
        }

        if (lastBeamSoundFrame == Time.frameCount) return;
        lastBeamSoundFrame = Time.frameCount;

        Vector3 mid = (_from + _to) * 0.5f;
        Sound.Play(SoundID.SFXBeamFire, mid);
        Sound.Play(SoundID.SFXPunchImpact, mid);
        Sound.Play(SoundID.SFXVoltageImpact, mid);
    }

    private PresentationLayer.VFX.VFX_DroneLaser GetDroneLaser()
    {
        if (null == assets.droneLaserPrefab || null == host) return null;

        float now = Time.time;
        for (int i = 0; i < droneLasers.Count; i++)
        {
            if (null == droneLasers[i] || now - droneLaserLastPlay[i] < DroneLaserBusySeconds) continue;
            droneLaserLastPlay[i] = now;
            return droneLasers[i];
        }

        PresentationLayer.VFX.VFX_DroneLaser created = UnityEngine.Object.Instantiate(assets.droneLaserPrefab, host.transform);
        created.transform.localPosition = Vector3.zero;
        droneLasers.Add(created);
        droneLaserLastPlay.Add(now);
        return created;
    }

    private void RebuildLinkLines(IReadOnlyList<RoguelikeStar> _stars, IReadOnlyList<Vector2Int> _links)
    {
        if (null == vfx) return;

        RemoveLinkLines();
        if (null == _stars || null == _links || 0 == _links.Count) return;

        // 별자리 점선 전체의 표시 여부("별자리 발현" 특성)는 건드리지 않는다 - 그러면 같은 필드의 본편 별자리 점선까지
        // 켜지거나 꺼진다. 로그라이크 이음선만 늘 보이게 등록한다.
        for (int i = 0; i < _links.Count; i++)
        {
            Vector2Int link = _links[i];
            if (link.x < 0 || link.y < 0 || link.x >= _stars.Count || link.y >= _stars.Count) continue;

            linkPoints[0] = _stars[link.x].transform.position + starCenterOffset;
            linkPoints[1] = _stars[link.y].transform.position + starCenterOffset;
            vfx.RegisterConstellationDottedLineAlwaysVisibleForRun(LinkGroupBase - i, linkPoints);
            registeredLinkCount = i + 1;
        }
    }

    private void RemoveLinkLines()
    {
        if (null == vfx) return;
        for (int i = 0; i < registeredLinkCount; i++) vfx.RemoveConstellationDottedLine(LinkGroupBase - i);
        registeredLinkCount = 0;
    }

    /// <summary>본편 포자 폭발(InDungeonObjectManager.ShieldExplosionRoutine)과 같은 4연발: 좌우를 번갈아 퍼지며 폭발음이 난다.</summary>
    private IEnumerator SporeBurstRoutine(Vector3 _position, int _generation)
    {
        if (null == vfx) yield break;

        for (int i = 0; i < SporeBursts; i++)
        {
            if (_generation != sporeGeneration) yield break;

            float side = 0 == (i & 1) ? -1f : 1f;
            Vector2 offset = new Vector2(side * SporeBurstSpread, (i < 2 ? 0.5f : 0f) + UnityEngine.Random.Range(-0.1f, 0.1f));
            Vector3 pos = _position + (Vector3)offset;
            vfx.PlaySporeExplosionVFX(pos, offset);
            Sound.Play(SoundID.SporeExplosion, pos);

            yield return new WaitForSeconds(SporeBurstInterval);
        }
    }

    /// <summary>
    /// 캐릭터 쪽에서 포자 수류탄을 포물선으로 던진다(기획서 v2.5 6-1 포자 수류탄 연출).
    ///   비행: 빙글빙글 돌며 날아가고, 빠르게 오르내릴 때 세로로 늘어났다가 꼭대기에서 원래 모양으로 돌아온다(스쿼시/스트레치).
    ///   착지: 납작하게 찌그러졌다가 감쇠하며 출렁이고(젤리 같은 탄성), 마지막에 부풀어 오르며 사라진다.
    /// 찌그러짐은 부모(월드 축), 회전은 자식에 걸어 돌면서도 늘어나는 방향이 화면 위아래로 유지된다.
    /// 착지 순간의 대폭발은 SporeBigExplosion 신호가 그린다.
    /// </summary>
    private IEnumerator SporeBombRoutine(Vector3 _from, Vector3 _to, float _throwSeconds, int _generation)
    {
        GameObject projectile = GetProjectile();
        if (null == projectile) yield break;

        Transform squash = projectile.transform;
        Transform spin = 0 < squash.childCount ? squash.GetChild(0) : squash;
        SpriteRenderer renderer = projectile.GetComponentInChildren<SpriteRenderer>(true);

        float throwSeconds = Mathf.Max(0.05f, _throwSeconds);
        float stretchAmount = Mathf.Clamp01(assets.sporeBombFlightStretch);
        squash.localScale = Vector3.one;
        spin.localRotation = Quaternion.identity;
        projectile.SetActive(true);

        float elapsed = 0f;
        while (elapsed < throwSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / throwSeconds);
            Vector3 pos = Vector3.Lerp(_from, _to, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * assets.sporeBombArcHeight;
            squash.position = pos;

            // 세로 속도(포물선의 기울기)가 클수록 늘어난다. 부피가 대략 유지되게 가로는 그만큼 줄인다.
            float stretch = 1f + stretchAmount * Mathf.Abs(Mathf.Cos(t * Mathf.PI));
            squash.localScale = new Vector3(1f / Mathf.Sqrt(stretch), stretch, 1f);
            spin.Rotate(0f, 0f, -assets.sporeBombSpinSpeed * Time.deltaTime);
            if (null != renderer) renderer.sortingOrder = Order(pos) + 200;
            yield return null;

            if (_generation != sporeGeneration)
            {
                projectile.SetActive(false);
                yield break;
            }
        }

        // 착지. 회전을 바로 세우고(바닥에 철퍼덕 붙는다), 찌그러졌다 출렁이다 부풀며 사라진다.
        squash.position = _to;
        spin.localRotation = Quaternion.identity;
        if (null != renderer) renderer.sortingOrder = Order(_to) + 200;

        float landSeconds = Mathf.Max(0.05f, assets.sporeBombLandSeconds);
        float squashAmount = Mathf.Clamp(assets.sporeBombLandSquash, 0f, 0.9f);
        float omega = Mathf.Max(0f, assets.sporeBombWobbleFrequency) * 2f * Mathf.PI;
        // 마지막 이 비율 동안 부풀어 오른다.
        const float PopShare = 0.3f;

        elapsed = 0f;
        while (elapsed < landSeconds)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / landSeconds);

            // 감쇠 진동: 0초에 가장 납작(세로 1 - squash, 가로 1 + squash)하고, 출렁이며 원래 모양으로 잦아든다.
            float wobble = squashAmount * Mathf.Exp(-assets.sporeBombWobbleDamping * elapsed) * Mathf.Cos(omega * elapsed);
            float pop = t > 1f - PopShare ? Mathf.Lerp(1f, Mathf.Max(1f, assets.sporeBombPopScale), (t - (1f - PopShare)) / PopShare) : 1f;
            squash.localScale = new Vector3((1f + wobble) * pop, (1f - wobble) * pop, 1f);

            yield return null;

            if (_generation != sporeGeneration) break;
        }

        squash.localScale = Vector3.one;
        projectile.SetActive(false);
    }

    private GameObject GetProjectile()
    {
        if (null == assets.sporeBombSprite || null == host) return null;

        for (int i = 0; i < projectilePool.Count; i++)
        {
            if (null != projectilePool[i] && false == projectilePool[i].activeSelf) return projectilePool[i];
        }

        // 부모(RoguelikeSporeBomb)는 위치와 찌그러짐, 자식(Sprite)은 회전만 맡는다.
        GameObject go = new GameObject("RoguelikeSporeBomb");
        go.transform.SetParent(host.transform, false);
        GameObject sprite = new GameObject("Sprite");
        sprite.transform.SetParent(go.transform, false);
        SpriteRenderer renderer = sprite.AddComponent<SpriteRenderer>();
        renderer.sprite = assets.sporeBombSprite;
        renderer.sortingLayerName = "Objects";
        go.SetActive(false);
        projectilePool.Add(go);
        return go;
    }
}
