using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// FireExplosion 스프라이트 시트를 프레임 단위로 재생하는 1회성 VFX 오브젝트.
/// SporeExplosionVFX와 완전히 동일한 방식으로 동작하며(프리팹 + InDungeonVFXManager의 ObjectPool),
/// 과열 강화된 ShockWave가 나무를 때렸을 때 InDungeonObjectManager가 재생한다.
/// 프레임은 인스펙터에서 직접 연결한다 (Resources.LoadAll 사용 안 함).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class FireExplosionVFX : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;

    private const float FrameRate = 24f;
    private const int SortingPrecision = 100;
    private const float MinStartScale = 0.5f;
    private const float MaxStartScale = 0.8f;
    private const float MinDriftPixels = 3f;
    private const float MaxDriftPixels = 4f;

    // 새로 생성되는 GameObject는 기본적으로 "Default" 정렬 레이어를 쓰는데, 이 프로젝트는
    // Default를 정렬 레이어 목록 맨 뒤(가장 안쪽)에만 두고 있어 다른 오브젝트에 전부 가려진다.
    // 나무 본체와 동일한 레이어를 써야 화면에 정상적으로 보인다.
    private const string SortingLayerName = "Objects";

    private SpriteRenderer spriteRenderer;
    private IObjectPool<FireExplosionVFX> pool;
    private float frameTimer;
    private float elapsedTime;
    private float animationDuration;
    private int currentFrame;
    private Vector3 startPosition;
    private Vector3 endPosition;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetPool(IObjectPool<FireExplosionVFX> _pool)
    {
        pool = _pool;
    }

    public void Play(int _sortingOrderOffset, Vector2 _outwardDirection)
    {
        if (frames == null || frames.Length == 0)
        {
            ReturnToPool();
            return;
        }

        currentFrame = 0;
        frameTimer = 0f;
        elapsedTime = 0f;
        animationDuration = frames.Length / FrameRate;

        float randomScale = Random.Range(MinStartScale, MaxStartScale);
        transform.localScale = Vector3.one * randomScale;
        transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        Vector2 driftDirection = _outwardDirection.sqrMagnitude > 0f
            ? _outwardDirection.normalized
            : Random.insideUnitCircle.normalized;
        float pixelsPerUnit = Mathf.Max(1f, frames[0].pixelsPerUnit);
        float driftDistance = Random.Range(MinDriftPixels, MaxDriftPixels) / pixelsPerUnit;
        startPosition = transform.position;
        endPosition = startPosition + (Vector3)(driftDirection * driftDistance);

        spriteRenderer.sprite = frames[0];
        spriteRenderer.sortingLayerName = SortingLayerName;

        // CustomSortable과 동일한 공식으로 1회성 정렬 순서를 계산한다 (매 프레임 갱신할 필요는 없음).
        spriteRenderer.sortingOrder = -Mathf.RoundToInt(transform.position.y * SortingPrecision) + _sortingOrderOffset;
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        float deltaTime = Time.deltaTime;
        frameTimer += deltaTime;
        elapsedTime += deltaTime;

        float normalizedTime = animationDuration > 0f
            ? Mathf.Clamp01(elapsedTime / animationDuration)
            : 1f;
        float easedTime = 1f - Mathf.Pow(1f - normalizedTime, 3f);
        transform.position = Vector3.LerpUnclamped(startPosition, endPosition, easedTime);

        float frameDuration = 1f / FrameRate;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                ReturnToPool();
                return;
            }

            spriteRenderer.sprite = frames[currentFrame];
        }
    }

    private void ReturnToPool()
    {
        pool?.Release(this);
    }
}
