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

    private Coroutine routine;
    private readonly FramePattern fallbackFramePattern = new FramePattern();

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
            fallbackFramePattern.name = "Fallback";
            fallbackFramePattern.frameIndices = ParsePattern(fallbackPattern);
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

        for (int i = 0; i < pattern.frameIndices.Length; i++)
        {
            SetFrame(pattern.frameIndices[i]);
            yield return new WaitForSeconds(frameDuration);
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
