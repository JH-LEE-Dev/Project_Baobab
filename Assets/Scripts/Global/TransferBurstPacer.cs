using UnityEngine;

/// <summary>
/// 원목 넣기·빼기 발사를 "묶음"으로 나누는 계산기. 운반 상자(OffroadContainer)와 원목 보관함(LogContainer)이 쓴다.
///
/// 아이템 전송 속도 특성을 올리면 발사 간격이 0.075초 / 배율로 계속 줄어서, 배율이 높으면 원목이 한 줄로 뭉개지고
/// 효과음이 기관총처럼 들린다. 그래서 간격은 배율 MaxVisualSpeedMul(200%)에서 더 줄이지 않고, 그 위의 속도는
/// 한 번에 여러 개를 같이 내보내는 것으로 채운다. 한 묶음의 평균 개수는 배율 / MaxVisualSpeedMul이고 남는 소수는
/// 다음 묶음으로 넘긴다(300% -> 2,1,2,1 / 340% -> 2,1,2,2,1… / 900% -> 5,4,5,4). 그래서 1초에 옮기는 개수는
/// 묶음 없이 쏘던 때와 같다. 배율이 MaxVisualSpeedMul 이하면 묶음은 항상 1개이고 간격 식도 예전과 같은 값이다.
///
/// 한 묶음은 같은 프레임에 같은 자리에서 나가므로, 착지 효과음·진동·흔들림은 묶음의 첫 원목만 내고(나머지는
/// 조용히 착지), 비행 무작위 값은 범위를 묶음 개수만큼 나눠 칸마다 하나씩 뽑아(Range) 서로 겹쳐 보이지 않게 한다.
/// </summary>
public struct TransferBurstPacer
{
    /// <summary>발사 간격이 더 줄지 않는 배율(2 = 200%). 이보다 빠른 속도는 묶음 개수로 낸다.</summary>
    public const float MaxVisualSpeedMul = 2f;

    // 지금까지 내보낸 개수와 내보냈어야 할 개수의 차이. 반올림으로 정하므로 -0.5 이상 0.5 미만에 머문다.
    private double carry;

    /// <summary>
    /// 묶음 사이 발사 간격. 배율이 MaxVisualSpeedMul 이하면 예전 식(_baseInterval / Max(0.01, 배율))과 같은 값이다.
    /// </summary>
    public static float GetInterval(float _baseInterval, float _speedMul)
    {
        return _baseInterval / Mathf.Min(Mathf.Max(0.01f, _speedMul), MaxVisualSpeedMul);
    }

    /// <summary>
    /// 이번 묶음에 내보낼 개수(1 이상). 배율이 MaxVisualSpeedMul 이하면 항상 1이다.
    /// </summary>
    public int NextCount(float _speedMul)
    {
        if (_speedMul <= MaxVisualSpeedMul)
        {
            carry = 0d;
            return 1;
        }

        // 반올림해서 첫 묶음부터 넉넉히 내보낸다(300%면 2,1,2,1). 배율 비가 1보다 크고 carry가 -0.5 이상이라 1 이상이다.
        double total = carry + (double)_speedMul / MaxVisualSpeedMul;
        int count = (int)System.Math.Floor(total + 0.5 + 1e-9);
        carry = total - count;
        return count;
    }

    /// <summary>
    /// _itemCount개를 이 계산기로 나눠 보내면 묶음이 몇 번 나가는지. 연출 길이를 미리 알아야 할 때 쓴다.
    /// </summary>
    public static int CountBursts(int _itemCount, float _speedMul)
    {
        TransferBurstPacer pacer = default;
        int bursts = 0;
        int sent = 0;
        while (sent < _itemCount)
        {
            sent += pacer.NextCount(_speedMul);
            bursts++;
        }
        return bursts;
    }

    /// <summary>
    /// 묶음 안 _index번째 원목의 무작위 값. 범위를 _count칸으로 나눠 _index번째 칸 안에서 뽑아, 같은 묶음의 원목들이
    /// 같은 궤적으로 겹치지 않게 한다. 묶음이 1개면 UnityEngine.Random.Range(_min, _max)를 그대로 부른다(예전과 같은 값).
    /// </summary>
    public static float Range(float _min, float _max, int _index, int _count)
    {
        if (_count <= 1)
        {
            return UnityEngine.Random.Range(_min, _max);
        }

        return Mathf.Lerp(_min, _max, (_index + UnityEngine.Random.value) / _count);
    }
}
