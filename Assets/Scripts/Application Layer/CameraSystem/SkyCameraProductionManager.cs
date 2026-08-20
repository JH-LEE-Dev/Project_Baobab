using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using DG.Tweening;
using System;
using PresentationLayer.UISystem.CustomNumber;

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

    // 카운터 폰트 팝(2D UI 사운드)의 볼륨을 카메라 높이에 맞춰 깎기 위한 이번 연출의 기준 높이.
    // groundY(캐릭터 높이)에서 배율 1, apexY(상공)에서 0이 된다. 시간이 아니라 위치로 계산하므로
    // 이징 커브가 무엇이든 화면에 보이는 카메라 높이와 소리 크기가 항상 맞아떨어지고,
    // 트윈이 중간에 교체돼도 다음 연출이 자기 기준을 새로 잡아 값이 어긋난 채 남지 않는다.
    //
    // 정규화 기준을 yOffset이 아니라 "이번 연출의 실제 시작/끝 높이"로 잡는 것이 중요하다.
    // 하강은 상승(+yOffset)을 그대로 되짚지 않고 ResetCameraPos()가 스냅시킨 +10 지점에서
    // 시작하므로(rollbackDistance 주석 참고), yOffset으로 나누면 0이 아니라 0.8쯤에서 살아난다.
    private float skyVolumeGroundY;
    private float skyVolumeApexY;
    private bool bSkyVolumeTracking;

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

        // 이 매니저는 GameInstaller(DontDestroyOnLoad)의 자식이라 Town↔Dungeon 씬 전환으로는
        // 파괴되지 않는다. 즉 여기는 게임을 아예 접거나 메인 메뉴로 빠져나갈 때만 도달한다.
        // (씬 전환 중에 이게 불려버리면 아래 OnSkyProductionEnd가 걸어둔 무음이 로딩 도중
        //  풀려서, 고치려던 "로딩 화면에서 카운터 소리가 새어나오는" 버그가 그대로 재현된다)
        // 배율은 static이라 오브젝트가 사라져도 값이 남으므로, 여기서만 안전하게 원복한다.
        EndSkyVolumeTracking(1f);

        if (dummyTarget != null)
        {
            Destroy(dummyTarget.gameObject);
        }
    }

    private void Update()
    {
        if (bSkyVolumeTracking)
            UpdateSkyVolumeFactor();
    }

    /// <summary>
    /// 이번 연출에서 카메라가 실제로 오갈 두 높이를 기준으로 카운터 사운드 볼륨 추적을 시작한다.
    /// _groundY(캐릭터 높이)에서 배율 1, _apexY(상공)에서 0이 된다.
    /// </summary>
    private void BeginSkyVolumeTracking(float _groundY, float _apexY)
    {
        skyVolumeGroundY = _groundY;
        skyVolumeApexY = _apexY;
        bSkyVolumeTracking = true;

        // 연출 시작 프레임부터 곧바로 올바른 값이 적용되도록 한 번 즉시 계산한다.
        UpdateSkyVolumeFactor();
    }

    /// <summary>
    /// 추적을 끝내고 배율을 확정값으로 고정한다. 연출이 정상 종료된 지점뿐 아니라,
    /// 카메라/캐릭터가 없어 연출을 통째로 건너뛰는 분기에서도 반드시 호출해야
    /// 배율이 0에 머문 채 사운드가 영영 무음으로 남는 일이 없다.
    /// </summary>
    private void EndSkyVolumeTracking(float _finalFactor)
    {
        bSkyVolumeTracking = false;
        CurrencyFontHUD.SetSkyProductionVolumeFactor(_finalFactor);
    }

    private void UpdateSkyVolumeFactor()
    {
        if (dummyTarget == null)
            return;

        float range = skyVolumeApexY - skyVolumeGroundY;

        // 기준 높이가 같거나 뒤집힌 비정상 상태(캐릭터 참조 유실 등)에서는 깎지 않는다.
        // 무음보다는 원래 볼륨으로 들리는 쪽이 안전한 실패 방향이다.
        if (range <= 0.0001f)
        {
            CurrencyFontHUD.SetSkyProductionVolumeFactor(1f);
            return;
        }

        // 카메라는 dummyTarget을 Follow하므로 트윈이 직접 움직이는 이 값이 곧 카메라가 향하는 높이다.
        // 실제 카메라는 Cinemachine 댐핑으로 약간 뒤처지지만, 연출 의도(그리고 구름 UI가 덮이는
        // 타이밍)와는 이쪽이 정확히 일치한다.
        float progress = Mathf.Clamp01((dummyTarget.position.y - skyVolumeGroundY) / range);
        CurrencyFontHUD.SetSkyProductionVolumeFactor(1f - progress);
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
            EndSkyVolumeTracking(1f);
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
            // 반면 폰트 팝은 2D 사운드라 거리 감쇠를 못 받으므로, 상승 높이에 맞춰 직접 깎아준다.
            BeginSkyVolumeTracking(dummyTarget.position.y, targetPosition.y);

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

            // 하강 시작 지점(ResetCameraPos가 스냅시킨 +10)에서 0, 도착 지점에서 1이 되도록
            // 폰트 팝 볼륨을 되살린다. 마을 씬은 하강이 시작되기 전에 이미 맵타입이 Town으로
            // 바뀌어 있어(TownStartedSignal이 씬 로드 시점에 발행된다) 이 처리가 없으면
            // 카메라가 아직 상공에 있는 동안 소리만 원래 볼륨으로 먼저 튀어나온다.
            BeginSkyVolumeTracking(targetRollbackPos.y, dummyTarget.position.y);

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
        // 상승 완료 - 카메라가 상공에 머무는 동안은 완전히 무음으로 고정한다.
        //
        // 이 0f는 곧바로 이어지는 씬 전환(로딩 화면) 구간까지 그대로 유지되어야 한다.
        // 이 매니저가 DontDestroyOnLoad라 씬이 바뀌어도 값이 살아남는 것이 바로 그 장치다.
        // 로딩 중에도 LogProcessingManager.Update()는 계속 돌아 제재소가 배경에서 원목을
        // 가공하고 상점 잔액을 올리는데, 맵타입은 아직 전환 전 값이라 게이트가 열려 있어
        // 가공 완료 타이밍이 로딩과 겹치는 순간에만 소리가 새어나왔다(같은 전환인데도 소리가
        // 날 때와 안 날 때가 갈리던 원인). 배율을 0으로 붙잡아 타이밍과 무관하게 막는다.
        EndSkyVolumeTracking(0f);

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
        // 하강 완료 - 마을 일반 플레이 구간이므로 원래 볼륨으로 확정 복구한다.
        EndSkyVolumeTracking(1f);

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
            EndSkyVolumeTracking(1f);
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

        // 메인 메뉴 → 타운도 하강 연출이므로 폰트 팝을 0에서 원래 볼륨으로 되살린다.
        BeginSkyVolumeTracking(_characterTransform.position.y, startPos.y);

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

        EndSkyVolumeTracking(1f);

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
            EndSkyVolumeTracking(1f);
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
        // 폰트 팝만 2D라 거리 감쇠가 없으므로 여기서도 높이에 맞춰 직접 깎는다.
        BeginSkyVolumeTracking(_characterTransform.position.y, targetPos.y);

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
        // 상승 완료 - OnSkyProductionEnd(왕복 상승)와 동일하게 무음으로 고정한다.
        // 이 상태로 메인 메뉴에 들어가더라도, 다시 타운으로 돌아올 때 PlayIntroDescend가
        // (실패 분기까지 포함해) 반드시 1f로 되돌리므로 무음이 굳지 않는다.
        EndSkyVolumeTracking(0f);

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
            EndSkyVolumeTracking(1f);
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

        // 카메라를 상승 완료 지점에 즉시 배치한 상태이므로 폰트 팝도 무음에서 시작한다.
        // 뒤이어 실행될 하강(StartCameraMove)이 ResetCameraPos() 이후의 실제 시작 높이를 기준으로
        // 추적을 새로 잡아 1f까지 되살린다.
        EndSkyVolumeTracking(0f);

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
