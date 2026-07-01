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
    public void IncreaseShockWaveSpeed(float _amount);
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
    public void IncreaseWeakPointDamageMul(float _amount);
    public void IncreaseHelloDamage(float _amount);
    public void SetMultiAttack(bool _boolean);
    public void SetFinalAttackHealthPercent(float _percent);
    public void SetAttackRythmSpeedAmount(float _amount);
    public void ActivateWhirlWind(bool _boolean);
    public void IncreaseCriticalChance(float _amount);
    public void IncreaseCriticalDamage(float _amount);
    public void ActivateShockWaveCritical(bool _boolean);
    public void ActivateShockWaveEnforcement(bool _boolean);
    public void ShockWaveMastery(bool _boolean);
}
