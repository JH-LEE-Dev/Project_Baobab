using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogEvaluator : MonoBehaviour, ILogEvaluatorCH
{
    // (최종 가격, 평가한 원목 수). 원목 수는 동시 가공 묶음이면 2 이상이다(상점이 쌓인 원목 수로 금화 수를 정한다).
    public event Action<long, int> logEvaluatedEvent;

    /// <summary>
    /// 특등품 평가(가격 2배)가 터졌을 때. 인자는 (2배 전 가격, 최종 가격). 돈 처리(logEvaluatedEvent) 직후에 온다.
    /// 동시 가공 묶음에서 여러 원목이 당첨돼도 한 번만 온다(가격은 묶음 전체 합).
    /// 연출 전용 통로라 맵(마을/던전)을 가리지 않고 항상 보낸다 - 보여줄지는 받는 쪽이 정한다.
    /// </summary>
    public event Action<long, long> TopgradeAssessedEvent;

    [SerializeField] private LogItemValueDataBase logItemValueDataBase;
    [SerializeField] private GameObject storageObj;
    [SerializeField] private float evaluationDelay = 1.5f;
    [SerializeField] private LogStorage logStorage;
    [SerializeField] private List<LogItemStateValueData> logItemStateValueDatas;

    private readonly int startHash = Animator.StringToHash("bStart");

    private float logValueMultiplier = 1.0f;
    private PercentAccumulator logValueAccum;

    private CustomSortable customSortable;
    private float topgradeAssessmentChance = 0f;
    private PercentAccumulator topgradeAssessmentAccum;
    private MapType mapType;

    // LogCutter.GetSoundVolume()과 동일한 규칙: 마을이 아니면(=던전에 있는 동안 배경에서 계속
    // 평가가 진행되는 상태) 평가 완료음도 재생하지 않는다.
    public void SetMapType(MapType _mapType)
    {
        mapType = _mapType;
    }

    private float GetSoundVolume()
    {
        return mapType == MapType.Town ? 1f : 0f;
    }

    public void Initialize()
    {
        logStorage.Initialize();
    }

    /// <summary>
    /// 커터에서 막 나온 원목의 특등품 평가를 미리 굴려 원목에 적어 둔다. 확률은 평가 순간과 같은
    /// topgradeAssessmentChance 하나이고, 굴리는 시점만 커터 배출로 앞당겨진다(쌍둥이 원목 연출용).
    /// 동시 가공 묶음이면 묶인 원목마다 따로 굴려 당첨 수를 적는다(묶음 1개면 예전처럼 한 번만 굴린다).
    /// 쌍둥이가 하나라도 나오면(당첨이 있거나 묶음이면) 연출 번호를 새로 붙인다.
    /// </summary>
    public void PreRollTopgrade(LogItem _item)
    {
        if (_item == null) return;

        int bundleCount = _item.ProcessBundleCount;
        int hitCount = 0;
        for (int i = 0; i < bundleCount; i++)
        {
            if (UnityEngine.Random.value < topgradeAssessmentChance) ++hitCount;
        }

        ETopgradePreRoll preRoll = hitCount > 0 ? ETopgradePreRoll.Hit : ETopgradePreRoll.Miss;
        bool bHasTwin = hitCount > 0 || bundleCount > 1;
        _item.SetTopgradePreRoll(preRoll, bHasTwin ? LogItem.NextPresentationTicket() : 0, hitCount);
    }

    /// <summary>
    /// 원목을 평가해 돈을 낸다. _bundleCount는 동시 가공으로 묶인 원목 수로, 원목마다 같은 값이 매겨지고 특등품도 원목마다 따로다.
    /// _hitCount는 커터에서 미리 굴린 당첨 수(_preRoll이 Hit일 때만 본다, 0 이하면 1로 친다).
    /// </summary>
    public void EvaluateLog(ILogItemData _itemData, ETopgradePreRoll _preRoll = ETopgradePreRoll.None, int _bundleCount = 1, int _hitCount = 1)
    {
        Sound.Play(SoundID.ConvayerComplete, transform.position, GetSoundVolume());
        Sound.Play(SoundID.ConvayerPrize, transform.position, GetSoundVolume());

        LogItemValueData valueData = logItemValueDataBase.Get(_itemData.treeType);
        if (valueData == null)
        {
            Debug.LogError($"LogEvaluator: Value data for {_itemData.treeType} not found.");
            return;
        }

        long baseValue = valueData.value;

        // 등급 배율은 LogItemValueDataBase가 단일 출처다(교체 시스템의 환율과 같은 표). 이 프리팹의
        // 사본(logItemStateValueDatas)은 DB에 표가 없는 예전 에셋을 위한 폴백으로만 남긴다.
        float stateMultiplier;
        if (logItemValueDataBase.stateValueDatas != null && logItemValueDataBase.stateValueDatas.Count > 0)
        {
            // 표에 없는 등급은 데이터 누락이다. 예전 Find 경로는 기본 구조체(배율 0)로 조용히 0원에
            // 팔았고, 지금 폴백은 1(정가)이다 - 어느 쪽이든 조용히 지나가면 안 되므로 오류로 드러낸다.
            if (!logItemValueDataBase.TryGetStateMultiplier(_itemData.logState, out stateMultiplier))
            {
                Debug.LogError($"LogEvaluator: {_itemData.logState} 등급의 가치 배율이 LogItemValueDataBase.stateValueDatas에 없습니다. 배율 1로 평가합니다.");
            }
        }
        else
        {
            stateMultiplier = logItemStateValueDatas.Find(x => x.logState == _itemData.logState).valueMultiplier;
        }

        // 최종 가격 = 기본 가치 * 가치 배율 * 내구도 배율 * 스킬 배율
        // double로 계산한 뒤 long으로 반올림한다. 예전에는 Mathf.RoundToInt였는데,
        // 본편 기준으로 흑요목(기본가 3천만) x Perfect(x20) = 6억이고 여기에 logValueMultiplier가
        // 최대 x111까지 곱해져 int 범위(약 21억)를 한참 넘는다. float/int로는 그 시점에
        // 값이 정의되지 않은 음수로 튀어 "수금하면 소지금이 마이너스가 되는" 상태가 됐다.
        // 배율은 float라 1.3이 1.29999995로 들어가 큰 금액에서 몇십 원씩 어긋났으므로, 입력값 그대로의
        // 십진수로 되돌려 decimal로 곱한다(MoneyMath).
        long finalPrice = MoneyMath.MultiplyRound(baseValue, stateMultiplier, logValueMultiplier);

        // 확률적으로 최종 가격 2배 책정. 커터에서 미리 굴린 원목은 그 결과를 쓰고,
        // 미리 굴리지 않은 원목(세이브에서 복원된 벨트 원목 등)은 예전처럼 여기서 굴린다.
        // 동시 가공 묶음은 원목마다 따로다: 당첨된 원목만 2배라 최종 가격 = 원목 값 x (묶음 수 + 당첨 수).
        int bundleCount = Mathf.Max(1, _bundleCount);
        int hitCount;
        switch (_preRoll)
        {
            case ETopgradePreRoll.Hit: hitCount = Mathf.Clamp(_hitCount, 1, bundleCount); break;
            case ETopgradePreRoll.Miss: hitCount = 0; break;
            default:
                hitCount = 0;
                for (int i = 0; i < bundleCount; i++)
                {
                    if (UnityEngine.Random.value < topgradeAssessmentChance) ++hitCount;
                }
                break;
        }
        bool bTopgrade = hitCount > 0;

        long unitPrice = finalPrice;
        long basePrice = unitPrice * bundleCount;
        finalPrice = basePrice + unitPrice * hitCount;

        logEvaluatedEvent?.Invoke(finalPrice, bundleCount);

        if (logStorage != null) logStorage.TriggerBounce();

        if (bTopgrade)
        {
            // 평가기의 "쾅"(평소 반동을 덮어쓰는 큰 반동)과 소리·진동·카메라는 받는 쪽(TopgradeAssessmentPresenter)이
            // 맡는다. 평소 반동을 먼저 걸어 두므로, 받는 쪽이 연출하지 않는 경우(던전에 있는 동안)에도 예전과 같다.
            TopgradeAssessedEvent?.Invoke(basePrice, finalPrice);
        }
    }

    /// <summary>평가기 몸체(원목이 빨려 들어가는 기계). 특등품 연출이 여기를 기준으로 움직인다.</summary>
    public LogStorage Storage => logStorage;

    private IEnumerator StopAnimationRoutine()
    {
        yield return new WaitForSeconds(evaluationDelay);
    }

    /// <summary>
    /// "원목 판매 가치" 특성이 쌓아올린 배율(1.0 = 증가 없음).
    /// 용광로 주괴 판매에도 같은 배율이 걸려야 해서 밖에서 읽을 수 있게 열어둔다.
    /// </summary>
    public float LogValueMultiplier => logValueMultiplier;

    public void IncreaseLogValueMultiplier(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 증가)
        logValueMultiplier = logValueAccum.Add(logValueMultiplier, _amount);
    }

    public void IncreaseTopgradeAssessmentChance(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 증가)
        topgradeAssessmentChance = topgradeAssessmentAccum.Add(topgradeAssessmentChance, _amount);
    }
}
