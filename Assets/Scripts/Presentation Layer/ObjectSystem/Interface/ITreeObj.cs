using UnityEngine;

public interface ITreeObj
{
    public IHealthComponent health { get; }
    public Transform GetTransform();

    public bool bDead { get; }

    // 현재 보석 단계. 0 = 일반, 1 = 황금, 2 = 다이아, 3 = 무지개.
    // 나무 등급(TreeGrade)이 허용하는 최대 단계까지 체력이 0이 될 때마다 한 단계씩 올라간다.
    public int gemStage { get; }

    // 지금 보석 상태인지(= gemStage > 0). 분기용 단축 프로퍼티.
    public bool bIsGemStage { get; }

    // 여러 NPC가 같은 나무를 동시에 타겟팅하지 못하도록 하는 예약 플래그
    public bool bReserved { get; set; }
}
