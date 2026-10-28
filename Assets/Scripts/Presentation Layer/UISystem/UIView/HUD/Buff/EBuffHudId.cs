/// <summary>버프 HUD 식별자. HUD_BuffBar의 모든 호출(AddOrUpdate/SetValue/Dispel/Remove)이 이 값을 id로 쓴다.</summary>
public enum EBuffHudId
{
    None = 0,
    AttackRythm = 1,        // 벌목 리듬
    SourceOfSpeed = 2,      // 속도의 원천
    StaminaRecover = 3,     // 휴식
    StarPathSpeedBoost = 4, // 별길 걸음
    OverHeat = 5,           // 과열
    StarRain = 6,           // 별비
}
