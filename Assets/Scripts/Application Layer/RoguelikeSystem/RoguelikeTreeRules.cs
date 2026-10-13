/// <summary>로그라이크 세션의 나무 규칙. 웨이브가 바뀔 때 HP 배율만 갱신한다.</summary>
public sealed class RoguelikeTreeRules : IRunTreeRules
{
    public bool bForceNormalGrade => true;
    public bool bDisableStageGimmicks => true;
    public bool bSuppressItemDrops => true;

    public float TreeHpMultiplier { get; private set; } = 1f;

    /// <summary>웨이브 시작 전에 부른다. 나무 스탯은 풀에서 꺼낼 때 정해지므로 스폰 전에 바꿔야 한다.</summary>
    public void SetTreeHpMultiplier(float _multiplier)
    {
        TreeHpMultiplier = _multiplier > 0f ? _multiplier : 1f;
    }
}
