using UnityEngine;

public class ArmAnimValueHandler
{
    private Animator anim;

    public readonly int bAttackHash = Animator.StringToHash("bAttack");


    public void Initialize(Animator _anim)
    {
        anim = _anim;
    }

    public void AttackEnd(bool _boolean)
    {
        anim.SetBool(bAttackHash, !_boolean);
    }
}
