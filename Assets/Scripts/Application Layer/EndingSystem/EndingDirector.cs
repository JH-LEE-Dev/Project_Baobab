using System.Collections;
using DG.Tweening;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 본편 엔딩 씬(EndingScene)의 연출 순서. GameInstaller 없이 이 씬 안에서 혼자 돈다.
///
///   1. 도착: 마을에서 구름(EndingSkyCover)에 덮인 채 넘어온다. 하늘 연출이 빨라지기 전 초기 버전(cf38c73f)의
///      던전 도착 그대로다: 0.75초 뒤 구름이 걷히는 동시에 카메라가 +10에서 스냅된 뒤 1.8 × 0.8 = 1.44초 동안 내려온다
///      (SkyDown, BGM 페이드, 3D 볼륨 복구, 시퀀스 전체 이징). 카메라는 본편처럼 더미 타깃을 Follow해 같은 Damping으로 따라간다.
///      내려오는 동안에도 차는 이미 1스테이지 숲 오솔길(우하단으로 뻗은 길)을 달리고 있다. 1스테이지 BGM은 본편 던전처럼 하강이 끝난 뒤 켠다.
///   2. 크레딧: 검은 패널이 오른쪽에서 들어오고 카메라가 함께 옮겨가 차가 왼쪽 영역 가운데에 온다.
///      크레딧은 건너뛸 수 없고, 아무 키나 누르고 있으면 빨라진다(EndingCreditRoll).
///   3. 퇴장: 크레딧이 끝나면 패널이 오른쪽으로 빠지고 차가 다시 화면 가운데로 온 뒤, 카메라는 멈추고 차만 가속해 화면 밖으로 나간다.
///   4. 소풍: 페이드 아웃 → 풀빛너른숲 3의 빈터에서 캐릭터와 강아지가 돗자리에 앉아 있다. "플레이 해주셔서 감사합니다"와
///      (최초 해제 때만) "로그라이크 모드 해제!"를 보여 준다. 강아지는 본편 SitLookAtOwner처럼 주인을 보고 앉아
///      꼬리를 흔들고, 문구가 떠오를 때 한 번 짖는다.
///   5. 메인메뉴 복귀: 본편 마을 → 메인메뉴(TownSystem.GoToMainMenuRequested → TownProductionManager.StartGoToMainMenu)와 같다.
///      BGM 페이드아웃, 메인메뉴 패널이 위에서 내려오고(BootStrap.BeginMainMenuReturnFromEnding), 구름이 메인메뉴 모드로 아래에서 덮고,
///      카메라가 yOffset만큼 올라간다(SkyCameraProductionManager.PlayAscendOut). 상승이 끝나면 메인메뉴 씬으로 바뀌며
///      구름은 본편 SkyProduction(GameInstaller와 함께 해제)처럼 그 순간 사라진다.
///
/// 차·숲·카메라 타깃의 움직임은 모두 Update에서 정한다. CinemachineBrain이 LateUpdate에서 타깃을 따라가므로
/// 같은 프레임 안에서 차와 카메라 타깃이 어긋나지 않는다.
/// </summary>
public class EndingDirector : MonoBehaviour
{
    [Header("참조")]
    [SerializeField] private EndingForest forest;
    [SerializeField] private EndingOffroadVisual car;
    [SerializeField] private EndingCreditRoll credits;
    [SerializeField] private CinemachineCamera virtualCamera;
    [SerializeField] private Camera worldCamera;
    [Tooltip("엔딩 씬을 에디터에서 바로 켰을 때만 쓴다(평소에는 마을에서 만든 덮개가 넘어온다).")]
    [SerializeField] private EndingSkyCover skyCoverPrefab;

    [Header("본편 그림자 머티리얼 (LightingController와 같은 색)")]
    [SerializeField] private Material[] shadowMaterials;
    [SerializeField] private Color shadowColor = new Color(0f, 0f, 0f, 0.45f);

    [Header("주행")]
    [Tooltip("차 기준점과 길 위 지면의 차이(차 그림의 기준점이 바퀴보다 위에 있다).")]
    [SerializeField] private Vector3 carPivotOffset = new Vector3(0.1f, 0f, 0f);
    [Tooltip("화면 가운데에 둘 지점(차 지면 기준).")]
    [SerializeField] private Vector3 cameraLookOffset = new Vector3(0f, 0.4f, 0f);
    [SerializeField] private float driveSpeed = 2f;

