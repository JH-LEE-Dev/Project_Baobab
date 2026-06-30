
using UnityEngine;

public interface IDamageable
{
    void TakeDamage(float _damage);
    void KnockBack(Vector2 _knockBackDir,float _knockBackForce);
    bool bCanApplyDamage { get; }
    IBaseHealthComponent health { get; }
}
