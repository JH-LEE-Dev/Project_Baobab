using System.Collections.Generic;
using PresentationLayer.ObjectSystem;
using UnityEngine;

/// <summary>
/// 유닛(나무, 동물 등)의 상태를 나타내는 UI 요소(HP 바, 차지 바 등)를 관리하는 UIView 클래스입니다.
/// 풀링 시스템을 통해 효율적으로 UI 요소를 생성하고 재사용합니다.
/// </summary>
public class UIView_Unit : UIView
{
    // //외부 의존성
    private ICharacter character;

    // //내부 의존성
    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private GameObject hpBarPrefab;
    [SerializeField] private GameObject chargePrefab;

    [Header("Offset Settings")]
    [SerializeField] private float characterYOffset = 1.5f;
    [SerializeField] private float treesYOffset = 1.5f;
    [SerializeField] private float animalsYOffset = 1.5f;

    [Header("Display Settings")]
    [SerializeField] private float hpBarShowDuration = 2.0f;

    private Dictionary<object, HUD_HPBar> activeHpBars = new Dictionary<object, HUD_HPBar>(64);
    private List<HUD_HPBar> hpBarPool = new List<HUD_HPBar>(32);
    private HUD_ChargeGageBar uiCharge;
    private System.Action<HUD_HPBar> returnToPoolAction;

    // //퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        returnToPoolAction = ReturnHPBarToPool;

        InitHPBarPool();
        InitChargeBar();
    }

    public void SetCharacter(ICharacter _character)
    {
        character = _character;
        BindChargeUIFunction();
    }

    public void TreeGetHit(ITreeObj _treeObj)
    {
        if (null == _treeObj)
            return;

        ProcessUnitHit(_treeObj, _treeObj.health, _treeObj.bDead, _treeObj.GetTransform(), treesYOffset);
    }

    public void AnimalGetHit(IAnimalObj _animalObj)
    {
        if (null == _animalObj)
            return;

        ProcessUnitHit(_animalObj, _animalObj.health, _animalObj.bDead, _animalObj.GetTransform(), animalsYOffset);
    }

    public void WeaponModeChanged(WeaponMode _currentWeaponMode)
    {
        PlayWeaponSwapCool(); 
    }

    public void DependencyInjection(IReadOnlyList<ITreeObj> _trees)
    {
        // NOTE: GameplayUIManager 구조 유지를 위해 정의만 둡니다.
    }

    // //프라이빗 메서드

    private void InitHPBarPool()
    {
        if (null == hpBarPrefab)
            return;

        for (int _i = 0; 32 > _i; _i++)
        {
            HUD_HPBar _bar = CreateNewHPBar();
            
            if (null != _bar)
                hpBarPool.Add(_bar);
        }
    }

    private HUD_HPBar CreateNewHPBar()
    {
        GameObject _obj = Instantiate(hpBarPrefab, null != uiRoot ? uiRoot : this.transform);
        
        if (null == _obj)
            return null;

        HUD_HPBar _bar = _obj.GetComponent<HUD_HPBar>();
        
        if (null != _bar)
            _bar.Initialize();
            
        _obj.SetActive(false);

        return _bar;
    }

    private void ProcessUnitHit(object _owner, IHealthComponent _health, bool _bDead, Transform _tf, float _yOffset)
    {
        if (null == _owner || null == _health || null == _tf)
            return;

        if (true == activeHpBars.TryGetValue(_owner, out HUD_HPBar _bar))
            UpdateHPBarState(_bar, _health, _bDead, _tf, _yOffset);
        else
        {
            if (true == _bDead)
                return;

            HUD_HPBar _newBar = GetHPBarFromPool();
            
            if (null != _newBar)
            {
                _newBar.SetOwner(_owner);
                activeHpBars.Add(_owner, _newBar);
                UpdateHPBarState(_newBar, _health, _bDead, _tf, _yOffset);
            }
        }
    }

    private void UpdateHPBarState(HUD_HPBar _bar, IHealthComponent _health, bool _bDead, Transform _tf, float _yOffset)
    {
        _bar.Setup(_tf.gameObject, _yOffset, hpBarShowDuration);

        float _ratio = _health.GetCurrentHealth() / _health.GetMaxHealth();
        
        if (true == _bDead)
            _ratio = 0.0f;

        _bar.UpdateValue(_ratio);

        if (true == _bDead)
        {
            _bar.OnHide();
            return;
        }

        _bar.TriggerActive(returnToPoolAction);
    }

    private HUD_HPBar GetHPBarFromPool()
    {
        HUD_HPBar _bar = null;

        if (0 < hpBarPool.Count)
        {
            int _lastIndex = hpBarPool.Count - 1;
            _bar = hpBarPool[_lastIndex];
            hpBarPool.RemoveAt(_lastIndex);
        }
        else
            _bar = CreateNewHPBar();

        return _bar;
    }

    private void ReturnHPBarToPool(HUD_HPBar _bar)
    {
        if (null == _bar)
            return;

        if (null != _bar.Owner)
            activeHpBars.Remove(_bar.Owner);

        _bar.OnDespawn();
        hpBarPool.Add(_bar);
    }

    private void InitChargeBar()
    {
        if (null == chargePrefab)
            return;

        GameObject _obj = Instantiate(chargePrefab, null != uiRoot ? uiRoot : this.transform);
        uiCharge = _obj.GetComponent<HUD_ChargeGageBar>();
        
        if (null == uiCharge)
            return;

        uiCharge.Initialize();
        uiCharge.UpdateYOffset(characterYOffset);
        uiCharge.OnHide();
    }

    private void BindChargeUIFunction()
    {
        if (null == character || null == uiCharge)
            return;

        uiCharge.UpdateTargetObj(character.GetTransform().gameObject);

        IRifleComponent _rifle = character.armComponent?.rifleComponent;
        
        if (null == _rifle)
            return;

        _rifle.ReloadStartEvent -= PlayRifleReloadCool;
        _rifle.ReloadStartEvent += PlayRifleReloadCool;
    }

    private void PlayRifleReloadCool()
    {
        if (null == character || null == uiCharge)
            return;

        IStatComponent _stats = character.statComponent;
        
        if (null == _stats)
            return;

        uiCharge.SetCharge(_stats.reloadDuration);
    }

    private void PlayWeaponSwapCool()
    {
        if (null == character || null == uiCharge)
            return;

        IStatComponent _stats = character.statComponent;
        
        if (null == _stats)
            return;

        uiCharge.SetCharge(_stats.weaponChangeCoolTime);
    }

    // //유니티 이벤트 함수

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }
}
