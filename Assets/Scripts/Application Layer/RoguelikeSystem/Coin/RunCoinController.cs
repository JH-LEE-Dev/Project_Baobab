using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 로그라이크 코인. 기획서 3-3 "골드(코인) 획득" 5단계.
///   1. 벌목: 나무를 베면 코인이 흩뿌려진다. 바로 흡수되지 않는다.
///   2. 전투 중: 바닥 코인은 알파를 낮춰 거의 안 보이게 한다.
///   3~4. 웨이브 종료: 알파를 되돌리고 한꺼번에 흡수한다(묘목 연출과 동시).
///   5. 표시: 도착할 때마다 지갑에 더한다(HUD는 RoguelikeGoldChangedSignal로 오른다).
///
/// 웨이브 도중 게임오버가 나면 바닥 코인은 지갑에 들어가지 않는다(DiscardAll). 결과 화면 총 획득 코인에서도 빠진다.
/// 코인 프리팹이 비어 있어도 동작한다 - 그때는 값만 모아 두었다가 흡수 시점에 한 번에 지갑에 넣는다.
/// </summary>
public class RunCoinController : MonoBehaviour
{
    [SerializeField] private RunCoin coinPrefab;
    [Tooltip("나무 한 그루가 뿌리는 코인 오브젝트 수 상한. 골드가 많아도 이 개수로 나눠 담는다.")]
    [SerializeField] private int maxCoinObjectsPerTree = 3;
    [Tooltip("전투 중 바닥 코인의 알파. 흩뿌려지는 동안은 불투명하고, 내려앉은 뒤 이 값으로 옅어진다(시간은 코인 프리팹의 RunCoin).")]
    [SerializeField] private float groundAlpha = 0.2f;
    [SerializeField] private float scatterRadius = 0.8f;
    [Tooltip("웨이브 종료 때 바닥 코인의 알파가 원래대로 돌아오는 시간(초). 다 돌아온 뒤 흡수가 시작된다(기획서 3-3 골드 획득 3단계).")]
    [SerializeField] private float alphaRestoreSeconds = 0.25f;
    [Tooltip("일괄 흡수에서 코인 사이 출발 간격(초). 드르르륵 느낌을 만든다.")]
    [SerializeField] private float absorbStagger = 0.008f;
    [Tooltip("흡수가 이 시간 안에 끝나지 않으면 남은 값을 한 번에 넣고 끝낸다.")]
    [SerializeField] private float absorbTimeout = 4f;
    [SerializeField] private int poolDefaultCapacity = 256;

    [Header("흡수음(본편 상인 NPC 수금음과 같은 규칙: 짧은 간격 제한 + 연달아 들어올수록 음이 높아진다)")]
    [SerializeField, Min(0f)] private float coinGetCooldown = 0.04f;
    [SerializeField, Min(0f)] private float coinPitchResetDelay = 0.2f;
    [SerializeField, Min(0f)] private float coinPitchStep = 0.05f;
    [SerializeField, Min(1f)] private float coinPitchMax = 1.5f;

    private const float CoinPitchBase = 1f;
    private float currentCoinPitch = CoinPitchBase;
    private float lastCoinGetPlayedTime = float.NegativeInfinity;

    private IObjectPool<RunCoin> pool;
    // 알파 복구를 시작할 때 코인마다의 알파(막 떨어져 아직 불투명한 코인도 있다).
    private readonly List<float> restoreFromAlpha = new List<float>(512);
    private readonly List<RunCoin> groundCoins = new List<RunCoin>(512);
    private Action<RunCoin> onCoinArrivedHandler;
    private Action<long> absorbValueCallback;
    private int absorbingCount;

    // DiscardAll이 올린다. 진행 중이던 AbsorbAll이 자기 회차가 끝났음을 알아채고 손을 뗀다.
    private int absorbGeneration;

    // 프리팹 없이 돌 때(또는 풀이 상한에 막혔을 때) 값만 쌓아 두는 곳.
    private long valueWithoutObject;

    public long GroundValue { get; private set; }

    public void Initialize()
    {
        onCoinArrivedHandler = OnCoinArrived;

        if (null != coinPrefab)
        {
            pool = new ObjectPool<RunCoin>(CreateCoin, GetCoin, ReleaseCoin, DestroyCoin, false, poolDefaultCapacity, 2048);
        }
    }

    public void Release()
    {
        DiscardAll();
        pool?.Clear();
    }

    /// <summary>나무가 쓰러진 자리에 _value 골드를 흩뿌린다.</summary>
    public void Spawn(Vector3 _position, long _value)
    {
        if (_value <= 0) return;

        GroundValue += _value;

        if (null == pool)
        {
            valueWithoutObject += _value;
            return;
        }

        int objects = (int)Math.Min(Math.Max(1, maxCoinObjectsPerTree), _value);
        long per = _value / objects;
        long remainder = _value - per * objects;

        for (int i = 0; i < objects; i++)
        {
            long value = per + (i < remainder ? 1 : 0);
            RunCoin coin = pool.Get();
            Vector2 offset = UnityEngine.Random.insideUnitCircle * scatterRadius;
            offset.y *= 0.5f;
            coin.Launch(_position, _position + (Vector3)offset, value, groundAlpha);
            groundCoins.Add(coin);
        }
    }

