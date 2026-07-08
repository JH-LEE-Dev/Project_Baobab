using UnityEngine;

/// <summary>
/// 드론을 생성/회수하는 쪽의 공개 API. DroneCreator가 구현하며,
/// 호출자(Character 등)는 구체 클래스 대신 이 인터페이스로만 참조한다.
/// </summary>
public interface IDroneCreator
{
    Drone SpawnDrone(Vector3 _position, Transform _followTarget);
    void DespawnDrone(Drone _drone);
}
