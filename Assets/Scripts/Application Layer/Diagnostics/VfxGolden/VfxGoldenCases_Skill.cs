// VFX 골든 캡처 케이스 묶음 (Skill). 규칙은 VfxGoldenCases.cs 머리 주석을 보세요. 에디터 전용.
#if UNITY_EDITOR

using System.Collections.Generic;
using System.Reflection;
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

namespace Diagnostics.VfxGolden
{
    public static class VfxGoldenCasesSkill
    {
        public static void Append(List<VfxGoldenCase> _cases)
        {
            // 포자 대폭발(T1은 기본 목록에 있다). 범위는 윤곽표 상한(140px = 4.375유닛) 아래 / 위로 나눈다.
            _cases.Add(new SkillSporeBigExplosionCase("T2_R1_5", 2, 1.5f, new Vector2(0.0f, 1.5f), new Vector2(10.0f, 10.0f)));
            _cases.Add(new SkillSporeBigExplosionCase("T3_R5", 3, 5.0f, new Vector2(0.0f, 3.0f), new Vector2(18.0f, 16.0f)));

            // 유성우(별빛 나침반). 지속시간은 프레임 예산 때문에 최소값(2초)으로 줄였다.
            _cases.Add(new SkillStarRainCase(1, 15));
            _cases.Add(new SkillStarRainCase(2, 20));
            _cases.Add(new SkillStarRainCase(3, 25));

            // 별자리 노바(원형) / 로그라이크 아레나 노바(마름모)
            _cases.Add(new SkillConstellationNovaCase("R4", 4.0f, new Vector2(0.0f, 2.0f), new Vector2(14.0f, 14.0f)));
            _cases.Add(new SkillConstellationNovaCase("R2_5", 2.5f, new Vector2(0.0f, 1.5f), new Vector2(10.0f, 10.0f)));
            _cases.Add(new SkillRoguelikeArenaNovaCase());

            // 별자리 레이저 / 점선
            _cases.Add(new SkillConstellationLaserLoopCase());
            _cases.Add(new SkillConstellationLaserStellaLinkCase());
            _cases.Add(new SkillConstellationDottedLineCase());

            // 회전베기(본편 단발) / 회전베기+부메랑(로그라이크 상시 반복)
            _cases.Add(new SkillWhirlwindCase("Miss", 0));
            _cases.Add(new SkillWhirlwindCase("Hits4", 4));
            _cases.Add(new SkillWhirlwindCase("Hits14", 14));
            _cases.Add(new SkillWhirlwindBoomerangLoopCase(false));
            _cases.Add(new SkillWhirlwindBoomerangLoopCase(true));

            // 과열 부메랑 꼬리
            _cases.Add(new SkillBoomerangHeatTrailCase());

            // 캐릭터 과열 아우라(단계별) / 드론 아우라
            _cases.Add(new SkillOverheatAuraCase(SkillOverheatAuraCase.EMode.Preheat));
            _cases.Add(new SkillOverheatAuraCase(SkillOverheatAuraCase.EMode.Heating));
            _cases.Add(new SkillOverheatAuraCase(SkillOverheatAuraCase.EMode.BlueFlame));
            _cases.Add(new SkillOverheatAuraCase(SkillOverheatAuraCase.EMode.BlueFlameMoving));
            _cases.Add(new SkillOverheatAuraCase(SkillOverheatAuraCase.EMode.StageSwap));
            _cases.Add(new SkillDroneOverheatAuraCase());

            // 불씨 본체(드랍 → 착지 → 흡입 → 획득 / 제자리 → 소멸)와 단발 이펙트
            _cases.Add(new SkillOverheatEmberDropAcquireCase());
            _cases.Add(new SkillOverheatEmberPlaceExpireCase());
            _cases.Add(new SkillOverheatEmberOneShotCase("Acquire", "OverheatEmberAcquire", 140));
            _cases.Add(new SkillOverheatEmberOneShotCase("Expire", "OverheatEmberExpire", 100));

            // 헤이스트 / 대시 박차기
            _cases.Add(new SkillHasteCase(false));
            _cases.Add(new SkillHasteCase(true));
            _cases.Add(new SkillDashKickoffCase("Right", new Vector2(1.0f, 0.0f), 0));
            _cases.Add(new SkillDashKickoffCase("UpLeftBlocked", new Vector2(-0.8944272f, 0.4472136f), 5));

            // 과열 최대 열기 물결
            _cases.Add(new SkillStageOverheatBurstCase());

            // 스텔라 별 폭발 / 유지시간 게이지
            _cases.Add(new SkillStellaBurstCase("R2_2", 2.2f, false, new Vector2(12.0f, 12.0f)));
            _cases.Add(new SkillStellaBurstCase("R4", 4.0f, false, new Vector2(16.0f, 14.0f)));
            _cases.Add(new SkillStellaBurstCase("R2_2_Repeat", 2.2f, true, new Vector2(12.0f, 12.0f)));
            _cases.Add(new SkillStellaGaugeCase(false));
            _cases.Add(new SkillStellaGaugeCase(true));

            // 오프로드 경로 표시
            _cases.Add(new SkillOffroadLaneCase(SkillOffroadLaneCase.EMode.Fire));
            _cases.Add(new SkillOffroadLaneCase(SkillOffroadLaneCase.EMode.Cancel));
            _cases.Add(new SkillOffroadLaneCase(SkillOffroadLaneCase.EMode.TwoLanes));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 공용 도구
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Skill 묶음 케이스가 함께 쓰는 경로·정렬 계산·리플렉션 도구입니다.</summary>
    public static class SkillGoldenUtil
    {
        public const string CharacterPrefabPath = "Assets/Prefabs/Objects/Character/Character.prefab";
        public const string DronePrefabPath = "Assets/Prefabs/Objects/Drone/Drone.prefab";
        public const string EmberPrefabPath = "Assets/Prefabs/Objects/Item/OverheatEmber.prefab";
        public const string LaserPrefabPath = "Assets/Prefabs/VFX/Constellation/VFX_ConstellationPixelLaser.prefab";
        public const string DottedLinePrefabPath = "Assets/Prefabs/VFX/Constellation/VFX_ConstellationDottedLine.prefab";
        public const string HeatTrailMaterialPath = "Assets/Graphics/VFX/Materials/M_BoomerangHeatTrail.mat";

        /// <summary>RoguelikeSkillVfxPlayer.Order와 같은 계산(-y x 100).</summary>
        public static int RoguelikeOrder(Vector3 _position)
        {
            return -Mathf.RoundToInt(_position.y * 100.0f);
        }

        public static void SetPrivateField(object _target, string _fieldName, object _value)
        {
            FieldInfo field = _target.GetType().GetField(_fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (null == field)
            {
                throw new System.MissingFieldException("[VfxGolden] " + _target.GetType().Name + "." + _fieldName + " 필드가 없습니다.");
            }
            field.SetValue(_target, _value);
        }

        public static void SetPrivateStaticField(System.Type _type, string _fieldName, object _value)
        {
            FieldInfo field = _type.GetField(_fieldName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (null == field)
            {
                throw new System.MissingFieldException("[VfxGolden] " + _type.Name + "." + _fieldName + " 정적 필드가 없습니다.");
            }
            field.SetValue(null, _value);
        }

        /// <summary>케이스 시드에서 0이 아닌 고정 난수 상태를 만듭니다.</summary>
        public static uint DeriveSeed(VfxGoldenContext _context, uint _salt)
        {
            return unchecked(_context.CaseSeed * 2654435761u + _salt * 40503u) | 1u;
        }
    }

    /// <summary>
    /// GameInstaller가 아닌 프리팹(캐릭터·드론)에 등록된 VFX 풀을 게임과 같은 VFXComponent 경로로 재생합니다.
    /// VfxGoldenContext.PlayPooled와 같은 규칙(예열 수 0, 꺼낸 뒤 파티클 시드 고정, Random 재고정, 월드로 떼어지면 Track)을 따릅니다.
    /// </summary>
    public sealed class SkillPrefabPool
    {
        // //외부 의존성
        private readonly VfxGoldenContext context;

        // //내부 의존성
        private readonly VFXComponent pool;

        public VFXComponent Component => pool;

        private SkillPrefabPool(VfxGoldenContext _context, VFXComponent _pool)
        {
            context = _context;
            pool = _pool;
        }

        public static SkillPrefabPool Create(VfxGoldenContext _context, string _prefabPath, params string[] _tags)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (null == prefab)
            {
                throw new System.IO.FileNotFoundException("[VfxGolden] 프리팹이 없습니다: " + _prefabPath);
            }

            FieldInfo listField = typeof(VFXComponent).GetField("vfxPoolDataList", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo sizeField = typeof(VFXPoolData).GetField("initialPoolSize", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo[] dataFields = typeof(VFXPoolData).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            VFXComponent[] components = prefab.GetComponentsInChildren<VFXComponent>(true);

            List<VFXPoolData> copies = new List<VFXPoolData>(_tags.Length);
            for (int t = 0; t < _tags.Length; t++)
            {
                VFXPoolData source = null;
                for (int i = 0; i < components.Length && null == source; i++)
                {
                    List<VFXPoolData> list = listField.GetValue(components[i]) as List<VFXPoolData>;
                    if (null == list) continue;
                    for (int k = 0; k < list.Count; k++)
                    {
                        if (null != list[k] && _tags[t] == list[k].VfxTag)
                        {
                            source = list[k];
                            break;
                        }
                    }
                }

                if (null == source)
                {
                    throw new System.ArgumentException("[VfxGolden] " + _prefabPath + "에 없는 VFX 태그입니다: " + _tags[t]);
                }

                VFXPoolData copy = new VFXPoolData();
                for (int f = 0; f < dataFields.Length; f++) dataFields[f].SetValue(copy, dataFields[f].GetValue(source));
                sizeField.SetValue(copy, 0);
                copies.Add(copy);
            }

            GameObject go = new GameObject("[Pool] " + prefab.name);
            go.SetActive(false);
            go.transform.SetParent(_context.Root, false);
            VFXComponent component = go.AddComponent<VFXComponent>();
            typeof(VFXComponent).GetField("initializeOnAwake", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, false);
            listField.SetValue(component, copies);
            go.SetActive(true);
            component.Initialize();

            return new SkillPrefabPool(_context, component);
        }

        /// <summary>VFXComponent.Play(VFXPlaySettings)와 같은 경로.</summary>
        public ParticleSystem Play(VFXPlaySettings _settings)
        {
            ParticleSystem effect = GetFixed(_settings.VfxTag);
            pool.Play(effect, _settings);
            context.Track(effect.transform.root.gameObject);
            return effect;
        }

        /// <summary>VFXComponent.Play(string, Vector3, Quaternion, Transform)와 같은 경로(소팅 오버라이드 없음).</summary>
        public ParticleSystem Play(string _tag, Vector3 _position, Quaternion _rotation, Transform _parent = null)
        {
            ParticleSystem effect = GetFixed(_tag);
            pool.Play(effect, _position, _rotation, _parent);
            context.Track(effect.transform.root.gameObject);
            return effect;
        }

        private ParticleSystem GetFixed(string _tag)
        {
            ParticleSystem effect = pool.Get(_tag);
            if (null == effect)
            {
                throw new System.InvalidOperationException("[VfxGolden] 풀에서 꺼내지 못했습니다: " + _tag);
            }

            context.FixParticleSeeds(effect.gameObject);
            Random.InitState((int)context.CaseSeed);
            return effect;
        }
    }

    /// <summary>
    /// 캐릭터·드론 프리팹의 Objects 레이어 스프라이트(켜져 있는 것)만 같은 계층·같은 로컬 변환으로 옮긴 대역입니다.
    /// 게임 스크립트(Character, Drone 등)는 붙이지 않습니다. 과열 아우라 실루엣·헤이스트·대시가 읽는 것은 이 스프라이트들과 Transform뿐입니다.
    /// 대역 루트는 씬 최상위에 두고 Track합니다. 그래야 VFX_OverheatAura가 부모 계층에서 찾는 루트(transform.root)가 게임의 캐릭터 루트처럼 대역 루트가 됩니다.
    /// </summary>
    public sealed class SkillSpriteStandIn
    {
        // //내부 의존성
        private readonly Transform prefabRoot;
        private readonly Dictionary<Transform, Transform> copies = new Dictionary<Transform, Transform>(32);
        private readonly List<SpriteRenderer> rendererBuffer = new List<SpriteRenderer>(16);

        public Transform Root { get; private set; }

        public SkillSpriteStandIn(VfxGoldenContext _context, string _prefabPath, Vector3 _position)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (null == prefab)
            {
                throw new System.IO.FileNotFoundException("[VfxGolden] 프리팹이 없습니다: " + _prefabPath);
            }

            prefabRoot = prefab.transform;

            GameObject root = new GameObject("[StandIn] " + prefab.name);
            root.transform.position = _position;
            _context.Track(root);
            Root = root.transform;
            copies[prefabRoot] = Root;

            int objectsLayer = SortingLayer.NameToID("Objects");
            prefab.GetComponentsInChildren(true, rendererBuffer);
            for (int i = 0; i < rendererBuffer.Count; i++)
            {
                SpriteRenderer source = rendererBuffer[i];
                if (objectsLayer != source.sortingLayerID || false == source.enabled || null == source.sprite) continue;
                if (false == IsActiveInPrefab(source.transform)) continue;

                Transform node = GetCopy(source.transform);
                SpriteRenderer target = node.gameObject.AddComponent<SpriteRenderer>();
                target.sprite = source.sprite;
                target.color = source.color;
                target.flipX = source.flipX;
                target.flipY = source.flipY;
                target.sharedMaterial = source.sharedMaterial;
                target.sortingLayerID = source.sortingLayerID;

                // 게임 프리팹의 SortingGroup(캐릭터 [Base Visuals] 등)을 그대로 옮긴다. 그룹 밖 이펙트는 그룹 전체와 정렬되므로
                // 그룹이 없으면 이펙트가 몸 조각 사이에 끼어드는 등 게임과 앞뒤가 달라진다.
                // 그룹 안에서 순서가 같은 조각(몸·얼굴·깜빡임)은 실행마다 앞뒤가 바뀌므로(동률), 순서 x 64 + 계층 순번으로 고정한다.
                // 그룹 안의 순서는 그룹 밖과 비교되지 않으므로 이 보정은 이펙트와의 앞뒤에 영향이 없다.
                UnityEngine.Rendering.SortingGroup group = source.GetComponentInParent<UnityEngine.Rendering.SortingGroup>(true);
                if (null != group && group.transform.IsChildOf(prefabRoot))
                {
                    Transform groupCopy = GetCopy(group.transform);
                    if (null == groupCopy.GetComponent<UnityEngine.Rendering.SortingGroup>())
                    {
                        UnityEngine.Rendering.SortingGroup copyGroup = groupCopy.gameObject.AddComponent<UnityEngine.Rendering.SortingGroup>();
                        copyGroup.sortingLayerID = group.sortingLayerID;
                        copyGroup.sortingOrder = group.sortingOrder;
                    }
                    target.sortingOrder = source.sortingOrder * 64 + i;
                }
                else
                {
                    target.sortingOrder = source.sortingOrder;
                }
            }
            rendererBuffer.Clear();
        }

        /// <summary>프리팹 안의 이름이 _name인 노드에 해당하는 대역 노드(없으면 계층째 만듦).</summary>
        public Transform Find(string _name)
        {
            Transform source = FindRecursive(prefabRoot, _name);
            if (null == source)
            {
                throw new System.ArgumentException("[VfxGolden] 프리팹에 없는 노드입니다: " + _name);
            }
            return GetCopy(source);
        }

        private Transform GetCopy(Transform _source)
        {
            if (copies.TryGetValue(_source, out Transform existing)) return existing;

            Transform parent = GetCopy(_source.parent);
            GameObject go = new GameObject(_source.name);
            Transform node = go.transform;
            node.SetParent(parent, false);
            node.localPosition = _source.localPosition;
            node.localRotation = _source.localRotation;
            node.localScale = _source.localScale;
            copies[_source] = node;
            return node;
        }

        private bool IsActiveInPrefab(Transform _node)
        {
            for (Transform t = _node; null != t; t = t.parent)
            {
                if (false == t.gameObject.activeSelf) return false;
                if (t == prefabRoot) break;
            }
            return true;
        }

        private static Transform FindRecursive(Transform _node, string _name)
        {
            if (_name == _node.name) return _node;
            for (int i = 0; i < _node.childCount; i++)
            {
                Transform found = FindRecursive(_node.GetChild(i), _name);
                if (null != found) return found;
            }
            return null;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 포자 대폭발 / 유성우 / 노바
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// InDungeonVFXManager.PlaySporeBigExplosionVFX (InDungeonObjectManager.TriggerSporeBigExplosion, RoguelikeSkillVfxPlayer.Play SporeBigExplosion):
    /// 풀 재생("FlyingItem", 10) → SetRange(0보다 클 때) → SetTier.
    /// </summary>
    public class SkillSporeBigExplosionCase : VfxGoldenCase
    {
        private readonly string variant;
        private readonly int tier;
        private readonly float range;
        private readonly Vector2 viewCenter;
        private readonly Vector2 viewSize;

        public SkillSporeBigExplosionCase(string _variant, int _tier, float _range, Vector2 _viewCenter, Vector2 _viewSize)
        {
            variant = _variant;
            tier = _tier;
            range = _range;
            viewCenter = _viewCenter;
            viewSize = _viewSize;
        }

        public override string Name => "Skill_SporeBigExplosion_" + variant;
        public override int Frames => 100;
        public override Vector2 ViewCenter => viewCenter;
        public override Vector2 ViewSize => viewSize;

        public override void Setup(VfxGoldenContext _context)
        {
            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("SporeBigExplosionEffect", Vector3.zero, Quaternion.identity, "FlyingItem", 10, null));
            VFX_SporeBigExplosion explosion = effect.GetComponentInChildren<VFX_SporeBigExplosion>(true);
            if (0.0f < range) explosion.SetRange(range);
            explosion.SetTier(tier);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayStarRainVFX (InDungeonObjectManager.MeteorShowerRoutine): ClampCount → 시드 Random.Range(1, int.MaxValue)
    /// → 풀 재생("FlyingItem", 10) → VFX_StarRain.Play(수, 지속시간, 범위, 시드, 단계). 범위는 게임 기본값 3.5, 지속시간은 최소 2초로 줄였다.
    /// </summary>
    public class SkillStarRainCase : VfxGoldenCase
    {
        private const float Duration = 2.0f;
        private const float FallRange = 3.5f;

        private readonly int tier;
        private readonly int requestedCount;

        public SkillStarRainCase(int _tier, int _requestedCount)
        {
            tier = _tier;
            requestedCount = _requestedCount;
        }

        public override string Name => "Skill_StarRain_T" + tier;
        public override int Frames => 190;
        public override Vector2 ViewCenter => new Vector2(1.0f, 2.5f);
        public override Vector2 ViewSize => new Vector2(16.0f, 16.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            int count = VFX_StarRain.ClampCount(requestedCount, Duration);
            int seed = Random.Range(1, int.MaxValue);

            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("StarRainEffect", Vector3.zero, Quaternion.identity, "FlyingItem", 10, null));
            VFX_StarRain rain = effect.GetComponentInChildren<VFX_StarRain>(true);
            rain.Play(count, Duration, FallRange, seed, tier);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayConstellationNovaVFX (InDungeonObjectManager의 별자리 발현, CurrentConstellationBlastRadius = 4):
    /// 풀 재생("FlyingItem", 10) → SetRadius(0보다 클 때). R2_5는 마법진 표(정적 캐시)가 다른 반지름으로 다시 만들어지는 경로다.
    /// </summary>
    public class SkillConstellationNovaCase : VfxGoldenCase
    {
        private readonly string variant;
        private readonly float radius;
        private readonly Vector2 viewCenter;
        private readonly Vector2 viewSize;

        public SkillConstellationNovaCase(string _variant, float _radius, Vector2 _viewCenter, Vector2 _viewSize)
        {
            variant = _variant;
            radius = _radius;
            viewCenter = _viewCenter;
            viewSize = _viewSize;
        }

        public override string Name => "Skill_ConstellationNova_" + variant;
        public override int Frames => 185;
        public override Vector2 ViewCenter => viewCenter;
        public override Vector2 ViewSize => viewSize;

        public override void Setup(VfxGoldenContext _context)
        {
            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("ConstellationNovaEffect", Vector3.zero, Quaternion.identity, "FlyingItem", 10, null));
            if (radius <= 0.0f) return;

            VFX_ConstellationNova nova = effect.GetComponentInChildren<VFX_ConstellationNova>(true);
            nova.SetRadius(radius);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayRoguelikeArenaNovaVFX (RoguelikeSkillVfxPlayer.Play StarNova ← RoguelikeSkillController의 스텔라+드론 노바):
    /// 풀 재생("FlyingItem", 10) → SetArenaExtents(반폭, 반높이). 반폭·반높이는 기본 섬(8, 4.5) + 반 칸(1, 0.5)의 절반.
    /// </summary>
    public class SkillRoguelikeArenaNovaCase : VfxGoldenCase
    {
        public override string Name => "Skill_RoguelikeArenaNova";
        public override int Frames => 185;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.75f);
        public override Vector2 ViewSize => new Vector2(20.0f, 14.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            const float halfWidth = 8.0f + 0.5f;
            const float halfHeight = 4.5f + 0.25f;

            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("RoguelikeArenaNovaEffect", Vector3.zero, Quaternion.identity, "FlyingItem", 10, null));
            VFX_ConstellationNova nova = effect.GetComponentInChildren<VFX_ConstellationNova>(true);
            nova.SetArenaExtents(halfWidth, halfHeight);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 별자리 레이저 / 점선
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 레이저 풀러(ConstellationPixelLaserCreator)를 게임처럼 프리팹 연결 → Get으로 만든다. 풀러는 GameInstaller 하위 컴포넌트라
    /// 빈 오브젝트에 붙이고 직렬화 필드(laserPrefab)만 리플렉션으로 채운다(나머지 값은 컴포넌트 기본값).
    /// </summary>
    public static class SkillLaserCreatorUtil
    {
        public static ConstellationPixelLaserCreator Create(VfxGoldenContext _context)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SkillGoldenUtil.LaserPrefabPath);
            if (null == prefab)
            {
                throw new System.IO.FileNotFoundException("[VfxGolden] 프리팹이 없습니다: " + SkillGoldenUtil.LaserPrefabPath);
            }

            GameObject go = _context.CreateEmpty("ConstellationPixelLaserCreator", Vector3.zero);
            go.SetActive(false);
            ConstellationPixelLaserCreator creator = go.AddComponent<ConstellationPixelLaserCreator>();
            SkillGoldenUtil.SetPrivateField(creator, "laserPrefab", prefab.GetComponent<ConstellationPixelLaser>());
            go.SetActive(true);
            return creator;
        }
    }

    /// <summary>InDungeonObjectManager.FireConstellationLaser: constellationLaserCreator.Get() → PlayLaser(별 경로, 닫힌 루프).</summary>
    public class SkillConstellationLaserLoopCase : VfxGoldenCase
    {
        public override string Name => "Skill_ConstellationLaser_Loop5";
        public override int Frames => 75;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.2f);
        public override Vector2 ViewSize => new Vector2(8.0f, 6.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            ConstellationPixelLaserCreator creator = SkillLaserCreatorUtil.Create(_context);
            ConstellationPixelLaser laser = creator.Get();

            List<Vector3> path = new List<Vector3>(5)
            {
                new Vector3(-3.0f, 0.0f, 0.0f),
                new Vector3(-0.5f, 2.0f, 0.0f),
                new Vector3(2.5f, 1.2f, 0.0f),
                new Vector3(1.8f, -1.8f, 0.0f),
                new Vector3(-1.5f, -1.5f, 0.0f),
            };
            laser.PlayLaser(path, true);
        }
    }

    /// <summary>
    /// RoguelikeSkillVfxPlayer.FireLaser의 대체 경로(드론 레이저 에셋이 없을 때): laserCreator.Get() → ApplyStellaTint → PlayLaser(시작, 끝).
    /// 위치는 별 그림 중심(starCenterOffset을 더한 값)으로 둔다.
    /// </summary>
    public class SkillConstellationLaserStellaLinkCase : VfxGoldenCase
    {
        public override string Name => "Skill_ConstellationLaser_StellaLink";
        public override int Frames => 75;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.5f);
        public override Vector2 ViewSize => new Vector2(8.0f, 5.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            ConstellationPixelLaserCreator creator = SkillLaserCreatorUtil.Create(_context);
            ConstellationPixelLaser laser = creator.Get();
            laser.ApplyStellaTint();
            laser.PlayLaser(new Vector3(-2.5f, -0.25f, 0.0f), new Vector3(2.5f, 1.25f, 0.0f));
        }
    }

    /// <summary>
    /// InDungeonVFXManager.ShowDottedLine → SetConstellationDottedLineNodeFelled → RemoveConstellationDottedLine(HideDottedLine) 흐름.
    /// 원점 배치 → SetNodes(노드, 닫힘) → SetLastStarWarning. 나무를 하나씩 베며 SetNodeAsBigStar + 마지막 별 맥동 켜기/끄기, 끝에 ReturnToPool.
    /// (풀 대신 프리팹을 직접 만들었으므로 ReturnToPool은 비활성화로 끝난다.)
    /// </summary>
    public class SkillConstellationDottedLineCase : VfxGoldenCase
    {
        private static readonly Vector3[] NodePositions =
        {
            new Vector3(-2.5f, -0.5f, 0.0f),
            new Vector3(-0.75f, 1.75f, 0.0f),
            new Vector3(2.25f, 1.0f, 0.0f),
            new Vector3(1.5f, -1.5f, 0.0f),
        };
        private static readonly int[] FellFrames = { 10, 60, 110, 200 };
        private const int ReturnFrame = 270;

        private ConstellationDottedLine line;
        private readonly bool[] felled = new bool[4];

        public override string Name => "Skill_ConstellationDottedLine_Felling";
        public override int Frames => 290;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.2f);
        public override Vector2 ViewSize => new Vector2(8.0f, 6.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            line = _context.Spawn(SkillGoldenUtil.DottedLinePrefabPath, Vector3.zero).GetComponent<ConstellationDottedLine>();
            line.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            List<ConstellationNode> nodes = new List<ConstellationNode>(NodePositions.Length);
            for (int i = 0; i < NodePositions.Length; i++) nodes.Add(new ConstellationNode(NodePositions[i], false));
            line.SetNodes(nodes, true);
            line.SetLastStarWarning(false);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            for (int i = 0; i < FellFrames.Length; i++)
            {
                if (FellFrames[i] != _frame) continue;

                felled[i] = true;
                line.SetNodeAsBigStar(i, true);

                int remaining = 0;
                for (int k = 0; k < felled.Length; k++)
                {
                    if (false == felled[k]) remaining++;
                }
                line.SetLastStarWarning(1 == remaining);
            }

            if (ReturnFrame == _frame) line.ReturnToPool();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 회전베기 / 부메랑 꼬리
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// AttackComponent의 회전베기: WhirlwindVFX.Spawn(중심, 판정 반경 1.25 x 기본 배율 2) → QueueWhirlwindSweep(GetSweepTime 순 정렬)
    /// → WhirlwindSweepRoutine(같은 프레임 elapsed 0부터, 매 프레임 deltaTime을 더해 시각이 된 나무마다 AddHit(중심 + 방향, 방향)).
    /// WhirlwindVFX는 정적 재생 번호(playCounter)로 난수를 시드하므로 케이스 시작마다 0으로 되돌린다(새 플레이 세션과 같은 상태,
    /// 일부 케이스만 돌려도 결과가 같도록).
    /// </summary>
    public class SkillWhirlwindCase : VfxGoldenCase
    {
        private const float AttackRadius = 1.25f * 2.0f;

        private struct PendingHit
        {
            public Vector2 direction;
            public float time;
        }

        private readonly string variant;
        private readonly int treeCount;
        private readonly List<PendingHit> hits = new List<PendingHit>(16);
        private WhirlwindVFX whirlwind;
        private int playId;
        private int next;
        private float elapsed;

        public SkillWhirlwindCase(string _variant, int _treeCount)
        {
            variant = _variant;
            treeCount = _treeCount;
        }

        public override string Name => "Skill_Whirlwind_" + variant;
        public override int Frames => 75;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.5f);
        public override Vector2 ViewSize => new Vector2(10.0f, 8.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            SkillGoldenUtil.SetPrivateStaticField(typeof(WhirlwindVFX), "playCounter", 0);

            Vector3 center = Vector3.zero;
            whirlwind = WhirlwindVFX.Spawn(center, AttackRadius);
            if (null == whirlwind)
            {
                throw new System.InvalidOperationException("[VfxGolden] WhirlwindVFX 셰이더를 찾지 못했습니다.");
            }
            _context.Track(whirlwind.gameObject);
            playId = whirlwind.PlayId;

            // 범위(등각 타원 가로 2.5 / 세로 1.25) 안에 고르게 흩어진 나무. 시각 순으로 끼워 넣는다(AttackComponent.InsertWhirlwindHit).
            hits.Clear();
            for (int i = 0; i < treeCount; i++)
            {
                float angle = (i * (360.0f / Mathf.Max(1, treeCount)) + 7.0f) * Mathf.Deg2Rad;
                float ratio = 0.35f + 0.13f * (i % 5);
                Vector2 direction = new Vector2(Mathf.Cos(angle) * AttackRadius * ratio, Mathf.Sin(angle) * AttackRadius * 0.5f * ratio);
                PendingHit hit = new PendingHit { direction = direction, time = WhirlwindVFX.GetSweepTime(direction) };

                int index = hits.Count;
                while (index > 0 && hits[index - 1].time > hit.time) index--;
                hits.Insert(index, hit);
            }

            next = 0;
            elapsed = 0.0f;
            ProcessHits();
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (next >= hits.Count) return;

            elapsed += Time.deltaTime;
            ProcessHits();
        }

        private void ProcessHits()
        {
            while (next < hits.Count && hits[next].time <= elapsed)
            {
                PendingHit hit = hits[next++];
                whirlwind.AddHit(playId, (Vector3)hit.direction, hit.direction);
            }
        }
    }

    /// <summary>
    /// RoguelikeSkillVfxPlayer.AttachSpinBoomerang / SetSpinBoomerangPaused / DetachSpinBoomerang (회전베기+부메랑):
    /// WhirlwindVFX.SpawnAttached(부메랑, 부메랑 판정 0.5 x spinBoomerangRadiusRatio 1.5) → 날아가는 동안 반복 → 멈춤/재개 → Stop(PlayId).
    /// bTargetOff 변형은 부메랑이 꺼지면(회수·파괴) 연출이 스스로 풀로 돌아가는 경로다. 판정 쪽(SpinStrikeAroundBoomerang)은 AddHit을 부르지 않는다.
    /// </summary>
    public class SkillWhirlwindBoomerangLoopCase : VfxGoldenCase
    {
        private const float Radius = 0.5f * 1.5f;

        private readonly bool bTargetOff;
        private Transform boomerang;
        private WhirlwindVFX whirlwind;
        private int playId;

        public SkillWhirlwindBoomerangLoopCase(bool _bTargetOff)
        {
            bTargetOff = _bTargetOff;
        }

        public override string Name => "Skill_Whirlwind_BoomerangLoop" + (bTargetOff ? "_TargetOff" : string.Empty);
        public override int Frames => bTargetOff ? 110 : 220;
        public override Vector2 ViewCenter => new Vector2(0.5f, 0.5f);
        public override Vector2 ViewSize => new Vector2(10.0f, 7.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            SkillGoldenUtil.SetPrivateStaticField(typeof(WhirlwindVFX), "playCounter", 0);

            boomerang = _context.CreateEmpty("Boomerang", BoomerangPosition(0)).transform;
            whirlwind = WhirlwindVFX.SpawnAttached(boomerang, Radius);
            if (null == whirlwind)
            {
                throw new System.InvalidOperationException("[VfxGolden] WhirlwindVFX 셰이더를 찾지 못했습니다.");
            }
            _context.Track(whirlwind.gameObject);
            playId = whirlwind.PlayId;
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (bTargetOff)
            {
                if (_frame < 90) boomerang.position = BoomerangPosition(_frame);
                else if (90 == _frame) boomerang.gameObject.SetActive(false);
                return;
            }

            // 80~109프레임은 경고 UI로 멈춘 동안이라 부메랑도 제자리
            if (80 == _frame) whirlwind.SetPaused(playId, true);
            if (110 == _frame) whirlwind.SetPaused(playId, false);

            int moveFrame = _frame < 80 ? _frame : (_frame < 110 ? 80 : _frame - 30);
            if (_frame < 200) boomerang.position = BoomerangPosition(moveFrame);
            if (200 == _frame) whirlwind.Stop(playId);
        }

        // 나갔다 돌아오는 휘어진 궤적(가로 4유닛, 세로로 부푼 곡선)
        private static Vector3 BoomerangPosition(int _frame)
        {
            float t = _frame / 170.0f;
            float angle = t * Mathf.PI * 2.0f;
            return new Vector3(-2.0f + 2.0f * (1.0f - Mathf.Cos(angle)), 1.2f * Mathf.Sin(angle), 0.0f);
        }
    }

    /// <summary>
    /// Boomerang의 과열 꼬리: VFX_BoomerangHeatTrail.Create(M_BoomerangHeatTrail) + DontDestroyOnLoad → 비행 시작 Begin
    /// → LateUpdate마다 Feed(위치, 스프라이트 소팅, hitRadius 0.5) → 멈춤 SetPaused → 회수 시 SetPaused(false) + End(남은 꼬리 소멸).
    /// 이펙트가 GetHashCode(인스턴스 ID)로 난수를 시드해 실행마다 달라지므로, 만든 직후 randomState만 케이스 시드로 고정한다.
    /// </summary>
    public class SkillBoomerangHeatTrailCase : VfxGoldenCase
    {
        private const float HitRadius = 0.5f;
        private const int FlightFrames = 72;

        private VFX_BoomerangHeatTrail trail;
        private int objectsLayerId;

        public override string Name => "Skill_BoomerangHeatTrail_Flight";
        public override int Frames => 170;
        public override Vector2 ViewCenter => new Vector2(0.5f, 0.5f);
        public override Vector2 ViewSize => new Vector2(10.0f, 7.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SkillGoldenUtil.HeatTrailMaterialPath);
            if (null == material)
            {
                throw new System.IO.FileNotFoundException("[VfxGolden] 머티리얼이 없습니다: " + SkillGoldenUtil.HeatTrailMaterialPath);
            }

            trail = VFX_BoomerangHeatTrail.Create(material);
            Object.DontDestroyOnLoad(trail.gameObject);
            _context.Track(trail.gameObject);
            SkillGoldenUtil.SetPrivateField(trail, "randomState", SkillGoldenUtil.DeriveSeed(_context, 17u));

            objectsLayerId = SortingLayer.NameToID("Objects");
            trail.Begin();
            Feed(0);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            // 30~39프레임은 경고 UI로 부메랑이 멈춘 동안
            if (30 == _frame) trail.SetPaused(true);
            if (40 == _frame) trail.SetPaused(false);

            if (_frame < FlightFrames)
            {
                Feed(_frame < 30 ? _frame : (_frame < 40 ? 30 : _frame - 10));
            }
            else if (FlightFrames == _frame)
            {
                trail.SetPaused(false);
                trail.End();
            }
        }

        private void Feed(int _moveFrame)
        {
            float t = _moveFrame / 62.0f;
            float angle = t * Mathf.PI * 2.0f;
            Vector3 position = new Vector3(-3.0f + 3.0f * (1.0f - Mathf.Cos(angle)), 1.3f * Mathf.Sin(angle), 0.0f);
            trail.Feed(position, objectsLayerId, SkillGoldenUtil.RoguelikeOrder(position), HitRadius);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 과열 아우라
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// OverheatComponent.RefreshLoopVfx / StopActiveVfx / ApplyStageProgress (캐릭터 과열 단계 1 예열·2 가열·3 푸른 불꽃):
    /// 캐릭터 VFXComponent 풀(Character.prefab의 OverheatLoopEffect_Stage1/_Stage2/OverheatLoopEffect)에서
    /// Play(VFXPlaySettings(태그, OverheatComponent 위치 + vfxOffset(0), 캐릭터 정렬 + 1, 부모 = OverheatComponent)) → SetStageProgress
    /// → 끝에 vfxComponent.Stop(이펙트, false)(방출만 멈춤 → 아우라 소화 연출).
    /// 캐릭터는 Objects 레이어 스프라이트만 옮긴 대역이다(SkillSpriteStandIn). 정렬 동기화(OverheatComponent.LateUpdate)는 대역 정렬이 고정이라 값이 바뀌지 않는다.
    /// </summary>
    public class SkillOverheatAuraCase : VfxGoldenCase
    {
        public enum EMode
        {
            Preheat,
            Heating,
            BlueFlame,
            BlueFlameMoving,
            StageSwap,
        }

        private const string TagStage1 = "OverheatLoopEffect_Stage1";
        private const string TagStage2 = "OverheatLoopEffect_Stage2";
        private const string TagStage3 = "OverheatLoopEffect";
        private const int SortingOrder = 1;

        private readonly EMode mode;
        private SkillSpriteStandIn character;
        private Transform overheatTransform;
        private SkillPrefabPool pool;
        private ParticleSystem activeVfx;
        private ParticleSystem fadingVfx;
        private VFX_OverheatAura activeAura;

        public SkillOverheatAuraCase(EMode _mode)
        {
            mode = _mode;
        }

        public override string Name => "Skill_OverheatAura_" + mode;
        public override int Frames => 290;
        public override Vector2 ViewCenter => EMode.BlueFlameMoving == mode ? new Vector2(0.0f, 0.6f) : new Vector2(0.0f, 0.8f);
        public override Vector2 ViewSize => EMode.BlueFlameMoving == mode ? new Vector2(8.0f, 6.0f) : new Vector2(5.0f, 5.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            character = new SkillSpriteStandIn(_context, SkillGoldenUtil.CharacterPrefabPath, Vector3.zero);
            overheatTransform = character.Find("OverheatComponent");
            pool = SkillPrefabPool.Create(_context, SkillGoldenUtil.CharacterPrefabPath, TagStage1, TagStage2, TagStage3);

            switch (mode)
            {
                case EMode.Preheat:
                    StartStage(TagStage1, 0.5f);
                    break;
                case EMode.Heating:
                    StartStage(TagStage2, 0.0f);
                    break;
                case EMode.StageSwap:
                    StartStage(TagStage1, 0.0f);
                    break;
                default:
                    StartStage(TagStage3, 1.0f);
                    break;
            }
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            switch (mode)
            {
                case EMode.Preheat:
                    if (60 == _frame) SetProgress(0.8f);
                    if (140 == _frame) StopActive();
                    break;

                case EMode.Heating:
                    if (0 == _frame % 10 && _frame <= 150) SetProgress(_frame / 150.0f);
                    if (160 == _frame) StopActive();
                    break;

                case EMode.BlueFlame:
                    if (150 == _frame) StopActive();
                    break;

                case EMode.BlueFlameMoving:
                    if (_frame <= 200)
                    {
                        // 반지름 1.5 원을 따라 달린다(초당 약 3유닛). 150프레임에 과열이 꺼져도 소화 연출 동안 계속 움직인다.
                        float angle = _frame * (3.0f / 1.5f) / 60.0f;
                        character.Root.position = new Vector3(Mathf.Sin(angle) * 1.5f, (1.0f - Mathf.Cos(angle)) * 0.75f - 0.75f, 0.0f);
                    }
                    if (150 == _frame) StopActive();
                    break;

                case EMode.StageSwap:
                    if (0 == _frame % 10 && _frame < 70) SetProgress(_frame / 70.0f);
                    if (70 == _frame)
                    {
                        // 과열 34 도달: 1단계 오라는 사그라들고(fadingVfx) 2단계 오라를 새로 켠다
                        StopActive();
                        StartStage(TagStage2, 0.0f);
                    }
                    if (0 == _frame % 10 && 80 <= _frame && _frame < 160) SetProgress((_frame - 70) / 90.0f);
                    // 원정이 바뀌어 과열 0: 사그라드는 1단계 오라가 남아 있으면 즉시 회수, 2단계 오라는 방출만 멈춘다
                    if (160 == _frame) StopActive();
                    break;
            }
        }

        private void StartStage(string _tag, float _progress)
        {
            activeVfx = pool.Play(new VFXPlaySettings(_tag, overheatTransform.position, Quaternion.identity, SortingOrder, overheatTransform));
            activeAura = activeVfx.GetComponentInChildren<VFX_OverheatAura>(true);
            SetProgress(_progress);
        }

        private void SetProgress(float _progress)
        {
            if (null != activeAura) activeAura.SetStageProgress(_progress);
        }

        // OverheatComponent.StopActiveVfx: 사그라드는 중인 이전 오라는 즉시 회수하고, 지금 오라는 방출만 멈춘다.
        private void StopActive()
        {
            if (null == activeVfx) return;

            if (null != fadingVfx && fadingVfx.gameObject.activeSelf) pool.Component.Stop(fadingVfx, true);

            pool.Component.Stop(activeVfx, false);
            fadingVfx = activeVfx;
            activeVfx = null;
            activeAura = null;
        }
    }

    /// <summary>
    /// Drone.StartOverheatAura / UpdateOverheatAura / StopOverheatAura(false): 드론 VFXComponent 풀(Drone.prefab의 DroneOverheatAura)에서
    /// 부모 없이 Play(VFXPlaySettings(태그, 드론 위치, 드론 정렬 + 1, null)) → SetSourceRoot(드론) → 매 프레임 위치 따라가기 → 방출 정지.
    /// 드론은 Objects 레이어 스프라이트(Visual)만 옮긴 대역이고, Visual을 위아래로 흔들어 useFirstSourceAsOrigin 경로를 탄다.
    /// </summary>
    public class SkillDroneOverheatAuraCase : VfxGoldenCase
    {
        private const string Tag = "DroneOverheatAura";
        private const int SortingOrder = 1;

        private SkillSpriteStandIn drone;
        private Transform visual;
        private SkillPrefabPool pool;
        private ParticleSystem auraVfx;

        public override string Name => "Skill_OverheatAura_Drone";
        public override int Frames => 280;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.8f);
        public override Vector2 ViewSize => new Vector2(6.0f, 5.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            drone = new SkillSpriteStandIn(_context, SkillGoldenUtil.DronePrefabPath, new Vector3(0.0f, 0.75f, 0.0f));
            visual = drone.Find("Visual");
            pool = SkillPrefabPool.Create(_context, SkillGoldenUtil.DronePrefabPath, Tag);

            auraVfx = pool.Play(new VFXPlaySettings(Tag, drone.Root.position, Quaternion.identity, SortingOrder, null));
            VFX_OverheatAura aura = auraVfx.GetComponentInChildren<VFX_OverheatAura>(true);
            aura.SetSourceRoot(drone.Root);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            float angle = _frame * 1.5f / 60.0f;
            drone.Root.position = new Vector3(Mathf.Sin(angle) * 1.2f, 0.75f + Mathf.Sin(angle * 2.0f) * 0.3f, 0.0f);
            visual.localPosition = new Vector3(0.0f, Mathf.Sin(_frame * 0.15f) * 0.06f, 0.0f);

            if (150 == _frame) pool.Component.Stop(auraVfx, false);

            if (null != auraVfx && auraVfx.gameObject.activeSelf) auraVfx.transform.position = drone.Root.position;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 불씨
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 불씨 아이템 하나를 EmberItemController처럼 돌리는 도구: 풀 꺼냄(ResetItem) → Initialize → Launch/Place,
    /// 갱신 필요 이벤트를 받는 동안 매 프레임 ManualUpdate, 획득·소멸 이벤트에서 단발 이펙트 재생 후 회수(ResetItem + 비활성).
    /// 회수 때 날아가던 꼬리는 OverheatEmberResidue 드라이버가 이어 그리므로 드라이버도 해시·정리 대상에 올린다.
    /// </summary>
    public sealed class SkillEmberDriver
    {
        // //외부 의존성
        private readonly VfxGoldenContext context;

        // //내부 의존성
        public EmberItem Ember { get; private set; }

        private bool bNeedsUpdate;
        private bool bReleased;

        public SkillEmberDriver(VfxGoldenContext _context, Vector3 _spawnPosition)
        {
            context = _context;
            Ember = _context.Spawn(SkillGoldenUtil.EmberPrefabPath, _spawnPosition).GetComponent<EmberItem>();
            Ember.EmberAcquiredEvent += OnAcquired;
            Ember.EmberExpiredEvent += OnExpired;
            Ember.EmberUpdateNeededChangedEvent += OnUpdateNeededChanged;

            // EmberItemController.OnGetEmberItem
            Ember.ResetItem();

            TrackResidueDriver(_context);
        }

        public bool IsReleased => bReleased;

        /// <summary>EmberItemController.Update와 같은 갱신(갱신이 필요한 동안만).</summary>
        public void Tick()
        {
            if (true == bReleased || false == bNeedsUpdate) return;
            Ember.ManualUpdate(Time.deltaTime);
        }

        private void OnUpdateNeededChanged(EmberItem _ember, bool _bNeedsUpdate)
        {
            bNeedsUpdate = _bNeedsUpdate;
        }

        // EmberItemController.OnEmberAcquired / OnEmberExpired → PlayOneShotVfx(태그, 위치) → TryReleaseEmber(OnReleaseEmberItem)
        private void OnAcquired(EmberItem _ember)
        {
            context.PlayPooled(new VFXPlaySettings("OverheatEmberAcquire", _ember.transform.position, Quaternion.identity));
            Release();
        }

        private void OnExpired(EmberItem _ember)
        {
            context.PlayPooled(new VFXPlaySettings("OverheatEmberExpire", _ember.transform.position, Quaternion.identity));
            Release();
        }

        private void Release()
        {
            if (true == bReleased) return;
            bReleased = true;

            Ember.ResetItem();
            Ember.gameObject.SetActive(false);
        }

        // 잔여 드라이버(OverheatEmberResidue)는 불씨 OnEnable의 Prepare가 만든 숨김 DontDestroyOnLoad 오브젝트다.
        private static void TrackResidueDriver(VfxGoldenContext _context)
        {
            GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
            for (int i = 0; i < objects.Length; i++)
            {
                GameObject go = objects[i];
                if ("OverheatEmberResidue" != go.name || false == go.scene.IsValid() || EditorUtility.IsPersistent(go)) continue;
                _context.Track(go);
            }
        }
    }

    /// <summary>
    /// EmberItemController.SpawnDropEmbers(나무 자리에서 튀어나옴: insideUnitCircle 방향, 1.25~1.75 거리, 0.75~1.25 높이, 수명 droppedLifetime 20)
    /// → 착지 → 캐릭터 아이템 감지의 SetSuckTarget(캐릭터) → 흡입 → 획득(OverheatEmberAcquire 단발 + 회수, 남은 꼬리는 잔여 드라이버).
    /// VFX_OverheatEmber 본체와 OverheatEmberOrb/Particles/Trail/Residue를 모두 지난다.
    /// </summary>
    public class SkillOverheatEmberDropAcquireCase : VfxGoldenCase
    {
        private const float DroppedLifetime = 20.0f;
        private const int SuckFrame = 80;

        private SkillEmberDriver driver;
        private Transform suckTarget;

        public override string Name => "Skill_OverheatEmber_DropAcquire";
        public override int Frames => 270;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.8f);
        public override Vector2 ViewSize => new Vector2(8.0f, 6.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            Vector3 treePosition = Vector3.zero;
            suckTarget = _context.CreateEmpty("CharacterCenter", new Vector3(-2.5f, 0.25f, 0.0f)).transform;

            driver = new SkillEmberDriver(_context, treePosition);
            driver.Ember.Initialize(null, DroppedLifetime);

            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomDist = Random.Range(1.25f, 1.75f);
            Vector3 endPos = treePosition + new Vector3(randomDir.x, randomDir.y * 0.5f, 0.0f) * randomDist;
            driver.Ember.Launch(treePosition, endPos, Random.Range(0.75f, 1.25f));
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (SuckFrame == _frame && false == driver.IsReleased) driver.Ember.SetSuckTarget(suckTarget);
            driver.Tick();
        }
    }

    /// <summary>
    /// EmberItemController.PlaceEmber(제자리 배치, 수명 있음) → 수명 만료 → 소멸(OverheatEmberExpire 단발 + 회수).
    /// 수명은 프레임 예산 때문에 1.5초로 줄였다.
    /// </summary>
    public class SkillOverheatEmberPlaceExpireCase : VfxGoldenCase
    {
        private const float Lifetime = 1.5f;

        private SkillEmberDriver driver;

        public override string Name => "Skill_OverheatEmber_PlaceExpire";
        public override int Frames => 200;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.5f);
        public override Vector2 ViewSize => new Vector2(4.0f, 4.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            driver = new SkillEmberDriver(_context, Vector3.zero);
            driver.Ember.Initialize(null, Lifetime);
            driver.Ember.Place(Vector3.zero);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            driver.Tick();
        }
    }

    /// <summary>EmberItemController.PlayOneShotVfx: vfxComponent.Play(태그, 불씨 위치, identity) - 소팅 오버라이드 없음.</summary>
    public class SkillOverheatEmberOneShotCase : VfxGoldenCase
    {
        private readonly string variant;
        private readonly string tag;
        private readonly int frames;

        public SkillOverheatEmberOneShotCase(string _variant, string _tag, int _frames)
        {
            variant = _variant;
            tag = _tag;
            frames = _frames;
        }

        public override string Name => "Skill_OverheatEmberOneShot_" + variant;
        public override int Frames => frames;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.6f);
        public override Vector2 ViewSize => new Vector2(4.0f, 4.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            _context.PlayPooled(new VFXPlaySettings(tag, Vector3.zero, Quaternion.identity));
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 헤이스트 / 대시
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// CharacterAnimator.PlayHaste / StopHaste(false): 캐릭터 VFXComponent 풀의 "Haste"를 Play(태그, Animator 위치, identity)
    /// → VFX_Haste.Begin(Animator Transform, 본체 SpriteRenderer) → 버프가 끝나면 End(남은 입자 0.6초 뒤 스스로 반환).
    /// 캐릭터는 대역(SkillSpriteStandIn)이고 bRun이면 달리는 경로를 따라 움직인다.
    /// </summary>
    public class SkillHasteCase : VfxGoldenCase
    {
        private readonly bool bRun;
        private SkillSpriteStandIn character;
        private VFX_Haste haste;

        public SkillHasteCase(bool _bRun)
        {
            bRun = _bRun;
        }

        public override string Name => "Skill_Haste_" + (bRun ? "Run" : "Idle");
        public override int Frames => bRun ? 210 : 175;
        public override Vector2 ViewCenter => bRun ? new Vector2(0.0f, 0.4f) : new Vector2(0.0f, 0.6f);
        public override Vector2 ViewSize => bRun ? new Vector2(9.0f, 6.0f) : new Vector2(5.0f, 4.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            character = new SkillSpriteStandIn(_context, SkillGoldenUtil.CharacterPrefabPath, bRun ? new Vector3(-3.0f, 0.0f, 0.0f) : Vector3.zero);
            Transform animator = character.Find("Animator");
            SkillPrefabPool pool = SkillPrefabPool.Create(_context, SkillGoldenUtil.CharacterPrefabPath, "Haste");

            ParticleSystem effect = pool.Play("Haste", animator.position, Quaternion.identity);
            haste = effect.GetComponentInChildren<VFX_Haste>(true);
            haste.Begin(animator, animator.GetComponent<SpriteRenderer>());
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (bRun && _frame <= 180)
            {
                // 초당 약 4유닛으로 오른쪽 → 위로 꺾었다가 내려오는 경로
                float t = _frame / 60.0f;
                character.Root.position = new Vector3(-3.0f + t * 2.0f, Mathf.Sin(t * 2.0f) * 1.2f, 0.0f);
            }

            int endFrame = bRun ? 150 : 120;
            if (endFrame == _frame && haste.gameObject.activeInHierarchy) haste.End();
        }
    }

    /// <summary>
    /// Character 대시 시작 → CharacterAnimator.PlayDashKickoff: 캐릭터 VFXComponent 풀의 "DashKickoff"를 Play(태그, Animator 위치, identity)
    /// → SetSortingSettings(본체 소팅) → SetDash(방향, 거리 = 3칸 x 칸 등각 길이 / 방향 등각 길이, 0.18초) → 대시 끝 FinishDashKickoff(멈춘 위치).
    /// blockFrame이 0보다 크면 그 프레임에 벽에 막혀 일찍 멈춘다.
    /// </summary>
    public class SkillDashKickoffCase : VfxGoldenCase
    {
        private const float DashDuration = 0.18f;
        private const float DashDistanceTiles = 3.0f;
        // 그리드 축 한 칸(0.5, 0.25)의 등각 길이(세로 x 2)
        private const float CellStep = 0.70710678f;

        private readonly string variant;
        private readonly Vector2 direction;
        private readonly int blockFrame;
        private SkillSpriteStandIn character;
        private Transform animator;
        private VFX_DashKickoff kickoff;
        private float distance;
        private bool bFinished;

        public SkillDashKickoffCase(string _variant, Vector2 _direction, int _blockFrame)
        {
            variant = _variant;
            direction = _direction;
            blockFrame = _blockFrame;
        }

        public override string Name => "Skill_DashKickoff_" + variant;
        public override int Frames => 60;
        public override Vector2 ViewCenter => new Vector2(direction.x, direction.y + 0.5f);
        public override Vector2 ViewSize => new Vector2(7.0f, 5.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            character = new SkillSpriteStandIn(_context, SkillGoldenUtil.CharacterPrefabPath, Vector3.zero);
            animator = character.Find("Animator");
            SpriteRenderer baseRenderer = animator.GetComponent<SpriteRenderer>();
            SkillPrefabPool pool = SkillPrefabPool.Create(_context, SkillGoldenUtil.CharacterPrefabPath, "DashKickoff");

            // Character.GetDashWorldDistance
            float isoLength = Mathf.Sqrt(direction.x * direction.x + 4.0f * direction.y * direction.y);
            distance = DashDistanceTiles * CellStep / isoLength;

            ParticleSystem effect = pool.Play("DashKickoff", animator.position, Quaternion.identity);
            if (null != baseRenderer) pool.Component.SetSortingSettings(effect, baseRenderer.sortingLayerID, baseRenderer.sortingOrder);

            kickoff = effect.GetComponentInChildren<VFX_DashKickoff>(true);
            kickoff.SetDash(direction, distance, DashDuration);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (true == bFinished) return;

            float t = Mathf.Clamp01(_frame / 60.0f / DashDuration);
            bool bBlocked = 0 < blockFrame && _frame >= blockFrame;
            if (false == bBlocked) character.Root.position = (Vector3)(direction * (distance * t));

            if (true == bBlocked || 1.0f <= t)
            {
                bFinished = true;
                if (kickoff.gameObject.activeInHierarchy) kickoff.Finish(animator.position);
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 과열 최대 물결
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// InDungeonObjectManager의 과열 최대 순간: StageOverheatBurst.Play(캐릭터 발밑). 처음 쓸 때 만들어지는 "[StageOverheatBurst]" 오브젝트를
    /// 해시·정리 대상에 올린다(정리되면 OnDestroy가 전역값을 0으로 돌린다). 로그라이크 모드(GameModeContext.IsRoguelike)면 아무것도 그리지 않는다.
    /// </summary>
    public class SkillStageOverheatBurstCase : VfxGoldenCase
    {
        public override string Name => "Skill_StageOverheatBurst";
        public override int Frames => 90;
        public override Vector2 ViewSize => new Vector2(32.0f, 18.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            StageOverheatBurst.Play(Vector3.zero);
            GameObject go = GameObject.Find("[StageOverheatBurst]");
            if (null != go) _context.Track(go);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 스텔라 / 오프로드
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// InDungeonVFXManager.PlayStellaBurstVFX (RoguelikeSkillVfxPlayer.Play StarBlast / StarBlastRepeat): 풀 재생("FlyingItem", Order(위치) + 40) → SetRadius(반경).
    /// 반경은 stellaBlastRadius 2.2(범위 증가 반영 시 더 큼). Repeat는 stellaBlastRepeatDelay 0.2초 뒤 같은 자리에 한 번 더 터져 겹침 축소(activeBurstCount) 경로를 탄다.
    /// </summary>
    public class SkillStellaBurstCase : VfxGoldenCase
    {
        private const int RepeatFrame = 12;

        private readonly string variant;
        private readonly float radius;
        private readonly bool bRepeat;
        private readonly Vector2 viewSize;

        public SkillStellaBurstCase(string _variant, float _radius, bool _bRepeat, Vector2 _viewSize)
        {
            variant = _variant;
            radius = _radius;
            bRepeat = _bRepeat;
            viewSize = _viewSize;
        }

        public override string Name => "Skill_StellaBurst_" + variant;
        public override int Frames => bRepeat ? 200 : 185;
        public override Vector2 ViewCenter => new Vector2(0.0f, 2.0f);
        public override Vector2 ViewSize => viewSize;

        public override void Setup(VfxGoldenContext _context)
        {
            PlayBurst(_context);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (bRepeat && RepeatFrame == _frame) PlayBurst(_context);
        }

        private void PlayBurst(VfxGoldenContext _context)
        {
            Vector3 position = Vector3.zero;
            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("StellaBurstEffect", position, Quaternion.identity, "FlyingItem", SkillGoldenUtil.RoguelikeOrder(position) + 40, null));
            VFX_StellaBurst burst = effect.GetComponentInChildren<VFX_StellaBurst>(true);
            burst.SetRadius(radius);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayStellaGaugeVFX (RoguelikeSkillVfxPlayer.PlayStarOverlay): 풀 재생("FlyingItem", Order + 50) → SetSortingOrder → Begin(수명).
    /// bEndEarly면 별이 터질 때처럼 EndStarOverlay → End()(퇴장 연출), 아니면 수명이 다해 스스로 퇴장한다. 수명은 프레임 예산 때문에 2.5초로 줄였다.
    /// </summary>
    public class SkillStellaGaugeCase : VfxGoldenCase
    {
        private const int EndFrame = 90;

        private readonly bool bEndEarly;
        private VFX_StellaDurationGauge gauge;

        public SkillStellaGaugeCase(bool _bEndEarly)
        {
            bEndEarly = _bEndEarly;
        }

        public override string Name => "Skill_StellaGauge_" + (bEndEarly ? "EndEarly" : "Expire");
        public override int Frames => bEndEarly ? 140 : 200;
        public override Vector2 ViewCenter => new Vector2(0.0f, 1.0f);
        public override Vector2 ViewSize => new Vector2(4.0f, 4.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            Vector3 position = Vector3.zero;
            int order = SkillGoldenUtil.RoguelikeOrder(position) + 50;
            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("StellaGaugeEffect", position, Quaternion.identity, "FlyingItem", order, null));
            gauge = effect.GetComponentInChildren<VFX_StellaDurationGauge>(true);
            gauge.SetSortingOrder(order);
            gauge.Begin(bEndEarly ? 12.0f : 2.5f);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (bEndEarly && EndFrame == _frame && gauge.isActiveAndEnabled) gauge.End();
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayOffroadLaneVFX (RoguelikeSkillController.PickPendingOffroadLanes → 발동 때 Fire(걷힘 시간), 웨이브 종료 때 Cancel):
    /// 풀 재생("FlyingItem", 0) → Begin(출발, 도착, offroadHitRadius 0.9) → SetRemaining(발동까지 남은 시간).
    /// TwoLanes는 두 경로가 정적 칸 창(activeLanes)을 함께 쓰며 교차하고, offroadVolleyStagger만큼 어긋나 발동한다.
    /// </summary>
    public class SkillOffroadLaneCase : VfxGoldenCase
    {
        public enum EMode
        {
            Fire,
            Cancel,
            TwoLanes,
        }

        private const float HalfWidth = 0.9f;

        private readonly EMode mode;
        private VFX_OffroadLane laneA;
        private VFX_OffroadLane laneB;

        public SkillOffroadLaneCase(EMode _mode)
        {
            mode = _mode;
        }

        public override string Name => "Skill_OffroadLane_" + mode;
        public override int Frames => EMode.Cancel == mode ? 100 : (EMode.Fire == mode ? 260 : 240);
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.3f);
        public override Vector2 ViewSize => new Vector2(14.0f, 9.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            switch (mode)
            {
                case EMode.Fire:
                    laneA = BeginLane(_context, new Vector3(-5.0f, -2.5f, 0.0f), new Vector3(5.0f, 2.5f, 0.0f), 2.0f);
                    break;
                case EMode.Cancel:
                    laneA = BeginLane(_context, new Vector3(-5.0f, 2.5f, 0.0f), new Vector3(5.0f, -2.5f, 0.0f), 3.0f);
                    break;
                case EMode.TwoLanes:
                    laneA = BeginLane(_context, new Vector3(-5.0f, -2.5f, 0.0f), new Vector3(5.0f, 2.5f, 0.0f), 1.5f);
                    laneB = BeginLane(_context, new Vector3(-5.0f, 2.5f, 0.0f), new Vector3(5.0f, -2.5f, 0.0f), 1.6f);
                    break;
            }
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            switch (mode)
            {
                case EMode.Fire:
                    if (120 == _frame) FireLane(laneA, 1.2f);
                    break;
                case EMode.Cancel:
                    if (80 == _frame && laneA.isActiveAndEnabled) laneA.Cancel();
                    break;
                case EMode.TwoLanes:
                    if (90 == _frame) FireLane(laneA, 1.0f);
                    if (96 == _frame) FireLane(laneB, 1.0f);
                    break;
            }
        }

        private static VFX_OffroadLane BeginLane(VfxGoldenContext _context, Vector3 _from, Vector3 _to, float _remaining)
        {
            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("OffroadLaneEffect", _from, Quaternion.identity, "FlyingItem", 0, null));
            VFX_OffroadLane lane = effect.GetComponentInChildren<VFX_OffroadLane>(true);
            lane.Begin(_from, _to, HalfWidth);
            lane.SetRemaining(_remaining);
            return lane;
        }

        private static void FireLane(VFX_OffroadLane _lane, float _clearSeconds)
        {
            if (null != _lane && _lane.isActiveAndEnabled) _lane.Fire(_clearSeconds);
        }
    }
}

#endif