    /// <summary>
    /// 바닥의 모든 코인을 _target으로 빨아들인다. 코인이 도착할 때마다 _onValueArrived로 값을 넘긴다.
    ///
    /// 도착한 코인도 이 루틴이 끝날 때까지 groundCoins에 남겨 둔다(OnCoinArrived는 값만 넣고 숨긴다).
    /// 출발 루프가 yield하는 사이 먼저 도착한 코인을 목록에서 빼면 인덱스가 당겨져 뒤 코인이 출발하지 못한다.
    /// </summary>
    public IEnumerator AbsorbAll(Transform _target, Action<long> _onValueArrived)
    {
        int generation = absorbGeneration;
        absorbValueCallback = _onValueArrived;

        if (valueWithoutObject > 0)
        {
            long value = valueWithoutObject;
            valueWithoutObject = 0;
            GroundValue -= value;
            _onValueArrived?.Invoke(value);
        }

        absorbingCount = groundCoins.Count;

        // 3단계: 알파를 원래대로 되돌린 뒤 흡수한다.
        if (alphaRestoreSeconds > 0f && groundCoins.Count > 0)
        {
            restoreFromAlpha.Clear();
            for (int i = 0; i < groundCoins.Count; i++) restoreFromAlpha.Add(groundCoins[i].Alpha);

            float restoreElapsed = 0f;
            while (restoreElapsed < alphaRestoreSeconds)
            {
                restoreElapsed += Time.deltaTime;
                float t = Mathf.Clamp01(restoreElapsed / alphaRestoreSeconds);
                for (int i = 0; i < groundCoins.Count && i < restoreFromAlpha.Count; i++) groundCoins[i].SetAlpha(Mathf.Lerp(restoreFromAlpha[i], 1f, t));

                yield return null;
                if (generation != absorbGeneration) yield break;
            }
        }

        for (int i = 0; i < groundCoins.Count; i++)
        {
            groundCoins[i].SetAlpha(1f);
        }

        currentCoinPitch = CoinPitchBase;

        for (int i = 0; i < groundCoins.Count; i++)
        {
            groundCoins[i].BeginAbsorb(_target, onCoinArrivedHandler);

            if (absorbStagger > 0f && (i & 3) == 3)
            {
                yield return new WaitForSeconds(absorbStagger);
                if (generation != absorbGeneration) yield break;
            }
        }

        float elapsed = 0f;
        while (absorbingCount > 0 && elapsed < absorbTimeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
            if (generation != absorbGeneration) yield break;
        }

        // 시간 안에 못 들어온 코인(값이 남아 있는 코인)은 값만 넣는다. 도착한 코인과 함께 전부 풀로 돌린다.
        for (int i = 0; i < groundCoins.Count; i++)
        {
            RunCoin coin = groundCoins[i];
            if (coin.Value > 0)
            {
                GroundValue -= coin.Value;
                absorbValueCallback?.Invoke(coin.Value);
            }

            pool.Release(coin);
        }

        groundCoins.Clear();
        absorbingCount = 0;
        absorbValueCallback = null;
    }

    /// <summary>바닥 코인을 값 없이 전부 치운다(게임오버·포기). 진행 중인 AbsorbAll도 여기서 끝난다.</summary>
    public void DiscardAll()
    {
        absorbGeneration++;

        for (int i = groundCoins.Count - 1; i >= 0; i--)
        {
            pool?.Release(groundCoins[i]);
        }

        groundCoins.Clear();
        GroundValue = 0;
        valueWithoutObject = 0;
        absorbingCount = 0;
        absorbValueCallback = null;
    }

    private void OnCoinArrived(RunCoin _coin)
    {
        long value = _coin.Value;
        if (value <= 0) return;

        GroundValue -= value;
        absorbingCount--;
        absorbValueCallback?.Invoke(value);
        TryPlayCoinGetSound(_coin.transform.position);

        // 목록에서는 빼지 않는다(AbsorbAll 끝에 한꺼번에 풀로 돌린다). 값을 비워 두 번 세지 않게 하고 숨긴다.
        _coin.ResetCoin();
        _coin.gameObject.SetActive(false);
    }

    /// <summary>본편 상인 NPC 수금음(ShopNPC.TryPlayCoinGetSound)과 같다: 짧은 간격으로 겹치지 않게 막고, 연달아 들어올수록 음을 높인다.</summary>
    private void TryPlayCoinGetSound(Vector3 _position)
    {
        float currentTime = Time.time;
        if (coinPitchResetDelay <= currentTime - lastCoinGetPlayedTime) currentCoinPitch = CoinPitchBase;
        if (coinGetCooldown > currentTime - lastCoinGetPlayedTime) return;

        Sound.Play(SoundID.CoinGet, _position, 1f, true, currentCoinPitch);
        lastCoinGetPlayedTime = currentTime;
        currentCoinPitch = Mathf.Min(Mathf.Max(CoinPitchBase, coinPitchMax), currentCoinPitch + coinPitchStep);
    }

    private RunCoin CreateCoin()
    {
        RunCoin coin = Instantiate(coinPrefab, transform);
        coin.gameObject.SetActive(false);
        return coin;
    }

    private static void GetCoin(RunCoin _coin)
    {
        _coin.gameObject.SetActive(true);
    }

    private static void ReleaseCoin(RunCoin _coin)
    {
        _coin.ResetCoin();
        _coin.gameObject.SetActive(false);
    }

    private static void DestroyCoin(RunCoin _coin)
    {
        if (null != _coin) Destroy(_coin.gameObject);
    }
}
