using UnityEngine;

/// <summary>
/// 럼버잭 NPC 멈춤 버그 추적용 디버그 로그의 온/오프 스위치. `Enabled`를 켜면 [LJDebug] 로그가
/// 다시 출력된다. InDungeonUnitSpawner의 인스펙터 체크박스로 켜고 끌 수 있다.
///
/// Log/LogWarning에는 Conditional이 붙어 있어 릴리즈 빌드에서는 호출부가 통째로 컴파일에서
/// 제거된다. 인자로 넘기는 보간 문자열($"...")도 함께 사라지므로, Enabled가 false여도 매번
/// 문자열이 만들어지던 낭비가 출시 빌드에는 남지 않는다. 에디터와 개발 빌드에서는 그대로 동작한다.
/// (Conditional은 여러 개를 붙이면 OR로 취급된다)
/// </summary>
public static class LJDebugLog
{
    public static bool Enabled = false;

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void Log(string _message)
    {
        if (Enabled) Debug.Log(_message);
    }

    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
    public static void LogWarning(string _message)
    {
        if (Enabled) Debug.LogWarning(_message);
    }
}
