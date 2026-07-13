using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// StarrootForest에서 별 표식 나무가 죽은 자리에 스폰되어 TreeStarMark_Ground 스프라이트를
/// Loop로 재생하는 마커 애니메이션. 소속 그룹의 별자리 발현이 트리거되어 InDungeonVFXManager가
/// 강제로 회수하기 전까지는 자동으로 사라지지 않고 계속 재생된다. Destroy 없이 ObjectPool로 재사용된다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TreeStarMarkGroundAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames; // 인스펙터에서 직접 할당 (Resources.LoadAll 사용 안 함)
    [SerializeField] private float frameRate = 12f;

    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");
    private MaterialPropertyBlock _mpb;
    private MaterialPropertyBlock Mpb => _mpb ??= new MaterialPropertyBlock();

    private SpriteRenderer spriteRenderer;
    private IObjectPool<TreeStarMarkGroundAnimator> pool;
    private float frameTimer;
    private int currentFrame;
    private bool isReturned;

    public int GroupId { get; private set; } = -1;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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
        if (spriteRenderer != null) spriteRenderer.sortingOrder = _order;
    }

    // 죽은 나무의 constellationRenderer가 쓰던 HDR 강도를 그대로 물려받아 동일한 발광 세기를 유지한다.
    public void SetHDRIntensity(float _intensity)
    {
        if (spriteRenderer == null) return;

        Mpb.SetFloat(HDRIntensityID, _intensity);
        spriteRenderer.SetPropertyBlock(Mpb);
    }

    public void Play()
    {
        isReturned = false;
        frameTimer = 0f;
        currentFrame = 0;

        if (frames != null && frames.Length > 0 && spriteRenderer != null)
            spriteRenderer.sprite = frames[0];
    }

    // 소속 그룹의 별자리 발현이 트리거되면 InDungeonVFXManager가 호출해 즉시 회수한다.
    public void ForceReturnToPool()
    {
        if (isReturned) return;
        isReturned = true;

        pool?.Release(this);
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / frameRate;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrame = (currentFrame + 1) % frames.Length; // 발현 전까지 계속 루프
            spriteRenderer.sprite = frames[currentFrame];
        }
    }
}
