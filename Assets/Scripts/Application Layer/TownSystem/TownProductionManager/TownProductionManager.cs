using System;
using System.Collections;
using UnityEngine;

public class TownProductionManager : MonoBehaviour
{
    public event Action OffroadDriveEndEvent;
    public event Action CharacterRideEndEvent;
    public event Action<bool> StartSkyProductionEvent;
    public event Action RollbackSkyProductionEvent;
    public event Action CameraUpIsEndEvent;
    public event Action CameraUpDownEndEvent;
    public event Action PopupUIDownEvent;
    public event Action MainMenuCurtainRollbackEvent;
    public event Action MainMenuIntroEndEvent;
    public event Action GoToMainMenuReadyEvent;
    public event Action GoToMainMenuCurtainRevealEvent;
    /// <summary>엔딩 출발: 주인과 강아지가 모두 차에 탄 순간(주행 직전).</summary>
    public event Action EndingBoardedEvent;
    /// <summary>엔딩 출발: 카메라가 하늘로 올라가 상공 체공까지 끝난 순간. 여기서 엔딩 씬으로 넘어간다.</summary>
    public event Action EndingAscendCompleteEvent;

    private InputManager inputManager;

    public OffroadVehicleObj offroadVehicleObj { get; private set; }
    public Character character { get; private set; }

    [SerializeField] private Transform offroadDriveEndPoint;

    [Tooltip("컨테이너가 차 위에 안착한 뒤 하늘(카메라 상승) 연출이 시작되기까지의 대기 시간")]
    [SerializeField] private float skyProductionDelayAfterLanding = 0.5f;

    [Header("Ending")]
    [Tooltip("엔딩 출발 때 화면을 덮는 하늘. 씬이 바뀌어도 남아 엔딩 씬에서 걷힌다.")]
    [SerializeField] private EndingSkyCover endingSkyCoverPrefab;
    // 엔딩 출발은 차량·하늘 연출 모두 두 번 빨라지기 전 초기 버전(cf38c73f) 그대로다:
    // 주행 시작부터 3.75초 고정 타이머 뒤 하늘 연출이 시작되고, 카메라는 1.8초 상승 뒤 상공에서 0.5초 머문다.
    [Tooltip("엔딩 출발: 주행 시작부터 하늘 연출이 시작되기까지(cf38c73f StartSkyProductionRoutine)")]
    [SerializeField] private float endingSkyProductionDelay = 3.75f;
    [SerializeField] private float endingAscendDuration = 1.8f;
    [SerializeField] private float endingAscendHoverSeconds = 0.5f;

    private Coroutine endingDepartureCoroutine;
    private Coroutine endingSkyCoroutine;

    // 하늘 연출을 예약해 둔 주행이 진행 중인지. 컨테이너 안착 이벤트는 주행 연출에서만 오지만,
    // 예약 없이 들어온 안착에 반응해 연출이 제멋대로 시작되는 일이 없도록 명시적으로 잠근다.
    private bool bSkyProductionPending = false;

    private Transform characterRidePoint;
    private Coroutine characterRideCoroutine;
    private SkyCameraProductionManager skyCameraProductionManager;
    private Vector3 originalRidePosition;
    public bool bCanGetOff = true;

    public bool bCurrentlyTownScene = true;
    public bool bRetryGame = false;

    public void Initialize(InputManager _inputManager, SkyCameraProductionManager _skyCameraProductionManager)
    {
        inputManager = _inputManager;
        skyCameraProductionManager = _skyCameraProductionManager;

        BindEvents();
    }

    private void BindEvents()
    {
        skyCameraProductionManager.SkyProductionEndEvent -= CameraUpIsEnd;
        skyCameraProductionManager.SkyProductionEndEvent += CameraUpIsEnd;

        skyCameraProductionManager.SkyProductionRollbackEndEvent -= CameraDownIsEnd;
        skyCameraProductionManager.SkyProductionRollbackEndEvent += CameraDownIsEnd;

        skyCameraProductionManager.IntroRevealEndEvent -= MainMenuIntroEnd;
        skyCameraProductionManager.IntroRevealEndEvent += MainMenuIntroEnd;

        skyCameraProductionManager.AscendOutEndEvent -= GoToMainMenuAscendComplete;
        skyCameraProductionManager.AscendOutEndEvent += GoToMainMenuAscendComplete;
    }

