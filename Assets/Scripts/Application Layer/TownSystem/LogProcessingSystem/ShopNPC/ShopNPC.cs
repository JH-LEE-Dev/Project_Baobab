using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;
using PresentationLayer.VFX;

public class ShopNPC : MonoBehaviour, IShopNPC, IShadowCaster
{
    private struct FlyingCoin
    {
        public Coin coin;
        public long value;
        public float trailTimer; // 비행 꼬리 금가루를 다음에 뿌릴 때까지 남은 시간(연출 전용)

        public FlyingCoin(Coin _coin, long _value)
        {
            coin = _coin;
            value = _value;
            trailTimer = 0f;
        }
    }

    // 평가기 -> 상점(원격 입금이면 -> 캐릭터)으로 날아가는 입금 금화. 돈은 이미 처리된 뒤라 순수 연출이다.
    private struct DepositCoin
    {
        public Coin coin;
        public bool bToCharacter;
        public float trailTimer;
    }

    // 같은 순간에 여러 개를 던질 때(특등품) 살짝 시차를 두고 출발시키기 위한 대기열.
    private struct PendingDeposit
    {
        public Vector3 from;
        public float delay;
        public bool bTopgrade;
    }

    private struct CoinSpawnInfo
    {
        public CoinType type;
        public long value;

        public CoinSpawnInfo(CoinType _type, long _value)
        {
            type = _type;
            value = _value;
        }
    }

    public event Action ShopMoneyChangedEvent;
    public event Action<bool> InteractStateEvent;
    public event Action<long> EarnMoneyEvent;
    public event Action<bool> RemoteDepositModeChangedEvent;

    // 외부 의존성
    [SerializeField] private Transform npcTransform;
    [SerializeField] private GameObject outLineObject;
    [SerializeField] private GameObject animatorObject;
    [SerializeField] private Transform coinThrowTransform;

    // 내부 의존성
    private bool bCanInteract = false;
    private bool bPhysicalOverlapped = false;
    private bool bCanReach = true;
    private bool bLastInteractState = false;
    private bool bRemoteDepositLocked = false;

    public bool isPhysicalOverlapped => bPhysicalOverlapped;
    private InputManager inputManager;
    private long money;
    private bool bFirstTimeEarnMoney = true;
    private CustomSortable customSortable;
    private Transform characterTransform;
    private CoinItemPoolingManager coinItemPoolingManager;
    private Character character;
    private List<FlyingCoin> flyingCoins;
    private Coroutine coinThrowCoroutine;
    private Coroutine animationCoroutine;

    // 코인은 개별적으로 날아가 보이지만, 실제 재화 지급은 연출과 무관하게 첫 번째 코인이
    // 캐릭터에 도착하는 순간 전액 한 번에 처리한다(뒤이어 도착하는 코인들은 시각 연출일 뿐).
    private long pendingBatchMoney = 0;
    private bool bAwaitingFirstArrival = false;

    // 상호작용 키는 Input System 콜백(해당 프레임의 모든 Update()보다 먼저 실행됨)에서 바로 들어온다.
    // 원목 가공 완료로 인한 InsertMoney()는 LogInBelt 등의 Update() 체인 안에서 일어나므로, 여기서
    // money를 즉시 읽어버리면 "같은 프레임에 막 들어온 돈"을 못 잡고 상점에 남겨두는 경합이 생긴다.
    // 그래서 키 입력은 요청 플래그만 세우고, 실제 수금은 그 프레임의 모든 Update()가 끝난 뒤인
    // LateUpdate()에서 처리해 이 경합을 없앤다.
    private bool bInteractRequested = false;

    // 요청을 받은 LateUpdate()에서 조건(돈이 있음 / 상호작용 가능)이 아직 안 맞아도 즉시 버리지 않고,
    // 이 프레임 수만큼 들고 있다가 재시도한다. 한 프레임만 어긋나서 입력이 통째로 씹히는 경우가 있다.
    //  - bCanInteract: LogProcessingManager.Update()의 CalcDistForInteraction()이 입력 콜백과 이
    //    LateUpdate() 사이에서 돌면서 상점 도달 가능 여부를 뒤집을 수 있다(보관함과 콜라이더가 겹치는 경계).
    //  - money == 0: 광클 중에는 직전 프레임에 이미 수금해 잔액이 0인 프레임에 입력이 떨어지는 일이
    //    대부분이라, 바로 다음 프레임의 InsertMoney()까지는 그 입력으로 이어서 받게 해준다.
    private const int InteractRequestLifeFrames = 2;
    private int interactRequestLifeFrames = 0;

