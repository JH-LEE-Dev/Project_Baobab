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

using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 마을 용광로. 지금은 보이는 것과 상호작용 범위까지만 담당한다.
///   - 불꽃 애니메이션(AnimatedObj에 위임). 멈춰 있을 때와 도는 동안이 시트의 서로 다른 구간을 쓴다.
///   - 이펙트: 도는 동안 계속 나오는 LoopVFX + 아지랑이, 주괴를 쏠 때 한 번 터지는 FireImpact
///   - 아이소메트릭 정렬(CustomSortable + SortingGroup)
///   - 아웃라인은 자기가 켜지 않는다. 세 대가 한 덩어리로 같이 빛나야 해서 BlastFurnaceManager가 켠다.
///
/// 여러 상호작용 대상이 겹칠 때 누가 선택되는지는 BlastFurnaceManager가 정하고 SetCanReach()로 알려준다.
/// ShopNPC/LogContainer가 LogProcessingManager에게 같은 방식으로 판정받는 것과 동일한 구조다.
///
/// 제련 자체는 이 컴포넌트가 돈다. 원석이 레시피 개수만큼 모이면 한 번에 차감하고, 가공량을
/// 초당 1씩(가속 배율 적용) 깎아 0이 되면 주괴 하나를 내보낸다. 던전에 가 있는 동안에도 계속
/// 돌아야 하므로 이 오브젝트는 꺼지지 않고 화면 밖으로 치워지기만 한다(제재소와 같은 방식).
/// </summary>
public class BlastFurnace : MonoBehaviour
{
    // 상호작용 가능 여부가 바뀔 때 알린다. 상호작용 안내 UI가 붙으면 여기를 구독하면 된다.
    public event Action<bool> InteractableChangedEvent;

    /// <summary>주괴 하나가 완성됐을 때. BlastFurnaceManager가 받아 상점NPC로 날려보낸다.</summary>
    public event Action<BlastFurnace> IngotCompletedEvent;

    /// <summary>쌓인 원석 수나 가공 진행도가 바뀔 때. 진행도 UI가 붙으면 여기를 구독하면 된다.</summary>
    public event Action<BlastFurnace> ProgressChangedEvent;

    private const string PLAYER_TAG = "Player";

    [Header("Ore")]
    [Tooltip("이 용광로가 다루는 원석 등급. 마을에는 황금/다이아/프리즘 한 대씩 서 있다.")]
    [SerializeField] private GemOreType gemOreType = GemOreType.Gold;

    [Header("Visual")]
    [Tooltip("불꽃 프레임 애니메이션. 타일맵 데코가 쓰는 것과 같은 컴포넌트를 그대로 쓴다.")]
    [SerializeField] private AnimatedObj animatedObj;

    // Blast Furnace 시트 한 장에 두 상태가 들어 있다. 앞쪽이 대기, 뒤쪽이 가동이다.
    // 두 구간 모두 핑퐁으로 돈다(0 1 2 3 2 1 0 1 2 3...). 그림이 바뀌면 여기 숫자만 고치면 된다.
    [Tooltip("멈춰 있을 때 쓸 구간의 첫 프레임 번호.")]
    [SerializeField] private int idleFrameStart = 0;

    [Tooltip("멈춰 있을 때 쓸 프레임 수.")]
    [SerializeField] private int idleFrameCount = 4;

    [Tooltip("제련이 도는 동안 쓸 구간의 첫 프레임 번호.")]
    [SerializeField] private int runFrameStart = 4;

    [Tooltip("제련이 도는 동안 쓸 프레임 수.")]
    [SerializeField] private int runFrameCount = 5;

    [Tooltip("제련이 도는 동안의 초당 프레임 수.")]
    [SerializeField] private float runFrameRate = 10f;

    [Tooltip("멈춰 있을 때의 초당 프레임 수. 가동보다 느리게 둬서 쉬고 있는 느낌을 준다.")]
    [SerializeField] private float idleFrameRate = 6f;

    [Header("VFX")]
    [Tooltip("제련이 도는 동안 계속 나오는 이펙트. Visual 아래에 프리팹에 저장된 좌표 그대로 붙는다.")]
    [SerializeField] private ParticleSystem loopVfxPrefab;

