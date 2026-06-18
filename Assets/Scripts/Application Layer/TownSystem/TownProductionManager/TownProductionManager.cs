using System;
using System.Collections;
using UnityEngine;

public class TownProductionManager : MonoBehaviour
{
    public event Action OffroadDriveEndEvent;
    public event Action CharacterRideEndEvent;
    public event Action StartSkyProductionEvent;
    public event Action RollbackSkyProductionEvent;
    public event Action CameraUpIsEndEvent;
    public event Action CameraUpDownEndEvent;
    public event Action PopupUIDownEvent;

    private InputManager inputManager;

    public OffroadVehicleObj offroadVehicleObj { get; private set; }
    public Character character { get; private set; }

    [SerializeField] private Transform offroadDriveEndPoint;

    private Transform characterRidePoint;
    private Coroutine characterRideCoroutine;
    private SkyCameraProductionManager skyCameraProductionManager;
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
    }

    private void ReleaseEvents()
    {
        skyCameraProductionManager.SkyProductionEndEvent -= CameraUpIsEnd;
        skyCameraProductionManager.SkyProductionRollbackEndEvent -= CameraDownIsEnd;
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

        Vector3 startPos = character.transform.position;
        Vector3 startScale = character.transform.localScale;

        character.transform.position = characterRidePoint.position;
        character.gameObject.SetActive(false);

        character.EnableShadow();

        // 캐릭터 도착 시 차량 착륙 임팩트 연출 실행
        if (offroadVehicleObj != null)
        {
            yield return offroadVehicleObj.CharacterRideLandingImpactSequence(InvokeCharacterRideEndEvent);
        }

        characterRideCoroutine = null;
    }

    private void InvokeCharacterRideEndEvent()
    {
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
        if (character == null || offroadVehicleObj == null || offroadVehicleObj.getOffTransform == null || bCanGetOff == false)
        {
            return;
        }

        // 1. 캐릭터 복구 및 위치 설정
        character.gameObject.SetActive(true);
        character.transform.position = offroadVehicleObj.getOffTransform.position;

        character.EnableShadow();

        // 3. 탑승 위치에서 내리는 위치를 바라보도록 설정
        if (offroadVehicleObj.CharacterRidePoint != null)
        {
            Vector3 getOffDir = offroadVehicleObj.getOffTransform.position - offroadVehicleObj.CharacterRidePoint.position;
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

        skyCameraProductionManager.StartCameraMove();
        StartSkyProductionEvent?.Invoke();
    }

    public void SetCharacterTransform()
    {
        skyCameraProductionManager.SetCharacterTransform(character.transform);
    }

    public void RollbackCameraMove()
    {
        StartCoroutine(RollbackCameraMoveRoutine());
    }

    private IEnumerator RollbackCameraMoveRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        RollbackSkyProductionEvent?.Invoke();
        skyCameraProductionManager.StartCameraMove();
    }

    private void CameraUpIsEnd()
    {
        if (bCurrentlyTownScene == false)
            return;

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
