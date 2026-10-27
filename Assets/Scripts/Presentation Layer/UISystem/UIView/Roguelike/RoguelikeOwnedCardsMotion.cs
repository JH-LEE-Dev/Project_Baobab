using UnityEngine;

/// <summary>
/// 보유 카드 창(UIView_RoguelikeOwnedCards)과 HUD 트레이(UI_RoguelikeOwnedCardTray)의 열기 · 닫기 연출이 함께 쓰는 시간표와 이징.
/// 시간은 "연출 단위"다: 실제 초 = 연출 단위 / Speed (시뮬레이션에서 확정한 2.2배). 창과 트레이가 같은 시작 시각 · 같은 표를 쓰므로 따로 돌려도 도착 순간이 맞는다.
/// 둘 다 게임이 일시정지(Time.timeScale = 0)된 채로 돌기 때문에 Time.unscaledDeltaTime으로 진행한다. 탄성(오버슈트)은 없다.
/// </summary>
public static class RoguelikeOwnedCardsMotion
{
    /// <summary>연출 속도 배율. 실제 시간 = 연출 단위 / Speed. 트레이의 프레임 표(UI_RoguelikeOwnedCardTray.MsPerUnit)가 2.2배를 기준으로 짜여 있어 둘이 같이 움직인다.</summary>
    public const float Speed = 2.2f;

    /// <summary>한 프레임에 진행할 수 있는 최대 시간(초). 프리팹을 켜는 프레임의 긴 델타가 연출을 건너뛰지 않게 한다.</summary>
    public const float MaxFrameSeconds = 0.05f;

    /// <summary>창 열기 길이(연출 단위).</summary>
    public const float OpenDuration = 1.0f;

    /// <summary>닫기에서 책이 트레이 칸에 닿는 시각(연출 단위). 이 시각에 창이 사라진다.</summary>
    public const float ArriveTime = 0.85f;

    /// <summary>[_from, _to] 구간에서의 진행도(0~1).</summary>
    public static float Segment(float _t, float _from, float _to)
    {
        return Mathf.Clamp01((_t - _from) / (_to - _from));
    }

    /// <summary>처음에 빠르고 끝에 느려지는 3차 이징(오버슈트 없음).</summary>
    public static float EaseOut(float _k)
    {
        float remain = 1f - _k;
        return 1f - remain * remain * remain;
    }

    /// <summary>처음과 끝이 느린 3차 이징(오버슈트 없음).</summary>
    public static float EaseInOut(float _k)
    {
        if (0.5f > _k) return 4f * _k * _k * _k;

        float remain = -2f * _k + 2f;
        return 1f - remain * remain * remain * 0.5f;
    }
}
