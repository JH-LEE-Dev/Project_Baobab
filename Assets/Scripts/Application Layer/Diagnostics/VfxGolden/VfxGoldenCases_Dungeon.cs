// VFX 골든 캡처 케이스 묶음 (Dungeon). 규칙은 VfxGoldenCases.cs 머리 주석을 보세요. 에디터 전용.
//
// [이 묶음의 공통 전제]
// - 나무 하나는 (0, 0)에 서 있다고 본다. Tree.prefab 기준 topRoot = (0, 0.75), bottomRoot = (0, 0.05).
// - 나무 정렬은 CustomSortable(offset 0, precision 100)이 밑동 렌더러를 0으로 두고 TreeVisualComponent.UpdateSortingOrder가
//   쌓는 값이다: topShield 5, topHighlight 7, GetFrontEffectSortingOrder() 9. 나무가 필요 없는 케이스는 이 값을 상수로 쓴다.
// - TreeVisualComponent/TreeObj가 꼭 필요한 케이스만 실제 Tree.prefab을 만들어 게임과 같은 정렬 초기화를 거친다(SpawnTree).
// - 새 케이스는 Append의 맨 끝에만 덧붙인다(시작 프레임 번호가 목록 순서로 정해진다).
#if UNITY_EDITOR

using System.Collections.Generic;
using System.Reflection;
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;

namespace Diagnostics.VfxGolden
{
    public static class VfxGoldenCasesDungeon
    {
        public static void Append(List<VfxGoldenCase> _cases)
        {
            Vector3 top = DungeonGolden.TreeTop;
            Vector3 bottom = DungeonGolden.TreeBottom;

            // 첫 인사 / 약점 타격 (InDungeonVFXManager.PlayHelloHitVFX / PlayWeakPointHitVFX)
            _cases.Add(new DungeonHelloHitCase("Dungeon_HelloHit_Reply", true, 1));
            _cases.Add(new DungeonHelloHitCase("Dungeon_HelloHit_NoReply", false, -1));
            _cases.Add(new DungeonWeakPointHitCase("Dungeon_WeakPointHit_Expose", VFX_WeakPointHit.ModeExpose, 1));
            _cases.Add(new DungeonWeakPointHitCase("Dungeon_WeakPointHit_Strike", VFX_WeakPointHit.ModeStrike, -1));

            // 보석 단계 연출 (PlayGemTransformBurst / PlayGemDeathBurst)
            _cases.Add(new DungeonGemTierBurstCase("Dungeon_GemTierBurst_Transform_T1", VFX_GemTierBurst.ModeTransform, 1));
            _cases.Add(new DungeonGemTierBurstCase("Dungeon_GemTierBurst_Transform_T3", VFX_GemTierBurst.ModeTransform, 3));
            _cases.Add(new DungeonGemTierBurstCase("Dungeon_GemTierBurst_Death_T2", VFX_GemTierBurst.ModeDeath, 2));
            _cases.Add(new DungeonGemTierBurstCase("Dungeon_GemTierBurst_Death_T3", VFX_GemTierBurst.ModeDeath, 3));

            // 보석 원목 줍기 / 잔광 (PlayGemLogPickup / PlayGemLogTrails)
            _cases.Add(new DungeonGemLogPickupCase("Dungeon_GemLogPickup_T1", 1, false));
            _cases.Add(new DungeonGemLogPickupCase("Dungeon_GemLogPickup_T3_Stack", 3, true));
            _cases.Add(new DungeonGemLogTrailCase("Dungeon_GemLogTrail_T2", 2, false));
            _cases.Add(new DungeonGemLogTrailCase("Dungeon_GemLogTrail_T3", 3, false));
            _cases.Add(new DungeonGemLogTrailCase("Dungeon_GemLogTrail_T3_Culling", 3, true));

            // 화상 / 열기 방출
            _cases.Add(new DungeonTreeBurnCase("Dungeon_TreeBurn_Loop", false));
            _cases.Add(new DungeonTreeBurnCase("Dungeon_TreeBurn_Rekindle", true));
            // InDungeonVFXManager.PlayTreeHeatEmitVFX: 밑동 + (0, 0.5), 밑동 회전, topHighlight + 1 (VFX_TreeHeatBurst 프리팹)
            _cases.Add(new DungeonPooledCase("Dungeon_TreeHeatBurst", 170, new Vector2(0.0f, 1.0f), new Vector2(12.0f, 10.0f),
                new VFXPlaySettings("TreeHeatEmitEffect", bottom + new Vector3(0.0f, 0.5f, 0.0f), Quaternion.identity, DungeonGolden.TopHighlightOrder + 1, null)));

            // 발현 낙인 (PlayManifestationBrandStampVFX / BeginBrandStarWrap)
            _cases.Add(new DungeonBrandStampCase("Dungeon_BrandStampBurst", false));
            _cases.Add(new DungeonBrandStampCase("Dungeon_BrandStampBurst_Culling", true));
            _cases.Add(new DungeonBrandStarWrapCase("Dungeon_BrandStarWrap_End"));

            // 3스테이지 배리어 (OnTreeBarrierBlocked / OnTreeBarrierBroken)
            _cases.Add(new DungeonBarrierSparkCase("Dungeon_BarrierSpark"));
            _cases.Add(new DungeonTreeBarrierHitCase("Dungeon_TreeBarrier_HitWithSpark"));
            _cases.Add(new DungeonTreeBarrierBreakCase("Dungeon_TreeBarrier_BreakWithShatter"));

            // 드론 (Drone.PlayAtkHitVfx / PlayChargingVfx / PlayChainZap)
            _cases.Add(new DungeonLaserHitCase("Dungeon_LaserHit_Normal", false, false));
            _cases.Add(new DungeonLaserHitCase("Dungeon_LaserHit_Overheat", true, false));
            _cases.Add(new DungeonLaserHitCase("Dungeon_LaserHit_Culling", false, true));
            _cases.Add(new DungeonChargeVortexCase("Dungeon_ChargeVortex_Release", false, true));
            _cases.Add(new DungeonChargeVortexCase("Dungeon_ChargeVortex_OverheatCancel", true, false));
            _cases.Add(new DungeonDroneLaserCase("Dungeon_DroneLaser_Chain"));

            // 열기 예고 / 보석 변신 / 포자 폭발 / 별똥별
            _cases.Add(new DungeonTreeHeatIndicatorCase("Dungeon_TreeHeatIndicator"));
            _cases.Add(new DungeonTreeTransformCase("Dungeon_TreeTransformVFX_T1", 1));
            _cases.Add(new DungeonTreeTransformCase("Dungeon_TreeTransformVFX_T3", 3));
            _cases.Add(new DungeonSporeExplosionCase("Dungeon_SporeExplosionVFX_Burst"));
            _cases.Add(new DungeonShootingStarCase("Dungeon_ShootingStarVFX_Impact"));

            // 풀 파티클: 보석 피격 (PlayTreeGemHitVFX: top, "Objects", topHighlight + 1)
            Vector2 gemView = new Vector2(8.0f, 8.0f);
            Vector2 gemCenter = new Vector2(0.0f, 0.75f);
            _cases.Add(new DungeonPooledCase("Dungeon_RareImpact_Gold", 75, gemCenter, gemView,
                new VFXPlaySettings("RareImpact_Gold", top, Quaternion.identity, DungeonGolden.TreeSortingLayerName, DungeonGolden.TopHighlightOrder + 1, null)));
            _cases.Add(new DungeonPooledCase("Dungeon_RareImpact_Diamond", 75, gemCenter, gemView,
                new VFXPlaySettings("RareImpact_Diamond", top, Quaternion.identity, DungeonGolden.TreeSortingLayerName, DungeonGolden.TopHighlightOrder + 1, null)));
            _cases.Add(new DungeonPooledCase("Dungeon_RareImpact_Rainbow", 75, gemCenter, gemView,
                new VFXPlaySettings("RareImpact_Rainbow", top, Quaternion.identity, DungeonGolden.TreeSortingLayerName, DungeonGolden.TopHighlightOrder + 1, null)));

            // 풀 파티클: 포자막 파괴 (PlayShieldBrokenVFX: top + (0, shieldBrokenVfxYOffset -0.5), topShield + 1)
            Vector3 shieldPos = top + new Vector3(0.0f, DungeonGolden.ShieldBrokenVfxYOffset, 0.0f);
            _cases.Add(new DungeonPooledCase("Dungeon_SporeShieldBroken", 110, new Vector2(0.0f, 0.5f), new Vector2(10.0f, 10.0f),
                new VFXPlaySettings("SporeShieldBrokenEffect", shieldPos, Quaternion.identity, DungeonGolden.TopShieldOrder + 1, null)));
            _cases.Add(new DungeonPooledCase("Dungeon_SporeShieldBroken_Bellpine", 110, new Vector2(0.0f, 0.5f), new Vector2(10.0f, 10.0f),
                new VFXPlaySettings("SporeShieldBrokenEffect_Bellpine", shieldPos, Quaternion.identity, DungeonGolden.TopShieldOrder + 1, null)));

            // 풀 파티클: 나무 사망 스테이지 변형. 공유 이미터가 없거나 Emit이 실패했을 때의 풀 경로(PlayTreeDeadEffectPart 폴백)
            Vector2 deadView = new Vector2(12.0f, 12.0f);
            Vector2 deadCenter = new Vector2(0.0f, 0.5f);
            ParticleColorSet sporeTop = DungeonGolden.GetTopVfxColor(TreeType.SporepuffTree);
            ParticleColorSet sporeBottom = DungeonGolden.GetBottomVfxColor(TreeType.SporepuffTree);
            ParticleColorSet starTop = DungeonGolden.GetTopVfxColor(TreeType.StarrootTree);
            ParticleColorSet starBottom = DungeonGolden.GetBottomVfxColor(TreeType.StarrootTree);
            _cases.Add(new DungeonPooledCase("Dungeon_TreeDead_Spore_Pool", 105, deadCenter, deadView,
                new VFXPlaySettings("TreeDeadEffect_SporeTop", top, Quaternion.identity, sporeTop.startColor, false, null),
                new VFXPlaySettings("TreeDeadEffect_SporeBottom", bottom, Quaternion.identity, sporeBottom.startColor, false, null)));
            _cases.Add(new DungeonPooledCase("Dungeon_TreeDead_Star_Pool", 100, deadCenter, deadView,
                new VFXPlaySettings("TreeDeadEffect_StarTop", top, Quaternion.identity, starTop.startColor, false, null),
                new VFXPlaySettings("TreeDeadEffect_StarBottom", bottom, Quaternion.identity, starBottom.startColor, false, null)));
            _cases.Add(new DungeonPooledCase("Dungeon_TreeDead_GemGold_Pool", 80, deadCenter, deadView,
                new VFXPlaySettings("TreeDeadEffect_GemGoldTop", top, Quaternion.identity),
                new VFXPlaySettings("TreeDeadEffect_GemGoldBottom", bottom, Quaternion.identity)));
            _cases.Add(new DungeonPooledCase("Dungeon_TreeDead_MagmaBlast_Pool", 80, deadCenter, deadView,
                new VFXPlaySettings("TreeDeadEffect_MagmaBlastTop", top, Quaternion.identity),
                new VFXPlaySettings("TreeDeadEffect_MagmaBlastBottom", bottom, Quaternion.identity)));

            // 공유 이미터(SharedBurstEmitter) 경로 (PlayTreeHitVFX / PlayTreeDeadVFX)
            _cases.Add(new DungeonSharedTreeHitCase("Dungeon_TreeHit_Shared"));
            _cases.Add(new DungeonSharedTreeDeadCase("Dungeon_TreeDead_Leaf_Shared", TreeType.OakTree));
            _cases.Add(new DungeonSharedTreeDeadCase("Dungeon_TreeDead_Magma_Shared", TreeType.CinderTree));
        }
    }

