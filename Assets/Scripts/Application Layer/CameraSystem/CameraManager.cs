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
    private float snapDistanceThreshold = 0.015f;
    private float stopThreshold = 0.001f; // 캐릭터가 멈췄다고 판단할 이동량 임계값

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
        virtualCamera = Object.FindAnyObjectByType<CinemachineCamera>();

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
            virtualCamera.Lens.OrthographicSize = 5.625f;

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

        ReadyCamera();
    }

    public void Release()
    {
        virtualCamera.Follow = null;
        virtualCamera.LookAt = null;

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
        signalHub.Subscribe<CharacterSpawendSignal>(CharacterSpawned);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<CharacterSpawendSignal>(CharacterSpawned);
    }

    private void CharacterSpawned(CharacterSpawendSignal characterSpawendSignal)
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
        if (virtualCamera == null || characterTransform == null) return;

        Vector3 currentCharacterPos = characterTransform.position;
        Vector3 currentCameraPos = virtualCamera.transform.position;

        // 입력이 멈췄을 때만 스냅 로직을 체크하도록 함
        if (currentInput.sqrMagnitude == 0)
        {
            // 2D 거리 및 이동량 계산을 위해 Z축 무시
            Vector3 currentCharacterPos2D = new Vector3(currentCharacterPos.x, currentCharacterPos.y, 0);
            Vector3 lastCharacterPos2D = new Vector3(lastCharacterPosition.x, lastCharacterPosition.y, 0);
            Vector3 currentCameraPos2D = new Vector3(currentCameraPos.x, currentCameraPos.y, 0);

            float characterMovement = Vector3.Distance(currentCharacterPos2D, lastCharacterPos2D);
            float distanceToTarget = Vector3.Distance(currentCameraPos2D, currentCharacterPos2D);

            // 입력이 없고 캐릭터가 거의 멈춘 상태에서 카메라가 타겟과 충분히 가까우면 스냅
            if (characterMovement < stopThreshold && distanceToTarget > 0 && distanceToTarget < snapDistanceThreshold)
            {
                Vector3 finalPos = currentCharacterPos;
                finalPos.z = currentCameraPos.z;

                virtualCamera.ForceCameraPosition(finalPos, virtualCamera.transform.rotation);
            }
        }

        lastCharacterPosition = currentCharacterPos;
    }

    private void CharacterMoved(Vector2 _input)
    {
        currentInput = _input;
    }

    private void ReadyCamera()
    {
        var parent = virtualCamera.gameObject.transform.parent;

        if (parent != null)
        {
            var quadController = parent.gameObject.GetComponentInChildren<CameraQuadController>();

            quadController.Ready();
        }
    }
}
