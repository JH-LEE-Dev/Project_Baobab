using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 나무가 보석 단계(황금/다이아/무지개)로 변할 때 한 번 재생하는 스프라이트 시트 VFX.
/// SporeExplosionVFX와 동일한 패턴 - 프리팹으로 존재하고 InDungeonVFXManager가 ObjectPool로 관리하며,
/// 프레임은 인스펙터에서 직접 연결한다 (Resources.LoadAll 사용 안 함).
///
/// 스프라이트 피벗이 이펙트의 원(폭발 중심)에 맞춰져 있으므로, 오브젝트를 나무 Top 위치에 그대로
/// 놓기만 하면 원 중심이 그 지점에 정렬된다. 별도의 오프셋 보정이 필요 없다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TreeTransformVFX : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float frameRate = 24f;

    [Tooltip("발광 세기. 머티리얼을 다른 VFX와 공유하므로 이 이펙트에만 적용되도록 프로퍼티 블록으로 넣는다.")]
    [SerializeField] private float hdrIntensity = 1.5f;

    // 날아다니는 아이템과 같은 레이어. 나무 본체보다 앞에 그려진다.
    private const string SortingLayerName = "FlyingItem";
    private const int SortingPrecision = 100;

    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");

    private SpriteRenderer spriteRenderer;
    private IObjectPool<TreeTransformVFX> pool;
    private MaterialPropertyBlock mpb;
    private float frameTimer;
    private int currentFrame;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void SetPool(IObjectPool<TreeTransformVFX> _pool)
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

        // 풀에서 재사용되므로 이전 재생이 남긴 변형을 지운다.
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;

        spriteRenderer.sprite = frames[0];
        spriteRenderer.sortingLayerName = SortingLayerName;

        // 공유 머티리얼을 직접 고치면 같은 재질을 쓰는 다른 VFX(SporeExplosion 등)까지 밝아지므로
        // 이 렌더러에만 적용되는 프로퍼티 블록으로 넣는다. 재생마다 세팅해 인스펙터 수정도 바로 반영된다.
        if (mpb == null) mpb = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(HDRIntensityID, hdrIntensity);
        spriteRenderer.SetPropertyBlock(mpb);

        // CustomSortable과 동일한 공식으로 1회성 정렬 순서를 계산한다 (매 프레임 갱신할 필요는 없음).
        spriteRenderer.sortingOrder = -Mathf.RoundToInt(transform.position.y * SortingPrecision) + _sortingOrderOffset;
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / Mathf.Max(1f, frameRate);

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
