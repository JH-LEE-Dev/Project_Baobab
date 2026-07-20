using System;
using UnityEngine;

// 아래 enum들은 정수값이 그대로 Settings.json에 직렬화된다.
// 따라서 기존 항목의 순서를 바꾸거나 중간에 삽입하면 저장된 설정이 다른 값으로 읽힌다.

/// <summary>
/// 주의: 현재 실제로 지원되는 항목은 Korean, English 둘뿐입니다.
/// (SettingsData.SUPPORTED_LANGUAGE_COUNT 참고. 나머지는 선택될 수 없으며 영어로 처리됩니다)
/// 언어를 늘리려면 그 상수와 SettingsManager의 언어 매핑·라벨을 함께 손봐야 합니다.
/// </summary>
public enum EOptionLanguage { Korean, English, Japanese, Chinese, Russian }

public enum EWindowMode { Windowed, Fullscreen }
public enum EOnOff { Off, On }

/// <summary>
/// 주의: 반드시 "해상도 오름차순"으로 선언되어야 합니다.
/// ClampResolution이 인덱스를 1씩 낮추며 강등하고, 첫 항목을 하한으로 취급합니다.
/// 중간 삽입이나 순서 변경은 저장된 정수값의 의미를 바꾸므로,
/// SettingsRepository의 버전을 올려 구버전 파일이 폐기되도록 해야 합니다.
///
/// 항목은 640x360의 정수배만 둡니다.
/// UI 캔버스가 640x360 기준이고(Assets/Prefabs/UI/Canvas/*.prefab) 게임 아트가
/// Point 필터 픽셀아트라, 비정수 배율에서는 원본 1px이 화면에서 2px과 3px로
/// 들쭉날쭉하게 찍혀 테두리·폰트 굵기가 불균일해집니다.
/// (그래서 2.5배인 1600x900은 제외했습니다)
/// </summary>
public enum EResolution { Res640x360, Res1280x720, Res1920x1080, Res2560x1440 }

/// <summary>
/// 주의: 숫자 FPS 항목이 앞쪽에 연속으로 오고, 그 뒤에 VSync/Unlimited가 와야 합니다.
/// SettingsManager의 fpsValues 배열이 앞쪽 항목과 인덱스로 1:1 대응합니다.
/// </summary>
public enum EFPS { FPS60, FPS75, FPS120, FPS144, FPS165, FPS240, VSync, Unlimited }

/// <summary>
/// 환경설정 값 전체를 담는 데이터 모델입니다.
/// UI가 아닌 SettingsManager가 소유하며, UI는 이 값을 읽어 표시만 합니다.
/// 여기 담긴 값은 "유저가 선택한 원본"이며, 현재 디스플레이 사정에 따른 보정은
/// 적용 시점에만 수행하고 이 구조체에는 반영하지 않습니다.
/// </summary>
[Serializable]
public struct SettingsData
{
    public EOptionLanguage language;
    public EResolution resolution;
    public EWindowMode windowMode;
    public EFPS fps;
    public EOnOff pauseOnUnfocus;

    public float cameraShake;
    public float crosshairBrightness;
    public float chromaticAberration;
    public float brightness;
    public float saturation;

    public float masterVolume;
    public float bgmVolume;
    public float sfxVolume;

    public const float SLIDER_MIN = 0f;
    public const float SLIDER_MAX = 100f;

    // enum 순환·검증에 쓰는 길이 상수 (Enum.IsDefined는 박싱이 발생하므로 쓰지 않는다)
    // 해상도 항목 수는 resolutionSizes 배열에서 파생된다. (SettingsData.ResolutionCount)
    public const int WINDOW_MODE_COUNT = 2;
    public const int FPS_COUNT = 8;
    public const int ON_OFF_COUNT = 2;

    /// <summary>
    /// LocalizationManager가 실제로 처리할 수 있는 언어 수입니다.
    /// EOptionLanguage에는 더 많은 항목이 선언되어 있지만, 지원되지 않는 값을 허용하면
    /// 선택기에는 "日本語"가 뜨는데 게임은 영어로 도는 불일치가 생깁니다.
    /// </summary>
    public const int SUPPORTED_LANGUAGE_COUNT = 2;

    public static SettingsData CreateDefault()
    {
        return new SettingsData
        {
            language = EOptionLanguage.Korean,
            resolution = EResolution.Res1920x1080,
            windowMode = EWindowMode.Windowed,
            fps = EFPS.Unlimited,
            pauseOnUnfocus = EOnOff.Off,

            cameraShake = SLIDER_MAX,
            crosshairBrightness = SLIDER_MAX,
            chromaticAberration = SLIDER_MAX,
            brightness = SLIDER_MAX,
            saturation = SLIDER_MAX,

            masterVolume = SLIDER_MAX,
            bgmVolume = SLIDER_MAX,
            sfxVolume = SLIDER_MAX
        };
    }

    public readonly struct ResolutionSize
    {
        public readonly int width;
        public readonly int height;

        public ResolutionSize(int _width, int _height)
        {
            width = _width;
            height = _height;
        }
    }

