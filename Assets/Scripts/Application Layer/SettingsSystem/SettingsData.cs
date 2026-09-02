using System;
using UnityEngine;

// 아래 enum들은 정수값이 그대로 Settings.json에 직렬화된다.
// 따라서 기존 항목의 순서를 바꾸거나 중간에 삽입하면 저장된 설정이 다른 값으로 읽힌다.

/// <summary>
/// 주의: 현재 실제로 지원되는 항목은 Korean, English, ChineseSimplified, ChineseTraditional, Japanese 다섯입니다.
/// (SettingsData.SUPPORTED_LANGUAGE_COUNT 참고. 나머지(Russian)는 선택될 수 없으며 영어로 처리됩니다)
/// 지원 항목은 반드시 맨 앞에서부터 인덱스 0..SUPPORTED_LANGUAGE_COUNT-1로 연속되어야 합니다.
/// (CycleLanguage와 Validate가 이 구간만 순환·허용하기 때문입니다)
/// 언어를 늘리려면 그 상수와 SettingsManager의 언어 매핑·라벨을 함께 손봐야 합니다.
///
/// ChineseSimplified/ChineseTraditional/Japanese는 선택은 가능하지만, 아직 실제 번역 텍스트가 없어
/// LocalizationEntry.zhHans/zhHant/ja가 비어 있으면 영어 텍스트로 폴백합니다.
/// (LocalizationManager.ParseJson 참고)
/// </summary>
public enum EOptionLanguage { Korean, English, ChineseSimplified, ChineseTraditional, Japanese, Russian }

public enum EWindowMode { Windowed, Fullscreen }
public enum EOnOff { Off, On }

/// <summary>
/// 크래시 리포트(Sentry)와 플레이 통계(GameAnalytics) 수집에 대한 유저의 선택입니다.
///
/// EOnOff를 쓰지 않고 별도 enum을 두는 이유는 "아직 묻지 않았음"이 반드시 구분되어야 하기
/// 때문입니다. 둘을 구분하지 못하면 (a) 최초 동의 팝업을 몇 번 띄워야 하는지 알 수 없고,
/// (b) 거부한 유저에게 매 실행마다 다시 묻게 됩니다.
///
/// NotAsked가 0인 것은 필수입니다. JsonUtility는 JSON에 없는 키를 default(0)로 채우므로,
/// 이 필드가 생기기 전에 만들어진 설정 파일은 자동으로 "아직 묻지 않음"이 되어 다음 실행에
/// 동의 팝업이 한 번 뜹니다. 반대로 Granted가 0이면 기존 유저 전원이 묻지도 않고 동의한
/// 것으로 처리되므로 절대 순서를 바꾸지 마십시오.
/// (SettingsRepository의 hapticStrength 보정이 필요 없는 것도 같은 이유입니다)
/// </summary>
public enum EDataConsent { NotAsked, Granted, Declined }

