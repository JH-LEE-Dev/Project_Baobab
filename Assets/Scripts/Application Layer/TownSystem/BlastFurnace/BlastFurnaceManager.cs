// ─────────────────────────────────────────────────────────────────────────────
// [죽은 코드] 원석 / 용광로 계열.
//
// SYSTEM_VAR.GEM_ORE_SYSTEM_ENABLED 가 false 라서 이 파일의 코드는 한 줄도 실행되지
// 않는다. 원석이 게임에 들어오는 입구(InDungeonObjectManager.OnTreeDead)가 막혀 있어
// 원석 아이템이 생성되지 않고, 용광로도 열리지 않는다.
//
// 버그 / 회귀 검토 대상에서 제외한다. 동작하지 않는 코드라 여기서 나오는 지적은 의미가 없다.
// 되살릴 때는 Assets/Scripts/Global/System.cs 의 그 스위치를 true 로 바꾸면 되고,
// 그 시점에 이 헤더들도 같이 지워야 한다.
// ─────────────────────────────────────────────────────────────────────────────

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 마을 용광로 배치를 담당한다. 원석 등급마다 한 대씩(황금/다이아/프리즘) 세운다.
///
/// 자리는 Grid 프리팹의 BlastColliderTilemap에 찍힌 타일이 정한다. 타일을 옮기면 용광로도
/// 따라 움직인다 - 충돌 타일과 배치 위치를 따로 관리하면 반드시 어긋나기 때문에, 한쪽(타일)만
/// 보고 다른 쪽을 유도한다.
///
/// 맞추는 기준은 "용광로 밑면 마름모가 콜라이더 마름모 안에 들어앉는 것"이다.
///
/// 기준을 셀이나 타일 그림이 아니라 '콜라이더 도형'에서 직접 읽는다. 이 타일맵의 타일은
/// colliderType이 Sprite라 실제 충돌 도형은 스프라이트의 물리 도형(32x16 마름모)인데,
/// 그 도형이 셀 중심보다 7px 위에 놓여 있다. 셀(32x16)이나 타일 그림(32x32)으로 계산하면
/// 씬 뷰의 초록색 콜라이더와 어긋난다. 물리 도형을 읽으므로 타일을 바꿔도 따라간다.
///
/// 던전에 가 있는 동안에도 제련은 계속 돌아야 하므로, 용광로를 끄지 않는다. LogProcessingManager가
/// 제재소를 다루는 방식(Enable/DisableShopObj)을 그대로 따라 화면 밖으로 치워두기만 한다.
/// GameObject를 비활성화하면 Update가 멎어 그 사이 제련이 통째로 멈춘다.
///
/// 생성은 Initialize()에서 한 번만 한다. 메인메뉴에서 곧바로 던전으로 들어가는 경로에서는
/// StartTownSystem()이 한 번도 돌지 않으므로, 배치(ApplyPlacement)를 기다렸다 만들면 그 회차
/// 내내 용광로가 존재하지 않게 된다. 제재소(shopObj)를 Initialize에서 만드는 것과 같은 이유다.
/// </summary>
public class BlastFurnaceManager : MonoBehaviour, IBlastFurnaceCH
{
    /// <summary>
    /// 용광로 상호작용 범위 진입/이탈. TownSystem이 받아 시그널로 올리고, 캐릭터 위 E키 안내가 켜진다.
    /// 세 대가 한 덩어리이므로 창구(가운데) 한 대의 상태만 올린다.
    /// </summary>
    public event System.Action<bool> InteractStateChangedEvent;

    /// <summary>
    /// 용광로 구성이 바뀌었을 때(해금/가공 시작·종료/쌓인 원석 변화) 한 번 알린다.
    /// UI가 위젯을 다시 짜야 하는 시점이며, 진행도처럼 매 프레임 변하는 값은 이 이벤트가 아니라
    /// 넘겨준 목록(UIDatas)을 그대로 읽으면 된다.
    /// </summary>
    public event System.Action<System.Collections.Generic.IReadOnlyList<BlastFurnaceUIData>> FurnaceStateChangedEvent;

    // 아래 값들은 전부 LogContainer가 원목을 날릴 때 쓰는 것과 같은 값이다.
    // 원목 납품과 눈에 보이는 것도 손맛도 같아야 해서 숫자를 새로 잡지 않고 그대로 맞췄다.
    private const float OreFlyInterval = 0.075f;    // LogContainer.FLY_INTERVAL
    private const float FlyDuration = 0.5f;
    private const float OreFlyHeightMin = 0.8f;
    private const float OreFlyHeightMax = 1.2f;

    // 마을 밖에 있을 때 용광로를 치워두는 자리. 제재소가 쓰는 (-99, -99)와 같은 구석이고,
    // 하이라키/씬 뷰에서 세 대가 겹쳐 보이지 않도록 한 대씩 벌려 세운다.
    private static readonly Vector3 OffscreenOrigin = new Vector3(-99f, -99f, 0f);
    private const float OffscreenSpacing = 2f;

    // 콜라이더 마름모의 아래 꼭짓점에서 얼마나 올려야 용광로 밑면 마름모의 중심이
    // 콜라이더 마름모의 중심과 겹치는지. 용광로 밑면은 아랫줄에서 6px 올라간 곳이 가장 넓고,
    // 콜라이더 마름모(32x16)는 아래 꼭짓점에서 8px 올라간 곳이 가장 넓다. 그 둘을 맞추려면
    // 8 - 6 = 2px 올리면 된다. (그림 아랫줄이 마름모 아래 꼭짓점보다 2px 위에 놓인다)
    private const float BaseNestingLift = 2f / 32f;

    [SerializeField] private BlastFurnace blastFurnacePrefab;

    [Tooltip("세울 용광로 목록. 타일을 '가장 왼쪽 아래'부터 훑으며 이 순서대로 자리를 준다.")]
    [SerializeField] private GemOreType[] oreOrder = { GemOreType.Gold, GemOreType.Diamond, GemOreType.Prism };

    [Tooltip("셀 중심에서 추가로 밀어줄 양. 타일 안에서 미세하게 위치를 다듬고 싶을 때만 쓴다.")]
    [SerializeField] private Vector2 placementOffset = Vector2.zero;

    [Tooltip("세 대를 한 덩어리로 다룬다. 한 대의 콜라이더만 열고 나머지는 닫는다.")]
    [SerializeField] private bool bSingleInteractionCollider = true;

    [Tooltip("상호작용 콜라이더를 고정할 자리. 증설(해금 수)과 무관하게 항상 이 등급의 용광로 위치에 둔다.")]
    [SerializeField] private GemOreType interactionAnchorType = GemOreType.Diamond;

