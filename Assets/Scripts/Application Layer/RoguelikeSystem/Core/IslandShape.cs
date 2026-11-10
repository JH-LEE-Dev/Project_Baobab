using UnityEngine;

/// <summary>로그라이크 섬 모양. 반지름 (rx, ry)는 화면(월드) 가로·세로 반폭이다.</summary>
public enum IslandShapeType
{
    /// <summary>화면 기준 완벽한 타원.</summary>
    Ellipse = 0,
    /// <summary>마름모(|x|/rx + |y|/ry ≤ 1). 꼭짓점이 화면 좌우·상하 가운데 축 위에 놓인다.</summary>
    Diamond = 1,
}

/// <summary>섬 모양 판정. 지형 생성 · 나무 필드 · 오프로드 차선이 같은 공식을 쓴다.</summary>
public static class IslandShape
{
    /// <summary>섬 중심에서 (_dx, _dy)만큼 떨어진 점이 섬 안(경계 포함)인지.</summary>
    public static bool Contains(IslandShapeType _shape, float _dx, float _dy, float _radiusX, float _radiusY)
    {
        if (_radiusX <= 0f || _radiusY <= 0f) return false;

        // 칸 격자에 맞춘 마름모는 가장자리 칸 중심이 경계 위(값 1)에 정확히 놓인다. 부동소수 오차로 한쪽만 빠지지 않게 여유를 둔다.
        if (IslandShapeType.Diamond == _shape) return Value(_shape, _dx, _dy, _radiusX, _radiusY) <= 1.0001f;
        return Value(_shape, _dx, _dy, _radiusX, _radiusY) <= 1f;
    }

    /// <summary>정규화한 경계 값. 경계 위가 1, 안쪽이 1 미만이다.</summary>
    public static float Value(IslandShapeType _shape, float _dx, float _dy, float _radiusX, float _radiusY)
    {
        if (IslandShapeType.Diamond == _shape) return Mathf.Abs(_dx) / _radiusX + Mathf.Abs(_dy) / _radiusY;

        // 타원은 예전 판정과 같은 식(역제곱을 곱한다)이라 경계 칸이 바뀌지 않는다.
        float invRx2 = 1f / (_radiusX * _radiusX);
        float invRy2 = 1f / (_radiusY * _radiusY);
        return _dx * _dx * invRx2 + _dy * _dy * invRy2;
    }
}
