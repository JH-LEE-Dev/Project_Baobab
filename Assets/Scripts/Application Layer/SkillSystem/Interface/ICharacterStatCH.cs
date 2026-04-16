using UnityEngine;

public interface ICharacterStatCH
{
    public void CanHunting();
    public void IncreaseAxeDamage(float _amount);
    public void IncreaseGunDamage(float _amount);
    public void IncreaseSwitchSpeed(float _amount);
    public void StaminaDecreaseAlpha(float _amount);
    public void StaminaIncreaseAlpha(float _amount);
    public void IncreaseMaxStamina(float _amount);
}
