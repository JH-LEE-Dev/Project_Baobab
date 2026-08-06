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
            // 씬 로드 시 3D 볼륨이 0으로 덕킹돼 있고, 하강 분기가 그걸 되돌리는 유일한 지점이다.
            // 카메라를 못 찾아 연출을 통째로 건너뛰는 경우에도 볼륨은 반드시 원복해야 한다.
            Sound.SetProduction3DVolumeFactor(1f);
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
            // 상승 중에는 3D 볼륨 계수를 일부러 건드리지 않는다. 카메라가 yOffset(50)만큼 멀어지면
            // 가청 한계(화면 대각선 x 1.4, 약 16유닛)를 훨씬 넘어서므로 거리 감쇠만으로 자연히
            // 무음이 된다. 여기서 계수까지 함께 깎으면 이중으로 적용되어 의도보다 빨리 죽는다.
            // (씬 로드 시 AudioManager.OnSceneLoaded가 계수를 0으로 내리고 3D 사운드를 전부 정지시키므로,
            //  다음 씬은 언제나 0에서 시작해 하강 연출에서 1로 회복된다.)
            Sound.PlayUI(SoundID.SkyUP);
            Sequence seq = DOTween.Sequence();
            seq.Append(dummyTarget.DOMove(targetPosition, moveDuration));
            seq.AppendInterval(0.5f);
            seq.AppendCallback(OnSkyProductionEnd);
            cameraMoveTween = seq;
        }
        else
        {
            ResetCameraPos();

            // 하강은 상승(yOffset=50)을 그대로 되짚지 않는다. ResetCameraPos()가 카메라를 캐릭터 기준
            // +10(= yOffset - rollbackDistance) 지점으로 먼저 스냅시키므로, 실제로 내려가는 거리는 10이다.
            // rollbackDistance는 그 시작 높이를 정하는 값이자 하강 시간을 정하는 기준값으로만 쓰인다.
            float rollbackDistance = 40.0f;
            float rollbackDuration = moveDuration * (rollbackDistance / yOffset);

            // 최종 도착지 = 캐릭터 위치 (ResetCameraPos에서 cameraStartPos에 대입해 둔 값)
            Vector3 targetRollbackPos = cameraStartPos;

            // 원래 위치(캐릭터 위치 + 오프셋 잔여분)로 더미 타겟 복원 및 복원 완료 시 원래 타겟팅 재연결
            Sound.PlayUI(SoundID.SkyDown);
            Sound.FadeOutBGM(rollbackDuration * 0.5f);
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

    private bool bClearFollowLookAtOnArrive;

    /// <summary>
    /// 다음 하강 완료 시점에 Follow/LookAt을 캐릭터로 재연결하지 않고 null로 둔다.
    /// 카메라는 하강이 끝난 그 자리(캐릭터 원래 위치)에 그대로 정지해 있게 된다.
    /// 한 번 적용되면 즉시 초기화되어(1회성) 이후 왕복에는 영향을 주지 않는다.
    /// </summary>
    public void ClearFollowAndLookAtOnArrive()
    {
        bClearFollowLookAtOnArrive = true;
    }

    /// <summary>
    /// ClearFollowAndLookAtOnArrive()로 Follow/LookAt이 비워진 상태에서, 지정한 타겟으로 카메라 추적을
    /// 다시 연결한다(MainMenu → Dungeon 튜토리얼의 캐릭터 하차 시점).
    /// 이후 Town↔Dungeon 왕복의 복원 대상도 이 타겟이 되도록 캐시까지 함께 갱신한다.
    /// </summary>
    public void AttachFollowAndLookAt(Transform _target)
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (virtualCamera == null || _target == null)
        {
            Debug.LogWarning("[SkyCameraProductionManager] AttachFollowAndLookAt 실패. 카메라/타겟 중 null이 있습니다.");
            return;
        }

        cachedFollowTarget = _target;
        cachedLookAtTarget = _target;

        virtualCamera.Follow = _target;
        virtualCamera.LookAt = _target;
    }

    private void OnRollbackCameraComplete()
    {
        if (virtualCamera != null)
        {
            if (bClearFollowLookAtOnArrive)
            {
                // 하강 트윈이 도착한 그 자리(캐릭터가 차량 탑승 위치로 옮겨지기 이전 위치)에 그대로 둔다.
                // 이후 캐릭터가 탑승 위치로 재배치되어도 카메라는 따라가지 않는다.
                virtualCamera.Follow = null;
                virtualCamera.LookAt = null;
            }
            else
            {
                virtualCamera.Follow = cachedFollowTarget;
                virtualCamera.LookAt = cachedLookAtTarget;
            }
        }

        bClearFollowLookAtOnArrive = false;
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
            // 씬 로드 시 3D 볼륨이 0으로 덕킹된 상태다. 이 하강 연출이 유일한 복원 지점이므로,
            // 건너뛸 때도 반드시 원복해야 한다(안 그러면 이후 모든 3D 사운드가 영영 무음이 된다).
            Sound.SetProduction3DVolumeFactor(1f);
            IntroRevealEndEvent?.Invoke();
            return;
        }

        KillCameraMoveTween();

        cachedFollowTarget = virtualCamera.Follow;
        cachedLookAtTarget = virtualCamera.LookAt;

        // 메인 메뉴 -> 타운 진입 시에도 타운 -> 던전과 동일하게 +10 위치에서 시작해 하강하도록 변경
        float rollbackDistance = 40.0f;
        float rollbackDuration = moveDuration * (rollbackDistance / yOffset);

        Vector3 startPos = _characterTransform.position;
        startPos.y += (yOffset - rollbackDistance); // +10 위치에서 시작

        dummyTarget.position = startPos;
        virtualCamera.Follow = dummyTarget;
        virtualCamera.LookAt = dummyTarget;
        virtualCamera.transform.position = startPos;
        virtualCamera.ForceCameraPosition(startPos, virtualCamera.transform.rotation);

        Sound.FadeOutBGM(rollbackDuration * 0.5f);
        Sound.SetProduction3DVolumeFactor(0f);
        Sequence seq = DOTween.Sequence();
        seq.InsertCallback(0.1f, () => Sound.PlayUI(SoundID.SkyDown));
        seq.AppendCallback(() => Sound.RampProduction3DVolume(1f, rollbackDuration));
        seq.Append(dummyTarget.DOMove(_characterTransform.position, rollbackDuration));
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

        // 상승 연출은 PlayCameraMove의 상승 분기와 동일하게 거리 감쇠에만 맡긴다(위 주석 참고).
        Sound.PlayUI(SoundID.SkyUP);
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

    /// <summary>
    /// MainMenu → Dungeon 전용. 카메라를 상승 완료 상태(dummyTarget이 캐릭터 + yOffset 위치)에
    /// 즉시 배치하여, 이후 StartCameraMove()가 하강 모드로 동작하도록 준비한다.
    /// Town↔Dungeon 왕복의 "올라갔다가 내려오는" 흐름에서 "올라가는" 부분을 대체한다.
    /// </summary>
    public void PrepareForDescend(Transform _characterTransform)
    {
        if (virtualCamera == null)
        {
            virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        }

        if (virtualCamera == null || _characterTransform == null || dummyTarget == null)
        {
            Debug.LogWarning("[SkyCameraProductionManager] PrepareForDescend 실패. 카메라/캐릭터/더미타겟 중 null이 있습니다.");
            return;
        }

        KillCameraMoveTween();

        // 현재 카메라 위치(캐릭터 레벨)를 저장 — 하강 시 도착지로 사용된다.
        cameraStartPos = virtualCamera.transform.position;
        cachedFollowTarget = virtualCamera.Follow;
        cachedLookAtTarget = virtualCamera.LookAt;

        // 더미 타겟을 캐릭터 상공(yOffset)에 배치
        Vector3 elevatedPos = _characterTransform.position;
        elevatedPos.y += yOffset;
        dummyTarget.position = elevatedPos;
        virtualCamera.Follow = dummyTarget;
        virtualCamera.LookAt = dummyTarget;
        virtualCamera.transform.position = elevatedPos;
        virtualCamera.ForceCameraPosition(elevatedPos, virtualCamera.transform.rotation);

        Sound.SetProduction3DVolumeFactor(0f);

        // isMoved = true로 세팅해야 다음 StartCameraMove()에서 !isMoved = false (하강 분기)로 진입한다.
        isMoved = true;
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
