using System;
using UnityEngine;
using PresentationLayer.UISystem.UIView.HUD.Equipment;
using PresentationLayer.UISystem.UIView.HUD.DirectionalIndicator;
using PresentationLayer.DOTweenAnimationSystem;

public class UIView_HUD : UIView
{
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject moveHUD;
    [SerializeField] private GameObject hudEquipmentPrefab;
    [SerializeField] private GameObject hudSteminaBarPrefab;
    [SerializeField] private GameObject hudDirectionalIndicatorPrefab;
    [SerializeField] private GameObject hudMessagePrefab;
    [SerializeField] private GameObject hudScreenBloodPrefab;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private string mapTransitionMotionTag = "GoDown";

    [System.Serializable]
    public struct BuffHudIconEntry
    {
        public EBuffHudId id;
        public Sprite icon;
    }

    // 버프 id별 아이콘(12x12, Point 필터). 없으면 HUD_BuffBar의 임시 아이콘이 쓰인다.
    [SerializeField] private BuffHudIconEntry[] buffHudIcons;

    private HUD_Equipment hudEquipment;
    private HUD_Stemina hudStaminaBar;
    private HUD_BuffBar hudBuffBar;
    private HUD_DashGauge hudDashGauge;
    private HUD_DirIndicator hudDirIndicator;
    private HUD_Message hudMessage;
    private HUD_ScreenBlood hudScreenBlood;

    // 던전 입장 배너(HUD_Message) 연출이 끝까지 재생된 순간. 대지역 기믹 안내 Quest UI를 이 뒤에 띄운다.
    public event Action DungeonMessageCompletedEvent;

    // 던전 입장 배너가 재생 중인지(시작 지연 구간 포함).
    public bool IsDungeonMessagePlaying => null != hudMessage && hudMessage.IsPlaying;

    // 모션 중첩 방지를 위한 현재 재생 중인 엔트리 참조
    private MotionEntry goDownEntry;
    private MotionEntry goUpEntry;

    private ICharacter character;
    // 스태미나 순간 변화 이벤트 구독용. 구독/해제에 같은 델리게이트 인스턴스를 써야 하므로 한 번만 만든다.
    private IPHealthComponent subscribedHealthComponent;
    private Action<float> cachedOnStaminaDamaged;
    private Action<float> cachedOnStaminaRecovered;
    private Action cachedOnStaminaRecoverWasted;

    private MapType currentMapType;
    private ForestType currentForestType;

    // 대시 충전 상태(잔여 횟수·충전 진행도). 표시는 아직 없다 - 아래 "HUD_Dash Logic"의 두 메서드에서 그리면 된다.
    private IDashState dashState;
    private bool bDashShownUnlocked = false;
    private int dashShownCharges = 0;
    private int dashShownMax = 0;
    private float dashLastProgress = 0f;

    // 로그라이크 런: 본편 스태미너 바 · 대쉬 게이지는 화면에서만 숨기고(CanvasGroup alpha 0) 같은 호출을 로그라이크 HUD의 새 컴포넌트에도 전달한다(RoguelikeHUDLink).
    private GameObject hudStaminaRoot;
    private CanvasGroup staminaCanvasGroup;
    private float staminaOriginalAlpha = 1f;
    private bool bRunStaminaHidden = false;
    private UI_RoguelikeDashCells syncedRunDashCells;

    [Header("Indicator Settings")]
    [SerializeField] private float dirIndicatorShowDelay = 1f;

    #region Default Logic

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (null != omp)
            omp.Initialize();

        currentMapType = MapType.Town;

        Init_HUDEquipment();
        Init_HUDMessage();
        Init_HUDDirIndicator();
        Init_HUDScreenBlood();
        Init_HUDStaminaBar();

        bool isTown = MapType.Town == currentMapType;

