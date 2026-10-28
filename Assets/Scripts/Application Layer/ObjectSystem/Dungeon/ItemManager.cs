using System;
using UnityEngine;

public class ItemManager : MonoBehaviour
{
    // 내부 의존성
    public LogItemController logItemController { get; private set; }
    public CarrotItemController carrrotItemController { get; private set; }
    public GemOreItemController gemOreItemController { get; private set; }
    // 4스테이지 불씨. 인벤토리를 거치지 않는다(주우면 과열 수치가 오른다).
    public EmberItemController emberItemController { get; private set; }
    private IInventoryChecker inventoryChecker;
    private ICharacter character;

    public void Initialize(IInventoryChecker _inventoryChecker, ICharacter _character, IEnvironmentProvider _environmentProvider)
    {
        inventoryChecker = _inventoryChecker;
        character = _character;

        logItemController = GetComponentInChildren<LogItemController>();
        carrrotItemController = GetComponentInChildren<CarrotItemController>();
        gemOreItemController = GetComponentInChildren<GemOreItemController>();
        emberItemController = GetComponentInChildren<EmberItemController>();

        if (emberItemController != null)
        {
            emberItemController.Initialize(character, _environmentProvider);
        }

        if (logItemController != null)
        {
            logItemController.Initialize(inventoryChecker, character, _environmentProvider);
        }

        if (gemOreItemController != null)
        {
            gemOreItemController.Initialize(character, _environmentProvider.tilemapDataProvider, inventoryChecker);
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
        emberItemController?.SetCharacter(_character);
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
            // 던전 준비 시점에 원목 풀을 미리 채운다(전투 중 대량 벌목 프레임의 Instantiate를 줄인다).
            logItemController.PrewarmPool();
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
    ///
    /// <b>[죽은 코드]</b> SYSTEM_VAR.GEM_ORE_SYSTEM_ENABLED 가 false 라 유일한 호출부인
    /// InDungeonObjectManager.OnTreeDead 가 이 길로 들어오지 않는다. 버그 검토 대상이 아니다.
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

    // ── 4스테이지 불씨 (EmberItemController) ──

    public void BeginEmberRun(MapType _mapType, bool _bStageGimmicksDisabled, Func<bool> _canSpawnMore)
    {
        emberItemController?.BeginRun(_mapType, _bStageGimmicksDisabled, _canSpawnMore);
    }

    public void SpawnInitialEmbers(System.Collections.Generic.IReadOnlyList<TreeObj> _trees)
    {
        emberItemController?.SpawnInitialEmbers(_trees);
    }

    public void SpawnEmberDrop(Vector3 _treePosition)
    {
        emberItemController?.SpawnDropEmbers(_treePosition);
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

    public event Action<EmberItem> EmberItemAcquiredEvent
    {
        add { if (emberItemController != null) emberItemController.EmberAcquiredEvent += value; }
        remove { if (emberItemController != null) emberItemController.EmberAcquiredEvent -= value; }
    }

    public void ReleaseAllItems()
    {
        //carrrotItemController.ClearAll();
        logItemController.ClearAll();
        gemOreItemController?.ClearAll();
        // 원정이 끝났으니 불씨도 치우고 다음 원정(BeginEmberRun)까지 자연 생성을 멈춘다.
        emberItemController?.EndRun();
    }

    public void CancelActiveSucking()
    {
        logItemController?.CancelActiveSucking();
        gemOreItemController?.CancelActiveSucking();
        emberItemController?.CancelActiveSucking();
    }
}