    [Tooltip("주괴를 쏘아 보낼 때 한 번 터지는 이펙트. 붙는 방식은 LoopVFX와 같다.")]
    [SerializeField] private ParticleSystem fireImpactPrefab;

    [Tooltip("아지랑이 디스토션. 가동 이펙트와 함께 켜고 끈다. 비워두면 자식에서 찾는다.")]
    [SerializeField] private BlastFurnaceHeatHaze heatHaze;

    [Tooltip("원석이 날아와 꽂히는 지점. 원목이 LogContainer의 inputTransform으로 들어가는 것과 같다.")]
    [SerializeField] private Transform inPoint;

    [Tooltip("원석이 꽂힐 때 찌그러졌다 펴질 시각 루트(LogContainer.visualTransform과 같은 역할).")]
    [SerializeField] private Transform visualTransform;

    [SerializeField] private CustomSortable customSortable;
    [SerializeField] private SortingGroup sortingGroup;

    [Header("Interaction")]
    [Tooltip("범위 안에 들어왔을 때 켜지는 아웃라인. 스텐실 라이터와 아웃라인이 함께 묶여 있다.")]
    [SerializeField] private GameObject outLineObject;

    [Tooltip("상호작용 범위 트리거. 가장 가까운 대상을 고를 때 거리 계산에도 쓴다.")]
    [SerializeField] private Collider2D interactionCollider;

    // 제련이 도는 중인지. 꺼져 있으면 불꽃이 첫 프레임에 멈춘다.
    private bool bRunning = false;

    // 가공 규칙(필요 원석/가공량/주괴 가치). BlastFurnaceManager가 생성 직후 넣어준다.
    private BlastFurnaceRecipe recipe;

    // 넣어둔 원석. 레시피 개수만큼 차면 가공이 시작되고, 그 몫은 가공이 끝날 때 한 번에 빠진다.
    // 도는 동안에도 남아 있으므로 숫자가 중간에 줄지 않는다.
    private int storedOre = 0;

    // 날아오는 중이라 아직 도착하지 않은 원석. 재화는 발사 시점에 이미 빠졌으므로,
    // 받을 자리를 미리 잡아두지 않으면 도착했을 때 거절되어 그만큼 재화가 증발한다.
    // (원목이 LogContainer.CanAddItemByData의 pendingCount로 막는 것과 같은 장치)
    private int pendingOre = 0;

    // 남은 가공량. 0보다 크면 가공 중이다.
    private float remainingWork = 0f;

    // 가공 속도 배율(가속 특성). 1이면 초당 1.
    private float speedMultiplier = 1f;

    // 프리팹에서 한 번만 찍어두고 계속 재사용한다. 단발 이펙트도 매번 새로 만들지 않고 되감아 다시 튼다.
    private ParticleSystem loopVfx;
    private ParticleSystem fireImpactVfx;

    private bool bPlayerInRange = false;

    // 다른 상호작용 대상(다른 용광로, 집)보다 가까운지. BlastFurnaceManager가 매 프레임 정해준다.
    private bool bCanReach = true;

    private bool bInteractable = false;

    // 원석이 꽂힐 때의 스쿼시&스트레치. 수식과 지속시간 모두 LogContainer.UpdateBounce와 같다.
    private const float BounceDuration = 0.4f;
    private float bounceTime = BounceDuration;

    public GemOreType GemOreType => gemOreType;

    public bool Running => bRunning;

    public BlastFurnaceRecipe Recipe => recipe;

    /// <summary>
    /// 용광로에 들어 있는 원석 수. <b>가공 중인 배치도 여기 그대로 남아 있다.</b>
    /// 한 배치에 들어간 원석은 가공이 끝나는 순간에 한 번에 빠진다.
    /// </summary>
    public int StoredOre => storedOre;

