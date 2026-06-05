using UnityEngine;
using System.Collections.Generic;

public class LogStorage : MonoBehaviour
{
    public SpriteRenderer spriteRenderer;

    // 시각적 효과용 (Squash & Stretch)
    private Transform visualTransform;
    private float bounceTime = 1f;
    private const float BOUNCE_DURATION = 0.2f;

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
        if (bounceTime >= BOUNCE_DURATION)
        {
            if (visualTransform != null && visualTransform.localScale != Vector3.one)
                visualTransform.localScale = new Vector3(1f, 1f, 1f);
            return;
        }

        bounceTime += _deltaTime;
        float t = bounceTime / BOUNCE_DURATION;

        // 진폭을 0.4로 키우고 감쇠를 3f로 늦춰 더 찰진 느낌 부여
        float curve = Mathf.Sin(t * Mathf.PI * 3f) * Mathf.Exp(-t * 1.5f) * 0.2f;

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
