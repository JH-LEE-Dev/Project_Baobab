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
    private Vector3 cameraStartPos;
    private bool isMoved = false;

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

        isMoved = !isMoved;

        if (isMoved)
        {
            // 1. 첫 연출 시작 시점의 위치와 타겟팅 백업
            cameraStartPos = virtualCamera.transform.position;
            cachedFollowTarget = virtualCamera.Follow;
            cachedLookAtTarget = virtualCamera.LookAt;

            virtualCamera.Follow = null;
            virtualCamera.LookAt = null;

            Vector3 targetPosition = cameraStartPos;
            targetPosition.y += yOffset;

            cameraMoveTween = virtualCamera.transform.DOMove(targetPosition, moveDuration);
        }
        else
        {
            // 2. 원래 위치로 복원 및 복원 완료 시 타겟팅 재연결
            cameraMoveTween = virtualCamera.transform.DOMove(cameraStartPos, moveDuration)
                .OnComplete(() =>
                {
                    if (virtualCamera != null)
                    {
                        virtualCamera.Follow = cachedFollowTarget;
                        virtualCamera.LookAt = cachedLookAtTarget;
                    }
                });
        }

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
