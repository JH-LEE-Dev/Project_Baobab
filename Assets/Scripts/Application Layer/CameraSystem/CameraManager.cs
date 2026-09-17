using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    //내부 의존성
    private CinemachineCamera virtualCamera;
    private SignalHub signalHub;
    private InputManager inputManager;

    private Transform characterTransform;
    private Vector3 lastCharacterPosition;
    private Vector2 currentInput;
    //private float snapDistanceThreshold = 0.015f;
    //private float stopThreshold = 0.001f; // 캐릭터가 멈췄다고 판단할 이동량 임계값

    // F1 시네마틱 모드는 개발 중 확인용 기능이라 릴리즈 빌드에서는 통째로 제외한다.
    // 가드가 없으면 출시 빌드에서 플레이어가 F1을 누르는 것만으로 하위 Canvas가 전부 꺼지고
    // 카메라가 캐릭터 추적에서 풀려버린다.
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private bool isCinematicMode = false;
#endif

    public void Initialize(SignalHub _signalHub, InputManager _inputManager)
    {
        signalHub = _signalHub;
        inputManager = _inputManager;

        BindEvents();
        SubscribeSignals();

        ResetCamera();
    }

    public void ResetCamera()
    {
        // 씬 내에 존재하는 CinemachineCamera를 자동으로 검색하여 할당합니다.
        virtualCamera = UnityEngine.Object.FindAnyObjectByType<CinemachineCamera>();

        if (virtualCamera == null)
        {
            Debug.LogWarning("CameraManager: 씬에서 CinemachineCamera를 찾을 수 없습니다.");
        }

        if (characterTransform != null)
        {
            lastCharacterPosition = characterTransform.position;
        }

        // 시네머신 카메라의 추적 대상 및 렌즈 설정
        if (virtualCamera != null)
        {
            //virtualCamera.Lens.OrthographicSize = 5.625f;

            if (characterTransform != null)
            {
                virtualCamera.Follow = characterTransform;
                virtualCamera.LookAt = characterTransform;
            }
            else
            {
                virtualCamera.Follow = null;
                virtualCamera.LookAt = null;
            }
        }

        // 예전에는 여기서 ReadyCamera()를 불러 "Final Camera" 쪽 출력 쿼드(CameraQuadController)와
        // "UI Camera"를 찾아 크기를 맞췄습니다. 그 파이프라인은 지금 프로젝트 어디에도 배치돼 있지
        // 않습니다 - Final Camera.prefab을 참조하는 씬/프리팹이 하나도 없고, "UI Camera"라는 이름의
        // 오브젝트도 존재하지 않습니다(있는 것은 PP UI Camera). 즉 양쪽 분기가 모두 빈손이었고,
        // 남은 효과는 virtualCamera를 무가드로 역참조해 터질 수 있다는 것 하나뿐이었습니다.
        // 바로 위의 null 경고가 무의미해지는 것도 그 때문이라(경고만 남기고 여섯 줄 뒤 NRE),
        // 가드를 덧대는 대신 메서드를 통째로 걷어냈습니다.
        //
        // 이 메서드는 SetupGameInstaller()의 첫 줄이고 그 호출부에는 try/catch가 없어서, 여기서
        // 예외가 나면 맵 생성/던전 시작/캔버스 셋업/마을 도착 자동저장까지 전부 실행되지 않습니다.
        // 쿼드 출력 경로를 되살릴 일이 생기면 반드시 virtualCamera null 가드를 함께 넣으십시오.
    }

    public void Release()
    {
        // null 가드가 필요한 이유: 이 두 줄이 이 메서드에서 예외가 날 수 있는 유일한 지점이고,
        // 터지면 아래 ReleaseEvents()/UnSubscribeSignals()가 통째로 건너뛰어진다.
        // GameInstaller.SafeRelease가 예외는 삼켜주지만 건너뛴 구독 해제까지 되살리지는 못한다.
        // (inputReader는 BootStrap이 들고 있어 GameInstaller보다 오래 살므로 MoveEvent 구독이 샌다)
        if (virtualCamera != null)
        {
            virtualCamera.Follow = null;
            virtualCamera.LookAt = null;
        }

        ReleaseEvents();
        UnSubscribeSignals();
    }

    private void BindEvents()
    {
        if (inputManager?.inputReader != null)
        {
            inputManager.inputReader.MoveEvent -= CharacterMoved;
            inputManager.inputReader.MoveEvent += CharacterMoved;
        }
    }

    private void ReleaseEvents()
    {
        if (inputManager?.inputReader != null)
        {
            inputManager.inputReader.MoveEvent -= CharacterMoved;
        }
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
    }

    private void CharacterSpawned(CharacterSpawnedSignal characterSpawendSignal)
    {
        characterTransform = characterSpawendSignal.character.transform;
        lastCharacterPosition = characterTransform.position;

        // 시네머신 카메라의 추적 대상 설정
        if (virtualCamera != null)
        {
            virtualCamera.Follow = characterTransform;
            virtualCamera.LookAt = characterTransform;
        }
    }

    private void LateUpdate()
    {
        // if (virtualCamera == null || characterTransform == null) return;

        // Vector3 currentCharacterPos = characterTransform.position;
        // Vector3 currentCameraPos = virtualCamera.transform.position;

        // // 입력이 멈췄을 때만 스냅 로직을 체크하도록 함
        // if (currentInput.sqrMagnitude == 0)
        // {
        //     // 2D 거리 및 이동량 계산을 위해 Z축 무시
        //     Vector3 currentCharacterPos2D = new Vector3(currentCharacterPos.x, currentCharacterPos.y, 0);
        //     Vector3 lastCharacterPos2D = new Vector3(lastCharacterPosition.x, lastCharacterPosition.y, 0);
        //     Vector3 currentCameraPos2D = new Vector3(currentCameraPos.x, currentCameraPos.y, 0);

        //     float characterMovement = Vector3.Distance(currentCharacterPos2D, lastCharacterPos2D);
        //     float distanceToTarget = Vector3.Distance(currentCameraPos2D, currentCharacterPos2D);

        //     // 입력이 없고 캐릭터가 거의 멈춘 상태에서 카메라가 타겟과 충분히 가까우면 스냅
        //     if (characterMovement < stopThreshold && distanceToTarget > 0 && distanceToTarget < snapDistanceThreshold)
        //     {
        //         Vector3 finalPos = currentCharacterPos;
        //         finalPos.z = currentCameraPos.z;

        //         virtualCamera.ForceCameraPosition(finalPos, virtualCamera.transform.rotation);
        //     }
        // }

        // lastCharacterPosition = currentCharacterPos;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.f1Key.wasPressedThisFrame)
        {
            isCinematicMode = !isCinematicMode;

            // 시네마틱 모드에 진입하면 GameInstaller 하위의 모든 캔버스를 비활성화 (종료 시 재활성화)
            Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
            foreach (var canvas in canvases)
            {
                canvas.enabled = !isCinematicMode;
            }

            if (isCinematicMode)
            {
                if (characterTransform != null && virtualCamera != null)
                {
                    virtualCamera.Follow = null;
                    virtualCamera.LookAt = null;

                    Vector3 startPos = characterTransform.position + new Vector3(-30f, 30f, 0f); // 좌상단으로 더 멀리 위치 이동
                    startPos.z = virtualCamera.transform.position.z;
                    
                    virtualCamera.transform.position = startPos;
                    virtualCamera.ForceCameraPosition(startPos, virtualCamera.transform.rotation);
                }
            }
            else
            {
                if (characterTransform != null && virtualCamera != null)
                {
                    virtualCamera.Follow = characterTransform;
                    virtualCamera.LookAt = characterTransform;
                }
            }
        }

        if (isCinematicMode && virtualCamera != null)
        {
            Vector3 moveDir = new Vector3(1f, -1f, 0f).normalized; // 우하단 방향
            float speed = 2f; // 이동 속도
            
            Vector3 nextPos = virtualCamera.transform.position + (moveDir * speed * Time.deltaTime);
            virtualCamera.transform.position = nextPos;
            virtualCamera.ForceCameraPosition(nextPos, virtualCamera.transform.rotation);
        }
    }
#endif

    private void CharacterMoved(Vector2 _input)
    {
        currentInput = _input;
    }
}
