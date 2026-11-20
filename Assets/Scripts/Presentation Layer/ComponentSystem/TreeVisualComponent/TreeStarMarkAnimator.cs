using UnityEngine;

/// <summary>
/// StarrootForest에서 별자리를 구성하는(별 표식) 나무에 붙어, TreeStarMark 스프라이트 시트를
/// 반복 재생하는 마커 애니메이션. 나무가 별 표식일 때만 활성화된다 (TreeVisualComponent.SetConstellationMarkActive).
/// 프레임은 인스펙터에서 직접 연결한다 (Resources.LoadAll 사용 안 함).
/// 표식이 수백 개라 각자 Update를 받지 않고 TreeStarMarkAnimatorDriver가 한 번의 Update에서 차례로 진행시킨다.
/// 프레임 타이머는 드라이버가 같은 때 켜진 같은 frameRate의 표식들끼리 묶어 한 번만 계산하고, 넘길 프레임 수만 Advance로 준다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TreeStarMarkAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float frameRate = 12f;

    private SpriteRenderer spriteRenderer;
    private int currentFrame;

    // 드라이버 목록 안의 위치(-1: 등록 안 됨). 드라이버만 쓴다.
    internal int driverIndex = -1;
    internal bool bDriverPending;
    internal TreeStarMarkAnimatorDriver.Bucket driverBucket;

    internal float FrameRate => frameRate;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        // 프레임 타이머는 드라이버의 새 묶음이 0에서 시작한다.
        currentFrame = 0;

        if (frames != null && frames.Length > 0 && spriteRenderer != null)
        {
            currentFrame = Random.Range(0, frames.Length);
            spriteRenderer.sprite = frames[currentFrame];
        }

        TreeStarMarkAnimatorDriver.Register(this);
    }

    private void OnDisable()
    {
        TreeStarMarkAnimatorDriver.Unregister(this);
    }

    // 원래 Update 본문의 프레임 넘김. _count는 이번 프레임에 넘길 프레임 수(드라이버가 묶음 타이머로 정한다, 1 이상).
    internal void Advance(int _count)
    {
        if (frames == null || frames.Length == 0) return;

        for (int i = 0; i < _count; i++)
        {
            currentFrame = (currentFrame + 1) % frames.Length; // 별 표식이 유지되는 동안 계속 루프
            spriteRenderer.sprite = frames[currentFrame];
        }
    }
}
