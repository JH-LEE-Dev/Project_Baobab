using UnityEngine;
using UnityEngine.Rendering;

public class CharacterVisualComponent : MonoBehaviour
{
    // 외부 의존성
    private IEnvironmentProvider environmentProvider;

    // 내부 의존성 (컴포넌트 및 오브젝트)
    private Animator anim;
    private SpriteRenderer sr;
    private Animator onWaterAnim;
    private SpriteRenderer onWaterSR;
    private Animator shadowAnim;
    private SpriteRenderer shadowSR;
    private Shadow shadowObject;

    [SerializeField] private GameObject faceObject;
    [SerializeField] private GameObject faceObjectBlink;
    [SerializeField] private GameObject onWaterFaceObject;
    [SerializeField] private GameObject onWaterFaceObjectBlink;

    // 내부 의존성 (Face)
    private Animator faceAnim;
    private SpriteRenderer faceSR;
    private Animator faceBlinkAnim;
    private SpriteRenderer faceBlinkSR;

    private Animator onWaterFaceAnim;
    private SpriteRenderer onWaterFaceSR;
    private Animator onWaterFaceBlinkAnim;
    private SpriteRenderer onWaterFaceBlinkSR;

    // 상태 및 데이터
    private bool bIsUnderShadow = false;
    private float shadowLerp = 0f;
    private float currentFadeDuration = 0.3f;
    private Color normalColor = Color.white;
    private Color shadowTint = new Color(0.6f, 0.6f, 0.7f, 1f);
    private float currentFacingAngle = 0f;

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

    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    private readonly int isMovingHash = Animator.StringToHash("IsMoving");
    private readonly int bInHubHash = Animator.StringToHash("bInHub");

    public Animator Anim => anim;

    private CustomSortable customSortable;

    public GameObject characterVisualComponent;

    #region Public Methods (Initialization & Control)

    public void Initialize(IEnvironmentProvider _environmentProvider, GameObject _onWaterAnimatorObject, Shadow _shadowObject,
        CustomSortable _customSortable)
    {
        environmentProvider = _environmentProvider;
        shadowObject = _shadowObject;
        customSortable = _customSortable;
        defaultSortingLayerId = SortingLayer.NameToID("Default");

        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();

        if (faceObject != null)
        {
            faceAnim = faceObject.GetComponent<Animator>();
            faceSR = faceObject.GetComponent<SpriteRenderer>();
            if (faceSR != null)
            {
                originalFaceSortingLayer = faceSR.sortingLayerID;
            }
        }

        if (faceObjectBlink != null)
        {
            faceBlinkAnim = faceObjectBlink.GetComponent<Animator>();
            faceBlinkSR = faceObjectBlink.GetComponent<SpriteRenderer>();
            if (faceBlinkSR != null)
            {
                faceBlinkSR.enabled = false;
                originalFaceBlinkSortingLayer = faceBlinkSR.sortingLayerID;
            }
        }

        if (onWaterFaceObject != null)
        {
            onWaterFaceAnim = onWaterFaceObject.GetComponent<Animator>();
            onWaterFaceSR = onWaterFaceObject.GetComponent<SpriteRenderer>();

            if (onWaterFaceSR != null)
            {
                onWaterFaceSR.material.SetFloat("_DistortionAmount", 0.5f);
                originalOnWaterFaceSortingLayer = onWaterFaceSR.sortingLayerID;
            }
        }

        if (onWaterFaceObjectBlink != null)
        {
            onWaterFaceBlinkAnim = onWaterFaceObjectBlink.GetComponent<Animator>();
            onWaterFaceBlinkSR = onWaterFaceObjectBlink.GetComponent<SpriteRenderer>();

            if (onWaterFaceBlinkSR != null)
            {
                onWaterFaceBlinkSR.enabled = false;
                onWaterFaceBlinkSR.material.SetFloat("_DistortionAmount", 0.5f);
                originalOnWaterFaceBlinkSortingLayer = onWaterFaceBlinkSR.sortingLayerID;
            }
        }

        if (_onWaterAnimatorObject != null)
        {
            onWaterSR = _onWaterAnimatorObject.GetComponent<SpriteRenderer>();
            onWaterAnim = _onWaterAnimatorObject.GetComponent<Animator>();

            // 수면 위 일렁임 강도 캐릭터에 맞춰 감소 (기본 1.0 -> 0.5)
            if (onWaterSR != null)
            {
                onWaterSR.material.SetFloat("_DistortionAmount", 0.5f);
            }
        }

        if (shadowObject != null)
        {
            shadowSR = shadowObject.GetComponent<SpriteRenderer>();
            shadowAnim = shadowObject.GetComponent<Animator>();
            shadowObject.Initialize();
        }

        if(customSortable != null)
        {
            customSortable.SetSortingGroup(characterVisualComponent.GetComponent<SortingGroup>());
        }

        // 모든 애니메이터의 초기 파라미터 동기화 설정
        SetupInitialAnimatorParameters(anim);
        SetupInitialAnimatorParameters(faceAnim);
        SetupInitialAnimatorParameters(faceBlinkAnim);
        SetupInitialAnimatorParameters(onWaterAnim);
        SetupInitialAnimatorParameters(onWaterFaceAnim);
        SetupInitialAnimatorParameters(onWaterFaceBlinkAnim);
        SetupInitialAnimatorParameters(shadowAnim);
    }

