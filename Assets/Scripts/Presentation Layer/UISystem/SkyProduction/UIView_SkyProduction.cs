using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class UIView_SkyProduction : UIView
{
    // //외부 의존성

    // //내부 의존성
    [SerializeField] private RectTransform cloudImage;
    [SerializeField] private RectTransform skyImage;
    [SerializeField] private RectTransform cloudTargetTransform;
    [SerializeField] private RectTransform skyTargetTransform;
    [SerializeField] private float moveDuration = 1.0f;
    [SerializeField] private bool useCustomCurve = false;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Ease moveEase = Ease.OutCubic;

    private Sequence moveSequence;
    private Vector2 cloudStartPos;
    private Vector2 skyStartPos;
    private bool isMoved = false;
    private bool hasStartPos = false;

    #region Public Methods

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        CacheStartPositions();
    }

    public override void SetupUI()
    {
        base.SetupUI();
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    public override void Release()
    {
        KillMoveSequence();
        base.Release();
    }

    #endregion

    #region Protected Methods

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    #endregion

    #region Unity Events

    protected override void Awake()
    {
        base.Awake();
        CacheStartPositions();
    }

    public override void OnDestroy()
    {
        KillMoveSequence();
        base.OnDestroy();
    }

    public override void Update()
    {
        base.Update();

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
        {
            PlayMoveSequence();
        }
    }

    #endregion

    #region Private Methods

    private void CacheStartPositions()
    {
        if (hasStartPos) return;

        if (cloudImage != null)
            cloudStartPos = cloudImage.anchoredPosition;
        if (skyImage != null)
            skyStartPos = skyImage.anchoredPosition;

        hasStartPos = true;
    }

    private void PlayMoveSequence()
    {
        CacheStartPositions();
        KillMoveSequence();

        moveSequence = DOTween.Sequence();
        isMoved = !isMoved;

        Vector2 cloudDest = isMoved ? (cloudTargetTransform != null ? cloudTargetTransform.anchoredPosition : cloudStartPos) : cloudStartPos;
        Vector2 skyDest = isMoved ? (skyTargetTransform != null ? skyTargetTransform.anchoredPosition : skyStartPos) : skyStartPos;

        if (null != cloudImage)
        {
            var tween = cloudImage.DOAnchorPos(cloudDest, moveDuration);
            if (useCustomCurve)
                tween.SetEase(moveCurve);
            else
                tween.SetEase(moveEase);
            moveSequence.Join(tween);
        }

        if (null != skyImage)
        {
            var tween = skyImage.DOAnchorPos(skyDest, moveDuration);
            if (useCustomCurve)
                tween.SetEase(moveCurve);
            else
                tween.SetEase(moveEase);
            moveSequence.Join(tween);
        }
    }

    private void KillMoveSequence()
    {
        if (null != moveSequence && true == moveSequence.IsActive())
        {
            moveSequence.Kill();
        }
    }

    #endregion
}
