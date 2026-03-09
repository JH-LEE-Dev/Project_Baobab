using UnityEngine;

public class EComponent : MonoBehaviour
{
    private EComponentCtx ctx;

    public void Initialize(EComponentCtx _ctx)
    {
        ctx = _ctx;
    }
}