    private void ReleaseEvents()
    {
        skyCameraProductionManager.SkyProductionEndEvent -= CameraUpIsEnd;
        skyCameraProductionManager.SkyProductionRollbackEndEvent -= CameraDownIsEnd;
        skyCameraProductionManager.IntroRevealEndEvent -= MainMenuIntroEnd;
        skyCameraProductionManager.AscendOutEndEvent -= GoToMainMenuAscendComplete;
    }

    public void Release()
    {
        ReleaseEvents();

        if (offroadVehicleObj != null)
        {
            offroadVehicleObj.OffroadDriveEndEvent -= DriveEnd;
            offroadVehicleObj.ContainerLandedOnVehicleEvent -= ContainerLandedOnVehicle;
        }

        bSkyProductionPending = false;

        if (endingDepartureCoroutine != null)
        {
            StopCoroutine(endingDepartureCoroutine);
            endingDepartureCoroutine = null;
        }

        if (endingSkyCoroutine != null)
        {
            StopCoroutine(endingSkyCoroutine);
            endingSkyCoroutine = null;
        }

        if (characterRideCoroutine != null)
        {
            StopCoroutine(characterRideCoroutine);
            characterRideCoroutine = null;
        }
    }

    public void Offroad_DI(OffroadVehicleObj _offroadVehicleObj)
    {
        offroadVehicleObj = _offroadVehicleObj;

        offroadVehicleObj.OffroadDriveEndEvent -= DriveEnd;
        offroadVehicleObj.OffroadDriveEndEvent += DriveEnd;

        offroadVehicleObj.ContainerLandedOnVehicleEvent -= ContainerLandedOnVehicle;
        offroadVehicleObj.ContainerLandedOnVehicleEvent += ContainerLandedOnVehicle;

        characterRidePoint = offroadVehicleObj.CharacterRidePoint;

        // 새 차량을 받는 시점(=마을 진입)에 하차 가능 상태를 되돌린다.
        // bCanGetOff는 던전 확정 때 false가 되고 오직 DriveEnd(주행 완료)에서만 true로 돌아오는데,
        // 그 이벤트를 발행하는 차량은 씬 오브젝트라 씬 언로드와 함께 파괴된다. 즉 주행이 씬 전환보다
        // 늦게 끝나면 이벤트가 영영 오지 않고, 이 매니저는 살아남으므로 false가 다음 마을 세션까지
        // 따라간다. 그러면 차량에 탑승한 뒤 내비를 취소해도 GetOffFromTheVehicle()이 조기 반환해
        // 캐릭터가 비활성 + 이동 잠금 상태로 차 안에 갇힌다.
        // 지금 타이밍상으로는 주행이 먼저 끝나지만, 연출 시간(StartSkyProductionRoutine의 대기 ·
        // SkyCameraProductionManager.moveDuration)이나 주행 거리를 조정하면 곧바로 깨지는 전제라
        // 진입 시점에 못 박아둔다.
        bCanGetOff = true;
    }

    public void Character_DI(Character _character)
    {
        character = _character;
    }

    public void StartDrive()
    {
        StartSkyProduction();
        offroadVehicleObj.StartDrive(offroadDriveEndPoint);
        StartCoroutine(PopupUIDown());
    }

    /// <summary>
    /// 주행 연출에서 컨테이너가 차 위에 안착한 순간. 여기서부터
    /// skyProductionDelayAfterLanding만큼 뒤에 카메라가 하늘로 올라가기 시작한다.
    /// </summary>
    private void ContainerLandedOnVehicle()
    {
        if (bSkyProductionPending == false) return;

        bSkyProductionPending = false;
        StartCoroutine(StartSkyProductionRoutine());
    }

    /// <summary>
    /// 엔딩 출발. 주인이 평소처럼 차에 타고, 강아지가 달려와 같은 방식으로 탄 뒤, 숲 선택 없이 곧바로 출발한다.
    /// 주행과 하늘 연출은 두 번 빨라지기 전 초기 버전(cf38c73f) 그대로다(StartEndingDrive).
    /// </summary>
    public bool StartEndingDeparture(PuppyManager _puppyManager)
    {
        if (character == null || characterRidePoint == null || offroadVehicleObj == null) return false;

        if (characterRideCoroutine != null)
        {
            StopCoroutine(characterRideCoroutine);
            characterRideCoroutine = null;
        }

        if (endingDepartureCoroutine != null)
            StopCoroutine(endingDepartureCoroutine);

        endingDepartureCoroutine = StartCoroutine(EndingDepartureRoutine(_puppyManager));
        return true;
    }

