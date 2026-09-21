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
    /// 등급 배율. 표에 없는 등급이면 false를 돌려주고 배율은 1로 채운다.
    ///
    /// 표에 없는 등급을 어떻게 볼지는 부르는 쪽이 정한다 - 순서를 매기는 LogValue는 조용히 1로 쓰고
    /// (Destoyed/Damaged는 풀 리셋 값이라 드랍되지 않는다), 실제로 돈을 매기는 LogEvaluator는 false를
    /// 오류로 드러낸다. 예전엔 Find가 기본 구조체(배율 0)를 돌려줘 조용히 0원에 팔렸는데, 둘 다
    /// 데이터 누락을 숨기는 동작이라 어느 쪽도 그대로 두지 않는다.
    /// </summary>
    public bool TryGetStateMultiplier(LogState _logState, out float _multiplier)
    {
        _multiplier = 1f;
        if (stateValueDatas == null) return false;

        for (int i = 0; i < stateValueDatas.Count; i++)
        {
            if (stateValueDatas[i].logState == _logState)
            {
                _multiplier = stateValueDatas[i].valueMultiplier;
                return true;
            }
        }

        return false;
    }

    /// <summary>표에 없는 등급은 1로 보는 편의 버전(순서 매기기용).</summary>
    public float GetStateMultiplier(LogState _logState)
    {
        TryGetStateMultiplier(_logState, out float multiplier);
        return multiplier;
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