    public void UpdateVisuals(bool _isMoving, bool _bInHub)
    {
        UpdateCharacterColor();
        UpdateFaceVisual(_isMoving, _bInHub);
        UpdateShadowVisual(_isMoving, _bInHub);
        UpdateOnWaterVisual(_isMoving, _bInHub);

        if (shadowObject != null)
        {
            shadowObject.ManualUpdate(
                environmentProvider.shadowDataProvider.CurrentShadowAngle,
                environmentProvider.shadowDataProvider.CurrentShadowScaleY,
                environmentProvider.shadowDataProvider.IsShadowActive);
        }
    }

    public void SetFacingDirection(Vector2 _input)
    {
        if (_input.sqrMagnitude < 0.01f) return;

        float angle = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        currentFacingAngle = angle; // 각도 저장
        SetAnimatorDirection(anim, sr, _input);

        if (faceAnim != null) SetAnimatorDirection(faceAnim, faceSR, _input);
        if (faceBlinkAnim != null) SetAnimatorDirection(faceBlinkAnim, faceBlinkSR, _input);
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
        anim.SetBool(bInHubHash, _bInHub);
        if (faceAnim != null) faceAnim.SetBool(bInHubHash, _bInHub);
        if (faceBlinkAnim != null) faceBlinkAnim.SetBool(bInHubHash, _bInHub);
    }

    #endregion

    #region Private Methods

    private void SetupInitialAnimatorParameters(Animator _targetAnim)
    {
        if (_targetAnim == null) return;

        _targetAnim.SetFloat(facingDirHash, 3f); // 3f: 아래 방향 (Vector2.down)
        _targetAnim.SetBool(isMovingHash, false);
        _targetAnim.SetBool(bInHubHash, true);
    }

    private void UpdateCharacterColor()
    {
        float target = bIsUnderShadow ? 1f : 0f;
        float speed = currentFadeDuration > 0 ? 1.0f / currentFadeDuration : 100f;
        shadowLerp = Mathf.MoveTowards(shadowLerp, target, Time.deltaTime * speed);
        Color finalColor = Color.Lerp(normalColor, shadowTint, shadowLerp);
        sr.color = finalColor;
        if (onWaterSR != null) onWaterSR.color = finalColor;
        if (faceSR != null) faceSR.color = finalColor;
        if (faceBlinkSR != null) faceBlinkSR.color = finalColor;
        if (onWaterFaceSR != null) onWaterFaceSR.color = finalColor;
        if (onWaterFaceBlinkSR != null) onWaterFaceBlinkSR.color = finalColor;
    }

    private void UpdateFaceVisual(bool _isMoving, bool _bInHub)
    {
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);

        // 1. 애니메이터 동기화
        if (faceAnim != null)
        {
            faceAnim.SetFloat(facingDirHash, anim.GetFloat(facingDirHash));
            faceAnim.SetBool(isMovingHash, _isMoving);
            faceAnim.SetBool(bInHubHash, _bInHub);
            AnimatorStateInfo childState = faceAnim.GetCurrentAnimatorStateInfo(0);
            if (childState.fullPathHash != 0)
            {
                if (Mathf.Abs(childState.normalizedTime - stateInfo.normalizedTime) > 0.02f)
                {
                    faceAnim.Play(childState.fullPathHash, 0, stateInfo.normalizedTime);
                }
            }
        }