    private IEnumerator EndingDepartureRoutine(PuppyManager _puppyManager)
    {
        // 1. 주인 탑승 - CharacterRideRoutine과 같은 연출이다(내비게이션을 여는 탑승 완료 이벤트만 보내지 않는다).
        inputManager.PauseMove(true);

        originalRidePosition = character.transform.position;
        character.transform.position = characterRidePoint.position;
        Sound.Play(SoundID.OffroadClose, character.transform.position);
        character.bRide = true;
        character.gameObject.SetActive(false);
        offroadVehicleObj.PlayShinyEffect();
        character.EnableShadow();

        offroadVehicleObj.SetActiveWheelForStencil(false);
        yield return offroadVehicleObj.CharacterRideLandingImpactSequence();
        yield return new WaitForSeconds(0.25f);

        // 2. 강아지가 주인이 차에 오른 자리까지 달려와 주인과 똑같이 탄다.
        if (_puppyManager != null && _puppyManager.HasPuppyInTown)
        {
            bool bArrived = false;
            _puppyManager.BoardVehicle(originalRidePosition, () => bArrived = true);
            while (bArrived == false) yield return null;

            _puppyManager.HidePuppyForRide();
            Sound.Play(SoundID.OffroadClose, characterRidePoint.position);
            offroadVehicleObj.PlayShinyEffect();
            yield return offroadVehicleObj.CharacterRideLandingImpactSequence();
            yield return new WaitForSeconds(0.25f);
        }

        EndingBoardedEvent?.Invoke();

        // 3. 출발
        StartEndingDrive();

        endingDepartureCoroutine = null;
    }

    /// <summary>
    /// cf38c73f의 StartDrive 그대로: 상호작용 키를 잠그고, 차량은 상자 도약·안착이 끝난 뒤 시동을 거는 당시 순서로 달리며,
    /// 하늘 연출은 상자 안착이 아니라 주행 시작부터 센 고정 시간 뒤에 시작한다. 1초 뒤 HUD가 내려가는 것도 같다.
    /// </summary>
    private void StartEndingDrive()
    {
        inputManager.PauseInteractKey(true);

        if (endingSkyCoroutine != null) StopCoroutine(endingSkyCoroutine);
        endingSkyCoroutine = StartCoroutine(StartEndingSkyRoutine());

        offroadVehicleObj.StartDrive(offroadDriveEndPoint, true);
        StartCoroutine(PopupUIDown());
    }

    // cf38c73f의 StartSkyProductionRoutine 순서: BGM 페이드아웃 → 카메라 상승 → 구름이 덮음(같은 프레임).
    private IEnumerator StartEndingSkyRoutine()
    {
        yield return new WaitForSeconds(endingSkyProductionDelay);

        // 카메라가 실제로 하늘로 올라가기 시작하는 이 시점에 맞춰 타운 BGM을 페이드아웃한다.
        Sound.FadeOutBGM(endingAscendDuration);

        skyCameraProductionManager.PlayAscendForEnding(character.transform, endingAscendDuration, endingAscendHoverSeconds, EndingAscendComplete);

        EndingSkyCover cover = EndingSkyCover.Create(endingSkyCoverPrefab);
        if (cover != null) cover.PlayCover();

        endingSkyCoroutine = null;
    }

    private void EndingAscendComplete()
    {
        // StartEndingDrive에서 건 상호작용 키 잠금은 평소 던전 도착 뒤 PopupUIUp에서 풀린다.
        // 엔딩은 그 경로를 타지 않고 세션이 끝나므로 여기서 짝을 맞춘다(InputManager는 씬을 넘어 살아남는다).
        inputManager.PauseInteractKey(false);

        EndingAscendCompleteEvent?.Invoke();
    }

    public void StartCharacterRide()
    {
        if (character == null) return;

        if (characterRideCoroutine != null)
        {
            StopCoroutine(characterRideCoroutine);
        }

        characterRideCoroutine = StartCoroutine(CharacterRideRoutine());
    }

