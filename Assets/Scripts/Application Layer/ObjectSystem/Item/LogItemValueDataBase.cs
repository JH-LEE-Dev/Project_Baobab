using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LogItemValueDataBase", menuName = "Game/Log Item Value Database")]
public class LogItemValueDataBase : ScriptableObject
{
    public List<LogItemValueData> datas;

    /// <summary>
    /// 등급(LogState)별 가치 배율. 원목 가치의 <b>단일 출처</b>다.
    ///
    /// 예전엔 LogEvaluator 프리팹마다 같은 표를 따로 들고 있었다. 교체 시스템과 흡입 정렬이 실제
    /// 가치를 봐야 하게 되면서(LogValue), 던전에서 보는 환율과 마을에서 보는 가격이 한 표에서
    /// 나오도록 여기로 모았다. LogEvaluator는 이 목록이 비어 있을 때만 자기 사본을 쓴다.
    /// </summary>
    public List<LogItemStateValueData> stateValueDatas;

    public LogItemValueData Get(TreeType _type)
    {
        return datas.Find(x => x.treeType == _type);
    }

    /// <summary>
    /// 등급 배율. 표에 없는 등급(Destoyed/Damaged처럼 드랍되지 않는 것)은 1로 본다 - 순서를
    /// 매기는 쪽에서 0이 되어 "가치 없음"으로 뭉개지는 것을 막기 위해서다.
    /// </summary>
    public float GetStateMultiplier(LogState _logState)
    {
        if (stateValueDatas == null) return 1f;

        for (int i = 0; i < stateValueDatas.Count; i++)
        {
            if (stateValueDatas[i].logState == _logState) return stateValueDatas[i].valueMultiplier;
        }

        return 1f;
    }
}

[Serializable]
public struct LogItemStateValueData
{
    public LogState logState;
    public float valueMultiplier;
}

[Serializable]
public struct LogItemDurabilityData
{
    public LogState logState;
    public float durabilityMultiplier;
}
