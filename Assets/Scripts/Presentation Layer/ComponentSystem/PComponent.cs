using UnityEngine;

public class PComponent : MonoBehaviour
{
    protected ComponentCtx ctx;

    public virtual void Initialize(ComponentCtx _ctx)
    {
        ctx = _ctx;
    }
}