    /// <summary>
    /// 더 받을 수 있는 원석 수.
    ///
    /// 한도는 "주괴 하나에 드는 원석 수"가 아니라 <b>용광로의 저장 한도</b>다. 둘을 같게 묶으면
    /// 10개를 받은 뒤로는 영영 더 받지 못한다.
    /// 한도가 0이면 무제한이라 가진 원석을 전부 쌓아둘 수 있다.
    ///
    /// 가공 중인 배치도 storedOre에 남아 있으므로 그동안 자리를 계속 차지한다. 지금은 모든
    /// 레시피가 무제한(0)이라 차이가 없지만, 한도를 두게 되면 이 점을 감안해 잡아야 한다.
    ///
    /// 날아오는 중인 것(pendingOre)까지 빼야 과발사로 재화가 새지 않는다.
    /// </summary>
    public int AcceptableOre
    {
        get
        {
            if (false == recipe.IsValid) return 0;

            // 0 = 무제한. 한 번에 다 보내지지는 않고 어차피 발사 간격만큼씩 나가므로 큰 값이면 충분하다.
            if (recipe.oreCapacity <= 0) return int.MaxValue - storedOre - pendingOre;

            return Mathf.Max(0, recipe.oreCapacity - storedOre - pendingOre);
        }
    }

    /// <summary>날아오는 중인 원석 수.</summary>
    public int PendingOre => pendingOre;

    /// <summary>
    /// 원석 한 개가 날아올 자리를 잡아둔다. 자리가 없으면 false를 돌려주므로 발사하지 말아야 한다.
    /// 재화를 빼기 전에 반드시 먼저 부를 것.
    /// </summary>
    public bool ReserveOre()
    {
        if (AcceptableOre <= 0) return false;

        pendingOre++;
        return true;
    }

    /// <summary>잡아둔 자리를 되돌린다(비행이 취소된 경우).</summary>
    public void CancelOreReservation(int _count = 1)
    {
        pendingOre = Mathf.Max(0, pendingOre - _count);
    }

    /// <summary>가공 진행도 0~1. 가공 중이 아니면 0.</summary>
    public float WorkProgress01
    {
        get
        {
            if (false == bRunning || recipe.workPerIngot <= 0f) return 0f;
            return Mathf.Clamp01(1f - (remainingWork / recipe.workPerIngot));
        }
    }

    /// <summary>플레이어가 트리거 안에 들어와 있는지. 가장 가까운 대상을 고르는 후보 판정에 쓴다.</summary>
    public bool PlayerInRange => bPlayerInRange;

    /// <summary>범위 안이면서 경합에서도 이겨 실제로 상호작용할 수 있는 상태인지.</summary>
    public bool Interactable => bInteractable;

    public Collider2D InteractionCollider => interactionCollider;

    /// <summary>원석이 날아와 꽂히는 지점. 지정이 없으면 용광로 자기 위치를 쓴다.</summary>
    public Vector3 InPointPosition => inPoint != null ? inPoint.position : transform.position;

    /// <summary>이 용광로가 상호작용 창구로 열려 있는지(콜라이더가 살아 있는지).</summary>
    public bool InteractionEnabled => interactionCollider != null && interactionCollider.enabled;

    /// <summary>
    /// 상호작용 창구로 쓸지 여부. 마을 용광로는 세 대가 붙어 있어 가운데 한 대의 콜라이더만 열고
    /// 양옆 두 대는 닫아서 한 덩어리처럼 다룬다(BlastFurnaceManager가 정한다).
    /// 닫을 때는 이미 범위 안에 있던 상태도 함께 풀어야 아웃라인이 켜진 채로 남지 않는다.
    /// </summary>
    public void SetInteractionEnabled(bool _bEnabled)
    {
        if (interactionCollider != null)
        {
            interactionCollider.enabled = _bEnabled;
        }

        if (false == _bEnabled)
        {
            bPlayerInRange = false;
            UpdateInteractState();
        }
    }

    /// <summary>
    /// BlastFurnaceManager가 배치 데이터(어느 자리가 황금/다이아/프리즘인지)에 맞춰 생성 직후 지정한다.
    /// 프리팹은 한 종류뿐이고 등급만 다르므로, 등급별 프리팹을 따로 두지 않는다.
    /// </summary>
    public void SetGemOreType(GemOreType _gemOreType)
    {
        gemOreType = _gemOreType;
    }

