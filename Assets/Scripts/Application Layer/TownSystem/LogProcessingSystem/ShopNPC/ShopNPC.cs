using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Rendering;

public class ShopNPC : MonoBehaviour, IShopNPC
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
    public SpriteRenderer sr;
    public SpriteRenderer outlineSr;
    private int currentFrameIndex = 0;
    private WaitForSeconds frameWait;
    [SerializeField] private float frameTime = 0.05f;

    private const string playerTag = "Player";

    Transform IShopNPC.npcTransform => npcTransform;

    public int currentMoney => money;

    [SerializeField] private List<Sprite> animationSprite;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
        customSortable = GetComponent<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.SetSortingGroup(GetComponent<SortingGroup>());

        coinItemPoolingManager = GetComponent<CoinItemPoolingManager>();
        coinItemPoolingManager.Initialize();

        flyingCoins = new List<FlyingCoin>(32);

        money = 10000;

        frameWait = new WaitForSeconds(frameTime);

        BindEvents();
    }

    public void Release()
    {
        ReleaseEvents();
        if (coinThrowCoroutine != null)
        {
            StopCoroutine(coinThrowCoroutine);
            coinThrowCoroutine = null;
        }
        if (animationCoroutine != null)
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
        if (currentState != bLastInteractState)
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

        if (leftover > 0)
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
            if (bPhysicalOverlapped == false)
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
        if (money == 0 || bCanInteract == false)
            return;

        int tempMoney = money;
        money = 0;
        ShopMoneyChangedEvent?.Invoke();

        pendingBatchMoney += tempMoney;
        bAwaitingFirstArrival = true;

        if (bFirstTimeEarnMoney == true)
        {
            bFirstTimeEarnMoney = false;
        }

        // 동전 개수 계산 (최대 20개 제한). 등급 구분 없이 항상 골드 동전만 생성하며,
        // 예전에 Bronze(동화)를 나누던 것과 동일하게 10 단위로만 쪼갠다.
        int remainingMoney = tempMoney;
        List<CoinSpawnInfo> coinsToSpawn = new List<CoinSpawnInfo>(20);

        while (remainingMoney > 0)
        {
            if (coinsToSpawn.Count == 19)
            {
                coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Gold, remainingMoney));
                remainingMoney = 0;
                break;
            }

            if (remainingMoney >= 10)
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

        if (coinThrowCoroutine != null)
        {
            StopCoroutine(coinThrowCoroutine);
        }
        coinThrowCoroutine = StartCoroutine(CoThrowCoins(coinsToSpawn));
    }

    private IEnumerator CoThrowCoins(List<CoinSpawnInfo> _coins)
    {
        int coinCount = _coins.Count;
        float currentInterval = 0.09f;
        if (coinCount > 0)
        {
            float maxAllowedInterval = 0.9f / coinCount;
            if (currentInterval > maxAllowedInterval)
            {
                currentInterval = maxAllowedInterval;
            }
        }

        for (int i = 0; i < coinCount; i++)
        {
            CoinSpawnInfo info = _coins[i];
            Coin coin = coinItemPoolingManager.GetCoin(info.type);
            if (coin == null) continue;

            coin.gameObject.SetActive(true);

            Vector3 start = coinThrowTransform != null ? coinThrowTransform.position : transform.position;
            Vector3 end = characterTransform != null ? characterTransform.position : transform.position;

            Vector3 dir = (end - start).normalized;
            if (dir == Vector3.zero) dir = Vector3.up;
            Vector3 normal = new Vector3(-dir.y, dir.x, 0f);
            float arcPower = UnityEngine.Random.Range(-0.3f, 0.3f);
            Vector3 trajectoryJitter = normal * arcPower;

            float rotationSpeed = UnityEngine.Random.Range(60f, 160f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

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
        if (customSortable != null)
            customSortable.SetHeight(0f);

        UpdateFlyingCoins(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (customSortable != null)
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

                coinItemPoolingManager.ReturnCoin(fc.coin);
                flyingCoins.RemoveAt(i);
            }
        }
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
        if (animationCoroutine != null)
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
            if (animationSprite != null && animationSprite.Count > 0)
            {
                currentFrameIndex = _bOpen ? animationSprite.Count - 1 : 0;
                Sprite currentSprite = animationSprite[currentFrameIndex];
                if (sr != null) sr.sprite = currentSprite;
                if (outlineSr != null) outlineSr.sprite = currentSprite;
            }
        }
    }

    private IEnumerator CoPlayAnimation(bool _bOpen)
    {
        if (animationSprite == null || animationSprite.Count == 0) yield break;

        int targetFrame = _bOpen ? animationSprite.Count - 1 : 0;
        int step = _bOpen ? 1 : -1;

        while (currentFrameIndex != targetFrame)
        {
            currentFrameIndex = Mathf.Clamp(currentFrameIndex + step, 0, animationSprite.Count - 1);
            Sprite currentSprite = animationSprite[currentFrameIndex];
            if (sr != null) sr.sprite = currentSprite;
            if (outlineSr != null) outlineSr.sprite = currentSprite;

            yield return frameWait;
        }

        animationCoroutine = null;
    }
}
