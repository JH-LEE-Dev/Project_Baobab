using System;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;

/// <summary>
/// 별 표식 나무가 사라진 자리에 표시되는 지상 마크입니다.
/// Spawn, Idle, Manifest 상태를 독립적으로 관리하며 모든 스프라이트는 프리팹에서 직접 참조합니다.
/// </summary>
[RequireComponent(typeof(SortingGroup))]
public class TreeStarMarkGroundAnimator : MonoBehaviour
{
    private enum AnimationState
    {
        Inactive,
        Spawn,
        Idle,
        ManifestDelay,
        Manifest,
        ManifestComplete
    }

    [Header("Renderers")]
    [SerializeField] private SortingGroup sortingGroup;
    [SerializeField] private SpriteRenderer starRenderer;
    [SerializeField] private SpriteRenderer sparkleRenderer;

    [Header("Sparkle Animation")]
    [Tooltip("StarMark_2의 두 번째 스프라이트부터 순서대로 할당합니다.")]
    [SerializeField] private Sprite[] sparkleFrames;
    [Min(0.01f)]
    [SerializeField] private float sparkleFrameRate = 12f;

    [Header("Idle")]
    [Tooltip("프리팹 루트 기준 별과 반짝이의 표시 위치입니다. 정렬 기준은 루트의 SortingGroup 위치를 사용합니다.")]
    [SerializeField] private Vector2 visualLocalOffset = new Vector2(0f, 0.5f);
    [Min(0.01f)]
    [SerializeField] private float bobCycleDuration = 1.6667f;
    [Min(0.01f)]
    [SerializeField] private float rotationCycleDuration = 1.9f;
    [Min(0f)]
    [SerializeField] private float bobAmplitude = 0.08f;
    [Min(0f)]
    [SerializeField] private float rotationAmplitude = 6f;

    [Header("Spawn")]
    [Min(0.01f)]
    [SerializeField] private float spawnDuration = 0.8f;
    [Min(1f)]
    [SerializeField] private float spawnOvershootScale = 1.2f;
    [Range(0.8f, 1f)]
    [SerializeField] private float spawnSettleUndershootScale = 0.97f;
    [SerializeField] private float spawnClockwiseRotation = -360f;

    [Header("Manifest")]
    [Min(0f)]
    [SerializeField] private float manifestRandomDelayMax = 0.15f;
    [Min(0.01f)]
    [SerializeField] private float manifestDuration = 0.4f;
    [Min(1f)]
    [SerializeField] private float manifestOvershootScale = 1.2f;
    [Min(0f)]
    [SerializeField] private float manifestEndScale = 0.1f;
    [SerializeField] private float manifestClockwiseRotation = -270f;

    [Header("Rendering")]
    [SerializeField] private float hdrIntensity = 1.75f;
    [SerializeField] private int sparkleSortingOrderOffset = 1;

    public event Action<TreeStarMarkGroundAnimator> ManifestFinishedEvent;

    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");
    private const float TwoPi = Mathf.PI * 2f;

    private MaterialPropertyBlock materialPropertyBlock;
    private IObjectPool<TreeStarMarkGroundAnimator> pool;
    private AnimationState state;
    private float stateTimer;
    private float idleTimer;
    private float manifestDelay;
    private float sparkleFrameTimer;
    private float currentStarAngle;
    private float manifestStartAngle;
    private Vector3 manifestLocalPosition;
    private int currentSparkleFrame;
    private bool isReturned;

    public int GroupId { get; private set; } = -1;

    private MaterialPropertyBlock PropertyBlock =>
        materialPropertyBlock ??= new MaterialPropertyBlock();

    private void Awake()
    {
        ApplyVisualDefaults();
        ResetVisualPose();
    }

    private void OnDisable()
    {
        state = AnimationState.Inactive;
        ResetVisualPose();
    }

    public void SetPool(IObjectPool<TreeStarMarkGroundAnimator> _pool)
    {
        pool = _pool;
    }

    public void SetGroupId(int _groupId)
    {
        GroupId = _groupId;
    }

    public void SetSortingOrder(int _order)
    {
        if (sortingGroup != null)
        {
            sortingGroup.sortingOrder = _order;

            if (starRenderer != null)
                starRenderer.sortingOrder = 0;

            if (sparkleRenderer != null)
                sparkleRenderer.sortingOrder = sparkleSortingOrderOffset;

            return;
        }

        if (starRenderer != null)
            starRenderer.sortingOrder = _order;

        if (sparkleRenderer != null)
            sparkleRenderer.sortingOrder = _order + sparkleSortingOrderOffset;
    }

