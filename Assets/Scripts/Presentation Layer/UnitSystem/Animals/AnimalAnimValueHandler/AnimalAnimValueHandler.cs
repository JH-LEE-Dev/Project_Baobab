using UnityEngine;

public class AnimalAnimValueHandler
{
    private Animator anim;

    private bool bIdleAnimType = false;

    public readonly int runStartEndHash = Animator.StringToHash("bRunStartEnd");
    public readonly int idleTypeHash = Animator.StringToHash("bType");


    public void Initialize(Animator _anim)
    {
        anim = _anim;
    }

    public void RunStartEnd(bool _boolean)
    {
        anim.SetBool(runStartEndHash, _boolean);
    }

    public void IdleEnd()
    {
        bIdleAnimType = Random.value < 0.5f;
        anim.SetBool(idleTypeHash, bIdleAnimType);
    }
}
