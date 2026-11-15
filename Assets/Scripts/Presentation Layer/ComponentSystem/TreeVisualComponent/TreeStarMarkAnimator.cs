using UnityEngine;

/// <summary>
/// StarrootForest에서 별자리를 구성하는(별 표식) 나무에 붙어, TreeStarMark 스프라이트 시트를
/// 반복 재생하는 마커 애니메이션. 나무가 별 표식일 때만 활성화된다 (TreeVisualComponent.SetConstellationMarkActive).
/// 프레임은 인스펙터에서 직접 연결한다 (Resources.LoadAll 사용 안 함).
/// 표식이 수백 개라 각자 Update를 받지 않고 TreeStarMarkAnimatorDriver가 한 번의 Update에서 차례로 진행시킨다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TreeStarMarkAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float frameRate = 12f;

    private SpriteRenderer spriteRenderer;
    private float frameTimer;
    private int currentFrame;

    // 드라이버 목록 안의 위치(-1: 등록 안 됨). 드라이버만 쓴다.
    internal int driverIndex = -1;
    internal bool bDriverPending;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void OnEnable()
    {
        frameTimer = 0f;
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

    // 원래 Update 본문. _deltaTime은 그 프레임의 Time.deltaTime이다.
    internal void Tick(float _deltaTime)
    {
        if (frames == null || frames.Length == 0) return;

        frameTimer += _deltaTime;
        float frameDuration = 1f / Mathf.Max(0.01f, frameRate);

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrame = (currentFrame + 1) % frames.Length; // 별 표식이 유지되는 동안 계속 루프
            spriteRenderer.sprite = frames[currentFrame];
        }
    }
}
