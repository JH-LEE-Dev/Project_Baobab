using System;
using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class UnitLogicManager : MonoBehaviour, IUnitLogicProvider
{
    public event Action GameEndEvent;
    public event Action CharacterStaminaIsEmptyEvent;
    public event Action<WeaponMode> WeaponModeChangedEvent;
    public event Action TreeDetectedEvent;
    public event Action TreeDetectionClearedEvent;

    private Character character;
    private InputManager inputManager;

    [SerializeField] private List<StaminaAmountData> staminaData;

    public void Initialize(InputManager _inputManager)
    {
        inputManager = _inputManager;
    }

    public void Release()
    {
        ReleaseEvents();
    }

    private void BindEvents()
    {
        character.WeaponModeChangedEvent -= WeaponModeChanged;
        character.WeaponModeChangedEvent += WeaponModeChanged;

        character.StaminaIsEmptyEvent -= CharacterStaminaIsEmpty;
        character.StaminaIsEmptyEvent += CharacterStaminaIsEmpty;

        character.TreeDetectedEvent -= TreeDetected;
        character.TreeDetectedEvent += TreeDetected;

        character.TreeDetectionClearedEvent -= TreeDetectionCleared;
        character.TreeDetectionClearedEvent += TreeDetectionCleared;
    }

    private void ReleaseEvents()
    {
        character.WeaponModeChangedEvent -= WeaponModeChanged;
        character.StaminaIsEmptyEvent -= CharacterStaminaIsEmpty;
        character.TreeDetectedEvent -= TreeDetected;
        character.TreeDetectionClearedEvent -= TreeDetectionCleared;
    }

    public void SetCharacter(Character _character)
    {
        character = _character;

        if (character != null)
            BindEvents();
    }

    public void SetCharacterStaminaState(bool _bStaminaUpDown, float _staminaDecAmount, float _staminaIncAmount)
    {
        character.SetStaminaUpDownState(_bStaminaUpDown, _staminaDecAmount, _staminaIncAmount);
    }

    public void CharacterIsInDungeon(ForestType _forestType)
    {
        for (int i = 0; i < staminaData.Count; i++)
        {
            if (staminaData[i].forestType == _forestType)
            {
                SetCharacterStaminaState(false, staminaData[i].decAmount, 0);
                return;
            }
        }
    }

    public void SetCharacterTransform(Transform _transform)
    {
        character.transform.position = _transform.position;
    }

    public void SetCharacterPos(Vector3 _pos)
    {
        character.transform.position = _pos;
        Camera.main.transform.position = character.transform.position;
    }

    public Transform GetCharacterTransform()
    {
        return character.transform;
    }

    public void SetWhereIsCharacter(bool _bInDungeon)
    {
        character.SetWhereIsCharacter(_bInDungeon);
    }

    public void WeaponModeChanged(WeaponMode _currentMode)
    {
        WeaponModeChangedEvent?.Invoke(_currentMode);
    }

    public void CharacterSleep()
    {
        character.StaminaReset();
    }

    private void CharacterStaminaIsEmpty()
    {
        CharacterStaminaIsEmptyEvent?.Invoke();
        StartCoroutine(GameEnd());
    }

    private void TreeDetected()
    {
        TreeDetectedEvent?.Invoke();
    }

    private void TreeDetectionCleared()
    {
        TreeDetectionClearedEvent?.Invoke();
    }

    public void RefreshCharacter()
    {
        character.RefreshCharacterStat();
    }

    public void ActivateCharacter()
    {
        character.ActivateCharacter();
    }

    public void EnableCharacterAim()
    {
        character.EnableAim();
    }

    public void StartDecreaseStamina()
    {
        character.StartDecreaseStamina();
    }

    public void SetMinStaminaPercent(float _percent)
    {
        character.SetMinStaminaPercent(_percent);
    }

    private IEnumerator GameEnd()
    {
        yield return new WaitForSeconds(1.5f);
        GameEndEvent?.Invoke();
    }

    public void ResetCharacterStatus()
    {
        character.ResetStatus();
    }

    public void SourceOfStaminaRecover()
    {
        character.StaminaRecover();
    }
}