    [Header("도착 (cf38c73f 하늘 연출)")]
    [SerializeField] private float arriveDelay = 0.75f;
    [SerializeField] private float descentHeight = 10f;
    [SerializeField] private float descentDuration = 1.44f;
    [SerializeField] private Ease descentEase = Ease.InOutCubic;
    [Tooltip("내려온 뒤 크레딧 패널이 들어오기 전까지 차가 가운데서 달리는 시간.")]
    [SerializeField] private float introDriveSeconds = 2.5f;

    [Header("크레딧")]
    [SerializeField] private Ease cameraPanEase = Ease.InOutSine;
    [SerializeField] private float afterScrollSeconds = 0.6f;
    [SerializeField] private float beforeExitSeconds = 1.2f;

    [Header("퇴장")]
    [SerializeField] private float exitAcceleration = 9f;
    [SerializeField] private float exitMaxSpeed = 14f;
    [SerializeField] private float exitMaxSeconds = 3f;
    [SerializeField] private float exitFadeSeconds = 1f;

    [Header("소풍")]
    [SerializeField] private Transform picnicRoot;
    [Tooltip("감사 문구가 떠오를 때 한 번 짖는 강아지(본편 PuppyBark).")]
    [SerializeField] private Transform picnicPuppy;
    [SerializeField] private float puppyBarkDelay = 0.35f;
    [Tooltip("소풍 장면에서 차를 세워 두는 자리(빈터 가운데 기준).")]
    [SerializeField] private Vector3 picnicCarOffset = new Vector3(-3.25f, 1.6f, 0f);
    [SerializeField] private Vector3 picnicCameraOffset = new Vector3(0f, 0.9f, 0f);
    [SerializeField] private float picnicFadeInSeconds = 1.2f;
    [SerializeField] private float picnicBeforeMessageSeconds = 1f;
    [SerializeField] private float messageFadeSeconds = 0.8f;
    [SerializeField] private float messageHoldSeconds = 3.5f;
    [Tooltip("로그라이크 해제 문구가 함께 뜰 때 더 보여 주는 시간.")]
    [SerializeField] private float unlockExtraHoldSeconds = 1.5f;
    [Header("메인메뉴 복귀 (본편 마을 → 메인메뉴)")]
    [Tooltip("카메라 상승·구름 덮기 시간. 본편 GameInstaller의 SkyCameraProductionManager.moveDuration, SkyProduction.moveDuration, UIView_MainMenu.exitMoveDuration과 같은 값.")]
    [SerializeField] private float returnAscendDuration = 1.5f;
    [Tooltip("카메라 상승 높이. 본편 SkyCameraProductionManager.yOffset과 같은 값.")]
    [SerializeField] private float returnAscendHeight = 50f;
    [Tooltip("상승 이징. 본편 SkyCameraProductionManager.moveEase와 같은 값(시퀀스 전체에 건다).")]
    [SerializeField] private Ease returnAscendEase = Ease.InOutCubic;

    private EndingSkyCover cover;
    // 본편 SkyCameraProductionManager의 dummyTarget과 같은 역할. 가상 카메라가 이걸 Follow해 본편과 같은 Damping으로 따라간다.
    private Transform cameraTarget;
    private float carRow;
    private float carSpeed;
    private bool bCarDriving;
    private bool bCameraFollow = true;
    private float cameraLift;
    private float cameraPanWeight;
    private Vector3 cameraPosition;
    private Tween cameraLiftTween;
    private Tween cameraPanTween;

    private void Start()
    {
        if (null == virtualCamera) virtualCamera = FindAnyObjectByType<CinemachineCamera>();
        if (null == worldCamera) worldCamera = Camera.main;
        cameraTarget = new GameObject("EndingCameraTarget").transform;
        cameraTarget.SetParent(transform, false);
        if (null != virtualCamera)
        {
            virtualCamera.Follow = cameraTarget;
            virtualCamera.LookAt = cameraTarget;
        }

        ApplyShadowColor();

        if (null != picnicRoot) picnicRoot.gameObject.SetActive(false);

        cover = EndingSkyCover.Current;
        if (null == cover && null != skyCoverPrefab)
        {
            cover = EndingSkyCover.Create(skyCoverPrefab);
            if (null != cover) cover.SetBlack(1f);
        }

        carRow = 0f;
        carSpeed = driveSpeed;
        bCarDriving = true;
        bCameraFollow = true;
        cameraLift = descentHeight;
        cameraPanWeight = 0f;

        if (null != forest) forest.BeginRoad(carRow);
        if (null != car) car.SetCarrying(true);
        UpdateCarAndCamera(0f, true);

        StartCoroutine(Run());
    }

