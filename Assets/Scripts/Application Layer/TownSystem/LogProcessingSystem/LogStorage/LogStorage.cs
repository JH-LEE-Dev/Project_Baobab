using UnityEngine;
using System.Collections.Generic;

public class LogStorage : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;

    // 시각적 효과용 (Squash & Stretch)
    private Transform visualTransform;
    private float bounceTime = 1f;
    private const float BOUNCE_DURATION = 0.2f;

    // 지금 재생 중인 반동의 모양. 평소 평가(TriggerBounce)는 예전 값 그대로이고,
    // 특등품 평가(TriggerTopgradeBounce)만 더 깊게 눌렸다가 길게 출렁인다.
    private const float BOUNCE_AMPLITUDE = 0.2f;
    private const float BOUNCE_DECAY = 1.5f;
    private const float BOUNCE_HALF_CYCLES = 3f;
    private float activeBounceDuration = BOUNCE_DURATION;
    private float activeBounceAmplitude = BOUNCE_AMPLITUDE;
    private float activeBounceDecay = BOUNCE_DECAY;
    private float activeBounceHalfCycles = BOUNCE_HALF_CYCLES;

    private CustomSortable customSortable;
    [SerializeField] private List<Sprite> animationSprites;
    [SerializeField] private Sprite idleSprite;

    private bool isPlayingAnimation;
    private float animationTime;
    private const float FRAME_RATE = 10f;

    public void Initialize()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 자식 오브젝트의 SpriteRenderer Transform 캐싱
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) visualTransform = sr.transform;

        customSortable = GetComponent<CustomSortable>();
        customSortable.Initialize(transform);
        customSortable.AddSpriteRenderer(spriteRenderer);
    }

    private void Update()
    {
        UpdateBounce(Time.deltaTime);
        UpdateAnimation(Time.deltaTime);
    }

    private void LateUpdate()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }

    public void TriggerBounce()
    {
        activeBounceDuration = BOUNCE_DURATION;
        activeBounceAmplitude = BOUNCE_AMPLITUDE;
        activeBounceDecay = BOUNCE_DECAY;
        activeBounceHalfCycles = BOUNCE_HALF_CYCLES;
        StartBounce();
    }

    /// <summary>
    /// 특등품 평가의 "쾅". 평소 반동과 같은 눌림(가로로 퍼지고 세로로 눌림)으로 시작하되 더 깊고 길게 출렁인다.
    /// 출력 애니메이션(animationSprites)도 평소처럼 처음부터 다시 돈다.
    /// </summary>
    public void TriggerTopgradeBounce(float _duration, float _amplitude, float _decay, float _halfCycles)
    {
        activeBounceDuration = Mathf.Max(0.01f, _duration);
        activeBounceAmplitude = _amplitude;
        activeBounceDecay = _decay;
        activeBounceHalfCycles = _halfCycles;
        StartBounce();
    }

    private void StartBounce()
    {
        bounceTime = 0f;
        if (animationSprites != null && animationSprites.Count > 0)
        {
            isPlayingAnimation = true;
            animationTime = 0f;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = animationSprites[0];
            }
        }
    }

    private void UpdateBounce(float _deltaTime)
    {
        if (bounceTime >= activeBounceDuration)
        {
            if (visualTransform != null && visualTransform.localScale != Vector3.one)
                visualTransform.localScale = new Vector3(1f, 1f, 1f);
            return;
        }

        bounceTime += _deltaTime;
        float t = bounceTime / activeBounceDuration;

        // 진폭을 0.4로 키우고 감쇠를 3f로 늦춰 더 찰진 느낌 부여
        float curve = Mathf.Sin(t * Mathf.PI * activeBounceHalfCycles) * Mathf.Exp(-t * activeBounceDecay) * activeBounceAmplitude;

        if (visualTransform != null)
        {
            // X축 확대 시 Y축 축소 (Squash & Stretch)
            visualTransform.localScale = new Vector3(1f + curve, 1f - curve, 1f);
        }
    }

    private void UpdateAnimation(float _deltaTime)
    {
        if (!isPlayingAnimation) return;

        if (animationSprites == null || animationSprites.Count == 0)
        {
            isPlayingAnimation = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
            return;
        }

        animationTime += _deltaTime;
        int index = Mathf.FloorToInt(animationTime * FRAME_RATE);

        if (index >= animationSprites.Count)
        {
            isPlayingAnimation = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = idleSprite;
            }
        }
        else
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.sprite = animationSprites[index];
            }
        }
    }
}
