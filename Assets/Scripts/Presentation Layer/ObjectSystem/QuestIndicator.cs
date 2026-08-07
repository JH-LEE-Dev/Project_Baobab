using System.Collections.Generic;
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

    [Header("Target Shimmer")]
    [Tooltip("인디케이터가 떠 있는 동안 대상 오브젝트에 은은한 하얀 반짝임을 넣을지")]
    [SerializeField] private bool enableTargetShimmer = true;
    [Tooltip("반짝임 최대 강도 (0~1, _FlashAmount와 동일한 스케일)")]
    [SerializeField] private float shimmerMaxAmount = 0.25f;
    [Tooltip("반짝임이 밝아졌다 옅어지길 반복하는 속도")]
    [SerializeField] private float shimmerSpeed = 2f;

    // Custom-Sprite-Default 계열 셰이더가 공유하는 프로퍼티. CharacterVisualComponent/TreeVisualComponent/
    // OffroadVehicleObj의 피격·획득 플래시와 동일한 방식(MaterialPropertyBlock)으로 SRP 배칭을 깨지 않는다.
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private MaterialPropertyBlock shimmerMPB;
    private readonly List<SpriteRenderer> targetRenderers = new List<SpriteRenderer>(8);

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

        if (targetTransform != _target)
        {
            // 대상이 바뀌는 순간(PutItemsInLogContainer의 OffroadContainer→LogContainer 전환 등) 이전
            // 대상에 남아있는 반짝임을 지우고, 새 대상의 렌더러를 다시 캐싱한다.
            ClearTargetShimmer();
            targetTransform = _target;
            CacheTargetRenderers();
        }

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
        ClearTargetShimmer();
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
        ClearTargetShimmer();

        bShowing = false;
        targetTransform = null;
        targetRenderers.Clear();

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

    /// <summary>
    /// 현재 대상 트랜스폼 하위의 모든 SpriteRenderer를 캐싱한다. 반짝임 대상은 화살표가 가리키는
    /// 오브젝트 전체(예: 차량의 base/wheel/inner)이므로, 특정 렌더러 하나가 아니라 하위 전부를 모은다.
    /// </summary>
    private void CacheTargetRenderers()
    {
        targetRenderers.Clear();

        if (null != targetTransform)
            targetTransform.GetComponentsInChildren(true, targetRenderers);
    }

    private void ClearTargetShimmer()
    {
        ApplyShimmerToTargets(0f);
    }

    private void ApplyShimmerToTargets(float _amount)
    {
        if (targetRenderers.Count == 0)
            return;

        if (null == shimmerMPB)
            shimmerMPB = new MaterialPropertyBlock();

        for (int i = 0; i < targetRenderers.Count; i++)
        {
            SpriteRenderer _renderer = targetRenderers[i];
            if (null == _renderer)
                continue;

            _renderer.GetPropertyBlock(shimmerMPB);
            shimmerMPB.SetFloat(FlashAmountID, _amount);
            _renderer.SetPropertyBlock(shimmerMPB);
        }
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

        if (bShowing && enableTargetShimmer)
        {
            // 0~1을 은은하게 오가는 사인파. 최대값(shimmerMaxAmount)을 낮게 잡아 눈에 띄지 않게 억제한다.
            float _shimmer = (Mathf.Sin(Time.time * shimmerSpeed) * 0.5f + 0.5f) * shimmerMaxAmount;
            ApplyShimmerToTargets(_shimmer);
        }
    }

    private void OnDestroy()
    {
        KillSequence();
        ClearTargetShimmer();
    }
}