    /// <summary>
    /// 던전 묶음 케이스가 함께 쓰는 상수와 도구입니다. 게임 경로를 그대로 따라 하기 위한 값만 둡니다.
    /// </summary>
    public static class DungeonGolden
    {
        public const string InstallerPrefabPath = "Assets/Prefabs/Installer/Installer/GameInstaller.prefab";
        public const string TreePrefabPath = "Assets/Prefabs/Objects/Trees/Tree.prefab";
        public const string DronePrefabPath = "Assets/Prefabs/Objects/Drone/Drone.prefab";
        public const string DroneLaserPrefabPath = "Assets/Prefabs/VFX/Lightning/VFX_DroneLaser.prefab";
        public const string LogItemPrefabPath = "Assets/Prefabs/Objects/Item/LogItem.prefab";
        public const string LogItemTypeDataBasePath = "Assets/Scriptable Obj/ItemData/LogItemData/LogItemTypeDataBase.asset";
        public const string TreeVisualDataBasePath = "Assets/Scriptable Obj/TreeVisualData/Tree Visual Data Base.asset";
        public const string BrandStarWrapPrefabPath = "Assets/Prefabs/VFX/Constellation/VFX_BrandStarWrap.prefab";
        public const string TreeTransformPrefabPath = "Assets/Prefabs/VFX/Tree/TreeTransformVFX.prefab";
        public const string SporeExplosionPrefabPath = "Assets/Prefabs/VFX/Spore/SporeExplosionVFX.prefab";
        public const string ShootingStarPrefabPath = "Assets/Prefabs/VFX/ShootingStar/ShootingStarVFX.prefab";
        public const string TreeHeatIndicatorMaterialPath = "Assets/Materials/Indicators/TreeHeatIndicator.mat";

        // InDungeonVFXManager 상수·직렬화 값(GameInstaller.prefab)
        public const string TreeSortingLayerName = "Objects";
        public const string FlyingItemSortingLayerName = "FlyingItem";
        public const string IndicatorSortingLayerName = "Indicators";
        public const float ShieldBrokenVfxYOffset = -0.5f;
        public const float TreeTransformVfxYOffset = -0.25f;
        public const float TreeHeatIndicatorRadius = 1.5f;
        public const float TreeHeatIndicatorLeadTime = 1.5f;
        public const int TreeHeatIndicatorSortingOrder = -1;
        public const int SharedBurstEmitterCapacity = 120;

        // (0, 0)에 선 나무의 정렬(머리 주석 참고)
        public const int TopShieldOrder = 5;
        public const int TopHighlightOrder = 7;
        public const int FrontEffectOrder = 9;

        public static readonly Vector3 TreeTop = new Vector3(0.0f, 0.75f, 0.0f);
        public static readonly Vector3 TreeBottom = new Vector3(0.0f, 0.05f, 0.0f);

