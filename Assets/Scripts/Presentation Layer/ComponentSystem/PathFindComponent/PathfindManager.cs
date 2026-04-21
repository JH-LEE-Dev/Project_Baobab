using UnityEngine;

/// <summary>
/// 길찾기 요청 부하 분산을 위한 전역 매니저 (Throttling / Load Balancing)
/// </summary>
public static class PathfindManager
{
    private static int maxSearchesPerFrame = 5; // 한 프레임당 최대 길찾기 시도 횟수
    private static int currentSearchesThisFrame;
    private static int lastFrameCount = -1;

    /// <summary>
    /// 현재 프레임에서 길찾기 요청이 가능한지 확인합니다.
    /// </summary>
    public static bool CanRequest()
    {
        // 프레임이 바뀌면 카운터 초기화
        if (Time.frameCount != lastFrameCount)
        {
            lastFrameCount = Time.frameCount;
            currentSearchesThisFrame = 0;
        }

        if (currentSearchesThisFrame < maxSearchesPerFrame)
        {
            currentSearchesThisFrame++;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 한 프레임당 허용되는 길찾기 횟수를 조정합니다.
    /// </summary>
    public static void SetMaxSearches(int _count) => maxSearchesPerFrame = _count;
}
