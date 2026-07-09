using UnityEngine;

public interface IInDungeonObjManagerCH
{
    public void IncreaseGrowthSpeed(float _amount);
    public void IncreaseRepairBoxCount(float _amount);
    public void IncreaseRepairAmount(float _amount);
    public void IncreaseShieldDamageMultiplier(float _amount);
    public void IncreaseShieldPenetration(float _amount);
    public void IncreaseShieldRegenReduction(float _amount);
    public void UnlockShieldExplosion(bool _boolean);
    public void IncreaseShieldExplosionDamage(float _amount);
    public void IncreaseShieldExplosionRange(float _amount);
    public void IncreaseShieldExplosionResearchChance(float _amount);
    public void UnlockConstellationManifest(bool _boolean);
    public void IncreaseStarMarkDamage(float _amount);
    public void IncreaseConstellationDamage(float _amount);
    public void IncreaseConstellationHitCount(float _amount);
    public void IncreaseManifestationBrandBonus(float _amount);
}
