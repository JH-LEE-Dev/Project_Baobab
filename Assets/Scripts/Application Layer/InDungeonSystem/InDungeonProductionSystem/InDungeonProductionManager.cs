using System;
using System.Collections;
using UnityEngine;

public class InDungeonProductionManager : MonoBehaviour
{
    //외부 의존성
    private InputManager inputManager;
    public OffroadVehicleObj offroadVehicleObj { get; private set; }
    public Character character { get; private set; }

    //내부 의존성
    private Transform characterRidePoint;
    private Coroutine characterRideCoroutine;

    public event Action CharacterRideEndEvent;

    private SkyCameraProductionManager skyCameraProductionManager;

    public void Initialize(InputManager _inputManager, SkyCameraProductionManager _skyCameraProductionManager)
    {
        inputManager = _inputManager;
        skyCameraProductionManager = _skyCameraProductionManager;
    }

    public void Release()
    {
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
        character.SetFacingDirection(characterRidePoint.position - startPos);
        character.transform.localScale = startScale;
        character.SetHeight(0f);
        character.gameObject.SetActive(false);

        character.EnableShadow();

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
}
