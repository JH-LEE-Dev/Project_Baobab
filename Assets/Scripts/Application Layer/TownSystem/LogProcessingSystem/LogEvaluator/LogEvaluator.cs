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

    private CustomSortable customSortable;
    private float topgradeAssessmentChance = 0f;
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

        // 등급 데이터 검색
        LogItemStateValueData stateData = logItemStateValueDatas.Find(x => x.logState == _itemData.logState);

        // 최종 가격 = 기본 가치 * 가치 배율 * 내구도 배율 * 스킬 배율
        // double로 계산한 뒤 long으로 반올림한다. 예전에는 Mathf.RoundToInt였는데,
        // 본편 기준으로 흑요목(기본가 3천만) x Perfect(x20) = 6억이고 여기에 logValueMultiplier가
        // 최대 x111까지 곱해져 int 범위(약 21억)를 한참 넘는다. float/int로는 그 시점에
        // 값이 정의되지 않은 음수로 튀어 "수금하면 소지금이 마이너스가 되는" 상태가 됐다.
        long finalPrice = (long)System.Math.Round((double)baseValue * stateData.valueMultiplier * logValueMultiplier);

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

    public void IncreaseLogValueMultiplier(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 증가)
        logValueMultiplier += (_amount / 100.0f);
    }

    public void IncreaseTopgradeAssessmentChance(float _amount)
    {
        // _amount는 0보다 큰 퍼센트 (예: 10.0f는 10% 증가)
        topgradeAssessmentChance += (_amount / 100.0f);
    }
}
