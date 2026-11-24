using UnityEngine;

/// <summary>
/// 로그라이크 전투 HUD의 대쉬 칸 전달 컴포넌트. 그림은 없고 월드 HUD(RoguelikeWorldHud)의 스태미너 숫자 아래 대쉬 칸으로 넘기기만 한다(WorldHUD_Plan.md 3장).
/// 본편 대쉬 게이지(HUD_DashGauge)는 로그라이크에서 화면에서만 숨고(UIView_HUD가 CanvasGroup alpha 0) 동작은 그대로 이어지며,
/// UIView_HUD가 같은 입력을 이 컴포넌트에도 전달한다(RoguelikeHUDLink.DashCells). 메서드 모양은 HUD_DashGauge와 같다.
///   잠겨 있으면(SetUnlocked false) 대쉬 칸이 숨는다. 해금되면 등장 연출 없이 바로 나타난다.
///   최대 칸 수가 1이면 한 칸 트레이, 2이면 두 칸이 한 트레이에 붙는다. 칸 채움은 정수 px(12 x 충전 비율)이고 가득이면 흰 점이 붙는다.
/// 월드 HUD는 UIView_RoguelikeHUD가 만들어 Initialize로 넘긴다(넘기기 전의 호출은 무시된다).
/// </summary>
public class UI_RoguelikeDashCells : MonoBehaviour
{
    public const int MaxCells = 2;

    //외부 의존성
    private RoguelikeWorldHud worldHud;

    public bool IsUnlocked => null != worldHud && true == worldHud.IsDashUnlocked;

    public void Initialize(RoguelikeWorldHud _worldHud)
    {
        worldHud = _worldHud;
    }

    /// <summary>대시를 쓸 수 있을 때만 보인다. 등장 연출은 없다.</summary>
    public void SetUnlocked(bool _bUnlocked)
    {
        if (null != worldHud) worldHud.SetDashUnlocked(_bUnlocked);
    }

    /// <summary>표시할 칸 수(0~2). 1이면 두 번째 칸이 숨는다.</summary>
    public void SetMaxCharges(int _count)
    {
        if (null != worldHud) worldHud.SetDashMax(_count);
    }

    /// <summary>칸 하나의 충전 비율(0~1).</summary>
    public void SetCharge(int _index, float _ratio)
    {
        if (null != worldHud) worldHud.SetDashCharge(_index, _ratio);
    }

    /// <summary>연출 없이 칸 채움만 바꾼다(처음 보이게 될 때의 초기 상태 맞춤용). 월드 HUD 칸에는 번쩍임 연출이 없어 SetCharge와 같다.</summary>
    public void SetChargeSilently(int _index, float _ratio)
    {
        if (null != worldHud) worldHud.SetDashCharge(_index, _ratio);
    }

    /// <summary>모든 칸을 같은 비율로 되돌린다(씬 진입/초기화용).</summary>
    public void ResetCharges(float _ratio)
    {
        for (int i = 0; MaxCells > i; i++) SetChargeSilently(i, _ratio);
    }

    public void SetVisible(bool _bVisible)
    {
        gameObject.SetActive(_bVisible);
    }
}
