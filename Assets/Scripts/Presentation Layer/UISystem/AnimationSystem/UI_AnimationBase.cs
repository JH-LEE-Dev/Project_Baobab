using UnityEngine;
using DG.Tweening;

[System.Serializable]
public abstract class UI_AnimationBase
{
    [Header("Animation Settings")]
    
    [SerializeField] protected RectTransform objRect;

    public bool joinAnimation { get; } = false;
    public float waitForSec { get; } = 0f;
    protected float duration = 0.3f;
    protected Ease easeType = Ease.InQuad;

    public abstract void Initialize();

    public abstract void ResetState();

    public abstract Tween PlayEnter();
}
