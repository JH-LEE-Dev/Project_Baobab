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
    [SerializeField] private Sprite loadingCloudSprite_MainMenu;
    [SerializeField] private Sprite loadingCloudSprite;

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
            var tween = cloudImage.DOAnchorPos(cloudDest, moveDuration, true);
            if (useCustomCurve)
                tween.SetEase(moveCurve);
            else
                tween.SetEase(moveEase);
            moveSequence.Join(tween);
        }

        if (null != skyImage)
        {
            var tween = skyImage.DOAnchorPos(skyDest, moveDuration, true);
            if (useCustomCurve)
                tween.SetEase(moveCurve);
            else
                tween.SetEase(moveEase);
            moveSequence.Join(tween);
        }

        if (null != skyImage2)
        {
            var tween = skyImage2.DOAnchorPos(skyDest2, moveDuration, true);
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
                .SetOptions(true)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (null != skyImage2)
        {
            Vector2 currentPos = skyImage2.anchoredPosition;
            skyTween2 = skyImage2.DOAnchorPosY(currentPos.y - (floatingAmplitude * 0.4f), floatingDuration)
                .SetOptions(true)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (null != cloudImage)
        {
            Vector2 currentPos = cloudImage.anchoredPosition;
            cloudTween = cloudImage.DOAnchorPosY(currentPos.y + floatingAmplitude, floatingDuration * 0.85f)
                .SetOptions(true)
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

    public void StartSkyProduction()
    {
        PlayMoveSequence();
    }

    /// <summary>
    /// 애니메이션 없이 즉시 "구름이 덮인" 상태로 스냅한다. MainMenu → Town 최초 진입 시,
    /// 화면이 메인 메뉴에 가려져 있는 동안 미리 덮인 상태로 세팅해두고, 이후 StartSkyProduction()
    /// 한 번으로 카메라 하강과 같은 타이밍에 "걷히는" 연출만 재생하기 위한 용도.
    /// </summary>
    public void SnapToCoveredState()
    {
        CacheStartPositions();
        KillMoveSequence();
        KillFloatingSequence();

        SetMainMenuMode(true);

        isMoved = true;

        if (cloudImage != null)
            cloudImage.anchoredPosition = cloudTargetTransform != null ? cloudTargetTransform.anchoredPosition : cloudStartPos;

        if (skyImage != null)
            skyImage.anchoredPosition = skyTargetTransform != null ? skyTargetTransform.anchoredPosition : skyStartPos;

        if (skyImage2 != null)
            skyImage2.anchoredPosition = skyTargetTransform != null ? skyTargetTransform.anchoredPosition : skyStartPos2;
    }

    /// <summary>
    /// 메인 메뉴 연출 등 특정 상황에서 배경 하늘(Sky)과 보조 구름(LoadingSkyCloud) 객체를 끄고 켤 수 있으며, 구름 스프라이트를 변경합니다.
    /// </summary>
    public void SetMainMenuMode(bool isMainMenu)
    {
        if (null != skyImage)
        {
            skyImage.gameObject.SetActive(!isMainMenu);
        }
        
        if (null != skyImage2)
        {
            skyImage2.gameObject.SetActive(!isMainMenu);
        }

        if (null != cloudImage)
        {
            var img = cloudImage.GetComponent<UnityEngine.UI.Image>();
            if (img != null)
            {
                img.sprite = isMainMenu ? loadingCloudSprite_MainMenu : loadingCloudSprite;
            }
        }
    }
}
