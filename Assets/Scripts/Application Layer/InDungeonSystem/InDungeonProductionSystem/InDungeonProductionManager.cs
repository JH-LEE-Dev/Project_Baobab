using System;
using System.Collections;
using UnityEngine;

public class InDungeonProductionManager : MonoBehaviour
{
    // 이벤트
    public event Action CameraUpIsEndEvent;
    public event Action CameraDownEndEvent;
    public event Action RollbackSkyProductionEvent;

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
    }

    private void ReleaseEvents()
    {
        skyCameraProductionManager.SkyProductionEndEvent -= CameraUpIsEnd;
        skyCameraProductionManager.SkyProductionRollbackEndEvent -= CameraDownIsEnd;
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
        character.gameObject.SetActive(false);

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

    private void CameraUpIsEnd()
    {
        if (bCurrentlyDungeonScene == false)
            return;

        character.gameObject.SetActive(true);
        CameraUpIsEndEvent?.Invoke();
    }

    private void CameraDownIsEnd()
    {
        if (bCurrentlyDungeonScene == true && bRetryGame == false)
            return;

        inputManager.PauseMove(false);

        CameraDownEndEvent?.Invoke();
    }

    public void StartSkyProduction()
    {
        character.col.enabled = false;
        skyCameraProductionManager.StartCameraMove();
    }

    private IEnumerator RollbackCameraMoveRoutine()
    {
        yield return new WaitForSeconds(1.5f);

        RollbackSkyProductionEvent?.Invoke();
        skyCameraProductionManager.StartCameraMove();
    }

    public void RollbackCameraMove()
    {
        StartCoroutine(RollbackCameraMoveRoutine());
    }

}
