using UnityEngine;

public class CutterAnimTrigger : MonoBehaviour
{
    private Animator anim;

    public void Awake()
    {
        anim = GetComponent<Animator>();
    }

    public void CuttingEnd()
    {
        anim.speed = 1.0f;
    }

    public void BladeBack()
    {
        anim.speed = 1.15f;
    }
}
