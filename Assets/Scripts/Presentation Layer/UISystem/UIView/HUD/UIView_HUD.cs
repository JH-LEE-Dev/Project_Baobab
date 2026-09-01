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

    private MapType currentMapType;
    private ForestType currentForestType;

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
        character = _character;

        hudEquipment?.BindingRef(character);
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
        }
    }

    public override void Update()
    {
        if (null != character && null != character.pHealthComponent)
        {
            UsedSteminaEvent(character.pHealthComponent.GetCurrentStamina(), character.pHealthComponent.GetMaxStamina());
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
        hudStaminaBar = Instantiate(hudSteminaBarPrefab, moveHUD.transform).GetComponent<HUD_Stemina>();

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
            hudLoot = Instantiate(hudLootPrefab, uiRoot.transform).GetComponent<HUD_Loot>();

        if (null != hudLoot)
            hudLoot.Initialize(viewCtx?.inputManager);
    }



    private void UsedSteminaEvent(float _currentStemina, float _maxStemina)
    {
        float newRatio = _currentStemina / _maxStemina;
        hudStaminaBar?.UpdateValue(Mathf.Clamp01(newRatio));
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

        hudLoot?.OnHUDGoUp();

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
