using System;
using UnityEngine;

/// <summary>
/// 부메랑을 생성/발사하는 쪽의 공개 API. BoomerangCreator가 구현하며,
/// 호출자(Character 등)는 구체 클래스 대신 이 인터페이스로만 참조한다.
/// </summary>
public interface IBoomerangCreator
{
    Boomerang ThrowBoomerang(Vector3 _origin, Vector3 _direction, float _maxDistance, Transform _returnTarget, Action _onFinished);
}
