using System.Text;
using UnityEngine;

/// <summary>
/// 지금 이 게임이 돌고 있는 기계가 무엇인지 한 덩어리 텍스트로 모읍니다.
///
/// [왜 필요한가]
/// 최소사양 표기는 "이 사양에서 실제로 목표 프레임이 나오는 것을 확인했다"는 약속이라,
/// 성능 수치만 있고 그것을 낸 기계가 무엇인지 모르면 아무 근거도 되지 못합니다.
/// 그래서 벤치마크 리포트는 항상 이 블록과 함께 저장됩니다.
///
/// [일부러 담지 않는 것 - deviceUniqueIdentifier]
/// SystemInfo.deviceUniqueIdentifier는 기기를 특정하는 식별자라 개인정보에 해당합니다.
/// 리포트 파일은 유저가 서포트 채널에 그대로 첨부하게 될 물건이므로, 처음부터 담지 않습니다.
/// 사양 판정에 필요한 정보는 아래 항목만으로 전부 나옵니다.
///
/// [Player.log와 중복 아닌가]
/// 유니티도 부팅 시 GPU 정보를 Player.log에 찍습니다. 다만 CPU 코어 수, 시스템 RAM,
/// 현재 적용된 해상도/창 모드/FPS 설정처럼 판정에 꼭 필요한 값이 흩어져 있거나 아예 없습니다.
/// 판정에 쓰는 값은 한곳에 모여 있어야 나중에 여러 기기의 리포트를 나란히 비교할 수 있습니다.
/// </summary>
public static class SystemSpecReport
{
    /// <summary>메모리 값이 유니티에서 "알 수 없음"으로 오는 경우 표기할 문자열입니다.</summary>
    private const string UNKNOWN = "알 수 없음";

    /// <summary>사양 블록을 만들어 문자열로 돌려줍니다.</summary>
    public static string Build()
    {
        StringBuilder _sb = new StringBuilder(1024);
        Append(_sb);
        return _sb.ToString();
    }

    /// <summary>
    /// 사양 블록을 기존 StringBuilder에 이어 붙입니다.
    /// 리포트 전체를 한 번에 만드는 쪽(BenchmarkHarness)이 문자열을 두 번 만들지 않도록
    /// Build가 아니라 이쪽을 쓰게 되어 있습니다.
    /// </summary>
    public static void Append(StringBuilder _sb)
    {
        _sb.AppendLine("[빌드]");
        _sb.AppendLine($"  제품            : {Application.productName} {Application.version}");
        _sb.AppendLine($"  변형            : {BuildInfo.Variant}");
        _sb.AppendLine($"  빌드 GUID       : {Application.buildGUID}");
        _sb.AppendLine($"  유니티          : {Application.unityVersion}");
        _sb.AppendLine($"  개발자 빌드     : {(true == Debug.isDebugBuild ? "예 (성능 수치를 최소사양 근거로 쓰지 마세요)" : "아니오")}");
        _sb.AppendLine();

        _sb.AppendLine("[OS / CPU / RAM]");
        _sb.AppendLine($"  OS              : {SystemInfo.operatingSystem}");
        _sb.AppendLine($"  CPU             : {SystemInfo.processorType}");
        _sb.AppendLine($"  코어(논리)      : {SystemInfo.processorCount}");
        _sb.AppendLine($"  CPU 클럭        : {FormatFrequency(SystemInfo.processorFrequency)}");
        _sb.AppendLine($"  시스템 RAM      : {FormatMegabytes(SystemInfo.systemMemorySize)}");
        _sb.AppendLine();

        _sb.AppendLine("[GPU]");
        _sb.AppendLine($"  이름            : {SystemInfo.graphicsDeviceName}");
        _sb.AppendLine($"  벤더            : {SystemInfo.graphicsDeviceVendor}");
        _sb.AppendLine($"  드라이버        : {SystemInfo.graphicsDeviceVersion}");
        _sb.AppendLine($"  그래픽 API      : {SystemInfo.graphicsDeviceType}");
        _sb.AppendLine($"  VRAM            : {FormatMegabytes(SystemInfo.graphicsMemorySize)}");
        _sb.AppendLine($"  셰이더 레벨     : {FormatShaderLevel(SystemInfo.graphicsShaderLevel)}");
        _sb.AppendLine($"  멀티스레드 렌더 : {SystemInfo.graphicsMultiThreaded}");
        _sb.AppendLine();

        _sb.AppendLine("[화면]");
        _sb.AppendLine($"  백버퍼          : {Screen.width}x{Screen.height}");
        _sb.AppendLine($"  창 모드         : {Screen.fullScreenMode}");
        _sb.AppendLine($"  모니터 주사율   : {SettingsManager.GetMonitorRefreshRate():0.##} Hz");
        _sb.AppendLine($"  품질 레벨       : {GetQualityLevelName()}");
        _sb.AppendLine();

        _sb.AppendLine("[게임 설정]");
        AppendGameSettings(_sb);
    }

