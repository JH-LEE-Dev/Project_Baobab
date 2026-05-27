using UnityEngine;

public class BeltObj : MonoBehaviour
{
    public Animator animator;
    public SpriteRenderer spriteRenderer;
    public int dir;
    public bool bRev = false;

    private readonly int dirHash = Animator.StringToHash("dir");
    private readonly int bRevHash = Animator.StringToHash("bRev");

    private CustomSortable customSortable;

    public void Initialize()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        animator.SetFloat(dirHash, dir);
        animator.SetBool(bRevHash, bRev);

        customSortable = GetComponent<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.AddSpriteRenderer(spriteRenderer);
    }

    private void LateUpdate()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }
}
