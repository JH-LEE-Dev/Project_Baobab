using UnityEngine;

/// <summary>
/// 럼버잭 NPC들이 공용으로 사용하는 전투/이동 스탯 컴포넌트.
/// </summary>
public class LumberjackStatComponent : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;

    [Header("Attack")]
    public float attackDamage = 10f;

    // 한 번의 공격(도끼질) 사이의 간격(초). 값이 작을수록 공격 속도가 빠르다.
    public float attackInterval = 1.0f;

    [Header("Skill")]
    // true면 캐릭터의 StatComponent에 정의된 셰이크웨이브 스탯을 그대로 사용해 셰이크웨이브를 쓸 수 있다.
    public bool bCanUseShockWave = false;
    // true면 캐릭터의 StatComponent에 정의된 부메랑 스탯(데미지/범위/사정거리/쿨타임/치명타)을 그대로 사용해
    // 캐릭터와 동일한 방식으로 부메랑을 쓸 수 있다.
    public bool bCanUseBoomerang = false;

    // 원래의 기본 공격 주기를 기억해두기 위한 변수
    private float baseAttackInterval;
    // 원래의 기본 공격력을 기억해두기 위한 변수
    private float baseAttackDamage;
    // 원래의 기본 이동 속도를 기억해두기 위한 변수
    private float baseMoveSpeed;
    
    // 누적된 공격 속도 증가량 (퍼센트 단위)
    private float totalAttackSpeedBonus = 0f;
    // 누적된 공격력 증가량 (퍼센트 단위)
    private float totalDamageBonus = 0f;
    // 누적된 이동 속도 증가량 (퍼센트 단위)
    private float totalMoveSpeedBonus = 0f;

    private void Awake()
    {
        // 게임 시작 시 인스펙터에 설정된 값을 기본값으로 저장
        baseAttackInterval = attackInterval;
        baseAttackDamage = attackDamage;
        baseMoveSpeed = moveSpeed;
    }

    public void IncreaseAttackSpeed(float _amount)
    {
        // 공속 증가 수치를 합산하여 누적합니다. (예: 5퍼센트, 10퍼센트 연속 적용 시 15퍼센트)
        totalAttackSpeedBonus += _amount;
        
        // 원래의 기본 공격 주기를 기준으로, 누적된 퍼센트만큼 감소시킨 값을 계산합니다.
        float newInterval = baseAttackInterval * (1f - (totalAttackSpeedBonus / 100f));
        
        // 계산된 공격 주기가 0 이하로 떨어지는 것을 방지 (최소 0.05초)
        attackInterval = Mathf.Max(newInterval, 0.05f);
    }

    public void IncreaseDamage(float _amount)
    {
        // 공격력 증가 수치를 합산하여 누적합니다. (예: 5, 10 연속 적용 시 총 15퍼센트 증가)
        totalDamageBonus += _amount;
        
        // 원래의 기본 공격력을 기준으로 누적된 퍼센트만큼 증가시킨 값을 계산합니다.
        attackDamage = baseAttackDamage * (1f + (totalDamageBonus / 100f));
    }

    public void IncreaseSpeed(float _amount)
    {
        // 이동 속도 증가 수치를 합산하여 누적합니다.
        totalMoveSpeedBonus += _amount;
        
        // 원래의 기본 이동 속도를 기준으로 누적된 퍼센트만큼 증가시킨 값을 계산합니다.
        moveSpeed = baseMoveSpeed * (1f + (totalMoveSpeedBonus / 100f));
    }

    public void SetShockWaveEnabled(bool _boolean)
    {
        bCanUseShockWave = _boolean;
    }

    public void SetBoomerangEnabled(bool _boolean)
    {
        bCanUseBoomerang = _boolean;
    }

}
