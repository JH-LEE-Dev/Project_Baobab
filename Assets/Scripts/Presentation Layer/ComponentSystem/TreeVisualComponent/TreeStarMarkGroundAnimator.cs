using System;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// StarrootForest에서 별 표식 나무가 죽은 자리에 스폰되어 TreeStarMark_Ground 스프라이트를
/// Loop로 재생하는 마커 애니메이션. 소속 그룹의 별자리 발현이 트리거되면 소멸 연출을 재생하고,
/// 연출이 끝나면(NotifyManifestFinished) 풀로 반환된다. Destroy 없이 ObjectPool로 재사용된다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TreeStarMarkGroundAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames; // 인스펙터에서 직접 할당 (Resources.LoadAll 사용 안 함)
    [SerializeField] private float frameRate = 12f;
    [SerializeField] private float hdrIntensity = 1.05f;

    // 소멸(별자리 발현) 연출이 끝났을 때 발생 - InDungeonVFXManager가 구독해 풀로 반환한다.
    public event Action<TreeStarMarkGroundAnimator> ManifestFinishedEvent;

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

    /// <summary>
    /// 생성 시점 - 그라운드 마크가 스폰될 때 InDungeonVFXManager가 호출한다.
    /// 스폰 연출(파티클, 팝업 등)이 필요하면 이 함수 안에 추가한다.
    /// </summary>
    public void Play()
    {
        isReturned = false;
        frameTimer = 0f;
        currentFrame = 0;

        if (spriteRenderer != null)
        {
            Mpb.SetFloat(HDRIntensityID, hdrIntensity);
            spriteRenderer.SetPropertyBlock(Mpb);
        }

        if (frames != null && frames.Length > 0 && spriteRenderer != null)
            spriteRenderer.sprite = frames[0];
    }

    /// <summary>
    /// 별자리 발현 시점 - 소속 그룹의 발현이 확정되면 InDungeonVFXManager가 호출한다.
    /// 즉시 풀로 반환되지 않으며, 소멸 연출(파티클, 페이드 등)을 여기에 추가한 뒤
    /// 연출이 끝나는 시점에 반드시 NotifyManifestFinished()를 호출해야 풀로 반환된다.
    /// </summary>
    public void PlayManifestEffect()
    {
        // 이펙트 작업자 연출 지점. 연출이 끝나면 NotifyManifestFinished()를 호출할 것.
    }

    /// <summary>
    /// 소멸 연출이 끝났을 때 이펙트 작업자가 호출한다(애니메이션 이벤트 등에서 연결).
    /// InDungeonVFXManager가 이 신호를 구독해 풀로 반환한다.
    /// </summary>
    public void NotifyManifestFinished()
    {
        if (isReturned) return;
        ManifestFinishedEvent?.Invoke(this);
    }

    // 던전 이탈 등 - 진행 중이던 연출과 무관하게 즉시 강제 회수한다.
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
        float frameDuration = 1f / Mathf.Max(0.01f, frameRate); // 0 이하 값이 들어와도 무한루프/정지되지 않도록 방어

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrame = (currentFrame + 1) % frames.Length; // 발현 전까지 계속 루프
            spriteRenderer.sprite = frames[currentFrame];
        }
    }
}
