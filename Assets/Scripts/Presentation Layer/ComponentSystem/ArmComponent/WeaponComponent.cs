using UnityEngine;

public class WeaponComponent : MonoBehaviour
{
    protected ComponentCtx ctx;
    protected bool bCanAction = false;
    protected float durability = 100f;
    public virtual void Initialize(ComponentCtx _ctx)
    {
        ctx = _ctx;

        anim = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        armAnimValueHandler = new ArmAnimValueHandler();
        armAnimValueHandler.Initialize(anim);
    }

    public ArmAnimValueHandler armAnimValueHandler { get; private set; }
    public Animator anim { get; private set; }
    public SpriteRenderer spriteRenderer { get; private set; }
    public virtual void SetEnable(bool _boolean)
    {
        bCanAction = _boolean;
        spriteRenderer.enabled = _boolean;
    }
    public virtual void SetFacingDir(Transform _attackTransform) { }
    public virtual void LeftButtonClicked() { }
    public virtual void LeftButtonReleased() { }
    public virtual void DecreaseDurability() { }
    public virtual void ResetDurability() { durability = 100f; }
}