    [Header("Coin Pickup Sound")]
    [SerializeField, Min(0f)] private float coinGetCooldown = 0.04f;

    private float lastCoinGetPlayedTime = float.NegativeInfinity;
    private MapType mapType;

    // //돈 연출(VFX_TownMoney). 전부 그림일 뿐이라 돈·판정·타이밍에는 관여하지 않는다. 마을에 있을 때만 보여준다.
    [Header("Money VFX - 입금 금화 (평가기 -> 상점)")]
    [Tooltip("입금 금화가 튀어 나오는 자리(평가기 기준점에서, 유닛). 평가기 윗면.")]
    [SerializeField] private Vector2 depositLaunchOffset = new Vector2(0f, 0.9f);
    [Tooltip("입금 금화가 들어가는 자리(상점 기준점에서, 유닛). 상점 계산대 창.")]
    [SerializeField] private Vector2 depositLandOffset = new Vector2(0.3f, 0.55f);
    [SerializeField] private float depositFlyDuration = 0.42f;
    [SerializeField] private float depositFlyHeight = 0.8f;
    [Tooltip("특등품이면 금화가 이만큼 더 날아간다(평가기 금화 분수와 함께).")]
    [SerializeField, Min(0)] private int depositTopgradeExtraCoins = 2;
    [Tooltip("여러 개를 같이 던질 때 출발 간격(초).")]
    [SerializeField] private float depositStagger = 0.07f;

    [Header("Money VFX - 비행 꼬리")]
    [Tooltip("날아가는 금화 하나가 꼬리 금가루를 뿌리는 간격(초).")]
    [SerializeField] private float coinTrailInterval = 0.03f;

    [Header("Money VFX - 상점 대기 반짝임")]
    [Tooltip("반짝임이 피는 범위의 가운데(상점 기준점에서, 유닛)와 반폭·반높이.")]
    [SerializeField] private Vector2 shopTwinkleCenter = new Vector2(0.1f, 0.75f);
    [SerializeField] private Vector2 shopTwinkleExtents = new Vector2(0.55f, 0.4f);
    [Tooltip("돈이 막 들어왔을 때(적게 쌓였을 때) 반짝임 간격(초).")]
    [SerializeField] private float shopTwinkleIntervalSlow = 0.5f;
    [Tooltip("많이 쌓였을 때 반짝임 간격(초).")]
    [SerializeField] private float shopTwinkleIntervalFast = 0.09f;
    [Tooltip("입금이 이만큼 쌓이면 가장 빠르게 반짝인다.")]
    [SerializeField, Min(1)] private int shopTwinkleDepositsForFast = 20;

    [Header("Money VFX - 주괴 판매")]
    [Tooltip("주괴가 꽂힐 때 터지는 자리(상점 기준점에서, 유닛).")]
    [SerializeField] private Vector2 ingotSaleOffset = new Vector2(0f, 0.6f);

    private VFX_TownMoney moneyVfx;
    private Transform depositLandPoint;
    private readonly List<DepositCoin> depositCoins = new List<DepositCoin>(16);
    private readonly List<PendingDeposit> pendingDeposits = new List<PendingDeposit>(8);
    private int pendingDepositHeat = 0;     // 마지막 수금 이후 쌓인 입금 횟수(반짝임 빈도용)
    private float shopTwinkleTimer = 0f;
    private int collectCoinsRemaining = 0;  // 이번 수금에서 아직 캐릭터에 닿지 않은 금화 수(마지막 금화 판별용)

    // LogCutter.GetSoundVolume()과 동일한 규칙: 마을이 아니면(=던전에 있는 동안 배경에서 계속
    // 수금이 진행되는 상태) 코인 사운드도 재생하지 않는다.
    public void SetMapType(MapType _mapType)
    {
        mapType = _mapType;

        // 마을을 떠나면 그리던 돈 이펙트를 지운다(날아가던 금화 자체는 그대로 끝까지 간다).
        if (MapType.Town != mapType && null != moneyVfx) moneyVfx.ClearAll();
    }

