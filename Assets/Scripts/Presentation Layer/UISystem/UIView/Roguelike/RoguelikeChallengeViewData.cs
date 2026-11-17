/// <summary>
/// 도전과제 창의 한 칸. 창(UIView_RoguelikeChallenges)은 이 목록을 받아 그리기만 한다.
/// 도전과제 정의 · 달성 판정 · 저장은 Application Layer 몫이고, 제목과 팁은 현재 언어로 번역된 문자열을 넘긴다.
/// </summary>
public struct RoguelikeChallengeViewData
{
    /// <summary>제목(1줄).</summary>
    public string title;
    /// <summary>팁 = 달성 조건 설명(최대 2줄, 넘치면 말줄임).</summary>
    public string tip;
    /// <summary>달성 여부. 달성한 칸은 금색 테두리 바탕 + 제목 금색 + 오른쪽 금색 깃발, 미달성 칸은 기본 바탕이다.</summary>
    public bool bCleared;
}
