using System;

/// <summary>
/// 캐릭터의 과열 수치(4스테이지 불씨)를 UI 같은 외부에 알리는 읽기 전용 창구.
/// </summary>
public interface IOverheatComponent
{
    // 과열 수치가 1 이상인지(이펙트 1단계 이상).
    public bool IsActive { get; }
    // HUD에 보이는 과열 수치(내림한 정수, 0~최대치). 모든 공식이 이 값을 쓴다.
    public int OverheatLevel { get; }
    // 이펙트 단계. 0 = 없음, 1 = 1~33(예열), 2 = 34~66(가열), 3 = 67 이상(푸른 불꽃, 최대치와 무관한 절대값).
    public int Stage { get; }
    public bool IsMax { get; }
    // 과열 수치 최대치(스탯 overheatMaxValue, 기본 100, 특성이 N씩 올린다). 게이지 채움 비율에 쓴다.
    public float MaxValue { get; }
    // 지금 과열 수치로 붙는 배율. 피해는 화상을 뺀 모든 공격, 공격속도는 도끼에만 곱한다.
    public float DamageMultiplier { get; }
    public float AxeAttackSpeedMultiplier { get; }

    // true = 과열 수치가 0에서 1 이상이 됨, false = 0으로 돌아감.
    public event Action<bool> OverheatStateChangedEvent;
    // 과열 수치(내림한 정수)가 바뀔 때마다. 인자는 새 값.
    public event Action<int> OverheatLevelChangedEvent;
    // 이펙트 단계가 바뀔 때마다. 인자는 새 단계(0~3).
    public event Action<int> OverheatStageChangedEvent;
    // 최대치가 바뀔 때마다. 인자는 새 최대치. 내려가서 수치가 넘쳤다면 잘라낸 뒤(OverheatLevelChangedEvent 먼저) 불린다.
    public event Action<float> OverheatMaxChangedEvent;
}
