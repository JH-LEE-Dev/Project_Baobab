using System;
using UnityEngine;

/// <summary>
/// 로그라이크 전투 HUD. 기획서 5-1 "전투 HUD (하단 중앙)".
///
/// 스태미너 바는 본편 HUD(UIView_HUD의 HUD_Stemina)를 그대로 쓴다. 이 뷰는 로그라이크에만 있는 것만 맡는다.
/// 요소마다 모양 · 색 · 아이콘이 다르다(구분이 안 되던 문제). 모두 글자 라벨 없이 아이콘 + 작은 숫자(UI_RoguelikeIconNumber)다.
///   - 웨이브 배지(WaveLabel): 화면 상단 중앙에 혼자 있는 프레임 배지. 깃발 + 1~30, 언리밋은 무한대 + 번호 - SetWave
///   - 목표치 바(ThresholdBar): 스태미너 바 바로 위 분홍적색 가로 막대. 도끼 + 현재 처치 수 / 목표 처치 수 - SetWaveProgress
///   - 유예 타이머(GraceTimer): 목표치 바와 임계치 바 사이의 얇은 바. 유예 중 보라 차오름, 쿨다운 장미주황 줄어듦. 숫자 없음 - SetThreshold, PlayPressureDamaged
///   - 임계치 바(PressureStrip): 호박색 가로 막대. 나무 + 나무 수 / 임계치 T, T 눈금과 초과 구간 - SetThreshold, PlayPressureDamaged
///   - 코인 HUD: 보유 골드(본편 코인 HUD 표현 재사용 권장 - CurrencyCounterHUD). 언리밋에서는 숨긴다.
///   - 보유 카드 열기(마우스 클릭 버튼, 패드 키) → OwnedCardsRequestedEvent
///
/// 값은 바뀔 때만 들어온다(매 프레임 같은 값을 다시 넣지 않는다).
///
/// [UI 담당 구현 지점] 아래 virtual 메서드의 본문.
/// </summary>
public class UIView_RoguelikeHUD : UIView
{
    /// <summary>보유 카드 확인 UI를 열어 달라(기획서 5-2 "보유 카드 확인").</summary>
    public event Action OwnedCardsRequestedEvent;

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike HUD)
    [SerializeField] private UI_RoguelikeThresholdBar thresholdBar;
    [SerializeField] private UI_RoguelikeWaveLabel waveLabel;
    [SerializeField] private UI_RoguelikePressureStrip pressureStrip;
    [SerializeField] private UI_RoguelikeGraceTimer graceTimer;

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        if (null != thresholdBar) thresholdBar.ResetBar();
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
    }

    /// <summary>웨이브 표기. 언리밋이면 _unlimitIndex를 무한대 아이콘 옆에 그린다. 웨이브가 시작될 때 임계치 바가 첫 값으로 곧바로 서게 한다(빈 바에서 차오르지 않는다).</summary>
    public virtual void SetWave(int _waveNumber, bool _bUnlimit, int _unlimitIndex)
    {
        if (null != waveLabel) waveLabel.SetWave(_waveNumber, _bUnlimit, _unlimitIndex);
        if (null != pressureStrip) pressureStrip.SnapNext();
    }

    /// <summary>처치 수 / 목표 처치 수.</summary>
    public virtual void SetWaveProgress(int _killed, int _target)
    {
        if (null != thresholdBar) thresholdBar.SetProgress(_killed, _target);
    }

    /// <summary>
    /// 임계치 바와 유예 타이머. 필드 나무 수 / 임계치 T / 스폰 상한(바의 끝)과 압박 상태.
    /// _graceRatio는 유예 진행도(0~1), _cooldownRatio는 지속 피해 중 다음 피해까지의 진행도(0~1).
    /// </summary>
    public virtual void SetThreshold(int _treeCount, int _threshold, int _spawnCap, EPressureState _state, float _graceRatio, float _cooldownRatio)
    {
        if (null != pressureStrip) pressureStrip.SetValues(_treeCount, _threshold, _spawnCap, _state);
        if (null != graceTimer) graceTimer.SetState(_state, EPressureState.Grace == _state ? _graceRatio : _cooldownRatio, 1f);
    }

    /// <summary>임계치 피해 1회. 스태미너 바의 피격 연출은 본편 HUD가 이미 재생하므로 임계치 바와 유예 타이머 쪽 반응만 넣는다.</summary>
    public virtual void PlayPressureDamaged(float _damage)
    {
        if (null != pressureStrip) pressureStrip.PlayDamaged();
        if (null != graceTimer) graceTimer.PlayDamaged();
    }

    /// <summary>보유 골드. _bAnimated면 코인이 흡수되어 오르는 연출(기획서 3-3 골드 획득 5단계).</summary>
    public virtual void SetGold(long _gold, bool _bAnimated) { }

    /// <summary>코인 HUD 표시 여부. 언리밋에서는 골드를 쓸 곳이 없어 숨긴다(기획서 3-5).</summary>
    public virtual void SetGoldVisible(bool _bVisible) { }

    /// <summary>
    /// [UI 담당] 웨이브 종료 때 바닥 코인이 날아가 닿는 지점(코인 HUD 아이콘). 기획서 3-3 골드 획득 5단계 "코인 HUD 쪽으로 날아가는 애니메이션".
    /// null이면 기획서 5-1 배치(하단 중앙, 바 기준 왼쪽 윗부분)의 임시 화면 지점으로 날아간다(RoguelikeSystem.coinHudFallbackViewport).
    /// </summary>
    public virtual RectTransform CoinFlyTarget => null;

    /// <summary>전투 HUD 요소(웨이브·목표치 바·유예 타이머·임계치 바) 표시 여부. 로비에서는 숨긴다.</summary>
    public virtual void SetCombatVisible(bool _bVisible)
    {
        if (null != thresholdBar) thresholdBar.SetVisible(_bVisible);
        if (null != pressureStrip) pressureStrip.SetVisible(_bVisible);
        if (null != graceTimer) graceTimer.SetVisible(_bVisible);
        if (null != waveLabel) waveLabel.SetVisible(_bVisible);
    }

    /// <summary>[UI 담당] 보유 카드 버튼·패드 키에서 부른다.</summary>
    protected void RequestOwnedCards() => OwnedCardsRequestedEvent?.Invoke();

    public override void OnDestroy()
    {
        OwnedCardsRequestedEvent = null;
        base.OnDestroy();
    }
}
