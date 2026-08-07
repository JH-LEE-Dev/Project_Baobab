using DG.Tweening;
using UnityEngine;

/// <summary>
/// 퀘스트 대상 오브젝트 위에 떠서 목표 위치를 알려주는 화살표 인디케이터.
/// 대상 트랜스폼을 매 프레임 따라다니며 위아래로 둥둥 떠다니고, 등장/퇴장은 스케일 + 페이드로 처리한다.
/// 화살표 스프라이트는 프리팹의 SpriteRenderer(arrowRenderer)에 직접 바인딩한다.
/// 표시/숨김 시점은 TutorialQuestIndicatorManager가 튜토리얼 신호를 받아 결정한다.
/// </summary>
public class QuestIndicator : MonoBehaviour
{
    // 내부 의존성
    [Header("Visual")]
    [Tooltip("화살표 스프라이트를 바인딩할 SpriteRenderer")]
    [SerializeField] private SpriteRenderer arrowRenderer;

    [Header("Float Motion")]
    [Tooltip("위아래로 둥둥 떠다니는 진폭(월드 단위)")]
    [SerializeField] private float floatAmplitude = 0.1f;
    [Tooltip("위아래로 둥둥 떠다니는 속도")]
    [SerializeField] private float floatSpeed = 2.5f;

    [Header("Show / Hide")]
    [SerializeField] private float showDuration = 0.3f;
    [SerializeField] private float hideDuration = 0.15f;
    [SerializeField] private Vector3 hiddenScale = new Vector3(0.3f, 0.3f, 1f);
    [SerializeField] private Vector3 shownScale = Vector3.one;
    [SerializeField] private Ease showEase = Ease.OutBack;
    [SerializeField] private Ease hideEase = Ease.InBack;

    // 내부 상태
    private Transform targetTransform;
    private Vector3 worldOffset;
    private float floatTime;
    private Sequence currentSequence;
    private bool bShowing;

    public bool isShowing => bShowing;

    /// <summary>
    /// 지정한 대상 위에 인디케이터를 띄운다. 이미 떠 있는 상태라면 등장 연출을 다시 재생하지 않고
    /// 추적 대상만 갈아끼운다(퀘스트가 연달아 바뀔 때 깜빡이지 않도록).
    /// </summary>
    public void Show(Transform _target, Vector3 _worldOffset)
    {
        if (null == _target)
            return;

        targetTransform = _target;
        worldOffset = _worldOffset;

        gameObject.SetActive(true);

        if (bShowing)
        {
            // 대상만 바뀐 경우 - 등장 첫 프레임부터 새 위치에 붙어 있도록 즉시 반영한다.
            FollowTarget();
            return;
        }

        bShowing = true;
        floatTime = 0f;

        KillSequence();

        // 등장 연출이 시작되기 전에 위치를 먼저 잡아둔다. 그렇지 않으면 이전 대상 위치에서
        // 한 프레임 동안 커지는 게 보인다.
        FollowTarget();

        transform.localScale = hiddenScale;
        SetAlpha(0f);

        currentSequence = DOTween.Sequence().SetLink(gameObject);
        currentSequence.Append(transform.DOScale(shownScale, showDuration).SetEase(showEase));

        if (null != arrowRenderer)
            currentSequence.Join(arrowRenderer.DOFade(1f, showDuration).SetEase(Ease.OutQuad));

        currentSequence.OnComplete(() => currentSequence = null);
    }

    /// <summary>
    /// 퇴장 연출 후 오브젝트를 비활성화한다.
    /// </summary>
    public void Hide()
    {
        if (false == bShowing)
        {
            targetTransform = null;
            gameObject.SetActive(false);
            return;
        }

        bShowing = false;
        KillSequence();

        currentSequence = DOTween.Sequence().SetLink(gameObject);
        currentSequence.Append(transform.DOScale(hiddenScale, hideDuration).SetEase(hideEase));

        if (null != arrowRenderer)
            currentSequence.Join(arrowRenderer.DOFade(0f, hideDuration).SetEase(Ease.InQuad));

        currentSequence.OnComplete(() =>
        {
            currentSequence = null;
            targetTransform = null;
            gameObject.SetActive(false);
        });
    }

    /// <summary>
    /// 연출 없이 즉시 숨긴다(씬 전환 등으로 대상이 사라질 때).
    /// </summary>
    public void HideImmediately()
    {
        KillSequence();

        bShowing = false;
        targetTransform = null;

        SetAlpha(0f);
        transform.localScale = hiddenScale;
        gameObject.SetActive(false);
    }

    private void FollowTarget()
    {
        if (null == targetTransform)
            return;

        float _bob = Mathf.Sin(floatTime) * floatAmplitude;
        transform.position = targetTransform.position + worldOffset + new Vector3(0f, _bob, 0f);
    }

    private void SetAlpha(float _alpha)
    {
        if (null == arrowRenderer)
            return;

        Color _color = arrowRenderer.color;
        _color.a = _alpha;
        arrowRenderer.color = _color;
    }

    private void KillSequence()
    {
        if (null != currentSequence && currentSequence.IsActive())
        {
            currentSequence.Kill();
        }

        currentSequence = null;
    }

    // 유니티 이벤트 함수
    private void LateUpdate()
    {
        if (null == targetTransform)
            return;

        // 대상이 꺼져 있는 동안(마을에 있을 때의 던전 차량 등)에는 추적은 유지하되 그리지 않는다.
        bool bTargetVisible = targetTransform.gameObject.activeInHierarchy;
        if (null != arrowRenderer && arrowRenderer.enabled != bTargetVisible)
        {
            arrowRenderer.enabled = bTargetVisible;
        }

        floatTime += Time.deltaTime * floatSpeed;
        FollowTarget();
    }

    private void OnDestroy()
    {
        KillSequence();
    }
}
