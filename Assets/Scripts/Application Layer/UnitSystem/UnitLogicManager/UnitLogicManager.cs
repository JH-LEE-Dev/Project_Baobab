using System;
using UnityEngine;

public class UnitLogicManager : MonoBehaviour, IUnitLogicProvider
{
    public event Action CharacterStaminaIsEmptyEvent;
    public event Action<WeaponMode> WeaponModeChangedEvent;

    private Character character;

    public void Initialize()
    {

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
    }

    private void ReleaseEvents()
    {
        character.WeaponModeChangedEvent -= WeaponModeChanged;
        character.StaminaIsEmptyEvent -= CharacterStaminaIsEmpty;
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
    }

    public void RefreshCharacter()
    {
        character.RefreshCharacterStat();
    }
}
