using UnityEngine;

/// <summary>
/// 실제 모니터 정보를 조회합니다.
/// Screen.currentResolution은 에디터에서 Game 뷰 크기를 반환할 수 있어
/// 플레이 모드와 빌드의 동작이 달라지므로, Unity 2022.2+의
/// Screen.mainWindowDisplayInfo를 우선 사용하고 실패 시에만 폴백합니다.
/// </summary>
public static class DisplayUtil
{
    /// <summary>게임 창이 놓인 모니터의 해상도입니다. 알 수 없으면 0을 반환합니다.</summary>
    public static void GetMainDisplaySize(out int _width, out int _height)
    {
        DisplayInfo _info = Screen.mainWindowDisplayInfo;

        if (_info.width > 0 && _info.height > 0)
        {
            _width = _info.width;
            _height = _info.height;
            return;
        }

        // 디스플레이 정보를 얻지 못하는 플랫폼을 위한 폴백
        _width = Screen.currentResolution.width;
        _height = Screen.currentResolution.height;
    }

    /// <summary>게임 창이 놓인 모니터의 주사율(Hz)입니다. 알 수 없으면 0을 반환합니다.</summary>
    public static float GetMainDisplayRefreshRate()
    {
        DisplayInfo _info = Screen.mainWindowDisplayInfo;

        double _rate = _info.refreshRate.value;
        if (_rate > 0d) return (float)_rate;

        _rate = Screen.currentResolution.refreshRateRatio.value;
        if (_rate > 0d) return (float)_rate;

        return 0f;
    }
}
