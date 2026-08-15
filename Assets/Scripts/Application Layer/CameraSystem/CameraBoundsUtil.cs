using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 기준 해상도의 절반 폭(halfWidth)을 기준으로, 아이소메트릭 2D 시점에 맞는 타원형 사거리를
/// 계산하는 정적 유틸리티. (부메랑 사거리 계산 등에 사용)
/// </summary>
public static class CameraBoundsUtil
{
    // 이 프로젝트의 아이소메트릭 연출(ShockWave의 GetIsometricDistSq 등)은 세로(Y) 이동이
    // 가로(X) 이동보다 화면에서 절반만큼만 보이는 2:1 비율을 쓴다. 그래서 타원의 단축(Y)도
    // 장축(X)의 절반으로 둔다.
    private const float IsometricAxisRatio = 0.5f;

    // PixelPerfectCamera를 찾지 못했을 때만 쓰는 대비용 값. (프리팹 설정과 동일)
    private const float FallbackAssetsPPU = 32f;

    /// <summary>
    /// _direction 방향으로, 던진 위치를 중심으로 한 타원 경계까지의 거리를 반환한다.
    /// 장축(가로)은 기준 해상도 절반 폭의 _majorAxisRatio 배이고, 단축(세로)은 아이소메트릭
    /// 2:1 비율에 따라 장축의 절반이다. _edgePadding만큼 장축을 줄여 안쪽으로 여유를 둔다.
    /// 카메라를 찾을 수 없거나 원근 카메라면 0을 반환한다.
    /// </summary>
    public static float GetMaxDistanceToEdge(Vector3 _direction, float _edgePadding = 0f, float _majorAxisRatio = 1f)
    {
        Camera cam = CameraFinder.Instance != null ? CameraFinder.Instance.PPMainCamera : null;
        if (cam == null || !cam.orthographic) return 0f;

        float halfWidth = GetReferenceHalfWidth(cam);

        float semiMajor = Mathf.Max(halfWidth * _majorAxisRatio - _edgePadding, 0f);
        float semiMinor = semiMajor * IsometricAxisRatio;

        if (semiMajor <= 0f || semiMinor <= 0f) return 0f;

        Vector3 dir = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Vector3.right;

        // 타원 (x/semiMajor)^2 + (y/semiMinor)^2 = 1 에서, 중심(원점)부터 dir 방향으로
        // 경계까지의 거리 r은 r = 1 / sqrt((dir.x/semiMajor)^2 + (dir.y/semiMinor)^2) 로 바로 구해진다.
        float denom = (dir.x * dir.x) / (semiMajor * semiMajor) + (dir.y * dir.y) / (semiMinor * semiMinor);
        if (denom <= 0f) return 0f;

        return 1f / Mathf.Sqrt(denom);
    }

    /// <summary>
    /// 기준 해상도(640 폭) 절반에 해당하는 월드 거리입니다. 화면 해상도·화면비와 무관하게
    /// 항상 같은 값이 나옵니다.
    ///
    /// 예전에는 cam.orthographicSize * cam.aspect로 "지금 실제로 보이는" 절반 폭을 썼습니다.
    /// 그런데 orthographicSize는 화면 세로만 따라가고 가로 시야는 aspect에 비례해 늘어나므로,
    /// 화면비가 넓을수록 사거리가 그대로 길어졌습니다. (21:9는 +34%, 32:9는 +100%)
    /// 모니터가 곧 스펙이 되는 구조라 기준 해상도로 고정합니다.
    ///
    /// 창모드 프리셋 8종은 모두 보이는 가로가 정확히 640이라 이 변경으로 값이 달라지지 않습니다.
    /// 달라지는 것은 전체화면에서 16:9·16:10이 아닌 모니터를 쓸 때뿐입니다.
    /// </summary>
    private static float GetReferenceHalfWidth(Camera _cam)
    {
        float _ppu = FallbackAssetsPPU;

        // PPU는 카메라에서 직접 읽는다. 상수로 또 박아두면 프리팹 설정과 조용히 어긋난다.
        if (_cam.TryGetComponent(out PixelPerfectCamera _pixelPerfectCamera)
            && _pixelPerfectCamera.assetsPPU > 0)
        {
            _ppu = _pixelPerfectCamera.assetsPPU;
        }

        return (SettingsData.PIXEL_PERFECT_REF_WIDTH * 0.5f) / _ppu;
    }
}