    [Header("Smelting")]
    [Tooltip("등급별 가공 규칙. 기획 표(필요 원석 / 가공량 / 주괴 가치)를 그대로 넣는다.")]
    [SerializeField] private BlastFurnaceRecipe[] recipes;

    [Tooltip("원석/주괴가 날아갈 때 쓰는 비행 오브젝트 프리팹.")]
    [SerializeField] private FlyingSpriteItem flyingItemPrefab;

    [Tooltip("비행 그림이 그려질 정렬 레이어. 원목 납품 연출과 같은 곳에 둔다.")]
    [SerializeField] private string flyingSortingLayer = "FlyingItem";

    private readonly List<BlastFurnace> furnaces = new List<BlastFurnace>(3);

    // 타일이 정해준 마을에서의 자리. 던전에 다녀와도 여기로 되돌아온다.
    private readonly List<Vector3> homePositions = new List<Vector3>(3);

    // 타일에서 읽어온 원본 자리. 여기서 용광로 수에 맞춰 고르게 나눈 결과가 homePositions다.
    private readonly List<Vector3> tilePositions = new List<Vector3>(4);

    // UI에 넘길 상태 묶음. 매 프레임 갱신하되 목록 자체는 재사용하므로 할당이 생기지 않고,
    // UI가 참조를 들고 있으면 진행도가 실시간으로 따라 움직인다.
    private readonly List<BlastFurnaceUIData> uiDatas = new List<BlastFurnaceUIData>(3);

    /// <summary>
    /// 각 용광로의 위치 / 원석 등급 / 해금 여부 / 가공 상태. UI는 이것만 읽으면 된다.
    /// 목록 길이는 용광로 수로 고정이고, 잠긴 것도 unlocked=false로 들어 있다.
    /// </summary>
    public IReadOnlyList<BlastFurnaceUIData> UIDatas => uiDatas;

    // 위 이벤트를 언제 쏠지 판단하기 위한 직전 상태
    private int lastNotifiedUnlockCount = -1;
    private int lastNotifiedStoredSum = -1;
    private int lastNotifiedRunningMask = -1;

    // 타일 물리 도형을 읽을 때 쓰는 재사용 버퍼.
    private static readonly List<Vector2> physicsShapeBuffer = new List<Vector2>(8);

    // 상호작용 경합 상대. 집(Home)이 바로 옆이라 둘을 한자리에서 비교한다.
    private Tent tent;
    private Character character;

    // 세 대를 대표해 상호작용을 받는 용광로(가운데). 나머지는 콜라이더가 꺼져 있다.
    private BlastFurnace interactionOwner;

    // 특성으로 열린 용광로 수. 0이면 마을에 용광로가 한 대도 없다.
    private int unlockedCount = 0;

    // 가공 속도 배율. "용광로 가속" 특성이 누적으로 올린다(1 = 초당 1).
    private float speedMultiplier = 1f;

    // 외부 의존성
    private InputManager inputManager;
    private InventoryManager inventory;
    private ShopNPC shopNPC;
    private Transform characterTransform;

    // "아이템 전송 속도" 특성이 걸린 배율을 원목 납품과 같이 쓰기 위해 참조한다.
    // 이 값을 복제해 두면 특성이 갱신될 때 한쪽만 바뀌어 어긋나므로, 원본을 그때그때 읽는다.
    private LogContainer logContainer;

    // "원목 판매 가치" 특성 배율을 주괴 판매에도 걸기 위해 참조한다. 위와 같은 이유로 그때그때 읽는다.
    private LogProcessingManager logProcessingManager;

    // 한 번에 한 개씩만 날리기 위한 회전 인덱스. 원목도 슬롯 하나씩 직렬로 보내므로 같은 방식으로 맞췄다.
    private int oreSendCursor = 0;

    // 착지 사운드의 피치/볼륨 상승. 값과 규칙 모두 LogContainer가 원목을 받을 때와 같다.
    private const float DepositPitchMin = 1.0f;
    private const float DepositPitchMax = 1.5f;
    private const float DepositPitchStep = 0.05f;
    private const float DepositVolumeBoostMax = 1.3f;
    private const float DepositPitchResetTimeout = 1f;

    private float currentDepositPitch = DepositPitchMin;
    private float lastDepositPitchTime = -999f;

    // 마을에 있는지. 용광로는 던전에 가 있는 동안에도 계속 돌아 주괴가 완성되므로,
    // 그때 소리와 카메라 흔들림이 던전에서 터지지 않도록 막는다.
    // (LogContainer.GetSoundVolume()이 mapType으로 같은 일을 한다)
    private bool bInTown = false;

    private float GetSoundVolume()
    {
        return bInTown ? 1f : 0f;
    }

    // 자동 투입 "세션"이 살아 있는지. 상호작용 키를 누르는 순간 true가 되고, 키를 떼도 유지된다
    // (LogContainer/OffroadContainer와 동일하게 한 번 누르면 계속 들어간다). 더 넣을 원석이 없거나
    // 상호작용 범위를 벗어나면 false가 되어 즉시 멈춘다.
    private bool bAutoInsertActive = false;
    private float oreFlyTimer = 0f;

    // 날아다니는 원석/주괴
    private readonly List<FlyingSpriteItem> flyingItems = new List<FlyingSpriteItem>(16);
    private readonly Stack<FlyingSpriteItem> flyingPool = new Stack<FlyingSpriteItem>(16);
    private readonly List<FlyingSpriteItem> flyingCleanup = new List<FlyingSpriteItem>(16);

    /// <summary>상호작용 창구를 맡고 있는 용광로. 제련 UI가 붙을 때 기준점으로 쓰면 된다.</summary>
    public BlastFurnace InteractionOwner => interactionOwner;

    public IReadOnlyList<BlastFurnace> Furnaces => furnaces;

    /// <summary>
    /// 용광로를 만들어 둔다. 아직 자리를 모르므로 일단 화면 밖에 세워두고,
    /// 마을에 들어올 때 ApplyPlacement()가 타일 자리로 옮긴다.
    /// </summary>
    /// <summary>
    /// 상호작용 경합에 함께 넣을 집(Home). 용광로 자리가 집과 가까워 트리거가 겹치므로,
    /// "가장 가까운 하나만 선택"에 집도 후보로 들어가야 한다.
    /// </summary>
    public void SetTent(Tent _tent)
    {
        tent = _tent;
    }

    public void SetCharacter(Character _character)
    {
        character = _character;
        characterTransform = _character != null ? _character.centerTransform : null;
    }

