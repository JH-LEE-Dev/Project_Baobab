using System.Collections;
using UnityEngine;
using DG.Tweening;
using System;

public class OffroadContainerVComponent : MonoBehaviour
{
    public event Action ContainerOpenedEvent;
    public event Action ContainerClosedEvent;

    // 외부 의존성
    [SerializeField] private float roofHeight = 1.0f;

    // 내부 의존성
    private SpriteRenderer spriteRenderer;
    private CustomSortable customSortable;
    private Animator anim;
    private Transform parentTransform;
    private Transform parentTransformForOpen;
    private float currentHeight = 0f;
    private Vector3 originalScale;
    private Quaternion originalRot;
    private Vector3 originalSelfScale;
    private Quaternion originalSelfRot;
    private Vector3 originalJumpContainerScale;
    private Quaternion originalJumpContainerRot;
    private Vector3 originalOpenScale;
    private Quaternion originalOpenRot;

    public readonly int bOpenHash = Animator.StringToHash("bOpen");

    public bool bActive = true;

    [SerializeField] private GameObject outlineStencilObj;
    [SerializeField] private GameObject outlineObj;
    [SerializeField] private GameObject containerForJump;
    [SerializeField] private GameObject containerForOpen;
    [SerializeField] private GameObject carriedContainer;
    private Material originalMaterial;

    private SpriteRenderer outlineStencilSR;
    private SpriteRenderer outlineSR;
    private Sprite currentSprite;

    public void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        customSortable = GetComponent<CustomSortable>();

        parentTransform = containerForJump.transform.parent != null ? containerForJump.transform.parent : transform;
        parentTransformForOpen = containerForOpen.transform.parent != null ? containerForOpen.transform.parent : transform;

        customSortable.Initialize(transform);
        customSortable.AddSpriteRenderer(spriteRenderer);
        anim = GetComponent<Animator>();

        originalScale = parentTransform.localScale;
        originalRot = parentTransform.localRotation;
        originalOpenScale = parentTransformForOpen.localScale;
        originalOpenRot = parentTransformForOpen.localRotation;
        originalSelfScale = transform.localScale;
        originalSelfRot = transform.localRotation;
        if (containerForJump != null)
        {
            originalJumpContainerScale = containerForJump.transform.localScale;
            originalJumpContainerRot = containerForJump.transform.localRotation;
        }

        originalMaterial = spriteRenderer.material;

