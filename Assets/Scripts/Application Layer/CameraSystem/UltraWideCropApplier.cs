using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 화면에 보이는 가로 폭이 기준 해상도(640)를 넘을 때 이 카메라의 Crop Frame을 Pillarbox로 켜서
/// 좌우를 잘라냅니다. 그 결과 어떤 해상도에서도 가로 시야가 정확히 640으로 고정됩니다.
///
/// 판정 기준이 "화면비"가 아니라 "보이는 가로 폭"인 이유:
/// 배율은 round(화면세로 / 360)이라 세로만 보고 정해지는데, 그 배율로 화면 가로를 나눈 값이
/// 실제로 보이는 폭입니다. 16:9 모니터여도 반올림 때문에 640을 넘을 수 있습니다.
/// (1600x900은 900/360=2.5가 2로 반올림되어 보이는 폭이 800, 1366x768은 683)
/// 화면비로 판정하면 이런 경우를 놓쳐서 UI 배경(640 폭)이 좌우를 못 덮습니다.
///
/// 세로는 자르지 않습니다. Pillarbox는 가로만 처리하고, 세로 시야는 화면에 따라
/// 350~450으로 가변입니다. UI 아트가 세로 450~500으로 넉넉해서 그 범위를 덮습니다.
/// 즉 이 프로젝트의 규칙은 "가로는 항상 640, 세로는 가변"입니다.
///
/// 주력 해상도(1920x1080, 2560x1440, 3840x2160, 16:10 프리셋 전부)는 보이는 폭이 정확히
/// 640이라 조건에 걸리지 않고 띠도 생기지 않습니다. 띠가 생기는 것은 1366x768(6%),
/// 1440x900(11%), 1600x900(20%) 같은 구형 해상도와 울트라와이드뿐입니다.
///
/// 이 카메라 하나만 다루므로 씬마다 카메라가 새로 생성·파괴되어도 안전합니다.
/// 정적 상태나 싱글톤 참조가 없어 이전 씬의 값이 남지 않습니다.
///
/// 월드 카메라와 UI 카메라 양쪽에 붙여야 합니다. 한쪽만 켜지면 UI가 검은 띠 위에 얹힙니다.
/// </summary>
[RequireComponent(typeof(PixelPerfectCamera))]
public class UltraWideCropApplier : MonoBehaviour
{
    /// <summary>
    /// 보이는 폭이 이 값을 넘을 때만 자릅니다. 부동소수 오차로 640.0000x가 걸리지 않도록
    /// 여유를 조금 둡니다.
    /// </summary>
    private const float WIDTH_EPSILON = 0.5f;

    private PixelPerfectCamera pixelPerfectCamera;

    // 마지막으로 반영한 화면 크기. 변화가 없으면 아무 일도 하지 않기 위한 것이다.
    private int appliedWidth;
    private int appliedHeight;

    private void Awake()
    {
        pixelPerfectCamera = GetComponent<PixelPerfectCamera>();
    }

    private void OnEnable()
    {
        // 다시 활성화될 때는 무조건 한 번 계산하도록 캐시를 비운다.
        appliedWidth = 0;
        appliedHeight = 0;

        Apply();
    }

    /// <summary>
    /// 해상도 변경 이벤트를 구독하지 않고 실제 화면 크기를 직접 감시합니다.
    /// Screen.SetResolution은 호출 즉시 반영되지 않아 이벤트 시점의 Screen 값이 아직
    /// 예전 값일 수 있고, 모니터 교체처럼 그 이벤트를 거치지 않는 경로도 있기 때문입니다.
    /// 값이 그대로면 int 비교 두 번으로 끝납니다.
    /// </summary>
    private void Update()
    {
        if (Screen.width == appliedWidth && Screen.height == appliedHeight) return;

        Apply();
    }

    private void Apply()
    {
        if (pixelPerfectCamera == null) return;

        // 화면 크기를 아직 신뢰할 수 없는 프레임에서는 캐시하지 않고 다음 프레임에 다시 시도한다.
        if (Screen.width <= 0 || Screen.height <= 0) return;

        appliedWidth = Screen.width;
        appliedHeight = Screen.height;

        // 배율은 세로만 보고 정해진다. 그 배율로 화면 가로를 나눈 값이 실제로 보이는 폭이다.
        int _scale = SettingsData.GetPixelScale(appliedHeight);
        float _visibleWidth = (float)appliedWidth / _scale;

        PixelPerfectCamera.CropFrame _desired =
            _visibleWidth > SettingsData.PIXEL_PERFECT_REF_WIDTH + WIDTH_EPSILON
                ? PixelPerfectCamera.CropFrame.Pillarbox
                : PixelPerfectCamera.CropFrame.None;

        // 같은 값을 다시 넣으면 PixelPerfectCamera가 오프스크린 RT를 재할당할 수 있으므로
        // 실제로 바뀔 때만 쓴다.
        if (pixelPerfectCamera.cropFrame != _desired)
        {
            pixelPerfectCamera.cropFrame = _desired;
        }
    }

    private void OnDisable()
    {
        // 이 컴포넌트가 멈추면 크롭 상태를 남기지 않는다. 그대로 두면 컴포넌트만 꺼진 채
        // 좌우가 잘린 화면이 유지되어 원인을 찾기 어려운 상태가 된다.
        if (pixelPerfectCamera != null)
        {
            pixelPerfectCamera.cropFrame = PixelPerfectCamera.CropFrame.None;
        }
    }
}
