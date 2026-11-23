using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 로그라이크 전투 HUD. 기획서 5-1 "전투 HUD" - 웨이브 · 임계치 · 처치 · 스태미너 · 대쉬는 섬 둘레 월드 HUD(RoguelikeWorldHud, WorldHUD_Plan.md)로 그리고,
/// 이 UI 뷰는 값을 월드 HUD로 넘기는 일과 우상단 보관함 트레이를 맡는다.
///   - 월드 HUD(worldHudPrefab): Initialize에서 씬 루트에 만들고 OnDestroy에서 파괴한다. 좌상단 웨이브 숫자(깃발 + 현재 웨이브, 언리밋은 무한대 + 번호) - SetWave,
///     좌상단 임계치 띠 + 나무 수 숫자 - SetThreshold, PlayPressurePulse, 위 가운데 남은 처치 수(도끼 + 남은 수) - SetWaveProgress.
///   - 스태미너(StaminaBar) + 대쉬 칸(DashCells): 그림 없는 전달 컴포넌트. 본편 HUD(UIView_HUD)의 같은 호출이 정적 연결점(RoguelikeHUDLink)을 통해 이 두 컴포넌트로 오고,
///     이 컴포넌트가 월드 HUD(우상단 스태미너 띠 + 숫자 + 대쉬 칸)로 넘긴다.
///   [퇴장] SetCombatVisible(false)는 월드 HUD가 0.25초(unscaled) 동안 사라지고, 보관함 트레이는 오른쪽으로 화면 밖으로 정수 픽셀 이동(약 0.25초, unscaled)한 뒤 숨긴다.
///     결과 화면이 뜰 때 뒤쪽을 비워 결과 창에만 시선이 가게 하는 용도다(호출은 시스템). 다시 켜면 월드 HUD는 카메라 중심에 맞춰 바로 나타나고 트레이는 원위치에서 나타난다.
///   - 보관함 트레이(OwnedTray): 우상단. 인벤토리 키 아이콘 + 보유 카드 총량 숫자판. 표시 전용(클릭 없음) - SetOwnedCardCounts
///     보유 카드 창은 이 뷰가 아니라 키(인벤토리 키)로 연다(RoguelikeUICoordinator).
///
/// 값은 바뀔 때만 들어온다(매 프레임 같은 값을 다시 넣지 않는다).
///
/// [UI 담당 구현 지점] 아래 virtual 메서드의 본문.
/// </summary>
public class UIView_RoguelikeHUD : UIView
{
    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike HUD)
    [SerializeField, Tooltip("월드 HUD 프리팹(Assets/Prefabs/Roguelike/UI/RoguelikeWorldHud.prefab). Initialize에서 씬 루트에 만든다. LumberBoy/UI/Build Roguelike World HUD 로 만든다.")]
    private RoguelikeWorldHud worldHudPrefab;
    [SerializeField] private UI_RoguelikeStaminaBar staminaBar;
    [SerializeField] private UI_RoguelikeDashCells dashCells;
    [SerializeField] private UI_RoguelikeOwnedCardTray ownedTray;
    [SerializeField] private UI_KeyboardImage ownedKeyImage;
    [SerializeField, Tooltip("도전과제 달성 팝업(우하단). LumberBoy/UI/Build Roguelike Challenge Toast 로 만든다. 전투 HUD 퇴장 모션 대상이 아니다.")] private UI_RoguelikeChallengeToast challengeToast;

    // 퇴장 모션: 화면 밖으로 빠지는 거리(px)와 시간(unscaled).
    private const float ExitDistance = 120f;
    private const int ExitCount = 1;
    private const int ExitTrayIndex = 0;
    private const float ExitSeconds = 0.25f;

    //내부 의존성
    private RoguelikeWorldHud worldHud;

    //상태 변수 (퇴장 모션)
    private readonly RectTransform[] exitRects = new RectTransform[ExitCount];
    private readonly Vector2[] exitBasePositions = new Vector2[ExitCount];
    private readonly Vector2[] exitDirections = new Vector2[ExitCount];
    private bool bExitBaseCaptured;
    private TweenCallback finishExitCallback;

    /// <summary>우상단 보관함 트레이. 보유 카드 창이 열리고 닫힐 때 이 트레이로 연결해(UIView_RoguelikeOwnedCards.SetTray) 트레이 연출과 줄어들 칸을 받는다.</summary>
    public UI_RoguelikeOwnedCardTray OwnedTray => ownedTray;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        // 보유 카드 창을 여는 키(인벤토리 키) 아이콘. 리바인딩과 입력 장치 전환을 따라간다(인벤토리 UI와 같은 방식).
        if (null != ownedKeyImage) ownedKeyImage.Initialize(_ctx?.inputManager);

        // 도전과제 달성 팝업: 머리말 문구와 언어 변경 구독.
        if (null != challengeToast) challengeToast.Initialize(_ctx?.localizationManager);

        // 월드 HUD는 씬 루트에 만든다(섬 둘레 물 위에 놓이는 월드 오브젝트). 스태미너 · 대쉬 전달 컴포넌트가 이 월드 HUD로 값을 넘긴다.
        if (null != worldHudPrefab)
        {
            worldHud = Instantiate(worldHudPrefab);
            worldHud.name = worldHudPrefab.name;
            worldHud.Initialize();
        }

        if (null != staminaBar) staminaBar.Initialize(worldHud);
        if (null != dashCells) dashCells.Initialize(worldHud);

        finishExitCallback = FinishExit;
        SetExitTarget(ExitTrayIndex, null != ownedTray ? ownedTray.transform as RectTransform : null, Vector2.right);

        // 본편 HUD(UIView_HUD)가 스태미너 · 대쉬 값을 이 두 컴포넌트에도 전달하도록 정적 연결점에 등록한다.
        RoguelikeHUDLink.StaminaBar = staminaBar;
        RoguelikeHUDLink.DashCells = dashCells;
        // 카드 선택 화면이 보관 연출이 끝나는 순간 트레이 도착 반응을 걸 수 있게 등록한다.
        RoguelikeHUDLink.OwnedTray = ownedTray;
    }

    protected override void OnShow()
    {
        base.OnShow();
        gameObject.SetActive(true);

        if (null != worldHud) worldHud.ResetState();
        if (null != ownedTray) ownedTray.ResetTray();
    }

    protected override void OnHide()
    {
        base.OnHide();
        if (null != worldHud) worldHud.HideImmediate();
        gameObject.SetActive(false);
    }

    /// <summary>웨이브 표기(좌상단 숫자). 언리밋이면 _unlimitIndex를 무한대 아이콘 옆에 그린다. 월드 HUD 위치를 카메라 중심에 다시 맞춘다.</summary>
    /// <param name="_regionStartWaves">쓰지 않는다(월드 HUD에는 웨이브 진행 점이 없다). 코디네이터 호출 시그니처 때문에 남겨 둔다.</param>
    public virtual void SetWave(int _waveNumber, bool _bUnlimit, int _unlimitIndex, IReadOnlyList<int> _regionStartWaves = null)
    {
        if (null != worldHud) worldHud.SetWave(_waveNumber, _bUnlimit, _unlimitIndex);
    }

    /// <summary>처치 수 / 목표 처치 수(아래 가운데 숫자, 게이지 없음).</summary>
    public virtual void SetWaveProgress(int _killed, int _target)
    {
        if (null != worldHud) worldHud.SetKills(_killed, _target);
    }

    /// <summary>
    /// 임계치 띠(좌상단) + 나무 수 숫자. 필드 나무 수 / 임계치 T. 손실 중(나무 수 &gt;= T)은 월드 HUD가 나무 수와 T로 판단하므로
    /// 스폰 상한(_spawnCap)과 압박 상태(_state)는 쓰지 않는다(띠 전체 길이는 상한과 상관없이 고정). 코디네이터 호출 시그니처 때문에 남겨 둔다.
    /// </summary>
    public virtual void SetThreshold(int _treeCount, int _threshold, int _spawnCap, EPressureState _state)
    {
        if (null != worldHud) worldHud.SetThreshold(_treeCount, _threshold);
    }

    /// <summary>
    /// 스태미너 손실 중 1초 펄스(기획서 v3.3 3-2). 손실은 프레임마다 조용히 깎이고, 이 메서드가 1초마다 임계치 띠 초과 토막 테 번쩍임 + 스태미너 잔상 + 효과음만 낸다.
    /// _lossRatio는 이번 펄스가 나타낼 최대 스태미너 대비 손실 비율이다(스태미너 쪽이 이 비율보다 작은 값은 무시한다).
    /// </summary>
    public virtual void PlayPressurePulse(float _lossRatio)
    {
        if (null != worldHud) worldHud.PlayPulse();
        if (null != staminaBar) staminaBar.PlayDamaged(_lossRatio);
        // 임시 연결(ZoomTick) - 전용 클립 수급 후 교체(기획서 C-2).
        Sound.PlayUI(SoundID.RoguelikePressurePulse);
    }

    /// <summary>보관함 트레이의 숫자판. 레어(케이스) · 일반(슬리브) 장수를 받아 합계를 보여 준다. 값을 받기 전에는 숫자판이 비어 있다.</summary>
    public virtual void SetOwnedCardCounts(int _rareCount, int _normalCount)
    {
        if (null != ownedTray) ownedTray.SetCounts(_rareCount, _normalCount);
    }

    /// <summary>
    /// 도전과제 달성 팝업(우하단, 284x48). 달성 신호를 받은 쪽이 번역된 제목(_data.title)을 넣어 부른다. 연달아 부르면 대기열(13)에서 하나씩 보여 준다.
    /// 카드 선택 · 일시정지 중에도 재생된다(unscaled). tip · bCleared는 쓰지 않는다. 이 HUD가 숨겨지면(런 종료) 대기열과 재생이 지워진다.
    /// </summary>
    public virtual void ShowChallengeToast(in RoguelikeChallengeViewData _data)
    {
        if (null != challengeToast) challengeToast.Show(in _data);
    }

    /// <summary>
    /// [삭제 예정] 골드는 폐지되어 이 메서드는 아무 일도 하지 않는다. 코디네이터(RoguelikeUICoordinator.cs 88줄)가 아직 호출하므로
    /// 정의만 남긴다. Application Layer 정리 후 삭제 예정(기획서 v3.2 C-2).
    /// </summary>
    public virtual void SetGoldVisible(bool _bVisible) { }

    /// <summary>
    /// 전투 HUD 요소(월드 HUD · 보관함 트레이) 표시 여부.
    /// 끌 때는 월드 HUD가 0.25초 동안 사라지고 트레이가 화면 밖으로 빠지는 퇴장 모션을 재생한 뒤 숨긴다(결과 화면 등장과 동시에 뒤쪽을 비운다).
    /// 켤 때는 월드 HUD가 카메라 중심에 맞춰 바로 나타나고 트레이는 원위치에서 바로 나타난다.
    /// </summary>
    public virtual void SetCombatVisible(bool _bVisible)
    {
        DOTween.Kill(this);

        if (true == _bVisible)
        {
            RestoreExitPositions();
            ApplyCombatVisible(true);
            if (null != worldHud) worldHud.SetVisible(true);
            return;
        }

        if (null != worldHud) worldHud.SetVisible(false);
        if (false == PlayExit()) ApplyCombatVisible(false);
    }

    private void ApplyCombatVisible(bool _bVisible)
    {
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
        if (RoguelikeHUDLink.OwnedTray == ownedTray) RoguelikeHUDLink.OwnedTray = null;

        DOTween.Kill(this);
        for (int i = 0; exitRects.Length > i; i++)
        {
            if (null != exitRects[i]) exitRects[i].DOKill();
        }

        if (null != worldHud) Destroy(worldHud.gameObject);
        base.OnDestroy();
    }
}
