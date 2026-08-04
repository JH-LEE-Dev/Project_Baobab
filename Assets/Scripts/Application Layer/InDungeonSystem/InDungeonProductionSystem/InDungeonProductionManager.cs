using System;
using System.Collections;
using UnityEngine;

public class InDungeonProductionManager : MonoBehaviour
{
    // 이벤트
    public event Action CameraUpIsEndEvent;
    public event Action CameraDownEndEvent;
    public event Action RollbackSkyProductionEvent;
    public event Action GoToMainMenuReadyEvent;
    public event Action GoToMainMenuCurtainRevealEvent;

    //외부 의존성
    private InputManager inputManager;
    public OffroadVehicleObj offroadVehicleObj { get; private set; }
    public Character character { get; private set; }

    //내부 의존성
    private Transform characterRidePoint;
    private Coroutine characterRideCoroutine;

    public event Action CharacterRideEndEvent;

    private SkyCameraProductionManager skyCameraProductionManager;

    public bool bCurrentlyDungeonScene = false;
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

        skyCameraProductionManager.AscendOutEndEvent -= GoToMainMenuAscendComplete;
        skyCameraProductionManager.AscendOutEndEvent += GoToMainMenuAscendComplete;
    }

    private void ReleaseEvents()
    {
        skyCameraProductionManager.SkyProductionEndEvent -= CameraUpIsEnd;
        skyCameraProductionManager.SkyProductionRollbackEndEvent -= CameraDownIsEnd;
        skyCameraProductionManager.AscendOutEndEvent -= GoToMainMenuAscendComplete;
    }

    public void Release()
    {
        ReleaseEvents();

        if (characterRideCoroutine != null)
        {
            StopCoroutine(characterRideCoroutine);
            characterRideCoroutine = null;
        }
    }

    public void Offroad_DI(OffroadVehicleObj _offroadVehicleObj)
    {
        offroadVehicleObj = _offroadVehicleObj;
        characterRidePoint = offroadVehicleObj.CharacterRidePoint;
    }

    public void Character_DI(Character _character)
    {
        character = _character;
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
        // 던전 탈출(귀환) 확정 후 차량 탑승 시작 - 취소 가능한 UI 없이 곧장 결과창/귀환으로 이어지므로 여기서부터 막아도 안전하다.
        // 종료 시점은 InDungeonProductionManager.CameraDownIsEnd()(타운 도착) 또는 재시도 시 같은 지점.
        inputManager.PauseESCKey(true);
        //character.DisableShadow();

        Vector3 startPos = character.transform.position;
        Vector3 startScale = character.transform.localScale;

        character.transform.position = characterRidePoint.position;
        Sound.Play(SoundID.OffroadClose, character.transform.position);
        character.bRide = true;
        character.gameObject.SetActive(false);
        offroadVehicleObj.PlayShinyEffect();

        // 착륙 임팩트에 맞춘 카메라 셰이크 + 줌 펀치 연출
        //CameraMoveController.Instance?.ShakeCamera(3f, 0.2f);
        //CameraMoveController.Instance?.ZoomCamera(1.025f, 0.06f, 0.03f, 0.11f);

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

    private void CameraUpIsEnd()
    {
        if (bCurrentlyDungeonScene == false)
            return;

        character.bRide = false;
        character.gameObject.SetActive(true);
        CameraUpIsEndEvent?.Invoke();
    }

    private void CameraDownIsEnd()
    {
        if (bCurrentlyDungeonScene == true && bRetryGame == false)
            return;

        inputManager.PauseMove(false);
        inputManager.PauseESCKey(false); // 던전→타운 귀환 연출 종료 (InDungeonSystem.GoHome()에서 걸어둔 PauseESCKey(true) 해제)

        // 캐릭터가 실제로 움직일 수 있게 되는 시점(던전 -> 타운)에 타운 BGM을 재생한다.
        // bRetryGame이면 타운이 아니라 같은 타입의 새 던전으로 이어지므로 재생하지 않는다.
        if (!bRetryGame)
        {
            Sound.PlayBGM(SoundID.TownBGM);
        }

        CameraDownEndEvent?.Invoke();
    }

    public void StartSkyProduction()
    {
        skyCameraProductionManager.StartCameraMove();
    }

    /// <summary>
    /// Dungeon → MainMenu 전용 연출. 기존 왕복용 isMoved/StartCameraMove()/SkyProductionEndEvent는 건드리지 않는다.
    /// </summary>
    public void StartGoToMainMenu()
    {
        if (character == null)
        {
            Debug.LogWarning("[InDungeonProductionManager] StartGoToMainMenu: character가 null이라 카메라 연출을 건너뜁니다.");
            GoToMainMenuCurtainRevealEvent?.Invoke();
            GoToMainMenuReadyEvent?.Invoke();
            return;
        }

        inputManager.PauseMove(true);
        inputManager.PauseESCKey(true); // 던전→메인메뉴 이탈 연출 시작 - 종료 시점은 BootStrap.SetupMainMenuScene()

        GoToMainMenuCurtainRevealEvent?.Invoke();

        skyCameraProductionManager.PlayAscendOut(character.transform);
    }

    private void GoToMainMenuAscendComplete()
    {
        GoToMainMenuReadyEvent?.Invoke();
    }

    private IEnumerator RollbackCameraMoveRoutine()
    {
        yield return new WaitForSeconds(0.75f);

        RollbackSkyProductionEvent?.Invoke();
        skyCameraProductionManager.StartCameraMove();
    }

    public void RollbackCameraMove()
    {
        StartCoroutine(RollbackCameraMoveRoutine());
    }

}
