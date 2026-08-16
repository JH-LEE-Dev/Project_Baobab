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
    private int lastInventoryFullFrame = -1;

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
                axeComponent.DurabilityRestoredEvent -= AxeDurabilityRestored;
                axeComponent.DurabilityRestoredEvent += AxeDurabilityRestored;
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
        {
            InputManager _inputMgr = null != viewCtx ? viewCtx.inputManager : null;
            interactionUnit.Initialize(_inputMgr);
        }
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

    public void LootPillarInteractStateChanged(bool _state)
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
        lastInventoryFullFrame = Time.frameCount;
        int id = (int)ESpeechBubbleId.InventoryFull;
        SpeechBubblePlay(id, viewCtx.localizationManager.GetText(speechBubbleJsonId, id));
        speechBubble.AddShownId((int)ESpeechBubbleId.ItemCantAcquired);
    }

    public void ItemCantAcquired_Inventory()
    {
        if (Time.frameCount == lastInventoryFullFrame)
            return;

        int id = (int)ESpeechBubbleId.ItemCantAcquired;
        SpeechBubblePlay(id, viewCtx.localizationManager.GetText(speechBubbleJsonId, id));
        speechBubble.AddShownId((int)ESpeechBubbleId.InventoryFull);
    }

    private void AxeDurabilityEmpty()
    {
        int id = (int)ESpeechBubbleId.AxeDurabilityEmpty;
        SpeechBubblePlay(id, viewCtx.localizationManager.GetText(speechBubbleJsonId, id));
    }

    private void AxeDurabilityRestored()
    {
        if (null != speechBubble)
        {
            speechBubble.RemoveShownId((int)ESpeechBubbleId.AxeDurabilityEmpty);
        }
    }

    private void SpeechBubblePlay(int _id, string _text)
    {
        if (null != speechBubble)
            speechBubble.Play(_id, _text, speechBubbleDuration);
    }

    public void TownStarted()
    {
        if (null != speechBubble)
        {
            speechBubble.ResetSpeechBubble();
            speechBubble.SetLockEnabled(false);
        }
    }

    public void DungeonStarted()
    {
        if (null != speechBubble)
        {
            speechBubble.ResetSpeechBubble();
            speechBubble.SetLockEnabled(true);
        }
    }

    public void InventoryItemToOffroadContainer()
    {
        if (null != speechBubble)
        {
            speechBubble.RemoveShownId((int)ESpeechBubbleId.InventoryFull);
            speechBubble.RemoveShownId((int)ESpeechBubbleId.ItemCantAcquired);
        }
    }

    /// <summary>
    /// 던전 진입 연출(차량 시동 꺼짐 → 캐릭터 하차)이 끝나고 실제로 조작 가능해지는 시점에 호출된다.
    /// CompleteDungeonEntrySignal(InDungeonSystem에서 발행) 수신 시 GameplayUICoordinator를 통해 호출됨.
    /// </summary>
    public void CompleteDungeonEntry()
    {
        interactionUnit?.ShowTutorialKey(TutorialKeyType.Move);
    }

    /// <summary>
    /// 튜토리얼 중 플레이어가 첫 나무를 벌목(CutTree 스텝 완료)한 즉시 호출된다.
    /// GameplayUICoordinator.TutorialStepCompleted(TutorialStepCompletedSignal)에서 step이 CutTree일 때 호출됨.
    /// </summary>
    public void TutorialOffroadResultUIOpened()
    {
        interactionUnit?.HideAllTutorialKeys();
    }

    /// <summary>
    /// 튜토리얼 진행 중(GameplayUICoordinator.bIsTutorialActive)에만 호출된다. AttackComponent의
    /// 공격 범위 안에 나무가 하나도 없다가 처음 감지된 시점에 한 번만 호출되며, 감지된 나무가
    /// 다른 나무로 바뀌거나 매 탐지 주기(0.2초)마다 호출되지는 않는다.
    /// </summary>
    public void TreeDetected()
    {
        interactionUnit?.ShowTutorialKey(TutorialKeyType.Attack);
    }

    /// <summary>
    /// TreeDetected()로 감지가 통지된 뒤, 공격 범위에 감지되어 있던 나무가 전부 사라진 시점에
    /// 한 번만 호출된다(TreeDetected()와 항상 쌍으로 호출됨).
    /// </summary>
    public void TreeDetectionCleared()
    {
        interactionUnit?.ShowTutorialKey(TutorialKeyType.Move);
    }
}
