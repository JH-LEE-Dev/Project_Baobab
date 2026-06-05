using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

public class UIView_SkyProduction : UIView
{
    // //외부 의존성

    // //내부 의존성
    [SerializeField] private RectTransform cloudImage;
    [SerializeField] private RectTransform skyImage;
    [SerializeField] private RectTransform skyImage2;
    [SerializeField] private RectTransform cloudTargetTransform;
    [SerializeField] private RectTransform skyTargetTransform;
    [SerializeField] private float moveDuration = 1.0f;
    [SerializeField] private bool useCustomCurve = false;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Ease moveEase = Ease.OutCubic;
    [SerializeField] private float floatingAmplitude = 7.0f;
    [SerializeField] private float floatingDuration = 4.0f;

    private Sequence moveSequence;
    private Tween skyTween1;
    private Tween skyTween2;
    private Tween cloudTween;
    private Vector2 cloudStartPos;
    private Vector2 skyStartPos;
    private Vector2 skyStartPos2;
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
        KillFloatingSequence();
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
        KillFloatingSequence();
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
        if (skyImage2 != null)
            skyStartPos2 = skyImage2.anchoredPosition;

        hasStartPos = true;
    }

    private void PlayMoveSequence()
    {
        CacheStartPositions();
        KillMoveSequence();
        KillFloatingSequence();

        moveSequence = DOTween.Sequence();
        isMoved = !isMoved;

        Vector2 cloudDest = isMoved ? (cloudTargetTransform != null ? cloudTargetTransform.anchoredPosition : cloudStartPos) : cloudStartPos;
        Vector2 skyDest = isMoved ? (skyTargetTransform != null ? skyTargetTransform.anchoredPosition : skyStartPos) : skyStartPos;
        Vector2 skyDest2 = isMoved ? (skyTargetTransform != null ? skyTargetTransform.anchoredPosition : skyStartPos2) : skyStartPos2;

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

        if (null != skyImage2)
        {
            var tween = skyImage2.DOAnchorPos(skyDest2, moveDuration);
            if (useCustomCurve)
                tween.SetEase(moveCurve);
            else
                tween.SetEase(moveEase);
            moveSequence.Join(tween);
        }

        moveSequence.OnComplete(StartFloatingSequence);
    }

    private void StartFloatingSequence()
    {
        KillFloatingSequence();

        // Sequence를 쓰지 않고 개별 Tween으로 처리하여 경고를 방지합니다.
        if (null != skyImage)
        {
            Vector2 currentPos = skyImage.anchoredPosition;
            skyTween1 = skyImage.DOAnchorPosY(currentPos.y + (floatingAmplitude * 0.4f), floatingDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (null != skyImage2)
        {
            Vector2 currentPos = skyImage2.anchoredPosition;
            skyTween2 = skyImage2.DOAnchorPosY(currentPos.y - (floatingAmplitude * 0.7f), floatingDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (null != cloudImage)
        {
            Vector2 currentPos = cloudImage.anchoredPosition;
            cloudTween = cloudImage.DOAnchorPosY(currentPos.y + floatingAmplitude, floatingDuration * 0.85f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }

    private void KillMoveSequence()
    {
        if (null != moveSequence && true == moveSequence.IsActive())
        {
            moveSequence.Kill();
        }
    }

    private void KillFloatingSequence()
    {
        if (null != skyTween1 && true == skyTween1.IsActive())
        {
            skyTween1.Kill();
        }
        skyTween1 = null;

        if (null != skyTween2 && true == skyTween2.IsActive())
        {
            skyTween2.Kill();
        }
        skyTween2 = null;

        if (null != cloudTween && true == cloudTween.IsActive())
        {
            cloudTween.Kill();
        }
        cloudTween = null;
    }

    #endregion
}
