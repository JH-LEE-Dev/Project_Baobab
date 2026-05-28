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
    private float currentHeight = 0f;
    private Vector3 originalScale;
    private Quaternion originalRot;

    public readonly int bOpenHash = Animator.StringToHash("bOpen");

    public bool bActive = true;

    public void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        customSortable = GetComponent<CustomSortable>();

        parentTransform = transform.parent != null ? transform.parent : transform;

        customSortable.Initialize(transform);
        customSortable.AddSpriteRenderer(spriteRenderer);
        anim = GetComponent<Animator>();

        originalScale = parentTransform.localScale;
        originalRot = parentTransform.localRotation;
    }

    public void LateUpdate()
    {
        // CustomSortable에게 현재 공중에 떠 있는 높이(arc + 지붕높이)를 전달하여 정렬 보정
        customSortable.SetHeight(currentHeight);
        customSortable.ManualLateUpdate();
    }

    public IEnumerator JumpSequence(Vector3 _targetPos, float _jumpHeight, float _duration, float _springFreq, float _springDamping)
    {
        Vector3 startPos = parentTransform.position;
        Vector3 initialScale = parentTransform.localScale;

        // 1. 포물선 점프 단계
        float jumpElapsed = 0f;
        while (jumpElapsed < _duration)
        {
            float t = jumpElapsed / _duration;

            // 수평 및 수직(포물선) 이동을 Transform이 직접 수행
            // Lerp를 통해 시작 지점(바닥)에서 목표 지점(지붕)으로 이동
            Vector3 groundLerpPos = Vector3.Lerp(startPos, _targetPos, t);
            float arc = Mathf.Sin(t * Mathf.PI) * _jumpHeight;
            parentTransform.position = groundLerpPos + new Vector3(0, arc, 0);

            // CustomSortable을 위한 Height 계산: 
            // 현재 지면으로부터 떠 있는 총 높이 = (지붕으로 올라가는 높이) + (점프 곡선 높이)
            float ascendingHeight = t * roofHeight;
            currentHeight = ascendingHeight + arc;

            // 공중에서의 쫀득한 스케일
            float stretch = Mathf.Sin(t * Mathf.PI) * 0.2f;
            parentTransform.localScale = initialScale + new Vector3(-stretch, stretch, 0);

            jumpElapsed += Time.deltaTime;
            yield return null;
        }

        // 2. 안착 단계 (연출 제거)
        parentTransform.position = _targetPos;
        currentHeight = roofHeight;
        parentTransform.localScale = initialScale;
    }

    private Coroutine openCoroutine;

    public void Open()
    {
        if (bActive == false || anim.GetBool(bOpenHash)) return;

        parentTransform.DOKill();

        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
        }

        openCoroutine = StartCoroutine(OpenSequence());
    }

    private IEnumerator OpenSequence()
    {
        Vector3 startScale = parentTransform.localScale;
        Vector3 squashedScale = new Vector3(originalScale.x * 1.3f, originalScale.y * 0.5f, originalScale.z);
        Vector3 stretchedScale = new Vector3(originalScale.x * 0.8f, originalScale.y * 1.25f, originalScale.z);
        Vector3 bounceScale = new Vector3(originalScale.x * 1.1f, originalScale.y * 0.9f, originalScale.z);

        // 1. 납작해짐 (Anticipation - 0.15초)
        float elapsed = 0f;
        float duration = 0.15f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransform.localScale = Vector3.LerpUnclamped(startScale, squashedScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransform.localScale = squashedScale;

        // 2. Animator 트리거 + 뒤뚱거림
        anim.SetBool(bOpenHash, true);
        parentTransform.DOPunchRotation(new Vector3(0f, 0f, 15f), 0.2f, 8, 1f);

        // 3. 위로 뽀잉 솟구침 (0.15초)
        elapsed = 0f;
        duration = 0.15f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransform.localScale = Vector3.LerpUnclamped(squashedScale, stretchedScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransform.localScale = stretchedScale;

        // 4. 아래로 살짝 찌그러짐 (0.12초)
        elapsed = 0f;
        duration = 0.08f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * t * (3f - 2f * t); // SmoothStep
            parentTransform.localScale = Vector3.LerpUnclamped(stretchedScale, bounceScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransform.localScale = bounceScale;

        ContainerOpenedEvent?.Invoke();

        // 5. 원래 크기로 복귀 (0.15초)
        elapsed = 0f;
        duration = 0.11f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransform.localScale = Vector3.LerpUnclamped(bounceScale, originalScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }

        parentTransform.localScale = originalScale;
        parentTransform.localRotation = originalRot;
        openCoroutine = null;
    }

    private Coroutine closeCoroutine;

    public void Close()
    {
        if (!anim.GetBool(bOpenHash)) return;

        parentTransform.DOKill();

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
        Vector3 startScale = parentTransform.localScale;
        Vector3 stretchedScale = new Vector3(originalScale.x * 0.8f, originalScale.y * 1.2f, originalScale.z);
        Vector3 squashedScale = new Vector3(originalScale.x * 1.35f, originalScale.y * 0.55f, originalScale.z);
        Vector3 bounceScale = new Vector3(originalScale.x * 0.93f, originalScale.y * 1.07f, originalScale.z);

        // 1. 살짝 위로 늘어남 (준비 동작 - 0.12초)
        anim.SetBool(bOpenHash, false);
        float elapsed = 0f;
        float duration = 0.12f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransform.localScale = Vector3.LerpUnclamped(startScale, stretchedScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransform.localScale = stretchedScale;

        // 2. 쾅! 닫히며 강하게 찌그러짐 + 뒤뚱거림 (0.05초)
        parentTransform.DOPunchRotation(new Vector3(0f, 0f, 20f), 0.5f, 10, 1f);

        ContainerClosedEvent?.Invoke();
        
        elapsed = 0f;
        duration = 0.05f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * t * t; // InCubic (급격하게 쿵!)
            parentTransform.localScale = Vector3.LerpUnclamped(stretchedScale, squashedScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransform.localScale = squashedScale;

        // 3. 반동으로 튕겨올라감 (0.07초)
        elapsed = 0f;
        duration = 0.07f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransform.localScale = Vector3.LerpUnclamped(squashedScale, bounceScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }
        parentTransform.localScale = bounceScale;

        // 4. 원래 크기로 안착 (0.08초)
        elapsed = 0f;
        duration = 0.08f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * t * (3f - 2f * t); // SmoothStep
            parentTransform.localScale = Vector3.LerpUnclamped(bounceScale, originalScale, ease);
            elapsed += Time.deltaTime;
            yield return null;
        }

        parentTransform.localScale = originalScale;
        parentTransform.localRotation = originalRot;
        closeCoroutine = null;
    }
}
