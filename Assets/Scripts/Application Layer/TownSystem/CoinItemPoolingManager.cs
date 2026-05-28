using UnityEngine;
using UnityEngine.Pool;

public class CoinItemPoolingManager : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Coin bronzeCoinPrefab;
    [SerializeField] private Coin silverCoinPrefab;
    [SerializeField] private Coin goldCoinPrefab;

    // 내부 의존성
    private IObjectPool<Coin> bronzeCoinPool;
    private IObjectPool<Coin> silverCoinPool;
    private IObjectPool<Coin> goldCoinPool;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize()
    {
        bronzeCoinPool = new ObjectPool<Coin>(
            createFunc: CreateBronzeCoin,
            actionOnGet: OnGetCoin,
            actionOnRelease: OnReleaseCoin,
            actionOnDestroy: OnDestroyCoin,
            collectionCheck: true,
            defaultCapacity: 20,
            maxSize: 100
        );

        silverCoinPool = new ObjectPool<Coin>(
            createFunc: CreateSilverCoin,
            actionOnGet: OnGetCoin,
            actionOnRelease: OnReleaseCoin,
            actionOnDestroy: OnDestroyCoin,
            collectionCheck: true,
            defaultCapacity: 20,
            maxSize: 100
        );

        goldCoinPool = new ObjectPool<Coin>(
            createFunc: CreateGoldCoin,
            actionOnGet: OnGetCoin,
            actionOnRelease: OnReleaseCoin,
            actionOnDestroy: OnDestroyCoin,
            collectionCheck: true,
            defaultCapacity: 20,
            maxSize: 100
        );
    }

    public void Release()
    {
        // 풀 자원 해제 로직 필요 시 구현
    }

    public Coin GetCoin(CoinType _coinType)
    {
        Coin coin = null;
        switch (_coinType)
        {
            case CoinType.Bronze:
                coin = bronzeCoinPool.Get();
                break;
            case CoinType.Silver:
                coin = silverCoinPool.Get();
                break;
            case CoinType.Gold:
                coin = goldCoinPool.Get();
                break;
        }

        if (coin != null)
        {
            coin.Initailize(_coinType);
        }

        return coin;
    }

    public void ReturnCoin(Coin _coin)
    {
        if (_coin == null) return;

        switch (_coin.coinType)
        {
            case CoinType.Bronze:
                bronzeCoinPool.Release(_coin);
                break;
            case CoinType.Silver:
                silverCoinPool.Release(_coin);
                break;
            case CoinType.Gold:
                goldCoinPool.Release(_coin);
                break;
        }
    }

    // 내부 풀 관리 메서드
    private Coin CreateBronzeCoin()
    {
        Coin newCoin = Instantiate(bronzeCoinPrefab, transform);
        return newCoin;
    }

    private Coin CreateSilverCoin()
    {
        Coin newCoin = Instantiate(silverCoinPrefab, transform);
        return newCoin;
    }

    private Coin CreateGoldCoin()
    {
        Coin newCoin = Instantiate(goldCoinPrefab, transform);
        return newCoin;
    }

    private void OnGetCoin(Coin _coin)
    {
        _coin.gameObject.SetActive(true);
    }

    private void OnReleaseCoin(Coin _coin)
    {
        _coin.gameObject.transform.SetParent(transform);
        _coin.gameObject.SetActive(false);
    }

    private void OnDestroyCoin(Coin _coin)
    {
        if (_coin != null)
        {
            Destroy(_coin.gameObject);
        }
    }
}