        private static readonly FieldInfo poolListField = typeof(VFXComponent).GetField("vfxPoolDataList", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Tree.prefab을 만들고 게임과 같은 정렬 초기화(TreeVisualComponent.Initialize → CustomSortable 갱신 → UpdateSortingOrder)를 거칩니다.
        /// TreeObj.Initialize는 환경 제공자(그림자·체력)가 필요해 부르지 않습니다.
        /// </summary>
        public static TreeObj SpawnTree(VfxGoldenContext _context, Vector3 _position)
        {
            GameObject go = _context.Spawn(TreePrefabPath, _position);
            TreeObj tree = go.GetComponent<TreeObj>();
            CustomSortable sortable = go.GetComponent<CustomSortable>();
            if (null != tree.treeVisualComponent)
            {
                tree.treeVisualComponent.Initialize(null, sortable);
                if (null != sortable) sortable.ManualLateUpdate();
                tree.treeVisualComponent.UpdateSortingOrder();
            }
            return tree;
        }

        public static ParticleColorSet GetTopVfxColor(TreeType _type)
        {
            TreeVisualDataBase db = AssetDatabase.LoadAssetAtPath<TreeVisualDataBase>(TreeVisualDataBasePath);
            return null != db ? db.Get(_type).topVfxColor : DefaultColor();
        }

        public static ParticleColorSet GetBottomVfxColor(TreeType _type)
        {
            TreeVisualDataBase db = AssetDatabase.LoadAssetAtPath<TreeVisualDataBase>(TreeVisualDataBasePath);
            return null != db ? db.Get(_type).bottomVfxColor : DefaultColor();
        }

        private static ParticleColorSet DefaultColor()
        {
            return new ParticleColorSet { startColor = new ParticleSystem.MinMaxGradient(Color.white), overrideChildrenColor = true };
        }

        /// <summary>프리팹 안 VFXComponent들에서 태그 설정을 찾습니다(읽기만 함).</summary>
        public static VFXPoolData FindPoolData(string _prefabPath, string _tag)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (null == prefab) return null;

            VFXComponent[] components = prefab.GetComponentsInChildren<VFXComponent>(true);
            for (int i = 0; i < components.Length; i++)
            {
                List<VFXPoolData> list = poolListField.GetValue(components[i]) as List<VFXPoolData>;
                if (null == list) continue;
                for (int k = 0; k < list.Count; k++)
                {
                    if (null != list[k] && _tag == list[k].VfxTag) return list[k];
                }
            }
            return null;
        }

