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

    public void Initialize(AbilityNoticeStackPresenter _owner)
    {
        owner = _owner;
        BindReferencesIfNeeded();
        ResetForPool();
    }

    public void Show(string _message, RectTransform _targetPivot, float _entryOffsetX, float _showDuration, float _lifeTime, Ease _showEase)
    {
        BindReferencesIfNeeded();
        KillSequence();

        gameObject.SetActive(true);
        isVisible = true;
        isHiding = false;
        remainingTime = Mathf.Max(0.0f, _lifeTime);

        if (noticeText != null)
            noticeText.text = _message;

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

        isVisible = false;
        isHiding = true;

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

        isVisible = false;
        isHiding = false;
        remainingTime = 0.0f;

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
}
