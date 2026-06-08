using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using DG.Tweening;
using System;

public class SkyCameraProductionComponent : MonoBehaviour
{
    public event Action SkyProductionEndEvent;
    public event Action SkyProductionRollbackEndEvent;
    
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

            Sequence seq = DOTween.Sequence();
            seq.Append(virtualCamera.transform.DOMove(targetPosition, moveDuration));
            seq.AppendInterval(0.5f);
            seq.AppendCallback(OnSkyProductionEnd);
            cameraMoveTween = seq;
        }
        else
        {
            // 2. 원래 위치로 복원 및 복원 완료 시 타겟팅 재연결
            Sequence seq = DOTween.Sequence();
            seq.Append(virtualCamera.transform.DOMove(cameraStartPos, moveDuration));
            seq.AppendCallback(OnRollbackCameraComplete);
            seq.AppendInterval(1.0f);
            seq.AppendCallback(OnSkyProductionRollbackEnd);
            cameraMoveTween = seq;
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

    private void OnSkyProductionEnd()
    {
        SkyProductionEndEvent?.Invoke();
    }

    private void OnRollbackCameraComplete()
    {
        if (virtualCamera != null)
        {
            virtualCamera.Follow = cachedFollowTarget;
            virtualCamera.LookAt = cachedLookAtTarget;
        }
    }

    private void OnSkyProductionRollbackEnd()
    {
        SkyProductionRollbackEndEvent?.Invoke();
    }

    private void KillCameraMoveTween()
    {
        if (null != cameraMoveTween && true == cameraMoveTween.IsActive())
        {
            cameraMoveTween.Kill();
        }
    }

    public void StartCameraMove()
    {
        PlayCameraMove();
    }

    public void ResetCameraPos(Transform _characterTransform)
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (virtualCamera == null || _characterTransform == null)
        {
            return;
        }

        KillCameraMoveTween();

        if (isMoved)
        {
            Vector3 targetPosition = _characterTransform.position;
            targetPosition.y += yOffset;
            virtualCamera.transform.position = targetPosition;

            cameraStartPos = _characterTransform.position;
        }
        else
        {
            virtualCamera.Follow = _characterTransform;
            virtualCamera.LookAt = _characterTransform;
            virtualCamera.transform.position = _characterTransform.position;
        }
    }
}