    /// <summary>
    /// 해상도 목록의 단일 소스입니다. EResolution 선언 순서와 인덱스가 1:1 대응합니다.
    /// 크기·표기 문자열·항목 수가 모두 여기서 파생되므로, 해상도를 추가할 때
    /// enum과 이 배열만 함께 늘리면 됩니다.
    ///
    /// 640x360의 정수배만 추가하세요. (옆의 배율 주석 참고)
    /// </summary>
    private static readonly ResolutionSize[] resolutionSizes =
    {
        new ResolutionSize(640, 360),    // 1x
        new ResolutionSize(1280, 720),   // 2x
        new ResolutionSize(1920, 1080),  // 3x
        new ResolutionSize(2560, 1440)   // 4x
    };

    public static int ResolutionCount => resolutionSizes.Length;

    public static void GetResolutionSize(EResolution _res, out int _width, out int _height)
    {
        int _idx = (int)_res;

        // 목록에 없는 값이면 기본 해상도로 폴백한다.
        // (switch + default 구조와 달리, 항목 누락이 조용히 묻히지 않고 여기 한 곳에서만 처리된다)
        if (_idx < 0 || _idx >= resolutionSizes.Length)
        {
            _idx = (int)EResolution.Res1920x1080;
        }

        _width = resolutionSizes[_idx].width;
        _height = resolutionSizes[_idx].height;
    }

    /// <summary>
    /// 저장 파일이 손상되었거나 변조되었을 때를 대비해 값을 유효 범위로 교정합니다.
    /// 여기서 고치는 것은 "애초에 있을 수 없는 값"뿐이며, 현재 모니터 사정에 따른
    /// 해상도 보정은 하지 않습니다. (그 보정을 여기서 하면 저장 파일에 기록되어
    /// 임시로 작은 화면에 연결한 것만으로 유저 설정이 영구히 사라집니다)
    /// </summary>
    /// <returns>값을 하나라도 교정했으면 true. (호출부가 정리된 값을 다시 저장할지 판단합니다)</returns>
    public bool Validate()
    {
        bool _corrected = false;

        // 제네릭 헬퍼로 묶으면 enum→int 변환에 박싱이 생기므로 구체 타입으로 직접 검사한다.
        if ((int)language < 0 || (int)language >= SUPPORTED_LANGUAGE_COUNT) { language = EOptionLanguage.Korean; _corrected = true; }
        if ((int)resolution < 0 || (int)resolution >= ResolutionCount) { resolution = EResolution.Res1920x1080; _corrected = true; }
        if ((int)windowMode < 0 || (int)windowMode >= WINDOW_MODE_COUNT) { windowMode = EWindowMode.Windowed; _corrected = true; }
        if ((int)fps < 0 || (int)fps >= FPS_COUNT) { fps = EFPS.Unlimited; _corrected = true; }
        if ((int)pauseOnUnfocus < 0 || (int)pauseOnUnfocus >= ON_OFF_COUNT) { pauseOnUnfocus = EOnOff.Off; _corrected = true; }

        _corrected |= ClampSlider(ref cameraShake);
        _corrected |= ClampSlider(ref crosshairBrightness);
        _corrected |= ClampSlider(ref chromaticAberration);
        _corrected |= ClampSlider(ref brightness);
        _corrected |= ClampSlider(ref saturation);

        _corrected |= ClampSlider(ref masterVolume);
        _corrected |= ClampSlider(ref bgmVolume);
        _corrected |= ClampSlider(ref sfxVolume);

        return _corrected;
    }

    private static bool ClampSlider(ref float _value)
    {
        // NaN은 어떤 비교에도 false라 Clamp만으로는 걸러지지 않으므로 별도로 처리한다.
        if (float.IsNaN(_value))
        {
            _value = SLIDER_MAX;
            return true;
        }

        float _clamped = Mathf.Clamp(_value, SLIDER_MIN, SLIDER_MAX);
        if (_clamped == _value) return false;

        _value = _clamped;
        return true;
    }

    /// <summary>
    /// 주어진 최대 크기 안에 들어가는 해상도로 낮춘 값을 반환합니다.
    /// 원본 값은 건드리지 않으므로, 큰 모니터로 돌아가면 원래 설정이 그대로 복원됩니다.
    ///
    /// 디스플레이를 직접 조회하지 않고 크기를 인자로 받는 이유:
    /// 이 구조체는 직렬화되는 순수 데이터여야 하며, 같은 입력에 항상 같은 답을 내야
    /// 하드웨어 없이도 검증할 수 있습니다. 실제 조회는 SettingsManager가 담당합니다.
    /// </summary>
    public static EResolution ClampResolution(EResolution _res, int _maxWidth, int _maxHeight)
    {
        // 디스플레이 정보를 신뢰할 수 없는 환경에서는 판단하지 않는다.
        if (_maxWidth <= 0 || _maxHeight <= 0) return _res;

        EResolution _result = _res;

        // 인덱스 0(가장 낮은 해상도)이 하한이다. 특정 항목을 하드코딩하지 않으므로
        // 목록 맨 앞에 더 낮은 해상도를 추가해도 그대로 동작한다.
        while ((int)_result > 0)
        {
            GetResolutionSize(_result, out int _width, out int _height);
            if (_width <= _maxWidth && _height <= _maxHeight) break;

            _result = (EResolution)((int)_result - 1);
        }

        return _result;
    }
}
