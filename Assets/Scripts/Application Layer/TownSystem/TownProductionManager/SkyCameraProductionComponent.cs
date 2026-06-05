using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using DG.Tweening;

public class SkyCameraProductionComponent : MonoBehaviour
{
    // //외부 의존성
    [SerializeField] private CinemachineCamera virtualCamera;

    // //내부 의존성
    [SerializeField] private float moveDuration = 2.0f;
    [SerializeField] private float yOffset = 5.0f;
    [SerializeField] private bool useCustomCurve = false;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Ease moveEase = Ease.OutCubic;

    private Tween cameraMoveTween;
    private Transform cachedFollowTarget;
    private Transform cachedLookAtTarget;

    public void Initialize()
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            PlayCameraMove();
        }
    }

    private void OnDestroy()
    {
        KillCameraMoveTween();
    }

    private void PlayCameraMove()
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (virtualCamera == null)
        {
            return;
        }

        KillCameraMoveTween();

        // 1. 연출 시작 전 현재 타겟팅 객체를 백업하고 해제
        cachedFollowTarget = virtualCamera.Follow;
        cachedLookAtTarget = virtualCamera.LookAt;
        virtualCamera.Follow = null;
        virtualCamera.LookAt = null;

        // 2. 가상 카메라의 Transform 위치 기준으로 이동
        Transform camTrans = virtualCamera.transform;
        Vector3 targetPosition = camTrans.position;
        targetPosition.y += yOffset;

        cameraMoveTween = camTrans.DOMove(targetPosition, moveDuration);
        if (useCustomCurve)
        {
            cameraMoveTween.SetEase(moveCurve);
        }
        else
        {
            cameraMoveTween.SetEase(moveEase);
        }
    }

    private void KillCameraMoveTween()
    {
        if (null != cameraMoveTween && true == cameraMoveTween.IsActive())
        {
            cameraMoveTween.Kill();
        }
    }
}
