using UnityEngine;

public interface ITreeObj
{
    public IHealthComponent health { get; }
    public Transform GetTransform();

    public bool bDead { get; }

    // 여러 NPC가 같은 나무를 동시에 타겟팅하지 못하도록 하는 예약 플래그
    public bool bReserved { get; set; }
}
