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
    [SerializeField] private GameObject hudLootPrefab;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private string mapTransitionMotionTag = "GoDown";

    private HUD_Equipment hudEquipment;
    private HUD_Stemina hudStaminaBar;
    private HUD_BuffBar hudBuffBar;
    private HUD_DirIndicator hudDirIndicator;
    private HUD_Message hudMessage;
    private HUD_ScreenBlood hudScreenBlood;
    private HUD_Loot hudLoot;

    // 모션 중첩 방지를 위한 현재 재생 중인 엔트리 참조
    private MotionEntry goDownEntry;
    private MotionEntry goUpEntry;

    private ILootDataProvider lootDataProvider;
    [Header("Loot Data")]
    [SerializeField] private LootItemTypeDataBase lootItemTypeDataBase;

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

    [Header("Indicator Settings")]
    [SerializeField] private float dirIndicatorShowDelay = 1f;

    #region Default Logic

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (null != omp)
            omp.Initialize();

        currentMapType = MapType.Town;

        Init_HUDLoot();
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
        hudMessage?.Release();

        if (lootDataProvider != null)
        {
            lootDataProvider.LootAcquiredEvent -= OnLootAcquired;
        }
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
    }

    private void OnStaminaDamaged(float _lossRatio)
    {
        hudStaminaBar?.PlayDamaged(_lossRatio);
    }

    private void OnStaminaRecovered(float _gainRatio)
    {
        hudStaminaBar?.PlayRecovered(_gainRatio);
    }

    public void DependencyInjection(ILootDataProvider _lootDataProvider)
    {
        if (lootDataProvider != null)
        {
            lootDataProvider.LootAcquiredEvent -= OnLootAcquired;
        }

        lootDataProvider = _lootDataProvider;
        
        if (lootDataProvider != null)
        {
            lootDataProvider.LootAcquiredEvent += OnLootAcquired;
        }
    }

    private void OnLootAcquired(LootType newlyAcquiredType)
    {
        if (null != hudLoot && LootType.SporePotion == newlyAcquiredType)
        {
            hudLoot.AcquireLoot(newlyAcquiredType);

            if (MapType.Town == currentMapType)
            {
                ChangedActiveStateLoot(true);
            }
        }
    }

    public override void Update()
    {
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
    }

    /// <summary>
    /// [UI 작업용] 매 프레임 불린다. 다음 1회가 차오른 정도(0~1)이며, 전부 찼으면 1이다.
    /// </summary>
    private void UpdateDashRecharge(float _rechargeProgress)
    {
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

    // 버프 줄(조합 프리팹을 쓰지 않으면 null)
    public HUD_BuffBar BuffBar
    {
        get { return hudBuffBar; }
    }

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
        HUD_StaminaGroup _group = _staminaObject.GetComponent<HUD_StaminaGroup>();

        // 스태미나 + 버프 조합 프리팹이면 그룹이 둘을 함께 초기화한다. 단독 스태미나 프리팹이면 기존 방식 그대로.
        if (null != _group)
        {
            _group.Initialize(hudScreenBlood);
            hudStaminaBar = _group.Stamina;
            hudBuffBar = _group.BuffBar;
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
            hudMessage.Initialize(viewCtx?.localizationManager);
    }

    private void Init_HUDLoot()
    {
        if (null == hudLoot)
            hudLoot = Instantiate(hudLootPrefab, moveHUD.transform).GetComponent<HUD_Loot>();

        if (null != hudLoot)
            hudLoot.Initialize(viewCtx?.inputManager);
    }



    private void UsedSteminaEvent(float _currentStemina, float _maxStemina)
    {
        float newRatio = _currentStemina / _maxStemina;
        hudStaminaBar?.UpdateValue(Mathf.Clamp01(newRatio));
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

        if (null != lootDataProvider && null != hudLoot)
        {
            var _ownedLoots = lootDataProvider.CurrentOwnedLoots;
            if (null != _ownedLoots)
            {
                // HUD_Loot는 포션 슬롯 하나만 담당하므로(전달하는 LootType은 내부에서 무시된다)
                // 실시간 경로인 OnLootAcquired와 동일하게 SporePotion만 걸러서 넘긴다.
                // 필터 없이 넘기면 별빛 나침반 같은 다른 전리품만 가진 상태에서도 포션 슬롯이 켜진다.
                for (int i = 0; i < _ownedLoots.Count; i++)
                {
                    if (LootType.SporePotion != _ownedLoots[i]) continue;

                    hudLoot.AcquireLoot(_ownedLoots[i], false);
                }
            }
        }

        ChangedActiveStateLoot(MapType.Town == currentMapType);
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
        ChangedActiveStateLoot(bTown);
    }

    private void ChangedActiveStateEquipment(bool _isTwon)
    {
        if (null == hudEquipment)
            return;

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

            hudBuffBar.gameObject.SetActive(!_isTwon);
        }

        if (null == hudStaminaBar)
            return;

        hudStaminaBar.SetActivate(!_isTwon);
    }

    private void ChangedActiveStateLoot(bool _isTown)
    {
        if (null == hudLoot)
            return;

        if (true == _isTown)
        {
            hudLoot.gameObject.SetActive(false);
        }
        else
        {
            hudLoot.gameObject.SetActive(true == hudLoot.HasAcquired);
        }
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
            
        hudLoot?.OnHUDGoDown();

        if (null != omp)
        {
            // 올라가는 모션이 재생 중이라면 강제 종료하여 모션 중첩을 방지합니다.
            omp.SettingEntryMotion(goUpEntry, true, true);
            goDownEntry = omp.Play(mapTransitionMotionTag, bReset: true);
        }
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
            goUpEntry = omp.PlayBackward(mapTransitionMotionTag, _onComplete: () => _onCompleted?.Invoke(), bReset: true);
        }
        else
        {
            _onCompleted?.Invoke();
        }

        if (MapType.Town != currentMapType)
        {
            hudLoot?.OnHUDGoUp();

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
