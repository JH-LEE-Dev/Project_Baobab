using UnityEngine;

/// <summary>
/// StarrootForest에서 별자리를 구성하는(별 표식) 나무에 붙어, TreeStarMark 스프라이트 시트를
/// 반복 재생하는 마커 애니메이션. 나무가 별 표식일 때만 활성화된다 (TreeVisualComponent.SetConstellationMarkActive).
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class TreeStarMarkAnimator : MonoBehaviour
{
    private const string ResourcePath = "TreeStarMark/TreeStarMark";
    private const float FrameRate = 12f;

    private static Sprite[] cachedFrames;

    private SpriteRenderer spriteRenderer;
    private float frameTimer;
    private int currentFrame;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureFramesLoaded();
    }

    private void OnEnable()
    {
        frameTimer = 0f;
        currentFrame = 0;

        if (cachedFrames != null && cachedFrames.Length > 0 && spriteRenderer != null)
        {
            spriteRenderer.sprite = cachedFrames[0];
        }
    }

    private static void EnsureFramesLoaded()
    {
        if (cachedFrames != null) return;

        Sprite[] loaded = Resources.LoadAll<Sprite>(ResourcePath);
        System.Array.Sort(loaded, (a, b) => ExtractFrameIndex(a.name).CompareTo(ExtractFrameIndex(b.name)));
        cachedFrames = loaded;
    }

    private static int ExtractFrameIndex(string _spriteName)
    {
        int underscoreIdx = _spriteName.LastIndexOf('_');
        if (underscoreIdx >= 0 && int.TryParse(_spriteName.Substring(underscoreIdx + 1), out int idx))
        {
            return idx;
        }
        return 0;
    }

    private void Update()
    {
        if (cachedFrames == null || cachedFrames.Length == 0) return;

        frameTimer += Time.deltaTime;
        float frameDuration = 1f / FrameRate;

        while (frameTimer >= frameDuration)
        {
            frameTimer -= frameDuration;
            currentFrame = (currentFrame + 1) % cachedFrames.Length; // 별 표식이 유지되는 동안 계속 루프
            spriteRenderer.sprite = cachedFrames[currentFrame];
        }
    }
}
