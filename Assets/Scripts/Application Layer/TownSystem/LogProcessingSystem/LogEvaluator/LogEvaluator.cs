using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogEvaluator : MonoBehaviour, ILogEvaluatorCH
{
    public event Action<long> logEvaluatedEvent;

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

    public void EvaluateLog(ILogItemData _itemData)
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

        // 확률적으로 최종 가격 2배 책정
        if (UnityEngine.Random.value < topgradeAssessmentChance)
        {
            finalPrice *= 2;
        }

        logEvaluatedEvent?.Invoke(finalPrice);

        if (logStorage != null) logStorage.TriggerBounce();
    }

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
