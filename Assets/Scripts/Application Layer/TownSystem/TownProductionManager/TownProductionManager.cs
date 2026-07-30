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

    private InputManager inputManager;

    public OffroadVehicleObj offroadVehicleObj { get; private set; }
    public Character character { get; private set; }

    [SerializeField] private Transform offroadDriveEndPoint;

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

        characterRidePoint = offroadVehicleObj.CharacterRidePoint;
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
        Sound.Play(SoundID.OffroadOut, character.transform.position);
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
        yield return new WaitForSeconds(3.75f);

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

    public void RollbackCameraMove()
    {
        StartCoroutine(RollbackCameraMoveRoutine());
    }

    private IEnumerator RollbackCameraMoveRoutine()
    {
        yield return new WaitForSeconds(0.75f);

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
        if (bCurrentlyTownScene == true || bRetryGame == true)
            return;

        CameraUpDownEndEvent?.Invoke();
    }

    public void StartSkyProduction()
    {
        inputManager.PauseInteractKey(true);
        StartCoroutine(StartSkyProductionRoutine());
    }

    private IEnumerator PopupUIDown()
    {
        yield return new WaitForSeconds(1f);
        PopupUIDownEvent?.Invoke();
    }
}
