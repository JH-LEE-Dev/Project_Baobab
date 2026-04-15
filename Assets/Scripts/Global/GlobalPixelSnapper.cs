using UnityEngine;

/// <summary>
/// 프로젝트 전역에서 사용하는 픽셀 스냅 유틸리티 클래스입니다.
/// </summary>
public static class GlobalPixelSnapper
{
    public const float PixelsPerUnit = 32f;

    /// <summary>
    /// Vector2 좌표를 32 PPU 그리드에 맞춰 스냅합니다.
    /// </summary>
    public static Vector2 Snap(Vector2 _pos)
    {
        float x = Mathf.Round(_pos.x * PixelsPerUnit) / PixelsPerUnit;
        float y = Mathf.Round(_pos.y * PixelsPerUnit) / PixelsPerUnit;
        return new Vector2(x, y);
    }

    /// <summary>
    /// Vector3 좌표를 32 PPU 그리드에 맞춰 스냅합니다. (Z축 유지)
    /// </summary>
    public static Vector3 Snap(Vector3 _pos)
    {
        float x = Mathf.Round(_pos.x * PixelsPerUnit) / PixelsPerUnit;
        float y = Mathf.Round(_pos.y * PixelsPerUnit) / PixelsPerUnit;
        return new Vector3(x, y, _pos.z);
    }

    /// <summary>
    /// Rigidbody2D의 위치를 부드럽게 픽셀 그리드로 이동시킵니다. (FixedUpdate에서 사용 권장)
    /// </summary>
    public static void SnapRigidbody(Rigidbody2D _rb, float _deltaTime)
    {
        if (_rb == null) return;

        Vector2 currentPos = _rb.position;
        Vector2 snappedPos = Snap(currentPos);

        if (Vector2.SqrMagnitude(currentPos - snappedPos) > 0.00001f)
        {
            _rb.position = Vector2.MoveTowards(currentPos, snappedPos, _deltaTime);
        }
    }
}
