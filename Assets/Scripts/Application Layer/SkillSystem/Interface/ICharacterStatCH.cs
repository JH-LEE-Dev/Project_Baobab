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
    public void IncreaseAmmoCap(int _amount);
    public void IncreaseMagCap(int _amount);
    public void IncreaseGunPenetration(float _amount);
    public void IncreaseRicochetCnt(int _amount);
    public void IncreaseSpeedWhileAction(float _amount);
    public void IncreaseShockWaveChance(float _amount);
    public void IncreaseShockWaveDamage(float _amount);
    public void IncreaseShockWaveDuration(float _amount);
    public void IncreaseAxeRangeMultiplier(float _amount);
    public void IncreaseAxeDurability(float _amount);
    public void IncreaseAxeDurabilityDecIgnoreChance(float _amount);
    public void IncreasePickupRange(float _amount);
    public void IncreaseRicochetRange(float _amount);
    public void IncreaseRicochetDamage(float _amount);
    public void IncreaseReloadSpeed(float _amount);
    public void IncreaseRifleAttackSpeed(float _amount);
    public void IncreaseMovementSpeed(float _amount);
    public void IncreaseAxeAttackSpeed(float _amount);
}