        outlineStencilSR = outlineStencilObj.GetComponent<SpriteRenderer>();
        outlineSR = outlineObj.GetComponentInChildren<SpriteRenderer>();
    }

    public void LateUpdate()
    {
        // CustomSortable에게 현재 공중에 떠 있는 높이(arc + 지붕높이)를 전달하여 정렬 보정
        customSortable.SetHeight(currentHeight);
        customSortable.ManualLateUpdate();

        currentSprite = spriteRenderer.sprite;
        outlineStencilSR.sprite = currentSprite;
        outlineSR.sprite = currentSprite;

        outlineSR.sortingOrder = outlineSR.sortingOrder + 1;
    }

    public IEnumerator JumpSequence(Vector3 _targetPos, float _jumpHeight, float _duration, float _springFreq, float _springDamping)
    {
        // 점프 시작 시 활성화/비활성화 처리
        if (containerForJump != null)
        {
            containerForJump.SetActive(true);
        }
        spriteRenderer.enabled = false;

        Vector3 startPos = parentTransform.position;

        // 1. [부모 스케일 조절] 점프 전 납작해지는 준비 단계 (Anticipation - 0.15초)
        float prepDuration = 0.15f;
        float prepElapsed = 0f;
        Vector3 squashedScale = new Vector3(originalScale.x * 1.3f, originalScale.y * 0.5f, originalScale.z);

        while (prepElapsed < prepDuration)
        {
            float t = prepElapsed / prepDuration;
            float ease = t * (2f - t); // OutQuad
            parentTransform.localScale = Vector3.Lerp(originalScale, squashedScale, ease);
            prepElapsed += Time.deltaTime;
            yield return null;
        }
        parentTransform.localScale = squashedScale;

        // 2. [부모 스케일 조절] 뽀잉 솟구치며 원래 크기로 원복 (0.08초 동안 살짝 늘어났다가 원래 크기로 복구)
        float bounceDuration = 0.02f;
        float bounceElapsed = 0f;
        Vector3 parentStretchedScale = new Vector3(originalScale.x * 0.85f, originalScale.y * 1.25f, originalScale.z);

        while (bounceElapsed < bounceDuration)
        {
            float t = bounceElapsed / bounceDuration;
            parentTransform.localScale = Vector3.Lerp(squashedScale, parentStretchedScale, t);
            bounceElapsed += Time.deltaTime;
            yield return null;
        }
        parentTransform.localScale = originalScale;

        // 3. 포물선 점프 단계
        float jumpElapsed = 0f;
        Vector3 targetLandScaleForJump = originalJumpContainerScale * 0.5f; // 공중 이동 중 0.5배까지 스케일 축소

        while (jumpElapsed < _duration)
        {
            float t = jumpElapsed / _duration;

            // 수평 및 수직(포물선) 이동을 Transform이 직접 수행
            Vector3 groundLerpPos = Vector3.Lerp(startPos, _targetPos, t);
            float arc = Mathf.Sin(t * Mathf.PI) * _jumpHeight;
            parentTransform.position = groundLerpPos + new Vector3(0, arc, 0);

            // CustomSortable을 위한 Height 계산: 
            float ascendingHeight = t * roofHeight;
            currentHeight = ascendingHeight + arc;

            if (containerForJump != null)
            {
                // containerForJump 회전 연출 (720도 회전 적용)
                containerForJump.transform.localRotation = Quaternion.Euler(0f, 0f, t * -720f) * originalJumpContainerRot;

                // containerForJump 스케일 연출 (원래 크기에서 0.5배까지 선형적으로 감소)
                containerForJump.transform.localScale = Vector3.Lerp(originalJumpContainerScale, targetLandScaleForJump, t);
            }

            jumpElapsed += Time.deltaTime;
            yield return null;
        }

        // 4. 안착 단계
        parentTransform.position = _targetPos;
        currentHeight = roofHeight;
        parentTransform.localScale = originalScale * 0.25f; // 착지 시 부모 스케일 0.25배 안착
        transform.localScale = originalSelfScale;
        transform.localRotation = originalSelfRot;

        {
            containerForJump.SetActive(false);
            carriedContainer.SetActive(true);
        }
    }

    private Coroutine openCoroutine;

    public void Open()
    {
        if (bActive == false || anim.GetBool(bOpenHash)) return;

        parentTransformForOpen.DOKill();

        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
        }

        openCoroutine = StartCoroutine(OpenSequence());
    }

    private IEnumerator OpenSequence()
    {
        Vector3 startScale = parentTransformForOpen.localScale;
        Vector3 squashedScale = new Vector3(originalOpenScale.x * 1.3f, originalOpenScale.y * 0.5f, originalOpenScale.z);
        Vector3 stretchedScale = new Vector3(originalOpenScale.x * 0.8f, originalOpenScale.y * 1.25f, originalOpenScale.z);
        Vector3 bounceScale = new Vector3(originalOpenScale.x * 1.1f, originalOpenScale.y * 0.9f, originalOpenScale.z);

        // 1. 납작해짐 (Anticipation - 0.15초)
        float elapsed = 0f;
        float duration = 0.15f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransformForOpen.localScale = Vector3.LerpUnclamped(startScale, squashedScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransformForOpen.localScale = squashedScale;

        // 2. Animator 트리거 + 뒤뚱거림
        anim.SetBool(bOpenHash, true);
        parentTransformForOpen.DOPunchRotation(new Vector3(0f, 0f, 15f), 0.2f, 8, 1f);
        ContainerOpenedEvent?.Invoke();
        // 3. 위로 뽀잉 솟구침 (0.15초)
        elapsed = 0f;
        duration = 0.15f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransformForOpen.localScale = Vector3.LerpUnclamped(squashedScale, stretchedScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransformForOpen.localScale = stretchedScale;

        // 4. 아래로 살짝 찌그러짐 (0.12초)
        elapsed = 0f;
        duration = 0.08f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * t * (3f - 2f * t); // SmoothStep
            parentTransformForOpen.localScale = Vector3.LerpUnclamped(stretchedScale, bounceScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransformForOpen.localScale = bounceScale;

        // 5. 원래 크기로 복귀 (0.15초)
        elapsed = 0f;
        duration = 0.11f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransformForOpen.localScale = Vector3.LerpUnclamped(bounceScale, originalOpenScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }

        parentTransformForOpen.localScale = originalOpenScale;
        parentTransformForOpen.localRotation = originalOpenRot;
        openCoroutine = null;
    }

    private Coroutine closeCoroutine;

    public void Close()
    {
        if (!anim.GetBool(bOpenHash)) return;

        parentTransformForOpen.DOKill();

        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
            openCoroutine = null;
        }

        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
        }

        closeCoroutine = StartCoroutine(CloseSequence());
    }

    private IEnumerator CloseSequence()
    {
        Vector3 startScale = parentTransformForOpen.localScale;
        Vector3 stretchedScale = new Vector3(originalOpenScale.x * 0.8f, originalOpenScale.y * 1.2f, originalOpenScale.z);
        Vector3 squashedScale = new Vector3(originalOpenScale.x * 1.35f, originalOpenScale.y * 0.55f, originalOpenScale.z);
        Vector3 bounceScale = new Vector3(originalOpenScale.x * 0.93f, originalOpenScale.y * 1.07f, originalOpenScale.z);

        // 1. 살짝 위로 늘어남 (준비 동작 - 0.12초)
        anim.SetBool(bOpenHash, false);
        float elapsed = 0f;
        float duration = 0.12f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransformForOpen.localScale = Vector3.LerpUnclamped(startScale, stretchedScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransformForOpen.localScale = stretchedScale;

        // 2. 쾅! 닫히며 강하게 찌그러짐 + 뒤뚱거림 (0.05초)
        parentTransformForOpen.DOPunchRotation(new Vector3(0f, 0f, 20f), 0.5f, 10, 1f);

        ContainerClosedEvent?.Invoke();

        elapsed = 0f;
        duration = 0.05f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * t * t; // InCubic (급격하게 쿵!)
            parentTransformForOpen.localScale = Vector3.LerpUnclamped(stretchedScale, squashedScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransformForOpen.localScale = squashedScale;

        // 3. 반동으로 튕겨올라감 (0.07초)
        elapsed = 0f;
        duration = 0.07f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransformForOpen.localScale = Vector3.LerpUnclamped(squashedScale, bounceScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransformForOpen.localScale = bounceScale;

        // 4. 원래 크기로 안착 (0.08초)
        elapsed = 0f;
        duration = 0.08f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * t * (3f - 2f * t); // SmoothStep
            parentTransformForOpen.localScale = Vector3.LerpUnclamped(bounceScale, originalOpenScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }

        parentTransformForOpen.localScale = originalOpenScale;
        parentTransformForOpen.localRotation = originalOpenRot;
        closeCoroutine = null;
    }

    public void SetOutlineMaterial()
    {
        outlineStencilObj.SetActive(true);
    }

    public void ResetMaterial()
    {
        outlineStencilObj.SetActive(false);
    }

    public void Reset()
    {
        spriteRenderer.enabled = true;
        containerForJump.SetActive(false);
        carriedContainer.SetActive(false);
    }
}
