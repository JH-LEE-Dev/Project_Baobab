using UnityEngine;

/// <summary>
/// 오프로드 포터 NPC들이 공용으로 사용하는 이동/인벤토리 스탯 컴포넌트.
/// </summary>
public class OffroadPorterStatComponent : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 3f;

    [Header("Inventory")]
    public int slotCapacity = 1;

    [Header("Jackpot")]
    // 납품 시 로그 하나가 한 단계 높은 LogState로 승급될 확률 (퍼센트, 0~100)
    public float jackpotChance = 0f;

    // 원래의 기본 이동 속도를 기억해두기 위한 변수
    private float baseMoveSpeed;
    // 원래의 기본 슬롯 용량을 기억해두기 위한 변수
    private int baseSlotCapacity;

    // 누적된 이동 속도 증가량 (퍼센트 단위)
    private float totalMoveSpeedBonus = 0f;
    // 누적된 슬롯 용량 증가량 (개수 단위)
    private int totalSlotCapacityBonus = 0;

    private void Awake()
    {
        // 게임 시작 시 인스펙터에 설정된 값을 기본값으로 저장
        baseMoveSpeed = moveSpeed;
        baseSlotCapacity = slotCapacity;
    }

    public void IncreaseSpeed(float _amount)
    {
        // 이동 속도 증가 수치를 합산하여 누적합니다.
        totalMoveSpeedBonus += _amount;

        // 원래의 기본 이동 속도를 기준으로 누적된 퍼센트만큼 증가시킨 값을 계산합니다.
        moveSpeed = baseMoveSpeed * (1f + (totalMoveSpeedBonus / 100f));
    }

    public void IncreaseSlotCapacity(int _amount)
    {
        // 슬롯 용량 증가 수치를 합산하여 누적합니다.
        totalSlotCapacityBonus += _amount;

        slotCapacity = baseSlotCapacity + totalSlotCapacityBonus;
    }

    public void IncreaseJackpotChance(float _amount)
    {
        // IncreaseSpeed와 동일하게, 스킬 레벨이 오를 때마다 전달되는 수치를 그대로 누적합니다.
        // 확률 자체가 값이므로(기준값에 곱하는 방식이 아님) 0~100 범위로만 클램프합니다.
        jackpotChance = Mathf.Clamp(jackpotChance + _amount, 0f, 100f);
    }
}
