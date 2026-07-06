/// <summary>
/// 캐릭터의 StatComponent 중, NPC(럼버잭 등)가 캐릭터와 동일한 스탯을 그대로 가져다 쓸 때 필요한
/// 값만 읽기 전용으로 노출하는 인터페이스. NPC 쪽 코드가 무거운 StatComponent 실체를 몰라도 되게 한다.
/// </summary>
public interface ICharacterStatForNPC
{
    public float shockWaveChance { get; }
    public float shockWaveDamage { get; }
    public float shockWaveSpeed { get; }
    public float shockWaveDuration { get; }
    public float shockWaveCreateDelay { get; }
    public bool bShockWaveMastery { get; }
    public bool bShockWaveCritical { get; }
    public bool bShockWaveEnforcement { get; }
    public float criticalChance { get; }
    public float ciriticalDamageMul { get; }
}
