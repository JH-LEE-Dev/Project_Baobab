using System;
using UnityEngine;

/// <summary>
/// 로그라이크 전투 HUD. 기획서 5-1 "전투 HUD (하단 중앙)".
///
/// 스태미너 바는 본편 HUD(UIView_HUD의 HUD_Stemina)를 그대로 쓴다. 이 뷰는 로그라이크에만 있는 것만 맡는다.
///   - 임계치 바: 필드 나무 수 / 임계치 T, 유예 진행도(스태미너 바와 통일감, 색만 다르게)
///   - 코인 HUD: 보유 골드(본편 코인 HUD 표현 재사용 권장 - CurrencyCounterHUD). 언리밋에서는 숨긴다.
///   - 웨이브 표기: 1~30, 언리밋은 "Unlimit-N"
///   - 웨이브 진행도(처치 수 / 목표)
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

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);
    }

    protected override void OnHide()
    {
        base.OnHide();
        gameObject.SetActive(false);
    }

    /// <summary>웨이브 표기. 언리밋이면 _unlimitIndex로 "Unlimit-N"을 그린다.</summary>
    public virtual void SetWave(int _waveNumber, bool _bUnlimit, int _unlimitIndex) { }

    /// <summary>처치 수 / 목표 처치 수.</summary>
    public virtual void SetWaveProgress(int _killed, int _target)
    {
        if (null != thresholdBar) thresholdBar.SetProgress(_killed, _target);
    }

    /// <summary>
    /// 임계치 수치(나무 수 / 임계치 / 스폰 상한 / 압박 상태). 임계 바는 처치 진행도(SetWaveProgress)를 보여 주므로 기본 구현은 쓰지 않는다.
    /// _graceRatio는 유예 진행도(0~1), _cooldownRatio는 지속 피해 중 다음 피해까지의 진행도(0~1).
    /// </summary>
    public virtual void SetThreshold(int _treeCount, int _threshold, int _spawnCap, EPressureState _state, float _graceRatio, float _cooldownRatio) { }

    /// <summary>임계치 피해 1회. 스태미너 바의 피격 연출은 본편 HUD가 이미 재생하므로 임계치 바 쪽 반응만 넣는다.</summary>
    public virtual void PlayPressureDamaged(float _damage)
    {
        if (null != thresholdBar) thresholdBar.PlayDamaged();
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

    /// <summary>전투 HUD 요소(임계치 바·웨이브) 표시 여부. 로비에서는 숨긴다.</summary>
    public virtual void SetCombatVisible(bool _bVisible)
    {
        if (null != thresholdBar) thresholdBar.SetVisible(_bVisible);
    }

    /// <summary>[UI 담당] 보유 카드 버튼·패드 키에서 부른다.</summary>
    protected void RequestOwnedCards() => OwnedCardsRequestedEvent?.Invoke();

    public override void OnDestroy()
    {
        OwnedCardsRequestedEvent = null;
        base.OnDestroy();
    }
}
