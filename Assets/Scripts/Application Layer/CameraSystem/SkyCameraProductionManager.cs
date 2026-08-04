using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using DG.Tweening;
using System;

public class SkyCameraProductionManager : MonoBehaviour
{
    public event Action SkyProductionEndEvent;
    public event Action SkyProductionRollbackEndEvent;
    public event Action IntroRevealEndEvent;
    public event Action AscendOutEndEvent;

    // //외부 의존성
    [SerializeField] private CinemachineCamera virtualCamera = null;

    // //내부 의존성
    [SerializeField] private float moveDuration = 2.0f;
    // 카메라가 하늘로 올라가는 시간(moveDuration) 동안 반드시 끝나야 하는 연출(BGM 페이드아웃 등)이
    // 참조할 수 있도록 외부에 노출한다.
    public float MoveDuration => moveDuration;
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
            Sound.RampProduction3DVolume(0f, moveDuration);
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
            seq.AppendCallback(() => Sound.RampProduction3DVolume(1f, rollbackDuration));
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

    /// <summary>
    /// Town↔Dungeon 왕복에 쓰이는 isMoved/cameraStartPos 상태와 무관하게 동작하는 독립 연출.
    /// MainMenu→Town 최초 진입 시, 캐릭터 상공에서 캐릭터 위치까지 카메라가 내려오는 연출만 재생한다.
    /// </summary>
    public void PlayIntroDescend(Transform _characterTransform)
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (virtualCamera == null || _characterTransform == null || dummyTarget == null)
        {
            // 연출을 못 하더라도 완료 이벤트는 반드시 발행해, 이 이벤트에 물려 있는 입력 재개/HUD 복귀/메인 메뉴 정리가
            // 실행되도록 한다(안 그러면 입력이 잠기고 메인 메뉴가 화면에 남는다).
            Debug.LogWarning($"[SkyCameraProductionManager] PlayIntroDescend 실패로 카메라 하강 연출을 건너뜁니다. " +
                $"(virtualCamera={virtualCamera != null}, character={_characterTransform != null}, dummyTarget={dummyTarget != null}) 즉시 완료 처리합니다.");
            IntroRevealEndEvent?.Invoke();
            return;
        }

        KillCameraMoveTween();

        cachedFollowTarget = virtualCamera.Follow;
        cachedLookAtTarget = virtualCamera.LookAt;

        // "위로 올라가는 연출(up 분기)"의 종착 높이(character.position + yOffset)에서 시작해서, 그 연출과
        // 똑같은 moveDuration으로 캐릭터 위치까지 전부 내려온다. 던전 귀환용 rollback(40만 내려가는 짧은 버전)은
        // "이미 위에 가 있던" 상태를 마무리하는 것뿐이라 여기서 재사용하면 안 된다 — MainMenu→Town은 사전에
        // "올라가는" 연출이 없었으므로, 올라간 높이 전체(yOffset)를 내려와야 같은 속도(yOffset/moveDuration)로
        // 제대로 된 하강처럼 보인다.
        Vector3 startPos = _characterTransform.position;
        startPos.y += yOffset;

        dummyTarget.position = startPos;
        virtualCamera.Follow = dummyTarget;
        virtualCamera.LookAt = dummyTarget;
        virtualCamera.transform.position = startPos;
        virtualCamera.ForceCameraPosition(startPos, virtualCamera.transform.rotation);

        Sound.SetProduction3DVolumeFactor(0f);
        Sequence seq = DOTween.Sequence();
        seq.AppendCallback(() => Sound.RampProduction3DVolume(1f, moveDuration));
        seq.Append(dummyTarget.DOMove(_characterTransform.position, moveDuration));
        seq.AppendCallback(OnIntroDescendComplete);

        if (useCustomCurve)
        {
            seq.SetEase(moveCurve);
        }
        else
        {
            seq.SetEase(moveEase);
        }

        cameraMoveTween = seq;
    }

    private void OnIntroDescendComplete()
    {
        if (virtualCamera != null)
        {
            virtualCamera.Follow = cachedFollowTarget;
            virtualCamera.LookAt = cachedLookAtTarget;
        }

        IntroRevealEndEvent?.Invoke();
    }

    /// <summary>
    /// Town/Dungeon → MainMenu 전용 연출. isMoved/cameraStartPos(Town↔Dungeon 왕복 상태)는 건드리지 않는다.
    /// 캐릭터 위치에서 yOffset만큼 카메라가 올라간다. 이 호출 직후 씬 자체가 통째로 파괴되므로
    /// Follow/LookAt을 원래대로 복원할 필요가 없다.
    /// </summary>
    public void PlayAscendOut(Transform _characterTransform)
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (virtualCamera == null || _characterTransform == null || dummyTarget == null)
        {
            Debug.LogWarning($"[SkyCameraProductionManager] PlayAscendOut 실패로 카메라 상승 연출을 건너뜁니다. " +
                $"(virtualCamera={virtualCamera != null}, character={_characterTransform != null}, dummyTarget={dummyTarget != null}) 즉시 완료 처리합니다.");
            AscendOutEndEvent?.Invoke();
            return;
        }

        KillCameraMoveTween();

        dummyTarget.position = _characterTransform.position;
        virtualCamera.Follow = dummyTarget;
        virtualCamera.LookAt = dummyTarget;

        Vector3 targetPos = _characterTransform.position;
        targetPos.y += yOffset;

        Sound.RampProduction3DVolume(0f, moveDuration);
        Sequence seq = DOTween.Sequence();
        seq.Append(dummyTarget.DOMove(targetPos, moveDuration));
        seq.AppendCallback(OnAscendOutComplete);

        if (useCustomCurve)
        {
            seq.SetEase(moveCurve);
        }
        else
        {
            seq.SetEase(moveEase);
        }

        cameraMoveTween = seq;
    }

    private void OnAscendOutComplete()
    {
        AscendOutEndEvent?.Invoke();
    }

    public void SetCharacterTransform(Transform _characterTransform)
    {
        characterTransform = _characterTransform;
    }

    private void ResetCameraPos()
    {
        KillCameraMoveTween();

        // 카메라 초기 재설정 시 3D 사운드를 0(무음)으로 확실히 억제
        Sound.SetProduction3DVolumeFactor(0f);

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
