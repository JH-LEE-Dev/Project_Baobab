using UnityEngine;

/// <summary>
/// StarrootForest에서 별자리를 구성하는(별 표식) 나무에 붙어, TreeStarMark 스프라이트 시트를
/// 반복 재생하는 마커 애니메이션. 나무가 별 표식일 때만 활성화된다 (TreeVisualComponent.SetConstellationMarkActive).
/// 프레임은 인스펙터에서 직접 연결한다 (Resources.LoadAll 사용 안 함).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TreeStarMarkAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;

    private const float FrameRate = 12f;

    private SpriteRenderer spriteRenderer;
    private float frameTimer;
    private int currentFrame;

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
            spriteRenderer.sprite = frames[0];
        }
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / FrameRate;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrame = (currentFrame + 1) % frames.Length; // 별 표식이 유지되는 동안 계속 루프
            spriteRenderer.sprite = frames[currentFrame];
        }
    }
}
