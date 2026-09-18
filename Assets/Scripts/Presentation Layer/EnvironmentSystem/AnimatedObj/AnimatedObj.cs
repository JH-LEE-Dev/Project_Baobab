using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 스프라이트 목록을 일정 간격으로 넘기는 단순 프레임 애니메이터.
///
/// 기본 동작은 "전체 목록을 0 1 2 3 0 1 2 3... 으로 돌리기"이고, 타일맵 데코(풀/나비/물가)가
/// 그대로 쓴다. 여기에 두 가지가 선택 기능으로 얹혀 있다.
///   - 핑퐁: 구간 끝에서 되돌아온다(0 1 2 3 2 1 0 1 2 3...). 끝 프레임은 한 번만 나온다.
///   - 재생 구간: 목록 일부만 돌린다. 한 장짜리 시트에 여러 상태가 들어 있을 때 쓴다.
///     (용광로가 0~3은 대기, 4~8은 가동으로 나눠 쓰는 식)
///
/// 둘 다 기본값이 꺼짐/전체 구간이라, 쓰지 않는 쪽은 예전과 똑같이 동작한다.
/// </summary>
public class AnimatedObj : MonoBehaviour
{
    // // 외부 의존성
    [SerializeField] private float hdrIntensity = 1f; 
    [SerializeField] private List<Sprite> sprites;
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private float frameRate = 10f;

    [Tooltip("켜면 구간 끝에서 되돌아온다(0 1 2 3 2 1 0 ...). 끄면 끝에서 처음으로 점프한다(0 1 2 3 0 ...).")]
    [SerializeField] private bool bPingPong = false;

    // // 내부 의존성 및 상태 필드
    private static readonly int HDRIntensityID = Shader.PropertyToID("_HDRIntensity");
    private CustomSortable customSortable;
    private int currentFrameIndex;
    private float timer;
    private float frameDuration;

    // 재생 구간. Initialize()에서 목록 전체로 잡히고, SetFrameRange()로 일부만 돌릴 수 있다.
    private int frameStartIndex;
    private int frameCount;

    // 구간 안에서 몇 칸 넘어갔는지. 핑퐁은 이 값으로 삼각파를 만든다.
    // 한 주기(stepPeriod)로 감싸두므로 오래 돌아도 값이 넘치지 않는다.
    private int step;
    private int stepPeriod = 1;

    // // 퍼블릭 초기화 및 제어 메서드

    public void Initialize()
    {
        customSortable = GetComponent<CustomSortable>();
        if (customSortable != null)
        {
            customSortable.Initialize(transform);
            customSortable.AddSpriteRenderer(sr);
        }

        var mpb = new MaterialPropertyBlock();
        sr.GetPropertyBlock(mpb);
        mpb.SetFloat(HDRIntensityID, hdrIntensity);
        sr.SetPropertyBlock(mpb);

        frameDuration = frameRate > 0f ? 1f / frameRate : 0.1f;

        frameStartIndex = 0;
        frameCount = sprites != null ? sprites.Count : 0;
        stepPeriod = CalcStepPeriod();

        ResetAnimationToRandomFrame();
    }

    public void SetSortingOrder()
    {
        if (customSortable != null)
            customSortable.ManualLateUpdate();
    }

    /// <summary>
    /// 재생 구간을 바꾼다. 구간이 실제로 달라질 때만 첫 프레임부터 다시 시작하므로,
    /// 같은 구간으로 여러 번 불러도 애니메이션이 끊기지 않는다.
    /// </summary>
    public void SetFrameRange(int _startIndex, int _count)
    {
        if (sprites == null || sprites.Count == 0) return;

        int _start = Mathf.Clamp(_startIndex, 0, sprites.Count - 1);
        int _len = Mathf.Clamp(_count, 1, sprites.Count - _start);

        if (_start == frameStartIndex && _len == frameCount) return;

        frameStartIndex = _start;
        frameCount = _len;
        stepPeriod = CalcStepPeriod();

        step = 0;
        timer = 0f;
        ApplyFrame();
    }

    /// <summary>초당 프레임 수를 바꾼다. 지금 세고 있던 시간은 그대로 두고 다음 전환부터 적용된다.</summary>
    public void SetFrameRate(float _frameRate)
    {
        frameRate = _frameRate;
        frameDuration = frameRate > 0f ? 1f / frameRate : 0.1f;
    }

    /// <summary>핑퐁 재생을 켜고 끈다. 진행 위치가 주기를 벗어나지 않도록 구간 처음으로 되돌린다.</summary>
    public void SetPingPong(bool _bPingPong)
    {
        if (bPingPong == _bPingPong) return;

        bPingPong = _bPingPong;
        stepPeriod = CalcStepPeriod();

        step = 0;
        ApplyFrame();
    }

    public void ResetAnimationToRandomFrame()
    {
        if (sprites == null || sprites.Count == 0) return;

        step = Random.Range(0, stepPeriod);
        timer = Random.Range(0f, frameDuration);

        ApplyFrame();
    }

    // // 내부 메서드

    /// <summary>
    /// 한 바퀴가 몇 칸인지. 루프는 구간 길이 그대로이고, 핑퐁은 끝 프레임을 두 번 내보내지 않기 위해
    /// (길이 - 1) * 2 다. 예를 들어 4장짜리 구간은 0 1 2 3 2 1 로 6칸이다.
    /// </summary>
    private int CalcStepPeriod()
    {
        if (frameCount <= 1) return 1;

        return bPingPong ? (frameCount - 1) * 2 : frameCount;
    }

    private void ApplyFrame()
    {
        if (sr == null || sprites == null || sprites.Count == 0) return;

        int _offset = step;

        if (true == bPingPong && frameCount > 1)
        {
            int _period = (frameCount - 1) * 2;
            _offset = step < frameCount ? step : _period - step;
        }

        currentFrameIndex = frameStartIndex + Mathf.Clamp(_offset, 0, frameCount - 1);
        sr.sprite = sprites[currentFrameIndex];
    }

    // // 유니티 이벤트 함수

    private void Update()
    {
        if (sprites == null || frameCount <= 1) return;

        timer += Time.deltaTime;
        if (timer >= frameDuration)
        {
            timer -= frameDuration;
            step = (step + 1) % stepPeriod;
            ApplyFrame();
        }
    }
}
