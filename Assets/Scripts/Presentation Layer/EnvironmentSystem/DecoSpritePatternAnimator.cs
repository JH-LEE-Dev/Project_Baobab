using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class DecoSpritePatternAnimator : MonoBehaviour
{
    public enum PlaybackMode
    {
        LoopPattern,
        RandomPatternWithWait,
        PlayPatternThenHideWithWait
    }

    [System.Serializable]
    public class FramePattern
    {
        public string name;
        public int[] frameIndices;
    }

    [SerializeField] private float hdrIntensity = 1f;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private FramePattern[] patterns;
    [SerializeField] private string fallbackPattern = "0,1,2,1,0";
    [SerializeField] private PlaybackMode playbackMode = PlaybackMode.LoopPattern;
    [SerializeField, Min(0.01f)] private float frameDuration = 0.333f;
    [SerializeField, Min(0f)] private float waitMin = 0.5f;
    [SerializeField, Min(0f)] private float waitMax = 1f;
    [SerializeField] private bool randomizeInitialWait = true;
    [SerializeField] private bool hideBetweenPatterns;

    // 물 위 배치(TileMapGenerator)가 육지·물가 픽셀을 피할 때 쓰는 모양. 모든 프레임의 불투명 픽셀을 합친 것을
    // 피벗 기준 픽셀 칸 좌표의 가로줄 (y, xMin, xMax - 둘 다 포함)로 담는다. 프레임을 바꾸면 에디터가 다시 잰다(OnValidate).
    [SerializeField, HideInInspector] private Vector3Int[] waterFootprintRows;
    [SerializeField, HideInInspector] private float waterFootprintPixelsPerUnit = 32f;

    public Vector3Int[] WaterFootprintRows => waterFootprintRows;
    public float WaterFootprintPixelsPerUnit => waterFootprintPixelsPerUnit;
    public Sprite FirstFrame => (frames != null && frames.Length > 0) ? frames[0] : null;

    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");
    // Get→Set은 블록 내용을 복사해 쓰므로 인스턴스마다 새로 만들지 않고 하나를 공유한다(메인 스레드 단일 실행).
    // 주의: static 필드 초기화(정적 생성자)로 만들면 안 된다. 타입이 MonoBehaviour 생성 중에 처음 쓰이면 그 시점에 정적 생성자가 돌고,
    // Unity는 그 문맥에서 MaterialPropertyBlock 생성을 금지해(CreateImpl 예외) 타입 초기화가 통째로 실패한다. Awake/Initialize에서 지연 생성한다.
    private static MaterialPropertyBlock sharedMpb;
    private static MaterialPropertyBlock SharedMpb => sharedMpb ??= new MaterialPropertyBlock();

    private Coroutine routine;
    private readonly FramePattern fallbackFramePattern = new FramePattern();
    // 프레임 간격은 런타임에 바뀌지 않으므로 대기 객체를 한 번만 만든다(인스턴스당 코루틴 1개라 재사용 안전).
    private WaitForSeconds frameWait;
    private float frameWaitDuration = -1f;

    private CustomSortable customSortable;

    private void Reset()
    {
        targetRenderer = GetComponent<SpriteRenderer>();
        customSortable = GetComponent<CustomSortable>();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;
        // 배치는 프리팹 에셋의 값을 읽으므로 프리팹 에셋(또는 프리팹 편집 화면)에서만 다시 잰다.
        // 씬에 만들어진 인스턴스(에디터 미리보기가 띄운 수백 개 등)마다 원본 PNG를 읽지 않도록 한다.
        if (false == UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this)
            && null == UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(gameObject)) return;
        RebuildWaterFootprint();
    }

    // 텍스처는 Read/Write가 꺼져 있어 원본 PNG를 직접 읽는다. 읽지 못하면(원본 크기와 임포트 크기가 다른 경우 등) 기존 값을 두고 경고한다.
    private void RebuildWaterFootprint()
    {
        if (frames == null || frames.Length == 0) return;

        var spans = new System.Collections.Generic.SortedDictionary<int, Vector2Int>();
        var pixelCache = new System.Collections.Generic.Dictionary<Texture2D, Color32[]>();
        float ppu = 0f;

        for (int f = 0; f < frames.Length; f++)
        {
            Sprite sprite = frames[f];
            if (sprite == null) continue;

            Texture2D texture = sprite.texture;
            if (false == pixelCache.TryGetValue(texture, out Color32[] pixels))
            {
                pixels = ReadSourcePixels(texture);
                if (pixels == null)
                {
                    Debug.LogWarning($"[DecoSpritePatternAnimator] {name}: 원본 그림을 읽지 못해 물 위 배치 모양을 다시 재지 못했습니다(예전 모양 유지). 텍스처: {UnityEditor.AssetDatabase.GetAssetPath(texture)}", this);
                    return;
                }
                pixelCache.Add(texture, pixels);
            }

            ppu = sprite.pixelsPerUnit;
            Rect rect = sprite.rect;
            int pivotX = Mathf.RoundToInt(sprite.pivot.x);
            int pivotY = Mathf.RoundToInt(sprite.pivot.y);

            for (int y = 0; y < (int)rect.height; y++)
            {
                int rowStart = ((int)rect.y + y) * texture.width + (int)rect.x;
                for (int x = 0; x < (int)rect.width; x++)
                {
                    if (pixels[rowStart + x].a == 0) continue;

                    int key = y - pivotY;
                    int px = x - pivotX;
                    spans[key] = spans.TryGetValue(key, out Vector2Int span)
                        ? new Vector2Int(Mathf.Min(span.x, px), Mathf.Max(span.y, px))
                        : new Vector2Int(px, px);
                }
            }
        }

        if (ppu <= 0f) return;

        var rows = new Vector3Int[spans.Count];
        int i = 0;
        foreach (var pair in spans) rows[i++] = new Vector3Int(pair.Key, pair.Value.x, pair.Value.y);

        waterFootprintRows = rows;
        waterFootprintPixelsPerUnit = ppu;
    }

    private static Color32[] ReadSourcePixels(Texture2D _texture)
    {
        string path = UnityEditor.AssetDatabase.GetAssetPath(_texture);
        if (string.IsNullOrEmpty(path) || false == path.EndsWith(".png", System.StringComparison.OrdinalIgnoreCase)) return null;
        if (false == System.IO.File.Exists(path)) return null;

        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            if (false == source.LoadImage(System.IO.File.ReadAllBytes(path))) return null;
            if (source.width != _texture.width || source.height != _texture.height) return null;
            return source.GetPixels32();
        }
        finally
        {
            DestroyImmediate(source);
        }
    }
