/// <summary>
/// "마무리 벌목" 특성 판정. 남은 체력이 최대 체력의 일정 비율(StatComponent.finalAttackHealthPercent) 이하인 나무는
/// 남은 체력 전부를 피해로 받는다. 도끼(AttackComponent.ProcessAxeHit)와 부메랑·드론(연쇄 포함)·충격파가 같은 판정을 쓴다.
/// </summary>
public static class FinalAttackRule
{
    // 나무 체력은 데미지 누계로 정확히 판정한다(EHealthComponent.IsHealthRatioAtOrBelow). 그 밖의 체력은 예전 방식 그대로.
    public static bool IsHealthRatioAtOrBelow(IBaseHealthComponent _health, float _currentHp, float _maxHp, float _ratio)
    {
        if (_health is IHealthComponent enemyHealth) return enemyHealth.IsHealthRatioAtOrBelow(_ratio);
        return (_currentHp / _maxHp) <= _ratio;
    }

    /// <summary>
    /// _tree가 마무리 벌목 대상이면 true와 남은 체력을 돌려준다. 특성이 없으면(_ratio가 0 이하) 체력을 읽지 않고 false다
    /// (비율 0이면 살아 있는 나무는 어차피 대상이 아니다 - 남은 체력이 사망 판정 여유값 이하인 나무는 이미 죽었다).
    /// </summary>
    public static bool TryGetFinishingHp(TreeObj _tree, float _ratio, out float _currentHp)
    {
        _currentHp = 0f;
        if (_ratio <= 0f || null == _tree) return false;

        IBaseHealthComponent health = _tree.health;
        if (null == health) return false;

        float maxHp = health.GetMaxHealth();
        if (maxHp <= 0f) return false;

        _currentHp = health.GetCurrentHealth();
        return IsHealthRatioAtOrBelow(health, _currentHp, maxHp, _ratio);
    }

    /// <summary>
    /// 스킬 공격(부메랑·드론·충격파)의 피해에 마무리 벌목을 적용한다. 대상이면 남은 체력에 이번 타격의 치명타 배율(_damageMul)을
    /// 곱한 값(도끼와 같은 계산)을 주되, 원래 피해보다 약해지지는 않게 큰 쪽을 쓴다(포자막이 흡수하는 경우 등). 대상이 아니면 _damage 그대로.
    /// </summary>
    public static float ApplyToSkillDamage(TreeObj _tree, float _damage, float _ratio, float _damageMul)
    {
        if (false == TryGetFinishingHp(_tree, _ratio, out float currentHp)) return _damage;

        float finishDamage = currentHp * _damageMul;
        return finishDamage > _damage ? finishDamage : _damage;
    }
}
