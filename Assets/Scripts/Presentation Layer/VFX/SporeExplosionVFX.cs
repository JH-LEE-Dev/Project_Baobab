using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// SporeExplosion 스프라이트 시트를 프레임 단위로 재생하는 1회성 VFX 오브젝트.
/// 프리팹(TreeStarMarkGroundAnimator와 동일한 패턴)으로 존재하며, InDungeonVFXManager가
/// ObjectPool로 관리한다. 프레임은 인스펙터에서 직접 연결한다 (Resources.LoadAll 사용 안 함).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class SporeExplosionVFX : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;

    private const float FrameRate = 24f;
    private const int SortingPrecision = 100;

    // 새로 생성되는 GameObject는 기본적으로 "Default" 정렬 레이어를 쓰는데, 이 프로젝트는
    // Default를 정렬 레이어 목록 맨 뒤(가장 안쪽)에만 두고 있어 다른 오브젝트에 전부 가려진다.
    // 나무 본체와 동일한 레이어를 써야 화면에 정상적으로 보인다.
    private const string SortingLayerName = "Objects";

    private SpriteRenderer spriteRenderer;
    private IObjectPool<SporeExplosionVFX> pool;
    private float frameTimer;
    private int currentFrame;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetPool(IObjectPool<SporeExplosionVFX> _pool)
    {
        pool = _pool;
    }

    public void Play(int _sortingOrderOffset)
    {
        if (frames == null || frames.Length == 0)
        {
            ReturnToPool();
            return;
        }

        currentFrame = 0;
        frameTimer = 0f;
        spriteRenderer.sprite = frames[0];
        spriteRenderer.sortingLayerName = SortingLayerName;

        // CustomSortable과 동일한 공식으로 1회성 정렬 순서를 계산한다 (매 프레임 갱신할 필요는 없음).
        spriteRenderer.sortingOrder = -Mathf.RoundToInt(transform.position.y * SortingPrecision) + _sortingOrderOffset;
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
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