    /// <summary>제련에 필요한 나머지 의존성. TownSystem이 넣어준다.</summary>
    public void SetDependencies(InputManager _inputManager, InventoryManager _inventory, ShopNPC _shopNPC,
                                LogProcessingManager _logProcessingManager)
    {
        inputManager = _inputManager;
        inventory = _inventory;
        shopNPC = _shopNPC;
        logProcessingManager = _logProcessingManager;
        logContainer = _logProcessingManager != null ? _logProcessingManager.logContainer : null;

        BindInput();
    }

    /// <summary>
    /// 주괴 한 개의 실제 판매 가격. 표의 기본 가치에 "원목 판매 가치" 특성 배율을 곱한다.
    /// 원목과 같은 배율을 그대로 쓰므로, 특성을 올리면 주괴 값도 함께 오른다.
    /// </summary>
    private long GetIngotPrice(BlastFurnaceRecipe _recipe)
    {
        float multiplier = logProcessingManager != null ? logProcessingManager.LogValueMultiplier : 1f;

        return (long)System.Math.Round((double)_recipe.ingotValue * multiplier);
    }

    /// <summary>
    /// 원목 납품에 걸리는 것과 같은 전송 속도 배율. "아이템 전송 속도" 특성이 올린다.
    /// </summary>
    private float GetTransferSpeedMultiplier()
    {
        return logContainer != null ? Mathf.Max(0.01f, logContainer.itemTransferSpeedMul) : 1f;
    }

    private void BindInput()
    {
        if (inputManager == null || inputManager.inputReader == null) return;

        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
        inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;
    }

    public void Release()
    {
        if (inputManager == null || inputManager.inputReader == null) return;

        inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
    }

    /// <summary>
    /// 상호작용 키는 "한 번 누르면 자동 투입 시작"이다(보관함과 동일). 키를 떼도 계속 들어가고,
    /// 넣을 원석이 없거나 열린 용광로가 전부 가득 차면 스스로 끝난다. 도중에 상호작용 범위를
    /// 벗어나면 UpdateOreInsertion에서 즉시 취소된다.
    /// </summary>
    private void InteractionKeyPressed()
    {
        // 용광로 앞이 아닐 때 누른 키로 세션이 켜지지 않도록 시작 시점에도 한 번 확인한다.
        if (false == IsInteractable()) return;

        bAutoInsertActive = true;

        // 누르자마자 한 개는 바로 들어가도록(원목 납품도 첫 개는 즉시 나간다)
        oreFlyTimer = OreFlyInterval;
    }

    // ── 특성 (IBlastFurnaceCH) ────────────────────────────────────────────

    /// <summary>
    /// "용광로" 특성. 누적 수치만큼 용광로를 연다(1 = 황금, 2 = 다이아, 3 = 프리즘 순서).
    /// Undo로 음수가 들어오면 그만큼 닫는다.
    /// </summary>
    public void IncreaseFurnaceCount(float _amount)
    {
        // 원석 계열이 꺼져 있으면 특성을 찍어도 용광로가 열리지 않는다.
        // 현재는 어느 특성 노드도 이 커맨드를 주지 않아 어차피 0이지만,
        // 나중에 노드가 붙었을 때 스위치를 되돌리지 않고 용광로만 살아나는 일이 없도록 여기서 막는다.
        if (false == SYSTEM_VAR.GEM_ORE_SYSTEM_ENABLED) return;

        unlockedCount = Mathf.Clamp(unlockedCount + Mathf.RoundToInt(_amount), 0, furnaces.Count);
        ApplyUnlockState();
    }

    /// <summary>
    /// "용광로 가속" 특성. 가공 속도가 N% 증가한다. 누적 퍼센트를 배율로 바꿔 각 용광로에 전달한다.
    /// </summary>
    public void IncreaseProcessingSpeed(float _percent)
    {
        speedMultiplier = Mathf.Max(0f, speedMultiplier + (_percent * 0.01f));
        ApplySpeedMultiplier();
    }

