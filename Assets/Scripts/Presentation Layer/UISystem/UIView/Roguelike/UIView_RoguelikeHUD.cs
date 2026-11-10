using DG.Tweening;
using UnityEngine;

/// <summary>
/// 로그라이크 전투 HUD. 기획서 5-1 "전투 HUD" - 화면 좌측 하단(여백 8px)에 막대를 세로로 쌓는다(RoguelikeHUD_Polish_Plan.md).
///
/// 요소마다 모양 · 색 · 아이콘이 다르다(구분이 안 되던 문제). 모두 글자 라벨 없이 아이콘 + 작은 숫자(UI_RoguelikeIconNumber)다.
/// 모든 막대는 모서리 1px을 비운 캡슐 + 3톤 채움이고, 반투명 픽셀은 HUD 실루엣을 왼쪽 아래(-1, -1)에 복제한 그림자 한 장(UnityEngine.UI.Shadow)뿐이다.
/// 위에서 아래로:
///   - 웨이브 줄(WaveLabel): 깃발 + 1~30 + 30칸 진행 점. 언리밋은 금색 깃발 + 무한대 + 번호 + 10칸 진행 점 - SetWave
///   - 임계치 바(PressureStrip): 호박색 가로 막대. 나무 + 임계치 T 표식과 초과 구간, 숫자는 막대 뒤 - SetThreshold, PlayPressureDamaged
///   - 유예 타이머(GraceTimer): 임계치 바 아래에 붙은 2px 줄. 유예 중 보라 차오름, 쿨다운 주황 줄어듦. 숫자 없음 - SetThreshold, PlayPressureDamaged
///   - 목표치 바(ThresholdBar): 분홍적색 가로 막대. 도끼 + 현재 처치 수 / 목표 처치 수 - SetWaveProgress
///   - 스태미너 바(StaminaBar) + 대쉬 칸(DashCells): 본편 HUD의 스태미너 · 대쉬 게이지는 로그라이크에서 화면에서만 숨고(UIView_HUD),
///     같은 호출이 정적 연결점(RoguelikeHUDLink)을 통해 이 두 컴포넌트에도 전달된다.
///   - 코인 HUD: 보유 골드(본편 코인 HUD 표현 재사용 권장 - CurrencyCounterHUD). 언리밋에서는 숨긴다.
///   [퇴장] SetCombatVisible(false)는 좌측 하단 요소가 모두 왼쪽으로, 보관함 트레이는 오른쪽으로 화면 밖으로 정수 픽셀 이동(약 0.25초, unscaled)한 뒤 숨긴다.
///     결과 화면이 뜰 때 뒤쪽을 비워 결과 창에만 시선이 가게 하는 용도다(호출은 시스템 - LobbyRemoval_AppLayer_Request.md S5). 다시 켜면 원위치에서 나타난다.
///   - 보관함 트레이(OwnedTray): 우하단. 인벤토리 키 아이콘 + 보유 카드 총량 숫자판. 표시 전용(클릭 없음) - SetOwnedCardCounts
///     보유 카드 창은 이 뷰가 아니라 키(인벤토리 키)로 연다(RoguelikeUICoordinator).
///
/// 값은 바뀔 때만 들어온다(매 프레임 같은 값을 다시 넣지 않는다).
///
/// [UI 담당 구현 지점] 아래 virtual 메서드의 본문.
/// </summary>
public class UIView_RoguelikeHUD : UIView
{
    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike HUD)
    [SerializeField] private UI_RoguelikeThresholdBar thresholdBar;
    [SerializeField] private UI_RoguelikeWaveLabel waveLabel;
    [SerializeField] private UI_RoguelikePressureStrip pressureStrip;
    [SerializeField] private UI_RoguelikeGraceTimer graceTimer;
    [SerializeField] private UI_RoguelikeStaminaBar staminaBar;
    [SerializeField] private UI_RoguelikeDashCells dashCells;
    [SerializeField] private UI_RoguelikeOwnedCardTray ownedTray;
    [SerializeField] private UI_KeyboardImage ownedKeyImage;

    // 퇴장 모션: 화면 밖으로 빠지는 거리(px)와 시간(unscaled).
    private const float ExitDistance = 120f;
    private const int ExitCount = 7;
    private const int ExitTrayIndex = 6;
    private const float ExitSeconds = 0.25f;

    //상태 변수 (퇴장 모션)
    private readonly RectTransform[] exitRects = new RectTransform[ExitCount];
    private readonly Vector2[] exitBasePositions = new Vector2[ExitCount];
    private readonly Vector2[] exitDirections = new Vector2[ExitCount];
    private bool bExitBaseCaptured;
    private TweenCallback finishExitCallback;

    /// <summary>우하단 보관함 트레이. 보유 카드 창이 열리고 닫힐 때 이 트레이로 연결해(UIView_RoguelikeOwnedCards.SetTray) 트레이 연출과 줄어들 칸을 받는다.</summary>
    public UI_RoguelikeOwnedCardTray OwnedTray => ownedTray;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        // 보유 카드 창을 여는 키(인벤토리 키) 아이콘. 리바인딩과 입력 장치 전환을 따라간다(인벤토리 UI와 같은 방식).
        if (null != ownedKeyImage) ownedKeyImage.Initialize(_ctx?.inputManager);

        finishExitCallback = FinishExit;
        SetExitTarget(0, null != waveLabel ? waveLabel.transform as RectTransform : null, Vector2.left);
        SetExitTarget(1, null != thresholdBar ? thresholdBar.transform as RectTransform : null, Vector2.left);
        SetExitTarget(2, null != graceTimer ? graceTimer.transform as RectTransform : null, Vector2.left);
        SetExitTarget(3, null != pressureStrip ? pressureStrip.transform as RectTransform : null, Vector2.left);
        SetExitTarget(4, null != staminaBar ? staminaBar.transform as RectTransform : null, Vector2.left);
        SetExitTarget(5, null != dashCells ? dashCells.transform as RectTransform : null, Vector2.left);
        SetExitTarget(ExitTrayIndex, null != ownedTray ? ownedTray.transform as RectTransform : null, Vector2.right);

        // 본편 HUD(UIView_HUD)가 스태미너 · 대쉬 값을 이 두 컴포넌트에도 전달하도록 정적 연결점에 등록한다.
        RoguelikeHUDLink.StaminaBar = staminaBar;
        RoguelikeHUDLink.DashCells = dashCells;
    }

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        if (null != thresholdBar) thresholdBar.ResetBar();
        if (null != ownedTray) ownedTray.ResetTray();
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

    /// <summary>보관함 트레이의 숫자판. 레어(케이스) · 일반(슬리브) 장수를 받아 합계를 보여 준다. 값을 받기 전에는 숫자판이 비어 있다.</summary>
    public virtual void SetOwnedCardCounts(int _rareCount, int _normalCount)
    {
        if (null != ownedTray) ownedTray.SetCounts(_rareCount, _normalCount);
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

    /// <summary>
    /// 전투 HUD 요소(웨이브·목표치 바·유예 타이머·임계치 바·스태미너 바·대쉬 칸·보관함 트레이) 표시 여부.
    /// 끌 때는 요소마다 화면 밖으로 빠지는 퇴장 모션을 재생한 뒤 숨긴다(결과 화면 등장과 동시에 뒤쪽을 비운다). 켤 때는 원위치에서 바로 나타난다.
    /// </summary>
    public virtual void SetCombatVisible(bool _bVisible)
    {
        DOTween.Kill(this);

        if (true == _bVisible)
        {
            RestoreExitPositions();
            ApplyCombatVisible(true);
            return;
        }

        if (false == PlayExit()) ApplyCombatVisible(false);
    }

    private void ApplyCombatVisible(bool _bVisible)
    {
        if (null != thresholdBar) thresholdBar.SetVisible(_bVisible);
        if (null != pressureStrip) pressureStrip.SetVisible(_bVisible);
        if (null != graceTimer) graceTimer.SetVisible(_bVisible);
        if (null != waveLabel) waveLabel.SetVisible(_bVisible);
        if (null != staminaBar) staminaBar.SetVisible(_bVisible);
        if (null != dashCells) dashCells.SetVisible(_bVisible);
        if (null != ownedTray) ownedTray.SetVisible(_bVisible);
    }

    private void SetExitTarget(int _index, RectTransform _rect, Vector2 _direction)
    {
        exitRects[_index] = _rect;
        exitDirections[_index] = _direction;
    }

    /// <summary>보이는 요소를 화면 밖으로 민다. 움직일 것이 없으면 false.</summary>
    private bool PlayExit()
    {
        if (false == bExitBaseCaptured)
        {
            for (int i = 0; exitRects.Length > i; i++)
            {
                if (null != exitRects[i]) exitBasePositions[i] = exitRects[i].anchoredPosition;
            }
            bExitBaseCaptured = true;
        }

        bool bAny = false;
        for (int i = 0; exitRects.Length > i; i++)
        {
            RectTransform rect = exitRects[i];
            if (null == rect || false == rect.gameObject.activeInHierarchy) continue;

            // 왼쪽으로 빠지는 요소는 오른쪽 끝(위치 + 폭)까지 화면 밖으로 나가야 하므로 그 폭이 기본 거리보다 크면 폭만큼 민다.
            float distance = Vector2.left == exitDirections[i] ? Mathf.Max(ExitDistance, exitBasePositions[i].x + rect.rect.width + 1f) : ExitDistance;

            rect.DOKill();
            rect.DOAnchorPos(exitBasePositions[i] + exitDirections[i] * distance, ExitSeconds, true).SetEase(Ease.InCubic).SetUpdate(true);
            bAny = true;
        }

        if (true == bAny) DOVirtual.DelayedCall(ExitSeconds, finishExitCallback, true).SetId(this);
        return bAny;
    }

    private void FinishExit()
    {
        // 화면 밖에서 숨긴다. 원위치 복귀는 다시 켤 때 한다(숨는 도중 원위치로 돌아와 보이는 일이 없게).
        ApplyCombatVisible(false);
    }

    private void RestoreExitPositions()
    {
        if (false == bExitBaseCaptured) return;

        for (int i = 0; exitRects.Length > i; i++)
        {
            if (null == exitRects[i]) continue;
            exitRects[i].DOKill();
            exitRects[i].anchoredPosition = exitBasePositions[i];
        }
    }

    public override void OnDestroy()
    {
        if (RoguelikeHUDLink.StaminaBar == staminaBar) RoguelikeHUDLink.StaminaBar = null;
        if (RoguelikeHUDLink.DashCells == dashCells) RoguelikeHUDLink.DashCells = null;

        DOTween.Kill(this);
        for (int i = 0; exitRects.Length > i; i++)
        {
            if (null != exitRects[i]) exitRects[i].DOKill();
        }
        base.OnDestroy();
    }
}
