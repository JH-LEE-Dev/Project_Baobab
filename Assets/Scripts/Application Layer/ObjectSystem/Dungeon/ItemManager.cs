using System;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    // 내부 의존성
    public LogItemController logItemController { get; private set; }
    public CarrotItemController carrrotItemController { get; private set; }
    public GemOreItemController gemOreItemController { get; private set; }
    private IInventoryChecker inventoryChecker;
    private ICharacter character;

    public void Initialize(IInventoryChecker _inventoryChecker, ICharacter _character, ITilemapDataProvider _tilemapDataProvider)
    {
        inventoryChecker = _inventoryChecker;
        character = _character;

        logItemController = GetComponentInChildren<LogItemController>();
        carrrotItemController = GetComponentInChildren<CarrotItemController>();
        gemOreItemController = GetComponentInChildren<GemOreItemController>();

        if (logItemController != null)
        {
            logItemController.Initialize(inventoryChecker, character, _tilemapDataProvider);
        }

        if (gemOreItemController != null)
        {
            gemOreItemController.Initialize(character, _tilemapDataProvider, inventoryChecker);
        }

        BindEvents();
    }

    /// <summary>
    /// Initialize()는 캐릭터가 스폰되기 전에 호출되므로 그때 넘어오는 _character는 항상 null이다.
    /// 캐릭터 스폰 이후(InDungeonObjectManager.SetCharacter) 여기로 뒤늦게 주입받아, 이후 스폰되는
    /// LogItem들이 올바른 캐릭터 참조를 갖도록 한다.
    /// </summary>
    public void SetCharacter(ICharacter _character)
    {
        character = _character;

        logItemController?.SetCharacter(_character);
        gemOreItemController?.SetCharacter(_character);
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void BindEvents()
    {

    }

    private void ReleaseEvents()
    {

    }

    public void SetupCulling()
    {
        if (logItemController != null)
        {
            logItemController.SetupCullingGroup();
        }

        if (carrrotItemController != null)
        {
            carrrotItemController.SetupCullingGroup();
        }

        if (gemOreItemController != null)
        {
            gemOreItemController.SetupCullingGroup();
        }
    }

    // 외부에서 접근하기 위한 래퍼 메서드 (필요한 경우)
    public void SpawnLogItem(TreeObj _treeObj, float _multiplier)
    {
        logItemController?.SpawnLogItem(_treeObj, _multiplier);
    }

    public void ReturnLogToPool(LogItem _item)
    {
        logItemController?.ReturnToPool(_item);
    }

    /// <summary>
    /// 보석 나무가 쓰러진 자리에 원석을 뿌린다. 원목 대신 떨어지므로 SpawnLogItem과는 배타적으로 호출한다.
    /// </summary>
    public void SpawnGemOre(TreeObj _treeObj, float _multiplier)
    {
        if (gemOreItemController == null) return;

        // 잭팟 스킬은 LogItemController 하나에만 걸리므로(ILogItemCH), 그 값을 그대로 읽어 넘긴다.
        // 보석 나무는 원목 대신 원석을 주기 때문에, 전달하지 않으면 그 나무에서만 스킬이 조용히 무력해진다.
        float jackPotChance = logItemController != null ? logItemController.JackPotChance : 0f;
        float jackPotAmount = logItemController != null ? logItemController.JackPotAmount : 1f;

        gemOreItemController.SpawnGemOre(_treeObj, _multiplier, jackPotChance, jackPotAmount);
    }

    public void ReturnGemOreToPool(GemOreItem _item)
    {
        gemOreItemController?.ReturnToPool(_item);
    }

    public void SpawnCarrotItem(Vector3 _position, AnimalType _animalType)
    {
        carrrotItemController?.SpawnCarrotItem(_position, _animalType);
    }

    public void ReturnCarrotToPool(CarrotItem _item)
    {
        carrrotItemController?.ReturnToPool(_item);
    }

    // 이벤트 구독을 위한 프로퍼티 중계
    public event Action<Item> LogItemAcquiredEvent
    {
        add { if (logItemController != null) logItemController.LogItemAcquiredEvent += value; }
        remove { if (logItemController != null) logItemController.LogItemAcquiredEvent -= value; }
    }

    public event Action<CarrotItem> CarrotItemAcquiredEvent
    {
        add { if (carrrotItemController != null) carrrotItemController.CarrotItemAcquiredEvent += value; }
        remove { if (carrrotItemController != null) carrrotItemController.CarrotItemAcquiredEvent -= value; }
    }

    public event Action<GemOreItem> GemOreItemAcquiredEvent
    {
        add { if (gemOreItemController != null) gemOreItemController.GemOreItemAcquiredEvent += value; }
        remove { if (gemOreItemController != null) gemOreItemController.GemOreItemAcquiredEvent -= value; }
    }

    public void ReleaseAllItems()
    {
        //carrrotItemController.ClearAll();
        logItemController.ClearAll();
        gemOreItemController?.ClearAll();
    }

    public void CancelActiveSucking()
    {
        logItemController?.CancelActiveSucking();
        gemOreItemController?.CancelActiveSucking();
    }
}