    private void Update()
    {
        UpdateCarAndCamera(Time.deltaTime, false);
    }

    private IEnumerator Run()
    {
        // 1. 도착: cf38c73f RollbackCameraMoveRoutine - 대기 뒤 구름이 걷히고(같은 프레임) 카메라가 내려온다.
        yield return new WaitForSeconds(arriveDelay);

        if (null != cover)
        {
            cover.PlayReveal(null);
            cover.FadeBlack(0f, descentDuration);
        }

        // cf38c73f ResetCameraPos: 3D 볼륨을 0으로 누르고 카메라를 내려올 시작 높이(+10)에 스냅한다.
        Sound.SetProduction3DVolumeFactor(0f);
        cameraLift = descentHeight;
        UpdateCarAndCamera(0f, true);

        // cf38c73f PlayCameraMove 하강 분기: SkyDown·BGM 페이드 → 3D 볼륨 복구와 함께 내려온다. 이징은 시퀀스 전체에 건다.
        Sound.PlayUI(SoundID.SkyDown);
        Sound.FadeOutBGM(descentDuration * 0.5f);
        Sequence descent = DOTween.Sequence();
        descent.AppendCallback(() => Sound.RampProduction3DVolume(1f, descentDuration));
        descent.Append(DOTween.To(() => cameraLift, _v => cameraLift = _v, 0f, descentDuration));
        descent.SetEase(descentEase);
        cameraLiftTween = descent;
        yield return cameraLiftTween.WaitForCompletion();

        // 본편 던전처럼 하강이 끝난 뒤 스테이지 BGM을 켠다.
        Sound.PlayBGM(SoundID.Stage1BGM);

        if (null != forest) forest.ShrinkRoadBehind();
        yield return new WaitForSeconds(introDriveSeconds);

        // 2. 크레딧: 패널이 들어오며 카메라가 옮겨가 차를 왼쪽 영역 가운데에 둔다.
        if (null != credits)
        {
            credits.SlideIn();
            yield return PanCamera(1f, credits.SlideDuration);

            bool bScrollDone = false;
            credits.PlayScroll(() => bScrollDone = true);
            while (false == bScrollDone) yield return null;

            yield return new WaitForSeconds(afterScrollSeconds);

            // 3. 퇴장: 패널이 빠지고 차가 가운데로 돌아온다.
            credits.SlideOut();
            yield return PanCamera(0f, credits.SlideDuration);
        }

        yield return new WaitForSeconds(beforeExitSeconds);

        // 카메라는 멈추고 차만 가속해 화면 밖으로 나간다.
        bCameraFollow = false;
        if (null != car) car.Launch();

        float exitStart = Time.time;
        while (Time.time - exitStart < exitMaxSeconds && IsCarVisible())
        {
            carSpeed = Mathf.Min(exitMaxSpeed, carSpeed + exitAcceleration * Time.deltaTime);
            yield return null;
        }

        if (null != car) car.StopEngine(exitFadeSeconds);
        yield return FadeToBlack(exitFadeSeconds);

        // 4. 소풍
        bCarDriving = false;
        BuildPicnic();

        if (null != cover) yield return WaitTween(cover.FadeBlack(0f, picnicFadeInSeconds));
        yield return new WaitForSeconds(picnicBeforeMessageSeconds);

        bool bUnlock = EndingContext.bShowRoguelikeUnlockMessage;
        EndingContext.bShowRoguelikeUnlockMessage = false;
        if (null != credits)
        {
            GetMessages(out string thanks, out string unlock);
            Tween messageTween = credits.ShowMessage(thanks, bUnlock ? unlock : null, messageFadeSeconds);
            if (null != picnicPuppy) DOVirtual.DelayedCall(puppyBarkDelay, () => Sound.Play(SoundID.PuppyBark, picnicPuppy.position));
            yield return WaitTween(messageTween);
        }

        yield return new WaitForSeconds(messageHoldSeconds + (bUnlock ? unlockExtraHoldSeconds : 0f));

        // 5. 메인메뉴
        yield return ReturnToMainMenu();
    }

