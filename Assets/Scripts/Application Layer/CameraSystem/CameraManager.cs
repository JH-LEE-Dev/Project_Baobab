using UnityEngine;
using Unity.Cinemachine;

public class CameraManager : MonoBehaviour
{
    //외부 의존성
    private CinemachineCamera virtualCamera;
    private SignalHub signalHub;

    private Transform characterTransform;

    public void Initialize(SignalHub _signalHub)
    {
        signalHub = _signalHub;

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

         // 시네머신 카메라의 추적 대상 설정
        if (virtualCamera != null && characterTransform != null)
        {
            virtualCamera.Follow = characterTransform;
            virtualCamera.LookAt = characterTransform;
        }
    }

    public void Release()
    {
        UnSubscribeSignals();
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
        
        // 시네머신 카메라의 추적 대상 설정
        if (virtualCamera != null)
        {
            virtualCamera.Follow = characterTransform;
            virtualCamera.LookAt = characterTransform;
        }
    }
}
