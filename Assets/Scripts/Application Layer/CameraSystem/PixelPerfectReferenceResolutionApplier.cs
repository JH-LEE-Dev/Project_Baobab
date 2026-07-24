using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 같은 GameObject의 PixelPerfectCamera 기준 해상도(Reference Resolution)를
/// 실제 화면 크기의 화면비에 맞춰 640x360(16:9) 또는 640x400(16:10)으로 전환합니다.
///
/// Crop Frame: None(확장)에서는 기준 해상도와 화면비가 어긋날 때마다 그 차이만큼
/// 여분의 시야가 노출되는데(SettingsData.GetReferenceResolution 주석 참고),
/// 화면비에 맞는 프로필로 미리 바꿔두면 정수배가 되는 해상도에서는 그 여분이 사라집니다.
/// </summary>
[RequireComponent(typeof(PixelPerfectCamera))]
public class PixelPerfectReferenceResolutionApplier : MonoBehaviour
{
    private PixelPerfectCamera pixelPerfectCamera;

    private void Awake()
    {
        pixelPerfectCamera = GetComponent<PixelPerfectCamera>();
    }

    private void OnEnable()
    {
        // SettingsManager는 최초 접근 시 자동 생성되는 싱글턴이라 null 체크가 필요 없다.
        SettingsManager.Instance.OnScreenTargetResolvedEvent -= ApplyReferenceResolution;
        SettingsManager.Instance.OnScreenTargetResolvedEvent += ApplyReferenceResolution;

        // Bootstrap이 이 오브젝트보다 먼저(또는 늦게) 실행됐을 수 있으므로,
        // 이벤트를 기다리지 않고 지금 시점 기준으로 한 번 즉시 반영한다.
        SettingsManager.Instance.GetCurrentScreenTarget(out int _width, out int _height);
        ApplyReferenceResolution(_width, _height);
    }

    private void OnDisable()
    {
        if (false == SettingsManager.HasInstance) return;
        SettingsManager.Instance.OnScreenTargetResolvedEvent -= ApplyReferenceResolution;
    }

    private void ApplyReferenceResolution(int _width, int _height)
    {
        SettingsData.GetReferenceResolution(_width, _height, out int _refWidth, out int _refHeight);

        pixelPerfectCamera.refResolutionX = _refWidth;
        pixelPerfectCamera.refResolutionY = _refHeight;
    }
}
