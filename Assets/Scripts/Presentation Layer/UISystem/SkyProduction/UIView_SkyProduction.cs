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

    #region Public Methods

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
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

    private void PlayMoveSequence()
    {
        KillMoveSequence();

        moveSequence = DOTween.Sequence();

        if (null != cloudImage && null != cloudTargetTransform)
        {
            var tween = cloudImage.DOAnchorPos(cloudTargetTransform.anchoredPosition, moveDuration);
            if (useCustomCurve)
                tween.SetEase(moveCurve);
            else
                tween.SetEase(moveEase);
            moveSequence.Join(tween);
        }

        if (null != skyImage && null != skyTargetTransform)
        {
            var tween = skyImage.DOAnchorPos(skyTargetTransform.anchoredPosition, moveDuration);
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
