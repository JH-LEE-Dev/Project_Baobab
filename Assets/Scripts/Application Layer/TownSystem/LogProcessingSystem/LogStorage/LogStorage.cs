using UnityEngine;

public class LogStorage : MonoBehaviour
{
    public Animator animator;
    public SpriteRenderer spriteRenderer;

    // 시각적 효과용 (Squash & Stretch)
    private Transform visualTransform;
    private float bounceTime = 1f;
    private const float BOUNCE_DURATION = 0.2f;

    public void Initialize()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 자식 오브젝트의 SpriteRenderer Transform 캐싱
        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) visualTransform = sr.transform;
    }

    private void Update()
    {
        UpdateBounce(Time.deltaTime);
    }

    public void TriggerBounce()
    {
        bounceTime = 0f;
    }

    private void UpdateBounce(float _deltaTime)
    {
        if (bounceTime >= BOUNCE_DURATION)
        {
            if (visualTransform != null && visualTransform.localScale != Vector3.one)
                visualTransform.localScale = Vector3.one;
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
}
