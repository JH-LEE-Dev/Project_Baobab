using DG.Tweening;
using TMPro;
using UnityEngine;

public class AbilityNoticeItem : MonoBehaviour
{
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text noticeText;

    private AbilityNoticeStackPresenter owner;
    private Sequence currentSequence;
    private Sequence refreshSequence;
    private Vector3 initialScale;
    private string noticeKey;
    private float remainingTime;
    private bool isVisible;
    private bool isHiding;

    public bool IsVisible
    {
        get { return isVisible; }
    }

    public bool IsHiding
    {
        get { return isHiding; }
    }

    public bool IsReusable
    {
        get { return isVisible == false && isHiding == false; }
    }

    public string NoticeKey
    {
        get { return noticeKey; }
    }

    public void Initialize(AbilityNoticeStackPresenter _owner)
    {
        owner = _owner;
        BindReferencesIfNeeded();
        initialScale = rectTransform != null ? rectTransform.localScale : Vector3.one;
        ResetForPool();
    }

    public void Show(string _key, string _message, RectTransform _targetPivot, float _entryOffsetX, float _showDuration, float _lifeTime, Ease _showEase)
    {
        BindReferencesIfNeeded();
        KillSequence();
        KillRefreshSequence();

        gameObject.SetActive(true);
        isVisible = true;
        isHiding = false;
        noticeKey = _key;
        remainingTime = Mathf.Max(0.0f, _lifeTime);
        rectTransform.localScale = initialScale;

        SetMessage(_message);

        if (canvasGroup != null)
            canvasGroup.alpha = 0.0f;

        Vector3 targetPosition = GetPivotPosition(_targetPivot);
        Vector3 startOffset = GetParentVector(Vector3.right * _entryOffsetX);
        rectTransform.position = targetPosition + startOffset;

        currentSequence = DOTween.Sequence();
        currentSequence.Join(rectTransform.DOMove(targetPosition, Mathf.Max(0.0f, _showDuration)).SetEase(_showEase));

        if (canvasGroup != null)
            currentSequence.Join(canvasGroup.DOFade(1.0f, Mathf.Max(0.0f, _showDuration)).SetEase(Ease.OutQuad));
    }

    public void Refresh(string _message, float _lifeTime, float _refreshDuration, Vector2 _squashScale, Vector2 _recoilScale, int _bounceCount, float _bounceDamping, Ease _squashEase, Ease _restoreEase)
    {
        if (isVisible == false || isHiding)
            return;

        BindReferencesIfNeeded();
        SetMessage(_message);
        ResetLifeTime(_lifeTime);
        PlayRefreshMotion(_refreshDuration, _squashScale, _recoilScale, _bounceCount, _bounceDamping, _squashEase, _restoreEase);
    }

    public bool Tick(float _deltaTime)
    {
        if (isVisible == false)
            return false;

        remainingTime -= _deltaTime;
        return remainingTime <= 0.0f;
    }

    public void MoveTo(RectTransform _targetPivot, float _moveDuration, Ease _moveEase)
    {
        if (isVisible == false || isHiding)
            return;

        BindReferencesIfNeeded();
        KillSequence();

        currentSequence = DOTween.Sequence();
        currentSequence.Join(rectTransform.DOMove(GetPivotPosition(_targetPivot), Mathf.Max(0.0f, _moveDuration)).SetEase(_moveEase));

        if (canvasGroup != null)
            currentSequence.Join(canvasGroup.DOFade(1.0f, Mathf.Max(0.0f, _moveDuration)).SetEase(Ease.OutQuad));
    }

    public void Hide(float _exitOffsetY, float _hideDuration, Ease _hideEase)
    {
        if (isHiding)
            return;

        BindReferencesIfNeeded();
        KillSequence();
        KillRefreshSequence();

        isVisible = false;
        isHiding = true;
        rectTransform.localScale = initialScale;

        Vector3 targetPosition = rectTransform.position + GetParentVector(Vector3.up * _exitOffsetY);

        currentSequence = DOTween.Sequence();
        currentSequence.Join(rectTransform.DOMove(targetPosition, Mathf.Max(0.0f, _hideDuration)).SetEase(_hideEase));

        if (canvasGroup != null)
            currentSequence.Join(canvasGroup.DOFade(0.0f, Mathf.Max(0.0f, _hideDuration)).SetEase(Ease.OutQuad));

        currentSequence.OnComplete(HandleHideComplete);
    }