    private IEnumerator CharacterRideRoutine()
    {
        if (character == null || characterRidePoint == null) yield break;

        inputManager.PauseMove(true);
        //character.DisableShadow();

        originalRidePosition = character.transform.position;
        Vector3 startPos = originalRidePosition;
        Vector3 startScale = character.transform.localScale;

        character.transform.position = characterRidePoint.position;
        Sound.Play(SoundID.OffroadClose, character.transform.position);
        character.bRide = true;
        character.gameObject.SetActive(false);
        offroadVehicleObj.PlayShinyEffect();
        character.EnableShadow();

        // 캐릭터 도착 시 차량 착륙 임팩트 연출 실행
        if (offroadVehicleObj != null)
        {
            offroadVehicleObj.SetActiveWheelForStencil(false);
            yield return offroadVehicleObj.CharacterRideLandingImpactSequence(InvokeCharacterRideEndEvent);
        }

        characterRideCoroutine = null;
    }

    private void InvokeCharacterRideEndEvent()
    {
        StartCoroutine(InvokeCharacterRideEndEventRoutine());
    }

    private IEnumerator InvokeCharacterRideEndEventRoutine()
    {
        yield return new WaitForSeconds(0.25f);
        CharacterRideEndEvent?.Invoke();
    }

    private void DriveEnd()
    {
        character.SetFacingDirection(Vector2.down);
        character.ResetStatus();

        bCanGetOff = true;
        OffroadDriveEndEvent?.Invoke();
    }

    public void GetOffFromTheVehicle()
    {
        if (character == null || offroadVehicleObj == null || bCanGetOff == false)
        {
            return;
        }

        // 1. 캐릭터 복구 및 위치 설정
        character.gameObject.SetActive(true);
        character.transform.position = originalRidePosition;
        character.bRide = false;
        character.EnableShadow();

        offroadVehicleObj.SetActiveWheelForStencil(true);

        // 3. 탑승 위치에서 내리는 위치를 바라보도록 설정
        if (offroadVehicleObj.CharacterRidePoint != null)
        {
            Vector3 getOffDir = originalRidePosition - offroadVehicleObj.CharacterRidePoint.position;
            character.SetFacingDirection(getOffDir);
        }

        // 4. 입력 제어 해제
        if (inputManager != null)
        {
            inputManager.PauseMove(false);
        }
    }

    public void SetbCanGetOff(bool _boolean)
    {
        bCanGetOff = _boolean;
    }

    private IEnumerator StartSkyProductionRoutine()
    {
        // 기준점은 컨테이너가 차 위에 안착한 순간(ContainerLandedOnVehicle). 예전엔 StartDrive
        // 시점부터 3.75초를 세는 고정 타이머라, 도약 · 시동 연출 길이를 손대면 하늘 연출이
        // 차량 연출의 아무 지점에서나 시작됐다.
        yield return new WaitForSeconds(skyProductionDelayAfterLanding);

        // 카메라가 실제로 하늘로 올라가기 시작하는 이 시점에 맞춰 타운 BGM을 페이드아웃한다.
        Sound.FadeOutBGM(skyCameraProductionManager.MoveDuration);

        skyCameraProductionManager.StartCameraMove();
        StartSkyProductionEvent?.Invoke(false);
    }

    public void SetCharacterTransform()
    {
        skyCameraProductionManager.SetCharacterTransform(character.transform);
    }

    /// <summary>
    /// MainMenu → Town 최초 진입 전용 연출. Town↔Dungeon 왕복에 쓰이는 isMoved/bCurrentlyTownScene 상태는 건드리지 않는다.
    /// </summary>
    public void StartMainMenuIntro()
    {
        if (character == null)
        {
            // 정상 흐름에선 unitSystem.CreateCharacter()가 SetupGameInstaller()보다 먼저 실행되어 항상 존재하지만,
            // 방어적으로: character가 없으면 카메라 연출을 못 하므로, 메인 메뉴 커튼만 즉시 걷어내고 인트로 종료 처리까지 흘려보내
            // 메인 메뉴가 화면에 영구히 남거나 입력이 잠기는 상황을 방지한다.
            Debug.LogWarning("[TownProductionManager] StartMainMenuIntro: character가 null이라 카메라 인트로를 건너뜁니다. 메인 메뉴 커튼만 즉시 걷고 종료 처리합니다.");
            MainMenuCurtainRollbackEvent?.Invoke();
            MainMenuIntroEndEvent?.Invoke();
            return;
        }

        inputManager.PauseMove(true);
        inputManager.PauseESCKey(true); // 메인메뉴→타운 인트로 연출 시작 - 종료 시점은 TownSystem.MainMenuIntroEnd()

        // 카메라 하강 시작과 같은 타이밍에 메인 메뉴 커튼이 걷히도록 먼저 발행한다.
        MainMenuCurtainRollbackEvent?.Invoke();

        // UIView_SkyProduction(구름)도 카메라 하강과 같은 타이밍에 재생 (기존 StartDrive/RollbackCameraMove와 동일한 배선 재사용)
        StartSkyProductionEvent?.Invoke(true);

        skyCameraProductionManager.PlayIntroDescend(character.transform);
    }