#endif

    private void Awake()
    {
        if (customSortable == null)
            customSortable = GetComponent<CustomSortable>();

        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        if (customSortable != null)
        {
            customSortable.Initialize(transform);
            customSortable.AddSpriteRenderer(targetRenderer);
            customSortable.ManualLateUpdate();
        }

        MaterialPropertyBlock mpb = SharedMpb;
        targetRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(HDRIntensityID, hdrIntensity);
        targetRenderer.SetPropertyBlock(mpb);
    }

    public void SetSortingOrder()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }

    private void OnEnable()
    {
        routine = StartCoroutine(AnimationLoop());
    }

    private void OnDisable()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }

        if (targetRenderer != null)
        {
            // targetRenderer.enabled = true; // 컬링 복귀 시 원치 않는 표시 방지
        }

        SetFrame(0);
    }

    private IEnumerator AnimationLoop()
    {
        bool startHidden = (playbackMode == PlaybackMode.PlayPatternThenHideWithWait) ||
                           (playbackMode == PlaybackMode.RandomPatternWithWait && hideBetweenPatterns);

        SetVisible(!startHidden);
        SetFrame(0);

        if (randomizeInitialWait && playbackMode != PlaybackMode.LoopPattern)
        {
            yield return new WaitForSeconds(GetRandomWait());
        }

        while (true)
        {
            SetVisible(true);
            FramePattern pattern = GetNextPattern();
            yield return PlayPattern(pattern);

            switch (playbackMode)
            {
                case PlaybackMode.LoopPattern:
                    break;

                case PlaybackMode.RandomPatternWithWait:
                    SetVisible(!hideBetweenPatterns);
                    yield return new WaitForSeconds(GetRandomWait());
                    SetVisible(true);
                    break;

                case PlaybackMode.PlayPatternThenHideWithWait:
                    SetVisible(false);
                    yield return new WaitForSeconds(GetRandomWait());
                    SetVisible(true);
                    break;
            }
        }
    }

    private FramePattern GetNextPattern()
    {
        if (patterns == null || patterns.Length == 0)
        {
            // 폴백 패턴 문자열은 고정이므로 한 번만 파싱한다.
            if (fallbackFramePattern.frameIndices == null)
            {
                fallbackFramePattern.name = "Fallback";
                fallbackFramePattern.frameIndices = ParsePattern(fallbackPattern);
            }
            return fallbackFramePattern;
        }

        if (playbackMode == PlaybackMode.RandomPatternWithWait)
        {
            return patterns[Random.Range(0, patterns.Length)];
        }

        return patterns[0];
    }

    private int[] ParsePattern(string patternText)
    {
        if (string.IsNullOrWhiteSpace(patternText))
        {
            return new[] { 0 };
        }

        string[] tokens = patternText.Split(',');
        int[] result = new int[tokens.Length];

        for (int i = 0; i < tokens.Length; i++)
        {
            if (!int.TryParse(tokens[i].Trim(), out result[i]))
            {
                result[i] = 0;
            }
        }

        return result;
    }

    private IEnumerator PlayPattern(FramePattern pattern)
    {
        if (pattern == null || pattern.frameIndices == null || pattern.frameIndices.Length == 0)
        {
            yield return null;
            yield break;
        }

        if (frameWait == null || frameWaitDuration != frameDuration)
        {
            frameWait = new WaitForSeconds(frameDuration);
            frameWaitDuration = frameDuration;
        }

        for (int i = 0; i < pattern.frameIndices.Length; i++)
        {
            SetFrame(pattern.frameIndices[i]);
            yield return frameWait;
        }
    }

    private float GetRandomWait()
    {
        float min = Mathf.Min(waitMin, waitMax);
        float max = Mathf.Max(waitMin, waitMax);
        return Random.Range(min, max);
    }

    private void SetFrame(int index)
    {
        if (targetRenderer == null || frames == null || index < 0 || index >= frames.Length || frames[index] == null)
        {
            return;
        }

        targetRenderer.sprite = frames[index];
    }

    private void SetVisible(bool isVisible)
    {
        if (targetRenderer != null)
        {
            targetRenderer.enabled = isVisible;
        }
    }
}
