using System.Collections;
using UnityEngine;
using DG.Tweening;
using System;
using PresentationLayer.VFX;
using PresentationLayer.Environment;

public class OffroadContainerVComponent : MonoBehaviour
{
    public event Action ContainerOpenedEvent;
    public event Action ContainerClosedEvent;
    public event Action<Vector3> ContainerJumpStartEvent;
    public event Action<Vector3> ContainerLandedEvent;

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

    public CustomSortable Sortable => customSortable;

    [SerializeField] private GameObject outlineStencilObj;
    [SerializeField] private GameObject outlineObj;
    [SerializeField] private GameObject containerForJump;
    [SerializeField] private GameObject containerForOpen;
    [SerializeField] private GameObject carriedContainer;
    private Material originalMaterial;

    private SpriteRenderer outlineStencilSR;
    private SpriteRenderer outlineSR;
    private Sprite currentSprite;

    // 엔딩 특성(소풍 바구니)을 찍으면 상자 대신 바구니로 보인다. 기능은 상자와 같다.
    // 특성 레벨은 세이브에 남아 이어하기 때 다시 적용되고, 새로하기면 특성이 비어 상자로 시작한다.
    // 비어 있는 슬롯은 상자 모습을 그대로 쓴다.
    [Header("Picnic Basket")]
    [SerializeField] private RuntimeAnimatorController picnicBasketAnimator; // 열고 닫는 애니메이션 (bOpen 파라미터 동일)
    [SerializeField] private Sprite picnicBasketJumpSprite;    // 차로 뛰어오를 때
    [SerializeField] private Sprite picnicBasketOpenSprite;    // 열림 연출용 오브젝트
    [SerializeField] private Sprite picnicBasketCarriedSprite; // 차 위에 실렸을 때
    private RuntimeAnimatorController originalAnimator;
    private SpriteRenderer jumpSR;
    private SpriteRenderer openSR;
    private SpriteRenderer carriedSR;
    private Sprite originalJumpSprite;
    private Sprite originalOpenSprite;
    private Sprite originalCarriedSprite;
    private bool bPicnicBasket;

    // 특성 창에서 소풍 바구니를 찍고 창을 닫으면, 전리품 기둥 진화와 같은 연출(VFX_LootEvolve)을 상자에서 재생하고
    // 폭발 시각에 바구니로 바꾼다. 번쩍임·뽀잉 값은 마을 전리품 기둥(LootPillar 프리팹)의 성장 연출 값과 같다.
    [Header("Picnic Basket Reveal")]
    [SerializeField] private VFX_LootEvolve picnicBasketRevealEffect;
    [SerializeField] private int revealFromTier = 2;
    [SerializeField] private int revealToTier = 3;
    [SerializeField] private float revealFlashHold = 0.05f;
    [SerializeField] private float revealFlashDuration = 0.3f;
    [SerializeField] private float revealPopDuration = 0.6f;
    [SerializeField] private float revealPopAmplitude = 0.2f;
    [SerializeField] private float revealPopWidthRatio = 0.6f;
    [SerializeField] private float revealPopFrequency = 3.5f;
    [SerializeField] private float revealPopDamping = 6f;
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private MaterialPropertyBlock revealFlashMPB;
    private Action cachedOnRevealBurst;
    // 연출이 재생 중이고 폭발 시각에 바구니로 바꿔야 하는지. 그 사이 특성이 되돌려지면(SetPicnicBasket(false)) 꺼서 바꾸지 않는다.
    private bool bRevealPending;
    private Coroutine revealPopCoroutine;
    private bool bRevealPopPlaying;
    private Vector3 revealPopBaseLocalPosition;
    private Vector3 revealPopBaseLocalScale;

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

