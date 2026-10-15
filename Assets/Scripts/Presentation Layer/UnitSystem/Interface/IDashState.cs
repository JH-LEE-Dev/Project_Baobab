using System;

/// <summary>
/// HUD 등 바깥에서 읽는 대시 충전 상태입니다. 읽기 전용이며, 값은 Character가 갱신합니다(DashCharge 참고).
///
/// 표시 방법:
///  - 칸 수(아이콘 개수)는 MaxCharges, 그중 찬 칸은 CurrentCharges입니다.
///  - 다음 칸이 차오르는 정도는 RechargeProgress(0~1)입니다. 매 프레임 바뀌므로 Update에서 읽으세요.
///    전부 찼으면 1입니다.
///  - DashStateChangedEvent는 해금 여부·찬 칸 수·최대 칸 수가 바뀔 때만 옵니다(진행도 변화로는 오지 않음).
///    칸이 차거나 쓰였을 때의 연출 계기로 쓰면 됩니다.
///  - bUnlocked가 false면(전리품 미보유, 마을, 로그라이크) 대시를 쓸 수 없으니 숨기면 됩니다.
/// </summary>
public interface IDashState
{
    event Action DashStateChangedEvent;

    bool bUnlocked { get; }
    int CurrentCharges { get; }
    int MaxCharges { get; }
    float RechargeProgress { get; }
}
