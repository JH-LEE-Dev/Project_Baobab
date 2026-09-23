/// <summary>
/// 퍼센트 증가량의 합을 double로 모아 두었다가 최종 값(시작값 + 합/100)을 만들어 주는 누산기.
/// 배율뿐 아니라 확률이나 관통력처럼 퍼센트로 누적되는 스탯에도 똑같이 쓴다.
///
/// 값에 직접 누적하면(value += _amount / 100f) 총합이 같아도 어떤 단위로 몇 번에 나눠 올렸느냐에
/// 따라 최종 값이 갈린다. 도끼 데미지 배율을 +20%로 세 번 올리면 1.6000001이 되고 +30%로 두 번
/// 올리면 1.5999999가 되는 식이다. 인게임 표기는 양쪽 다 1.6인데, 체력 8인 나무가 한쪽에서는
/// 5대에 베이고 다른 쪽에서는 6대에 베여서 같은 빌드인데 재현이 안 되는 버그가 됐다.
///
/// 그래서 증가량의 합만 double로 들고 있다가 값은 그 합에서 매번 새로 만든다. 순서와 분할에
/// 상관없이 같은 합은 항상 같은 값이 되고, 과열 버프나 스킬 Undo처럼 넣었다 빼는 경우
/// (+x 뒤 -x)에도 오차 없이 정확히 원래 값으로 돌아온다.
///
/// 쓰는 쪽은 기존 += 한 줄을 이렇게 바꾸기만 하면 된다.
/// <code>
/// private PercentAccumulator dropAccum;   // 값 필드 옆에 하나 둔다
///
/// dropMultiplier = dropAccum.Add(dropMultiplier, _amount);
/// </code>
///
/// 주의: 누산기가 시작값을 기억하는 방식이라, 값을 Increase 계열 함수 밖에서 직접 대입하는
/// 필드에는 쓰면 안 된다. 그런 대입은 다음 Add에서 덮여 사라진다.
/// </summary>
public struct PercentAccumulator
{
    private double percentTotal;
    private float baseValue;
    private bool bBaseCaptured;

    /// <summary>
    /// 증가량(%)을 누적하고 새 값을 돌려준다.
    /// _currentValue는 아직 한 번도 올라가지 않은 상태의 값(= 필드 선언이나 인스펙터에서 잡아둔
    /// 시작값)을 기억해 두기 위한 것으로 첫 호출에서만 쓰인다. 시작값이 1이 아닌 스탯(치명타 배율 2,
    /// 치명타 확률 0, 수리량 0.25 등)도 기존과 똑같이 "시작값 + 증가량 합"으로 동작한다.
    /// </summary>
    public float Add(float _currentValue, float _amount)
    {
        if (false == bBaseCaptured)
        {
            baseValue = _currentValue;
            bBaseCaptured = true;
        }

        percentTotal += _amount;
        return (float)(baseValue + (percentTotal / 100.0));
    }
}
