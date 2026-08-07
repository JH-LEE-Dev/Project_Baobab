using UnityEngine;

/// <summary>
/// 튜토리얼 퀘스트가 시작되면 해당 퀘스트의 목표 오브젝트 위에 화살표 인디케이터를 띄우고,
/// 퀘스트가 완료되면 내린다. 퀘스트 진행 자체는 TutorialSystem이 담당하고, 여기서는
/// TutorialStepStarted/Completed 신호를 받아 표시만 한다.
///
/// 나무 벌목(CutTree)은 대상이 특정 오브젝트가 아니라 맵 전체의 나무이므로 인디케이터를 띄우지 않는다.
/// </summary>
public class TutorialQuestIndicatorManager : MonoBehaviour
{
    // 내부 의존성
    [Header("Prefab")]
    [Tooltip("화살표 인디케이터 프리팹. 스프라이트는 프리팹 안의 SpriteRenderer에 바인딩한다.")]
    [SerializeField] private QuestIndicator indicatorPrefab;

    [Header("Offsets (대상 오브젝트 원점 기준 월드 오프셋)")]
    [Tooltip("FillOffroadContainer - 던전의 원목 운반 상자 / PutItemsInLogContainer 시작 직후 - 마을의 같은 운반 상자")]
    [SerializeField] private Vector3 offroadContainerOffset = new Vector3(0f, 2.5f, 0f);
    [Tooltip("GoHomeBeforeExhausted - 던전의 차량")]
    [SerializeField] private Vector3 dungeonVehicleOffset = new Vector3(0f, 3f, 0f);
    [Tooltip("PutItemsInLogContainer - 마을 OffroadContainer에서 원목을 인출한 뒤 가리킬 제재소 원목 보관함")]
    [SerializeField] private Vector3 logContainerOffset = new Vector3(0f, 2.5f, 0f);
    [Tooltip("ReceiveMoney - 상점 NPC")]
    [SerializeField] private Vector3 shopNPCOffset = new Vector3(0f, 2f, 0f);
    [Tooltip("UpgradeAxe - 집(텐트)")]
    [SerializeField] private Vector3 homeOffset = new Vector3(0f, 3.5f, 0f);
    [Tooltip("StartNewLogging - 마을의 차량")]
    [SerializeField] private Vector3 townVehicleOffset = new Vector3(0f, 3f, 0f);

    // 외부 의존성
    private SignalHub signalHub;
    private OffroadContainer offroadContainer;
    private InDungeonObjectManager inDungeonObjectManager;
    private TownObjectManager townObjectManager;
    private LogProcessingManager logProcessingManager;
    private TentManager tentManager;

    // 내부 상태
    private QuestIndicator indicator;
    private TutorialStep currentStep;

    public void Initialize(SignalHub _signalHub, OffroadContainer _offroadContainer,
        InDungeonObjectManager _inDungeonObjectManager, TownObjectManager _townObjectManager,
        LogProcessingManager _logProcessingManager, TentManager _tentManager)
    {
        signalHub = _signalHub;
        offroadContainer = _offroadContainer;
        inDungeonObjectManager = _inDungeonObjectManager;
        townObjectManager = _townObjectManager;
        logProcessingManager = _logProcessingManager;
        tentManager = _tentManager;

        SubscribeSignals();
    }

    public void Release()
    {
        UnSubscribeSignals();

        if (null != indicator)
        {
            indicator.HideImmediately();
        }
    }

    private void SubscribeSignals()
    {
        if (null == signalHub) return;

        signalHub.Subscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.Subscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
        signalHub.Subscribe<ItemAddedToInventorySignal>(ItemAddedToInventory);
    }

    private void UnSubscribeSignals()
    {
        if (null == signalHub) return;

        signalHub.UnSubscribe<TutorialStepStartedSignal>(TutorialStepStarted);
        signalHub.UnSubscribe<TutorialStepCompletedSignal>(TutorialStepCompleted);
        signalHub.UnSubscribe<ItemAddedToInventorySignal>(ItemAddedToInventory);
    }

    private void TutorialStepStarted(TutorialStepStartedSignal _signal)
    {
        currentStep = _signal.step;

        Transform _target = GetTargetTransform(_signal.step);

        // 대상이 없는 스텝(CutTree)이거나 아직 대상 오브젝트가 생성되지 않았다면 띄우지 않는다.
        if (null == _target)
        {
            HideIndicator();
            return;
        }

        ShowIndicator(_target, GetOffset(_signal.step));
    }

    private void TutorialStepCompleted(TutorialStepCompletedSignal _signal)
    {
        HideIndicator();
    }