    public void ResetForPool()
    {
        KillSequence();
        KillRefreshSequence();

        isVisible = false;
        isHiding = false;
        noticeKey = null;
        remainingTime = 0.0f;

        if (rectTransform != null)
            rectTransform.localScale = initialScale;

        if (canvasGroup != null)
            canvasGroup.alpha = 0.0f;

        gameObject.SetActive(false);
    }

    private void HandleHideComplete()
    {
        ResetForPool();

        if (owner != null)
            owner.OnNoticeReturned(this);
    }

    private Vector3 GetPivotPosition(RectTransform _pivot)
    {
        if (_pivot == null)
            return rectTransform.position;

        return _pivot.position;
    }

    private Vector3 GetParentVector(Vector3 _localVector)
    {
        Transform parentTransform = rectTransform.parent;
        if (parentTransform == null)
            return _localVector;

        return parentTransform.TransformVector(_localVector);
    }

    private void SetMessage(string _message)
    {
        if (noticeText != null)
            noticeText.text = _message;
    }

    private void ResetLifeTime(float _lifeTime)
    {
        remainingTime = Mathf.Max(0.0f, _lifeTime);
    }

    private void PlayRefreshMotion(float _refreshDuration, Vector2 _squashScale, Vector2 _recoilScale, int _bounceCount, float _bounceDamping, Ease _squashEase, Ease _restoreEase)
    {
        KillRefreshSequence();

        int bounceCount = Mathf.Max(_bounceCount, 1);
        float squashTimeRatio = 0.15f;
        float recoilTimeRatio = 0.2f;
        float restoreTimeRatio = 0.4f;
        float cycleRatio = squashTimeRatio + recoilTimeRatio;
        float totalRatio = Mathf.Max((cycleRatio * bounceCount) + restoreTimeRatio, 0.0001f);
        float squashDuration = Mathf.Max(0.0f, _refreshDuration) * Mathf.Clamp01(squashTimeRatio / totalRatio);
        float recoilDuration = Mathf.Max(0.0f, _refreshDuration) * Mathf.Clamp01(recoilTimeRatio / totalRatio);
        float restoreDuration = Mathf.Max(0.0f, _refreshDuration) * Mathf.Clamp01(restoreTimeRatio / totalRatio);

        Vector3 squashScale = new Vector3(initialScale.x * _squashScale.x, initialScale.y * _squashScale.y, initialScale.z);
        Vector3 recoilScale = new Vector3(initialScale.x * _recoilScale.x, initialScale.y * _recoilScale.y, initialScale.z);

        rectTransform.localScale = initialScale;
        refreshSequence = DOTween.Sequence();
        float intensity = 1.0f;

        for (int i = 0; i < bounceCount; i++)
        {
            Vector3 dampedSquashScale = Vector3.Lerp(initialScale, squashScale, intensity);
            Vector3 dampedRecoilScale = Vector3.Lerp(initialScale, recoilScale, intensity);

            refreshSequence.Append(rectTransform.DOScale(dampedSquashScale, squashDuration).SetEase(_squashEase));
            refreshSequence.Append(rectTransform.DOScale(dampedRecoilScale, recoilDuration).SetEase(Ease.OutQuad));

            intensity *= Mathf.Clamp01(_bounceDamping);
        }

        refreshSequence.Append(rectTransform.DOScale(initialScale, restoreDuration).SetEase(_restoreEase));
        refreshSequence.OnComplete(HandleRefreshComplete);
    }

    private void HandleRefreshComplete()
    {
        if (rectTransform != null)
            rectTransform.localScale = initialScale;
    }

    private void BindReferencesIfNeeded()
    {
        if (rectTransform == null)
            rectTransform = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (noticeText == null)
            noticeText = GetComponentInChildren<TMP_Text>(true);
    }

    private void KillSequence()
    {
        if (currentSequence == null)
            return;

        currentSequence.Kill();
        currentSequence = null;
    }

    private void KillRefreshSequence()
    {
        if (refreshSequence == null)
            return;

        refreshSequence.Kill();
        refreshSequence = null;
    }
}
