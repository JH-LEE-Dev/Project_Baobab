/// <summary>
/// 증가량의 합을 double로 모아 두었다가 최종 값(시작값 + 합)을 만들어 주는 누산기.
/// 퍼센트가 아니라 원시 값을 그대로 더하는 스탯에 쓴다. (퍼센트로 올라가는 쪽은
/// <see cref="PercentAccumulator"/>)
///
/// 값에 직접 누적하면(value += _amount) 더한 횟수만큼 반올림이 일어나 이상값에서 조금씩 밀린다.
/// 예를 들어 0.3을 열두 번 더하면 의도한 값보다 상대적으로 1.8e-07만큼 모자라는데, 이 배율들은
/// 전부 나무에 들어가는 데미지에 곱해지므로 그 모자람이 그대로 "다 깎았는데 한 대 더 맞아야 하는"
/// 증상이 된다. 스킬을 한 레벨 올릴 때마다 덧셈이 한 번씩 일어나므로, 노드 최대 레벨이 올라가거나
/// 같은 배율을 올리는 노드가 늘어날수록 밀림이 커진다.
///
/// 그래서 증가량의 합만 double로 들고 있다가 값은 그 합에서 매번 새로 만든다. 몇 번에 나눠
/// 올렸는지와 무관하게 같은 합은 항상 같은 값이 되고, 넣었다 빼는 경우(+x 뒤 -x)에도 오차 없이
/// 정확히 원래 값으로 돌아온다.
///
/// 쓰는 쪽은 기존 += 한 줄을 이렇게 바꾸기만 하면 된다.
/// <code>
/// private ValueAccumulator starMarkDamageAccum;   // 값 필드 옆에 하나 둔다
///
/// starMarkDamageMultiplier = starMarkDamageAccum.Add(starMarkDamageMultiplier, _amount);
/// </code>
///
/// 주의: 누산기가 시작값을 기억하는 방식이라, 값을 Increase 계열 함수 밖에서 직접 대입하는
/// 필드에는 쓰면 안 된다. 그런 대입은 다음 Add에서 덮여 사라진다.
/// </summary>
public struct ValueAccumulator
{
    private double amountTotal;
    private float baseValue;
    private bool bBaseCaptured;

    /// <summary>
    /// 증가량을 누적하고 새 값을 돌려준다.
    /// _currentValue는 아직 한 번도 올라가지 않은 상태의 값(= 필드 선언이나 인스펙터에서 잡아둔
    /// 시작값)을 기억해 두기 위한 것으로 첫 호출에서만 쓰인다. 시작값이 1이 아닌 스탯(발현 낙인
    /// 보너스 0 등)도 기존과 똑같이 "시작값 + 증가량 합"으로 동작한다.
    /// </summary>
    public float Add(float _currentValue, float _amount)
    {
        if (false == bBaseCaptured)
        {
            baseValue = _currentValue;
            bBaseCaptured = true;
        }

        amountTotal += _amount;
        return (float)(baseValue + amountTotal);
    }
}