        /// <summary>
        /// GameInstaller 밖 프리팹(Drone.prefab)의 풀 설정으로 전용 VFXComponent를 Root 아래에 만듭니다.
        /// VfxGoldenContext.PlayPooled와 같은 방식(설정 복사, 예열 0)입니다.
        /// </summary>
        public static VFXComponent CreatePool(VfxGoldenContext _context, string _prefabPath, string _tag)
        {
            VFXPoolData source = FindPoolData(_prefabPath, _tag);
            if (null == source)
            {
                throw new System.ArgumentException("[VfxGolden] " + _prefabPath + "에 없는 VFX 태그입니다: " + _tag);
            }

            VFXPoolData copy = new VFXPoolData();
            foreach (FieldInfo field in typeof(VFXPoolData).GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
            {
                field.SetValue(copy, field.GetValue(source));
            }
            typeof(VFXPoolData).GetField("initialPoolSize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(copy, 0);

            GameObject go = new GameObject("[Pool] " + _tag);
            go.SetActive(false);
            go.transform.SetParent(_context.Root, false);
            VFXComponent pool = go.AddComponent<VFXComponent>();
            typeof(VFXComponent).GetField("initializeOnAwake", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pool, false);
            poolListField.SetValue(pool, new List<VFXPoolData> { copy });
            go.SetActive(true);
            pool.Initialize();
            return pool;
        }

        /// <summary>CreatePool로 만든 풀에서 재생합니다(시드 고정 → Random 재고정 → VFXComponent.Play).</summary>
        public static ParticleSystem PlayFromPool(VfxGoldenContext _context, VFXComponent _pool, VFXPlaySettings _settings)
        {
            ParticleSystem effect = _pool.Get(_settings.VfxTag);
            if (null == effect)
            {
                throw new System.InvalidOperationException("[VfxGolden] 풀에서 꺼내지 못했습니다: " + _settings.VfxTag);
            }

            _context.FixParticleSeeds(effect.gameObject);
            Random.InitState((int)_context.CaseSeed);
            _pool.Play(effect, _settings);
            // 부모 없이 재생하면 씬 최상위로 분리되므로 그 루트를 등록한다. Root 아래 부모를 받으면 transform.root가 러너 자신이라 등록하지 않는다.
            if (false == effect.transform.IsChildOf(_context.Root)) _context.Track(effect.transform.root.gameObject);
            return effect;
        }

        /// <summary>
        /// InDungeonVFXManager.CreateSharedBurstEmitter와 같은 조건·용량으로 공유 이미터를 만듭니다. 호환되지 않으면 null(게임도 풀 경로로 감).
        /// 만든 뒤 시드를 고정하고 생성자가 했던 대로 재생 상태로 되돌립니다. 루트는 Track해 케이스 끝에 지워집니다.
        /// </summary>
        public static SharedBurstEmitter CreateSharedEmitter(VfxGoldenContext _context, string _tag)
        {
            VFXPoolData data = FindPoolData(InstallerPrefabPath, _tag);
            if (null == data || null == data.EffectPrefab) return null;
            if (false == SharedBurstEmitter.IsCompatible(data.EffectPrefab)) return null;

            int maxPoolSize = data.AllowDynamicExpansion ? data.MaxPoolSize : data.InitialPoolSize;
            int capacity = Mathf.Max(1, Mathf.Max(maxPoolSize, SharedBurstEmitterCapacity));
            SharedBurstEmitter emitter = new SharedBurstEmitter(data.EffectPrefab, capacity, "SharedEmitter_" + _tag);

            _context.Track(emitter.RootObject);
            _context.FixParticleSeeds(emitter.RootObject);
            ParticleSystem root = emitter.RootObject.GetComponent<ParticleSystem>();
            if (null != root) root.Play(true);
            return emitter;
        }

        /// <summary>InDungeonVFXManager.PlayTreeDeadEffectPart: 공유 이미터로 재생하고, 없거나 실패하면 풀 경로로 재생합니다.</summary>
        public static void PlayTreeDeadEffectPart(VfxGoldenContext _context, SharedBurstEmitter _emitter, string _tag, Vector3 _position, Quaternion _rotation, bool _bTreeColor, ParticleColorSet _color)
        {
            if (null != _emitter && _emitter.Emit(_position, _rotation, _bTreeColor, _bTreeColor ? _color.startColor : default(ParticleSystem.MinMaxGradient), false))
            {
                return;
            }

            _context.PlayPooled(_bTreeColor
                ? new VFXPlaySettings(_tag, _position, _rotation, _color.startColor, false, null)
                : new VFXPlaySettings(_tag, _position, _rotation));
        }

        /// <summary>InDungeonVFXManager.PlayTreeHitVFX / PlayTreeDeadVFX 기본 경로: 나무 색(자식 덮어쓰기 포함)으로 공유 이미터 → 풀 폴백.</summary>
        public static void PlayTreeColoredPart(VfxGoldenContext _context, SharedBurstEmitter _emitter, string _tag, Vector3 _position, Quaternion _rotation, ParticleColorSet _color)
        {
            if (null != _emitter && _emitter.Emit(_position, _rotation, true, _color.startColor, _color.overrideChildrenColor))
            {
                return;
            }

            _context.PlayPooled(new VFXPlaySettings(_tag, _position, _rotation, _color.startColor, _color.overrideChildrenColor, null));
        }

        /// <summary>캡처 카메라를 (x, y)로 옮깁니다(z는 러너 값 -10 유지).</summary>
        public static void MoveCamera(VfxGoldenContext _context, float _x, float _y)
        {
            Transform cameraTransform = _context.CaptureCamera.transform;
            cameraTransform.position = new Vector3(_x, _y, cameraTransform.position.z);
        }
    }

    /// <summary>이름·프레임·화면 범위를 생성자로 받는 던전 묶음 케이스 베이스입니다.</summary>
    public abstract class DungeonGoldenCase : VfxGoldenCase
    {
        private readonly string caseName;
        private readonly int frames;
        private readonly Vector2 viewCenter;
        private readonly Vector2 viewSize;

        protected DungeonGoldenCase(string _caseName, int _frames, Vector2 _viewCenter, Vector2 _viewSize)
        {
            caseName = _caseName;
            frames = _frames;
            viewCenter = _viewCenter;
            viewSize = _viewSize;
        }

        public override string Name => caseName;
        public override int Frames => frames;
        public override Vector2 ViewCenter => viewCenter;
        public override Vector2 ViewSize => viewSize;
    }

    /// <summary>
    /// 풀 경로(VFXComponent.Play)로 설정 하나 이상을 같은 프레임에 재생합니다. 설정값은 Append에서 게임 호출부와 같은 생성자로 만듭니다.
    /// </summary>
    public class DungeonPooledCase : DungeonGoldenCase
    {
        private readonly VFXPlaySettings[] settings;

        public DungeonPooledCase(string _caseName, int _frames, Vector2 _viewCenter, Vector2 _viewSize, params VFXPlaySettings[] _settings)
            : base(_caseName, _frames, _viewCenter, _viewSize)
        {
            settings = _settings;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            for (int i = 0; i < settings.Length; i++)
            {
                _context.PlayPooled(settings[i]);
            }
        }
    }

    /// <summary>InDungeonVFXManager.PlayHelloHitVFX: 밑동에서 재생, "FlyingItem" 층에 밑동 y 순서(-y*100 + 100).</summary>
    public class DungeonHelloHitCase : DungeonGoldenCase
    {
        private readonly bool bReply;
        private readonly int awayDirX;

        public DungeonHelloHitCase(string _caseName, bool _bReply, int _awayDirX)
            : base(_caseName, 45, new Vector2(0.0f, 0.75f), new Vector2(8.0f, 8.0f))
        {
            bReply = _bReply;
            awayDirX = _awayDirX;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            VFX_HelloHit vfx = VFX_HelloHit.Create(_context.Root);
            Vector3 bottom = DungeonGolden.TreeBottom;
            vfx.Play(bottom, DungeonGolden.TreeTop, bReply, awayDirX, DungeonGolden.FlyingItemSortingLayerName, -Mathf.RoundToInt(bottom.y * 100f) + 100);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayWeakPointHitVFX: 관통은 "FlyingItem" 층에 밑동 y 순서, 노출은 "Objects" 층 GetFrontEffectSortingOrder() + 1.
    /// </summary>
    public class DungeonWeakPointHitCase : DungeonGoldenCase
    {
        private readonly int mode;
        private readonly int exitDirX;

        public DungeonWeakPointHitCase(string _caseName, int _mode, int _exitDirX)
            : base(_caseName, 45, new Vector2(0.0f, 0.25f), new Vector2(8.0f, 8.0f))
        {
            mode = _mode;
            exitDirX = _exitDirX;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            VFX_WeakPointHit vfx = VFX_WeakPointHit.Create(_context.Root);
            Vector3 bottom = DungeonGolden.TreeBottom;
            if (VFX_WeakPointHit.ModeStrike == mode)
            {
                vfx.Play(mode, bottom, exitDirX, DungeonGolden.FlyingItemSortingLayerName, -Mathf.RoundToInt(bottom.y * 100f) + 100);
            }
            else
            {
                vfx.Play(mode, bottom, exitDirX, DungeonGolden.TreeSortingLayerName, DungeonGolden.FrontEffectOrder + 1);
            }
        }
    }

    /// <summary>InDungeonVFXManager.PlayGemTransformBurst / PlayGemDeathBurst: Create(매니저) → Play(모드, 단계, top, bottom).</summary>
    public class DungeonGemTierBurstCase : DungeonGoldenCase
    {
        private readonly int mode;
        private readonly int tier;

        public DungeonGemTierBurstCase(string _caseName, int _mode, int _tier)
            : base(_caseName, 90, new Vector2(0.0f, 1.0f), new Vector2(10.0f, 10.0f))
        {
            mode = _mode;
            tier = _tier;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            VFX_GemTierBurst vfx = VFX_GemTierBurst.Create(_context.Root);
            vfx.Play(mode, tier, DungeonGolden.TreeTop, DungeonGolden.TreeBottom);
        }
    }

    /// <summary>
    /// InDungeonObjectManager.OnItemAcquired → InDungeonVFXManager.PlayGemLogPickup(character.transform, 단계).
    /// 연달아 줍기 변형은 걸어가는 캐릭터(앵커 이동) 위에서 다섯 번 주워 한 인스턴스에 쌓입니다.
    /// </summary>
    public class DungeonGemLogPickupCase : DungeonGoldenCase
    {
        private readonly int tier;
        private readonly bool bStack;
        private VFX_GemLogPickup vfx;
        private Transform anchor;

        public DungeonGemLogPickupCase(string _caseName, int _tier, bool _bStack)
            : base(_caseName, _bStack ? 100 : 60, new Vector2(0.5f, 0.5f), new Vector2(7.0f, 6.0f))
        {
            tier = _tier;
            bStack = _bStack;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            anchor = _context.CreateEmpty("Character", Vector3.zero).transform;
            vfx = VFX_GemLogPickup.Create(_context.Root);
            vfx.Play(anchor, tier);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (false == bStack) return;

            // 걷는 속도(약 1.2유닛/초)로 오른쪽으로 이동하며 원목을 연달아 줍는다
            if (40 >= _frame) anchor.position += new Vector3(0.02f, 0.0f, 0.0f);
            if (5 == _frame || 10 == _frame || 15 == _frame || 30 == _frame) vfx.Play(anchor, tier);
        }
    }

    /// <summary>
    /// InDungeonObjectManager.OnTreeDead(보석 나무) → LogItemController.SpawnLogItem(높이 배율) → InDungeonVFXManager.PlayGemLogTrails.
    /// 실제 LogItem.prefab을 Initialize/Launch하고, LogItemController.Update처럼 매 프레임 ManualUpdate를 부릅니다.
    /// 컬링 변형은 카메라가 멀리서 시작해 25프레임에 이펙트 쪽으로 순간이동합니다.
    /// </summary>
    public class DungeonGemLogTrailCase : DungeonGoldenCase
    {
        private const int LogCount = 3;
        private const float FarOffsetX = 24.0f;
        private const int CameraJumpFrame = 25;
        private static readonly Vector2 Center = new Vector2(0.0f, 0.8f);

        private readonly int tier;
        private readonly bool bCulling;
        private readonly List<LogItem> updatingLogs = new List<LogItem>(LogCount);

        public DungeonGemLogTrailCase(string _caseName, int _tier, bool _bCulling)
            : base(_caseName, 110, _bCulling ? new Vector2(Center.x + FarOffsetX, Center.y) : Center, new Vector2(7.0f, 6.0f))
        {
            tier = _tier;
            bCulling = _bCulling;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            updatingLogs.Clear();

            LogItemTypeDataBase db = AssetDatabase.LoadAssetAtPath<LogItemTypeDataBase>(DungeonGolden.LogItemTypeDataBasePath);
            LogItemTypeData typeData = db.Get(TreeType.OakTree);
            LogState logState = 3 <= tier ? LogState.Perfect : LogState.Advanced;
            // InDungeonObjectManager.GetGemLogHeightScale
            float heightScale = 3 <= tier ? 1.6f : 1.45f;
            Vector3 spawnPos = Vector3.zero;

            List<LogItem> launched = new List<LogItem>(LogCount);
            for (int i = 0; i < LogCount; i++)
            {
                LogItem log = _context.Spawn(DungeonGolden.LogItemPrefabPath, spawnPos).GetComponent<LogItem>();
                log.Initialize(typeData, typeData.color, logState, null);
                log.LogItemDeActivatedEvent += OnLogDeactivated;

                // LogItemController.SpawnLogItem과 같은 난수 순서·범위(물 타일 보정 제외)
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                float randomDist = Random.Range(1.25f, 1.75f);
                Vector3 endPos = spawnPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0.0f) * randomDist;
                float height = Random.Range(0.75f, 1.25f);
                float randomRotation = Random.Range(1, 3) * 360f * (Random.value > 0.5f ? 1f : -1f);
                log.Launch(spawnPos, endPos, height * heightScale, randomRotation, 1f / Mathf.Sqrt(heightScale));

                launched.Add(log);
                updatingLogs.Add(log);
            }

            // InDungeonVFXManager.PlayGemLogTrails: 원목마다 잔광 하나
            for (int i = 0; i < launched.Count; i++)
            {
                VFX_GemLogTrail trail = VFX_GemLogTrail.Create(_context.Root);
                trail.Play(launched[i], tier);
            }
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            for (int i = updatingLogs.Count - 1; i >= 0; i--)
            {
                updatingLogs[i].ManualUpdate(Time.deltaTime);
            }

            if (bCulling && CameraJumpFrame == _frame) DungeonGolden.MoveCamera(_context, Center.x, Center.y);
        }

        private void OnLogDeactivated(LogItem _log)
        {
            // LogItemController.LogItemDeActivated: 착지 출렁임이 끝나면 갱신 목록에서 뺀다
            updatingLogs.Remove(_log);
        }
    }

    /// <summary>
    /// TreeObj.RefreshBurnVfx → VFX_TreeBurn.SetBurning(나무 프리팹의 비활성 자식). 화상 3틱(0.5초 간격)쯤 타다가 꺼진다.
    /// 재점화 변형은 사그라드는 도중 다시 불붙는 경로(Extinguishing → Burning)를 지난다.
    /// </summary>
    public class DungeonTreeBurnCase : DungeonGoldenCase
    {
        private readonly bool bRekindle;
        private VFX_TreeBurn burn;

        public DungeonTreeBurnCase(string _caseName, bool _bRekindle)
            : base(_caseName, 200, new Vector2(0.0f, 1.0f), new Vector2(6.0f, 6.0f))
        {
            bRekindle = _bRekindle;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            TreeObj tree = DungeonGolden.SpawnTree(_context, Vector3.zero);
            burn = tree.GetComponentInChildren<VFX_TreeBurn>(true);
            burn.SetBurning(true);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (bRekindle)
            {
                if (50 == _frame) burn.SetBurning(false);
                if (62 == _frame) burn.SetBurning(true);
                if (130 == _frame) burn.SetBurning(false);
            }
            else if (120 == _frame)
            {
                burn.SetBurning(false);
            }
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayManifestationBrandStampVFX: 풀 재생(top, top 회전, topHighlight + 2). 자식 VFX_BrandStampBurst가 그린다.
    /// 컬링 변형은 카메라가 22.5유닛 떨어져 시작해 10~40프레임 동안 등속으로 다가온다.
    /// </summary>
    public class DungeonBrandStampCase : DungeonGoldenCase
    {
        private const float FarOffsetX = 22.5f;
        private const int MoveStartFrame = 10;
        private const int MoveFrames = 30;
        private static readonly Vector2 Center = new Vector2(0.0f, 0.75f);

        private readonly bool bCulling;

        public DungeonBrandStampCase(string _caseName, bool _bCulling)
            : base(_caseName, 80, _bCulling ? new Vector2(Center.x + FarOffsetX, Center.y) : Center, new Vector2(8.0f, 8.0f))
        {
            bCulling = _bCulling;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            _context.PlayPooled(new VFXPlaySettings("ManifestationBrandStampEffect", DungeonGolden.TreeTop, Quaternion.identity, DungeonGolden.TopHighlightOrder + 2, null));
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (false == bCulling || MoveStartFrame > _frame || MoveStartFrame + MoveFrames < _frame) return;

            // 프레임당 0.75유닛(2진수로 정확한 값) 등속
            float x = Center.x + FarOffsetX - (_frame - MoveStartFrame) * (FarOffsetX / MoveFrames);
            DungeonGolden.MoveCamera(_context, x, Center.y);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.BeginBrandStarWrap: 프리팹 인스턴스 → Begin(나무 비주얼, "Objects"). 끝은 RoguelikeSkillVfxPlayer.EndMarkWrap의 End()
    /// (퇴장 연출 뒤 ReturnToPoolEvent → 풀이 SetActive(false)).
    /// </summary>
    public class DungeonBrandStarWrapCase : DungeonGoldenCase
    {
        private VFX_BrandStarWrap wrap;

        public DungeonBrandStarWrapCase(string _caseName)
            : base(_caseName, 220, new Vector2(0.0f, 0.8f), new Vector2(6.0f, 6.0f))
        {
        }

        public override void Setup(VfxGoldenContext _context)
        {
            TreeObj tree = DungeonGolden.SpawnTree(_context, Vector3.zero);
            wrap = _context.Spawn(DungeonGolden.BrandStarWrapPrefabPath, Vector3.zero).GetComponentInChildren<VFX_BrandStarWrap>(true);
            wrap.ReturnToPoolEvent += OnReturned;
            wrap.Begin(tree.treeVisualComponent, DungeonGolden.TreeSortingLayerName);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (180 == _frame) wrap.End();
        }

        private static void OnReturned(VFX_BrandStarWrap _wrap)
        {
            // InDungeonVFXManager.OnReleaseBrandStarWrap
            _wrap.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayTreeBarrierSparkVFX 단독: 풀 재생(맞은 자리, "Objects", front + 1) → SetDirection(바깥 법선).
    /// 맞은 자리는 InDungeonObjectManager.OnTreeBarrierBlocked처럼 왼쪽 아래 캐릭터 가슴 높이에서 GetImpactPoint로 구한다.
    /// </summary>
    public class DungeonBarrierSparkCase : DungeonGoldenCase
    {
        public DungeonBarrierSparkCase(string _caseName)
            : base(_caseName, 70, new Vector2(0.0f, 0.7f), new Vector2(8.0f, 8.0f))
        {
        }

        public override void Setup(VfxGoldenContext _context)
        {
            Vector3 source = new Vector3(-1.2f, -0.4f, 0.0f) + new Vector3(0.0f, 0.45f, 0.0f);
            Vector3 impact = VFX_TreeBarrier.GetImpactPoint(Vector3.zero, source, out Vector2 normal);
            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("TreeBarrierSparkEffect", impact, Quaternion.identity, DungeonGolden.TreeSortingLayerName, DungeonGolden.FrontEffectOrder + 1, null));
            VFX_BarrierSpark spark = effect.GetComponentInChildren<VFX_BarrierSpark>(true);
            spark.SetDirection(normal);
        }
    }

    /// <summary>
    /// InDungeonObjectManager.OnTreeBarrierBlocked 두 번(왼쪽, 오른쪽 위): TreeObj.SetBarrier(true) → GetImpactPoint → TreeObj.PlayBarrierHit(VFX_TreeBarrier)
    /// + PlayTreeBarrierSparkVFX(front + 1, SetDirection).
    /// </summary>
    public class DungeonTreeBarrierHitCase : DungeonGoldenCase
    {
        private TreeObj tree;

        public DungeonTreeBarrierHitCase(string _caseName)
            : base(_caseName, 120, new Vector2(0.0f, 0.7f), new Vector2(8.0f, 8.0f))
        {
        }

        public override void Setup(VfxGoldenContext _context)
        {
            tree = DungeonGolden.SpawnTree(_context, Vector3.zero);
            tree.SetBarrier(true);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (5 == _frame) Block(_context, new Vector3(-1.2f, -0.4f, 0.0f));
            if (45 == _frame) Block(_context, new Vector3(1.0f, 0.6f, 0.0f));
        }

        private void Block(VfxGoldenContext _context, Vector3 _characterPos)
        {
            Vector3 root = tree.transform.position;
            Vector3 source = _characterPos + new Vector3(0.0f, 0.45f, 0.0f);
            Vector3 impact = VFX_TreeBarrier.GetImpactPoint(root, source, out Vector2 normal);
            tree.PlayBarrierHit(impact);

            ParticleSystem effect = _context.PlayPooled(new VFXPlaySettings("TreeBarrierSparkEffect", impact, Quaternion.identity, DungeonGolden.TreeSortingLayerName, tree.treeVisualComponent.GetFrontEffectSortingOrder() + 1, null));
            VFX_BarrierSpark spark = effect.GetComponentInChildren<VFX_BarrierSpark>(true);
            spark.SetDirection(normal);
        }
    }

    /// <summary>
    /// 별자리 발현 폭발이 배리어를 깸: TreeObj.BreakBarrier(폭발 중심) → VFX_TreeBarrier.PlayBreak(잔광 포함) → TreeBarrierBrokenEvent
    /// → InDungeonObjectManager.OnTreeBarrierBroken → PlayTreeBarrierShatterVFX(밑동, 맞은 자리, front + 1, SetImpact).
    /// </summary>
    public class DungeonTreeBarrierBreakCase : DungeonGoldenCase
    {
        private VfxGoldenContext context;
        private TreeObj tree;

        public DungeonTreeBarrierBreakCase(string _caseName)
            : base(_caseName, 120, new Vector2(0.0f, 0.8f), new Vector2(9.0f, 9.0f))
        {
        }

        public override void Setup(VfxGoldenContext _context)
        {
            context = _context;
            tree = DungeonGolden.SpawnTree(_context, Vector3.zero);
            tree.SetBarrier(true);
            tree.TreeBarrierBrokenEvent += OnBarrierBroken;
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (10 == _frame) tree.BreakBarrier(new Vector3(2.0f, 2.5f, 0.0f));
        }

        private void OnBarrierBroken(TreeObj _tree, Vector3 _impact)
        {
            ParticleSystem effect = context.PlayPooled(new VFXPlaySettings("TreeBarrierShatterEffect", _tree.transform.position, Quaternion.identity, DungeonGolden.TreeSortingLayerName, _tree.treeVisualComponent.GetFrontEffectSortingOrder() + 1, null));
            VFX_BarrierShatter shatter = effect.GetComponentInChildren<VFX_BarrierShatter>(true);
            shatter.SetImpact(_impact);
        }
    }

    /// <summary>
    /// Drone.PlayAtkHitVfx: Drone.prefab 풀("DroneAtkHit")에서 나무 top에 재생(소팅 오버라이드 없음) → SetOverheat → SetTint(Default).
    /// 컬링 변형은 카메라가 멀리서 시작해 15프레임에 이펙트 쪽으로 순간이동합니다.
    /// </summary>
    public class DungeonLaserHitCase : DungeonGoldenCase
    {
        private const float FarOffsetX = 24.0f;
        private const int CameraJumpFrame = 15;
        private static readonly Vector2 Center = new Vector2(0.0f, 0.75f);

        private readonly bool bOverheat;
        private readonly bool bCulling;

        public DungeonLaserHitCase(string _caseName, bool _bOverheat, bool _bCulling)
            : base(_caseName, 60, _bCulling ? new Vector2(Center.x + FarOffsetX, Center.y) : Center, new Vector2(6.0f, 6.0f))
        {
            bOverheat = _bOverheat;
            bCulling = _bCulling;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            VFXComponent pool = DungeonGolden.CreatePool(_context, DungeonGolden.DronePrefabPath, "DroneAtkHit");
            ParticleSystem effect = DungeonGolden.PlayFromPool(_context, pool, new VFXPlaySettings("DroneAtkHit", DungeonGolden.TreeTop, Quaternion.identity));
            VFX_LaserHit laserHit = effect.GetComponentInChildren<VFX_LaserHit>(true);
            laserHit.SetOverheat(bOverheat);
            laserHit.SetTint(EDroneLaserTint.Default);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (bCulling && CameraJumpFrame == _frame) DungeonGolden.MoveCamera(_context, Center.x, Center.y);
        }
    }

    /// <summary>
    /// Drone.PlayChargingVfx: Drone.prefab 풀("DroneCharging")에서 총구 위치에 드론 Visual 자식으로 재생 → SetOverheat → SetChargeDuration.
    /// UpdateChargingVfxPosition처럼 매 프레임 총구 위치와 정렬(본체 순서 + 1, 수평 방향)을 맞춘다.
    /// 발사 변형: 30프레임에 Release + 방출 정지(ReleaseChargingVfx), 취소 변형: 20프레임에 방출만 정지(StopChargingVfx(false)).
    /// 둘 다 최대 수명 + 0.2초 뒤 풀로 즉시 반환(UpdateChargingVfxFade).
    /// </summary>
    public class DungeonChargeVortexCase : DungeonGoldenCase
    {
        private const int CaseFrames = 110;
        private static readonly Vector3 DronePos = new Vector3(0.0f, 0.6f, 0.0f);
        private static readonly Vector3 MuzzlePos = new Vector3(0.25f, 0.75f, 0.0f);

        private readonly bool bOverheat;
        private readonly bool bRelease;
        private VFXComponent pool;
        private ParticleSystem effect;
        private VFX_ChargeVortex vortex;
        private ParticleSystemRenderer[] renderers;
        private int stopEmitFrame;
        private int returnFrame;

        public DungeonChargeVortexCase(string _caseName, bool _bOverheat, bool _bRelease)
            : base(_caseName, CaseFrames, new Vector2(0.25f, 0.75f), new Vector2(5.0f, 5.0f))
        {
            bOverheat = _bOverheat;
            bRelease = _bRelease;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            Transform droneVisual = _context.CreateEmpty("DroneVisual", DronePos).transform;
            pool = DungeonGolden.CreatePool(_context, DungeonGolden.DronePrefabPath, "DroneCharging");
            effect = DungeonGolden.PlayFromPool(_context, pool, new VFXPlaySettings("DroneCharging", MuzzlePos, Quaternion.identity, droneVisual));
            renderers = effect.GetComponentsInChildren<ParticleSystemRenderer>(true);
            vortex = effect.GetComponentInChildren<VFX_ChargeVortex>(true);
            vortex.SetOverheat(bOverheat);
            vortex.SetChargeDuration(0.5f);

            stopEmitFrame = bRelease ? 30 : 20;
            int fadeFrames = Mathf.CeilToInt((ComputeMaxLifetime(effect) + 0.2f) * 60.0f);
            returnFrame = Mathf.Min(stopEmitFrame + fadeFrames, CaseFrames - 16);
            ApplyPositionAndSorting();
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (null == effect) return;

            if (stopEmitFrame == _frame)
            {
                if (bRelease) vortex.Release();
                effect.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (returnFrame == _frame)
            {
                pool.Stop(effect, true);
                effect = null;
                return;
            }

            ApplyPositionAndSorting();
        }

        private void ApplyPositionAndSorting()
        {
            effect.transform.position = MuzzlePos;
            // CustomSortable.ComputeSortingOrder(드론 y, offset 0, precision 100) + 1(수평 방향은 본체 앞)
            int order = -Mathf.RoundToInt(DronePos.y * 100f) + 1;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (null != renderers[i]) renderers[i].sortingOrder = order;
            }
        }

        // Drone.ComputeChargingVfxMaxLifetime
        private static float ComputeMaxLifetime(ParticleSystem _vfx)
        {
            float maxLifetime = _vfx.main.startLifetime.constantMax;
            ParticleSystem[] children = _vfx.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < children.Length; i++)
            {
                float lifetime = children[i].main.startLifetime.constantMax;
                if (lifetime > maxLifetime) maxLifetime = lifetime;
            }
            return maxLifetime;
        }
    }

    /// <summary>
    /// Drone.PlayChainZap: 드론 전용 VFX_DroneLaser(Drone.prefab 안 VFX_DroneLaser.prefab 인스턴스) → SetColor(평소 노랑, 1.35) → PlayZap(총구 → 나무 top 3개).
    /// 40프레임에 과열 색(파랑)으로 다시 쏜다.
    /// </summary>
    public class DungeonDroneLaserCase : DungeonGoldenCase
    {
        // Drone.prefab 직렬화 값
        private static readonly Color NormalColor = new Color(1.0f, 0.92156863f, 0.015686275f, 1.0f);
        private static readonly Color OverheatColor = new Color(0.0f, 0.5019608f, 1.0f, 1.0f);
        private const float Intensity = 1.35f;

        private readonly List<Vector3> points = new List<Vector3>(4);
        private VFX_DroneLaser laser;

        public DungeonDroneLaserCase(string _caseName)
            : base(_caseName, 80, new Vector2(0.25f, 1.0f), new Vector2(10.0f, 6.0f))
        {
        }

        public override void Setup(VfxGoldenContext _context)
        {
            Vector3 muzzle = new Vector3(-2.5f, 1.6f, 0.0f);
            points.Clear();
            points.Add(muzzle);
            points.Add(DungeonGolden.TreeTop);
            points.Add(new Vector3(1.5f, -0.25f, 0.0f) + DungeonGolden.TreeTop);
            points.Add(new Vector3(3.0f, 0.25f, 0.0f) + DungeonGolden.TreeTop);

            laser = _context.Spawn(DungeonGolden.DroneLaserPrefabPath, muzzle).GetComponentInChildren<VFX_DroneLaser>(true);
            laser.SetColor(NormalColor, Intensity);
            laser.PlayZap(points, points.Count);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (40 != _frame) return;

            laser.SetColor(OverheatColor, Intensity);
            laser.PlayZap(points, points.Count);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayTreeHeatIndicator / CreateTreeHeatIndicator: SpriteRenderer + TreeHeatIndicator.Initialize(머티리얼, "Indicators", -1)
    /// → Play(나무, 숨김 1.5초, 표시 1.5초, 반지름 1.5). 카운트다운 3초(흔한 값). 끝나면 풀 반환(SetActive(false)).
    /// </summary>
    public class DungeonTreeHeatIndicatorCase : DungeonGoldenCase
    {
        private const float CountdownDelay = 3.0f;

        public DungeonTreeHeatIndicatorCase(string _caseName)
            : base(_caseName, 200, new Vector2(0.0f, 0.6f), new Vector2(6.0f, 5.0f))
        {
        }

        public override void Setup(VfxGoldenContext _context)
        {
            TreeObj tree = DungeonGolden.SpawnTree(_context, Vector3.zero);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(DungeonGolden.TreeHeatIndicatorMaterialPath);

            GameObject indicatorObject = _context.CreateEmpty("TreeHeatIndicator", Vector3.zero);
            indicatorObject.AddComponent<SpriteRenderer>();
            TreeHeatIndicator indicator = indicatorObject.AddComponent<TreeHeatIndicator>();
            indicator.Initialize(material, SortingLayer.NameToID(DungeonGolden.IndicatorSortingLayerName), DungeonGolden.TreeHeatIndicatorSortingOrder, OnFinished);

            float showDuration = Mathf.Min(Mathf.Max(0.01f, DungeonGolden.TreeHeatIndicatorLeadTime), CountdownDelay);
            float hiddenDuration = Mathf.Max(0f, CountdownDelay - showDuration);
            indicator.Play(tree, hiddenDuration, showDuration, DungeonGolden.TreeHeatIndicatorRadius);
        }

        private static void OnFinished(TreeHeatIndicator _instance)
        {
            // InDungeonVFXManager.OnReleaseTreeHeatIndicator
            _instance.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayTreeTransformVFX: 풀(ObjectPool) 인스턴스 → 위치 top + (0, -0.25) → Play(100, 보석 단계). 끝나면 풀로 반환.
    /// </summary>
    public class DungeonTreeTransformCase : DungeonGoldenCase
    {
        private readonly int gemStage;
        private VfxGoldenContext context;
        private IObjectPool<TreeTransformVFX> pool;

        public DungeonTreeTransformCase(string _caseName, int _gemStage)
            : base(_caseName, 50, new Vector2(0.0f, 0.5f), new Vector2(8.0f, 8.0f))
        {
            gemStage = _gemStage;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            context = _context;
            pool = new ObjectPool<TreeTransformVFX>(Create, OnGet, OnRelease, null, false, 1, 4);

            TreeTransformVFX instance = pool.Get();
            instance.transform.position = DungeonGolden.TreeTop + new Vector3(0f, DungeonGolden.TreeTransformVfxYOffset, 0f);
            instance.Play(100, gemStage);
        }

        private TreeTransformVFX Create()
        {
            TreeTransformVFX instance = context.Spawn(DungeonGolden.TreeTransformPrefabPath, Vector3.zero).GetComponent<TreeTransformVFX>();
            instance.SetPool(pool);
            return instance;
        }

        private static void OnGet(TreeTransformVFX _instance)
        {
            _instance.gameObject.SetActive(true);
        }

        private static void OnRelease(TreeTransformVFX _instance)
        {
            _instance.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// InDungeonObjectManager.ShieldExplosionRoutine → InDungeonVFXManager.PlaySporeExplosionVFX(기본 순서 오프셋 100) 4연타.
    /// 수관/밑동을 번갈아, 좌우를 번갈아(시작은 난수), 0.15초(9프레임) 간격. 오프셋 범위 0.25.
    /// </summary>
    public class DungeonSporeExplosionCase : DungeonGoldenCase
    {
        private const int PopCount = 4;
        private const int PopIntervalFrames = 9;
        private const float Spread = 0.25f;

        private VfxGoldenContext context;
        private IObjectPool<SporeExplosionVFX> pool;
        private bool bNextTopLeft;
        private bool bNextBottomLeft;

        public DungeonSporeExplosionCase(string _caseName)
            : base(_caseName, 90, new Vector2(0.0f, 0.4f), new Vector2(6.0f, 6.0f))
        {
        }

        public override void Setup(VfxGoldenContext _context)
        {
            context = _context;
            pool = new ObjectPool<SporeExplosionVFX>(Create, OnGet, OnRelease, null, false, 4, 64);
            bNextTopLeft = Random.value < 0.5f;
            bNextBottomLeft = Random.value < 0.5f;
            Pop(0);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (0 != _frame % PopIntervalFrames) return;

            int index = _frame / PopIntervalFrames;
            if (index < PopCount) Pop(index);
        }

        private void Pop(int _index)
        {
            bool bUseTop = 0 == _index % 2;
            Vector3 basePos = bUseTop ? DungeonGolden.TreeTop : Vector3.zero;
            bool bUseLeft = bUseTop ? bNextTopLeft : bNextBottomLeft;
            if (bUseTop) bNextTopLeft = !bNextTopLeft;
            else bNextBottomLeft = !bNextBottomLeft;

            float horizontalOffset = Random.Range(0f, Spread);
            Vector3 randomOffset = new Vector3(bUseLeft ? -horizontalOffset : horizontalOffset, Random.Range(-Spread, Spread), 0f);

            SporeExplosionVFX instance = pool.Get();
            instance.transform.position = basePos + randomOffset;
            instance.Play(100, randomOffset);
        }

        private SporeExplosionVFX Create()
        {
            SporeExplosionVFX instance = context.Spawn(DungeonGolden.SporeExplosionPrefabPath, Vector3.zero).GetComponent<SporeExplosionVFX>();
            instance.SetPool(pool);
            return instance;
        }

        private static void OnGet(SporeExplosionVFX _instance)
        {
            _instance.gameObject.SetActive(true);
        }

        private static void OnRelease(SporeExplosionVFX _instance)
        {
            _instance.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// InDungeonObjectManager.StarGazeRoutine → PlayShootingStarVFX(나무 밑동, topHighlight + 1, 착지 콜백)
    /// → 착지 때 PlayStarImpactExplosionVFX(같은 자리, 같은 순서). 카메라 흔들림·판정은 제외.
    /// </summary>
    public class DungeonShootingStarCase : DungeonGoldenCase
    {
        private VfxGoldenContext context;
        private IObjectPool<ShootingStarVFX> pool;

        public DungeonShootingStarCase(string _caseName)
            : base(_caseName, 200, new Vector2(0.0f, 2.5f), new Vector2(12.0f, 12.0f))
        {
        }

        public override void Setup(VfxGoldenContext _context)
        {
            context = _context;
            pool = new ObjectPool<ShootingStarVFX>(Create, OnGet, OnRelease, null, false, 1, 4);

            ShootingStarVFX instance = pool.Get();
            instance.Begin(Vector3.zero, DungeonGolden.TopHighlightOrder + 1, OnLanded);
        }

        private void OnLanded()
        {
            context.PlayPooled(new VFXPlaySettings("StarImpactExplosionEffect", Vector3.zero, Quaternion.identity, DungeonGolden.TopHighlightOrder + 1, null));
        }

        private ShootingStarVFX Create()
        {
            ShootingStarVFX instance = context.Spawn(DungeonGolden.ShootingStarPrefabPath, Vector3.zero).GetComponent<ShootingStarVFX>();
            instance.SetPool(pool);
            return instance;
        }

        private static void OnGet(ShootingStarVFX _instance)
        {
            _instance.gameObject.SetActive(true);
        }

        private static void OnRelease(ShootingStarVFX _instance)
        {
            _instance.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayTreeHitVFX: TreeHitEffect_Top/Bottom 공유 이미터에 나무 색(참나무)으로 Emit(호환되지 않으면 풀 경로).
    /// 0, 10, 20프레임 연타로 같은 이미터에 입자가 겹쳐 쌓인다. 스테이지 별가루(StageHitSparkles)는 켜진 스테이지 전용이라 제외.
    /// </summary>
    public class DungeonSharedTreeHitCase : DungeonGoldenCase
    {
        private VfxGoldenContext context;
        private SharedBurstEmitter topEmitter;
        private SharedBurstEmitter bottomEmitter;
        private ParticleColorSet topColor;
        private ParticleColorSet bottomColor;

        public DungeonSharedTreeHitCase(string _caseName)
            : base(_caseName, 90, new Vector2(0.0f, 0.5f), new Vector2(8.0f, 8.0f))
        {
        }

        public override void Setup(VfxGoldenContext _context)
        {
            context = _context;
            topEmitter = DungeonGolden.CreateSharedEmitter(_context, "TreeHitEffect_Top");
            bottomEmitter = DungeonGolden.CreateSharedEmitter(_context, "TreeHitEffect_Bottom");
            topColor = DungeonGolden.GetTopVfxColor(TreeType.OakTree);
            bottomColor = DungeonGolden.GetBottomVfxColor(TreeType.OakTree);
            Hit();
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (10 == _frame || 20 == _frame) Hit();
        }

        private void Hit()
        {
            DungeonGolden.PlayTreeColoredPart(context, topEmitter, "TreeHitEffect_Top", DungeonGolden.TreeTop, Quaternion.identity, topColor);
            DungeonGolden.PlayTreeColoredPart(context, bottomEmitter, "TreeHitEffect_Bottom", DungeonGolden.TreeBottom, Quaternion.identity, bottomColor);
        }
    }

    /// <summary>
    /// InDungeonVFXManager.PlayTreeDeadVFX(공유 이미터 경로).
    /// - 참나무: TreeDeadEffect_Top/Bottom(나무 색, 자식 덮어쓰기 포함) + 1스테이지 임팩트 TreeDeadEffect_LeafTop(고유 색).
    /// - 용암 나무: TreeDeadEffect_MagmaTop/Bottom(나무 색은 루트만) + 큰 폭발 TreeDeadEffect_MagmaBlastTop/Bottom(고유 색, 예산 안).
    /// 이미터가 호환되지 않으면 게임과 같이 풀 경로로 재생한다.
    /// </summary>
    public class DungeonSharedTreeDeadCase : DungeonGoldenCase
    {
        private readonly TreeType treeType;

        public DungeonSharedTreeDeadCase(string _caseName, TreeType _treeType)
            : base(_caseName, 90, new Vector2(0.0f, 0.5f), new Vector2(12.0f, 12.0f))
        {
            treeType = _treeType;
        }

        public override void Setup(VfxGoldenContext _context)
        {
            Vector3 top = DungeonGolden.TreeTop;
            Vector3 bottom = DungeonGolden.TreeBottom;
            ParticleColorSet topColor = DungeonGolden.GetTopVfxColor(treeType);
            ParticleColorSet bottomColor = DungeonGolden.GetBottomVfxColor(treeType);

            if (TreeType.CinderTree == treeType)
            {
                SharedBurstEmitter magmaTop = DungeonGolden.CreateSharedEmitter(_context, "TreeDeadEffect_MagmaTop");
                SharedBurstEmitter magmaBottom = DungeonGolden.CreateSharedEmitter(_context, "TreeDeadEffect_MagmaBottom");
                SharedBurstEmitter blastTop = DungeonGolden.CreateSharedEmitter(_context, "TreeDeadEffect_MagmaBlastTop");
                SharedBurstEmitter blastBottom = DungeonGolden.CreateSharedEmitter(_context, "TreeDeadEffect_MagmaBlastBottom");

                DungeonGolden.PlayTreeDeadEffectPart(_context, magmaTop, "TreeDeadEffect_MagmaTop", top, Quaternion.identity, true, topColor);
                DungeonGolden.PlayTreeDeadEffectPart(_context, magmaBottom, "TreeDeadEffect_MagmaBottom", bottom, Quaternion.identity, true, bottomColor);
                DungeonGolden.PlayTreeDeadEffectPart(_context, blastTop, "TreeDeadEffect_MagmaBlastTop", top, Quaternion.identity, false, topColor);
                DungeonGolden.PlayTreeDeadEffectPart(_context, blastBottom, "TreeDeadEffect_MagmaBlastBottom", bottom, Quaternion.identity, false, bottomColor);
                return;
            }

            SharedBurstEmitter deadTop = DungeonGolden.CreateSharedEmitter(_context, "TreeDeadEffect_Top");
            SharedBurstEmitter deadBottom = DungeonGolden.CreateSharedEmitter(_context, "TreeDeadEffect_Bottom");
            SharedBurstEmitter leafTop = DungeonGolden.CreateSharedEmitter(_context, "TreeDeadEffect_LeafTop");

            DungeonGolden.PlayTreeColoredPart(_context, deadTop, "TreeDeadEffect_Top", top, Quaternion.identity, topColor);
            DungeonGolden.PlayTreeColoredPart(_context, deadBottom, "TreeDeadEffect_Bottom", bottom, Quaternion.identity, bottomColor);
            DungeonGolden.PlayTreeDeadEffectPart(_context, leafTop, "TreeDeadEffect_LeafTop", top, Quaternion.identity, false, topColor);
        }
    }
}

#endif