    private bool IsMoneyVfxVisible()
    {
        return null != moneyVfx && MapType.Town == mapType;
    }

    private float GetSoundVolume()
    {
        return MapType.Town == mapType ? 1f : 0f;
    }
    public SpriteRenderer sr;
    public SpriteRenderer outlineSr;
    private int currentFrameIndex = 0;
    private WaitForSeconds frameWait;
    [SerializeField] private float frameTime = 0.05f;

    private const string playerTag = "Player";

    Transform IShopNPC.npcTransform => npcTransform;

    public long currentMoney => money;

    [SerializeField] private List<Sprite> animationSprite;

    [Header("Shadow")]
    // 그림자 판정 타원. SpriteRenderer.bounds는 쓰지 않는다 - 그림자 스프라이트(NewMarket_Shadow, 128x128px)에
    // 투명 여백이 많아 bounds가 실제로 보이는 그림자보다 한참 크기 때문이다.
    // 아래 값은 실제 불투명 픽셀 영역에서 측정한 것이다(PPU 32). 스프라이트에는 건물 밑변을 따라
    // 오른쪽 위로 뻗는 2~3px 두께의 대각선 꼬리가 하나 더 있지만, 건물 스프라이트(Sorting Layer 13)에
    // 완전히 가려져 화면에 보이지 않으므로 측정에서 제외했다(3x3 opening으로 제거 -> 본체 429px).
    //   본체 불투명 영역 = x 33~69, y(아래에서) 48~68 -> 중심 픽셀 (51.5, 58.3)
    //   보이는 그림자 중심 = ShopBuildingShadow 상대 위치(-0.435,-0.328) + 픽셀 중심 보정(-0.391, -0.178) = (-0.83, -0.50)
    //   보이는 그림자 반경 = (0.58, 0.33) / 그림자축(34도) 기준 폭은 단축 0.28, 길이 0.53
    //     -> 끝이 뾰족한 평행사변형이라 장축은 실측보다 짧게 잡는다: 단축 0.28 / 장축배율 1.5
    // Scene 뷰에서 이 오브젝트를 선택하면 판정 타원이 그려지니 눈으로 보고 조절하면 된다.
    [SerializeField] private Vector2 shadowEllipseCenter = new Vector2(-0.83f, -0.5f);
    [SerializeField, Min(0f)] private float shadowEllipseRadius = 0.28f;
    [SerializeField, Min(1f)] private float shadowEllipseLengthScale = 1.5f;

    // 타원 중심을 Position에 직접 담으므로 TopShadowOffset은 항상 0이다.
    // (TopShadowOffset은 회전 보정된 로컬 좌표계 값이라 그대로 넣으면 방향이 어긋난다.)
    public Vector2 Position => (Vector2)transform.position + shadowEllipseCenter;
    public float TopShadowRadius => shadowEllipseRadius;
    public Vector2 TopShadowOffset => Vector2.zero;
    public float ShadowLengthScaleOverride => shadowEllipseLengthScale;

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (0f >= shadowEllipseRadius) return;

        Tent.DrawShadowEllipseGizmo(transform.position, shadowEllipseCenter, shadowEllipseRadius, shadowEllipseLengthScale, 34f + 90f);
    }
