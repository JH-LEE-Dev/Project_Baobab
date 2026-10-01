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
