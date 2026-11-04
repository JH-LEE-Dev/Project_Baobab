using UnityEngine;

/// <summary>
/// 엔딩 소풍 장면의 캐릭터·강아지 그림을 일정 속도로 돌린다. 게임플레이 로직(이동·입력·상태)을 붙이지 않고
/// 본편 애니메이션 프레임만 그대로 재생하기 위한 것이다.
/// 깜빡임 렌더러가 있으면 본편 얼굴(CharacterVisualComponent.UpdateBlink)처럼 2~5초마다 1~2번 눈을 감는다.
/// 깜빡임 그림은 본편 CharacterAnimator처럼 지금 얼굴 프레임과 같은 순번을 쓴다.
/// </summary>
public class EndingSpriteLoop : MonoBehaviour
{
    // 본편 CharacterVisualComponent와 같은 값.
    private const float BlinkDuration = 0.15f;
    private const float BlinkGapDuration = 0.05f;

    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float framesPerSecond = 5f;

    [Header("눈 깜빡임 (선택)")]
    [SerializeField] private SpriteRenderer blinkRenderer;
    [SerializeField] private Sprite[] blinkFrames;
    [SerializeField] private float blinkIntervalMin = 2f;
    [SerializeField] private float blinkIntervalMax = 5f;

    private float timer;
    private int frameIndex;
    private float blinkTimer;
    private float nextBlinkInterval;
    private int blinkCount = 1;

    private void OnEnable()
    {
        timer = 0f;
        frameIndex = 0;
        blinkTimer = 0f;
        nextBlinkInterval = Random.Range(blinkIntervalMin, blinkIntervalMax);
        blinkCount = 1;
        ApplyFrame(false);
    }

    private void Update()
    {
        if (null != frames && frames.Length > 1)
        {
            timer += Time.deltaTime;
            float duration = 1f / Mathf.Max(0.01f, framesPerSecond);
            while (timer >= duration)
            {
                timer -= duration;
                frameIndex = (frameIndex + 1) % frames.Length;
            }
        }

        ApplyFrame(UpdateBlink());
    }

    // 본편 UpdateBlink와 같은 순서: 감기 → (2회면) 잠깐 뜨기 → 다시 감기 → 끝.
    private bool UpdateBlink()
    {
        if (null == blinkRenderer || null == blinkFrames || 0 == blinkFrames.Length) return false;

        blinkTimer += Time.deltaTime;
        if (blinkTimer < nextBlinkInterval) return false;

        float t = blinkTimer - nextBlinkInterval;
        if (t < BlinkDuration) return true;
        if (2 == blinkCount && t < BlinkDuration + BlinkGapDuration) return false;
        if (2 == blinkCount && t < BlinkDuration * 2f + BlinkGapDuration) return true;

        blinkTimer = 0f;
        nextBlinkInterval = Random.Range(blinkIntervalMin, blinkIntervalMax);
        blinkCount = Random.Range(1, 3);
        return false;
    }

    private void ApplyFrame(bool _bBlink)
    {
        if (null != targetRenderer && null != frames && frames.Length > 0)
        {
            targetRenderer.sprite = frames[frameIndex % frames.Length];
        }

        if (null == blinkRenderer) return;

        bool bHasBlink = null != blinkFrames && blinkFrames.Length > 0;
        bool bBlink = _bBlink && bHasBlink;
        blinkRenderer.enabled = bBlink;
        if (bBlink) blinkRenderer.sprite = blinkFrames[frameIndex % blinkFrames.Length];
        if (null != targetRenderer && bHasBlink) targetRenderer.enabled = false == bBlink;
    }
}