    /// <summary>
    /// 풀에서 꺼낸 마크의 Spawn 연출을 처음부터 시작합니다.
    /// Manifest가 이미 진행 중인 경우에는 해당 연출을 방해하지 않습니다.
    /// </summary>
    public void Play()
    {
        if (state == AnimationState.Manifest || state == AnimationState.ManifestComplete)
            return;

        isReturned = false;
        state = AnimationState.Spawn;
        stateTimer = 0f;
        idleTimer = 0f;
        manifestDelay = 0f;
        sparkleFrameTimer = 0f;
        currentSparkleFrame = 0;
        currentStarAngle = 0f;

        ApplyVisualDefaults();

        if (sparkleRenderer != null)
        {
            sparkleRenderer.sprite = HasSparkleFrames ? sparkleFrames[0] : null;
            sparkleRenderer.enabled = HasSparkleFrames;
        }

        ApplyPosition(visualLocalOffset);
        ApplyStarRotation(0f);
        ApplyUniformScale(0f);
    }

    /// <summary>
    /// Idle에서는 0~manifestRandomDelayMax초 뒤 발현하고,
    /// Spawn 중이거나 아직 정상 Idle에 진입하지 않았다면 즉시 Spawn을 취소하고 발현합니다.
    /// </summary>
    public void PlayManifestEffect()
    {
        switch (state)
        {
            case AnimationState.Manifest:
            case AnimationState.ManifestComplete:
            case AnimationState.ManifestDelay:
                return;

            case AnimationState.Idle:
                manifestDelay = UnityEngine.Random.Range(0f, manifestRandomDelayMax);

                if (manifestDelay <= 0f)
                {
                    StartManifest();
                    return;
                }

                state = AnimationState.ManifestDelay;
                stateTimer = 0f;
                return;

            default:
                StartManifest();
                return;
        }
    }

    public void NotifyManifestFinished()
    {
        if (isReturned)
            return;

        ManifestFinishedEvent?.Invoke(this);
    }

    public void ForceReturnToPool()
    {
        if (isReturned)
            return;

        isReturned = true;
        pool?.Release(this);
    }

    private void Update()
    {
        float _deltaTime = Time.deltaTime;

        switch (state)
        {
            case AnimationState.Spawn:
                UpdateSparkleAnimation(_deltaTime);
                UpdateSpawn(_deltaTime);
                break;

            case AnimationState.Idle:
                UpdateSparkleAnimation(_deltaTime);
                UpdateIdle(_deltaTime);
                break;

            case AnimationState.ManifestDelay:
                UpdateSparkleAnimation(_deltaTime);
                UpdateIdle(_deltaTime);
                UpdateManifestDelay(_deltaTime);
                break;

            case AnimationState.Manifest:
                UpdateManifest(_deltaTime);
                break;
        }
    }

