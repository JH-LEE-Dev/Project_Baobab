using System;
using UnityEngine;
using PresentationLayer.UISystem.UIView.HUD.Equipment;
using  PresentationLayer.UISystem.UIView.HUD.DirectionalIndicator;
using PresentationLayer.DOTweenAnimationSystem;

public class UIView_HUD : UIView
{
    [Header("UI References")]
    [SerializeField] private Transform uiRoot; //일단 에디터에서 자기 자신 넣으면 됨.
    [SerializeField] private GameObject hudEquipmentPrefab;
    [SerializeField] private GameObject hudSteminaBarPrefab;
    [SerializeField] private GameObject hudDirectionalIndicatorPrefab;
    [SerializeField] private GameObject hudScreenBloodPrefab;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private string mapTransitionMotionTag = "GoDown";

    private HUD_Equipment hudEquipment;
    private HUD_Stemina hudSteminaBar;
    private HUD_DirIndicator hudDirIndicator;
    private HUD_ScreenBlood hudScreenBlood;

    private ICharacter character;

    private MapType currentMapType;
    private ForestType currentForestType;

    #region Default Logic

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        if (null != omp)
            omp.Initialize();

        currentMapType = MapType.Town;

        Init_HUDScreenBlood();
        Init_HUDDirIndicator();
        Init_HUDSteminaBar();
        Init_HUDEquipment();

        bool isTown = MapType.Town == currentMapType;

        ChangedActiveStateEquipment(isTown);
        ChangedActiveStateStemina(isTown);
    }

    public override void OnDestroy()
    {
        hudEquipment?.OnDestroy();
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

    public void DependencyInjection()
    {

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
        hudEquipment = Instantiate(hudEquipmentPrefab, uiRoot.transform).GetComponent<HUD_Equipment>();

        if (null != hudEquipment)
        {
            hudEquipment.Initialize();
        }
    }

    #endregion

    #region HUD_Stemina Logic

    private void Init_HUDScreenBlood()
    {
        Canvas _rootCanvas = GetComponentInParent<Canvas>();
        Transform _parent = (null != _rootCanvas) ? _rootCanvas.transform : transform;

        hudScreenBlood = Instantiate(hudScreenBloodPrefab, _parent).GetComponent<HUD_ScreenBlood>();

        if (null != hudScreenBlood)
        {
            hudScreenBlood.Initialize();
            hudScreenBlood.transform.SetAsLastSibling();
        }
    }

    private void Init_HUDSteminaBar()
    {
        hudSteminaBar = Instantiate(hudSteminaBarPrefab, uiRoot.transform).GetComponent<HUD_Stemina>();

        if (null != hudSteminaBar)
        {
            hudSteminaBar.Initialize(hudScreenBlood);
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

    private void UsedSteminaEvent(float _currentStemina, float _maxStemina)
    {
        float newRatio = _currentStemina / _maxStemina;
        hudSteminaBar?.UpdateValue(Mathf.Clamp01(newRatio));
    }

    #endregion

    //무기 모드 변환 시 호출. 기본값은 Axe
    public void WeaponModeChanged(WeaponMode _currentWeaponMode, bool _isMapChanged = false)
    {
      
    }


    public void InventorySpecChanged() //인벤토리 스펙 변동 시 호출
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

        if (false == bTown)
        {
            hudEquipment?.ResetAllMotions();
            hudDirIndicator?.OnShow();
        }
        else
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
        if (null == hudSteminaBar)
            return;

        hudSteminaBar.SetActivate(!_isTwon);
    }

    public void OffroadSpawned(IOffroadProvider _offroadProvider)
    {
        hudDirIndicator?.SetTarget(_offroadProvider.transform);
    }

    public void HUDGoDown()
    {
        hudDirIndicator?.OnHide();

        if (null != hudScreenBlood)
            hudScreenBlood.ResetAnimation(false);

        if (null != omp)
        {
            omp.Play(mapTransitionMotionTag, bReset: true);
        }
    }

    public void HUDGoUp()
    {
        if (null != omp)
        {
            omp.PlayBackward(mapTransitionMotionTag, bReset: true);
        }

        if (MapType.Town != currentMapType)
        {
            hudDirIndicator?.OnShow();
        }
    }
}