        if (faceBlinkAnim != null)
        {
            faceBlinkAnim.SetFloat(facingDirHash, anim.GetFloat(facingDirHash));
            faceBlinkAnim.SetBool(isMovingHash, _isMoving);
            faceBlinkAnim.SetBool(bInHubHash, _bInHub);
            AnimatorStateInfo childState = faceBlinkAnim.GetCurrentAnimatorStateInfo(0);
            if (childState.fullPathHash != 0)
            {
                if (Mathf.Abs(childState.normalizedTime - stateInfo.normalizedTime) > 0.02f)
                {
                    faceBlinkAnim.Play(childState.fullPathHash, 0, stateInfo.normalizedTime);
                }
            }
        }

        // 2. 눈 깜빡임 로직 업데이트
        UpdateBlink();

        // 3. 방향에 따른 활성화/비활성화 제어
        int dirIndex = Mathf.RoundToInt(currentFacingAngle / 45f) % 8;
        bool isFaceActive = (dirIndex == 0 || dirIndex == 4 || dirIndex == 5 || dirIndex == 6 || dirIndex == 7);

        // 현재 깜빡임 상태와 방향 가시성을 조합하여 최종 enabled 및 sortingLayerID 결정
        if (faceSR != null)
        {
            faceSR.sortingLayerID = isFaceActive ? originalFaceSortingLayer : defaultSortingLayerId;
            faceSR.enabled = !isBlinking;
        }
        if (faceBlinkSR != null)
        {
            faceBlinkSR.sortingLayerID = isFaceActive ? originalFaceBlinkSortingLayer : defaultSortingLayerId;
            faceBlinkSR.enabled = isBlinking;
        }