    // PutItemsInLogContainer 시작 시엔 마을 OffroadContainer를 먼저 가리키다가, 캐릭터가 거기서 실제로
    // 원목을 인출하는 순간(ItemAddedToInventorySignal은 OffroadContainer.AddToCharacterInventory에서만
    // 발행되므로 이 상황을 정확히 짚어낸다) 다음 목적지인 LogContainer로 화살표를 옮긴다.
    private void ItemAddedToInventory(ItemAddedToInventorySignal _signal)
    {
        if (currentStep != TutorialStep.PutItemsInLogContainer) return;

        if (null == logProcessingManager || null == logProcessingManager.logContainer) return;

        ShowIndicator(logProcessingManager.logContainer.transform, logContainerOffset);
    }

    /// <summary>
    /// 스텝별 목표 오브젝트. 차량/보관함 등은 런타임에 생성되므로 매번 현재 참조를 다시 가져온다.
    /// </summary>
    private Transform GetTargetTransform(TutorialStep _step)
    {
        switch (_step)
        {
            // 맵 전체의 나무가 대상이라 인디케이터를 띄우지 않는다.
            case TutorialStep.CutTree:
                return null;

            case TutorialStep.FillOffroadContainer:
                return GetOffroadContainerVisualTransform();

            case TutorialStep.GoHomeBeforeExhausted:
                return null != inDungeonObjectManager && null != inDungeonObjectManager.offroadVehicle
                    ? inDungeonObjectManager.offroadVehicle.transform
                    : null;

            // 시작 시점엔 아직 캐릭터 인벤토리가 비어 있으므로, 원목을 실제로 들고 있는 마을
            // OffroadContainer를 먼저 가리킨다. LogContainer로의 전환은 ItemAddedToInventory에서 처리한다.
            case TutorialStep.PutItemsInLogContainer:
                return GetOffroadContainerVisualTransform();

            case TutorialStep.ReceiveMoney:
                return null != logProcessingManager && null != logProcessingManager.shopNPC
                    ? logProcessingManager.shopNPC.transform
                    : null;

            case TutorialStep.UpgradeAxe:
                return null != tentManager ? tentManager.TentTransform : null;

            case TutorialStep.StartNewLogging:
                return null != townObjectManager && null != townObjectManager.offroadVehicle
                    ? townObjectManager.offroadVehicle.transform
                    : null;
        }

        return null;
    }

    /// <summary>
    /// OffroadContainer.GetTransform()은 위치 동기화용 로직 트랜스폼일 뿐 하위에 SpriteRenderer가
    /// 없다(실제 상자 스프라이트는 OffroadContainerVComponent 쪽에 있다). 인디케이터의 반짝임 효과가
    /// 렌더러를 찾으려면 반드시 GetVisualTransform()을 써야 하므로, 아직 세팅 전(null)인 극히 이른
    /// 타이밍 대비로만 GetTransform()에 폴백한다.
    /// </summary>
    private Transform GetOffroadContainerVisualTransform()
    {
        if (null == offroadContainer) return null;

        Transform _visual = offroadContainer.GetVisualTransform();
        return null != _visual ? _visual : offroadContainer.GetTransform();
    }

    private Vector3 GetOffset(TutorialStep _step)
    {
        switch (_step)
        {
            case TutorialStep.FillOffroadContainer: return offroadContainerOffset;
            case TutorialStep.GoHomeBeforeExhausted: return dungeonVehicleOffset;
            // 시작 시점의 타겟(마을 OffroadContainer)에 대한 오프셋. LogContainer로 전환된 뒤의
            // 오프셋은 ItemAddedToInventory에서 logContainerOffset을 직접 사용한다.
            case TutorialStep.PutItemsInLogContainer: return offroadContainerOffset;
            case TutorialStep.ReceiveMoney: return shopNPCOffset;
            case TutorialStep.UpgradeAxe: return homeOffset;
            case TutorialStep.StartNewLogging: return townVehicleOffset;
        }

        return Vector3.zero;
    }

    private void ShowIndicator(Transform _target, Vector3 _offset)
    {
        if (null == indicator)
        {
            if (null == indicatorPrefab)
                return;

            // GameInstaller 하위(DontDestroyOnLoad)에 붙여 씬 전환에도 살아남게 한다.
            indicator = Instantiate(indicatorPrefab, transform);
            indicator.HideImmediately();
        }

        indicator.Show(_target, _offset);
    }

    private void HideIndicator()
    {
        if (null == indicator)
            return;

        indicator.Hide();
    }

    // 유니티 이벤트 함수
    private void OnDestroy()
    {
        UnSubscribeSignals();
    }
}
