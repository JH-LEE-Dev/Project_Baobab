using UnityEngine;
using System.Collections.Generic;

public class LumberjackNPC : MonoBehaviour
{
    // 외부 데이터 제공자
    public ITilemapDataProvider tilemapDataProvider { get; private set; }
    public IPathfindTreeProvider pathfindTreeProvider { get; private set; }
    private IEnvironmentProvider environmentProvider;

    // 내부 컴포넌트
    [SerializeField] private CharacterVisualComponent visualComponent;
    [SerializeField] private LumberjackArmComponent armComponent;
    private PathFindComponent pathFindComponent;
    
    // 상태 머신
    public LumberjackStateMachine stateMachine { get; private set; }

    // 이동 관련 설정
    [Header("Movement Settings")]
    public float moveSpeed = 3f;
    
    [Header("Chop Settings")]
    public float chopDamage = 10f;
    public float chopInterval = 1.0f;

    // 현재 공유 데이터
    public ITreeObj targetTree;
    public List<Vector3> currentPath = new List<Vector3>();

    private bool isMoving = false;
    private Vector2 currentFacingDir = Vector2.down;
    
    private CustomSortable customSortable;

    public void Initialize(IEnvironmentProvider _envProvider, IPathfindTreeProvider _pathfindTreeProvider)
    {
        environmentProvider = _envProvider;
        tilemapDataProvider = environmentProvider.tilemapDataProvider;
        pathfindTreeProvider = _pathfindTreeProvider;

        // 길찾기 컴포넌트 초기화
        pathFindComponent = GetComponent<PathFindComponent>();
        if (pathFindComponent != null)
        {
            pathFindComponent.Initialize(tilemapDataProvider, environmentProvider.pathfindGridProvider, _pathfindTreeProvider);
        }

        if (visualComponent != null)
        {
            var shadow = GetComponentInChildren<Shadow>(true);
            customSortable = GetComponentInChildren<CustomSortable>(true);
            if (customSortable != null)
            {
                customSortable.Initialize(transform);
            }
            Transform waterObj = transform.Find("Visual/OnWaterAnimatorObject");
            GameObject waterGo = waterObj != null ? waterObj.gameObject : null;

            visualComponent.Initialize(environmentProvider, waterGo, shadow, customSortable);
        }

        if (armComponent != null) armComponent.Initialize();

        // 상태 머신 초기화
        stateMachine = new LumberjackStateMachine();
        AddState(new LJState_Idle());
        AddState(new LJState_Move());
        AddState(new LJState_Chop());

        stateMachine.ChangeState<LJState_Idle>();
    }

    private void AddState(LumberjackState _state)
    {
        _state.Initialize(stateMachine, this);
        stateMachine.AddState(_state);
    }

    private void Update()
    {
        stateMachine?.Update();
        
        if (visualComponent != null)
        {
            visualComponent.UpdateVisuals(isMoving, false, false);
        }
    }

    private void FixedUpdate()
    {
        stateMachine?.FixedUpdate();
    }

    private void LateUpdate()
    {
        customSortable?.ManualLateUpdate();
        armComponent?.UpdateSortingOrder();
    }

    private void OnDestroy()
    {
        stateMachine?.ReleaseAllState();
    }

    // --- State 머신에서 호출할 유틸리티 메서드들 ---

    public bool TryFindTree()
    {
        if (pathFindComponent == null) return false;

        return pathFindComponent.FindNearestTreePath(transform.position, out targetTree, currentPath);
    }

    public void SetVisualMoving(bool _isMoving)
    {
        isMoving = _isMoving;
    }

    public void SetVisualFacing(Vector2 _direction)
    {
        currentFacingDir = _direction;
        if (visualComponent != null)
        {
            visualComponent.SetFacingDirection(_direction);
        }
    }

    public void SetArmDirection(Vector2 _direction)
    {
        if (armComponent != null)
        {
            armComponent.SetTargetDirection(_direction);
        }
    }

    public void SwingAxe()
    {
        if (armComponent != null)
        {
            armComponent.SwingAxe(OnAxeImpact);
        }
    }

    private void OnAxeImpact()
    {
        if (targetTree != null)
        {
            // 데미지 전달 로직 (나무 객체에 맞게 수정 가능)
            if (targetTree is TreeObj treeObj)
            {
                // 실제 나무 오브젝트인 경우 데미지 적용
                treeObj.TakeDamage(chopDamage);
            }
        }
    }
}
