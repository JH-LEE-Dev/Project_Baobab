// VFX 골든 캡처 케이스 묶음 (TownLoot). 규칙은 VfxGoldenCases.cs 머리 주석을 보세요. 에디터 전용.
//
// 마을·전리품·기타 이펙트. 게임에서 이 이펙트들을 부르는 쪽(ShopNPC, TopgradeAssessmentPresenter, SawmillComboPresenter,
// RepairBox, RestAreaVisual, LootPillarManager/LootDisplayObject, LootManager/LootItem, LogItem, LogCutter, Puppy 등)은
// 매니저·캐릭터·세이브에 묶여 있어 통째로 띄울 수 없으므로, 케이스가 그 호출부와 같은 순서·같은 인자로 이펙트의 공개 API를 부른다.
// 이펙트 쪽 코드 경로는 바꾸지 않는다(가짜 클래스 없음). 프리팹·스프라이트·풀 설정은 게임이 쓰는 에셋에서 그대로 읽는다.
//
// [주의] VfxGoldenContext.PlayPooled는 부모 없이(월드로) 떼어 재생하는 경우 전용이다. 부모를 Root 아래로 주면
// effect.transform.root가 러너 오브젝트가 되어 Track에 러너가 올라가므로, 부모가 있는 풀 재생은 TownLootUtil.Play를 쓴다.
#if UNITY_EDITOR

using System.Collections.Generic;
using System.Reflection;
using PresentationLayer.Environment;
using PresentationLayer.VFX;
using UnityEditor;
using UnityEngine;

namespace Diagnostics.VfxGolden
{
    public static class VfxGoldenCasesTownLoot
    {
        public static void Append(List<VfxGoldenCase> _cases)
        {
            // 기준(base)을 기록한 뒤에는 이 순서를 바꾸지 말고 맨 끝에만 덧붙인다.

            // 마을 돈 (ShopNPC)
            _cases.Add(new TownLootMoneyDepositCase(false));
            _cases.Add(new TownLootMoneyDepositCase(true));
            _cases.Add(new TownLootMoneyTwinkleCase("TownLoot_TownMoney_ShopTwinkleLow", 0.15f));
            _cases.Add(new TownLootMoneyTwinkleCase("TownLoot_TownMoney_ShopTwinkleHigh", 1.0f));
            _cases.Add(new TownLootMoneyCollectCase());

            // 특등품 평가 (TopgradeAssessmentPresenter)
            _cases.Add(new TownLootTopgradeFlowCase());
            _cases.Add(new TownLootTopgradeStampComboCase());

            // 가공 콤보 (SawmillComboPresenter)
            _cases.Add(new TownLootSawmillComboCase("TownLoot_SawmillCombo_Overdrive", 2, 10, 270));
            _cases.Add(new TownLootSawmillComboCase("TownLoot_SawmillCombo_PartialDrain", 1, 5, 200));
            _cases.Add(new TownLootSawmillGaugeCase("TownLoot_SawmillComboGauge_Static", false));
            _cases.Add(new TownLootSawmillGaugeCase("TownLoot_SawmillComboGauge_Moving", true));

            // 모루 (RepairBox)
            _cases.Add(new TownLootAnvilGaugeCase("TownLoot_AnvilRepairGauge_Static", false));
            _cases.Add(new TownLootAnvilGaugeCase("TownLoot_AnvilRepairGauge_Moving", true));
            _cases.Add(new TownLootRepairSparkCase("TownLoot_RepairSpark_Single", 1));
            _cases.Add(new TownLootRepairSparkCase("TownLoot_RepairSpark_Hammering", 4));

            // 휴식처 (RestAreaVisual: VFX_RestAreaPlus + VFX_CampfireFlame)
            _cases.Add(new TownLootRestAreaCase("TownLoot_RestArea_OpenClose", false));
            _cases.Add(new TownLootRestAreaCase("TownLoot_RestArea_Moving", true));

            // 전리품 기둥 (LootPillarManager → LootDisplayObject: VFX_LootPillarGlow, VFX_LootEvolve)
            _cases.Add(new TownLootPillarCase("TownLoot_LootPillarGlow_T1", LootType.LostAndFoundBox, 1, 0));
            _cases.Add(new TownLootPillarCase("TownLoot_LootPillarGlow_T3", LootType.StarCompass, 3, 0));
            _cases.Add(new TownLootPillarCase("TownLoot_LootEvolve_1to2", LootType.SporePotion, 1, 2));
            _cases.Add(new TownLootPillarCase("TownLoot_LootEvolve_2to3", LootType.StarCompass, 2, 3));
            _cases.Add(new TownLootPillarCase("TownLoot_LootEvolve_1to3", LootType.ObsidianCharm, 1, 3));

            // 전리품 드랍 (LootManager → LootItem: VFX_RareLootAppear + VFX_Beam(ItemAuraEffectController))
            _cases.Add(new TownLootRareLootCase("TownLoot_RareLootAppear_LostAndFoundBox", LootType.LostAndFoundBox));
            _cases.Add(new TownLootRareLootCase("TownLoot_RareLootAppear_StarCompass", LootType.StarCompass));

            // 보석 원목 오라 (LogItem.PlayGemAura → ItemAuraEffectController)
            _cases.Add(new TownLootGemAuraCase("TownLoot_ItemAura_Fascinating", LogState.Fascinating, false));
            _cases.Add(new TownLootGemAuraCase("TownLoot_ItemAura_Advanced", LogState.Advanced, false));
            _cases.Add(new TownLootGemAuraCase("TownLoot_ItemAura_Perfect", LogState.Perfect, false));
            _cases.Add(new TownLootGemAuraCase("TownLoot_ItemAura_PerfectFlying", LogState.Perfect, true));

            // 풀 파티클
            _cases.Add(new TownLootLogShinyCase());
            _cases.Add(new TownLootGetOffCase());
            _cases.Add(new TownLootCuttingEffectCase("TownLoot_CuttingEffect_Tree", false));
            _cases.Add(new TownLootCuttingEffectCase("TownLoot_CuttingEffect_Gem", true));
            _cases.Add(new TownLootPuppyDustCase());
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 공용 도구
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>TownLoot 케이스가 함께 쓰는 에셋 조회·풀 재생 도구입니다.</summary>
    public static class TownLootUtil
    {
        public const string InstallerPath = "Assets/Prefabs/Installer/Installer/GameInstaller.prefab";
        public const string VehiclePath = "Assets/Prefabs/Objects/OffRoadVehicle/OffRoadVehicle.prefab";
        public const string CutterPath = "Assets/Prefabs/Objects/Shop/Cutter.prefab";
        public const string PuppyPath = "Assets/Prefabs/Objects/Puppy/Puppy.prefab";
        public const string LootPillarPath = "Assets/Prefabs/Objects/LootPillar/LootPillar.prefab";
        public const string LootItemPath = "Assets/Prefabs/Objects/Item/LootItem.prefab";

        public const float Dt = 1.0f / 60.0f;

        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;

        /// <summary>프리팹 에셋(자식 포함)에서 컴포넌트를 찾습니다. 읽기 전용으로만 씁니다.</summary>
        public static T LoadPrefabComponent<T>(string _path) where T : Component
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_path);
            if (null == prefab) throw new System.IO.FileNotFoundException("[VfxGolden] 프리팹이 없습니다: " + _path);

            T component = prefab.GetComponentInChildren<T>(true);
            if (null == component) throw new System.ArgumentException("[VfxGolden] " + _path + "에 " + typeof(T).Name + "가 없습니다.");
            return component;
        }

        /// <summary>직렬화 필드(비공개 포함)를 읽습니다. 게임이 인스펙터에서 받은 값을 그대로 쓰기 위함입니다.</summary>
        public static T GetField<T>(object _target, string _fieldName)
        {
            System.Type type = _target.GetType();
            while (null != type)
            {
                FieldInfo field = type.GetField(_fieldName, FieldFlags);
                if (null != field) return (T)field.GetValue(_target);
                type = type.BaseType;
            }
            throw new System.MissingFieldException(_target.GetType().Name, _fieldName);
        }

        /// <summary>
        /// _prefabPath 안의 VFXComponent에 등록된 _tag 설정으로 Root 아래에 전용 풀을 만듭니다(VfxGoldenContext.GetOrCreatePool과 같은 방식,
        /// 예열 수만 0). GameInstaller에 없는 태그(차량·커터·강아지 VFXComponent)나 부모가 있는 재생에 씁니다.
        /// </summary>
        public static VFXComponent CreatePool(VfxGoldenContext _context, string _prefabPath, string _tag)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (null == prefab) throw new System.IO.FileNotFoundException("[VfxGolden] 프리팹이 없습니다: " + _prefabPath);