/// <summary>
/// 주의: 기존 항목(Res640x360~Res2560x1440)의 순서·인덱스는 절대 바꾸지 않습니다.
/// 중간 삽입이나 순서 변경은 저장된 정수값의 의미를 바꾸므로,
/// SettingsRepository의 버전을 올려 구버전 파일이 폐기되도록 해야 합니다.
/// 새 해상도는 항상 맨 뒤에 추가해서 기존 저장 파일을 그대로 호환시킵니다.
///
/// 앞 4개(16:9)는 640x360의 정수배, 뒤 4개(16:10)는 640x400의 정수배입니다.
/// 이 목록에서 정수배만 고르는 이유는 월드 픽셀 크기 때문이 아닙니다. 카메라 배율은
/// CinemachinePixelPerfect가 화면세로/360을 반올림해 쓰므로 어떤 해상도에서도 이미
/// 정수입니다. (SettingsData.GetPixelScale)
///
/// UI 캔버스는 별개입니다. ScaleWithScreenSize + Match Height로 동작해 배율이
/// 화면세로/360(실수)이며, 그 덕분에 캔버스 논리 높이가 항상 정확히 360으로 유지됩니다.
/// UI 프리팹들이 640x360 고정 크기로 제작되어 있어서 이 성질에 의존합니다.
/// (배율을 정수로 만들면 캔버스 높이가 360을 벗어나 고정 크기 UI가 화면을 못 채웁니다)
///
/// 정수배 해상도만 고르면 나누어떨어져서 반올림 오차가 0이 되고, 화면에 보이는 월드가
/// 기준 시야와 정확히 같아집니다. 즉 같은 그룹의 창모드 프리셋끼리는 시야가 완전히
/// 동일하다는 보장이 생깁니다. 16:9 4개는 세로 360, 16:10 4개는 세로 400을 보여줍니다.
/// (제외한 1600x900은 900/360=2.5가 2로 반올림되어 세로 450이 보입니다)
///
/// 전체화면은 이 목록을 쓰지 않고 모니터 해상도를 그대로 씁니다(SettingsManager.
/// GetCurrentScreenTarget). 그쪽은 나누어떨어지지 않는 해상도도 들어오지만, 배율 자체는
/// 여전히 정수이고 반올림 오차만큼 시야가 넓거나 좁아질 뿐입니다.
/// (예: 2880x1800은 배율 5배에 시야 576x360, 1680x1050은 3배에 560x350)
///
/// 유저에게 보이는 나열 순서는 이 선언 순서가 아니라 displayOrder 배열이 정합니다.
/// CycleResolution과 ClampResolution 모두 그 배열을 따라 이동하므로, 여기 선언 순서는
/// 저장되는 정수값의 의미만 담당합니다. 표시 순서를 바꾸고 싶으면 displayOrder만
/// 고치면 되고 저장 파일 호환성에는 영향이 없습니다.
/// </summary>
public enum EResolution
{
    Res640x360, Res1280x720, Res1920x1080, Res2560x1440,
    Res640x400, Res1280x800, Res1920x1200, Res2560x1600
}

/// <summary>
/// 주의: 숫자 FPS 항목이 앞쪽에 연속으로 오고, 그 뒤에 VSync/Unlimited가 와야 합니다.
/// SettingsManager의 fpsValues 배열이 앞쪽 항목과 인덱스로 1:1 대응합니다.
/// </summary>
public enum EFPS { FPS60, FPS75, FPS120, FPS144, FPS165, FPS240, VSync, Unlimited }