    private void UpdateCarAndCamera(float _deltaTime, bool _bSnap)
    {
        if (null == forest) return;

        if (bCarDriving)
        {
            float rowLength = forest.RowStep.magnitude;
            if (rowLength > 0.0001f) carRow += carSpeed * _deltaTime / rowLength;

            Vector3 ground = forest.GetRoadPoint(carRow);
            if (null != car)
            {
                car.SetPose(ground + carPivotOffset, carSpeed);
                forest.UpdateOcclusion(car.BodyBounds);
            }

            if (bCameraFollow)
            {
                cameraPosition = ground + cameraLookOffset + new Vector3(GetLeftAreaShift() * cameraPanWeight, cameraLift, 0f);
                forest.UpdateRoad(carRow);
            }
        }

        PlaceCamera(cameraPosition, _bSnap);
    }

    // 카메라 타깃을 옮긴다. 스냅이면 본편 ResetCameraPos처럼 가상 카메라도 그 자리에 바로 붙여 Damping 없이 시작한다.
    private void PlaceCamera(Vector3 _position, bool _bSnap)
    {
        if (null == cameraTarget) return;

        Vector3 targetPosition = _position;
        targetPosition.z = 0f;
        cameraTarget.position = targetPosition;

        if (false == _bSnap || null == virtualCamera) return;

        Vector3 vcamPosition = _position;
        vcamPosition.z = virtualCamera.transform.position.z;
        virtualCamera.transform.position = vcamPosition;
        virtualCamera.ForceCameraPosition(vcamPosition, virtualCamera.transform.rotation);
    }

    /// <summary>
    /// 차를 패널 왼쪽 영역 가운데에 두려면 카메라를 오른쪽으로 옮겨야 하는 거리. 패널은 카메라 영역의 오른쪽 r만큼을 덮으므로
    /// 왼쪽 영역 가운데는 화면 가운데에서 r/2 × 화면 폭만큼 왼쪽이다. 화면비·필러박스와 무관하게 카메라 영역 비율로 계산한다.
    /// </summary>
    private float GetLeftAreaShift()
    {
        if (null == worldCamera || null == credits) return 0f;

        float viewWidth = worldCamera.orthographicSize * 2f * worldCamera.aspect;
        return credits.PanelWidthRatio * 0.5f * viewWidth;
    }

    private IEnumerator PanCamera(float _weight, float _duration)
    {
        if (null != cameraPanTween && cameraPanTween.IsActive()) cameraPanTween.Kill();
        cameraPanTween = DOTween.To(() => cameraPanWeight, _v => cameraPanWeight = _v, _weight, _duration).SetEase(cameraPanEase);
        yield return cameraPanTween.WaitForCompletion();
    }

    private bool IsCarVisible()
    {
        if (null == worldCamera || null == car) return false;

        Vector3 viewport = worldCamera.WorldToViewportPoint(car.transform.position);
        // 차 그림 크기(약 2유닛)만큼 여유를 두고 완전히 빠질 때까지 기다린다.
        return viewport.x < 1.15f && viewport.y > -0.2f;
    }

    private void BuildPicnic()
    {
        if (null == forest) return;

        forest.BuildPicnic();
        Vector3 center = forest.PicnicCenter;

        if (null != picnicRoot)
        {
            picnicRoot.position = center;
            picnicRoot.gameObject.SetActive(true);
            SortPicnicActors();
        }

        if (null != car)
        {
            car.SetCarrying(false);
            car.Park(center + picnicCarOffset + carPivotOffset);
        }

        cameraPosition = center + picnicCameraOffset;
        PlaceCamera(cameraPosition, true);
    }

    // 소풍 인물은 본편 CustomSortable과 같은 규칙(-y × 100)으로 한 번 정렬하고, 그림자는 숲과 같은 해 각도로 둔다.
    private void SortPicnicActors()
    {
        SortingGroup[] groups = picnicRoot.GetComponentsInChildren<SortingGroup>(true);
        for (int i = 0; i < groups.Length; i++)
        {
            groups[i].sortingOrder = -Mathf.RoundToInt(groups[i].transform.position.y * 100f);
        }

        Shadow[] shadows = picnicRoot.GetComponentsInChildren<Shadow>(true);
        for (int i = 0; i < shadows.Length; i++)
        {
            shadows[i].Initialize();
            shadows[i].ManualUpdate(forest.ShadowAngle, forest.ShadowScaleY, true);
        }
    }

