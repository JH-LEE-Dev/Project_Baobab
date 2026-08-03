using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class CharacterVisualComponent : MonoBehaviour
{
    // 외부 의존성
    private IEnvironmentProvider environmentProvider;

    // 내부 의존성 (컴포넌트 및 오브젝트)
    private SpriteRenderer sr;
    private SpriteRenderer onWaterSR;
    private SpriteRenderer shadowSR;
    private Shadow shadowObject;
    private VFXComponent vfxComponent;

    [SerializeField] private GameObject faceObject;
    [SerializeField] private GameObject faceObjectBlink;
    [SerializeField] private GameObject onWaterFaceObject;
    [SerializeField] private GameObject onWaterFaceObjectBlink;

    // 내부 의존성 (Face)
    private SpriteRenderer faceSR;
    private SpriteRenderer faceBlinkSR;
    private SpriteRenderer onWaterFaceSR;
    private SpriteRenderer onWaterFaceBlinkSR;

    // 상태 및 데이터
    private bool bIsUnderShadow = false;
    private float shadowLerp = 0f;
    private float currentFadeDuration = 0.3f;
    private Color normalColor = Color.white;
    private Color shadowTint = new Color(0.6f, 0.6f, 0.7f, 1f);
    private float currentFacingAngle = 0f;
    private bool bInHub = true;

    // Sorting Layer 관련 데이터
    private int originalFaceSortingLayer;
    private int originalFaceBlinkSortingLayer;
    private int originalOnWaterFaceSortingLayer;
    private int originalOnWaterFaceBlinkSortingLayer;
    private int defaultSortingLayerId;

    // 눈 깜빡임 타이머 데이터
    private float blinkTimer = 0f;
    private float nextBlinkInterval = 3f;
    private const float blinkDuration = 0.15f;
    private bool isBlinking = false;
    private int currentBlinkCountInSequence = 1;
    private const float blinkGapDuration = 0.05f;

    private CustomSortable customSortable;

    [SerializeField] private GameObject characterVisualComponent;

    // Base Visuals(몸통/팔 등)와 OnWater Visuals(수면 반사)를 함께 감싸는 최상위 "Visuals" 루트.
    // 아이템 획득 뽀잉 연출은 이 오브젝트 전체를 스케일링해서 적용한다(Character.characterVisualObjects와 동일한 대상).
    [SerializeField] private GameObject visualsRoot;
    private Vector3 visualsRootOriginalScale = Vector3.one;
    private float itemAcquireBounceTime = 1f;
    private const float ITEM_ACQUIRE_BOUNCE_DURATION = 0.2f;

    private CharacterAnimator characterAnimator;
    private bool bIsInitialized = false;

    // 사망 시 하얀 플래시 연출 (TreeVisualComponent.PlayHitFlash와 동일한 방식: _FlashAmount를
    // MaterialPropertyBlock으로 조작해 SRP 배칭을 깨지 않는다)
    [SerializeField] private float deathFlashDuration = 0.3f;
    [SerializeField] private AnimationCurve deathFlashCurve = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);
    private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
    private MaterialPropertyBlock flashMPB;
    private Coroutine deathFlashCoroutine;

    // Character.cs 컴파일 호환성 유지용 (혹시 외부에서 사용되는 경우 대비)
    public Animator Anim => null;

    #region Public Methods (Initialization & Control)

    public void Initialize(IEnvironmentProvider _environmentProvider, GameObject _onWaterAnimatorObject, Shadow _shadowObject,
        CustomSortable _customSortable)
    {
        vfxComponent = GetComponent<VFXComponent>();
        characterAnimator = GetComponent<CharacterAnimator>();

        vfxComponent.Initialize();

        environmentProvider = _environmentProvider;

        if (characterAnimator != null)
        {
            characterAnimator.Initialize(vfxComponent, environmentProvider?.tilemapDataProvider);
        }

        shadowObject = _shadowObject;
        customSortable = _customSortable;
        defaultSortingLayerId = SortingLayer.NameToID("Default");

        sr = GetComponent<SpriteRenderer>();

        if (!bIsInitialized)
        {
            if (faceObject != null)
            {
                faceSR = faceObject.GetComponent<SpriteRenderer>();
                if (faceSR != null)
                {
                    originalFaceSortingLayer = faceSR.sortingLayerID;
                }
            }

            if (faceObjectBlink != null)
            {
                faceBlinkSR = faceObjectBlink.GetComponent<SpriteRenderer>();
                if (faceBlinkSR != null)
                {
                    originalFaceBlinkSortingLayer = faceBlinkSR.sortingLayerID;
                }
            }

            if (onWaterFaceObject != null)
            {
                onWaterFaceSR = onWaterFaceObject.GetComponent<SpriteRenderer>();
                if (onWaterFaceSR != null)
                {
                    onWaterFaceSR.material.SetFloat("_DistortionAmount", 0.5f);
                    originalOnWaterFaceSortingLayer = onWaterFaceSR.sortingLayerID;
                }
            }

            if (onWaterFaceObjectBlink != null)
            {
                onWaterFaceBlinkSR = onWaterFaceObjectBlink.GetComponent<SpriteRenderer>();
                if (onWaterFaceBlinkSR != null)
                {
                    onWaterFaceBlinkSR.material.SetFloat("_DistortionAmount", 0.5f);
                    originalOnWaterFaceBlinkSortingLayer = onWaterFaceBlinkSR.sortingLayerID;
                }
            }
            bIsInitialized = true;
        }

        if (_onWaterAnimatorObject != null)
        {
            onWaterSR = _onWaterAnimatorObject.GetComponent<SpriteRenderer>();
            if (onWaterSR != null)
            {
                onWaterSR.material.SetFloat("_DistortionAmount", 0.5f);
            }
        }

        if (shadowObject != null)
        {
            shadowSR = shadowObject.GetComponent<SpriteRenderer>();
            shadowObject.Initialize();
        }

        if (customSortable != null)
        {
            customSortable.SetSortingGroup(characterVisualComponent.GetComponent<SortingGroup>());
        }

        if (visualsRoot != null)
        {
            // 오브젝트 풀 재사용 시(럼버잭/포터 NPC는 던전 재입장마다 Initialize()가 다시 호출됨) 이전
            // 생애에 뽀잉 연출이 채 끝나기 전에 비활성화됐다면 스케일이 일그러진 채로 멈춰있을 수 있다.
            // 그 상태를 그대로 "원래 스케일"로 새로 캐싱해버리면 왜곡이 영구적으로 고착되므로, 재확인 전에
            // 먼저 이전에 캐싱해둔 원래 스케일로 강제 복구한다(최초 1회 호출 시에는 진행 중인 연출이 없어 아무 효과 없음).
            if (itemAcquireBounceTime < ITEM_ACQUIRE_BOUNCE_DURATION)
            {
                visualsRoot.transform.localScale = visualsRootOriginalScale;
            }
            itemAcquireBounceTime = 1f;
            visualsRootOriginalScale = visualsRoot.transform.localScale;
        }
    }

    public void UpdateVisuals(bool _isMoving, bool _bInHub, bool _isDead = false)
    {
        bInHub = _bInHub;

        UpdateCharacterColor();
        
        if (!_isDead)
        {
            UpdateBlink();
        }

        // 방향 정보 추출을 위해 정밀 계산
        float shadowAngle = 0f;
        if (environmentProvider != null && environmentProvider.shadowDataProvider != null)
        {
            shadowAngle = environmentProvider.shadowDataProvider.CurrentShadowAngle;
        }

        // Animator를 쓰지 않고 CharacterAnimator로 애니메이션 업데이트 수행
        if (characterAnimator != null)
        {
            characterAnimator.UpdateAnimation(
                Time.deltaTime,
                _isMoving,
                _bInHub,
                currentFacingAngle,
                shadowAngle,
                isBlinking,
                _isDead
            );
        }

        // 수면 반사/얼굴 정렬 제어
        int dirIndex = Mathf.RoundToInt(currentFacingAngle / 45f) % 8;
        bool isFaceActive = !_isDead && (dirIndex == 0 || dirIndex == 4 || dirIndex == 5 || dirIndex == 6 || dirIndex == 7);
        if (faceSR != null)
        {
            faceSR.sortingLayerID = isFaceActive ? originalFaceSortingLayer : defaultSortingLayerId;
            if (!isFaceActive)
            {
                faceSR.enabled = false;
            }
        }
        if (faceBlinkSR != null)
        {
            faceBlinkSR.sortingLayerID = isFaceActive ? originalFaceBlinkSortingLayer : defaultSortingLayerId;
            if (!isFaceActive)
            {
                faceBlinkSR.enabled = false;
            }
        }
        if (onWaterFaceSR != null)
        {
            onWaterFaceSR.sortingLayerID = isFaceActive ? originalOnWaterFaceSortingLayer : defaultSortingLayerId;
            if (!isFaceActive)
            {
                onWaterFaceSR.enabled = false;
            }
        }
        if (onWaterFaceBlinkSR != null)
        {
            onWaterFaceBlinkSR.sortingLayerID = isFaceActive ? originalOnWaterFaceBlinkSortingLayer : defaultSortingLayerId;
            if (!isFaceActive)
            {
                onWaterFaceBlinkSR.enabled = false;
            }
        }

        if (shadowObject != null)
        {
            shadowObject.ManualUpdate(
                environmentProvider.shadowDataProvider.CurrentShadowAngle,
                environmentProvider.shadowDataProvider.CurrentShadowScaleY,
                environmentProvider.shadowDataProvider.IsShadowActive && !_isDead);
        }
    }

    public void SetFacingDirection(Vector2 _input)
    {
        if (_input.sqrMagnitude < 0.01f) return;

        float angle = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        currentFacingAngle = angle;
    }

    public void SetInShadow(bool _isInShadow, float _duration)
    {
        bIsUnderShadow = _isInShadow;
        currentFadeDuration = _duration;
    }

    public void SetOnWaterSROrder(Vector2 _position)
    {
        int order = (int)(_position.y * 100);
        if (onWaterSR != null) onWaterSR.sortingOrder = order;
        if (onWaterFaceSR != null) onWaterFaceSR.sortingOrder = order;
        if (onWaterFaceBlinkSR != null) onWaterFaceBlinkSR.sortingOrder = order;
    }

    public void SetHubState(bool _bInHub)
    {
        bInHub = _bInHub;
    }

    public void SetTilemapDataProvider(ITilemapDataProvider _tilemapDataProvider)
    {
        characterAnimator?.SetTilemapDataProvider(_tilemapDataProvider);
    }

    // OffroadContainer/캐릭터 인벤토리에 아이템이 도착했을 때(Character.PlayItemAcquireBounce와 동일한
    // 감쇠 진동 곡선)의 뽀잉 연출. LumberjackNPC/OffroadPorterNPC 등 CharacterVisualComponent를 쓰는
    // 모든 유닛이 공용으로 사용한다.
    public void PlayItemAcquireBounce()
    {
        itemAcquireBounceTime = 0f;
    }

    #endregion

    #region Private Methods

    private void UpdateCharacterColor()
    {
        float target = bIsUnderShadow ? 1f : 0f;
        float speed = currentFadeDuration > 0 ? 1.0f / currentFadeDuration : 100f;
        shadowLerp = Mathf.MoveTowards(shadowLerp, target, Time.deltaTime * speed);
        Color finalColor = Color.Lerp(normalColor, shadowTint, shadowLerp);
        
        if (sr != null) sr.color = finalColor;
        if (onWaterSR != null) onWaterSR.color = finalColor;
        if (faceSR != null) faceSR.color = finalColor;
        if (faceBlinkSR != null) faceBlinkSR.color = finalColor;
        if (onWaterFaceSR != null) onWaterFaceSR.color = finalColor;
        if (onWaterFaceBlinkSR != null) onWaterFaceBlinkSR.color = finalColor;
    }

    private void UpdateBlink()
    {
        blinkTimer += Time.deltaTime;

        if (blinkTimer >= nextBlinkInterval)
        {
            float sequenceTime = blinkTimer - nextBlinkInterval;

            // 1회차 깜빡임 구간
            if (sequenceTime < blinkDuration)
            {
                SetBlinkState(true);
            }
            // 깜빡임 사이의 아주 짧은 눈 뜨는 간격 (2회 깜빡임 설정 시)
            else if (currentBlinkCountInSequence == 2 && sequenceTime < blinkDuration + blinkGapDuration)
            {
                SetBlinkState(false);
            }
            // 2회차 깜빡임 구간 (2회 깜빡임 설정 시)
            else if (currentBlinkCountInSequence == 2 && sequenceTime < (blinkDuration * 2) + blinkGapDuration)
            {
                SetBlinkState(true);
            }
            // 전체 깜빡임 시퀀스 종료 시점
            else
            {
                SetBlinkState(false);
                blinkTimer = 0f;
                nextBlinkInterval = Random.Range(2f, 5f);
                currentBlinkCountInSequence = Random.Range(1, 3); // 다음 주기의 깜빡임 횟수(1~2회) 결정
            }
        }
    }

    private void SetBlinkState(bool _isBlinking)
    {
        isBlinking = _isBlinking;
    }

    // 아이템 획득 뽀잉 연출 전용 업데이트. UpdateVisuals()(외부에서 수동 호출되는 공용 로직)와는
    // 완전히 분리된, 이 컴포넌트 자체의 MonoBehaviour Update이다 - 기존 로직에 영향을 주지 않는다.
    private void Update()
    {
        UpdateItemAcquireBounce(Time.deltaTime);
    }

    private void UpdateItemAcquireBounce(float _deltaTime)
    {
        if (visualsRoot == null) return;

        if (itemAcquireBounceTime >= ITEM_ACQUIRE_BOUNCE_DURATION)
        {
            if (visualsRoot.transform.localScale != visualsRootOriginalScale)
                visualsRoot.transform.localScale = visualsRootOriginalScale;
            return;
        }

        itemAcquireBounceTime += _deltaTime;
        float t = itemAcquireBounceTime / ITEM_ACQUIRE_BOUNCE_DURATION;

        // 쫀득함(Squash & Stretch) 연출: 감쇠 진동 곡선(Damped Sine Wave). OffroadContainer.UpdateBounce와 동일한 방식.
        float curve = Mathf.Sin(t * Mathf.PI * 3f) * (1f - t) * 0.3f;

        visualsRoot.transform.localScale = new Vector3(
            visualsRootOriginalScale.x * (1f + curve),
            visualsRootOriginalScale.y * (1f - curve),
            visualsRootOriginalScale.z);
    }

    #endregion

    public void CharacterIsDead(bool _boolean)
    {
        if (onWaterSR != null) onWaterSR.enabled = !_boolean;
        if (onWaterFaceSR != null) onWaterFaceSR.enabled = !_boolean;
        if (onWaterFaceBlinkSR != null) onWaterFaceBlinkSR.enabled = !_boolean;
        if (shadowSR != null) shadowSR.enabled = !_boolean;
        if (faceSR != null) faceSR.enabled = !_boolean;
        if (faceBlinkSR != null) faceBlinkSR.enabled = !_boolean;
    }

    public void PlayDeathFlash()
    {
        if (!gameObject.activeInHierarchy) return;

        if (deathFlashCoroutine != null)
        {
            StopCoroutine(deathFlashCoroutine);
        }
        deathFlashCoroutine = StartCoroutine(DeathFlashRoutine());
    }

    private IEnumerator DeathFlashRoutine()
    {
        if (flashMPB == null) flashMPB = new MaterialPropertyBlock();

        float elapsed = 0f;
        while (elapsed < deathFlashDuration)
        {
            float t = elapsed / deathFlashDuration;
            SetFlashAmount(deathFlashCurve.Evaluate(t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetFlashAmount(0f);
        deathFlashCoroutine = null;
    }

    private void SetFlashAmount(float _flash)
    {
        if (sr == null) return;

        if (flashMPB == null) flashMPB = new MaterialPropertyBlock();
        sr.GetPropertyBlock(flashMPB);
        flashMPB.SetFloat(FlashAmountID, _flash);
        sr.SetPropertyBlock(flashMPB);
    }
}
