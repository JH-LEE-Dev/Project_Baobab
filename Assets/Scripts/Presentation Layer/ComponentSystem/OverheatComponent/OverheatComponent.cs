using UnityEngine;

public class OverheatComponent : PComponent
{
    private const float MoveSpeedBonus = 20f;
    private const float AttackSpeedBonus = 20f;
    private const float AxeDamageBonus = 20f;

    private float overheatTimer = 0f;
    private bool bActive = false;

    public bool IsActive => bActive;

    // 나무 열기(+15초), 용암 열기(초당 +2초) 등 열기 피해를 받을 때마다 호출된다.
    // "과열" 특성이 없으면 아무 효과가 없고, 지속시간은 상한 없이 계속 합산된다.
    public void AddOverheatDuration(float _seconds)
    {
        if (!ctx.characterStat.bOverheat || _seconds <= 0f) return;

        overheatTimer += _seconds;

        if (!bActive)
        {
            ActivateBuff();
        }
    }

    private void Update()
    {
        if (!bActive) return;

        overheatTimer -= Time.deltaTime;
        if (overheatTimer <= 0f)
        {
            DeactivateBuff();
        }
    }

    private void ActivateBuff()
    {
        bActive = true; // 스탯 적용보다 먼저 세워서, 같은 프레임에 트리거가 겹쳐도 중복 적용되지 않게 한다.
        ctx.characterStat.IncreaseMovementSpeed(MoveSpeedBonus);
        ctx.characterStat.IncreaseAxeAttackSpeed(AttackSpeedBonus);
        ctx.characterStat.IncreaseAxeDamage(AxeDamageBonus);
    }

    private void DeactivateBuff()
    {
        bActive = false;
        overheatTimer = 0f;
        ctx.characterStat.IncreaseMovementSpeed(-MoveSpeedBonus);
        ctx.characterStat.IncreaseAxeAttackSpeed(-AttackSpeedBonus);
        ctx.characterStat.IncreaseAxeDamage(-AxeDamageBonus);
    }
}