    /// <summary>가공 규칙을 넣어준다. 등급도 규칙을 따라간다.</summary>
    /// <summary>
    /// 이 용광로가 쓸 가공 규칙을 넣습니다.
    ///
    /// 종류(gemOreType)를 레시피에서 <b>조건부로만</b> 받는 이유:
    /// BlastFurnaceRecipe는 struct라 BlastFurnaceManager.FindRecipe()가 못 찾으면 default를
    /// 돌려주고, 그 gemOreType은 None이다. 그걸 그대로 대입하면 <b>바로 앞줄에서 부른
    /// SetGemOreType()이 정해준 종류가 덮여</b> 용광로가 무종류가 된다. 이 클래스에는
    /// recipe/gemOreType에 대한 null·None 가드가 없어서 그 뒤로는 조용히 어긋난다.
    ///
    /// 지금 이 경우가 없는 것은 DemoContentStripper가 레시피 <b>항목을 지우지 않고
    /// 스프라이트 참조만 끊기</b> 때문이다. 그 전제가 바뀌는 순간(= 안 쓰는 항목을 배열에서
    /// 빼는 순간) 깨지므로, 전제에 기대지 않도록 여기서 막는다.
    ///
    /// None이 아닌 값이 올 때의 동작은 종전과 완전히 동일하다.
    /// </summary>
    public void SetRecipe(BlastFurnaceRecipe _recipe)
    {
        recipe = _recipe;

        if (GemOreType.None != _recipe.gemOreType)
        {
            gemOreType = _recipe.gemOreType;
        }
    }

    /// <summary>가공 속도 배율. 1이면 초당 1씩 가공량이 준다(가속 특성이 이 값을 올린다).</summary>
    public void SetSpeedMultiplier(float _multiplier)
    {
        speedMultiplier = Mathf.Max(0f, _multiplier);
    }

    /// <summary>
    /// 원석을 넣는다. 실제로 받아들인 개수를 돌려준다(꽉 찼으면 0).
    /// 원석이 레시피 개수만큼 차고 가공 중이 아니면 그 자리에서 가공을 시작한다.
    /// </summary>
    public int InsertOre(int _count)
    {
        if (_count <= 0 || false == recipe.IsValid) return 0;

        // 도착했으므로 잡아둔 자리를 푼다. 그만큼 AcceptableOre가 늘어나 아래에서 정상적으로 들어간다.
        CancelOreReservation(_count);

        int accepted = Mathf.Min(_count, AcceptableOre);
        if (accepted <= 0) return 0;

        storedOre += accepted;

        TryStartSmelting();
        ProgressChangedEvent?.Invoke(this);

        return accepted;
    }

    /// <summary>
    /// 원석이 다 모였고 놀고 있으면 한 배치를 시작한다.
    ///
    /// <b>여기서 원석을 빼지 않는다.</b> 넣자마자 숫자가 줄어버리면 원석이 사라진 것처럼 보이므로,
    /// 이번 배치에 들어간 몫은 가공이 끝나는 순간(UpdateSmelting)에 한 번에 뺀다.
    /// 그래서 가공이 도는 동안에는 필요 개수만큼이 계속 차 있는 상태로 보인다.
    /// </summary>
    private void TryStartSmelting()
    {
        if (true == bRunning) return;
        if (false == recipe.IsValid) return;
        if (storedOre < recipe.orePerIngot) return;

        remainingWork = recipe.workPerIngot;

        SetRunning(true);
    }

    /// <summary>
    /// 가공량을 깎는다. 다 깎이면 주괴 하나를 내보내고, 원석이 남아 있으면 곧바로 다음 배치를 시작한다.
    /// 마을에 있든 던전에 있든 매 프레임 돈다(오브젝트를 끄지 않고 화면 밖으로만 치우기 때문).
    /// </summary>
    private void UpdateSmelting(float _deltaTime)
    {
        if (false == bRunning) return;

        remainingWork -= _deltaTime * speedMultiplier;

        if (remainingWork > 0f)
        {
            ProgressChangedEvent?.Invoke(this);
            return;
        }

        remainingWork = 0f;
        SetRunning(false);

        // 이번 배치에 들어간 원석을 여기서 비로소 뺀다(시작할 때가 아니라).
        // Max로 감싸는 것은 배치 시작 시점에 이미 빼두던 시절의 세이브를 읽었을 때를 위한 보호다.
        storedOre = Mathf.Max(0, storedOre - recipe.orePerIngot);

        // 주괴가 튀어나가는 순간. 아래 이벤트를 받은 BlastFurnaceManager가 곧바로 발사한다.
        PlayFireImpact();

        IngotCompletedEvent?.Invoke(this);

        // 남은 원석으로 이어서 돌린다.
        TryStartSmelting();
        ProgressChangedEvent?.Invoke(this);
    }

