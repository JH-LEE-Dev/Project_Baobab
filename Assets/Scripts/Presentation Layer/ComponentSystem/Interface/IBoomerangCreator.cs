using System;
using UnityEngine;

/// <summary>
/// 부메랑을 생성/발사하는 쪽의 공개 API. BoomerangCreator가 구현하며,
/// 호출자(Character 등)는 구체 클래스 대신 이 인터페이스로만 참조한다.
/// </summary>
public interface IBoomerangCreator
{
    // _bPlayHaptic: 플레이어(캐릭터)가 던진 부메랑만 true. NPC 부메랑이 진동을 울리면 가만히 있어도 패드가 떤다.
    Boomerang ThrowBoomerang(Vector3 _origin, Vector3 _direction, float _maxDistance, Transform _returnTarget, Action _onFinished, bool _bIsOverheat = false, bool _bPlayHaptic = false);
    // 완료 콜백으로 부메랑 자신을 넘기는 오버로드(발사마다 클로저를 만들지 않기 위한 경로).
    Boomerang ThrowBoomerang(Vector3 _origin, Vector3 _direction, float _maxDistance, Transform _returnTarget, Action<Boomerang> _onFinished, bool _bIsOverheat = false, bool _bPlayHaptic = false);
}
