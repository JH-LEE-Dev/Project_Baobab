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

    // 외부 의존성
    [SerializeField] private Transform npcTransform;
    [SerializeField] private GameObject outLineObject;
    [SerializeField] private GameObject animatorObject;
    [SerializeField] private Transform coinThrowTransform;

    // 내부 의존성
    private bool bCanInteract = false;
    private InputManager inputManager;
    private int money;
    private bool bFirstTimeEarnMoney = true;
    private CustomSortable customSortable;
    private Transform characterTransform;
    private CoinItemPoolingManager coinItemPoolingManager;
    private List<FlyingCoin> flyingCoins;
    private Coroutine coinThrowCoroutine;
    private Coroutine animationCoroutine;
    private SpriteRenderer sr;
    private SpriteRenderer outlineSr;
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

        money = 0;

        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
            sr = GetComponentInChildren<SpriteRenderer>();

        if (outLineObject != null)
        {
            outlineSr = outLineObject.GetComponent<SpriteRenderer>();
            if (outlineSr == null)
                outlineSr = outLineObject.GetComponentInChildren<SpriteRenderer>();
        }
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

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (_other.CompareTag(playerTag))
        {
            bCanInteract = true;
            animatorObject.SetActive(false);
            outLineObject.SetActive(true);

            InteractStateEvent?.Invoke(true);

            PlayAnimation(true);
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(playerTag))
        {
            animatorObject.SetActive(true);
            outLineObject.SetActive(false);
            bCanInteract = false;
            InteractStateEvent?.Invoke(false);

            PlayAnimation(false);
        }
    }

    private void InteractKeyPressed()
    {
        if (money == 0 || bCanInteract == false)
            return;

        int tempMoney = money;
        money = 0;
        ShopMoneyChangedEvent?.Invoke();

        if (bFirstTimeEarnMoney == true)
        {
            bFirstTimeEarnMoney = false;
        }

        // 동전 개수 계산 (최대 20개 제한, 금화=100,000, 은화=1,000, 동화=10)
        int remainingMoney = tempMoney;
        List<CoinSpawnInfo> coinsToSpawn = new List<CoinSpawnInfo>(20);

        while (remainingMoney > 0)
        {
            if (coinsToSpawn.Count == 19)
            {
                CoinType type = CoinType.Bronze;
                if (remainingMoney >= 100000)
                {
                    type = CoinType.Gold;
                }
                else if (remainingMoney >= 1000)
                {
                    type = CoinType.Silver;
                }
                coinsToSpawn.Add(new CoinSpawnInfo(type, remainingMoney));
                remainingMoney = 0;
                break;
            }

            if (remainingMoney >= 100000)
            {
                coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Gold, 100000));
                remainingMoney -= 100000;
            }
            else if (remainingMoney >= 1000)
            {
                coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Silver, 1000));
                remainingMoney -= 1000;
            }
            else if (remainingMoney >= 10)
            {
                coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Bronze, 10));
                remainingMoney -= 10;
            }
            else
            {
                coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Bronze, remainingMoney));
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
        float currentInterval = 0.05f;
        if (coinCount > 0)
        {
            float maxAllowedInterval = 0.5f / coinCount;
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

            float rotationSpeed = UnityEngine.Random.Range(90f, 270f) * (UnityEngine.Random.value > 0.5f ? 1f : -1f);

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
                EarnMoneyEvent?.Invoke(fc.value);
                coinItemPoolingManager.ReturnCoin(fc.coin);
                flyingCoins.RemoveAt(i);
            }
        }
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