    /// <summary>세이브에서 되살릴 때. 진행 중이던 배치까지 그대로 복원한다.</summary>
    public void LoadState(int _storedOre, float _remainingWork)
    {
        pendingOre = 0;
        storedOre = Mathf.Max(0, _storedOre);
        remainingWork = Mathf.Max(0f, _remainingWork);

        SetRunning(remainingWork > 0f);

        if (false == bRunning) TryStartSmelting();

        ProgressChangedEvent?.Invoke(this);
    }

    public float RemainingWork => remainingWork;

    /// <summary>
    /// 제련 가동 여부. 프레임 구간과 가동 이펙트(LoopVFX + 아지랑이)가 여기에 맞춰 함께 바뀐다.
    /// </summary>
    public void SetRunning(bool _bRunning)
    {
        if (bRunning == _bRunning) return;

        bRunning = _bRunning;
        ApplyRunningState();
    }

    /// <summary>
    /// 겹쳐 있는 상호작용 대상들 중 이 용광로가 가장 가까운지. BlastFurnaceManager가 알려준다.
    /// (ShopNPC.SetCanReach와 같은 역할)
    /// </summary>
    public void SetCanReach(bool _bCanReach)
    {
        if (bCanReach == _bCanReach) return;

        bCanReach = _bCanReach;
        UpdateInteractState();
    }

    private void Awake()
    {
        SetupVisual();
        SetupVfx();
        ApplyRunningState();

        bPlayerInRange = false;
        bCanReach = true;
        bInteractable = false;

        if (outLineObject != null) outLineObject.SetActive(false);
    }

    private void SetupVisual()
    {
        if (animatedObj != null)
        {
            animatedObj.Initialize();

            // 두 구간 모두 왕복 재생이다. 프리팹 값에 기대지 않고 여기서 켜둔다.
            animatedObj.SetPingPong(true);
        }

        if (customSortable == null) return;

        // AnimatedObj.Initialize()는 CustomSortable에 자기 SpriteRenderer 하나만 등록하고 정렬 기준도
        // 본체 오브젝트로 잡는다. 용광로는 아웃라인 자식까지 본체와 같은 순서로 묶여야 하고 정렬 기준은
        // 접지점인 루트여야 하므로(Tent와 동일), 여기서 다시 초기화해 자식 SpriteRenderer를 전부
        // 등록하고 SortingGroup을 물려준다.
        customSortable.Initialize(transform);
        customSortable.SetSortingGroup(sortingGroup);
    }

    /// <summary>
    /// 이펙트를 Visual 아래에 붙인다. <b>반드시 SetupVisual() 뒤에 부른다.</b>
    /// CustomSortable.Initialize()가 그 시점의 자식 SpriteRenderer를 전부 걷어가므로, 먼저 붙이면
    /// 스프라이트를 쓰는 이펙트가 딸려 들어가 정렬 순서를 용광로에 빼앗긴다.
    /// (지금 두 프리팹은 파티클뿐이라 걸릴 것이 없지만, 나중에 스프라이트 이펙트가 추가돼도 안전하도록 순서를 고정해 둔다.)
    /// </summary>
    private void SetupVfx()
    {
        loopVfx = SpawnVfx(loopVfxPrefab);
        fireImpactVfx = SpawnVfx(fireImpactPrefab);

        // 둘 다 Play On Awake라 붙자마자 한 번 터진다. 상태를 반영하기 전에 꺼서 지운다.
        StopVfx(loopVfx, true);
        StopVfx(fireImpactVfx, true);

        if (heatHaze == null) heatHaze = GetComponentInChildren<BlastFurnaceHeatHaze>(true);
    }