        // 수면 반사 얼굴도 동일 로직 적용
        if (onWaterFaceSR != null)
        {
            onWaterFaceSR.sortingLayerID = isFaceActive ? originalOnWaterFaceSortingLayer : defaultSortingLayerId;
            onWaterFaceSR.enabled = !isBlinking;
        }
        if (onWaterFaceBlinkSR != null)
        {
            onWaterFaceBlinkSR.sortingLayerID = isFaceActive ? originalOnWaterFaceBlinkSortingLayer : defaultSortingLayerId;
            onWaterFaceBlinkSR.enabled = isBlinking;
        }
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
        // 실제 enabled 제어는 UpdateFaceVisual에서 통합 관리
    }

    private void UpdateOnWaterVisual(bool _isMoving, bool _bInHub)
    {
        AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);

        if (onWaterAnim != null && onWaterSR != null)
        {
            onWaterAnim.SetFloat(facingDirHash, anim.GetFloat(facingDirHash));
            onWaterAnim.SetBool(isMovingHash, _isMoving);
            onWaterAnim.SetBool(bInHubHash, _bInHub);
            AnimatorStateInfo childState = onWaterAnim.GetCurrentAnimatorStateInfo(0);
            if (childState.fullPathHash != 0)
            {
                if (Mathf.Abs(childState.normalizedTime - stateInfo.normalizedTime) > 0.02f)
                {
                    onWaterAnim.Play(childState.fullPathHash, 0, stateInfo.normalizedTime);
                }
            }

            Vector3 reversedScale = sr.transform.localScale;
            reversedScale.x *= -1f;
            onWaterSR.transform.localScale = reversedScale;
        }

        if (onWaterFaceAnim != null && onWaterFaceSR != null)
        {
            onWaterFaceAnim.SetFloat(facingDirHash, anim.GetFloat(facingDirHash));
            onWaterFaceAnim.SetBool(isMovingHash, _isMoving);
            onWaterFaceAnim.SetBool(bInHubHash, _bInHub);
            AnimatorStateInfo childState = onWaterFaceAnim.GetCurrentAnimatorStateInfo(0);
            if (childState.fullPathHash != 0)
            {
                if (Mathf.Abs(childState.normalizedTime - stateInfo.normalizedTime) > 0.02f)
                {
                    onWaterFaceAnim.Play(childState.fullPathHash, 0, stateInfo.normalizedTime);
                }
            }

            Vector3 faceReversedScale = faceSR != null ? faceSR.transform.localScale : sr.transform.localScale;
            faceReversedScale.x *= -1f;
            onWaterFaceSR.transform.localScale = faceReversedScale;
        }

        if (onWaterFaceBlinkAnim != null && onWaterFaceBlinkSR != null)
        {
            onWaterFaceBlinkAnim.SetFloat(facingDirHash, anim.GetFloat(facingDirHash));
            onWaterFaceBlinkAnim.SetBool(isMovingHash, _isMoving);
            onWaterFaceBlinkAnim.SetBool(bInHubHash, _bInHub);
            AnimatorStateInfo childState = onWaterFaceBlinkAnim.GetCurrentAnimatorStateInfo(0);
            if (childState.fullPathHash != 0)
            {
                if (Mathf.Abs(childState.normalizedTime - stateInfo.normalizedTime) > 0.02f)
                {
                    onWaterFaceBlinkAnim.Play(childState.fullPathHash, 0, stateInfo.normalizedTime);
                }
            }

            Vector3 faceReversedScale = faceBlinkSR != null ? faceBlinkSR.transform.localScale : sr.transform.localScale;
            faceReversedScale.x *= -1f;
            onWaterFaceBlinkSR.transform.localScale = faceReversedScale;
        }
    }

    private void UpdateShadowVisual(bool _isMoving, bool _bInHub)
    {
        if (shadowAnim == null) return;

        shadowAnim.SetBool(isMovingHash, _isMoving);
        shadowAnim.SetBool(bInHubHash, _bInHub);

        float shadowAngle = environmentProvider.shadowDataProvider.CurrentShadowAngle;
        float normalizedAngle = shadowAngle % 360;
        if (normalizedAngle < 0) normalizedAngle += 360;

        if (normalizedAngle <= 22.5f || normalizedAngle >= 337.5f)
        {
            SetAnimatorDirection(shadowAnim, shadowSR, Vector2.right);
        }
        else if (normalizedAngle >= 157.5f && normalizedAngle <= 202.5f)
        {
            SetAnimatorDirection(shadowAnim, shadowSR, Vector2.left);
        }
        else
        {
            float lightPerspectiveAngle = currentFacingAngle - shadowAngle + 90f;
            Vector2 lightViewDir = new Vector2(
                Mathf.Cos(lightPerspectiveAngle * Mathf.Deg2Rad),
                Mathf.Sin(lightPerspectiveAngle * Mathf.Deg2Rad)
            );
            SetAnimatorDirection(shadowAnim, shadowSR, lightViewDir);
        }
    }

    private void SetAnimatorDirection(Animator _targetAnim, SpriteRenderer _targetSR, Vector2 _input)
    {
        if (_input.sqrMagnitude < 0.01f) return;

        float angle = Mathf.Atan2(_input.y, _input.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        int dirIndex = Mathf.RoundToInt(angle / 45f) % 8;
        bool flipX = false;
        int animIndex = -1;

        switch (dirIndex)
        {
            case 0: animIndex = 0; break;
            case 1: animIndex = 1; break;
            case 2: animIndex = 2; break;
            case 3: animIndex = 1; flipX = true; break;
            case 4: animIndex = 0; flipX = true; break;
            case 5: animIndex = 4; flipX = true; break;
            case 6: animIndex = 3; break;
            case 7: animIndex = 4; break;
        }

        if (animIndex != -1)
        {
            Vector3 scale = _targetSR.transform.localScale;
            scale.x = flipX ? -1f : 1f;
            _targetSR.transform.localScale = scale;

            _targetAnim.SetFloat(facingDirHash, animIndex);
        }
    }

    #endregion

    public void CharacterIsDead(bool _boolean)
    {
        onWaterSR.enabled = !_boolean;
        onWaterFaceSR.enabled = !_boolean;
        onWaterFaceBlinkSR.enabled = !_boolean;
        shadowSR.enabled = !_boolean;
        faceSR.enabled = !_boolean;
        faceBlinkSR.enabled = !_boolean;
    }
}