#endif

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
        customSortable = GetComponent<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.SetSortingGroup(GetComponent<SortingGroup>());

        coinItemPoolingManager = GetComponent<CoinItemPoolingManager>();
        coinItemPoolingManager.Initialize();

        flyingCoins = new List<FlyingCoin>(32);

        money = 0;

        frameWait = new WaitForSeconds(frameTime);

        SetupMoneyVfx();

        BindEvents();
    }

    // 돈 이펙트와 입금 금화 도착 지점을 만든다. 상점 건물(이 오브젝트)에는 SortingGroup이 있어 그 아래에 두면
    // 이펙트가 건물 정렬에 묶이므로, 그룹이 없는 부모(Building) 아래에 둔다. 셰이더가 없으면 이펙트 없이 진행한다.
    private void SetupMoneyVfx()
    {
        if (null == moneyVfx)
        {
            Transform vfxParent = null != transform.parent ? transform.parent : transform;
            moneyVfx = vfxParent.GetComponentInChildren<VFX_TownMoney>(true);
            if (null == moneyVfx) moneyVfx = VFX_TownMoney.Create(vfxParent);
        }

        if (null == depositLandPoint)
        {
            depositLandPoint = new GameObject("DepositLandPoint").transform;
            depositLandPoint.SetParent(transform, false);
        }
        depositLandPoint.localPosition = depositLandOffset;
    }

    public void Release()
    {
        ReleaseEvents();

        bInteractRequested = false;
        interactRequestLifeFrames = 0;

        if (null != coinThrowCoroutine)
        {
            StopCoroutine(coinThrowCoroutine);
            coinThrowCoroutine = null;
        }
        if (null != animationCoroutine)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        // 입금 금화는 순수 연출이라 남은 것을 풀로 돌려보내기만 한다.
        pendingDeposits.Clear();
        for (int i = 0; i < depositCoins.Count; i++)
        {
            if (null != depositCoins[i].coin) coinItemPoolingManager.ReturnCoin(depositCoins[i].coin);
        }
        depositCoins.Clear();
        if (null != moneyVfx) moneyVfx.ClearAll();
    }

    /// <summary>
    /// 원목 하나가 평가되어 돈이 들어온 순간의 연출. 평가기(_storagePos) 윗면에서 금화가 튀어 나와 상점으로 날아간다
    /// (원격 입금 중이면 캐릭터로). _bTopgrade면 금화가 더 날아간다. 돈은 이미 처리된 뒤라 그림만 그린다.
    /// </summary>
    public void PlayDepositVisual(Vector3 _storagePos, bool _bTopgrade)
    {
        if (false == IsMoneyVfxVisible()) return;

        Vector3 from = _storagePos + (Vector3)depositLaunchOffset;
        int count = _bTopgrade ? depositTopgradeExtraCoins : 1;
        // 특등품 금화는 평가기 "쾅" 금화 분수가 솟은 뒤에 따라 나오도록 한 칸 늦게 출발한다.
        float baseDelay = _bTopgrade ? depositStagger : 0f;
        for (int i = 0; i < count; i++)
        {
            pendingDeposits.Add(new PendingDeposit { from = from, delay = baseDelay + depositStagger * i, bTopgrade = _bTopgrade });
        }

        if (false == bRemoteDepositLocked) pendingDepositHeat += count;
    }

    /// <summary>용광로 주괴가 상점에 꽂히는 순간의 연출(BlastFurnaceManager가 돈을 넣은 직후 부른다).</summary>
    public void PlayIngotSaleVisual()
    {
        if (false == IsMoneyVfxVisible()) return;

        moneyVfx.PlayIngotSale(transform.position + (Vector3)ingotSaleOffset);
        if (false == bRemoteDepositLocked) pendingDepositHeat += 3;
    }

    public void InsertMoney(long _money)
    {
        money += _money;
        ShopMoneyChangedEvent?.Invoke();
    }

    public long GetMoney()
    {
        return money;
    }

    /// <summary>
    /// 수금은 됐지만 코인이 아직 캐릭터에 도착하지 않아 지급 전인 금액.
    /// 이 구간의 돈은 상점 잔액(money)에도 캐릭터 소지금에도 존재하지 않으므로,
    /// 저장 시 이 값을 함께 적어두지 않으면 통째로 사라진다(LogProcessingManager.AppendTransitToSaveData).
    /// </summary>
    public long GetPendingBatchMoney()
    {
        return pendingBatchMoney;
    }

    public void LoadSaveData(long _money, bool _bFirstTime)
    {
        money = _money;
        bFirstTimeEarnMoney = _bFirstTime;
        ShopMoneyChangedEvent?.Invoke();
    }

    public bool GetbFirstTimeEarnMoney()
    {
        return bFirstTimeEarnMoney;
    }

    private void BindEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractKeyPressed;
        inputManager.inputReader.InteractionKeyPressedEvent += InteractKeyPressed;
    }

    private void ReleaseEvents()
    {
        inputManager.inputReader.InteractionKeyPressedEvent -= InteractKeyPressed;
    }

    private void UpdateInteractState()
    {
        bool currentState = !bRemoteDepositLocked && bCanReach && bPhysicalOverlapped;
        if (bLastInteractState != currentState)
        {
            bLastInteractState = currentState;
            bCanInteract = currentState;
            InteractStateEvent?.Invoke(currentState);
            outLineObject.SetActive(currentState);
            PlayAnimation(currentState);
        }
    }

    public void SetCanReach(bool _bCanReach)
    {
        bCanReach = _bCanReach;
        UpdateInteractState();
    }

    public void SetRemoteDepositLock(bool _bLocked)
    {
        bRemoteDepositLocked = _bLocked;
        UpdateInteractState();
        RemoteDepositModeChangedEvent?.Invoke(_bLocked);
    }

    public void ClearMoneyToPlayer()
    {
        long leftover = money;
        money = 0;
        ShopMoneyChangedEvent?.Invoke();

        if (0 < leftover)
        {
            EarnMoneyEvent?.Invoke(leftover);
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.CompareTag(playerTag))
        {
            bPhysicalOverlapped = true;
            UpdateInteractState();
        }
    }

    private void OnTriggerStay2D(Collider2D _other)
    {
        if (_other.CompareTag(playerTag))
        {
            if (false == bPhysicalOverlapped)
            {
                bPhysicalOverlapped = true;
                UpdateInteractState();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(playerTag))
        {
            bPhysicalOverlapped = false;
            UpdateInteractState();
        }
    }

    private void InteractKeyPressed()
    {
        if (false == bCanInteract)
            return;

        bInteractRequested = true;
        interactRequestLifeFrames = InteractRequestLifeFrames;
    }

    // InteractKeyPressed()에서 요청 플래그만 받아, 그 프레임의 InsertMoney()까지 전부 반영된
    // 뒤(LateUpdate)에 실제 수금을 처리한다.
    private void ProcessInteractRequest()
    {
        if (false == bInteractRequested)
            return;

        // 조건이 아직 안 맞으면 입력을 버리지 않고 InteractRequestLifeFrames 동안 재시도한다.
        if (0 == money || false == bCanInteract)
        {
            --interactRequestLifeFrames;
            if (0 >= interactRequestLifeFrames)
                bInteractRequested = false;

            return;
        }

        bInteractRequested = false;

        long tempMoney = money;
        money = 0;
        ShopMoneyChangedEvent?.Invoke();

        pendingBatchMoney += tempMoney;
        bAwaitingFirstArrival = true;

        if (true == bFirstTimeEarnMoney)
        {
            bFirstTimeEarnMoney = false;
        }

        // 동전 개수 계산 (최대 20개 제한). 등급 구분 없이 항상 골드 동전만 생성하며,
        // 예전에 Bronze(동화)를 나누던 것과 동일하게 10 단위로만 쪼갠다.
        long remainingMoney = tempMoney;
        List<CoinSpawnInfo> coinsToSpawn = new List<CoinSpawnInfo>(20);

        while (0 < remainingMoney)
        {
            if (19 == coinsToSpawn.Count)
            {
                coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Gold, remainingMoney));
                remainingMoney = 0;
                break;
            }

            if (10 <= remainingMoney)
            {
                coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Gold, 10));
                remainingMoney -= 10;
            }
            else
            {
                coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Gold, remainingMoney));
                remainingMoney = 0;
            }
        }

        // 이미 날아가는 금화(직전 수금분)까지 이번 수금의 금화로 쳐서, 마지막으로 닿는 금화에 마무리 연출을 건다.
        // (다시 수금하면 직전 코루틴의 아직 안 던진 금화는 던져지지 않으므로 그 수는 세지 않는다.)
        // StartCoroutine이 첫 금화를 그 자리에서 던지므로 반드시 코루틴을 시작하기 전에 센다.
        collectCoinsRemaining = flyingCoins.Count + coinsToSpawn.Count;

        if (null != coinThrowCoroutine)
        {
            StopCoroutine(coinThrowCoroutine);
        }
        coinThrowCoroutine = StartCoroutine(CoThrowCoins(coinsToSpawn));

        pendingDepositHeat = 0;
        shopTwinkleTimer = 0f;
    }

    private IEnumerator CoThrowCoins(List<CoinSpawnInfo> _coins)
    {
        int coinCount = _coins.Count;
        float currentInterval = 0.09f;
        if (0 < coinCount)
        {
            float maxAllowedInterval = 0.9f / coinCount;
            if (maxAllowedInterval < currentInterval)
            {
                currentInterval = maxAllowedInterval;
            }
        }

        for (int i = 0; i < coinCount; i++)
        {
            CoinSpawnInfo info = _coins[i];
            Coin coin = coinItemPoolingManager.GetCoin(info.type);
            if (null == coin) continue;

            coin.gameObject.SetActive(true);

            Vector3 start = null != coinThrowTransform ? coinThrowTransform.position : transform.position;
            Vector3 end = null != characterTransform ? characterTransform.position : transform.position;

            Sound.Play(SoundID.CoinOut, start, GetSoundVolume(), pitchOverride: GetRandomCoinPitch(1f));

            Vector3 dir = (end - start).normalized;
            if (Vector3.zero == dir) dir = Vector3.up;
            Vector3 normal = new Vector3(-dir.y, dir.x, 0f);
            float arcPower = UnityEngine.Random.Range(-0.3f, 0.3f);
            Vector3 trajectoryJitter = normal * arcPower;

            float rotationSpeed = UnityEngine.Random.Range(60f, 160f) * (0.5f < UnityEngine.Random.value ? 1f : -1f);

            coin.DynamicTransferLaunch(
                start,
                characterTransform,
                UnityEngine.Random.Range(0.8f, 1.2f),
                UnityEngine.Random.Range(0.5f, 0.5f),
                trajectoryJitter,
                rotationSpeed
            );

            flyingCoins.Add(new FlyingCoin(coin, info.value));

            yield return new WaitForSeconds(currentInterval);
        }
    }

    /// <summary>
    /// 무언가 꽂혔을 때의 "뽀잉". 용광로에서 날아온 주괴가 박힐 때 BlastFurnaceManager가 부른다.
    /// 수식과 지속시간은 LogContainer가 원목을 받을 때와 같다.
    /// </summary>
    public void TriggerBounce()
    {
        bounceTime = 0f;
    }

    private const float BOUNCE_DURATION_SHOP = 0.4f;
    private float bounceTime = BOUNCE_DURATION_SHOP;

    private void UpdateBounce(float _deltaTime)
    {
        if (null == animatorObject) return;

        Transform visual = animatorObject.transform;

        if (bounceTime >= BOUNCE_DURATION_SHOP)
        {
            if (visual.localScale != Vector3.one) visual.localScale = Vector3.one;
            return;
        }

        bounceTime += _deltaTime;
        float t = bounceTime / BOUNCE_DURATION_SHOP;

        float curve = Mathf.Sin(t * Mathf.PI * 3f) * Mathf.Exp(-t * 4f) * 0.25f;

        visual.localScale = new Vector3(1f + curve, 1f - curve, 1f);
    }

    private void Update()
    {
        if (null != customSortable)
            customSortable.SetHeight(0f);

        UpdateBounce(Time.deltaTime);
        UpdateFlyingCoins(Time.deltaTime);
        UpdateDepositCoins(Time.deltaTime);
        UpdateShopTwinkle(Time.deltaTime);
    }

    // 입금 금화 출발·비행·도착. 돈과 무관한 순수 연출이다.
    private void UpdateDepositCoins(float _deltaTime)
    {
        for (int i = pendingDeposits.Count - 1; i >= 0; i--)
        {
            PendingDeposit pending = pendingDeposits[i];
            pending.delay -= _deltaTime;
            if (pending.delay > 0f)
            {
                pendingDeposits[i] = pending;
                continue;
            }

            pendingDeposits.RemoveAt(i);
            LaunchDepositCoin(pending.from, pending.bTopgrade);
        }

        for (int i = depositCoins.Count - 1; i >= 0; i--)
        {
            DepositCoin dc = depositCoins[i];
            dc.coin.ManualUpdate(_deltaTime);

            if (dc.coin.isArrived)
            {
                if (IsMoneyVfxVisible())
                {
                    if (dc.bToCharacter) moneyVfx.PlayCoinArrive(dc.coin.transform.position, false, false);
                    else moneyVfx.PlayDepositLand(dc.coin.transform.position);
                }

                coinItemPoolingManager.ReturnCoin(dc.coin);
                depositCoins.RemoveAt(i);
                continue;
            }

            EmitTrail(dc.coin, dc.bToCharacter ? characterTransform : depositLandPoint, ref dc.trailTimer, _deltaTime);
            depositCoins[i] = dc;
        }
    }

    private void LaunchDepositCoin(Vector3 _from, bool _bTopgrade)
    {
        if (false == IsMoneyVfxVisible()) return;

        // 원격 입금 중이면 상점이 잠겨 있으므로 금화가 캐릭터에게 바로 날아간다.
        bool bToCharacter = bRemoteDepositLocked && null != characterTransform;
        Transform target = bToCharacter ? characterTransform : depositLandPoint;
        if (null == target) return;

        Coin coin = coinItemPoolingManager.GetCoin(CoinType.Gold);
        if (null == coin) return;
        coin.gameObject.SetActive(true);

        float duration = depositFlyDuration;
        float height = depositFlyHeight;
        if (bToCharacter)
        {
            // 캐릭터는 마을 어디에나 있을 수 있어 거리에 맞춰 늘린다.
            float dist = Vector2.Distance(_from, target.position);
            duration = Mathf.Clamp(depositFlyDuration + dist * 0.05f, depositFlyDuration, 1f);
            height = Mathf.Min(depositFlyHeight + dist * 0.08f, 2f);
        }

        Vector3 dir = (target.position - _from).normalized;
        if (Vector3.zero == dir) dir = Vector3.up;
        Vector3 normal = new Vector3(-dir.y, dir.x, 0f);
        Vector3 jitter = normal * UnityEngine.Random.Range(-0.15f, 0.15f);

        coin.DynamicTransferLaunch(_from, target, height * UnityEngine.Random.Range(0.9f, 1.1f), duration, jitter, 0f);
        depositCoins.Add(new DepositCoin { coin = coin, bToCharacter = bToCharacter, trailTimer = 0f });

        moneyVfx.PlayDepositPop(_from, _bTopgrade);
    }

    // 날아가는 금화 뒤로 꼬리 금가루를 일정 간격으로 뿌린다. 등장·흡수 구간(작아져 있을 때)과 도착 지점 바로 앞에서는
    // 뿌리지 않는다 - 연달아 도착하는 금화의 꼬리가 캐릭터 얼굴 위에 쌓여 가리기 때문이다.
    private const float TrailStopDistanceSqr = 0.5f * 0.5f;

    private void EmitTrail(Coin _coin, Transform _target, ref float _timer, float _deltaTime)
    {
        if (false == IsMoneyVfxVisible()) return;

        _timer -= _deltaTime;
        if (_timer > 0f) return;
        _timer += coinTrailInterval;
        if (_timer < 0f) _timer = 0f;

        if (_coin.transform.localScale.y < 0.6f) return;

        Vector3 pos = _coin.transform.position;
        if (null != _target && ((Vector2)(_target.position - pos)).sqrMagnitude < TrailStopDistanceSqr) return;

        moneyVfx.EmitCoinTrail(pos);
    }

    // 수금 안 한 돈이 있으면 상점 둘레에 반짝임이 핀다. 입금이 쌓일수록 자주 핀다.
    private void UpdateShopTwinkle(float _deltaTime)
    {
        if (false == IsMoneyVfxVisible() || 0 >= money || bRemoteDepositLocked) return;

        shopTwinkleTimer -= _deltaTime;
        if (shopTwinkleTimer > 0f) return;

        float heat = Mathf.Clamp01(pendingDepositHeat / (float)shopTwinkleDepositsForFast);
        float interval = Mathf.Lerp(shopTwinkleIntervalSlow, shopTwinkleIntervalFast, heat);
        shopTwinkleTimer = interval * UnityEngine.Random.Range(0.7f, 1.3f);

        Vector3 hopPos = null != depositLandPoint ? depositLandPoint.position : transform.position;
        moneyVfx.EmitShopTwinkle(transform.position + (Vector3)shopTwinkleCenter, shopTwinkleExtents, hopPos, heat);
    }

    private void LateUpdate()
    {
        ProcessInteractRequest();

        if (null != customSortable)
            customSortable.ManualLateUpdate();
    }

    private void UpdateFlyingCoins(float _deltaTime)
    {
        for (int i = flyingCoins.Count - 1; i >= 0; i--)
        {
            FlyingCoin fc = flyingCoins[i];
            fc.coin.ManualUpdate(_deltaTime);

            if (fc.coin.isArrived)
            {
                // 도착 이펙트: 첫 금화(실제 지급 순간)와 마지막 금화는 크게 터진다.
                if (0 < collectCoinsRemaining) --collectCoinsRemaining;
                if (IsMoneyVfxVisible())
                {
                    moneyVfx.PlayCoinArrive(fc.coin.transform.position, bAwaitingFirstArrival, 0 == collectCoinsRemaining);
                }

                if (bAwaitingFirstArrival)
                {
                    EarnMoneyEvent?.Invoke(pendingBatchMoney);
                    pendingBatchMoney = 0;
                    bAwaitingFirstArrival = false;
                }

                // 실제 재화 지급은 첫 코인 도착 시 한 번에 처리하지만, 반짝임/카메라 셰이크는
                // 코인이 도착할 때마다(짤랑짤랑) 매번 재생해 손맛을 살린다.
                character?.PlayItemAcquireBounce();
                character?.PlayItemAcquireFlash();

                // 코인이 캐릭터에 박히는 순간. 이 경로는 항상 캐릭터가 받는 흐름이다.
                // 개수가 많으면 간격이 0.03초까지 좁아지므로 연속 전용(약한) 파형을 쓴다.
                Rumble.Play(EHapticEvent.ItemStream);

                TryPlayCoinGetSound(fc.coin.transform.position);

                coinItemPoolingManager.ReturnCoin(fc.coin);
                flyingCoins.RemoveAt(i);
                continue;
            }

            EmitTrail(fc.coin, characterTransform, ref fc.trailTimer, _deltaTime);
            flyingCoins[i] = fc;
        }
    }

    private void TryPlayCoinGetSound(Vector3 _position)
    {
        float currentTime = Time.time;
        if (coinGetCooldown > currentTime - lastCoinGetPlayedTime)
            return;

        Sound.Play(SoundID.CoinGet, _position, GetSoundVolume(), pitchOverride: GetRandomCoinPitch(2f));
        lastCoinGetPlayedTime = currentTime;
    }

    private static float GetRandomCoinPitch(float _maxSemitones)
    {
        float semitones = UnityEngine.Random.Range(-_maxSemitones, _maxSemitones);
        return Mathf.Pow(2f, semitones / 12f);
    }

    public void SetCharacter(Character _character)
    {
        character = _character;
    }

    public void SetCharacterTransform(Transform _transform)
    {
        characterTransform = _transform;
    }

    private void PlayAnimation(bool _bOpen)
    {
        if (null != animationCoroutine)
        {
            StopCoroutine(animationCoroutine);
            animationCoroutine = null;
        }

        if (gameObject.activeInHierarchy)
        {
            animationCoroutine = StartCoroutine(CoPlayAnimation(_bOpen));
        }
        else
        {
            if (null != animationSprite && 0 < animationSprite.Count)
            {
                currentFrameIndex = true == _bOpen ? animationSprite.Count - 1 : 0;
                Sprite currentSprite = animationSprite[currentFrameIndex];
                if (null != sr) sr.sprite = currentSprite;
                if (null != outlineSr) outlineSr.sprite = currentSprite;
            }
        }
    }

    private IEnumerator CoPlayAnimation(bool _bOpen)
    {
        if (null == animationSprite || 0 == animationSprite.Count) yield break;

        int targetFrame = true == _bOpen ? animationSprite.Count - 1 : 0;
        int step = true == _bOpen ? 1 : -1;

        while (targetFrame != currentFrameIndex)
        {
            currentFrameIndex = Mathf.Clamp(currentFrameIndex + step, 0, animationSprite.Count - 1);
            Sprite currentSprite = animationSprite[currentFrameIndex];
            if (null != sr) sr.sprite = currentSprite;
            if (null != outlineSr) outlineSr.sprite = currentSprite;

            yield return frameWait;
        }

        animationCoroutine = null;
    }
}
