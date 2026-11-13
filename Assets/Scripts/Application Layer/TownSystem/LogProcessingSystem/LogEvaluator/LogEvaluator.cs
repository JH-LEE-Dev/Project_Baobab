using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogEvaluator : MonoBehaviour, ILogEvaluatorCH
{
    public event Action<long> logEvaluatedEvent;

    /// <summary>
    /// 특등품 평가(가격 2배)가 터졌을 때. 인자는 (2배 전 가격, 최종 가격). 돈 처리(logEvaluatedEvent) 직후에 온다.
    /// 연출 전용 통로라 맵(마을/던전)을 가리지 않고 항상 보낸다 - 보여줄지는 받는 쪽이 정한다.
    /// </summary>
    public event Action<long, long> TopgradeAssessedEvent;

    // 미리 굴린 판정마다 붙이는 번호(LogItem.TopgradeTicket). 0은 "번호 없음"이라 1부터 쓴다.
    private static int topgradeTicketSeq = 0;

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
    /// </summary>
    public void PreRollTopgrade(LogItem _item)
    {
        if (_item == null) return;

        bool bHit = UnityEngine.Random.value < topgradeAssessmentChance;
        if (bHit)
        {
            ++topgradeTicketSeq;
            if (topgradeTicketSeq <= 0) topgradeTicketSeq = 1; // int가 한 바퀴 도는 경우에도 0(번호 없음)은 피한다
            _item.SetTopgradePreRoll(ETopgradePreRoll.Hit, topgradeTicketSeq);
        }
        else
        {
            _item.SetTopgradePreRoll(ETopgradePreRoll.Miss, 0);
        }
    }

    public void EvaluateLog(ILogItemData _itemData, ETopgradePreRoll _preRoll = ETopgradePreRoll.None)
    {
        Sound.Play(SoundID.ConvayerComplete, transform.position, GetSoundVolume());
        Sound.Play(SoundID.ConvayerPrize, transform.position, GetSoundVolume());

        LogItemValueData valueData = logItemValueDataBase.Get(_itemData.treeType);
        if (valueData == null)
        {
            Debug.LogError($"LogEvaluator: Value data for {_itemData.treeType} not found.");
            return;
        }

        float baseValue = valueData.value;

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
        long finalPrice = (long)System.Math.Round((double)baseValue * stateMultiplier * logValueMultiplier);

        // 확률적으로 최종 가격 2배 책정. 커터에서 미리 굴린 원목은 그 결과를 쓰고,
        // 미리 굴리지 않은 원목(세이브에서 복원된 벨트 원목 등)은 예전처럼 여기서 굴린다.
        bool bTopgrade;
        switch (_preRoll)
        {
            case ETopgradePreRoll.Hit: bTopgrade = true; break;
            case ETopgradePreRoll.Miss: bTopgrade = false; break;
            default: bTopgrade = UnityEngine.Random.value < topgradeAssessmentChance; break;
        }

        long basePrice = finalPrice;
        if (bTopgrade)
        {
            finalPrice *= 2;
        }

        logEvaluatedEvent?.Invoke(finalPrice);

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
