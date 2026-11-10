using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 웨이브 줄. 좌측 하단 막대 묶음의 맨 위 줄이다(RoguelikeHUD_Polish_Plan.md 2장).
/// 깃발 아이콘(Flag, 16x16) + 작은 숫자(UI_RoguelikeIconNumber, 4px 격자)로 "12/30"을 그리고, 그 오른쪽에 30칸 진행 점(2x2, 3px 간격, 10칸마다 +2)을 둔다.
///   진행 점  지난 칸은 금색 2행(위 밝음 / 아래 어두움), 현재 칸은 흰색 2x3(위로 1px 더), 남은 칸은 어두운 갈색.
///   언리밋   깃발이 금색, 무한대 아이콘(Infinity) + 단계 번호(금색)로 바뀌고 진행 점은 10칸이다. 칸 = (언리밋 번호 - 1) % 10 + 1 (10웨이브마다 처음부터).
/// 글자 라벨은 쓰지 않는다. 아이콘 · 숫자는 프리팹에 있고 이 컴포넌트는 숫자와 진행 점, 깃발 모양, 튐 연출만 맡는다.
///
/// [웨이브가 바뀔 때] 24Hz 틱으로 위로 { +2, +3, +1, 0 }px 튀어 오르고(스텝당 3틱), 처음 6틱은 깃발이 흰 실루엣과 원래 모양으로 번갈아 번쩍인다.
///   정수 픽셀 이동이고, 시간은 unscaled라 ESC로 멈춰도 이어진다. 처음 표시될 때(HUD가 켜질 때)는 튀지 않는다. 현재 점은 다음 칸으로 옮겨 간다.
/// </summary>
public class UI_RoguelikeWaveLabel : MonoBehaviour
{
    private const int TotalWaves = 30;
    private const int UnlimitPipCount = 10;
    private const int PipTextureWidth = 93;
    private const int PipTextureHeight = 3;
    private const int PipStep = 3;
    private const int PipGroupGap = 2;
    private const int PipGroupSize = 10;
    // 숫자 끝에서 진행 점까지의 간격.
    private const int PipGap = 5;
    private const int GridAdvance = 4;
    private const float TicksPerSecond = 24f;
    private const int StepTicks = 3;
    // 숫자 잉크 시작 x(라벨 왼쪽 기준, 라벨은 캔버스 x 7). 깃발만 있을 때(캔버스 19)와 깃발 + 무한대 16x16 아이콘이 있을 때(캔버스 32).
    private const int NumberX = 12;
    private const int NumberXUnlimit = 25;
    private const int FlashTicks = 6;
    private static readonly int[] HopPattern = { 2, 3, 1, 0 };

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform labelRoot;
    [SerializeField] private UI_RoguelikeIconNumber iconNumber;
    [SerializeField, Tooltip("숫자 앵커. 언리밋이면 무한대 아이콘 뒤로 옮긴다.")]
    private RectTransform numberAnchor;
    [SerializeField] private Image flagImage;
    [SerializeField] private Image infinityImage;
    [SerializeField, Tooltip("일반 깃발(빨강) 스프라이트.")]
    private Sprite normalFlagSprite;
    [SerializeField, Tooltip("언리밋 깃발(금색) 스프라이트.")]
    private Sprite unlimitFlagSprite;
    [SerializeField, Tooltip("웨이브가 바뀔 때 번쩍이는 흰 실루엣 깃발 스프라이트.")]
    private Sprite flashFlagSprite;
    [SerializeField, Tooltip("진행 점을 그리는 RawImage(93x3). 위치는 숫자 폭에 맞춰 코드가 옮긴다.")]
    private RawImage pipSurface;

    //상태 변수
    private RoguelikeBarPainter pipPainter;
    private int wave;
    private bool bUnlimit;
    private int unlimitIndex;
    private bool bHasWave;
    private float clock;
    private float hopStartClock = -1f;
    // 지금 라벨에 얹혀 있는 튐 오프셋(px). 위치를 통째로 덮어쓰지 않고 차이만 더해 퇴장 이동과 겹쳐도 어긋나지 않는다.
    private int appliedHop;
    private float lastHopY;
    // 퇴장 연출(숫자가 사라지는 동안)이 끝난 뒤 오브젝트를 끈다.
    private bool bHiding;
    private float hideClock;

    /// <summary>웨이브 표기. 언리밋이면 _unlimitIndex를 무한대 아이콘 옆에 그린다. 같은 웨이브가 다시 오면 아무것도 하지 않는다.</summary>
    public void SetWave(int _waveNumber, bool _bUnlimit, int _unlimitIndex)
    {
        bool bChanged = false == bHasWave || wave != _waveNumber || bUnlimit != _bUnlimit || unlimitIndex != _unlimitIndex;
        if (false == bChanged) return;

        bool bFirst = false == bHasWave;
        wave = _waveNumber;
        bUnlimit = _bUnlimit;
        unlimitIndex = _unlimitIndex;
        bHasWave = true;

        Refresh();
        if (false == bFirst) PlayChanged();
    }

    /// <summary>켜면 숫자가 등장 연출로 다시 나타난다. 끄면 숫자가 퇴장 연출을 재생한 뒤 오브젝트가 꺼진다.</summary>
    public void SetVisible(bool _bVisible)
    {
        if (true == _bVisible)
        {
            bHiding = false;
            gameObject.SetActive(true);
            if (null != iconNumber) iconNumber.Restore();
            return;
        }

        if (false == gameObject.activeInHierarchy || null == iconNumber)
        {
            gameObject.SetActive(false);
            return;
        }

        iconNumber.PlayExit();
        bHiding = true;
        hideClock = 0f;
    }