            FieldInfo listField = typeof(VFXComponent).GetField("vfxPoolDataList", BindingFlags.Instance | BindingFlags.NonPublic);
            VFXPoolData source = null;
            VFXComponent[] components = prefab.GetComponentsInChildren<VFXComponent>(true);
            for (int i = 0; i < components.Length && null == source; i++)
            {
                List<VFXPoolData> list = listField.GetValue(components[i]) as List<VFXPoolData>;
                if (null == list) continue;
                for (int k = 0; k < list.Count; k++)
                {
                    if (null != list[k] && _tag == list[k].VfxTag)
                    {
                        source = list[k];
                        break;
                    }
                }
            }
            if (null == source) throw new System.ArgumentException("[VfxGolden] " + _prefabPath + "에 없는 VFX 태그입니다: " + _tag);

            VFXPoolData copy = new VFXPoolData();
            foreach (FieldInfo field in typeof(VFXPoolData).GetFields(FieldFlags))
            {
                field.SetValue(copy, field.GetValue(source));
            }
            typeof(VFXPoolData).GetField("initialPoolSize", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(copy, 0);

            GameObject go = new GameObject("[TownLootPool] " + _tag);
            go.SetActive(false);
            go.transform.SetParent(_context.Root, false);
            VFXComponent pool = go.AddComponent<VFXComponent>();
            typeof(VFXComponent).GetField("initializeOnAwake", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(pool, false);
            listField.SetValue(pool, new List<VFXPoolData> { copy });
            go.SetActive(true);
            pool.Initialize();
            return pool;
        }

        /// <summary>VFXComponent.Play(VFXPlaySettings)와 같은 경로로 재생합니다. 꺼낸 뒤 시드를 고정하고 Random을 다시 고정합니다.</summary>
        public static ParticleSystem Play(VfxGoldenContext _context, VFXComponent _pool, VFXPlaySettings _settings)
        {
            ParticleSystem effect = Take(_context, _pool, _settings.VfxTag);
            _pool.Play(effect, _settings);
            TrackDetached(_context, effect);
            return effect;
        }

        /// <summary>VFXComponent.Play(tag, 위치, 회전, 부모)와 같은 경로로 재생합니다.</summary>
        public static ParticleSystem Play(VfxGoldenContext _context, VFXComponent _pool, string _tag, Vector3 _position, Quaternion _rotation, Transform _parent)
        {
            ParticleSystem effect = Take(_context, _pool, _tag);
            _pool.Play(effect, _position, _rotation, _parent);
            TrackDetached(_context, effect);
            return effect;
        }

        /// <summary>
        /// 프리팹 에셋 안의 자식 오브젝트 하나(_source)만 Root 아래에 복제합니다(Spawn과 같이 비활성 부모 아래에서 만든 뒤 시드 고정).
        /// 복제할 서브트리의 참조가 서브트리 안에서 닫혀 있어야 합니다(밖을 가리키면 에셋을 건드리게 됨).
        /// </summary>
        public static GameObject SpawnSubtree(VfxGoldenContext _context, GameObject _source, Vector3 _position)
        {
            GameObject holder = new GameObject("SpawnHolder");
            holder.SetActive(false);
            holder.transform.SetParent(_context.Root, false);

            GameObject instance = Object.Instantiate(_source, _position, Quaternion.identity, holder.transform);
            _context.FixParticleSeeds(instance);

            instance.transform.SetParent(_context.Root, true);
            Object.Destroy(holder);
            return instance;
        }

        private static ParticleSystem Take(VfxGoldenContext _context, VFXComponent _pool, string _tag)
        {
            ParticleSystem effect = _pool.Get(_tag);
            if (null == effect) throw new System.InvalidOperationException("[VfxGolden] 풀에서 꺼내지 못했습니다: " + _tag);

            _context.FixParticleSeeds(effect.gameObject);
            Random.InitState((int)_context.CaseSeed);
            return effect;
        }

        // 부모 없이 월드로 떼어진 경우에만 맨 위 오브젝트를 해시·정리 대상에 올린다(Root 아래면 이미 포함).
        private static void TrackDetached(VfxGoldenContext _context, ParticleSystem _effect)
        {
            Transform top = _effect.transform;
            if (top.IsChildOf(_context.Root)) return;
            while (null != top.parent) top = top.parent;
            _context.Track(top.gameObject);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VFX_TownMoney (ShopNPC)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 날아가는 금화 하나의 궤적(ShopNPC의 Coin.DynamicTransferLaunch 대역). 금화 자체는 이펙트가 아니라 그리지 않고,
    /// 이펙트에 넘길 위치만 만든다(포물선 + 옆 흔들림).
    /// </summary>
    public sealed class TownLootCoinFlight
    {
        public Vector3 from;
        public Vector3 to;
        public Vector3 jitter;
        public float height;
        public float duration;
        public float time;
        public float trailTimer;
        public bool bActive;

        public Vector3 Position
        {
            get
            {
                float t = Mathf.Clamp01(time / Mathf.Max(0.0001f, duration));
                float arc = 4.0f * t * (1.0f - t);
                return Vector3.Lerp(from, to, t) + jitter * arc + new Vector3(0.0f, height * arc, 0.0f);
            }
        }

        public bool IsArrived => time >= duration;
    }

    /// <summary>
    /// ShopNPC 입금: LaunchDepositCoin(PlayDepositPop) → UpdateDepositCoins(EmitTrail → EmitCoinTrail, 도착 시 PlayDepositLand).
    /// 특등품이면 금화가 depositTopgradeExtraCoins(2)개 더, depositStagger(0.07초) 간격으로 날아간다.
    /// 원본: ShopNPC.cs:LaunchDepositCoin / EmitTrail / (입금 금화 갱신 루프)
    /// </summary>
    public sealed class TownLootMoneyDepositCase : VfxGoldenCase
    {
        // ShopNPC 인스펙터 기본값
        private static readonly Vector3 EvaluatorPos = new Vector3(-2.2f, -0.6f, 0.0f);
        private static readonly Vector3 ShopPos = new Vector3(1.6f, 0.2f, 0.0f);
        private static readonly Vector3 DepositLaunchOffset = new Vector3(0.0f, 0.9f, 0.0f);
        private static readonly Vector3 DepositLandOffset = new Vector3(0.3f, 0.55f, 0.0f);
        private const float DepositFlyDuration = 0.42f;
        private const float DepositFlyHeight = 0.8f;
        private const float DepositStagger = 0.07f;
        private const float CoinTrailInterval = 0.03f;
        private const float TrailStopDistanceSqr = 0.5f * 0.5f;

        private readonly bool bTopgrade;
        private readonly List<TownLootCoinFlight> coins = new List<TownLootCoinFlight>(4);
        private VFX_TownMoney vfx;
        private int coinCount;
        private int launched;
        private float elapsed;

        public TownLootMoneyDepositCase(bool _bTopgrade)
        {
            bTopgrade = _bTopgrade;
        }

        public override string Name => bTopgrade ? "TownLoot_TownMoney_DepositTopgrade" : "TownLoot_TownMoney_Deposit";
        public override int Frames => 100;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.6f);
        public override Vector2 ViewSize => new Vector2(8.0f, 5.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            // ShopNPC.SetupMoneyVfx: 상점의 부모(SortingGroup 없는 Building) 아래에 만든다.
            GameObject building = _context.CreateEmpty("Building", Vector3.zero);
            vfx = VFX_TownMoney.Create(building.transform);
            if (null == vfx) throw new System.InvalidOperationException("[VfxGolden] VFX_TownMoney 셰이더를 찾지 못했습니다.");

            coins.Clear();
            coinCount = bTopgrade ? 3 : 1;
            launched = 0;
            elapsed = 0.0f;
            LaunchDue();
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            LaunchDue();

            Vector3 target = ShopPos + DepositLandOffset;
            for (int i = 0; i < coins.Count; i++)
            {
                TownLootCoinFlight coin = coins[i];
                if (false == coin.bActive) continue;

                coin.time += dt;
                if (coin.IsArrived)
                {
                    vfx.PlayDepositLand(coin.Position);
                    coin.bActive = false;
                    continue;
                }

                // ShopNPC.EmitTrail
                coin.trailTimer -= dt;
                if (coin.trailTimer > 0.0f) continue;
                coin.trailTimer += CoinTrailInterval;
                if (coin.trailTimer < 0.0f) coin.trailTimer = 0.0f;

                Vector3 pos = coin.Position;
                if (((Vector2)(target - pos)).sqrMagnitude < TrailStopDistanceSqr) continue;
                vfx.EmitCoinTrail(pos);
            }
        }

        // 지연(stagger)이 지난 금화를 띄운다: ShopNPC.LaunchDepositCoin
        private void LaunchDue()
        {
            while (launched < coinCount && launched * DepositStagger <= elapsed + 0.0001f)
            {
                Vector3 from = EvaluatorPos + DepositLaunchOffset;
                Vector3 to = ShopPos + DepositLandOffset;
                Vector3 dir = (to - from).normalized;
                Vector3 normal = new Vector3(-dir.y, dir.x, 0.0f);

                TownLootCoinFlight coin = new TownLootCoinFlight
                {
                    from = from,
                    to = to,
                    jitter = normal * Random.Range(-0.15f, 0.15f),
                    height = DepositFlyHeight * Random.Range(0.9f, 1.1f),
                    duration = DepositFlyDuration,
                    time = 0.0f,
                    trailTimer = 0.0f,
                    bActive = true,
                };
                coins.Add(coin);
                vfx.PlayDepositPop(from, bTopgrade);
                launched++;
            }
        }
    }

    /// <summary>
    /// ShopNPC 상점 대기 반짝임: 수금 안 한 돈이 있는 동안 간격 Lerp(0.5, 0.09, heat) x Random(0.7~1.3)마다 EmitShopTwinkle.
    /// 150프레임 뒤 수금(money = 0)으로 멈추고 남은 반짝임·튀는 금화가 사라질 때까지 기다린다.
    /// 원본: ShopNPC.cs:UpdateShopTwinkle
    /// </summary>
    public sealed class TownLootMoneyTwinkleCase : VfxGoldenCase
    {
        private static readonly Vector3 ShopPos = new Vector3(1.6f, 0.2f, 0.0f);
        private static readonly Vector3 DepositLandOffset = new Vector3(0.3f, 0.55f, 0.0f);
        private static readonly Vector3 ShopTwinkleCenter = new Vector3(0.1f, 0.75f, 0.0f);
        private static readonly Vector2 ShopTwinkleExtents = new Vector2(0.55f, 0.4f);
        private const float ShopTwinkleIntervalSlow = 0.5f;
        private const float ShopTwinkleIntervalFast = 0.09f;
        private const int StopFrame = 150;

        private readonly string caseName;
        private readonly float heat;
        private VFX_TownMoney vfx;
        private float timer;

        public TownLootMoneyTwinkleCase(string _caseName, float _heat)
        {
            caseName = _caseName;
            heat = _heat;
        }

        public override string Name => caseName;
        public override int Frames => 210;
        public override Vector2 ViewCenter => new Vector2(1.7f, 1.0f);
        public override Vector2 ViewSize => new Vector2(4.0f, 3.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            GameObject building = _context.CreateEmpty("Building", Vector3.zero);
            vfx = VFX_TownMoney.Create(building.transform);
            if (null == vfx) throw new System.InvalidOperationException("[VfxGolden] VFX_TownMoney 셰이더를 찾지 못했습니다.");

            timer = 0.0f;
            Tick(Time.deltaTime);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (_frame < StopFrame) Tick(Time.deltaTime);
        }

        private void Tick(float _dt)
        {
            timer -= _dt;
            if (timer > 0.0f) return;

            float interval = Mathf.Lerp(ShopTwinkleIntervalSlow, ShopTwinkleIntervalFast, heat);
            timer = interval * Random.Range(0.7f, 1.3f);

            Vector3 hopPos = ShopPos + DepositLandOffset;
            vfx.EmitShopTwinkle(ShopPos + ShopTwinkleCenter, ShopTwinkleExtents, hopPos, heat);
        }
    }

    /// <summary>
    /// ShopNPC 수금: 상점에서 캐릭터로 금화가 줄지어 날아가며 꼬리(EmitCoinTrail)를 남기고, 도착마다 PlayCoinArrive.
    /// 첫 금화(실제 지급 순간)·마지막 금화는 크게 터진다.
    /// 원본: ShopNPC.cs:UpdateFlyingCoins / EmitTrail
    /// </summary>
    public sealed class TownLootMoneyCollectCase : VfxGoldenCase
    {
        private static readonly Vector3 CollectFrom = new Vector3(1.9f, 0.75f, 0.0f);
        private static readonly Vector3 CharacterCenter = new Vector3(-1.0f, -1.2f, 0.0f);
        private const int CoinCount = 6;
        private const float CoinStagger = 0.06f;
        private const float FlyDuration = 0.5f;
        private const float FlyHeight = 1.0f;
        private const float CoinTrailInterval = 0.03f;
        private const float TrailStopDistanceSqr = 0.5f * 0.5f;

        private readonly List<TownLootCoinFlight> coins = new List<TownLootCoinFlight>(CoinCount);
        private VFX_TownMoney vfx;
        private int launched;
        private int remaining;
        private bool bAwaitingFirstArrival;
        private float elapsed;

        public override string Name => "TownLoot_TownMoney_CollectArrive";
        public override int Frames => 130;
        public override Vector2 ViewCenter => new Vector2(0.3f, -0.2f);
        public override Vector2 ViewSize => new Vector2(8.0f, 6.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            GameObject building = _context.CreateEmpty("Building", Vector3.zero);
            vfx = VFX_TownMoney.Create(building.transform);
            if (null == vfx) throw new System.InvalidOperationException("[VfxGolden] VFX_TownMoney 셰이더를 찾지 못했습니다.");

            coins.Clear();
            launched = 0;
            remaining = CoinCount;
            bAwaitingFirstArrival = true;
            elapsed = 0.0f;
            LaunchDue();
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            float dt = Time.deltaTime;
            elapsed += dt;
            LaunchDue();

            for (int i = 0; i < coins.Count; i++)
            {
                TownLootCoinFlight coin = coins[i];
                if (false == coin.bActive) continue;

                coin.time += dt;
                if (coin.IsArrived)
                {
                    if (0 < remaining) --remaining;
                    vfx.PlayCoinArrive(coin.Position, bAwaitingFirstArrival, 0 == remaining);
                    bAwaitingFirstArrival = false;
                    coin.bActive = false;
                    continue;
                }

                coin.trailTimer -= dt;
                if (coin.trailTimer > 0.0f) continue;
                coin.trailTimer += CoinTrailInterval;
                if (coin.trailTimer < 0.0f) coin.trailTimer = 0.0f;

                Vector3 pos = coin.Position;
                if (((Vector2)(CharacterCenter - pos)).sqrMagnitude < TrailStopDistanceSqr) continue;
                vfx.EmitCoinTrail(pos);
            }
        }

        private void LaunchDue()
        {
            while (launched < CoinCount && launched * CoinStagger <= elapsed + 0.0001f)
            {
                Vector3 dir = (CharacterCenter - CollectFrom).normalized;
                Vector3 normal = new Vector3(-dir.y, dir.x, 0.0f);
                coins.Add(new TownLootCoinFlight
                {
                    from = CollectFrom,
                    to = CharacterCenter,
                    jitter = normal * Random.Range(-0.2f, 0.2f),
                    height = FlyHeight * Random.Range(0.9f, 1.1f),
                    duration = FlyDuration,
                    time = 0.0f,
                    trailTimer = 0.0f,
                    bActive = true,
                });
                launched++;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VFX_TopgradeAssessment (TopgradeAssessmentPresenter)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 특등품 한 개의 흐름: 커터에서 쌍둥이가 튀어 오름(PlayReveal) → 원목 등에 착지(PlayLand) → 업혀 가며 반짝임
    /// (EmitRideSparkle, 0.14~0.24초 간격) → 평가기 쾅(PlayStamp, 콤보 1). 원목은 벨트를 따라 평가기로 움직인다.
    /// 원본: TopgradeAssessmentPresenter.cs:StartTwin(PlayReveal) / ApplyTwin(PlayLand, EmitRideSparkle) / OnTopgradeAssessed(PlayStamp)
    /// </summary>
    public sealed class TownLootTopgradeFlowCase : VfxGoldenCase
    {
        private static readonly Vector3 CutterPos = new Vector3(-2.0f, 0.0f, 0.0f);
        private static readonly Vector3 StoragePos = new Vector3(1.8f, -0.9f, 0.0f);
        private static readonly Vector3 MainStart = new Vector3(-1.4f, -0.3f, 0.0f);
        // 인스펙터 기본값
        private static readonly Vector3 CutterBladeOffset = new Vector3(0.0f, 0.6f, 0.0f);
        private static readonly Vector3 StampImpactOffset = new Vector3(0.0f, 0.35f, 0.0f);
        private static readonly Vector3 StampFountainOffset = new Vector3(0.0f, 0.8f, 0.0f);
        private static readonly Vector2 RideSparkleExtents = new Vector2(0.32f, 0.12f);
        private const float PopDuration = 0.36f;
        private const float PopApex = 0.6f;
        private const float PopFromCutterRatio = 0.25f;
        private const float RideHeight = 0.19f;
        private const float RideSparkleIntervalMin = 0.14f;
        private const float RideSparkleIntervalMax = 0.24f;
        private const float BeltSpeed = 2.2f;
        private const float DiveLag = 0.04f;

        private VFX_TopgradeAssessment vfx;
        private float elapsed;
        private float beltTime;
        private float sparkleTimer;
        private bool bLanded;
        private bool bArrived;
        private bool bStamped;
        private float arriveTime;

        public override string Name => "TownLoot_Topgrade_Flow";
        public override int Frames => 200;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.5f);
        public override Vector2 ViewSize => new Vector2(10.0f, 7.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            // 가공 라인마다 붙는 Presenter 아래에 만든다.
            GameObject presenter = _context.CreateEmpty("TopgradeAssessmentPresenter", Vector3.zero);
            vfx = VFX_TopgradeAssessment.Create(presenter.transform);
            if (null == vfx) throw new System.InvalidOperationException("[VfxGolden] VFX_TopgradeAssessment 셰이더를 찾지 못했습니다.");

            elapsed = 0.0f;
            beltTime = BeltDuration;
            bLanded = false;
            bArrived = false;
            bStamped = false;

            // StartTwin: 쌍둥이 출발점 = 원목 + (커터 - 원목) x popFromCutterRatio
            Vector3 twinPos = MainStart + (CutterPos - MainStart) * PopFromCutterRatio;
            Vector2 outDir = StoragePos - CutterPos;
            vfx.PlayReveal(twinPos, CutterPos + CutterBladeOffset, outDir);
        }

        private float BeltDuration => Vector3.Distance(MainStart, StoragePos) / BeltSpeed;

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            float beltT = Mathf.Clamp01(elapsed / beltTime);
            Vector3 main = Vector3.Lerp(MainStart, StoragePos, beltT);
            Vector3 ride = main + new Vector3(0.0f, RideHeight, 0.0f);

            if (false == bArrived)
            {
                if (elapsed < PopDuration)
                {
                    // 튀어 오르기(이펙트 없음, 위치만)
                    return;
                }

                if (false == bLanded)
                {
                    bLanded = true;
                    vfx.PlayLand(ride);
                    sparkleTimer = Random.Range(RideSparkleIntervalMin, RideSparkleIntervalMax);
                }
                else
                {
                    sparkleTimer -= dt;
                    if (sparkleTimer <= 0.0f)
                    {
                        sparkleTimer += Random.Range(RideSparkleIntervalMin, RideSparkleIntervalMax);
                        vfx.EmitRideSparkle(ride, RideSparkleExtents.x, RideSparkleExtents.y);
                    }
                }

                if (beltT >= 1.0f)
                {
                    bArrived = true;
                    arriveTime = elapsed;
                }
                return;
            }

            // 원목이 평가기로 들어가고 diveLag 뒤 쾅
            if (false == bStamped && elapsed - arriveTime >= DiveLag)
            {
                bStamped = true;
                vfx.PlayStamp(StoragePos + StampImpactOffset, StoragePos + StampFountainOffset, 1);
            }
        }
    }

    /// <summary>
    /// 평가기 콤보: comboWindow(1.2초) 안에 연달아 터져 콤보가 1→6으로 오른다(충격파 반경·금화 수 증가).
    /// 원본: TopgradeAssessmentPresenter.cs:OnTopgradeAssessed
    /// </summary>
    public sealed class TownLootTopgradeStampComboCase : VfxGoldenCase
    {
        private static readonly Vector3 StoragePos = new Vector3(0.0f, -1.0f, 0.0f);
        private static readonly Vector3 StampImpactOffset = new Vector3(0.0f, 0.35f, 0.0f);
        private static readonly Vector3 StampFountainOffset = new Vector3(0.0f, 0.8f, 0.0f);
        private const int StampInterval = 24;
        private const int StampCount = 6;

        private VFX_TopgradeAssessment vfx;
        private int combo;

        public override string Name => "TownLoot_Topgrade_StampCombo";
        public override int Frames => 220;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.2f);
        public override Vector2 ViewSize => new Vector2(7.0f, 6.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            GameObject presenter = _context.CreateEmpty("TopgradeAssessmentPresenter", Vector3.zero);
            vfx = VFX_TopgradeAssessment.Create(presenter.transform);
            if (null == vfx) throw new System.InvalidOperationException("[VfxGolden] VFX_TopgradeAssessment 셰이더를 찾지 못했습니다.");

            combo = 0;
            Stamp();
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (0 == _frame % StampInterval && _frame / StampInterval < StampCount) Stamp();
        }

        private void Stamp()
        {
            combo++;
            vfx.PlayStamp(StoragePos + StampImpactOffset, StoragePos + StampFountainOffset, combo);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // VFX_SawmillCombo / SawmillComboGauge (SawmillComboPresenter)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 가공 콤보 연출 전체(SawmillComboPresenter.Update를 그대로 따라 함): 콤보가 쌓이는 동안 켜는 중(정방향)인 커터에서 톱밥
    /// (EmitSawdust, 초당 Lerp(6, 40, 비율)), 최대 중첩에 닿는 순간 커터마다 PlayOverdrive, 그동안 열기(EmitHeat, 초당 9),
    /// 0으로 풀리는 순간 커터마다 PlaySmoke. 게이지(SawmillComboGauge.SetState)는 매 프레임 함께 갱신.
    /// 끝에 콤보 0·게이지만 보이는 대기 상태가 30프레임 넘게 이어진다.
    /// 원본: SawmillComboPresenter.cs:Setup / Update / EmitSawdust / GetCutterTop
    /// </summary>
    public sealed class TownLootSawmillComboCase : VfxGoldenCase
    {
        private const float SawdustRateMin = 6.0f;
        private const float SawdustRateMax = 40.0f;
        private const float HeatRate = 9.0f;
        private const float CutterTopRatio = 0.0f;
        private const float CutterHalfWidth = 0.38f;
        private const int MaxStack = 10;              // LogProcessingManager.MaxProcessingStack
        private const int RiseStart = 6;
        private const int RiseInterval = 8;
        private const int HoldFrames = 70;
        private const int CuttingCycle = 180;         // 3초 왕복(정방향 2초 : 역방향 1초)
        private const int CuttingForward = 120;

        private static readonly Vector3[] CutterPositions = { new Vector3(-0.8f, 0.0f, 0.0f), new Vector3(0.9f, -0.85f, 0.0f) };
        private static readonly Vector3 GaugeAnchor = new Vector3(-2.3f, 0.6f, 0.0f);

        private readonly string caseName;
        private readonly int lineCount;
        private readonly int peakStack;
        private readonly int frames;

        private VFX_SawmillCombo vfx;
        private SawmillComboGauge gauge;
        private Vector3 effectLocal;
        private Vector3 boundsCenterLocal;
        private Vector3 boundsExtents;
        private ParticleSystem.MinMaxGradient sawdustColor;
        private float[] sawdustAccum;
        private float[] heatAccum;
        private bool bSynced;
        private int lastStack;
        private uint randomState = 1u;

        public TownLootSawmillComboCase(string _caseName, int _lineCount, int _peakStack, int _frames)
        {
            caseName = _caseName;
            lineCount = Mathf.Clamp(_lineCount, 1, CutterPositions.Length);
            peakStack = Mathf.Clamp(_peakStack, 1, MaxStack);
            frames = _frames;
        }

        public override string Name => caseName;
        public override int Frames => frames;
        public override Vector2 ViewCenter => new Vector2(-0.6f, 0.2f);
        public override Vector2 ViewSize => new Vector2(6.0f, 4.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            // 커터 그림 경계·톱밥 자리·톱밥 색은 Cutter 프리팹 값 그대로(LogCutter.VisualBounds / EffectPoint / SampleEffectColor)
            LogCutter cutter = TownLootUtil.LoadPrefabComponent<LogCutter>(TownLootUtil.CutterPath);
            SpriteRenderer cutterRenderer = cutter.GetComponent<SpriteRenderer>();
            Bounds spriteBounds = null != cutterRenderer && null != cutterRenderer.sprite ? cutterRenderer.sprite.bounds : new Bounds(Vector3.zero, Vector3.zero);
            boundsCenterLocal = spriteBounds.center;
            boundsExtents = spriteBounds.extents;
            Transform effectTransform = TownLootUtil.GetField<Transform>(cutter, "effectTransform");
            effectLocal = null != effectTransform ? effectTransform.localPosition : Vector3.zero;
            List<TreeVFXColorData> treeColors = TownLootUtil.GetField<List<TreeVFXColorData>>(cutter, "treeVFXColorDatas");
            sawdustColor = null != treeColors && 0 < treeColors.Count ? treeColors[0].effectColor : new ParticleSystem.MinMaxGradient(Color.white);

            // SawmillComboPresenter.Setup: Shop 오브젝트 아래에 이펙트와 게이지(LogProcessingManager의 그림·셰이더)
            LogProcessingManager manager = TownLootUtil.LoadPrefabComponent<LogProcessingManager>(TownLootUtil.InstallerPath);
            GameObject shop = _context.CreateEmpty("Shop", Vector3.zero);
            vfx = VFX_SawmillCombo.Create(shop.transform);
            if (null == vfx) throw new System.InvalidOperationException("[VfxGolden] VFX_SawmillCombo 셰이더를 찾지 못했습니다.");
            gauge = SawmillComboGauge.Create(shop.transform,
                TownLootUtil.GetField<Sprite>(manager, "comboGaugePanelSprite"),
                TownLootUtil.GetField<Sprite>(manager, "comboGaugeTrackSprite"),
                TownLootUtil.GetField<Sprite>(manager, "comboGaugeFillSprite"),
                TownLootUtil.GetField<Shader>(manager, "comboGaugeShader"));
            if (null == gauge) throw new System.InvalidOperationException("[VfxGolden] SawmillComboGauge를 만들지 못했습니다.");

            sawdustAccum = new float[lineCount];
            heatAccum = new float[lineCount];
            randomState = unchecked((uint)Random.Range(1, int.MaxValue) * 2654435761u) | 1u;
            bSynced = false;
            lastStack = 0;

            Tick(0, Time.deltaTime);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            Tick(_frame, Time.deltaTime);
        }

        // 콤보 일정: RiseStart부터 RiseInterval마다 1씩 올라 peakStack에서 HoldFrames 동안 머문 뒤 0으로 풀린다.
        private int StackAt(int _frame)
        {
            if (_frame < RiseStart) return 0;
            int rise = 1 + (_frame - RiseStart) / RiseInterval;
            if (rise < peakStack) return rise;
            int peakFrame = RiseStart + (peakStack - 1) * RiseInterval;
            return _frame < peakFrame + HoldFrames ? peakStack : 0;
        }

        private bool IsCuttingForward(int _line, int _frame)
        {
            return ((_frame + _line * 60) % CuttingCycle) < CuttingForward;
        }

        private void Tick(int _frame, float _dt)
        {
            int stack = StackAt(_frame);
            int max = MaxStack;
            int activeCount = lineCount;

            if (false == bSynced)
            {
                lastStack = stack;
                bSynced = true;
            }
            else if (stack != lastStack)
            {
                if (stack >= max && lastStack < max)
                {
                    for (int i = 0; i < activeCount; i++) vfx.PlayOverdrive(GetCutterTop(i));
                }
                else if (stack == 0 && lastStack > 0)
                {
                    for (int i = 0; i < activeCount; i++) vfx.PlaySmoke(GetCutterTop(i), CutterHalfWidth);
                }
                lastStack = stack;
            }

            gauge.SetState(true, GaugeAnchor, stack, max);

            float ratio = stack / (float)max;
            bool bMax = stack >= max;

            for (int i = 0; i < activeCount; i++)
            {
                if (stack > 0)
                {
                    if (false == IsCuttingForward(i, _frame))
                    {
                        sawdustAccum[i] = 0.0f;
                    }
                    else
                    {
                        sawdustAccum[i] += _dt * Mathf.Lerp(SawdustRateMin, SawdustRateMax, ratio);
                        while (sawdustAccum[i] >= 1.0f)
                        {
                            sawdustAccum[i] -= 1.0f;
                            float t = Rand01();
                            vfx.EmitSawdust(CutterPositions[i] + effectLocal, sawdustColor.Evaluate(t, t));
                        }
                    }
                }
                else
                {
                    sawdustAccum[i] = 0.0f;
                }

                if (bMax)
                {
                    heatAccum[i] += _dt * HeatRate;
                    while (heatAccum[i] >= 1.0f)
                    {
                        heatAccum[i] -= 1.0f;
                        vfx.EmitHeat(GetCutterTop(i), CutterHalfWidth * 0.8f);
                    }
                }
                else
                {
                    heatAccum[i] = 0.0f;
                }
            }
        }

        private Vector3 GetCutterTop(int _line)
        {
            Vector3 center = CutterPositions[_line] + boundsCenterLocal;
            return new Vector3(center.x, center.y + boundsExtents.y * CutterTopRatio, 0.0f);
        }

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535f;
        }
    }

    /// <summary>
    /// 가공 콤보 게이지만: 콤보 0으로 보이는 대기(40프레임) → 3→10으로 쌓임(새 칸 반짝·가득 참 섬광·반짝임 회전) → 가득 찬 채 대기(40프레임)
    /// → 0으로 풀림(위 칸부터 꺼짐) → 0으로 대기(40프레임) → 숨김(SetState(false), 30프레임). _bMoving이면 매 프레임 기준점을 옮긴다.
    /// 원본: SawmillComboPresenter.cs:Update(gauge.SetState)
    /// </summary>
    public sealed class TownLootSawmillGaugeCase : VfxGoldenCase
    {
        private const int MaxStack = 10;
        private static readonly Vector3 BaseAnchor = new Vector3(0.0f, -0.3f, 0.0f);

        private readonly string caseName;
        private readonly bool bMoving;
        private SawmillComboGauge gauge;

        public TownLootSawmillGaugeCase(string _caseName, bool _bMoving)
        {
            caseName = _caseName;
            bMoving = _bMoving;
        }

        public override string Name => caseName;
        public override int Frames => 270;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.0f);
        public override Vector2 ViewSize => bMoving ? new Vector2(6.0f, 3.0f) : new Vector2(3.0f, 2.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            LogProcessingManager manager = TownLootUtil.LoadPrefabComponent<LogProcessingManager>(TownLootUtil.InstallerPath);
            GameObject shop = _context.CreateEmpty("Shop", Vector3.zero);
            gauge = SawmillComboGauge.Create(shop.transform,
                TownLootUtil.GetField<Sprite>(manager, "comboGaugePanelSprite"),
                TownLootUtil.GetField<Sprite>(manager, "comboGaugeTrackSprite"),
                TownLootUtil.GetField<Sprite>(manager, "comboGaugeFillSprite"),
                TownLootUtil.GetField<Shader>(manager, "comboGaugeShader"));
            if (null == gauge) throw new System.InvalidOperationException("[VfxGolden] SawmillComboGauge를 만들지 못했습니다.");

            Apply(0);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            Apply(_frame);
        }

        private void Apply(int _frame)
        {
            Vector3 anchor = BaseAnchor;
            if (bMoving) anchor += new Vector3(Mathf.Sin(_frame * 0.05f) * 1.5f, Mathf.Cos(_frame * 0.07f) * 0.4f, 0.0f);

            if (_frame >= 240)
            {
                gauge.SetState(false, Vector3.zero, 0, MaxStack);
                return;
            }

            gauge.SetState(true, anchor, StackAt(_frame), MaxStack);
        }

        private static int StackAt(int _frame)
        {
            if (_frame < 40) return 0;
            if (_frame < 110) return Mathf.Min(MaxStack, 3 + (_frame - 40) / 10);
            if (_frame < 160) return MaxStack;
            return 0;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // AnvilRepairGauge / VFX_RepairSpark (RepairBox)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 모루 원판: 도끼가 모루에 닿으면 Show + 첫 두드림(PlayHit), 이후 hammerInterval(0.5초)마다 PlayHit, 매 프레임 SetState(기준점, 내구도 비율).
    /// 가득 차면 마지막으로 한 번 두드리고 그 뒤로는 SetState만(아무 일도 없는 대기 50프레임) → 돌려받기(Hide) → 30프레임 대기.
    /// _bMoving이면 모루(부모)를 매 프레임 옮긴다(원판이 자식이라 함께 움직이고 SetState가 위치를 다시 씀).
    /// 원본: RepairBox.cs:Awake(Create) / BeginHammering / Update / PlayHammerHit / StartAxeReturn
    /// </summary>
    public sealed class TownLootAnvilGaugeCase : VfxGoldenCase
    {
        private const float HammerInterval = 0.5f;
        private const float StartFill = 0.35f;
        private const float FillPerFrame = 0.0045f;
        private const int HideFrame = 205;

        private readonly string caseName;
        private readonly bool bMoving;
        private AnvilRepairGauge gauge;
        private Transform box;
        private Vector2 gaugeLocalAnchor;
        private float fill;
        private float hammerTimer;
        private bool bFull;
        private bool bHidden;

        public TownLootAnvilGaugeCase(string _caseName, bool _bMoving)
        {
            caseName = _caseName;
            bMoving = _bMoving;
        }

        public override string Name => caseName;
        public override int Frames => 240;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.9f);
        public override Vector2 ViewSize => bMoving ? new Vector2(5.0f, 3.0f) : new Vector2(3.0f, 2.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            // 원판 그림·셰이더·기준점은 차량 프리팹의 RepairBox 값 그대로
            RepairBox repairBox = TownLootUtil.LoadPrefabComponent<RepairBox>(TownLootUtil.VehiclePath);
            gaugeLocalAnchor = TownLootUtil.GetField<Vector2>(repairBox, "gaugeLocalAnchor");

            box = _context.CreateEmpty("RepairBox", Vector3.zero).transform;
            gauge = AnvilRepairGauge.Create(box,
                TownLootUtil.GetField<Sprite>(repairBox, "gaugeBackgroundSprite"),
                TownLootUtil.GetField<Sprite>(repairBox, "gaugeOutlineSprite"),
                TownLootUtil.GetField<Sprite>(repairBox, "gaugeFillSprite"),
                TownLootUtil.GetField<Shader>(repairBox, "gaugeShader"));
            if (null == gauge) throw new System.InvalidOperationException("[VfxGolden] AnvilRepairGauge를 만들지 못했습니다.");

            fill = StartFill;
            bFull = false;
            bHidden = false;

            // BeginHammering
            gauge.Show(Anchor, fill);
            hammerTimer = HammerInterval;
            gauge.PlayHit();
        }

        private Vector3 Anchor => box.TransformPoint(gaugeLocalAnchor);

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (bMoving) box.position = new Vector3(Mathf.Sin(_frame * 0.04f) * 1.2f, Mathf.Sin(_frame * 0.09f) * 0.3f, 0.0f);

            if (bHidden) return;

            if (_frame >= HideFrame)
            {
                bHidden = true;
                gauge.Hide();
                return;
            }

            float dt = Time.deltaTime;
            if (false == bFull)
            {
                fill = Mathf.Min(1.0f, fill + FillPerFrame);
                if (fill >= 1.0f)
                {
                    bFull = true;
                    gauge.PlayHit();
                }
                else
                {
                    hammerTimer -= dt;
                    if (hammerTimer <= 0.0f)
                    {
                        gauge.PlayHit();
                        hammerTimer = Mathf.Max(hammerTimer + HammerInterval, 0.01f);
                    }
                }
            }

            gauge.SetState(Anchor, fill);
        }
    }

    /// <summary>
    /// 모루 수리 스파크: 차량 VFXComponent의 "RepairSpark"를 스파크 자리에서 재생하고 모루 그림 정렬 + 3으로 맞춘다.
    /// _hitCount번을 hammerInterval(0.5초 = 30프레임) 간격으로 두드린다(풀 재사용 포함).
    /// 원본: RepairBox.cs:PlayRepairFeedback
    /// </summary>
    public sealed class TownLootRepairSparkCase : VfxGoldenCase
    {
        private const int HammerFrames = 30;
        private const int AnvilSortingOrder = 10;

        private readonly string caseName;
        private readonly int hitCount;
        private VFXComponent pool;
        private Vector3 sparkPos;
        private int sortingLayerId;
        private int sortingOrder;

        public TownLootRepairSparkCase(string _caseName, int _hitCount)
        {
            caseName = _caseName;
            hitCount = Mathf.Max(1, _hitCount);
        }

        public override string Name => caseName;
        public override int Frames => (hitCount - 1) * HammerFrames + 140;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.4f);
        public override Vector2 ViewSize => new Vector2(8.0f, 6.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            RepairBox repairBox = TownLootUtil.LoadPrefabComponent<RepairBox>(TownLootUtil.VehiclePath);
            Transform sparkPoint = TownLootUtil.GetField<Transform>(repairBox, "repairSparkPoint");
            SpriteRenderer anvilRenderer = TownLootUtil.GetField<SpriteRenderer>(repairBox, "spriteRenderer");

            // 모루를 원점에 둔 채 스파크 자리(모루 기준 상대 위치)를 그대로 쓴다.
            sparkPos = null != sparkPoint ? repairBox.transform.InverseTransformPoint(sparkPoint.position)
                : (null != anvilRenderer ? repairBox.transform.InverseTransformPoint(anvilRenderer.transform.position) : Vector3.zero);
            sparkPos.z = 0.0f;
            sortingLayerId = null != anvilRenderer ? anvilRenderer.sortingLayerID : SortingLayer.NameToID("Objects");
            // 모루 정렬 순서는 CustomSortable이 위치로 정하므로 흔한 값으로 둔다.
            sortingOrder = AnvilSortingOrder + 3;

            pool = TownLootUtil.CreatePool(_context, TownLootUtil.VehiclePath, "RepairSpark");
            Hit(_context);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (0 == _frame % HammerFrames && _frame / HammerFrames < hitCount) Hit(_context);
        }

        private void Hit(VfxGoldenContext _context)
        {
            ParticleSystem effect = TownLootUtil.Play(_context, pool, "RepairSpark", sparkPos, Quaternion.identity, null);
            pool.SetSortingSettings(effect, sortingLayerId, sortingOrder);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // RestAreaVisual (VFX_RestAreaPlus + VFX_CampfireFlame)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 휴식처: 차량 프리팹 안의 RestArea 묶음(모닥불·타원·+ 입자·불꽃, 참조가 묶음 안에서 닫혀 있음)만 복제해
    /// StaminaRecoverCircle처럼 Initialize(null, radiusX, radiusY) 후 매 프레임 SetOpen을 부른다.
    /// 열림(타원 드러남·모닥불 튀어나옴·0.12초 뒤 점화) → 평소 상태(불꽃·+ 입자) → 닫힘(타원 페이드, 불꽃 즉시 멈춤) → 40프레임 대기.
    /// _bMoving이면 휴식처(차량)를 매 프레임 옮긴다.
    /// 원본: StaminaRecoverCircle.cs:Initialize / Update(restAreaVisual.SetOpen), RestAreaVisual.cs:SetOpen
    /// </summary>
    public sealed class TownLootRestAreaCase : VfxGoldenCase
    {
        private const int OpenFrame = 1;
        private const int CloseFrame = 210;

        private readonly string caseName;
        private readonly bool bMoving;
        private RestAreaVisual restArea;
        private bool bInitialized;

        public TownLootRestAreaCase(string _caseName, bool _bMoving)
        {
            caseName = _caseName;
            bMoving = _bMoving;
        }

        public override string Name => caseName;
        public override int Frames => 270;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.5f);
        public override Vector2 ViewSize => bMoving ? new Vector2(13.0f, 8.0f) : new Vector2(11.0f, 7.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            RestAreaVisual source = TownLootUtil.LoadPrefabComponent<RestAreaVisual>(TownLootUtil.VehiclePath);
            StaminaRecoverCircle circle = TownLootUtil.LoadPrefabComponent<StaminaRecoverCircle>(TownLootUtil.VehiclePath);

            GameObject instance = TownLootUtil.SpawnSubtree(_context, source.gameObject, Vector3.zero);
            restArea = instance.GetComponent<RestAreaVisual>();
            restArea.Initialize(null, circle.radiusX, circle.radiusY);
            bInitialized = true;
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (false == bInitialized) return;

            if (bMoving) restArea.transform.position = new Vector3(Mathf.Sin(_frame * 0.03f) * 1.0f, Mathf.Sin(_frame * 0.05f) * 0.4f, 0.0f);

            // StaminaRecoverCircle.Update: 특성이 열려 있는 동안 매 프레임 SetOpen(true)
            if (_frame >= OpenFrame) restArea.SetOpen(_frame < CloseFrame);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 전리품 기둥 (LootDisplayObject: VFX_LootPillarGlow / VFX_LootEvolve)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 마을 전리품 기둥: LootPillar 프리팹을 세우고 ApplySortingBasis → SetLootDisplay → SetGrowthTier(기둥 빛 재생).
    /// _evolveTo가 있으면 20프레임에 PlayGrowthEvolve(진화 연출 3초 + 폭발 시각에 후광 색 교체·뽀잉).
    /// (Initialize(InputManager)는 상호작용 키 연결뿐이라 부르지 않는다.)
    /// 원본: LootPillarManager.cs:SpawnAcquiredPillars / PlayPendingEvolutions
    /// </summary>
    public sealed class TownLootPillarCase : VfxGoldenCase
    {
        private const int EvolveFrame = 20;

        private readonly string caseName;
        private readonly LootType lootType;
        private readonly int tier;
        private readonly int evolveTo;
        private LootDisplayObject pillar;

        public TownLootPillarCase(string _caseName, LootType _lootType, int _tier, int _evolveTo)
        {
            caseName = _caseName;
            lootType = _lootType;
            tier = _tier;
            evolveTo = _evolveTo;
        }

        public override string Name => caseName;
        public override int Frames => 0 < evolveTo ? 250 : 150;
        public override Vector2 ViewCenter => new Vector2(0.0f, 1.5f);
        public override Vector2 ViewSize => new Vector2(8.0f, 10.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            GameObject go = _context.Spawn(TownLootUtil.LootPillarPath, Vector3.zero);
            pillar = go.GetComponent<LootDisplayObject>();
            pillar.ApplySortingBasis(go.transform.position.y);
            pillar.SetLootDisplay(lootType);
            pillar.SetGrowthTier(tier);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (0 < evolveTo && EvolveFrame == _frame) pillar.PlayGrowthEvolve(evolveTo);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 전리품 드랍 (LootItem: VFX_RareLootAppear + VFX_Beam)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 던전 전리품 드랍: 풀 생성(Prewarm) → 꺼냄(SetActive, ResetItem) → Initialize → SetSuckTarget(캐릭터) → Launch(무작위 포물선),
    /// 이후 매 프레임 ManualUpdate. 착지하면 바로 캐릭터로 빨려 들어가 도착 시 풀로 돌아간다(비활성).
    /// VFX_RareLootAppear(등장·착지·흡입 퇴장)와 VFX_Beam(ItemAuraEffectController 단발 버스트)이 함께 돈다.
    /// 원본: LootManager.cs:CreateLootItem / OnGetLootItem / SpawnLootItem / Update / OnReleaseLootItem
    /// </summary>
    public sealed class TownLootRareLootCase : VfxGoldenCase
    {
        private static readonly Vector3 SpawnPos = new Vector3(0.0f, 0.0f, 0.0f);
        private static readonly Vector3 CharacterCenter = new Vector3(1.6f, 0.4f, 0.0f);

        private readonly string caseName;
        private readonly LootType lootType;
        private LootItem lootItem;
        private bool bReleased;

        public TownLootRareLootCase(string _caseName, LootType _lootType)
        {
            caseName = _caseName;
            lootType = _lootType;
        }

        public override string Name => caseName;
        public override int Frames => 220;
        public override Vector2 ViewCenter => new Vector2(0.5f, 2.5f);
        public override Vector2 ViewSize => new Vector2(8.0f, 12.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            LootManager manager = TownLootUtil.LoadPrefabComponent<LootManager>(TownLootUtil.InstallerPath);
            LootItemTypeDataBase dataBase = TownLootUtil.GetField<LootItemTypeDataBase>(manager, "lootItemTypeDataBase");
            LootItemTypeData typeData = dataBase.Get(lootType);
            if (null == typeData) throw new System.ArgumentException("[VfxGolden] 전리품 데이터가 없습니다: " + lootType);

            // CreateLootItem
            GameObject go = _context.Spawn(TownLootUtil.LootItemPath, Vector3.zero);
            lootItem = go.GetComponent<LootItem>();
            lootItem.lootItemAcquiredEvent += OnLootItemArrived;
            lootItem.Prewarm();

            // OnGetLootItem
            lootItem.gameObject.SetActive(true);
            lootItem.ResetItem();
            bReleased = false;

            // SpawnLootItem
            Transform character = _context.CreateEmpty("CharacterCenter", CharacterCenter).transform;
            lootItem.transform.position = SpawnPos;
            lootItem.Initialize(typeData);
            lootItem.SetSuckTarget(character);

            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float randomDist = Random.Range(0.4f, 1.2f);
            Vector3 endPos = SpawnPos + new Vector3(randomDir.x, randomDir.y * 0.5f, 0.0f) * randomDist;
            float height = Random.Range(0.6f, 1.2f);
            float duration = Random.Range(0.3f, 0.6f);
            lootItem.Launch(SpawnPos, endPos, height, duration);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (bReleased) return;
            lootItem.ManualUpdate(Time.deltaTime);
        }

        // OnLootItemArrived → TryReleaseLootItem → OnReleaseLootItem(비활성). 획득 피드백(UI·소리)은 이 이펙트와 무관해 생략.
        private void OnLootItemArrived(LootItem _item)
        {
            if (bReleased) return;
            bReleased = true;
            _item.IsPooled = true;
            _item.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 보석 원목 오라 (LogItem.PlayGemAura → ItemAuraEffectController)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 보석 원목이 놓일 때 등급별 오라(LogItemController.logStateAuraDatas 프리셋)를 원목에 붙이고 Play, 매 프레임 SyncGemAura
    /// (그림 높이 위치·원목 정렬 -2), 흡입 시작 때 Stop 후 풀로 반납(컨트롤러 아래로 되돌리고 비활성).
    /// _bFlying이면 원목이 포물선으로 날아가는 동안(그림 높이가 바뀜) 따라가다가 버스트 도중에 흡입되어 끊긴다.
    /// 원본: LogItem.cs:PlayGemAura / SyncGemAura / StopGemAura, LogItemController.cs:GetAura(OnGetAura) / ReleaseAura(OnReleaseAura)
    /// </summary>
    public sealed class TownLootGemAuraCase : VfxGoldenCase
    {
        private const int LogSortingOrder = 10;

        private readonly string caseName;
        private readonly LogState logState;
        private readonly bool bFlying;
        private Transform controller;
        private Transform log;
        private Transform visual;
        private ItemAuraEffectController aura;
        private int sortingLayerId;
        private bool bStopped;

        public TownLootGemAuraCase(string _caseName, LogState _logState, bool _bFlying)
        {
            caseName = _caseName;
            logState = _logState;
            bFlying = _bFlying;
        }

        public override string Name => caseName;
        public override int Frames => 120;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.3f);
        public override Vector2 ViewSize => bFlying ? new Vector2(9.0f, 8.0f) : new Vector2(7.0f, 7.0f);

        private int StopFrame => bFlying ? 50 : 95;

        public override void Setup(VfxGoldenContext _context)
        {
            LogItemController source = TownLootUtil.LoadPrefabComponent<LogItemController>(TownLootUtil.InstallerPath);
            List<LogStateAuraData> datas = TownLootUtil.GetField<List<LogStateAuraData>>(source, "logStateAuraDatas");
            ItemAuraEffectController prefab = null;
            for (int i = 0; null != datas && i < datas.Count; i++)
            {
                if (datas[i].logState == logState && null != datas[i].auraPrefab)
                {
                    prefab = datas[i].auraPrefab;
                    break;
                }
            }
            if (null == prefab) throw new System.ArgumentException("[VfxGolden] 오라 프리셋이 없습니다: " + logState);

            sortingLayerId = SortingLayer.NameToID("Objects");
            controller = _context.CreateEmpty("LogItemController", Vector3.zero).transform;
            log = _context.CreateEmpty("LogItem", LogGround(0)).transform;
            visual = new GameObject("Visual").transform;
            visual.SetParent(log, false);
            visual.localPosition = new Vector3(0.0f, LogHeight(0), 0.0f);
            bStopped = false;

            // GetAura: createFunc(Instantiate(prefab, 컨트롤러)) → OnGetAura(SetActive(true))
            aura = Object.Instantiate(prefab, controller);
            aura.gameObject.SetActive(true);

            // PlayGemAura
            Transform auraTransform = aura.transform;
            auraTransform.SetParent(log, false);
            auraTransform.localPosition = Vector3.zero;
            SyncGemAura();
            aura.Play();
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (bStopped) return;

            log.position = LogGround(_frame);
            visual.localPosition = new Vector3(0.0f, LogHeight(_frame), 0.0f);

            if (_frame >= StopFrame)
            {
                // StopGemAura → ReleaseAura(OnReleaseAura)
                bStopped = true;
                aura.Stop();
                aura.transform.SetParent(controller, false);
                aura.transform.localPosition = Vector3.zero;
                aura.gameObject.SetActive(false);
                return;
            }

            SyncGemAura();
        }

        private void SyncGemAura()
        {
            aura.transform.localPosition = visual.localPosition;
            aura.SetSortingLayer(sortingLayerId);
            aura.SetSortingOrder(LogSortingOrder - 2);
        }

        // 날아가는 변형: 60프레임 동안 지면은 직선으로, 그림 높이는 포물선(LogItem 발사와 같은 모양)
        private Vector3 LogGround(int _frame)
        {
            if (false == bFlying) return Vector3.zero;
            float t = Mathf.Clamp01(_frame / 60.0f);
            return Vector3.Lerp(new Vector3(-1.5f, -0.5f, 0.0f), new Vector3(1.5f, 0.3f, 0.0f), t);
        }

        private float LogHeight(int _frame)
        {
            if (false == bFlying) return 0.0f;
            float t = Mathf.Clamp01(_frame / 60.0f);
            return -4.0f * 1.0f * (t - 0.5f) * (t - 0.5f) + 1.0f;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 풀 파티클
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 보석 원목 반짝임(GameInstaller "Shiny", VFX_Shiny_World 루프): 원목에 붙여 재생 → 크기 1 → 정렬을 원목 + 1로 맞춤(매 프레임 SyncParticleSorting)
    /// → 흡입 때 Stop(방출만 멈춤, 남은 입자가 사라지면 풀로 복귀).
    /// 원본: LogItem.cs:PlayShinyEffect / SyncParticleSorting / (흡입 시작의 vfxComponent.Stop(particleEffect, false))
    /// </summary>
    public sealed class TownLootLogShinyCase : VfxGoldenCase
    {
        private const int LogSortingOrder = 10;
        private const int StopFrame = 150;

        private VFXComponent pool;
        private ParticleSystem effect;
        private Transform log;
        private int sortingLayerId;

        public override string Name => "TownLoot_Pool_Shiny_LogItem";
        public override int Frames => 260;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.3f);
        public override Vector2 ViewSize => new Vector2(4.0f, 4.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            sortingLayerId = SortingLayer.NameToID("Objects");
            log = _context.CreateEmpty("LogItem", Vector3.zero).transform;

            pool = TownLootUtil.CreatePool(_context, TownLootUtil.InstallerPath, "Shiny");
            effect = TownLootUtil.Play(_context, pool, "Shiny", log.position, log.rotation, log);
            effect.transform.localScale = Vector3.one;
            pool.SetSortingSettings(effect, sortingLayerId, LogSortingOrder + 1);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (null == effect) return;

            if (StopFrame == _frame)
            {
                if (effect.transform.IsChildOf(log)) pool.Stop(effect, false);
                effect = null;
                return;
            }

            if (false == effect.transform.IsChildOf(log))
            {
                effect = null;
                return;
            }
            pool.SetSortingSettings(effect, sortingLayerId, LogSortingOrder + 1);
        }
    }

    /// <summary>
    /// 캐릭터 하차 먼지(GameInstaller "CharacterGetOffEffect"): 부모·정렬 오버라이드 없이 캐릭터 위치에서 한 번.
    /// 원본: InDungeonVFXManager.cs:PlayCharacterGetOffVFX
    /// </summary>
    public sealed class TownLootGetOffCase : VfxGoldenCase
    {
        public override string Name => "TownLoot_Pool_CharacterGetOff";
        public override int Frames => 60;
        public override Vector2 ViewSize => new Vector2(4.0f, 3.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            _context.PlayPooled(new VFXPlaySettings("CharacterGetOffEffect", Vector3.zero, Quaternion.identity));
        }
    }

    /// <summary>
    /// 커터 절단 톱밥(Cutter 프리팹 VFXComponent "CuttingEffect", VFX_SawDust 루프): 정방향(80프레임)마다 톱밥 자리에 붙여 색을 덮어 재생,
    /// 역방향(40프레임)에는 Stop(방출만 멈춤). 원목 두 개(각 2초, 왕복 1회)를 켠 뒤 남은 입자가 사라질 때까지 기다린다.
    /// _bGem이면 보석 원목 등급 색(Perfect), 아니면 나무 종류 색(Cutter 프리팹 treeVFXColorDatas 첫 항목).
    /// 원본: LogCutter.cs:UpdateVFXState / PlayCuttingEffect / StopCuttingEffect / GetVFXColorForCurrentTree
    /// </summary>
    public sealed class TownLootCuttingEffectCase : VfxGoldenCase
    {
        // 가공 2초짜리 원목: 왕복 1회(정방향 : 역방향 = 2 : 1, LogCutter.GetCycleCount)
        private const int CycleFrames = 120;
        private const int ForwardFrames = 80;
        private const int CycleCount = 2;

        private readonly string caseName;
        private readonly bool bGem;
        private VFXComponent pool;
        private Transform effectTransform;
        private ParticleSystem.MinMaxGradient color;
        private ParticleSystem cuttingEffect;
        private bool bWasForward;

        public TownLootCuttingEffectCase(string _caseName, bool _bGem)
        {
            caseName = _caseName;
            bGem = _bGem;
        }

        public override string Name => caseName;
        public override int Frames => CycleCount * CycleFrames + 50;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.5f);
        public override Vector2 ViewSize => new Vector2(4.0f, 4.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            LogCutter cutter = TownLootUtil.LoadPrefabComponent<LogCutter>(TownLootUtil.CutterPath);
            Transform sourcePoint = TownLootUtil.GetField<Transform>(cutter, "effectTransform");

            color = new ParticleSystem.MinMaxGradient(Color.white);
            if (bGem)
            {
                List<LogStateVFXColorData> gemColors = TownLootUtil.GetField<List<LogStateVFXColorData>>(cutter, "logStateVFXColorDatas");
                for (int i = 0; null != gemColors && i < gemColors.Count; i++)
                {
                    if (LogState.Perfect == gemColors[i].logState)
                    {
                        color = gemColors[i].effectColor;
                        break;
                    }
                }
            }
            else
            {
                List<TreeVFXColorData> treeColors = TownLootUtil.GetField<List<TreeVFXColorData>>(cutter, "treeVFXColorDatas");
                if (null != treeColors && 0 < treeColors.Count) color = treeColors[0].effectColor;
            }

            // 커터를 원점에 둔 채 톱밥 자리(커터 자식 EffectPoint)를 같은 로컬 위치·회전으로 만든다.
            Transform cutterRoot = _context.CreateEmpty("Cutter", Vector3.zero).transform;
            effectTransform = new GameObject("EffectPoint").transform;
            effectTransform.SetParent(cutterRoot, false);
            if (null != sourcePoint)
            {
                effectTransform.localPosition = sourcePoint.localPosition;
                effectTransform.localRotation = sourcePoint.localRotation;
                effectTransform.localScale = sourcePoint.localScale;
            }

            pool = TownLootUtil.CreatePool(_context, TownLootUtil.CutterPath, "CuttingEffect");
            bWasForward = false;
            cuttingEffect = null;
            UpdateVFXState(_context, 0);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            UpdateVFXState(_context, _frame);
        }

        private void UpdateVFXState(VfxGoldenContext _context, int _frame)
        {
            bool bForward = _frame < CycleCount * CycleFrames && (_frame % CycleFrames) < ForwardFrames;
            if (bForward == bWasForward) return;
            bWasForward = bForward;

            if (bForward)
            {
                // PlayCuttingEffect
                if (null != cuttingEffect) pool.Stop(cuttingEffect, true);
                VFXPlaySettings settings = new VFXPlaySettings("CuttingEffect", effectTransform.position, effectTransform.rotation, color, effectTransform);
                cuttingEffect = TownLootUtil.Play(_context, pool, settings);
            }
            else if (null != cuttingEffect)
            {
                // StopCuttingEffect
                pool.Stop(cuttingEffect);
                cuttingEffect = null;
            }
        }
    }

    /// <summary>
    /// 강아지 발 먼지(Puppy 프리팹 VFXComponent "Dust"): 달리는 동안 dustInterval(0.2초)마다 진행 방향 뒤쪽 dustOffsetDistance에
    /// 부모 없이 재생하고 몸 렌더러 레이어 + SortingGroup 순서(-y x 100)로 정렬. 1초 달린 뒤 멈춘다.
    /// 원본: Puppy.cs:UpdateDust / LateUpdate(sortingGroup.sortingOrder)
    /// </summary>
    public sealed class TownLootPuppyDustCase : VfxGoldenCase
    {
        private const int RunFrames = 60;
        private static readonly Vector2 StartPos = new Vector2(-1.6f, -0.8f);
        private static readonly Vector2 RunDir = new Vector2(0.894f, 0.447f);

        private VFXComponent pool;
        private int bodySortingLayerId;
        private float dustInterval;
        private float dustOffsetDistance;
        private float runSpeed;
        private float dustTimer;
        private Vector2 position;
        private int sortingOrder;

        public override string Name => "TownLoot_Pool_PuppyDust";
        public override int Frames => RunFrames + 70;
        public override Vector2 ViewCenter => new Vector2(0.0f, 0.0f);
        public override Vector2 ViewSize => new Vector2(6.0f, 4.0f);

        public override void Setup(VfxGoldenContext _context)
        {
            Puppy puppy = TownLootUtil.LoadPrefabComponent<Puppy>(TownLootUtil.PuppyPath);
            SpriteRenderer body = TownLootUtil.GetField<SpriteRenderer>(puppy, "bodyRenderer");
            bodySortingLayerId = null != body ? body.sortingLayerID : SortingLayer.NameToID("Objects");
            dustInterval = TownLootUtil.GetField<float>(puppy, "dustInterval");
            dustOffsetDistance = TownLootUtil.GetField<float>(puppy, "dustOffsetDistance");
            runSpeed = TownLootUtil.GetField<float>(puppy, "runSpeed");

            pool = TownLootUtil.CreatePool(_context, TownLootUtil.PuppyPath, "Dust");
            position = StartPos;
            dustTimer = 0.0f;
            sortingOrder = -Mathf.RoundToInt(position.y * 100.0f);
        }

        public override void OnFrame(VfxGoldenContext _context, int _frame)
        {
            if (_frame > RunFrames) return;

            float dt = Time.deltaTime;
            Vector2 before = position;
            position += RunDir * runSpeed * dt;
            Vector2 moveDelta = position - before;

            dustTimer += dt;
            if (dustTimer >= dustInterval)
            {
                dustTimer -= dustInterval;
                Vector2 spawn = position - moveDelta.normalized * dustOffsetDistance;
                ParticleSystem effect = TownLootUtil.Play(_context, pool, "Dust", spawn, Quaternion.identity, null);
                pool.SetSortingSettings(effect, bodySortingLayerId, sortingOrder);
            }

            // LateUpdate: 몸이 그려질 순서(다음 재생에 쓰임)
            sortingOrder = -Mathf.RoundToInt(position.y * 100.0f);
        }
    }
}

#endif
