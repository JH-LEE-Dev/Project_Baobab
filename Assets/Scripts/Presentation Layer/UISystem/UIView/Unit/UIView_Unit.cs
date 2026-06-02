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

    [Header("Offset Settings")]
    [SerializeField] private Vector2 interactionYOffset = new Vector2(0.0f, 0.75f);
    [SerializeField] private Vector2 speechBubbleYOffset = new Vector2(0.0f, 0.5f);
    [SerializeField] private float treesYOffset = 1.5f;
    [SerializeField] private float animalsYOffset = 1.5f;

    [Header("Display Settings")]
    [SerializeField] private float hpBarShowDuration = 2.0f;
    [SerializeField] private float hpBarDeadShowDelay = 0.2f;

    private Dictionary<object, HUD_HPBar> activeHpBars = new Dictionary<object, HUD_HPBar>(64);
    private List<HUD_HPBar> hpBarPool = new List<HUD_HPBar>(32);
    private System.Action<HUD_HPBar> returnToPoolAction;


    private UI_InteractionUnit interactionUnit;
    private UI_SpeechBubble speechBubble;

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
        if (null == hpBarPrefab)
            return;

        for (int _i = 0; 32 > _i; _i++)
        {
            HUD_HPBar _bar = CreateNewHPBar();
            
            if (null != _bar)
                hpBarPool.Add(_bar);
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
            speechBubble.Hide();
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
                float _maxHp = _health.GetMaxHealth();
                float _prevRatio = _maxHp > 0.0f ? Mathf.Clamp01(_health.GetPrevHealth() / _maxHp) : 1.0f;
                _newBar.SetOwner(_owner, _prevRatio);
                activeHpBars.Add(_owner, _newBar);
                UpdateHPBarState(_newBar, _health, _bDead, _tf, _yOffset);
            }
        }
    }

    private void UpdateHPBarState(HUD_HPBar _bar, IHealthComponent _health, bool _bDead, Transform _tf, float _yOffset)
    {
        _bar.Setup(_tf.gameObject, _yOffset, hpBarShowDuration);

        float _currentHp = _health.GetCurrentHealth();
        float _maxHp = _health.GetMaxHealth();
        float _ratio = Mathf.Clamp01(_currentHp / _maxHp);
        _bar.UpdateValue(_ratio);
        
        // bDead 타이밍 보완: 실제 체력이 0 이하인 경우도 사망으로 간주
        bool _isDead = (true == _bDead || 0.0f >= _currentHp);

        if (true == _isDead)
        {
            _bar.OnHide(hpBarDeadShowDelay);
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
                interactionUnit.HideInteraction();
        }
    }

    public void InventoryIsFull()
    {
        speechBubble?.Play(1, "가방이 가득 차 있어서 더 이상 아이템을 획득 할 수 없어.\n<로컬라이징 해야 돼>", 3.5f);
    }

    private void AxeDurabilityEmpty()
    {
        speechBubble?.Play(2, "도끼가 파손됐어!!!.\n<로컬라이징 해야 돼>", 3.5f);
    }

    public void ItemCantAcquired_Inventory()
    {

    }
}
