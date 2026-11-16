using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class HomeWaterGrassSway : MonoBehaviour
{
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite[] frames = new Sprite[3];
    [SerializeField, Min(0.01f)] private float frameDuration = 0.333f;
    [SerializeField, Min(0f)] private float waitMin = 0.5f;
    [SerializeField, Min(0f)] private float waitMax = 1f;
    [SerializeField] private bool randomizeInitialWait = true;

    private CustomSortable customSortable;

    private static readonly int[][] Patterns =
    {
        new[] { 0, 1, 2, 1, 0 },
        new[] { 0, 1, 0 },
        new[] { 0, 1, 2, 1, 2, 1, 0 },
        new[] { 0, 1, 2, 1, 0, 1, 0 }
    };

    private Coroutine swayRoutine;
    private WaitForSeconds frameWait;
    private float frameWaitDuration = -1f;

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

    private void OnEnable()
    {
        swayRoutine = StartCoroutine(SwayLoop());
    }

    private void OnDisable()
    {
        if (swayRoutine != null)
        {
            StopCoroutine(swayRoutine);
            swayRoutine = null;
        }

        SetFrame(0);
    }

    private IEnumerator SwayLoop()
    {
        SetFrame(0);

        if (randomizeInitialWait)
        {
            yield return new WaitForSeconds(GetRandomWait());
        }

        while (true)
        {
            int[] pattern = Patterns[Random.Range(0, Patterns.Length)];

            // 프레임 간격은 런타임에 바뀌지 않으므로 대기 객체를 재사용한다(인스턴스당 코루틴 1개라 안전, DecoSpritePatternAnimator와 같은 방식).
            if (frameWait == null || frameWaitDuration != frameDuration)
            {
                frameWait = new WaitForSeconds(frameDuration);
                frameWaitDuration = frameDuration;
            }

            for (int i = 0; i < pattern.Length; i++)
            {
                SetFrame(pattern[i]);
                yield return frameWait;
            }

            yield return new WaitForSeconds(GetRandomWait());
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
}
