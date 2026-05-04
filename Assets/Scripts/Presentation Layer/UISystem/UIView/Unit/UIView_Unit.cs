using System.Collections.Generic;
using PresentationLayer.ObjectSystem;
using UnityEngine;

public class UIView_Unit : UIView
{
    //외부 의존성
    private IReadOnlyList<ITreeObj> trees;
    private ICharacter character;

    //내부 의존성
    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private GameObject hpBarPrefab;
    [SerializeField] private GameObject chargePrefab;
    private ObjectPools hpBarPool;

    [Header("Offset Settings")]
    [SerializeField] private float characterYOffset = 1.5f;
    [SerializeField] private float treesYOffset = 1.5f;
    [SerializeField] private float animalsYOffset = 1.5f;

    [Header("Other")]
    [SerializeField] private float showCount = 1.5f;

    private Dictionary<ITreeObj, HUD_HPBar> damagedTrees = new Dictionary<ITreeObj, HUD_HPBar>(32);
    private Dictionary<IAnimalObj, HUD_HPBar> damagedAnimals = new Dictionary<IAnimalObj, HUD_HPBar>(32);
    private HUD_ChargeGageBar uiCharge;

    //퍼블릭 초기화 및 제어 메서드

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_HPBarPool();
        Init_ChargeBar();
    }

    private void Init_HPBarPool()
    {
        if (null == hpBarPool)
        {
            hpBarPool = gameObject.AddComponent<ObjectPools>();

            if (null != hpBarPool)
            {
                hpBarPool.Initialize();
                hpBarPool.Prewarm(hpBarPrefab, 32, this.transform);
            }
        }
    }

    public void TreeGetHit(ITreeObj _treeObj)
    {
        if (null == _treeObj)
            return;

        if (damagedTrees.TryGetValue(_treeObj, out HUD_HPBar _bar))
        {
            UpdateTreeHP(_bar, _treeObj);
        }
        else
        {
            HUD_HPBar _newBar = hpBarPool.Spawn<HUD_HPBar>(hpBarPrefab, Vector3.zero, Quaternion.identity, this.transform);
            if (null != _newBar)
            {
                damagedTrees.Add(_treeObj, _newBar);
                UpdateTreeHP(_newBar, _treeObj);
            }
        }
    }

    private void UpdateTreeHP(HUD_HPBar _bar, ITreeObj _treeObj)
    {
        if (null == _bar || null == _treeObj)
            return;

        if (true == _treeObj.bDead)
        {
            _bar.OnHide();
            return;
        }

        _bar.SetOwner(_treeObj);
        _bar.UpdateValue(_treeObj.health.GetCurrentHealth() / _treeObj.health.GetMaxHealth());
        _bar.UpdateYOffset(treesYOffset);
        _bar.UpdateTargetObj(_treeObj.GetTransform().gameObject);
        _bar.TriggerActiveForDuration(showCount, FinishedBar);
    }


    public void DependencyInjection(IReadOnlyList<ITreeObj> _trees)
    {
        trees = _trees;
    }

    public void AnimalGetHit(IAnimalObj _animalObj)
    {
        if (null == _animalObj)
            return;

        if (damagedAnimals.TryGetValue(_animalObj, out HUD_HPBar _bar))
        {
            UpdateAnimalHP(_bar, _animalObj);
        }
        else
        {
            if (true == _animalObj.bDead)
                return;

            HUD_HPBar _newBar = hpBarPool.Spawn<HUD_HPBar>(hpBarPrefab, Vector3.zero, Quaternion.identity, this.transform);
            if (null != _newBar)
            {
                damagedAnimals.Add(_animalObj, _newBar);
                UpdateAnimalHP(_newBar, _animalObj);
            }
        }
    }

    private void UpdateAnimalHP(HUD_HPBar _bar, IAnimalObj _animalObj)
    {
        if (null == _bar || null == _animalObj)
            return;

        if (true == _animalObj.bDead)
        {
            _bar.OnHide();
            return;
        }

        _bar.SetOwner(_animalObj);
        _bar.UpdateValue(_animalObj.health.GetCurrentHealth() / _animalObj.health.GetMaxHealth());
        _bar.UpdateYOffset(animalsYOffset);
        _bar.UpdateTargetObj(_animalObj.GetTransform().gameObject);
        _bar.TriggerActiveForDuration(showCount, FinishedAnimalBar);
    }


    private void FinishedAnimalBar(HUD_HPBar _bar)
    {
        if (null == _bar)
            return;

        ClearBarFromDictionaries(_bar);
        hpBarPool?.Despawn(_bar.gameObject);
    }



    public void SetCharacter(ICharacter _character)
    {
        character = _character;

        Bind_ChargeUIFunction();
    }

    private void FinishedBar(HUD_HPBar _bar)
    {
        if (null == _bar)
            return;

        ClearBarFromDictionaries(_bar);
        hpBarPool?.Despawn(_bar.gameObject);
    }


    private void ClearBarFromDictionaries(HUD_HPBar _bar)
    {
        if (null == _bar)
            return;

        if (null == _bar.Owner)
            return;

        if (_bar.Owner is ITreeObj _tree)
            damagedTrees.Remove(_tree);

        if (_bar.Owner is IAnimalObj _animal)
            damagedAnimals.Remove(_animal);
    }



    //무기 모드 변환 시 호출. 기본값은 Axe
    public void WeaponModeChanged(WeaponMode _currentWeaponMode)
    {
        weaponSwapCoolPlay(); 
    }

    private void Init_ChargeBar()
    {
        if (null == chargePrefab)
            return;

        uiCharge = Instantiate(chargePrefab, Vector3.zero, Quaternion.identity, uiRoot).GetComponent<HUD_ChargeGageBar>();
        if (null == uiCharge)
            return;

        uiCharge.Initialize();
        uiCharge.UpdateYOffset(characterYOffset);
        uiCharge.OnHide();
    }

    private void Bind_ChargeUIFunction()
    {
        if (null == character || null == uiCharge)
            return;

        uiCharge.UpdateTargetObj(character.GetTransform().gameObject);

        IRifleComponent _rifle = character.armComponent?.rifleComponent;
        if (null == _rifle)
            return;

        _rifle.ReloadStartEvent -= RifleReloadCoolPlay;
        _rifle.ReloadStartEvent += RifleReloadCoolPlay;
    }

    private void RifleReloadCoolPlay()
    {
        if (null == character || null == uiCharge)
            return;

        IStatComponent _stats = character.statComponent;
        if (null == _stats)
            return;

        uiCharge.SetCharge(_stats.reloadDuration);
    }

    private void weaponSwapCoolPlay()
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

    public override void Update()
    {
        base.Update();

        // HP 바 위치 추적 등 추가 로직 필요 시 여기에 작성
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
    }
}

