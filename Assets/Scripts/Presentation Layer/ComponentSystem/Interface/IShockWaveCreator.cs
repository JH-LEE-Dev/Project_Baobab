using UnityEngine;

/// <summary>
/// 셰이크웨이브를 생성/재생하는 쪽의 공개 API. AxeExtraAttackCreator가 구현하며,
/// 캐릭터의 AttackComponent뿐 아니라 럼버잭 NPC 등도 구체 클래스 대신 이 인터페이스로 참조한다.
/// </summary>
public interface IShockWaveCreator
{
    ShockWave CreateShockWave(Vector3 _position);
    void PlayShockWaveVisual(ShockWave _shockWave);
}
