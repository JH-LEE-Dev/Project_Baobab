using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class StunVisualComponent : MonoBehaviour
{
    // 내부 의존성
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float frameRate = 10f;

    private Coroutine playRoutine;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Play()
    {
        if (frames == null || frames.Length == 0) return;

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        // 비활성 상태에서는 StartCoroutine이 실패하므로, 반드시 활성화를 먼저 해야 한다.
        gameObject.SetActive(true);
        playRoutine = StartCoroutine(PlayRoutine());
    }

    public void Stop()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        gameObject.SetActive(false);
    }

    private IEnumerator PlayRoutine()
    {
        float frameDuration = frameRate > 0f ? 1f / frameRate : 0.1f;
        int index = 0;

        while (true)
        {
            spriteRenderer.sprite = frames[index];
            index = (index + 1) % frames.Length;
            yield return new WaitForSeconds(frameDuration);
        }
    }
}
