using UnityEngine;

/// <summary>
/// 로그라이크 전투 HUD의 스태미너 표시 전달 컴포넌트. 그림은 없고 월드 HUD(RoguelikeWorldHud)의 스태미너 띠 + 숫자로 넘기기만 한다(WorldHUD_Plan.md 3장).
/// 본편 스태미너 바(HUD_Stemina)는 로그라이크에서 화면에서만 숨고(UIView_HUD가 CanvasGroup alpha 0) 동작은 그대로 이어지며,
/// UIView_HUD가 같은 호출을 이 컴포넌트에도 전달한다(RoguelikeHUDLink.StaminaBar). 붉은 화면(BloodScreen)은 본편 쪽에서 계속 나온다.
///   UpdateValue       현재 스태미너 비율 -> 띠 채움 길이
///   SetValues         현재 / 최대 -> 숫자(올림 정수)
///   PlayDamaged       줄어든 구간이 TRAIL 3톤 잔상으로 0.35초 줄어든다
///   PlayRecovered     늘어난 구간이 0.15초 흰색
///   SetForcedWarning  띠 테가 0.5초 주기로 흰 테와 번갈아 칠해진다
///   SetInvincible     원인(EStaminaInvincibleSource)별 비트로 기억하고 하나라도 켜져 있으면 무적 3톤
/// 월드 HUD는 UIView_RoguelikeHUD가 만들어 Initialize로 넘긴다(넘기기 전의 호출은 무시된다).
/// </summary>
public class UI_RoguelikeStaminaBar : MonoBehaviour
{
    //외부 의존성
    private RoguelikeWorldHud worldHud;

    //상태 변수
    private int invincibleMask;

    public void Initialize(RoguelikeWorldHud _worldHud)
    {
        worldHud = _worldHud;
    }

    /// <summary>현재 스태미너 비율(0~1). 값이 바뀔 때마다 매 프레임 들어온다.</summary>
    public void UpdateValue(float _ratio)
    {
        if (null != worldHud) worldHud.SetStaminaRatio(_ratio);
    }

    /// <summary>현재 / 최대 스태미너. 숫자는 올림 정수로 그린다(기획서 3-2). 값이 바뀔 때마다 매 프레임 들어온다.</summary>
    public void SetValues(float _current, float _max)
    {
        if (null != worldHud) worldHud.SetStaminaNumbers(_current, _max);
    }

    /// <summary>스태미너가 한 번에 줄어든 순간. _lossRatio는 최대치 대비 줄어든 비율이다.</summary>
    public void PlayDamaged(float _lossRatio)
    {
        if (null != worldHud) worldHud.PlayStaminaDamaged(_lossRatio);
    }

    /// <summary>스태미너가 한 번에 회복된 순간.</summary>
    public void PlayRecovered(float _gainRatio)
    {
        if (null != worldHud) worldHud.PlayStaminaRecovered(_gainRatio);
    }

    /// <summary>최대치에 막혀 회복량이 버려진 회복. 월드 HUD는 아무것도 하지 않는다(소리 연출도 이 컴포넌트에 없었다).</summary>
    public void PlayRecoverWasted()
    {
    }

    /// <summary>로그라이크 강제 경고의 바 경고(임계점 초과 · 한 번 맞으면 죽는 상태).</summary>
    public void SetForcedWarning(bool _bBarWarning)
    {
        if (null != worldHud) worldHud.SetForcedWarning(_bBarWarning);
    }

    /// <summary>무적 구간 시작(true) / 종료(false). 원인별 비트로 기억하고 하나라도 켜져 있으면 무적 팔레트를 쓴다.</summary>
    public void SetInvincible(EStaminaInvincibleSource _source, bool _bInvincible)
    {
        int bit = 1 << (int)_source;

        if (true == _bInvincible) invincibleMask |= bit;
        else invincibleMask &= ~bit;

        if (null != worldHud) worldHud.SetInvincible(0 != invincibleMask);
    }

    public void SetVisible(bool _bVisible)
    {
        gameObject.SetActive(_bVisible);
    }
}
