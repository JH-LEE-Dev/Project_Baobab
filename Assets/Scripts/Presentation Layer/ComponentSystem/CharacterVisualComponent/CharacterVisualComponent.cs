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

    private CharacterAnimator characterAnimator;

    // Character.cs 컴파일 호환성 유지용 (혹시 외부에서 사용되는 경우 대비)
    public Animator Anim => null;

    #region Public Methods (Initialization & Control)

    public void Initialize(IEnvironmentProvider _environmentProvider, GameObject _onWaterAnimatorObject, Shadow _shadowObject,
        CustomSortable _customSortable)
    {
        
        vfxComponent = GetComponent<VFXComponent>();
        characterAnimator = GetComponent<CharacterAnimator>();

        vfxComponent.Initialize();

        if (characterAnimator != null)
        {
            characterAnimator.Initialize(vfxComponent);
        }
        
        environmentProvider = _environmentProvider;
        shadowObject = _shadowObject;
        customSortable = _customSortable;
        defaultSortingLayerId = SortingLayer.NameToID("Default");

        sr = GetComponent<SpriteRenderer>();

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
}
