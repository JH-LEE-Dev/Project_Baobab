using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogEvaluator : MonoBehaviour, ILogEvaluatorCH
{
    public event Action<int> logEvaluatedEvent;

    [SerializeField] private LogItemValueDataBase logItemValueDataBase;
    [SerializeField] private GameObject storageObj;
    [SerializeField] private float evaluationDelay = 1.5f;
    [SerializeField] private LogStorage logStorage;
    [SerializeField] private List<LogItemStateValueData> logItemStateValueDatas;

    private readonly int startHash = Animator.StringToHash("bStart");

    private float logValueMultiplier = 1.0f;

    private CustomSortable customSortable;


    public void Initialize()
    {
        logStorage.Initialize();
    }

    public void EvaluateLog(ILogItemData _itemData)
    {
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
        int finalPrice = Mathf.RoundToInt(baseValue * stateData.valueMultiplier * logValueMultiplier);
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

    public EvaluatorSaveData GetSaveData()
    {
        return new EvaluatorSaveData { logValueMultiplier = logValueMultiplier };
    }

    public void LoadSaveData(EvaluatorSaveData _data)
    {
        logValueMultiplier = _data.logValueMultiplier;
        Debug.Log("[LogEvaluator] Evaluator Save Data Loaded.");
    }
}
