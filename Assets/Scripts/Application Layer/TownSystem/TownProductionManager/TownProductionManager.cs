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

    private InputManager inputManager;

    public OffroadVehicleObj offroadVehicleObj { get; private set; }
    public Character character { get; private set; }

    [SerializeField] private Transform offroadDriveEndPoint;

    private Transform characterRidePoint;
    private Coroutine characterRideCoroutine;
    private SkyCameraProductionComponent skyCameraProductionComponent;
    private bool bCanGetOff = true;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;

        skyCameraProductionComponent = GetComponent<SkyCameraProductionComponent>();

        BindEvents();
    }

    private void BindEvents()
    {
        skyCameraProductionComponent.SkyProductionEndEvent -= CameraUpIsEnd;
        skyCameraProductionComponent.SkyProductionEndEvent += CameraUpIsEnd;

        skyCameraProductionComponent.SkyProductionRollbackEndEvent -= CameraDownIsEnd;
        skyCameraProductionComponent.SkyProductionRollbackEndEvent += CameraDownIsEnd;
    }

    private void ReleaseEvents()
    {
        skyCameraProductionComponent.SkyProductionEndEvent -= CameraUpIsEnd;
        skyCameraProductionComponent.SkyProductionRollbackEndEvent -= CameraDownIsEnd;    
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
    }

    public void StartCharacterRide()
    {
        if (character == null) return;

        character.col.enabled = false;

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
        character.DisableShadow();

        Vector3 startPos = character.transform.position;
        Vector3 startScale = character.transform.localScale;

        character.transform.position = characterRidePoint.position;
        character.SetFacingDirection(characterRidePoint.position - startPos);
        character.transform.localScale = startScale; // 다음 탑승 및 평소 상태를 위해 스케일 원상 복구
        character.SetHeight(0f);
        character.gameObject.SetActive(false);

        character.EnableShadow();

        // 캐릭터 도착 시 차량 착륙 임팩트 연출 실행
        if (offroadVehicleObj != null)
        {
            yield return offroadVehicleObj.CharacterRideLandingImpactSequence(() => CharacterRideEndEvent?.Invoke());
        }

        characterRideCoroutine = null;
    }

    private void DriveEnd()
    {
        character.col.enabled = true;
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

        // 2. 캐릭터 컴포넌트 상태 복구
        character.col.enabled = true;
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

        skyCameraProductionComponent.StartCameraMove();
        StartSkyProductionEvent?.Invoke();
    }

    public void SetCharacterTransform()
    {
        skyCameraProductionComponent.SetCharacterTransform(character.transform);
    }

    public void RollbackCameraMove()
    {
        StartCoroutine(RollbackCameraMoveRoutine());
    }

    private IEnumerator RollbackCameraMoveRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        RollbackSkyProductionEvent?.Invoke();
        skyCameraProductionComponent.StartCameraMove();
    }

    private void CameraUpIsEnd()
    {
        CameraUpIsEndEvent?.Invoke();
    }

    private void CameraDownIsEnd()
    {
        inputManager.PauseInteractKey(false);
        inputManager.PauseMove(false);
        CameraUpDownEndEvent?.Invoke();
    }

    public void StartSkyProduction()
    {
        inputManager.PauseInteractKey(true);
        StartCoroutine(StartSkyProductionRoutine());
    }
}