    private void UpdateSpawn(float _deltaTime)
    {
        stateTimer += _deltaTime;
        float _normalizedTime = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, spawnDuration));
        float _scale = EvaluateSpawnScale(_normalizedTime);
        float _rotationEnd = spawnClockwiseRotation + rotationAmplitude;
        float _rotationProgress = SmootherStep01(_normalizedTime);

        currentStarAngle = Mathf.LerpUnclamped(0f, _rotationEnd, _rotationProgress);

        ApplyPosition(visualLocalOffset);
        ApplyStarRotation(currentStarAngle);
        ApplyUniformScale(_scale);

        if (_normalizedTime < 1f)
            return;

        state = AnimationState.Idle;
        stateTimer = 0f;
        idleTimer = 0f;
        ApplyIdlePose();
    }

    private void UpdateIdle(float _deltaTime)
    {
        idleTimer += _deltaTime;
        ApplyIdlePose();
    }

    private void ApplyIdlePose()
    {
        float _bobPhase = idleTimer / Mathf.Max(0.01f, bobCycleDuration) * TwoPi;
        float _rotationPhase =
            idleTimer / Mathf.Max(0.01f, rotationCycleDuration) * TwoPi + Mathf.PI * 0.5f;
        float _bobOffset = Mathf.Sin(_bobPhase) * bobAmplitude;

        currentStarAngle = Mathf.Sin(_rotationPhase) * rotationAmplitude;

        ApplyPosition(new Vector3(
            visualLocalOffset.x,
            visualLocalOffset.y + _bobOffset,
            0f));
        ApplyStarRotation(currentStarAngle);
        ApplyUniformScale(1f);
    }

    private void UpdateManifestDelay(float _deltaTime)
    {
        stateTimer += _deltaTime;

        if (stateTimer >= manifestDelay)
            StartManifest();
    }

    private void StartManifest()
    {
        if (state == AnimationState.Manifest || state == AnimationState.ManifestComplete)
            return;

        state = AnimationState.Manifest;
        stateTimer = 0f;
        manifestStartAngle = currentStarAngle;
        manifestLocalPosition = starRenderer != null
            ? starRenderer.transform.localPosition
            : (Vector3)visualLocalOffset;

        ApplyPosition(manifestLocalPosition);
        ApplyStarRotation(manifestStartAngle);
        ApplyUniformScale(1f);
    }

    private void UpdateManifest(float _deltaTime)
    {
        stateTimer += _deltaTime;
        float _normalizedTime = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, manifestDuration));
        float _rotationProgress = SmootherStep01(_normalizedTime);

        currentStarAngle = manifestStartAngle + manifestClockwiseRotation * _rotationProgress;

        ApplyPosition(manifestLocalPosition);
        ApplyStarRotation(currentStarAngle);
        ApplyUniformScale(EvaluateManifestScale(_normalizedTime));

        if (_normalizedTime < 1f)
            return;

        state = AnimationState.ManifestComplete;
        NotifyManifestFinished();
    }

    private float EvaluateSpawnScale(float _normalizedTime)
    {
        const float OvershootEnd = 0.58f;
        const float UndershootEnd = 0.82f;

        if (_normalizedTime < OvershootEnd)
        {
            float _segmentTime = _normalizedTime / OvershootEnd;
            return Mathf.LerpUnclamped(0f, spawnOvershootScale, SmootherStep01(_segmentTime));
        }

        if (_normalizedTime < UndershootEnd)
        {
            float _segmentTime =
                (_normalizedTime - OvershootEnd) / (UndershootEnd - OvershootEnd);
            return Mathf.LerpUnclamped(
                spawnOvershootScale,
                spawnSettleUndershootScale,
                SmootherStep01(_segmentTime));
        }

        float _settleTime = (_normalizedTime - UndershootEnd) / (1f - UndershootEnd);
        return Mathf.LerpUnclamped(
            spawnSettleUndershootScale,
            1f,
            SmootherStep01(_settleTime));
    }

    private float EvaluateManifestScale(float _normalizedTime)
    {
        const float OvershootEnd = 0.3f;

        if (_normalizedTime < OvershootEnd)
        {
            float _segmentTime = _normalizedTime / OvershootEnd;
            return Mathf.LerpUnclamped(1f, manifestOvershootScale, SmootherStep01(_segmentTime));
        }

        float _shrinkTime = (_normalizedTime - OvershootEnd) / (1f - OvershootEnd);
        return Mathf.LerpUnclamped(
            manifestOvershootScale,
            manifestEndScale,
            SmootherStep01(_shrinkTime));
    }

    private void UpdateSparkleAnimation(float _deltaTime)
    {
        if (!HasSparkleFrames || sparkleRenderer == null)
            return;

        sparkleFrameTimer += _deltaTime;
        float _frameDuration = 1f / Mathf.Max(0.01f, sparkleFrameRate);

        while (sparkleFrameTimer >= _frameDuration)
        {
            sparkleFrameTimer -= _frameDuration;
            currentSparkleFrame = (currentSparkleFrame + 1) % sparkleFrames.Length;
            sparkleRenderer.sprite = sparkleFrames[currentSparkleFrame];
        }
    }

    private void ApplyPosition(Vector3 _localPosition)
    {
        if (starRenderer != null)
            starRenderer.transform.localPosition = _localPosition;

        if (sparkleRenderer != null)
            sparkleRenderer.transform.localPosition = _localPosition;
    }

    private void ApplyStarRotation(float _angle)
    {
        if (starRenderer != null)
            starRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, _angle);

        if (sparkleRenderer != null)
            sparkleRenderer.transform.localRotation = Quaternion.identity;
    }

    private void ApplyUniformScale(float _scale)
    {
        Vector3 _localScale = Vector3.one * _scale;

        if (starRenderer != null)
            starRenderer.transform.localScale = _localScale;

        if (sparkleRenderer != null)
            sparkleRenderer.transform.localScale = _localScale;
    }

    private void ApplyVisualDefaults()
    {
        PropertyBlock.SetFloat(HDRIntensityID, hdrIntensity);

        if (starRenderer != null)
        {
            starRenderer.enabled = starRenderer.sprite != null;
            starRenderer.SetPropertyBlock(PropertyBlock);
        }

        if (sparkleRenderer != null)
            sparkleRenderer.SetPropertyBlock(PropertyBlock);
    }

    private void ResetVisualPose()
    {
        currentStarAngle = 0f;
        ApplyPosition(visualLocalOffset);
        ApplyStarRotation(0f);
        ApplyUniformScale(1f);
    }

    private static float SmootherStep01(float _value)
    {
        float _t = Mathf.Clamp01(_value);
        return _t * _t * _t * (_t * (_t * 6f - 15f) + 10f);
    }

    private bool HasSparkleFrames => sparkleFrames != null && sparkleFrames.Length > 0;
}
