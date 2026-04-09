using UnityEngine;

public class WeaponComponent : MonoBehaviour
{
    public virtual void Initialize()
    {
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
        spriteRenderer.enabled = _boolean;
    }
    public virtual void SetFacingDir(Transform _attackTransform) { }
}