    /// <summary>
    /// 유저가 고른 설정을 적습니다. 성능 수치는 이 값들과 짝을 이룰 때만 의미가 있습니다.
    ///
    /// HasInstance로 확인하고 접근하는 이유는, 리포트를 쓰는 시점이 종료 직전일 수 있어서입니다.
    /// Instance 게터는 없으면 새로 만드는데, 그 시점에 만들어진 싱글턴은 정리되지 못한 채 남습니다.
    /// (SettingsManager.HasInstance 주석 참고)
    /// </summary>
    private static void AppendGameSettings(StringBuilder _sb)
    {
        if (false == SettingsManager.HasInstance)
        {
            _sb.AppendLine($"  (SettingsManager가 아직 없어 읽지 못했습니다)");
            return;
        }

        SettingsData _settings = SettingsManager.Instance.Current;

        _sb.AppendLine($"  해상도 선택     : {SettingsManager.GetResolutionLabel(_settings.resolution)}");
        _sb.AppendLine($"  창 모드 선택    : {_settings.windowMode}");
        _sb.AppendLine($"  FPS 선택        : {_settings.fps}");
        _sb.AppendLine($"  VSyncCount      : {QualitySettings.vSyncCount}");
        _sb.AppendLine($"  targetFrameRate : {Application.targetFrameRate}");
        _sb.AppendLine($"  백그라운드 실행 : {Application.runInBackground}");
    }

    /// <summary>
    /// 현재 품질 레벨의 이름입니다. 인덱스와 이름 배열이 어긋나는 상황(에디터에서 품질 레벨을
    /// 지우고 저장한 직후 등)에서 리포트 전체가 예외로 날아가지 않도록 범위를 확인합니다.
    /// </summary>
    private static string GetQualityLevelName()
    {
        int _level = QualitySettings.GetQualityLevel();
        string[] _names = QualitySettings.names;

        if (0 > _level || _level >= _names.Length) return $"{UNKNOWN} (index {_level})";

        return $"{_names[_level]} (index {_level})";
    }

    /// <summary>SystemInfo의 메모리 값은 알 수 없을 때 0 또는 음수로 옵니다.</summary>
    private static string FormatMegabytes(int _megabytes)
    {
        if (0 >= _megabytes) return UNKNOWN;

        if (1024 <= _megabytes) return $"{_megabytes} MB ({_megabytes / 1024f:0.#} GB)";

        return $"{_megabytes} MB";
    }

    private static string FormatFrequency(int _megahertz)
    {
        if (0 >= _megahertz) return UNKNOWN;

        return $"{_megahertz} MHz ({_megahertz / 1000f:0.##} GHz)";
    }

    /// <summary>
    /// graphicsShaderLevel은 45, 50 같은 정수로 오며 4.5 / 5.0을 뜻합니다.
    /// 이 프로젝트는 그래픽 API를 D3D11만 남겨 두었으므로 실질 하한은 50(Shader Model 5.0)입니다.
    /// 그보다 낮은 값이 찍힌 리포트가 나온다면 최소사양 표기가 아니라 그 기기의 실행 가능 여부부터
    /// 확인해야 합니다.
    /// </summary>
    private static string FormatShaderLevel(int _level)
    {
        if (0 >= _level) return UNKNOWN;

        return $"{_level / 10}.{_level % 10} ({_level})";
    }
}