    /// <summary>
    /// 보이는 것을 지금 상태에 다시 맞춘다. 마을 자리에 놓인 직후에 BlastFurnaceManager가 부른다.
    ///
    /// 세이브 로드는 용광로가 아직 화면 밖(대기 자리)에 있을 때 가동 상태를 복원한다. 파티클이
    /// 화면 밖에서는 멈추도록(Culling Mode: Pause) 되어 있어 그때 건 Play()는 살아나지 않고,
    /// 이후 아무도 다시 물려주지 않으면 돌고 있는 용광로가 불길 없이 서 있게 된다.
    /// </summary>
    public void RefreshRunningVisual()
    {
        ApplyRunningState();
    }

    /// <summary>보이는 것 전부(프레임 애니메이션 + 가동 이펙트)를 지금 상태에 맞춘다.</summary>
    private void ApplyRunningState()
    {
        ApplyFrameAnimation();
        ApplyRunningVfx();
    }

    /// <summary>
    /// 지금 상태에 맞는 프레임 구간과 재생 속도를 애니메이터에 알려준다.
    ///
    /// 멈춰 있을 때도 애니메이션은 계속 돈다(예전처럼 컴포넌트를 끄지 않는다). 대기와 가동이
    /// 시트의 서로 다른 구간을 쓰고, 대기 쪽만 조금 느리게 돌려 쉬고 있는 느낌을 준다.
    /// SetFrameRange는 구간이 실제로 바뀔 때만 처음으로 되감으므로 같은 상태에서 여러 번 불려도 괜찮다.
    /// </summary>
    private void ApplyFrameAnimation()
    {
        if (animatedObj == null) return;

        animatedObj.enabled = true;

        if (true == bRunning)
        {
            animatedObj.SetFrameRate(runFrameRate);
            animatedObj.SetFrameRange(runFrameStart, runFrameCount);
        }
        else
        {
            animatedObj.SetFrameRate(idleFrameRate);
            animatedObj.SetFrameRange(idleFrameStart, idleFrameCount);
        }
    }

    /// <summary>
    /// 가동 이펙트와 아지랑이를 지금 상태에 맞춘다. 둘은 항상 같이 켜지고 같이 꺼진다.
    ///
    /// 끌 때 남은 입자까지 지우지는 않는다(StopEmitting). 주괴가 완성되는 순간에는 가공이 한 번
    /// 꺼졌다가 남은 원석으로 곧바로 다시 켜지는데, 여기서 지워버리면 그 한 프레임 때문에 불길이
    /// 끊겼다 되살아나는 것처럼 보인다.
    /// </summary>
    private void ApplyRunningVfx()
    {
        if (loopVfx != null)
        {
            if (true == bRunning) loopVfx.Play(true);
            else StopVfx(loopVfx, false);
        }

        if (heatHaze != null) heatHaze.SetRunning(bRunning);
    }

    /// <summary>주괴가 튀어나가는 순간의 단발 이펙트. 재생 중이어도 처음부터 다시 튼다.</summary>
    private void PlayFireImpact()
    {
        if (fireImpactVfx == null) return;

        StopVfx(fireImpactVfx, true);
        fireImpactVfx.Play(true);
    }

    /// <summary>
    /// 이펙트 프리팹을 Visual 아래에 붙인다.
    /// worldPositionStays를 false로 줘야 프리팹에 저장된 로컬 좌표가 그대로 쓰인다
    /// (true면 지금 서 있는 월드 위치를 유지하려고 로컬 좌표를 다시 계산해버린다).
    /// </summary>
    private ParticleSystem SpawnVfx(ParticleSystem _prefab)
    {
        if (_prefab == null) return null;

        Transform _parent = visualTransform != null ? visualTransform : transform;

        return Instantiate(_prefab, _parent, false);
    }

    private static void StopVfx(ParticleSystem _vfx, bool _bClear)
    {
        if (_vfx == null) return;

        _vfx.Stop(true, _bClear ? ParticleSystemStopBehavior.StopEmittingAndClear : ParticleSystemStopBehavior.StopEmitting);
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (false == _other.CompareTag(PLAYER_TAG)) return;

        bPlayerInRange = true;
        UpdateInteractState();
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (false == _other.CompareTag(PLAYER_TAG)) return;

        bPlayerInRange = false;
        UpdateInteractState();
    }

