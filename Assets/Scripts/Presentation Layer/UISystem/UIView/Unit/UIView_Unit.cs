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
    [SerializeField] private GameObject interactionUnitPrefab;
    [SerializeField] private GameObject speechBubbleUnitPrefab;
    [SerializeField] private float speechBubbleDuration = 3.5f;

    [Header("Offset Settings")]
    [SerializeField] private Vector2 interactionYOffset = new Vector2(0.0f, 0.75f);
    [SerializeField] private Vector2 speechBubbleYOffset = new Vector2(0.0f, 0.5f);
    [SerializeField] private float treesYOffset = 1.5f;
    [SerializeField] private float animalsYOffset = 1.5f;

    [Header("Display Settings")]
    [SerializeField] private float hpBarShowDuration = 2.0f;
    [SerializeField] private float hpBarDeadShowDelay = 0.2f;

    [Header("Localization Settings")]
    [SerializeField] private int speechBubbleJsonId = 5;

    private Dictionary<object, HUD_ShieldHPBar> activeHpBars = new Dictionary<object, HUD_ShieldHPBar>(64);
    private List<HUD_ShieldHPBar> hpBarPool = new List<HUD_ShieldHPBar>(32);
    private System.Action<HUD_ShieldHPBar> returnToPoolAction;


    private UI_InteractionUnit interactionUnit;
    private UI_SpeechBubble speechBubble;

    //private bool isInitialOpen = false;

    // //퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        returnToPoolAction = ReturnHPBarToPool;

        InitHPBarPool();
        InitInteractionUnit();
        InitSpeechBubbleUnit();
    }

    public void SetCharacter(ICharacter _character)
    {
        character = _character;

        if (null != character)
        {
            IAxeComponent axeComponent = character.armComponent?.axeComponent;
            if (null != axeComponent)
            {
                axeComponent.DurabilityEmptyEvent -= AxeDurabilityEmpty;
                axeComponent.DurabilityEmptyEvent += AxeDurabilityEmpty;
            }

            interactionUnit?.SetTarget(character.GetTransform(), interactionYOffset);
            speechBubble?.SetTarget(character.GetTransform(), speechBubbleYOffset);
        }
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

    /// <summary>
    /// 나무의 쉴드 회복 중 지속적으로 호출하여 HP Bar를 갱신하고 노출 시간을 유지시킵니다.
    /// </summary>
    public void TreeShieldRecovering(ITreeObj _treeObj)
    {
        if (null == _treeObj)
            return;

        ProcessUnitHit(_treeObj, _treeObj.health, false, _treeObj.GetTransform(), treesYOffset);
    }

    public void WeaponModeChanged(WeaponMode _currentWeaponMode)
    {
    }

    public void DependencyInjection(IReadOnlyList<ITreeObj> _trees)
    {
        // NOTE: GameplayUIManager 구조 유지를 위해 정의만 둡니다.
    }

    // //프라이빗 메서드

    private void InitHPBarPool()
    {
        if (null == hpBarPrefab || null == hpBarPool)
            return;

        for (int _i = 0; 32 > _i; _i++)
        {
            HUD_ShieldHPBar _bar = CreateNewHPBar();

            if (null != _bar)
            {
                hpBarPool.Add(_bar);
                _bar.OnHide(_bSkip: true);
            }
        }
    }

    private void InitInteractionUnit()
    {
        if (null == interactionUnitPrefab)
            return;

        interactionUnit = Instantiate(interactionUnitPrefab, uiRoot.transform).GetComponent<UI_InteractionUnit>();

        if (null != interactionUnit)
            interactionUnit.Initialize();
    }

    private void InitSpeechBubbleUnit()
    {
        if (null == speechBubbleUnitPrefab)
            return;

        speechBubble = Instantiate(speechBubbleUnitPrefab, uiRoot.transform).GetComponent<UI_SpeechBubble>();

        if (null != speechBubble)
        {
            speechBubble.Initialize();
            speechBubble.Hide(true);
        }
    }

    private HUD_ShieldHPBar CreateNewHPBar()
    {
        GameObject _obj = Instantiate(hpBarPrefab, null != uiRoot ? uiRoot : this.transform);
        
        if (null == _obj)
            return null;

        HUD_ShieldHPBar _bar = _obj.GetComponent<HUD_ShieldHPBar>();
        
        if (null != _bar)
            _bar.Initialize();
            
        _obj.SetActive(false);

        return _bar;
    }

    private void ProcessUnitHit(object _owner, IHealthComponent _health, bool _bDead, Transform _tf, float _yOffset)
    {
        if (null == _owner || null == _health || null == _tf)
            return;

        if (true == activeHpBars.TryGetValue(_owner, out HUD_ShieldHPBar _bar))
            UpdateHPBarState(_bar, _health, _bDead, _tf, _yOffset);
        else
        {
            if (true == _bDead)
                return;

            HUD_ShieldHPBar _newBar = GetHPBarFromPool();
            
            if (null != _newBar)
            {
                float _maxHp = _health.GetMaxHealth();
                float _prevRatio = 0.0f < _maxHp ? Mathf.Clamp01(_health.GetPrevHealth() / _maxHp) : 1.0f;
                float _maxSp = _health.GetMaxSP();
                float _prevSpRatio = 0.0f < _maxSp ? Mathf.Clamp01(_health.GetPrevSP() / _maxSp) : 0.0f;
                bool _useShield = 0.0f < _maxSp;

                _newBar.SetOwner(_owner, _prevRatio, _prevSpRatio, _useShield);
                activeHpBars.Add(_owner, _newBar);
                UpdateHPBarState(_newBar, _health, _bDead, _tf, _yOffset);
            }
        }
    }

    private void UpdateHPBarState(HUD_ShieldHPBar _bar, IHealthComponent _health, bool _bDead, Transform _tf, float _yOffset)
    {
        _bar.Setup(_tf.gameObject, _yOffset, hpBarShowDuration);

        float _currentHp = _health.GetCurrentHealth();
        float _maxHp = _health.GetMaxHealth();
        float _ratio = 0.0f < _maxHp ? Mathf.Clamp01(_currentHp / _maxHp) : 0.0f;
        
        float _currentSp = _health.GetCurrentSP();
        float _maxSp = _health.GetMaxSP();
        float _spRatio = 0.0f < _maxSp ? Mathf.Clamp01(_currentSp / _maxSp) : 0.0f;

        _bar.UpdateValues(_ratio, _spRatio);
        
        // bDead 타이밍 보완: 실제 체력이 0 이하인 경우도 사망으로 간주
        bool _isDead = (true == _bDead || 0.0f >= _currentHp);

        if (true == _isDead)
        {
            _bar.OnHide(hpBarDeadShowDelay);
            return;
        }

        _bar.TriggerActive(returnToPoolAction);
    }

    private HUD_ShieldHPBar GetHPBarFromPool()
    {
        HUD_ShieldHPBar _bar = null;

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

    private void ReturnHPBarToPool(HUD_ShieldHPBar _bar)
    {
        if (null == _bar)
            return;

        if (null != _bar.Owner)
            activeHpBars.Remove(_bar.Owner);

        _bar.OnDespawn();
        hpBarPool.Add(_bar);
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

    public void LogContainerInteractStateChanged(bool _state)
    {
        InteractionStateChange(_state);
    }

    public void OffroadContainerInteractStateChanged(bool _state)
    {
        InteractionStateChange(_state);
    }

    public void TentInteractStateChanged(bool _state)
    {
        InteractionStateChange(_state);
    }

    public void OffroadInteractStateChanged(bool _state)
    {
        InteractionStateChange(_state);
    }

    public void RepairBoxInteractStateChanged(bool _state)
    {
        InteractionStateChange(_state);
    }

    public void ShopInteractStateChanged(bool _state)
    {
        InteractionStateChange(_state);
    }

    private void InteractionStateChange(bool _state)
    {
        if (true == _state)
        {
            if (null != interactionUnit)
                interactionUnit.ShowInteraction();
        }
        else
        {
            if (null != interactionUnit)
            {
                bool _stopFollowing = (null != character && true == character.bRide);
                interactionUnit.HideInteraction(_bSkip: false, _stopFollowing: _stopFollowing);
            }
        }
    }

    public void InventoryIsFull()
    {
        int id = 1;
        SpeechBubblePlay(id, viewCtx.localizationManager.GetText(speechBubbleJsonId, id));
        speechBubble.AddShownId(2);
    }

    public void ItemCantAcquired_Inventory()
    {
        int id = 2;
        SpeechBubblePlay(id, viewCtx.localizationManager.GetText(speechBubbleJsonId, id));
        speechBubble.AddShownId(1);
    }

    private void AxeDurabilityEmpty()
    {
        int id = 3;
        SpeechBubblePlay(id, viewCtx.localizationManager.GetText(speechBubbleJsonId, id));
    }

    private void SpeechBubblePlay(int _id, string _text)
    {
        if (null != speechBubble)
            speechBubble.Play(_id, _text, speechBubbleDuration);
    }

    public void TownStarted()
    {
        if (null != speechBubble)
            speechBubble.RemoveAllShownIds();
    }

    public void InventoryItemToOffroadContainer()
    {
        if (null != speechBubble)
        {
            speechBubble.RemoveShownId(1);
            speechBubble.RemoveShownId(2);
        }
    }
}
