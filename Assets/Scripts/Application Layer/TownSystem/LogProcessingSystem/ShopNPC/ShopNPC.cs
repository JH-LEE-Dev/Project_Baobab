using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class ShopNPC : MonoBehaviour, IShopNPC, IShadowCaster
{
    private struct FlyingCoin
    {
        public Coin coin;
        public int value;

        public FlyingCoin(Coin _coin, int _value)
        {
            coin = _coin;
            value = _value;
        }
    }

    private struct CoinSpawnInfo
    {
        public CoinType type;
        public int value;

        public CoinSpawnInfo(CoinType _type, int _value)
        {
            type = _type;
            value = _value;
        }
    }

    public event Action ShopMoneyChangedEvent;
    public event Action<bool> InteractStateEvent;
    public event Action<int> EarnMoneyEvent;
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
    private int money;
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
    private int pendingBatchMoney = 0;
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
    [SerializeField, Min(0f)] private float coinPitchResetDelay = 0.2f;
    [SerializeField, Min(0f)] private float coinPitchStep = 0.05f;
    [SerializeField, Min(1f)] private float coinPitchMax = 1.5f;

    private const float CoinPitchBase = 1f;
    private float currentCoinPitch = CoinPitchBase;
    private float lastCoinGetPlayedTime = float.NegativeInfinity;
    private MapType mapType;

    // LogCutter.GetSoundVolume()과 동일한 규칙: 마을이 아니면(=던전에 있는 동안 배경에서 계속
    // 수금이 진행되는 상태) 코인 사운드도 재생하지 않는다.
    public void SetMapType(MapType _mapType)
    {
        mapType = _mapType;
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

    public int currentMoney => money;

    [SerializeField] private List<Sprite> animationSprite;

    [Header("Shadow")]
    // 그림자 판정 타원. SpriteRenderer.bounds는 쓰지 않는다 - 그림자 스프라이트(Market_Shadow, 64x80px)에
    // 투명 여백이 많아 bounds가 실제로 보이는 그림자보다 한참 크기 때문이다.
    // 아래 값은 실제 불투명 픽셀 영역(x 3~61, y 12~47 / PPU 32)에서 측정한 것이다.
    //   보이는 그림자 중심 = ShopBuildingShadow 상대 위치(-0.65,-0.15) + 픽셀 중심 보정(0.016, -0.313) = (-0.63, -0.46)
    //   보이는 그림자 반경 = (0.92, 0.56) -> 회전된 타원이 이 안에 들어오도록 단축 0.46 / 장축배율 1.5
    // Scene 뷰에서 이 오브젝트를 선택하면 판정 타원이 그려지니 눈으로 보고 조절하면 된다.
    [SerializeField] private Vector2 shadowEllipseCenter = new Vector2(-0.63f, -0.46f);
    [SerializeField, Min(0f)] private float shadowEllipseRadius = 0.46f;
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

        BindEvents();
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
    }

    public void InsertMoney(int _money)
    {
        money += _money;
        ShopMoneyChangedEvent?.Invoke();
    }

    public int GetMoney()
    {
        return money;
    }

    /// <summary>
    /// 수금은 됐지만 코인이 아직 캐릭터에 도착하지 않아 지급 전인 금액.
    /// 이 구간의 돈은 상점 잔액(money)에도 캐릭터 소지금에도 존재하지 않으므로,
    /// 저장 시 이 값을 함께 적어두지 않으면 통째로 사라진다(LogProcessingManager.AppendTransitToSaveData).
    /// </summary>
    public int GetPendingBatchMoney()
    {
        return pendingBatchMoney;
    }

    public void LoadSaveData(int _money, bool _bFirstTime)
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
        int leftover = money;
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

        int tempMoney = money;
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
        int remainingMoney = tempMoney;
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

        if (null != coinThrowCoroutine)
        {
            StopCoroutine(coinThrowCoroutine);
        }
        coinThrowCoroutine = StartCoroutine(CoThrowCoins(coinsToSpawn));
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

            Sound.Play(SoundID.CoinOut, start, GetSoundVolume());

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

    private void Update()
    {
        if (null != customSortable)
            customSortable.SetHeight(0f);

        ResetCoinPitchAfterIdle();
        UpdateFlyingCoins(Time.deltaTime);
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
            }
        }
    }

    private void TryPlayCoinGetSound(Vector3 _position)
    {
        float currentTime = Time.time;
        if (coinGetCooldown > currentTime - lastCoinGetPlayedTime)
            return;

        Sound.Play(SoundID.CoinGet, _position, GetSoundVolume(), true, currentCoinPitch);
        lastCoinGetPlayedTime = currentTime;
        currentCoinPitch = Mathf.Min(Mathf.Max(CoinPitchBase, coinPitchMax), currentCoinPitch + coinPitchStep);
    }

    private void ResetCoinPitchAfterIdle()
    {
        if (CoinPitchBase >= currentCoinPitch || float.IsNegativeInfinity(lastCoinGetPlayedTime))
            return;

        if (coinPitchResetDelay <= Time.time - lastCoinGetPlayedTime)
            currentCoinPitch = CoinPitchBase;
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
            Sound.Play(true == _bOpen ? SoundID.ShopOpen : SoundID.ShopClose, transform.position, GetSoundVolume());
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