        originalAnimator = anim != null ? anim.runtimeAnimatorController : null;
        jumpSR = containerForJump != null ? containerForJump.GetComponentInChildren<SpriteRenderer>(true) : null;
        openSR = containerForOpen != null ? containerForOpen.GetComponentInChildren<SpriteRenderer>(true) : null;
        carriedSR = carriedContainer != null ? carriedContainer.GetComponentInChildren<SpriteRenderer>(true) : null;
        if (jumpSR != null) originalJumpSprite = jumpSR.sprite;
        if (openSR != null) originalOpenSprite = openSR.sprite;
        if (carriedSR != null) originalCarriedSprite = carriedSR.sprite;
    }

    /// <summary>
    /// 소풍 바구니 모습으로 바꾸거나 상자로 되돌린다. 비어 있는 슬롯은 상자 모습을 그대로 쓴다.
    /// </summary>
    public void SetPicnicBasket(bool _bPicnicBasket)
    {
        bRevealPending = false;
        if (bPicnicBasket == _bPicnicBasket) return;
        bPicnicBasket = _bPicnicBasket;

        if (anim != null)
        {
            RuntimeAnimatorController target = bPicnicBasket && picnicBasketAnimator != null ? picnicBasketAnimator : originalAnimator;
            if (target != null && anim.runtimeAnimatorController != target)
            {
                bool bWasOpen = anim.GetBool(bOpenHash);
                anim.runtimeAnimatorController = target;
                anim.SetBool(bOpenHash, bWasOpen);
            }
        }

        if (jumpSR != null) jumpSR.sprite = bPicnicBasket && picnicBasketJumpSprite != null ? picnicBasketJumpSprite : originalJumpSprite;
        if (openSR != null) openSR.sprite = bPicnicBasket && picnicBasketOpenSprite != null ? picnicBasketOpenSprite : originalOpenSprite;
        if (carriedSR != null) carriedSR.sprite = bPicnicBasket && picnicBasketCarriedSprite != null ? picnicBasketCarriedSprite : originalCarriedSprite;
    }

    /// <summary>
    /// 전리품 진화 연출을 재생하고 폭발 시각에 바구니로 바꾼다. 특성 창을 닫았을 때 TownObjectManager가 부른다.
    /// 연출을 재생할 수 없거나(꺼져 있음/연결 안 됨) 이미 바구니면 바로 바꾼다.
    /// </summary>
    public void PlayPicnicBasketReveal()
    {
        if (true == bPicnicBasket) return;

        // 연출 컴포넌트는 쉬는 동안 꺼져 있으므로(재생할 때만 켜짐) 오브젝트가 켜져 있는지로 본다.
        // 상자가 차 위에 실려 바닥 그림이 꺼져 있으면 연출 자리가 맞지 않으니 바로 바꾼다.
        if (null == picnicBasketRevealEffect || false == isActiveAndEnabled
            || false == picnicBasketRevealEffect.gameObject.activeInHierarchy || false == spriteRenderer.enabled)
        {
            SetPicnicBasket(true);
            return;
        }

        if (null == cachedOnRevealBurst) cachedOnRevealBurst = OnPicnicBasketRevealBurst;

        StopRevealPop();
        picnicBasketRevealEffect.Play(revealFromTier, revealToTier, cachedOnRevealBurst);
        bRevealPending = true;

        LootDisplayObject.PlayEvolveStartSound(transform.position);
    }

    // 진화 연출의 폭발 시각: 바구니로 바꾸고 기둥처럼 하얗게 번쩍이며 뽀잉 튄다.
    private void OnPicnicBasketRevealBurst()
    {
        if (false == bRevealPending) return;

        SetPicnicBasket(true);

        // 연출이 끝까지 진행돼 폭발 시각에 닿았을 때만 소리와 뽀잉을 낸다(상자가 꺼지며 정리될 때는 모습만 바꾼다).
        if (false == isActiveAndEnabled) return;

        LootDisplayObject.PlayEvolveBurstSound(transform.position);
        StopRevealPop();
        revealPopCoroutine = StartCoroutine(RevealPopSequence());
    }

    // LootDisplayObject의 성장 뽀잉과 같은 곡선. 상자 그림 바닥을 기준으로 늘이고 줄인다.
    private IEnumerator RevealPopSequence()
    {
        revealPopBaseLocalPosition = transform.localPosition;
        revealPopBaseLocalScale = transform.localScale;
        bRevealPopPlaying = true;

        float _bottom = null != spriteRenderer.sprite ? spriteRenderer.sprite.bounds.min.y : 0f;
        Vector3 _anchor = revealPopBaseLocalPosition + Vector3.Scale(new Vector3(0f, _bottom, 0f), revealPopBaseLocalScale);
        _anchor.z = revealPopBaseLocalPosition.z;

        float _t = 0f;
        float _total = Mathf.Max(revealPopDuration, revealFlashDuration);
        while (_t < _total)
        {
            float _flash = _t <= revealFlashHold ? 1f : 1f - Mathf.Clamp01((_t - revealFlashHold) / Mathf.Max(0.0001f, revealFlashDuration - revealFlashHold));
            SetRevealFlash(_flash * _flash);

            float _a = 0f;
            if (_t < revealPopDuration)
            {
                float _remain = 1f - _t / revealPopDuration;
                _a = -revealPopAmplitude * Mathf.Exp(-revealPopDamping * _t) * Mathf.Cos(2f * Mathf.PI * revealPopFrequency * _t) * _remain;
            }
            Vector3 _scale = new Vector3(1f - _a * revealPopWidthRatio, 1f + _a, 1f);

            transform.localScale = Vector3.Scale(revealPopBaseLocalScale, _scale);
            transform.localPosition = _anchor + Vector3.Scale(revealPopBaseLocalPosition - _anchor, _scale);

            yield return null;
            _t += Time.deltaTime;
        }

        revealPopCoroutine = null;
        StopRevealPop();
    }

    // 뽀잉·번쩍임을 멈추고 원래 자리/크기/색으로 돌린다.
    private void StopRevealPop()
    {
        if (null != revealPopCoroutine)
        {
            StopCoroutine(revealPopCoroutine);
            revealPopCoroutine = null;
        }

        if (false == bRevealPopPlaying) return;
        bRevealPopPlaying = false;

        transform.localPosition = revealPopBaseLocalPosition;
        transform.localScale = revealPopBaseLocalScale;
        SetRevealFlash(0f);
    }

    private void SetRevealFlash(float _amount)
    {
        if (null == revealFlashMPB) revealFlashMPB = new MaterialPropertyBlock();

        spriteRenderer.GetPropertyBlock(revealFlashMPB);
        revealFlashMPB.SetFloat(FlashAmountID, _amount);
        spriteRenderer.SetPropertyBlock(revealFlashMPB);
    }

    private void OnDisable()
    {
        StopRevealPop();
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
        ContainerJumpStartEvent?.Invoke(parentTransform.position);
        Sound.Play(SoundID.BoxJumping, parentTransform.position);
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
        ContainerLandedEvent?.Invoke(_targetPos);
        Sound.Play(SoundID.BoxLanding, _targetPos);
        Sound.Play(SoundID.BoxLandingEx, _targetPos);
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
        bool bBoxOpenSoundPlayed = false;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            float ease = t * (2f - t); // OutQuad
            parentTransformForOpen.localScale = Vector3.LerpUnclamped(startScale, squashedScale, ease);

            // 1번(납작해짐)에서 2번(Animator 트리거) 시퀀스로 넘어가는 정확히 중간 지점에 재생
            if (!bBoxOpenSoundPlayed && elapsed >= duration * 0.5f)
            {
                Sound.Play(SoundID.BoxOpen, transform.position);
                bBoxOpenSoundPlayed = true;
            }

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

        Sound.Play(SoundID.BoxClose, transform.position);
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
        StopRevealPop();
        spriteRenderer.enabled = true;
        containerForJump.SetActive(false);
        carriedContainer.SetActive(false);
        
        bActive = true;
        if (anim != null)
        {
            anim.SetBool(bOpenHash, false);
            anim.Rebind();
            anim.Update(0f);
        }

        if (parentTransformForOpen != null)
        {
            parentTransformForOpen.DOKill();
            parentTransformForOpen.localScale = originalOpenScale;
            parentTransformForOpen.localRotation = originalOpenRot;
        }

        if (openCoroutine != null)
        {
            StopCoroutine(openCoroutine);
            openCoroutine = null;
        }
        if (closeCoroutine != null)
        {
            StopCoroutine(closeCoroutine);
            closeCoroutine = null;
        }
    }
}
