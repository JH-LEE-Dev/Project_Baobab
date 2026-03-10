using UnityEngine;

public class UnitLogicManager : MonoBehaviour, IUnitLogicProvider
{
    private Character character;

    public void Initialize()
    {

    }

    public void SetCharacter(Character _character)
    {
        character = _character;
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
    }

    public Transform GetCharacterTransform()
    {
        return character.transform;
    }
}