    private void ApplySpeedMultiplier()
    {
        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i] != null) furnaces[i].SetSpeedMultiplier(speedMultiplier);
        }
    }

    /// <summary>
    /// 열린 만큼만 용광로를 보여준다. 잠긴 용광로는 GameObject를 꺼서 화면에도 상호작용에도 안 잡힌다.
    /// 자리는 세 대 기준으로 미리 계산해 두므로, 몇 대가 열려 있든 황금은 항상 같은 자리에 선다.
    /// </summary>
    private void ApplyUnlockState()
    {
        // 창구 자리는 먼저 정해둔다. 그 자리의 용광로는 잠겨 있어도 오브젝트를 꺼선 안 되기 때문이다.
        interactionOwner = FindInteractionOwner();

        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i] == null) continue;

            bool unlocked = i < unlockedCount;

            // 창구를 맡은 용광로는 잠겨 있어도 오브젝트를 켜둔다(콜라이더가 살아 있어야 하므로).
            // 대신 그림은 감춰서 해금 전에는 보이지 않게 한다.
            bool keepAlive = unlocked || furnaces[i] == interactionOwner;

            furnaces[i].gameObject.SetActive(keepAlive);
            furnaces[i].SetVisualVisible(unlocked);
        }

        ApplyInteractionOwner();
    }

    public bool IsUnlocked(BlastFurnace _furnace)
    {
        int index = furnaces.IndexOf(_furnace);
        return index >= 0 && index < unlockedCount;
    }

    public void Initialize()
    {
        if (blastFurnacePrefab == null || oreOrder == null) return;
        if (furnaces.Count > 0) return;

        for (int i = 0; i < oreOrder.Length; i++)
        {
            BlastFurnace furnace = Instantiate(blastFurnacePrefab, GetOffscreenPosition(i), Quaternion.identity, transform);
            furnace.SetGemOreType(oreOrder[i]);
            furnace.SetRecipe(FindRecipe(oreOrder[i]));
            furnace.SetSpeedMultiplier(speedMultiplier);

            furnace.IngotCompletedEvent -= IngotCompleted;
            furnace.IngotCompletedEvent += IngotCompleted;

            furnaces.Add(furnace);
        }

        ApplyUnlockState();
    }

    private BlastFurnaceRecipe FindRecipe(GemOreType _gemOreType)
    {
        if (recipes != null)
        {
            for (int i = 0; i < recipes.Length; i++)
            {
                if (recipes[i].gemOreType == _gemOreType) return recipes[i];
            }
        }

        return default;
    }

    /// <summary>
    /// 상호작용 창구를 가운데 한 대로 몰아준다. 용광로 셋이 붙어 있어 각자 콜라이더를 들고 있으면
    /// 어느 대에 반응하는지가 서 있는 위치마다 달라지므로, 가운데 콜라이더 하나만 남긴다.
    ///
    /// "가운데"는 개수가 아니라 실제 위치로 고른다(양 끝의 중점에 가장 가까운 대). 타일을 늘려
    /// 간격이 바뀌어도 따라간다.
    /// </summary>
    private void ApplyInteractionOwner()
    {
        if (false == bSingleInteractionCollider)
        {
            interactionOwner = null;

            for (int i = 0; i < furnaces.Count; i++)
            {
                if (furnaces[i] != null) furnaces[i].SetInteractionEnabled(true);
            }

            return;
        }

        interactionOwner = FindInteractionOwner();

        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i] == null) continue;

            furnaces[i].SetInteractionEnabled(furnaces[i] == interactionOwner);
        }

        UpdateGroupOutline();

        // 해금 상태가 바뀐 직후에도 UI가 바로 최신 값을 읽을 수 있게 한 번 갱신한다
        // (Update를 기다리면 한 프레임 동안 옛 값이 보인다).
        UpdateUIDatas();
    }

    /// <summary>
    /// 상호작용 창구를 맡을 용광로. 증설과 무관하게 항상 같은 자리(기본값: 다이아 용광로)다.
    ///
    /// 해금 수에 따라 창구가 옮겨다니면 용광로를 하나 더 열 때마다 말을 거는 위치가 달라져
    /// 플레이어 입장에서 기준이 흔들린다. 그래서 자리를 고정한다.
    /// 그 자리의 용광로가 아직 잠겨 있어도 오브젝트는 켜둔 채 그림만 감추므로 콜라이더는 살아 있다.
    /// </summary>
    private BlastFurnace FindInteractionOwner()
    {
        if (unlockedCount <= 0 || furnaces.Count == 0) return null;

        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i] != null && furnaces[i].GemOreType == interactionAnchorType) return furnaces[i];
        }

        // 지정한 등급이 목록에 없으면 가운데 자리로 대신한다.
        return furnaces[furnaces.Count / 2];
    }

    /// <summary>
    /// 타일에 찍힌 자리를 읽어 용광로를 그리로 옮긴다. TownSystem이 Grid를 만든 직후에 호출한다.
    /// </summary>
    public void ApplyPlacement(Tilemap _blastColliderTilemap)
    {
        CollectPlacementPositions(_blastColliderTilemap, tilePositions);
        SpreadOverTiles(tilePositions, furnaces.Count, homePositions);

        MoveToTown();

        // 자리가 정해진 뒤에 "가운데"가 확정되므로 여기서 다시 고른다.
        ApplyUnlockState();
    }

    /// <summary>
    /// 타일 줄 위에 용광로를 고르게 벌려 세운다. 첫 대는 첫 타일, 마지막 대는 마지막 타일에 놓고
    /// 나머지는 그 사이를 균등하게 나눈 지점에 둔다. 나눈 지점이 타일과 타일 사이면 그 중간에 선다.
    ///
    /// 타일 수와 용광로 수가 같으면 결국 타일마다 한 대씩이 되므로, 칸을 늘리거나 줄이는 것만으로
    /// 간격을 조절할 수 있다. (예: 4칸 + 3대 -> 양 끝 타일에 하나씩, 가운데 한 대는 2·3번째 타일 사이)
    /// </summary>
    private static void SpreadOverTiles(List<Vector3> _tilePositions, int _count, List<Vector3> _outPositions)
    {
        _outPositions.Clear();

        if (_tilePositions.Count == 0 || _count <= 0) return;

        float lastIndex = _tilePositions.Count - 1;

        if (_count == 1)
        {
            _outPositions.Add(SampleAlongTiles(_tilePositions, lastIndex * 0.5f));
            return;
        }

        for (int i = 0; i < _count; i++)
        {
            _outPositions.Add(SampleAlongTiles(_tilePositions, lastIndex * (i / (float)(_count - 1))));
        }
    }

    /// <summary>타일 목록을 하나의 줄로 보고 실수 인덱스 위치를 보간해서 돌려준다.</summary>
    private static Vector3 SampleAlongTiles(List<Vector3> _tilePositions, float _index)
    {
        int low = Mathf.Clamp(Mathf.FloorToInt(_index), 0, _tilePositions.Count - 1);
        int high = Mathf.Clamp(low + 1, 0, _tilePositions.Count - 1);

        return Vector3.Lerp(_tilePositions[low], _tilePositions[high], _index - low);
    }

    /// <summary>마을에 도착했을 때. 타일이 정해준 자리로 되돌린다.</summary>
    public void MoveToTown()
    {
        bInTown = true;

        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i] == null) continue;

            // 타일이 하나도 없으면 자리를 못 받는다. 그때는 화면 밖에 그대로 둔다.
            if (i >= homePositions.Count) continue;

            furnaces[i].transform.position = homePositions[i] + (Vector3)placementOffset;

            // 자리에 놓인 뒤에 가동 상태를 한 번 더 물려준다. 세이브 로드는 용광로가 아직 화면
            // 밖에 있을 때 제련을 복원하는데, 파티클은 화면 밖에서 멈추므로 그때 건 Play()가
            // 살아나지 않는다. 여기서 다시 걸지 않으면 돌고 있는데도 불길이 없는 채로 남는다.
            furnaces[i].RefreshRunningVisual();
        }
    }

    /// <summary>
    /// 던전으로 떠날 때. 끄지 않고 치우기만 하므로 제련은 그대로 진행된다.
    /// </summary>
    public void MoveOffscreen()
    {
        bInTown = false;

        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i] == null) continue;

            furnaces[i].transform.position = GetOffscreenPosition(i);
            furnaces[i].SetOutlineVisible(false);

            // 순간이동으로 치우면 OnTriggerExit2D가 확실히 보장되지 않는다. 범위 안이라는 상태가
            // 남으면 마을로 돌아왔을 때 멀리 있어도 아웃라인과 E 안내가 켜진 채로 있게 된다.
            furnaces[i].ResetInteractState();
        }

        // 치워진 뒤에는 상호작용 대상이 없으므로 E 안내도 내린다.
        if (true == bLastInteractState)
        {
            bLastInteractState = false;
            InteractStateChangedEvent?.Invoke(false);
        }

        // 상호작용 대상이 사라졌으므로 진행 중이던 자동 투입도 끝낸다.
        bAutoInsertActive = false;

        // 날아가던 원석은 도착할 곳이 사라지므로 재화를 돌려주고 치운다.
        RefundAndClearFlyingOre();
    }

    /// <summary>
    /// 아직 날아가는 중인 원석을 전부 취소한다. 재화는 발사 시점에 이미 빠졌으므로 돌려준다.
    /// (주괴는 상점으로 가는 길이라 여기서 건드리지 않고 그대로 도착시킨다)
    /// </summary>
    private void RefundAndClearFlyingOre()
    {
        for (int i = flyingItems.Count - 1; i >= 0; i--)
        {
            FlyingSpriteItem item = flyingItems[i];
            if (item == null) { flyingItems.RemoveAt(i); continue; }

            if (false == (item.Payload is BlastFurnace furnace)) continue;

            furnace.CancelOreReservation();
            RefundOre(furnace.GemOreType, 1);

            flyingItems.RemoveAt(i);
            ReturnFlyingItem(item);
        }
    }

    private static Vector3 GetOffscreenPosition(int _index)
    {
        return OffscreenOrigin + new Vector3(_index * OffscreenSpacing, 0f, 0f);
    }

    /// <summary>
    /// 콜라이더 마름모의 아래 꼭짓점을 "가장 왼쪽 아래"부터 순서대로 모은다.
    /// 아이소메트릭이라 셀 좌표 순서와 화면상 좌우가 일치하지 않으므로, 셀이 아니라 월드 좌표로 정렬한다.
    /// </summary>
    private static void CollectPlacementPositions(Tilemap _tilemap, List<Vector3> _outPositions)
    {
        _outPositions.Clear();

        if (_tilemap == null) return;

        _tilemap.CompressBounds();

        foreach (Vector3Int cell in _tilemap.cellBounds.allPositionsWithin)
        {
            if (false == _tilemap.HasTile(cell)) continue;

            // 타일 그림은 tileAnchor(0.5, 0.5) 때문에 피벗이 셀 중심에 오도록 그려진다.
            // 물리 도형도 같은 피벗 기준이므로, 셀 중심에 그 도형의 최저점을 더하면 곧 콜라이더의
            // 아래 꼭짓점이다. 용광로 피벗이 그림 아랫줄이라 이 점에 그대로 놓으면 맞물린다.
            Vector3 center = _tilemap.GetCellCenterWorld(cell);
            float bottomOffset = GetColliderBottomOffset(_tilemap, cell);

            // z는 셀 크기에서 유도된 값이 들어올 수 있다. 2D라 평면에 붙여둔다.
            Vector3 pos = new Vector3(center.x, center.y + bottomOffset + BaseNestingLift, 0f);

            _outPositions.Add(pos);
        }

        _outPositions.Sort(CompareLeftBottomFirst);
    }

    /// <summary>
    /// 타일 콜라이더 도형의 최저점을 셀 중심 기준 오프셋으로 돌려준다.
    /// 물리 도형이 없는 타일이면 셀 마름모의 아래 꼭짓점으로 대신한다.
    /// </summary>
    private static float GetColliderBottomOffset(Tilemap _tilemap, Vector3Int _cell)
    {
        Sprite tileSprite = _tilemap.GetSprite(_cell);

        if (tileSprite != null)
        {
            float lowest = float.MaxValue;

            for (int i = 0; i < tileSprite.GetPhysicsShapeCount(); i++)
            {
                physicsShapeBuffer.Clear();
                tileSprite.GetPhysicsShape(i, physicsShapeBuffer);

                for (int k = 0; k < physicsShapeBuffer.Count; k++)
                {
                    if (physicsShapeBuffer[k].y < lowest) lowest = physicsShapeBuffer[k].y;
                }
            }

            if (lowest < float.MaxValue) return lowest;
        }

        return -_tilemap.layoutGrid.cellSize.y * 0.5f;
    }

    private static int CompareLeftBottomFirst(Vector3 _a, Vector3 _b)
    {
        int byX = _a.x.CompareTo(_b.x);
        if (byX != 0) return byX;

        return _a.y.CompareTo(_b.y);
    }

    private void Update()
    {
        UpdateNearestInteractable();
        UpdateOreInsertion(Time.deltaTime);
        UpdateFlyingItems(Time.deltaTime);
        UpdateUIDatas();
    }

    /// <summary>
    /// 지금 상태를 구독자에게 한 번 다시 보낸다. 변화가 없어도 강제로 보낸다.
    ///
    /// UpdateUIDatas는 구성이 바뀐 프레임에만 이벤트를 쏘고 "직전 상태"를 기억하므로, 늦게 구독한
    /// 쪽은 다음 변화까지 목록을 한 번도 못 받는다. TownSystem.StartTownSystem이 배치를 끝낸 뒤
    /// 이걸 불러 최초 1회(그리고 던전 복귀마다 1회)를 보장한다. 그 시점에는 UI가 시그널 구독을
    /// 끝냈고 위치도 확정돼 있다.
    /// </summary>
    public void NotifyUIState()
    {
        lastNotifiedUnlockCount = -1;
        lastNotifiedStoredSum = -1;
        lastNotifiedRunningMask = -1;

        UpdateUIDatas();
    }

    /// <summary>
    /// UI에 넘길 상태를 갱신한다. 목록은 재사용하므로 매 프레임 돌아도 할당이 없다.
    /// 구성이 실제로 바뀐 프레임에만 FurnaceStateChangedEvent를 쏜다(진행도는 매 프레임 변하므로 제외).
    /// </summary>
    private void UpdateUIDatas()
    {
        if (uiDatas.Count != furnaces.Count)
        {
            uiDatas.Clear();
            for (int i = 0; i < furnaces.Count; i++) uiDatas.Add(default);
        }

        int storedSum = 0;
        int runningMask = 0;

        for (int i = 0; i < furnaces.Count; i++)
        {
            BlastFurnace furnace = furnaces[i];
            if (furnace == null) continue;

            bool unlocked = i < unlockedCount;

            uiDatas[i] = new BlastFurnaceUIData
            {
                position = furnace.transform.position,
                gemOreType = furnace.GemOreType,
                unlocked = unlocked,
                running = furnace.Running,
                progress01 = furnace.WorkProgress01,
                storedOre = furnace.StoredOre,
                orePerIngot = furnace.Recipe.orePerIngot,
            };

            storedSum += furnace.StoredOre;
            if (furnace.Running) runningMask |= 1 << i;
        }

        if (unlockedCount == lastNotifiedUnlockCount
            && storedSum == lastNotifiedStoredSum
            && runningMask == lastNotifiedRunningMask) return;

        lastNotifiedUnlockCount = unlockedCount;
        lastNotifiedStoredSum = storedSum;
        lastNotifiedRunningMask = runningMask;

        FurnaceStateChangedEvent?.Invoke(uiDatas);
    }

    /// <summary>
    /// 자동 투입 세션이 살아 있는 동안 원석이 들어간다. 열려 있는 용광로들을 돌면서 자기 등급
    /// 원석을 한 개씩 날려보낸다(가진 재화가 있고, 아직 받을 자리가 있는 용광로만).
    ///
    /// 보관함(LogContainer/OffroadContainer)과 동작을 맞춘다. 키를 한 번 누르면 계속 들어가고,
    /// 넣을 원석을 다 쓰면 세션이 끝난다. 상호작용 범위를 벗어나면 발사 간격을 기다리는 중이더라도
    /// 그 프레임에 바로 취소된다.
    ///
    /// 재화 차감은 발사 시점에 한다. 원목 납품은 착지 시점에 컨테이너에 커밋하지만, 원석은 슬롯이
    /// 아니라 숫자라서 "날아가는 중에도 또 쓸 수 있는" 문제를 막으려면 먼저 빼는 편이 안전하다.
    /// </summary>
    private void UpdateOreInsertion(float _deltaTime)
    {
        if (false == bAutoInsertActive) return;

        // 상호작용이 열려 있을 때만(= 플레이어가 용광로 앞에 서 있고 경합에서도 이겼을 때).
        // 발사 간격 대기보다 먼저 확인해서, 범위를 벗어나는 즉시 세션을 끊는다.
        if (inventory == null || flyingItemPrefab == null || false == IsInteractable())
        {
            bAutoInsertActive = false;
            return;
        }

        oreFlyTimer += _deltaTime;
        if (oreFlyTimer < OreFlyInterval / GetTransferSpeedMultiplier()) return;

        oreFlyTimer = 0f;

        // 한 틱에 한 개만 보낸다(원목도 한 개씩 직렬로 나간다). 어느 용광로 차례인지는 커서로 돌려서
        // 세 등급을 다 갖고 있으면 번갈아 들어가게 한다.
        int count = Mathf.Min(unlockedCount, furnaces.Count);
        for (int i = 0; i < count; i++)
        {
            oreSendCursor = (oreSendCursor + 1) % count;

            if (TrySendOre(furnaces[oreSendCursor])) return;
        }

        // 한 바퀴를 다 돌았는데 아무 데도 못 보냈다. 여기서 곧바로 끝내면, 날아가는 중인 원석이
        // 잡아둔 자리(pendingOre)나 저장 한도(oreCapacity) 때문에 "잠깐" 막힌 것까지 "더 넣을 것이
        // 없다"로 오인해서, 원석을 잔뜩 들고 있는데도 투입이 멈춘다(발사 간격 0.075초 / 비행 0.5초
        // 라 한 번에 예닐곱 개가 예약 상태로 잡힌다). 그래서 "가진 원석이 없다"일 때만 세션을 끝내고,
        // 자리가 없어서 막힌 것이면 가공이 진행되어 자리가 날 때까지 기다린다.
        if (false == HasAnyOreToInsert())
        {
            bAutoInsertActive = false;
        }
    }

    /// <summary>
    /// 열려 있는 용광로 중에 지금 넣을 원석을 실제로 갖고 있는 곳이 하나라도 있는지.
    /// 용광로가 가득 찼는지는 일부러 보지 않는다 - 가공이 진행되면 자리는 다시 나기 때문이다.
    /// </summary>
    private bool HasAnyOreToInsert()
    {
        if (inventory == null) return false;

        int count = Mathf.Min(unlockedCount, furnaces.Count);
        for (int i = 0; i < count; i++)
        {
            if (furnaces[i] == null) continue;

            BlastFurnaceRecipe recipe = furnaces[i].Recipe;
            if (false == recipe.IsValid) continue;

            if (inventory.GetCurrentGemOre(recipe.gemOreType) > 0) return true;
        }

        return false;
    }

    /// <summary>
    /// 지금 원석을 넣을 수 있는 상태인지.
    ///
    /// 창구를 하나로 모으는 설정에서는 그 한 대만 보면 되지만, 그 설정을 끄면(bSingleInteractionCollider
    /// = false) 창구가 없어 interactionOwner가 null이 된다. 그때 창구만 보면 원석이 영영 안 들어가므로,
    /// 열려 있는 용광로 중 하나라도 상호작용 가능하면 받는다.
    /// </summary>
    private bool IsInteractable()
    {
        if (interactionOwner != null) return interactionOwner.Interactable;

        for (int i = 0; i < unlockedCount && i < furnaces.Count; i++)
        {
            if (furnaces[i] != null && furnaces[i].Interactable) return true;
        }

        return false;
    }

    private bool TrySendOre(BlastFurnace _furnace)
    {
        if (_furnace == null) return false;

        BlastFurnaceRecipe recipe = _furnace.Recipe;
        if (false == recipe.IsValid) return false;
        if (_furnace.AcceptableOre <= 0) return false;

        if (inventory.GetCurrentGemOre(recipe.gemOreType) <= 0) return false;

        // 날아오는 중인 것까지 포함해 자리를 먼저 잡는다. 이걸 빼먹으면 비행 시간(0.5초) 동안
        // 같은 자리로 여러 개가 날아가고, 도착해서 거절된 만큼 재화가 그냥 사라진다.
        if (false == _furnace.ReserveOre()) return false;

        inventory.DecreaseGemOre(recipe.gemOreType, 1);

        // 원목 한 개를 컨테이너로 보낼 때와 같은 소리(LogContainer.TransferOneSlotVisualRoutine).
        Sound.PlayUI(SoundID.OutItem, GetSoundVolume());

        Vector3 start = characterTransform != null ? characterTransform.position : _furnace.transform.position;

        FlyingSpriteItem item = GetFlyingItem();
        item.Setup(recipe.oreSprite, flyingSortingLayer, 0);
        item.Payload = _furnace;
        item.Launch(start, _furnace.InPointPosition,
                    UnityEngine.Random.Range(OreFlyHeightMin, OreFlyHeightMax), FlyDuration,
                    new Vector3(UnityEngine.Random.Range(-0.3f, 0f), UnityEngine.Random.Range(-0.2f, 0f), 0f),
                    UnityEngine.Random.Range(90f, 270f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f));

        flyingItems.Add(item);
        return true;
    }

    /// <summary>
    /// 주괴 완성. 상점NPC로 날려보낸다. 도착해야 돈이 오르므로, 여기서는 발사만 한다.
    /// </summary>
    private void IngotCompleted(BlastFurnace _furnace)
    {
        long price = GetIngotPrice(_furnace.Recipe);

        // 주괴도 원석이 들어온 자리(InPoint)에서 나간다.
        Vector3 launchPos = _furnace.InPointPosition;

        // 빠져나가는 순간 용광로가 뽀잉하고, 소리는 컨테이너에서 벨트로 원목이 빠질 때와 같은 것을 쓴다.
        _furnace.TriggerBounce();
        Sound.Play(SoundID.ConvayerPut, launchPos, GetSoundVolume());

        if (flyingItemPrefab == null || shopNPC == null)
        {
            // 연출을 못 쓰는 상황이라도 보상은 잃지 않게 바로 넣는다.
            shopNPC?.InsertMoney(price);
            return;
        }

        FlyingSpriteItem item = GetFlyingItem();
        item.Setup(_furnace.Recipe.ingotSprite, flyingSortingLayer, 0);
        item.Payload = price;
        item.LaunchToTarget(launchPos, shopNPC.transform,
                            UnityEngine.Random.Range(1.2f, 1.6f), FlyDuration,
                            new Vector3(UnityEngine.Random.Range(-0.3f, 0f), UnityEngine.Random.Range(-0.2f, 0f), 0f),
                            UnityEngine.Random.Range(90f, 270f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f));

        flyingItems.Add(item);
    }

    private void UpdateFlyingItems(float _deltaTime)
    {
        if (flyingItems.Count == 0) return;

        flyingCleanup.Clear();

        for (int i = 0; i < flyingItems.Count; i++)
        {
            FlyingSpriteItem item = flyingItems[i];
            if (item == null) { flyingCleanup.Add(item); continue; }

            item.ManualUpdate(_deltaTime);

            if (false == item.Flying) flyingCleanup.Add(item);
        }

        for (int i = 0; i < flyingCleanup.Count; i++)
        {
            FlyingSpriteItem item = flyingCleanup[i];
            flyingItems.Remove(item);

            if (item == null) continue;

            Arrived(item);
            ReturnFlyingItem(item);
        }

        flyingCleanup.Clear();
    }

    private void Arrived(FlyingSpriteItem _item)
    {
        if (_item.Payload is BlastFurnace furnace)
        {
            // 자리를 미리 잡아두므로 거절될 일이 없어야 하지만, 혹시 거절되면 재화를 돌려준다.
            // 빠진 재화가 조용히 사라지는 것만은 막는다.
            if (furnace.InsertOre(1) <= 0)
            {
                RefundOre(furnace.GemOreType, 1);
                return;
            }

            PlayDepositFeedback(furnace);
            return;
        }

        if (_item.Payload is long ingotValue && shopNPC != null)
        {
            shopNPC.InsertMoney(ingotValue);

            // 상점NPC에 꽂히는 순간. 원목이 컨테이너에 박힐 때와 같은 뽀잉/소리를 쓴다.
            shopNPC.TriggerBounce();
            Sound.Play(SoundID.GetItem, shopNPC.transform.position, GetSoundVolume());
        }
    }

    /// <summary>
    /// 원석이 꽂히는 순간의 손맛. LogContainer가 원목을 받을 때 하는 것을 그대로 맞췄다.
    ///   - 용광로가 찌그러졌다 펴지는 뽀잉
    ///   - GetItem 사운드. 연달아 넣을수록 피치/볼륨이 1.0~1.5 구간에서 올라가고,
    ///     흐름이 끊기면(1초) 다시 낮은 피치부터 시작한다.
    ///   - 카메라 흔들림과 패드 진동
    /// </summary>
    private void PlayDepositFeedback(BlastFurnace _furnace)
    {
        _furnace.TriggerBounce();

        if (Time.time - lastDepositPitchTime > DepositPitchResetTimeout)
        {
            currentDepositPitch = DepositPitchMin;
        }
        lastDepositPitchTime = Time.time;

        float depositT = (currentDepositPitch - DepositPitchMin) / (DepositPitchMax - DepositPitchMin);
        float volumeMul = Mathf.Lerp(1f, DepositVolumeBoostMax, depositT);

        Sound.Play(SoundID.GetItem, _furnace.transform.position, volumeMul * GetSoundVolume(), true, currentDepositPitch);
        currentDepositPitch = Mathf.Clamp(currentDepositPitch + DepositPitchStep, DepositPitchMin, DepositPitchMax);

        if (false == bInTown) return;

        CameraMoveController.Instance?.ShakeCamera(1f, 0.08f);

        // 0.075초 간격으로 연달아 들어오므로 연속 전용(약한) 파형을 쓴다(LogContainer와 동일).
        Rumble.Play(EHapticEvent.ItemStream);
    }

    /// <summary>
    /// 발사 시점에 주머니에서 뺀 원석을 되돌린다.
    ///
    /// 뺀 만큼만 돌려주는 것이고 마을에서는 원석이 새로 들어올 일이 없으므로 자리는 항상 있다.
    /// 그래도 주머니 한도에 막히면 재화가 조용히 사라지는 것이라, 그 경우만 눈에 띄게 남긴다.
    /// </summary>
    private void RefundOre(GemOreType _gemOreType, long _amount)
    {
        if (inventory == null) return;

        long accepted = inventory.GemOreEarned(_gemOreType, _amount);
        if (accepted >= _amount) return;

        Debug.LogWarning($"[BlastFurnace] 원석 환불이 주머니 한도에 막혔습니다({accepted}/{_amount}). " +
                         "발사할 때 뺀 만큼 돌려주는 것이라 이 경우는 없어야 합니다.");
    }

    private FlyingSpriteItem GetFlyingItem()
    {
        FlyingSpriteItem item = flyingPool.Count > 0 ? flyingPool.Pop() : Instantiate(flyingItemPrefab, transform);

        item.ResetItem();
        item.gameObject.SetActive(true);

        return item;
    }

    private void ReturnFlyingItem(FlyingSpriteItem _item)
    {
        _item.ResetItem();
        _item.gameObject.SetActive(false);

        flyingPool.Push(_item);
    }

    /// <summary>
    /// 상호작용 범위가 겹쳤을 때 가장 가까운 하나만 반응하게 만든다. 후보는 용광로 셋과 집이다.
    /// LogProcessingManager.CalcDistForInteraction()이 제재소에서 하는 판정과 같은 방식이고,
    /// 거리도 같은 기준(트리거 콜라이더까지의 거리, 둘 다 0이면 중심 거리)으로 잰다.
    /// </summary>
    private void UpdateNearestInteractable()
    {
        if (character == null || character.centerTransform == null) return;

        Vector3 playerPos = character.centerTransform.position;

        BlastFurnace nearestFurnace = null;
        float nearestFurnaceDistSq = float.MaxValue;

        for (int i = 0; i < furnaces.Count; i++)
        {
            BlastFurnace furnace = furnaces[i];
            if (furnace == null || false == furnace.PlayerInRange) continue;

            float distSq = GetDistanceSq(playerPos, furnace.InteractionCollider, furnace.transform);
            if (distSq >= nearestFurnaceDistSq) continue;

            nearestFurnaceDistSq = distSq;
            nearestFurnace = furnace;
        }

        // 집이 후보인지, 그리고 용광로보다 가까운지
        bool tentInRange = tent != null && tent.PlayerInRange;
        float tentDistSq = tentInRange ? GetDistanceSq(playerPos, tent.InteractionCollider, tent.transform) : float.MaxValue;

        bool tentWins = tentInRange && tentDistSq < nearestFurnaceDistSq;

        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i] == null) continue;

            furnaces[i].SetCanReach(false == tentWins && furnaces[i] == nearestFurnace);
        }

        // 경합 상대가 없을 때는 집을 건드리지 않아야 한다(항상 true로 되돌려 둔다).
        bool tentCanReach = nearestFurnace == null || tentWins;

        // 꺼지는 쪽을 먼저, 켜지는 쪽을 나중에 알린다.
        //
        // 캐릭터 위 E 안내는 대상마다 따로 세지 않고 마지막 알림을 그대로 따른다(UIView_Unit.InteractionStateChange).
        // 그래서 집이 "켜짐"을 보낸 뒤에 용광로가 "꺼짐"을 보내면, 집 앞에 서 있는데 안내가 사라진다.
        // 용광로에서 집 쪽으로 걸어 넘어가는 프레임이 정확히 그 순서였다.
        if (tent == null)
        {
            UpdateGroupOutline();
        }
        else if (true == tentCanReach)
        {
            // 집이 켜지는 차례. 용광로 쪽은 꺼지거나 그대로이므로 먼저 보낸다.
            UpdateGroupOutline();
            tent.SetCanReach(true);
        }
        else
        {
            // 용광로가 켜지는 차례. 집을 먼저 꺼야 용광로의 "켜짐"이 마지막에 남는다.
            tent.SetCanReach(false);
            UpdateGroupOutline();
        }
    }

    /// <summary>
    /// 아웃라인을 세 대에 한꺼번에 적용한다. 상호작용 창구는 가운데 한 대뿐이지만, 플레이어 눈에는
    /// 세 대가 한 시설이므로 같이 빛나야 한다.
    ///
    /// 창구를 쓰지 않는 설정(bSingleInteractionCollider = false)에서는 각자 자기 상태대로 켠다.
    /// </summary>
    private void UpdateGroupOutline()
    {
        if (false == bSingleInteractionCollider)
        {
            for (int i = 0; i < furnaces.Count; i++)
            {
                if (furnaces[i] != null) furnaces[i].SetOutlineVisible(furnaces[i].Interactable);
            }

            return;
        }

        bool highlight = interactionOwner != null && interactionOwner.Interactable;

        // 아직 해금되지 않은 용광로는 그림이 감춰져 있으므로 아웃라인도 켜지 않는다.
        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i] == null) continue;

            furnaces[i].SetOutlineVisible(highlight && i < unlockedCount);
        }

        // 아웃라인과 같은 판정으로 E키 안내도 켜고 끈다.
        if (highlight == bLastInteractState) return;

        bLastInteractState = highlight;
        InteractStateChangedEvent?.Invoke(highlight);
    }

    private bool bLastInteractState = false;

    /// <summary>
    /// 플레이어에서 대상까지의 거리(제곱). 트리거 콜라이더 표면까지를 재고, 안쪽에 들어가 있어
    /// 0이 나오면 중심 거리로 다시 잰다(LogProcessingManager와 같은 규칙).
    /// </summary>
    private static float GetDistanceSq(Vector3 _playerPos, Collider2D _collider, Transform _fallback)
    {
        if (_collider != null)
        {
            float surfaceSq = ((Vector2)_collider.ClosestPoint(_playerPos) - (Vector2)_playerPos).sqrMagnitude;
            if (surfaceSq > 0f) return surfaceSq;
        }

        return ((Vector2)_fallback.position - (Vector2)_playerPos).sqrMagnitude;
    }

    // ── 세이브 ────────────────────────────────────────────────────────────

    /// <summary>
    /// 각 용광로의 원석/진행도를 세이브 구조체에 채운다. 열린 대수는 특성이 정하므로 담지 않는다.
    /// 리스트는 재사용한다(SaveManager의 다른 Populate들과 같은 방식).
    /// </summary>
    public void PopulateSaveData(ref List<BlastFurnaceSaveData> _datas)
    {
        if (_datas == null) _datas = new List<BlastFurnaceSaveData>(furnaces.Count);
        else _datas.Clear();

        for (int i = 0; i < furnaces.Count; i++)
        {
            BlastFurnace furnace = furnaces[i];
            if (furnace == null) continue;

            // 날아가는 중인 원석도 함께 담는다. 재화는 발사 시점에 이미 빠졌으므로 여기서
            // 빼먹으면 저장 직후 껐다 켰을 때 그만큼 사라진다.
            // (LogContainer.AppendTransitToSaveData가 운반 중인 원목을 세이브에 합산하는 것과 같은 처리)
            _datas.Add(new BlastFurnaceSaveData
            {
                gemOreType = furnace.GemOreType,
                storedOre = furnace.StoredOre + furnace.PendingOre,
                remainingWork = furnace.RemainingWork,
            });
        }
    }

    /// <summary>
    /// 세이브에서 복원한다. 리스트가 null이면(용광로가 없던 시절의 예전 세이브) 아무것도 하지 않아
    /// 모든 용광로가 빈 상태로 시작한다 - 그게 그 세이브의 실제 상태이므로 맞다.
    ///
    /// 배열 순서가 아니라 gemOreType으로 맞춰 넣으므로, 저장 당시와 용광로 순서가 달라져도
    /// 남의 원석이 들어가지 않는다. 짝이 없는 항목은 조용히 버린다.
    /// </summary>
    public void LoadSaveData(List<BlastFurnaceSaveData> _datas)
    {
        if (_datas == null) return;

        for (int i = 0; i < _datas.Count; i++)
        {
            BlastFurnaceSaveData data = _datas[i];

            BlastFurnace furnace = GetFurnace(data.gemOreType);
            if (furnace == null) continue;

            furnace.LoadState(data.storedOre, data.remainingWork);
        }
    }

    /// <summary>등급으로 용광로를 찾는다. 제련 기능이 붙을 때 쓰라고 열어둔다.</summary>
    public BlastFurnace GetFurnace(GemOreType _gemOreType)
    {
        for (int i = 0; i < furnaces.Count; i++)
        {
            if (furnaces[i] != null && furnaces[i].GemOreType == _gemOreType) return furnaces[i];
        }

        return null;
    }
}
