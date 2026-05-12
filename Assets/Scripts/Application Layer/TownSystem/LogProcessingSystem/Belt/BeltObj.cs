using UnityEngine;

public class BeltObj : MonoBehaviour
{
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public int dir;

    private readonly int dirHash = Animator.StringToHash("dir");

    public void Initialize()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        animator.SetFloat(dirHash, dir);
    }
}