        ChangedActiveStateEquipment(isTown);
        ChangedActiveStateStemina(isTown);
    }
    public override void OnDestroy()
    {
        UnbindStaminaEvents();
        UnbindDashState();

        hudEquipment?.OnDestroy();

        if (null != hudMessage)
        {
            hudMessage.PlayCompletedEvent -= OnDungeonMessageCompleted;
            hudMessage.Release();
        }

        DungeonMessageCompletedEvent = null;
    }

    protected override void OnShow() //이 UI가 켜졌을 때 호출 됨.
    {
        base.OnShow();
    }

    protected override void OnHide() //이 UI가 꺼졌을 때 호출 됨.
    {
        base.OnHide();
    }

    public void SetCharacter(ICharacter _character)
    {
        UnbindStaminaEvents();
        UnbindDashState();

        character = _character;

        BindStaminaEvents();
        BindDashState();

        hudEquipment?.BindingRef(character);
    }

    private void BindStaminaEvents()
    {
        if (null == character || null == character.pHealthComponent)
            return;

        if (null == cachedOnStaminaDamaged)
            cachedOnStaminaDamaged = OnStaminaDamaged;

        if (null == cachedOnStaminaRecovered)
            cachedOnStaminaRecovered = OnStaminaRecovered;

        if (null == cachedOnStaminaRecoverWasted)
            cachedOnStaminaRecoverWasted = OnStaminaRecoverWasted;

        subscribedHealthComponent = character.pHealthComponent;
        subscribedHealthComponent.StaminaDamagedEvent += cachedOnStaminaDamaged;
        subscribedHealthComponent.StaminaRecoveredEvent += cachedOnStaminaRecovered;
        subscribedHealthComponent.StaminaRecoverWastedEvent += cachedOnStaminaRecoverWasted;
    }

    private void UnbindStaminaEvents()
    {
        if (null == subscribedHealthComponent)
            return;

        subscribedHealthComponent.StaminaDamagedEvent -= cachedOnStaminaDamaged;
        subscribedHealthComponent.StaminaRecoveredEvent -= cachedOnStaminaRecovered;
        subscribedHealthComponent.StaminaRecoverWastedEvent -= cachedOnStaminaRecoverWasted;
        subscribedHealthComponent = null;
    }

    private void OnStaminaRecoverWasted()
    {
        hudStaminaBar?.PlayRecoverWasted();

        UI_RoguelikeStaminaBar _runBar = RunStaminaBar;
        if (null != _runBar)
            _runBar.PlayRecoverWasted();
    }

    private void OnStaminaDamaged(float _lossRatio)
    {
        hudStaminaBar?.PlayDamaged(_lossRatio);

        UI_RoguelikeStaminaBar _runBar = RunStaminaBar;
        if (null != _runBar)
            _runBar.PlayDamaged(_lossRatio);
    }

    private void OnStaminaRecovered(float _gainRatio)
    {
        hudStaminaBar?.PlayRecovered(_gainRatio);

        UI_RoguelikeStaminaBar _runBar = RunStaminaBar;
        if (null != _runBar)
            _runBar.PlayRecovered(_gainRatio);
    }

    public override void Update()
    {
        SyncRunHud();

        if (null != character && null != character.pHealthComponent)
        {
            UsedSteminaEvent(character.pHealthComponent.GetCurrentStamina(), character.pHealthComponent.GetMaxStamina());
            hudStaminaBar?.SetResting(character.pHealthComponent.IsInRestArea);
        }

        if (null != dashState)
        {
            UpdateDashRecharge(dashState.RechargeProgress);
        }
    }

    #endregion

    #region HUD_Dash Logic

    private void BindDashState()
    {
        if (null == character || null == character.dashState)
            return;

        dashState = character.dashState;
        dashState.DashStateChangedEvent += OnDashStateChanged;

        OnDashStateChanged();
    }

    private void UnbindDashState()
    {
        if (null == dashState)
            return;

        dashState.DashStateChangedEvent -= OnDashStateChanged;
        dashState = null;
    }

    private void OnDashStateChanged()
    {
        if (null == dashState)
            return;

        UpdateDashCharges(dashState.bUnlocked, dashState.CurrentCharges, dashState.MaxCharges);
    }

    /// <summary>
    /// [UI 작업용] 대시 해금 여부·찬 횟수·최대 횟수가 바뀔 때 불린다(최초 바인딩 때도 한 번).
    /// _bUnlocked가 false면 대시를 쓸 수 없는 상태(전리품 미보유, 마을, 로그라이크)라 숨기면 된다.
    /// </summary>
    private void UpdateDashCharges(bool _bUnlocked, int _currentCharges, int _maxCharges)
    {
        bDashShownUnlocked = _bUnlocked;
        dashShownCharges = _currentCharges;
        dashShownMax = _maxCharges;

        // 로그라이크 런이면 새 대쉬 칸에도 같은 상태를 전달한다(본편 게이지가 없어도 전달한다)
        UI_RoguelikeDashCells _runCells = RunDashCells;
        if (null != _runCells)
            ApplyRunDashState(_runCells);

        if (null == hudDashGauge)
            return;

        // 막 보이게 되는 순간의 칸 상태는 연출(사용/충전 완료 번쩍임) 없이 곧바로 맞춘다
        bool _bBecameVisible = true == _bUnlocked && false == hudDashGauge.IsUnlocked;

        hudDashGauge.SetMaxCharges(_maxCharges);
        hudDashGauge.SetUnlocked(_bUnlocked);
        ApplyDashCells(_bBecameVisible);
    }

    /// <summary>
    /// [UI 작업용] 매 프레임 불린다. 다음 1회가 차오른 정도(0~1)이며, 전부 찼으면 1이다.
    /// </summary>
    private void UpdateDashRecharge(float _rechargeProgress)
    {
        dashLastProgress = _rechargeProgress;

        UI_RoguelikeDashCells _runCells = RunDashCells;
        if (null != _runCells && dashShownCharges < dashShownMax)
            _runCells.SetCharge(dashShownCharges, _rechargeProgress);

        if (null == hudDashGauge)
            return;

        // 차오르는 칸 하나만 갱신하면 된다(찬 칸/빈 칸은 UpdateDashCharges에서 정해짐)
        if (dashShownCharges < dashShownMax)
            hudDashGauge.SetCharge(dashShownCharges, _rechargeProgress);
    }

    // 찬 칸은 1, 다음에 차오르는 칸은 진행도, 그 뒤 칸은 0
    private void ApplyDashCells(bool _bSilent)
    {
        for (int i = 0; i < HUD_DashGauge.MaxCells; i++)
        {
            float _ratio = 0f;

            if (i < dashShownCharges)
                _ratio = 1f;
            else if (i == dashShownCharges)
                _ratio = dashLastProgress;

            if (true == _bSilent)
                hudDashGauge.SetChargeSilently(i, _ratio);
            else
                hudDashGauge.SetCharge(i, _ratio);
        }
    }

    // 로그라이크 대쉬 칸으로 해금 · 칸 수 · 충전 상태를 한꺼번에 전달한다. 처음 보이게 되는 순간의 칸 상태는 연출 없이 맞춘다.
    private void ApplyRunDashState(UI_RoguelikeDashCells _cells)
    {
        bool _bBecameVisible = true == bDashShownUnlocked && false == _cells.IsUnlocked;

        _cells.SetMaxCharges(dashShownMax);
        _cells.SetUnlocked(bDashShownUnlocked);

        for (int i = 0; i < UI_RoguelikeDashCells.MaxCells; i++)
        {
            float _ratio = 0f;

            if (i < dashShownCharges)
                _ratio = 1f;
            else if (i == dashShownCharges)
                _ratio = dashLastProgress;

            if (true == _bBecameVisible)
                _cells.SetChargeSilently(i, _ratio);
            else
                _cells.SetCharge(i, _ratio);
        }
    }

    #endregion

    #region HUD_Equipment Logic

    private void Init_HUDEquipment()
    {
        hudEquipment = Instantiate(hudEquipmentPrefab, moveHUD.transform).GetComponent<HUD_Equipment>();

        if (null != hudEquipment)
        {
            hudEquipment.Initialize(viewCtx?.inputManager, viewCtx?.localizationManager);
        }
    }

    #endregion

    // 대시 게이지(조합 프리팹을 쓰지 않으면 null)
    public HUD_DashGauge DashGauge
    {
        get { return hudDashGauge; }
    }

    // 버프 줄(조합 프리팹을 쓰지 않으면 null)
    public HUD_BuffBar BuffBar
    {
        get { return hudBuffBar; }
    }

    #region HUD_Buff Logic

    public void ApplyBuffHud(in BuffHudSignal _s)
    {
        if (null == hudBuffBar)
            return;

        int _id = (int)_s.id;

        switch (_s.op)
        {
            case EBuffHudOp.Add:
                // 버프 줄이 숨겨진 동안(마을, 로그라이크)에는 새로 띄우지 않는다
                if (MapType.Town == currentMapType || true == bBuffHiddenForRun)
                    return;

                // 스택형은 이미 떠 있으면 값만 바꾼다(AddOrUpdate 재호출은 지속형 갱신용)
                if (BuffGaugeKind.Duration != _s.kind && true == hudBuffBar.Contains(_id))
                {
                    hudBuffBar.SetValue(_id, _s.value);
                    return;
                }

                BuffDisplayInfo _info = new BuffDisplayInfo();
                _info.id = _id;
                _info.kind = _s.kind;
                _info.icon = FindBuffHudIcon(_s.id);
                _info.duration = _s.duration;
                _info.maxValue = _s.max;
                _info.initialValue = _s.value;
                _info.thresholdValue = _s.threshold;
                _info.decayDelay = _s.decayDelay;
                _info.decayInterval = _s.decayInterval;
                _info.decayPerSecond = _s.decayPerSecond;
                hudBuffBar.AddOrUpdate(_info);
                break;

            case EBuffHudOp.SetValue:
                hudBuffBar.SetValue(_id, _s.value);
                break;

            case EBuffHudOp.Trigger:
                hudBuffBar.Trigger(_id);
                break;

            case EBuffHudOp.Dispel:
                hudBuffBar.Dispel(_id);
                break;

            case EBuffHudOp.Remove:
                hudBuffBar.Remove(_id);
                break;

            case EBuffHudOp.ClearAll:
                hudBuffBar.Clear();
                break;
        }
    }

    private Sprite FindBuffHudIcon(EBuffHudId _id)
    {
        if (null == buffHudIcons)
            return null;

        for (int i = 0; i < buffHudIcons.Length; i++)
        {
            if (_id == buffHudIcons[i].id)
                return buffHudIcons[i].icon;
        }

        return null;
    }

    #endregion

    #region HUD_Stemina Logic

    private void Init_HUDScreenBlood()
    {
        hudScreenBlood = Instantiate(hudScreenBloodPrefab, uiRoot.transform).GetComponent<HUD_ScreenBlood>();

        if (null != hudScreenBlood)
        {
            hudScreenBlood.Initialize();
            hudScreenBlood.transform.SetAsLastSibling();
        }
    }

    private void Init_HUDStaminaBar()
    {
        GameObject _staminaObject = Instantiate(hudSteminaBarPrefab, moveHUD.transform);
        hudStaminaRoot = _staminaObject;
        HUD_StaminaGroup _group = _staminaObject.GetComponent<HUD_StaminaGroup>();

        // 스태미나 + 버프 조합 프리팹이면 그룹이 둘을 함께 초기화한다. 단독 스태미나 프리팹이면 기존 방식 그대로.
        if (null != _group)
        {
            _group.Initialize(hudScreenBlood);
            hudStaminaBar = _group.Stamina;
            hudBuffBar = _group.BuffBar;
            hudDashGauge = _group.DashGauge;

            // 캐릭터 바인딩이 HUD 생성보다 먼저였다면 그때 받은 대시 상태를 지금 반영한다
            if (null != dashState)
                UpdateDashCharges(bDashShownUnlocked, dashShownCharges, dashShownMax);
            return;
        }

        hudStaminaBar = _staminaObject.GetComponent<HUD_Stemina>();

        if (null != hudStaminaBar)
        {
            hudStaminaBar.Initialize(hudScreenBlood);
        }
    }

    private void Init_HUDDirIndicator()
    {
        if (null == hudDirIndicator)
            hudDirIndicator = Instantiate(hudDirectionalIndicatorPrefab, uiRoot.transform).GetComponent<HUD_DirIndicator>();

        if (null != hudDirIndicator)
        {
            hudDirIndicator.Initialize();
        }
    }

    private void Init_HUDMessage()
    {
        if (null == hudMessage)
        {
            hudMessage = Instantiate(hudMessagePrefab, viewCtx.overlayCanvas.transform).GetComponent<HUD_Message>();
            // OverlayRoot(Clone) 보다 위(이전)에 배치하여 ResultUI 등 팝업류가 HUD_Message 위에 그려지게 함
            hudMessage.transform.SetSiblingIndex(1);
        }

        if (null != hudMessage)
        {
            hudMessage.Initialize(viewCtx?.localizationManager);
            hudMessage.PlayCompletedEvent -= OnDungeonMessageCompleted;
            hudMessage.PlayCompletedEvent += OnDungeonMessageCompleted;
        }
    }

    private void OnDungeonMessageCompleted()
    {
        DungeonMessageCompletedEvent?.Invoke();
    }


    private void UsedSteminaEvent(float _currentStemina, float _maxStemina)
    {
        float newRatio = _currentStemina / _maxStemina;
        hudStaminaBar?.UpdateValue(Mathf.Clamp01(newRatio));

        UI_RoguelikeStaminaBar _runBar = RunStaminaBar;
        if (null != _runBar)
            _runBar.UpdateValue(Mathf.Clamp01(newRatio));
    }

    /// <summary>
    /// 로그라이크 임계점 경고(기획서 v2.5 10-1). 스태미너 바 경고 모션(흔들림 + 색 핑퐁)과 붉은 화면(BloodScreen)을 따로 켠다.
    /// 임계점을 넘으면 둘 다, 한 번 맞으면 죽는 상태(아직 넘기 전)에서는 바만 켠다. 로그라이크 코디네이터가 부른다.
    /// </summary>
    public void SetRoguelikePressureWarning(bool _bBarWarning, bool _bScreenBlood)
    {
        if (null != hudStaminaBar)
            hudStaminaBar.SetForcedWarning(_bBarWarning, _bScreenBlood);

        // 로그라이크 스태미너 바에는 바 경고만 전달한다(붉은 화면은 본편 쪽에서 계속 나온다)
        UI_RoguelikeStaminaBar _runBar = RunStaminaBar;
        if (null != _runBar)
            _runBar.SetForcedWarning(_bBarWarning);
    }

    /// <summary>
    /// 스태미나 무적 구간 시작(true) / 종료(false). 원인별로 온다(EStaminaInvincibleSource 참고).
    /// </summary>
    public void StaminaInvincibleStateChanged(EStaminaInvincibleSource _source, bool _bInvincible)
    {
        // ?. 대신 Unity null 비교를 쓴다. 휴식 구역 이탈 알림은 씬 언로드 중 차량의 OnDisable에서도 오는데,
        // 그때 게이지가 먼저 파괴됐으면 ?.는 걸러내지 못한다(파괴된 오브젝트는 C# 참조로는 null이 아니다).
        if (null != hudStaminaBar)
            hudStaminaBar.SetInvincible(_source, _bInvincible);

        UI_RoguelikeStaminaBar _runBar = RunStaminaBar;
        if (null != _runBar)
            _runBar.SetInvincible(_source, _bInvincible);
    }

    #endregion

    //무기 모드 변환 시 호출. 기본값은 Axe
    public void WeaponModeChanged(WeaponMode _currentWeaponMode, bool _isMapChanged = false)
    {

    }

    public override void Refresh()
    {
        if (null != hudEquipment)
        {
            hudEquipment.UpdateAxeDurability();
        }
    }

    public void SetCurrentMapType(MapType _currentMapType, ForestType _currentForestType)
    {
        currentMapType = _currentMapType;
        currentForestType = _currentForestType;

        bool bTown = MapType.Town == currentMapType;

        if (true == bTown)
        {
            hudDirIndicator?.OnHide();
        }

        ChangedActiveStateEquipment(bTown);
        ChangedActiveStateStemina(bTown);

        // 과열 게이지는 맵이 바뀌는 순간 일단 화면 밖에 대기시키고, HUD가 다 올라온 뒤(HUDGoUp 완료) 튀어 오르게 한다.
        hudEquipment?.SetHeatGaugeVisible(false, false);
    }

    #region HUD_HeatGauge Logic

    // "예열" 특성(입장하자마자 과열 수치를 받는 특성, 아무 스테이지에서나 사용 가능)이 켜져 있는지.
    private bool bPreheatActive;

    /// <summary>
    /// [연결 자리] "예열" 특성이 켜지고 꺼질 때 부른다. 특성이 아직 없으므로 지금은 아무도 부르지 않는다.
    /// 특성이 생기면 그 SkillCommand(Execute / Undo) → 신호 → GameplayUICoordinator → 이 메서드 순서로 이어 주면 된다.
    /// 던전 안에서 바뀌면 그 자리에서 등장 / 퇴장 연출을 한다. 입장 시 과열 수치를 실제로 부여하는 일은 특성 쪽이 맡는다.
    /// </summary>
    public void SetPreheatActive(bool _bActive)
    {
        if (_bActive == bPreheatActive)
            return;

        bPreheatActive = _bActive;

        if (MapType.Town != currentMapType)
            hudEquipment?.SetHeatGaugeVisible(IsHeatGaugeRequired(), true);
    }

    // 4스테이지(MagmaForest)는 맵 규칙으로 과열이 쌓이고, 다른 스테이지는 "예열" 특성이 있을 때만 과열이 있다.
    // 마을과 로그라이크(장비 HUD 숨김)에서는 보이지 않는다.
    private bool IsHeatGaugeRequired()
    {
        if (true == bEquipmentHiddenForRun || MapType.Town == currentMapType)
            return false;

        return MapType.MagmaForest == currentMapType || true == bPreheatActive;
    }

    private void ShowHeatGaugeIfRequired()
    {
        if (true == IsHeatGaugeRequired())
            hudEquipment?.SetHeatGaugeVisible(true, true);
    }

    #endregion

    /// <summary>
    /// [로그라이크 전용] 도끼 내구도 HUD(장비 HUD)를 숨긴다. 로그라이크는 도끼 내구도가 무제한이라 표시하지 않는다(기획서 v2.2 10-1).
    /// 숨긴 동안은 맵이 바뀌어도 다시 켜지지 않는다. 본편에서는 호출되지 않는다.
    /// </summary>
    public void SetEquipmentHiddenForRun(bool _bHidden)
    {
        bEquipmentHiddenForRun = _bHidden;
        ChangedActiveStateEquipment(MapType.Town == currentMapType);

        if (true == _bHidden)
            hudEquipment?.SetHeatGaugeVisible(false, false);

        SyncRunHud();
    }

    private bool bEquipmentHiddenForRun;

    // 로그라이크 런 상태(SetEquipmentHiddenForRun)일 때만 로그라이크 HUD의 스태미너 바 · 대쉬 칸으로 전달한다. 링크 대상이 런 도중에 생겨도 호출할 때마다 확인한다.
    private UI_RoguelikeStaminaBar RunStaminaBar => true == bEquipmentHiddenForRun ? RoguelikeHUDLink.StaminaBar : null;
    private UI_RoguelikeDashCells RunDashCells => true == bEquipmentHiddenForRun ? RoguelikeHUDLink.DashCells : null;

    /// <summary>
    /// 로그라이크 런이고 로그라이크 스태미너 바가 있으면 본편 스태미너 바 · 대쉬 게이지를 화면에서만 숨기고(루트 CanvasGroup alpha 0, 동작은 그대로),
    /// 아니면 되돌린다. 로그라이크 HUD가 런 도중에 늦게 생기면 대쉬 칸에 지금까지의 대쉬 상태를 한 번 전달한다. 매 프레임 불러도 값이 같으면 아무 일도 하지 않는다.
    /// </summary>
    private void SyncRunHud()
    {
        bool _bHide = null != RunStaminaBar;

        if (_bHide != bRunStaminaHidden)
        {
            bRunStaminaHidden = _bHide;
            ApplyRunStaminaAlpha();
        }

        UI_RoguelikeDashCells _runCells = RunDashCells;

        if (_runCells != syncedRunDashCells)
        {
            syncedRunDashCells = _runCells;

            if (null != _runCells && null != dashState)
                ApplyRunDashState(_runCells);
        }
    }

    private void ApplyRunStaminaAlpha()
    {
        if (null == hudStaminaRoot)
            return;

        if (null == staminaCanvasGroup)
        {
            staminaCanvasGroup = hudStaminaRoot.GetComponent<CanvasGroup>();

            if (null == staminaCanvasGroup)
                staminaCanvasGroup = hudStaminaRoot.AddComponent<CanvasGroup>();

            staminaOriginalAlpha = staminaCanvasGroup.alpha;
        }

        staminaCanvasGroup.alpha = true == bRunStaminaHidden ? 0f : staminaOriginalAlpha;
    }

    /// <summary>
    /// [로그라이크 전용] 버프 줄을 숨기고 비운다(로그라이크에는 버프 HUD가 동작할 일이 없다. 스태미너 바 바로 위에 임계 바가 놓인다).
    /// 숨긴 동안은 맵이 바뀌어도 다시 켜지지 않는다. 본편에서는 호출되지 않는다.
    /// </summary>
    public void SetBuffHiddenForRun(bool _bHidden)
    {
        bBuffHiddenForRun = _bHidden;

        if (null == hudBuffBar)
            return;

        if (true == _bHidden)
        {
            hudBuffBar.Clear();
            hudBuffBar.gameObject.SetActive(false);
            return;
        }

        hudBuffBar.gameObject.SetActive(MapType.Town != currentMapType);
    }

    private bool bBuffHiddenForRun;

    /// <summary>[로그라이크 전용] 맵 전환용 HUD 내림·올림 모션이 재생 중인지. 진입 암전이 이 모션이 끝난 뒤 걷히게 한다. 본편에서는 호출되지 않는다.</summary>
    public bool IsMapTransitionMotionPlayingForRun => null != omp && omp.IsPlaying(mapTransitionMotionTag);

    private void ChangedActiveStateEquipment(bool _isTwon)
    {
        if (null == hudEquipment)
            return;

        if (bEquipmentHiddenForRun)
        {
            hudEquipment.gameObject.SetActive(false);
            return;
        }

        //WeaponModeChanged(WeaponMode.Axe, !_isTwon);
        hudEquipment.gameObject.SetActive(!_isTwon);

        if (!_isTwon)
            Refresh();
    }

    private void ChangedActiveStateStemina(bool _isTwon)
    {
        // 버프 줄도 마을에서는 숨긴다. 숨기기 전에 남은 버프/입자를 비워 다음 던전에서 이어 보이지 않게 한다.
        if (null != hudBuffBar)
        {
            if (true == _isTwon)
                hudBuffBar.Clear();

            hudBuffBar.gameObject.SetActive(!_isTwon && false == bBuffHiddenForRun);
        }

        if (null == hudStaminaBar)
            return;

        hudStaminaBar.SetActivate(!_isTwon);
    }

    public void OffroadSpawned(IOffroadProvider _offroadProvider)
    {
        hudDirIndicator?.SetTarget(_offroadProvider.transform);
    }

    public void HUDGoDown()
    {
        Sound.PlayUI(SoundID.HUDDown);

        hudDirIndicator?.OnHide();

        if (null != hudScreenBlood)
            hudScreenBlood.ResetAnimation(false);

        if (null != omp)
        {
            // 올라가는 모션이 재생 중이라면 강제 종료하여 모션 중첩을 방지합니다.
            omp.SettingEntryMotion(goUpEntry, true, true);
            goDownEntry = omp.Play(mapTransitionMotionTag, bReset: true);
        }

        // HUD가 내려가는 동안 과열 게이지도 함께 아래로 빠진다(보이지 않던 상태면 아무 일도 없다).
        // 올라가는 모션을 끊을 때 그 완료 콜백이 게이지를 띄울 수 있으므로 모션 처리 뒤에 숨긴다.
        hudEquipment?.SetHeatGaugeVisible(false, true);
    }

    /// <param name="_onCompleted">HUD가 완전히 다 올라온 시점에 호출된다. 모션이 없으면 즉시 호출된다.</param>
    /// <param name="_bSuppressDungeonStateBanner">MainMenu → Dungeon 튜토리얼의 최초 HUD 노출처럼, 던전 상태 배너(HUD_Message)를
    /// 띄우면 안 되는 경우 true로 넘긴다.</param>
    public void HUDGoUp(Action _onCompleted = null, bool _bSuppressDungeonStateBanner = false)
    {
        Sound.PlayUI(SoundID.HUDUp);

        if (null != omp)
        {
            // 내려가는 모션이 재생 중이라면 강제 종료하여 모션 중첩을 방지합니다.
            omp.SettingEntryMotion(goDownEntry, true, true);
            // 과열 게이지는 HUD가 다 올라온 뒤에 튀어 오른다(올라오는 모션과 겹치면 쫀득함이 묻힌다).
            goUpEntry = omp.PlayBackward(mapTransitionMotionTag, _onComplete: () =>
            {
                ShowHeatGaugeIfRequired();
                _onCompleted?.Invoke();
            }, bReset: true);
        }
        else
        {
            ShowHeatGaugeIfRequired();
            _onCompleted?.Invoke();
        }

        if (MapType.Town != currentMapType)
        {
            if (false == _bSuppressDungeonStateBanner)
                hudMessage?.Play();

            hudDirIndicator?.ShowAfterDelay(dirIndicatorShowDelay);
        }
    }

    //Dungeon State가 선언됨. 
    public void DungeonStateDeclared(MapType _mapType, ForestType _forestType, DungeonState _dungeonState)
    {
        hudMessage?.SetMessage(_forestType, _dungeonState);
    }
}