    private void MainMenuIntroEnd()
    {
        MainMenuIntroEndEvent?.Invoke();
    }

    /// <summary>
    /// Town → MainMenu 전용 연출. StartMainMenuIntro()의 반대 방향이며, 기존 왕복용
    /// isMoved/StartCameraMove()/SkyProductionEndEvent는 전혀 건드리지 않는다.
    /// </summary>
    public void StartGoToMainMenu()
    {
        if (character == null)
        {
            Debug.LogWarning("[TownProductionManager] StartGoToMainMenu: character가 null이라 카메라 연출을 건너뜁니다.");
            GoToMainMenuCurtainRevealEvent?.Invoke();
            GoToMainMenuReadyEvent?.Invoke();
            return;
        }

        inputManager.PauseMove(true);
        inputManager.PauseESCKey(true); // 타운→메인메뉴 이탈 연출 시작 - 종료 시점은 BootStrap.SetupMainMenuScene()

        // 카메라 상승 시작과 같은 타이밍에 메인 메뉴 패널이 슬라이드 인 되도록 먼저 발행한다 (버튼/딤머/로고는 아직 안 보임).
        GoToMainMenuCurtainRevealEvent?.Invoke();

        StartSkyProductionEvent?.Invoke(true);
        PopupUIDownEvent?.Invoke();

        skyCameraProductionManager.PlayAscendOut(character.transform);
    }

    private void GoToMainMenuAscendComplete()
    {
        GoToMainMenuReadyEvent?.Invoke();
    }

    public void RollbackCameraMove(bool _bNoDelay = false)
    {
        StartCoroutine(RollbackCameraMoveRoutine(_bNoDelay));
    }

    private IEnumerator RollbackCameraMoveRoutine(bool _bNoDelay)
    {
        // 상공 체공 대기(예전 0.375초)는 없앴다. 한 프레임만 넘겨, 이 호출과 같은 프레임에 이어지는
        // 던전 셋업(OnTreesReady의 나머지 처리)이 전부 끝난 뒤에 하강이 시작되는 순서만 유지한다.
        if (!_bNoDelay) yield return null;

        RollbackSkyProductionEvent?.Invoke();
        skyCameraProductionManager.StartCameraMove();
    }

    private void CameraUpIsEnd()
    {
        if (bCurrentlyTownScene == false)
            return;

        character.bRide = false;
        character.ResetStatus();
        CameraUpIsEndEvent?.Invoke();
    }

    private void CameraDownIsEnd()
    {
        // 주의: 여기 bRetryGame은 재도전 하강을 걸러내지 못한다. TownSystem.DungeonStarted가
        // DungeonStartSignal 시점에 이 플래그를 지우는데, 그 하강 자체는 같은 메서드의 더 뒤쪽
        // (InDungeonSystem.OnTreesReady → inDungeonProductionManager.RollbackCameraMove)에서
        // 시작되기 때문이다. 실제 차단은 TownSystem.CameraDownIsEnd의 bRetryArrivalOwnedByDungeon이 한다.
        if (bCurrentlyTownScene == true || bRetryGame == true)
            return;

        CameraUpDownEndEvent?.Invoke();
    }

    /// <summary>
    /// 하늘 연출을 예약한다. 실제 시작은 컨테이너가 차 위에 안착한 뒤
    /// skyProductionDelayAfterLanding만큼 지난 시점이다(ContainerLandedOnVehicle).
    /// 상호작용 키 잠금은 연출을 기다리는 동안에도 유지되어야 하므로 예약 시점에 곧바로 건다.
    /// </summary>
    public void StartSkyProduction()
    {
        inputManager.PauseInteractKey(true);
        bSkyProductionPending = true;
    }

    private IEnumerator PopupUIDown()
    {
        yield return new WaitForSeconds(1f);
        PopupUIDownEvent?.Invoke();
    }
}
