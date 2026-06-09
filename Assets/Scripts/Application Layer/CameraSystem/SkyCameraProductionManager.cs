using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using DG.Tweening;
using System;

public class SkyCameraProductionManager : MonoBehaviour
{
    public event Action SkyProductionEndEvent;
    public event Action SkyProductionRollbackEndEvent;

    // //외부 의존성
    [SerializeField] private CinemachineCamera virtualCamera = null;

    // //내부 의존성
    [SerializeField] private float moveDuration = 2.0f;
    [SerializeField] private float yOffset = 5.0f;
    [SerializeField] private bool useCustomCurve = false;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Ease moveEase = Ease.OutCubic;

    [SerializeField] private Transform dummyTarget;
    private Tween cameraMoveTween;
    private Transform cachedFollowTarget;
    private Transform cachedLookAtTarget;
    private Vector3 cameraStartPos;
    private bool isMoved = false;
    private Transform characterTransform;

    public void Initialize()
    {
        if (dummyTarget == null)
        {
            GameObject dummyGo = new GameObject("SkyCameraDummyTarget");
            dummyTarget = dummyGo.transform;
            dummyTarget.SetParent(transform);
        }
    }

    private void OnDestroy()
    {
        KillCameraMoveTween();
        if (dummyTarget != null)
        {
            Destroy(dummyTarget.gameObject);
        }
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
            // 1. 첫 연출 시작 시점의 위치와 타겟팅 백업 및 더미 타겟으로 대체
            cameraStartPos = virtualCamera.transform.position;
            cachedFollowTarget = virtualCamera.Follow;
            cachedLookAtTarget = virtualCamera.LookAt;

            if (dummyTarget != null)
            {
                dummyTarget.position = characterTransform != null ? characterTransform.position : virtualCamera.transform.position;
                virtualCamera.Follow = dummyTarget;
                virtualCamera.LookAt = dummyTarget;
            }

            Vector3 targetPosition = dummyTarget.position;
            targetPosition.y += yOffset;

            // 더미 타겟을 위로 이동시키면 카메라도 이를 쫓아 위로 올라갑니다.
            Sequence seq = DOTween.Sequence();
            seq.Append(dummyTarget.DOMove(targetPosition, moveDuration));
            seq.AppendInterval(0.5f);
            seq.AppendCallback(OnSkyProductionEnd);
            cameraMoveTween = seq;
        }
        else
        {
            ResetCameraPos();

            // yOffset(50)에서 40을 뺀 만큼만 내려감 (최종 높이는 캐릭터 기준 +10)
            // 즉, 내려가는 실제 거리는 40f
            float rollbackDistance = 40.0f;
            float rollbackDuration = moveDuration * (rollbackDistance / yOffset);

            // 최종 도착지 = 캐릭터 위치 + (yOffset - 40)
            Vector3 targetRollbackPos = cameraStartPos;

            // 원래 위치(캐릭터 위치 + 오프셋 잔여분)로 더미 타겟 복원 및 복원 완료 시 원래 타겟팅 재연결
            Sequence seq = DOTween.Sequence();
            seq.Append(dummyTarget.DOMove(targetRollbackPos, rollbackDuration));
            seq.AppendCallback(OnRollbackCameraComplete);
            //seq.AppendInterval(1.0f);
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

    public void SetCharacterTransform(Transform _characterTransform)
    {
        characterTransform = _characterTransform;
    }

    private void ResetCameraPos()
    {
        KillCameraMoveTween();

        cachedFollowTarget = characterTransform;
        cachedLookAtTarget = characterTransform;

        if (dummyTarget != null && characterTransform != null)
        {
            // 더미 타겟을 캐릭터 머리 위에 셋업
            Vector3 dummyPos = characterTransform.position;
            dummyPos.y += yOffset-40f;
            dummyTarget.position = dummyPos;

            // 카메라도 즉시 더미 타겟 위치를 비추도록 설정
            virtualCamera.Follow = dummyTarget;
            virtualCamera.LookAt = dummyTarget;
            virtualCamera.transform.position = dummyPos;
            virtualCamera.ForceCameraPosition(dummyPos, virtualCamera.transform.rotation);
        }

        if (characterTransform != null)
        {
            cameraStartPos = characterTransform.position;
        }
    }
}