/// <summary>
/// 패드 버튼 아이콘을 어느 벤더 표기로 그릴지에 대한 "유저 설정"입니다.
/// 런타임 판별 결과인 EGamepadIconSet과는 별개의 타입인데, 이쪽에만 Auto가 있기 때문입니다.
/// (Auto = 자동 판별에 맡김. 나머지는 판별 결과를 무시하고 그 표기로 고정)
///
/// Auto가 아닌 선택지가 반드시 필요한 이유: Steam Input이 켜져 있으면 DualSense도 XInput
/// 가상 패드로 위장해서 들어와 자동 판별이 Xbox로 나옵니다. 어떤 판별 로직으로도 뚫을 수
/// 없으므로 유저가 직접 고를 수단이 있어야 합니다.
///
/// 주의: 다른 옵션 enum과 마찬가지로 정수값이 그대로 Settings.json에 직렬화됩니다.
/// 기존 항목의 순서를 바꾸거나 중간에 삽입하지 마세요. Auto는 기본값이므로 반드시 0입니다.
/// </summary>
public enum EGamepadIconPreference { Auto, Xbox, PlayStation, Generic }

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

    /// <summary>
    /// 데이터 수집(Sentry 크래시 리포트 / GameAnalytics 플레이 통계) 동의 여부입니다.
    ///
    /// 이 필드를 추가하면서 SettingsRepository의 버전을 올리지 않은 것은 의도적입니다.
    /// 기본값이 0(NotAsked)이라 키가 없는 구버전 파일도 올바르게 "아직 묻지 않음"으로
    /// 읽히므로, hapticStrength처럼 키 존재 여부를 확인하는 보정이 필요 없습니다.
    /// 버전을 올리면 파일이 통째로 폐기되어 유저의 해상도·볼륨 설정까지 날아갑니다.
    ///
    /// 이 값을 실제로 SDK에 반영하는 곳은 DataConsentGate 한 곳뿐입니다. 여기 값을 읽어
    /// 직접 SDK를 켜고 끄는 코드를 다른 곳에 만들지 마십시오. (정책이 갈라집니다)
    /// </summary>
    public EDataConsent dataConsent;

    public float cameraShake;
    public float crosshairBrightness;
    public float chromaticAberration;
    public float brightness;
    public float saturation;

    public float masterVolume;
    public float bgmVolume;
    public float sfxVolume;

    /// <summary>
    /// 패드 버튼 아이콘 표기 설정입니다.
    ///
    /// 이 필드를 추가하면서 SettingsRepository의 버전을 올리지 않은 것은 의도적입니다.
    /// JsonUtility는 키 이름으로 매칭하므로, 이 필드가 없는 구버전 파일을 읽어도 나머지 값은
    /// 그대로 살아나고 이 값만 default(0 = Auto)가 됩니다. 버전을 올리면 파일이 통째로
    /// 폐기되어(ESettingsLoadResult.Discarded) 유저의 해상도·볼륨 설정까지 날아갑니다.
    /// </summary>
    public EGamepadIconPreference gamepadIconPreference;

    /// <summary>
    /// 패드 진동 세기입니다. (0~100, 다른 슬라이더와 동일한 범위. 0 = 진동 끔)
    /// gamepadIconPreference와 같은 이유로 SettingsRepository 버전을 올리지 않았습니다.
    ///
    /// 주의: 이 값은 "없음"과 "0(유저가 끔)"을 구분할 수 없습니다. 기본값이 SLIDER_MAX인데
    /// 키가 없는 구버전 파일은 0으로 읽히므로, 그대로 두면 기존 유저의 진동이 조용히 꺼집니다.
    /// 그래서 SettingsRepository.TryLoad가 JSON에 이 키가 있는지 직접 확인해 보정합니다.
    /// (MIGRATION_KEY_HAPTIC_STRENGTH 참고)
    /// </summary>
    public float hapticStrength;

    /// <summary>
    /// 특성 UI 가상 커서의 이동 감도입니다. (0~100, 가운데 50이 기본 배율)
    ///
    /// 다른 슬라이더와 달리 0이 "끔"이 아닙니다. 0이어도 커서는 느리게나마 움직입니다.
    /// 감도 0은 커서를 아예 못 쓰는 상태라, 유저가 패드만으로 이 설정을 되돌릴 수단까지 잃습니다.
    /// 실제 속도 변환과 하한 처리는 UI_TentAbilityComponent.ApplyPadCursorSensitivity가 담당합니다.
    ///
    /// hapticStrength와 같은 이유로 SettingsRepository 버전을 올리지 않았고,
    /// 같은 이유로 "키 없음"과 "0"을 구분하는 보정이 필요합니다.
    /// (기본값이 50인데 키가 없는 파일은 0으로 읽혀서, 그대로 두면 기존 유저의 커서가
    ///  이유 없이 느려집니다. MIGRATION_KEY_CURSOR_SENSITIVITY 참고)
    /// </summary>
    public float virtualCursorSensitivity;

    public const float SLIDER_MIN = 0f;
    public const float SLIDER_MAX = 100f;

    /// <summary>
    /// BGM·효과음 볼륨의 기본값입니다. 게임의 사운드는 "슬라이더 50%가 기준"이라는 전제로
    /// 밸런싱되어 있어서, 이 값에서 원본 그대로(0dB) 재생됩니다. 즉 기본값을 50으로 둔다고
    /// 소리가 절반으로 줄어드는 것이 아니라, 유저가 100까지 올려 더 키울 여지를 주는 것입니다.
    /// (실제 dB 변환은 AudioManager와 AudioDuckSettings가 담당합니다)
    /// </summary>
    public const float SLIDER_MIX_DEFAULT = 50f;

    /// <summary>
    /// 가운데가 기본인 슬라이더의 기본값입니다. 감도처럼 "기준에서 위아래로 조절"하는 항목에 씁니다.
    /// SLIDER_MIX_DEFAULT와 숫자는 같지만 이유가 전혀 다르므로 따로 둡니다.
    /// (저쪽은 '0dB가 되는 지점', 이쪽은 '배율 1배가 되는 지점')
    /// </summary>
    public const float SLIDER_CENTER_DEFAULT = 50f;

    // enum 순환·검증에 쓰는 길이 상수 (Enum.IsDefined는 박싱이 발생하므로 쓰지 않는다)
    // 해상도 항목 수는 resolutionSizes 배열에서 파생된다. (SettingsData.ResolutionCount)
    public const int WINDOW_MODE_COUNT = 2;
    public const int FPS_COUNT = 8;
    public const int ON_OFF_COUNT = 2;
    public const int GAMEPAD_ICON_PREFERENCE_COUNT = 4;
    public const int DATA_CONSENT_COUNT = 3;

    /// <summary>
    /// LocalizationManager가 실제로 처리할 수 있는 언어 수입니다.
    /// EOptionLanguage에는 더 많은 항목이 선언되어 있지만, 지원되지 않는 값을 허용하면
    /// 선택기에는 "日本語"가 뜨는데 게임은 영어로 도는 불일치가 생깁니다.
    /// </summary>
    public const int SUPPORTED_LANGUAGE_COUNT = 5;

    public static SettingsData CreateDefault()
    {
        return new SettingsData
        {
            // 실제 첫 실행 언어는 이 값이 아니라 LanguageAutoDetect가 정한다(SettingsManager.Load).
            // 여기를 한국어로 두는 것은 추론까지 실패했을 때가 아니라, 이 메서드가 손상된 파일의
            // 폴백으로도 쓰이기 때문이다. 순수해야 해서 추론을 여기 넣지 않는다.
            language = EOptionLanguage.Korean,
            resolution = EResolution.Res1920x1080,
            windowMode = EWindowMode.Fullscreen,
            fps = EFPS.FPS60,
            pauseOnUnfocus = EOnOff.Off,

            // 기본값은 반드시 "아직 묻지 않음"이다. Granted를 기본값으로 두면 설정 파일이
            // 손상된 유저가 묻지도 않고 동의한 상태가 된다. (CreateDefault는 손상 파일의
            // 폴백으로도 쓰인다)
            dataConsent = EDataConsent.NotAsked,

            cameraShake = SLIDER_MAX,
            crosshairBrightness = SLIDER_MAX,
            chromaticAberration = SLIDER_MAX,
            brightness = SLIDER_MAX,
            saturation = SLIDER_MAX,

            masterVolume = SLIDER_MAX,
            bgmVolume = SLIDER_MIX_DEFAULT,
            sfxVolume = SLIDER_MIX_DEFAULT,

            gamepadIconPreference = EGamepadIconPreference.Auto,
            hapticStrength = SLIDER_MAX,
            virtualCursorSensitivity = SLIDER_CENTER_DEFAULT
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
    /// 새 항목은 반드시 맨 뒤에 추가하세요 (enum 주석 참고 - 기존 인덱스 불변).
    /// 640x360의 정수배(16:9) 또는 640x400의 정수배(16:10)만 추가하세요.
    /// </summary>
    private static readonly ResolutionSize[] resolutionSizes =
    {
        new ResolutionSize(640, 360),    // 16:9 1x
        new ResolutionSize(1280, 720),   // 16:9 2x
        new ResolutionSize(1920, 1080),  // 16:9 3x
        new ResolutionSize(2560, 1440),  // 16:9 4x
        new ResolutionSize(640, 400),    // 16:10 1x
        new ResolutionSize(1280, 800),   // 16:10 2x
        new ResolutionSize(1920, 1200),  // 16:10 3x
        new ResolutionSize(2560, 1600)   // 16:10 4x
    };

    public static int ResolutionCount => resolutionSizes.Length;

    /// <summary>
    /// 옵션 선택기에 보여줄 순서입니다. 가로폭 오름차순, 같은 가로폭 안에서는 세로 오름차순입니다.
    /// (1920x1080 다음에 1920x1200이 오도록)
    ///
    /// enum 선언 순서(= 저장되는 정수값)와 분리해 둔 이유가 중요합니다. enum 순서를 바꾸면
    /// 기존 저장 파일의 숫자가 다른 해상도를 가리키게 되어 SettingsRepository의 버전을 올려야
    /// 하지만, 표시 순서만 바꾸는 것은 저장값에 아무 영향이 없습니다.
    ///
    /// 해상도를 추가할 때는 resolutionSizes 맨 뒤에 넣고, 이 배열에는 원하는 위치에 끼워
    /// 넣으세요. 두 배열의 길이가 다르거나 항목이 빠지면 아래 정적 생성자가 잡아냅니다.
    /// </summary>
    private static readonly EResolution[] displayOrder =
    {
        EResolution.Res640x360,   // 640
        EResolution.Res640x400,
        EResolution.Res1280x720,  // 1280
        EResolution.Res1280x800,
        EResolution.Res1920x1080, // 1920
        EResolution.Res1920x1200,
        EResolution.Res2560x1440, // 2560
        EResolution.Res2560x1600
    };

    /// <summary>
    /// 어떤 모니터에도 들어간다고 가정하는 최소 해상도입니다. 표시 순서와 무관하게
    /// 목록에서 가장 작은 항목을 골라 두므로, 목록이 바뀌어도 하한이 따라옵니다.
    /// </summary>
    private static readonly EResolution smallestResolution = FindSmallestResolution();

    private static EResolution FindSmallestResolution()
    {
        int _best = 0;
        for (int i = 1; i < resolutionSizes.Length; i++)
        {
            bool _smaller = resolutionSizes[i].width < resolutionSizes[_best].width
                || (resolutionSizes[i].width == resolutionSizes[_best].width
                    && resolutionSizes[i].height < resolutionSizes[_best].height);

            if (_smaller) _best = i;
        }
        return (EResolution)_best;
    }

    // 표시 순서 배열이 목록과 어긋나면(누락·중복·길이 불일치) 선택기에서 특정 해상도를
    // 영영 고를 수 없게 된다. 조용히 넘어가면 찾기 어려우므로 로딩 시점에 한 번 검사한다.
    static SettingsData()
    {
        if (displayOrder.Length != resolutionSizes.Length)
        {
            Debug.LogError("[SettingsData] displayOrder와 resolutionSizes의 길이가 다릅니다.");
            return;
        }

        bool[] _seen = new bool[resolutionSizes.Length];
        for (int i = 0; i < displayOrder.Length; i++)
        {
            int _idx = (int)displayOrder[i];
            if (_idx < 0 || _idx >= _seen.Length || _seen[_idx])
            {
                Debug.LogError("[SettingsData] displayOrder에 중복되거나 잘못된 항목이 있습니다: " + displayOrder[i]);
                return;
            }
            _seen[_idx] = true;
        }
    }

    /// <summary>표시 순서상 몇 번째인지를 반환합니다. 목록에 없으면 0입니다.</summary>
    public static int GetDisplayOrderIndex(EResolution _res)
    {
        for (int i = 0; i < displayOrder.Length; i++)
        {
            if (displayOrder[i] == _res) return i;
        }
        return 0;
    }

    /// <summary>표시 순서상 _orderIndex번째 해상도입니다. 범위를 벗어나면 최소 해상도입니다.</summary>
    public static EResolution GetResolutionAtDisplayOrder(int _orderIndex)
    {
        if (_orderIndex < 0 || _orderIndex >= displayOrder.Length) return smallestResolution;
        return displayOrder[_orderIndex];
    }

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

    /// <summary>PixelPerfectCamera 기준 해상도의 가로폭입니다. 모든 프로필이 이 값을 공유합니다.</summary>
    public const int PIXEL_PERFECT_REF_WIDTH = 640;

    private const int PIXEL_PERFECT_REF_HEIGHT_16_9 = 360;
    private const int PIXEL_PERFECT_REF_HEIGHT_16_10 = 400;

    /// <summary>
    /// 실제로 적용될(또는 적용된) 화면 크기(_width, _height)를 보고 PixelPerfectCamera에
    /// 넣을 기준 해상도를 고릅니다. 16:9(360)와 16:10(400) 중 실제 화면비에 더 가까운
    /// 쪽을 선택하므로, 창모드 프리셋뿐 아니라 임의의 풀스크린 모니터 해상도에도 씁니다.
    ///
    /// 정확히 16:9나 16:10인 화면은 항상 해당 프로필이 선택되고(둘 사이 중간값에서만
    /// 애매하게 갈림), 그 결과 화면이 그 배율의 정수배라면 Crop Frame: None에서도
    /// 640x360(또는 640x400) 프레임 밖으로 노출되는 여분의 시야가 사라집니다.
    /// </summary>
    public static void GetReferenceResolution(int _width, int _height, out int _refWidth, out int _refHeight)
    {
        _refWidth = PIXEL_PERFECT_REF_WIDTH;

        if (_width <= 0 || _height <= 0)
        {
            _refHeight = PIXEL_PERFECT_REF_HEIGHT_16_9;
            return;
        }

        float _aspect = (float)_width / _height;
        float _aspect169 = 16f / 9f;
        float _aspect1610 = 16f / 10f;

        _refHeight = Mathf.Abs(_aspect - _aspect169) <= Mathf.Abs(_aspect - _aspect1610)
            ? PIXEL_PERFECT_REF_HEIGHT_16_9
            : PIXEL_PERFECT_REF_HEIGHT_16_10;
    }

    /// <summary>
    /// 게임플레이 카메라가 세로로 보여주도록 만들어진 시야 높이(기준 픽셀)입니다.
    /// 가상 카메라(Assets/Prefabs/Objects/Camera/Camera.prefab)의 Lens.OrthographicSize 5.625와
    /// PixelPerfectCamera의 Assets PPU 32에서 나옵니다. (5.625 * 2 * 32 = 360)
    ///
    /// 주의: 둘 중 하나라도 바꾸면 이 값도 같이 바꿔야 합니다. 씬에서 카메라의
    /// OrthographicSize를 오버라이드하는 경우도 마찬가지입니다. 어긋나면 줌 연출 이후
    /// 카메라가 엉뚱한 시야를 보여주는데, 아무 경고 없이 화면만 이상해지므로 찾기 어렵습니다.
    /// </summary>
    public const int CAMERA_VIEW_HEIGHT = 360;

    /// <summary>
    /// 주어진 화면에서 원본 1픽셀이 화면 몇 픽셀로 찍히는지(정수 배율)를 반환합니다.
    ///
    /// 이 프로젝트의 카메라는 CinemachinePixelPerfect가 붙어 있어서, 실제 배율이
    /// PixelPerfectCamera의 zoom(기준 해상도 대비 내림)이 아니라
    /// PixelPerfectCameraInternal.CorrectCinemachineOrthoSize의 cinemachineVCamZoom으로 정해집니다.
    ///
    ///   cinemachineVCamZoom = max(1, round(zoom * orthoSize / vcam의 OrthographicSize))
    ///
    /// 여기서 zoom * orthoSize는 화면세로/(2*PPU)로 정리되어 zoom이 약분됩니다. 즉
    /// PixelPerfectCamera의 기준 해상도(640x360 / 640x400)는 결과에 영향을 주지 않고,
    /// 화면 "세로"만으로 결정되며 내림이 아니라 반올림입니다. 그래서 아래 식이 곧 실제 배율입니다.
    ///
    /// 가로를 받지 않는 것도 같은 이유입니다. 시네머신은 세로(직교 크기)만 보정하므로
    /// 초광각(21:9, 32:9)에서도 가로는 배율에 관여하지 않고 시야만 넓어집니다.
    ///
    /// Mathf.RoundToInt는 .5에서 짝수로 붙는데(1600x900의 900/360=2.5 -> 2), 시네머신이
    /// 쓰는 것과 같은 함수라 일부러 그대로 둡니다. 다른 반올림을 쓰면 그 해상도에서
    /// UI와 월드의 배율이 어긋납니다.
    /// </summary>
    public static int GetPixelScale(int _screenHeight)
    {
        if (_screenHeight <= 0) return 1;

        // 기준 시야보다 작은 화면에서도 0배가 되지 않도록 1을 하한으로 둔다.
        return Mathf.Max(1, Mathf.RoundToInt((float)_screenHeight / CAMERA_VIEW_HEIGHT));
    }

    /// <summary>
    /// 주어진 화면 세로에서 픽셀 퍼펙트가 성립하는 직교 크기(OrthographicSize)를 반환합니다.
    /// _authoredOrthoSize는 가상 카메라에 작성해 둔 원본 값, 즉 CAMERA_VIEW_HEIGHT만큼을
    /// 보여주도록 정해진 크기입니다.
    ///
    /// CinemachinePixelPerfect가 켜져 있을 때 CorrectCinemachineOrthoSize가 만들어내는 값과
    /// 같습니다. 그래서 이 값을 카메라에 넣어두면 익스텐션의 보정이 항등이 되어,
    /// 익스텐션을 켜든 끄든 화면이 달라지지 않습니다. CameraMoveController가 줌 연출 동안
    /// 익스텐션을 끄기 때문에 이 성질이 필요합니다.
    ///
    /// 화면 세로가 CAMERA_VIEW_HEIGHT의 정수배인 해상도(16:9 프리셋 전부)에서는
    /// _authoredOrthoSize를 그대로 돌려주므로 아무것도 달라지지 않습니다.
    /// </summary>
    public static float GetPixelPerfectOrthoSize(int _screenHeight, float _authoredOrthoSize)
    {
        if (_screenHeight <= 0 || _authoredOrthoSize <= 0f) return _authoredOrthoSize;

        // 실제로 보이게 될 시야 높이(기준 픽셀). 배율이 정수라 반올림 오차가 여기로 흡수된다.
        float _viewHeight = (float)_screenHeight / GetPixelScale(_screenHeight);

        // 직교 크기는 시야 높이에 정비례하므로 기준 시야 대비 비율만 곱하면 된다.
        return _authoredOrthoSize * _viewHeight / CAMERA_VIEW_HEIGHT;
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
        if ((int)gamepadIconPreference < 0 || (int)gamepadIconPreference >= GAMEPAD_ICON_PREFERENCE_COUNT) { gamepadIconPreference = EGamepadIconPreference.Auto; _corrected = true; }

        // 범위를 벗어난 동의값은 Declined가 아니라 NotAsked로 되돌린다. 변조된 파일 때문에
        // 유저의 선택이 "거부"로 굳어버리는 것보다, 한 번 더 묻는 쪽이 안전하다.
        if ((int)dataConsent < 0 || (int)dataConsent >= DATA_CONSENT_COUNT) { dataConsent = EDataConsent.NotAsked; _corrected = true; }

        _corrected |= ClampSlider(ref cameraShake);
        _corrected |= ClampSlider(ref crosshairBrightness);
        _corrected |= ClampSlider(ref chromaticAberration);
        _corrected |= ClampSlider(ref brightness);
        _corrected |= ClampSlider(ref saturation);

        _corrected |= ClampSlider(ref masterVolume);
        _corrected |= ClampSlider(ref bgmVolume);
        _corrected |= ClampSlider(ref sfxVolume);

        _corrected |= ClampSlider(ref hapticStrength);
        _corrected |= ClampSlider(ref virtualCursorSensitivity);

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

        // enum 인덱스가 아니라 표시 순서를 따라 내려간다. 유저가 선택기에서 보는 순서와
        // 강등 순서가 같아야 "왼쪽으로 가면 점점 작아진다"는 기대가 유지된다.
        for (int i = GetDisplayOrderIndex(_res); i >= 0; i--)
        {
            EResolution _candidate = displayOrder[i];

            GetResolutionSize(_candidate, out int _width, out int _height);
            if (_width <= _maxWidth && _height <= _maxHeight) return _candidate;
        }

        // 표시 순서 맨 앞이 반드시 가장 작은 항목인 것은 아니므로(가로가 같으면 세로가 큰 쪽이
        // 앞에 온다) 여기까지 왔다면 목록에서 가장 작은 항목으로 떨어뜨린다.
        return smallestResolution;
    }
}