    /// <summary>_dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 이 메서드로 시간대별 모습을 만들 수 있다.</summary>
    public void Step(float _dt)
    {
        clock += _dt;

        if (hopStartClock < 0f) return;

        int tick = (int)((clock - hopStartClock) * TicksPerSecond);
        int step = tick / StepTicks;

        if (HopPattern.Length <= step)
        {
            hopStartClock = -1f;
            ApplyHop(0, false);
            return;
        }

        ApplyHop(HopPattern[step], tick < FlashTicks && 0 == tick % 2);
    }

    // // 내부

    private void Refresh()
    {
        if (null != infinityImage) infinityImage.enabled = bUnlimit;

        int numberX = true == bUnlimit ? NumberXUnlimit : NumberX;
        if (null != numberAnchor) numberAnchor.anchoredPosition = new Vector2(numberX, numberAnchor.anchoredPosition.y);

        int digits = 1;

        if (null != iconNumber)
        {
            if (true == bUnlimit)
            {
                iconNumber.SetSingle(unlimitIndex);
                iconNumber.SetTint(RoguelikeHudColors.NumberUnlimit);
                digits = CountDigits(unlimitIndex);
            }
            else
            {
                iconNumber.SetPair(wave, TotalWaves);
                iconNumber.SetTint(Color.white);
                digits = CountDigits(wave) + 1 + CountDigits(TotalWaves);
            }
        }

        // 진행 점은 숫자 오른쪽 끝에서 5px 뒤(숫자 폭 = 4 x 글자 수 + 1).
        if (null != pipSurface)
        {
            RectTransform pipRect = pipSurface.rectTransform;
            pipRect.anchoredPosition = new Vector2(numberX + GridAdvance * digits + 1 + PipGap, pipRect.anchoredPosition.y);
        }

        PaintPips();

        if (null != flagImage) flagImage.sprite = true == bUnlimit ? unlimitFlagSprite : normalFlagSprite;
    }

    /// <summary>진행 점 30칸(언리밋이면 10칸)을 다시 그린다.</summary>
    private void PaintPips()
    {
        if (null == pipSurface) return;

        EnsurePipPainter();

        int total = true == bUnlimit ? UnlimitPipCount : TotalWaves;
        int current = true == bUnlimit ? (unlimitIndex - 1) % UnlimitPipCount + 1 : wave;

        pipPainter.Clear();

        int gx = 0;
        for (int i = 0; i < total; i++)
        {
            if (0 < i && 0 == i % PipGroupSize) gx += PipGroupGap;

            if (i + 1 < current)
            {
                pipPainter.Fill(gx, 1, gx + 2, 2, RoguelikeHudColors.PipPassedTop);
                pipPainter.Fill(gx, 2, gx + 2, 3, RoguelikeHudColors.PipPassedBottom);
            }
            else if (i + 1 == current)
            {
                pipPainter.Fill(gx, 0, gx + 2, 3, RoguelikeHudColors.White);
            }
            else
            {
                pipPainter.Fill(gx, 1, gx + 2, 3, RoguelikeHudColors.PipRemain);
            }

            gx += PipStep;
        }

        pipPainter.Apply();
    }

    private void EnsurePipPainter()
    {
        if (null != pipPainter || null == pipSurface) return;

        pipPainter = new RoguelikeBarPainter(pipSurface, PipTextureWidth, PipTextureHeight, "UI_RoguelikeWavePips");
    }

    /// <summary>폰트(HUD_BuffNumberFont)와 같은 규칙: 0~999, 최대 3자리.</summary>
    private static int CountDigits(int _value)
    {
        int v = Mathf.Clamp(_value, 0, 999);
        if (100 <= v) return 3;
        if (10 <= v) return 2;
        return 1;
    }

    private void PlayChanged()
    {
        hopStartClock = clock;
        Step(0f);
    }

    private void ApplyHop(int _offset, bool _bFlash)
    {
        if (null != labelRoot)
        {
            Vector2 position = labelRoot.anchoredPosition;

            // 마지막으로 쓴 y가 아니면 퇴장 · 복귀 연출이 위치를 덮어쓴 것이라 얹어 둔 튐은 이미 사라졌다.
            if (0 != appliedHop && false == Mathf.Approximately(position.y, lastHopY)) appliedHop = 0;

            if (appliedHop != _offset)
            {
                position.y += _offset - appliedHop;
                labelRoot.anchoredPosition = position;
                appliedHop = _offset;
            }

            lastHopY = position.y;
        }

        if (null != flagImage) flagImage.sprite = true == _bFlash ? flashFlagSprite : (true == bUnlimit ? unlimitFlagSprite : normalFlagSprite);
    }

    private void Awake()
    {
        // 첫 프레임에 텍스처 없는 RawImage(흰 네모)가 보이지 않도록 진행 점 텍스처를 미리 만든다(값이 오면 그린다).
        EnsurePipPainter();
    }

    private void Update()
    {
        if (true == bHiding)
        {
            hideClock += Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);
            if (UI_RoguelikeIconNumber.ExitSeconds + 0.02f <= hideClock)
            {
                bHiding = false;
                gameObject.SetActive(false);
            }
        }

        Step(Mathf.Min(Time.unscaledDeltaTime, 1f / 20f));
    }

    private void OnDisable()
    {
        bHiding = false;
        hopStartClock = -1f;
        ApplyHop(0, false);
    }

    private void OnDestroy()
    {
        if (null != pipPainter) pipPainter.Dispose();
    }
}
