using UnityEngine;

/// <summary>
/// 럼버잭 NPC 멈춤 버그 추적용 디버그 로그의 온/오프 스위치. `Enabled`를 켜면 [LJDebug] 로그가
/// 다시 출력된다. InDungeonUnitSpawner의 인스펙터 체크박스로 켜고 끌 수 있다.
/// </summary>
public static class LJDebugLog
{
    public static bool Enabled = false;

    public static void Log(string _message)
    {
        if (Enabled) Debug.Log(_message);
    }

    public static void LogWarning(string _message)
    {
        if (Enabled) Debug.LogWarning(_message);
    }
}
