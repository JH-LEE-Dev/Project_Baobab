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
    [SerializeField] private GameObject frontObject;
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

    private const string playerTag = "Player";

    Transform IShopNPC.npcTransform => npcTransform;

    public int currentMoney => money;

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
            frontObject.SetActive(false);
            outLineObject.SetActive(true);

            InteractStateEvent?.Invoke(true);
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (_other.CompareTag(playerTag))
        {
            frontObject.SetActive(true);
            outLineObject.SetActive(false);
            bCanInteract = false;
            InteractStateEvent?.Invoke(false);
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

        // 동전 개수 계산 (금화=100,000, 은화=1,000, 동화=10)
        int goldCount = tempMoney / 100000;
        int remainder = tempMoney % 100000;
        int silverCount = remainder / 1000;
        remainder = remainder % 1000;
        int bronzeCount = remainder / 10;
        remainder = remainder % 10;
        if (remainder > 0)
        {
            bronzeCount += 1;
        }

        List<CoinSpawnInfo> coinsToSpawn = new List<CoinSpawnInfo>();

        for (int i = 0; i < goldCount; i++)
        {
            coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Gold, 100000));
        }

        for (int i = 0; i < silverCount; i++)
        {
            coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Silver, 1000));
        }

        int bronzeLoopCount = remainder > 0 ? bronzeCount - 1 : bronzeCount;
        for (int i = 0; i < bronzeLoopCount; i++)
        {
            coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Bronze, 10));
        }

        if (remainder > 0)
        {
            coinsToSpawn.Add(new CoinSpawnInfo(CoinType.Bronze, remainder));
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
        float maxInterval = 0.05f;
        float minInterval = 0.005f;
        int threshold = 50;

        float t = coinCount <= 1 ? 0f : Mathf.Clamp01((float)(coinCount - 1) / (threshold - 1));
        float currentInterval = Mathf.Lerp(maxInterval, minInterval, t);

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
}