    private IEnumerator FadeToBlack(float _seconds)
    {
        if (null == cover) yield break;
        yield return WaitTween(cover.FadeBlack(1f, _seconds));
    }

    private static IEnumerator WaitTween(Tween _tween)
    {
        if (null != _tween && _tween.IsActive()) yield return _tween.WaitForCompletion();
    }

    // 본편 마을 → 메인메뉴와 같은 순서. 문구는 본편 HUD가 내려가듯(PopupUIDown) 상승과 함께 걷는다.
    private IEnumerator ReturnToMainMenu()
    {
        BootStrap bootStrap = FindAnyObjectByType<BootStrap>();

        Sound.FadeOutBGM(returnAscendDuration);
        if (null != bootStrap)
        {
            // 메인메뉴 패널이 내려오는 동안에도 엔딩 문구가 보이도록 패널을 부르기 전에 켠다(메인메뉴 진입 때 꺼진다).
            EndingContext.bReturningToMainMenu = true;
            bootStrap.BeginMainMenuReturnFromEnding();
        }

        if (null != cover)
        {
            cover.SetMainMenuMode(true);
            cover.PlayCover(returnAscendDuration);
        }

        if (null != credits) credits.HideMessage(messageFadeSeconds);

        yield return PlayReturnAscend();

        if (null == bootStrap)
        {
            Debug.LogWarning("[EndingDirector] BootStrap이 없어 메인메뉴로 돌아가지 않습니다(엔딩 씬을 에디터에서 바로 켠 경우).");
            yield break;
        }

        // 본편은 상승이 끝나 씬을 바꿀 때 GameInstaller와 함께 SkyProduction 구름이 해제된다. 같은 순간에 걷는다.
        if (null != cover) cover.Dispose();
        bootStrap.GoToMainMenuScene();
    }

    // 본편 SkyCameraProductionManager.PlayAscendOut과 같다: 카메라 타깃을 지금 자리에서 높이만큼 올리고 SkyUP을 낸다.
    // 이징은 시퀀스 전체에 건다(안쪽 트윈은 DOTween 기본 이징).
    private IEnumerator PlayReturnAscend()
    {
        // 카메라 타깃은 Update(PlaceCamera)가 cameraPosition으로 매 프레임 옮기므로 cameraPosition을 올린다.
        Vector3 start = cameraPosition;
        float lift = 0f;

        Sound.PlayUI(SoundID.SkyUP);
        Sequence ascend = DOTween.Sequence();
        ascend.Append(DOTween.To(() => lift, _v =>
        {
            lift = _v;
            cameraPosition = start + new Vector3(0f, lift, 0f);
        }, returnAscendHeight, returnAscendDuration));
        ascend.SetEase(returnAscendEase);
        cameraLiftTween = ascend;
        yield return ascend.WaitForCompletion();
    }

    // 현지화 표는 UI 담당 범위라 건드리지 않는다. 임시로 한국어·영어만 두고 나머지 언어는 영어를 쓴다.
    private static void GetMessages(out string _thanks, out string _unlock)
    {
        if (Language.KR == FontLocalizer.CurrentLanguage)
        {
            _thanks = "플레이 해주셔서 감사합니다";
            _unlock = "로그라이크 모드 해제!";
            return;
        }

        _thanks = "Thank you for playing!";
        _unlock = "Roguelike Mode Unlocked!";
    }

    private void ApplyShadowColor()
    {
        if (null == shadowMaterials) return;

        int baseColorId = Shader.PropertyToID("_BaseColor");
        for (int i = 0; i < shadowMaterials.Length; i++)
        {
            if (null != shadowMaterials[i]) shadowMaterials[i].SetColor(baseColorId, shadowColor);
        }
    }

    private void OnDestroy()
    {
        if (null != cameraLiftTween && cameraLiftTween.IsActive()) cameraLiftTween.Kill();
        if (null != cameraPanTween && cameraPanTween.IsActive()) cameraPanTween.Kill();
    }
}