    private void UpdateInteractState()
    {
        bool _interactable = bPlayerInRange && bCanReach;
        if (_interactable == bInteractable) return;

        bInteractable = _interactable;

        // 아웃라인은 여기서 켜지 않는다. 상호작용 창구는 가운데 한 대뿐이지만 세 대가 한 덩어리로
        // 같이 빛나야 하므로, 아웃라인은 BlastFurnaceManager가 세 대에 한꺼번에 지시한다.
        InteractableChangedEvent?.Invoke(_interactable);
    }

    /// <summary>
    /// 원석이 꽂히는 순간의 "뽀잉" 연출을 시작한다. LogContainer.TriggerBounce와 같다.
    /// </summary>
    public void TriggerBounce()
    {
        bounceTime = 0f;
    }

    /// <summary>
    /// 스쿼시&스트레치. X가 커지면 Y가 줄어든다. 수식은 LogContainer.UpdateBounce를 그대로 옮겼다.
    /// </summary>
    private void UpdateBounce(float _deltaTime)
    {
        if (visualTransform == null) return;

        if (bounceTime >= BounceDuration)
        {
            if (visualTransform.localScale != Vector3.one) visualTransform.localScale = Vector3.one;
            return;
        }

        bounceTime += _deltaTime;
        float t = bounceTime / BounceDuration;

        float curve = Mathf.Sin(t * Mathf.PI * 3f) * Mathf.Exp(-t * 4f) * 0.25f;

        visualTransform.localScale = new Vector3(1f + curve, 1f - curve, 1f);
    }

    /// <summary>
    /// 범위 안이라는 상태를 강제로 푼다. 순간이동으로 치워질 때(던전 이동) OnTriggerExit2D가
    /// 확실히 보장되지 않아, 남아 있으면 돌아왔을 때 아웃라인/안내가 켜진 채로 있게 된다.
    /// </summary>
    public void ResetInteractState()
    {
        bPlayerInRange = false;
        UpdateInteractState();
    }

    /// <summary>
    /// 그림만 숨긴다. 오브젝트 자체는 살아 있어 콜라이더가 계속 동작한다.
    ///
    /// 상호작용 콜라이더를 증설과 무관하게 한 자리(다이아 용광로 위치)에 고정해야 해서 필요하다.
    /// 그 자리의 용광로가 아직 해금되지 않았을 때, 오브젝트를 꺼버리면 콜라이더도 같이 죽기 때문에
    /// 오브젝트는 켜둔 채 그림만 감춘다.
    /// </summary>
    public void SetVisualVisible(bool _bVisible)
    {
        if (visualTransform == null) return;
        if (visualTransform.gameObject.activeSelf == _bVisible) return;

        visualTransform.gameObject.SetActive(_bVisible);

        // 이펙트가 Visual 아래에 있어서 여기서 함께 꺼지고 켜진다. 파티클은 Play On Awake라
        // 다시 켜지는 순간 저절로 재생되므로, 지금 상태를 다시 물려주지 않으면 멈춰 있어야 할
        // 용광로에서 불길이 올라온다. (특성으로 잠긴 용광로가 해금되는 순간이 이 경로다)
        if (true == _bVisible) ApplyRunningVfx();
    }

    /// <summary>
    /// 아웃라인 표시. 세 대를 함께 켜고 끄기 위해 BlastFurnaceManager가 호출한다.
    /// </summary>
    public void SetOutlineVisible(bool _bVisible)
    {
        if (outLineObject == null) return;
        if (outLineObject.activeSelf == _bVisible) return;

        outLineObject.SetActive(_bVisible);
    }

    private void Update()
    {
        UpdateSmelting(Time.deltaTime);
        UpdateBounce(Time.deltaTime);

        if (customSortable != null)
        {
            // 용광로는 공중에 뜨지 않으므로 높이는 항상 0이다(Tent와 동일).
            customSortable.SetHeight(0f);
        }
    }

    private void LateUpdate()
    {
        if (customSortable != null)
        {
            customSortable.ManualLateUpdate();
        }
    }
}
