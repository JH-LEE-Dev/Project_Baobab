using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 작은 숫자. 버프 HUD가 쓰는 숫자 글리프(HUD_BuffNumberFont: SmallCurrencyNum 시트 8px 높이, 흰 획 + 검은 외곽선)로
/// "현재 / 목표"(예: 12/40) 또는 한 숫자를 RawImage 하나에 합성한다. 게이지 바 안에서 한글 글자(12pt)가 너무 커서 아이콘 + 이 숫자로 바꿨다.
/// 슬래시는 시트에 없어서 같은 규칙(흰 획 + 검은 외곽선 1px, 흰 획은 덮지 않음)으로 직접 만든다. 최대 999까지 그린다(버프 숫자 글리프의 자리수 제한).
///
/// [연출] 픽셀 규칙(24Hz 틱, 정수 픽셀, 스케일 1, 반투명 없음, unscaled 시간)을 따른다. 숫자는 게이지 바 안에 있으므로 위아래로 움직이지 않는다(바 밖으로 나가 보인다). 시간 상수는 아래에 모았다.
///   등장 (4틱)  첫 값이 들어오거나 HUD가 다시 켜질 때. 왼쪽에서 오른쪽으로 25 / 50 / 75 / 100% 폭으로 드러난다.
///   변경        값이 바뀔 때. 연출 없이 새 값으로 바로 바뀐다.
///   퇴장 (4틱)  PlayExit. 오른쪽에서 왼쪽으로 75 / 50 / 25 / 0% 폭으로 사라진다. 끝나면 이미지를 끈다(흰 네모가 보이지 않는다).
/// 값이 들어오기 전 · 퇴장이 끝난 뒤 · 오브젝트가 꺼져 있는 동안에는 이미지가 항상 꺼져 있다. 오브젝트가 다시 켜지면 마지막 값으로 등장 연출부터 시작한다.
/// bGridSpacing이면(로그라이크 전투 HUD) 글자를 4px 격자(글자 폭 5, 윤곽 1px 겹침)에 고정해 놓고 슬래시도 같은 격자에 5x7로 그린다(HUD 시안의 숫자 규격, 전체 폭 = 4 x 글자 수 + 1).
/// 위치는 정수 픽셀이다: 폭이 홀수여도 반 칸 어긋나지 않게 pivot을 왼쪽에 두고 폭의 절반(내림)만큼 앞으로 민다(가운데 정렬). bLeftAlign이면 앵커에서 오른쪽으로 시작하고, bRightAlign이면 앵커가 숫자의 오른쪽 끝이다(자릿수가 늘면 왼쪽으로 자란다).
/// </summary>
public class UI_RoguelikeIconNumber : MonoBehaviour
{
    /// <summary>퇴장 연출 길이(초). 소유 HUD가 이 시간 뒤에 오브젝트를 끈다.</summary>
    public const float ExitSeconds = 4f / 24f;

    private const int TexWidth = 64;
    private const int Height = HUD_BuffNumberFont.GlyphHeight;
    private const int SlashCellWidth = 6;
    // 격자 모드: 글자 하나가 차지하는 칸 폭(전진 4px)과 글리프 폭(윤곽 포함 5px).
    private const int GridAdvance = 4;
    private const int GridGlyphWidth = 5;
    private static readonly int[] GridSlashX = { 3, 3, 2, 2, 1 };
    private static readonly int[] GridSlashRow = { 2, 3, 4, 5, 6 };
    private const float TicksPerSecond = 24f;
    private const int AppearTicks = 4;
    private const int ExitTicks = 4;
    private const float MaxStepSeconds = 1f / 20f;

    private enum EPhase
    {
        Hidden,
        Appearing,
        Shown,
        Exiting,
    }

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private RawImage image;
    [SerializeField, Tooltip("숫자 스프라이트. 인덱스 d가 숫자 d(버프 HUD와 같은 SmallCurrencyNum 시트: _0~_8이 1~9, _9가 0).")]
    private Sprite[] digitSprites = new Sprite[0];
    [SerializeField, Tooltip("켜면 앵커에서 오른쪽으로 시작한다(왼쪽 정렬). 끄면 앵커를 가운데로 놓는다.")]
    private bool bLeftAlign = false;
    [SerializeField, Tooltip("켜면 앵커가 숫자의 오른쪽 끝이다(오른쪽 정렬). 자릿수가 늘면 왼쪽으로 자란다. bLeftAlign이 켜져 있으면 그쪽이 먼저다.")]
    private bool bRightAlign = false;
    [SerializeField, Tooltip("켜면 글자를 4px 격자에 고정한다(로그라이크 전투 HUD 규격). 끄면 글자를 붙여 쓴다.")]
    private bool bGridSpacing = false;

    //상태 변수
    private HUD_BuffNumberFont font;
    private Texture2D texture;
    private Color32[] buffer;
    private Color32[] bufferA;
    private Color32[] bufferB;
    private readonly Color32[] slashCell = new Color32[SlashCellWidth * Height];
    private readonly Color32[] gridSlashCell = new Color32[GridGlyphWidth * Height];
    private bool bReady;
    private bool bHasValue;
    private bool bPair;
    private int shownA = int.MinValue;
    private int shownB = int.MinValue;
    private int contentWidth;
    private EPhase phase = EPhase.Hidden;
    private float phaseClock;
    private Color baseTint = Color.white;

    /// <summary>"_current/_target"를 그린다.</summary>
    public void SetPair(int _current, int _target)
    {
        if (true == bHasValue && true == bPair && _current == shownA && _target == shownB && EPhase.Hidden != phase && EPhase.Exiting != phase) return;

        bool bWasShown = true == bHasValue && EPhase.Hidden != phase && EPhase.Exiting != phase;

        shownA = _current;
        shownB = _target;
        bPair = true;
        bHasValue = true;
        Rebuild();

        if (false == bWasShown) StartPhase(EPhase.Appearing);
        else if (EPhase.Shown == phase) Apply(contentWidth);
    }

    /// <summary>한 숫자만 그린다(슬래시 없음).</summary>
    public void SetSingle(int _value)
    {
        if (true == bHasValue && false == bPair && _value == shownA && EPhase.Hidden != phase && EPhase.Exiting != phase) return;

        bool bWasShown = true == bHasValue && EPhase.Hidden != phase && EPhase.Exiting != phase;

        shownA = _value;
        shownB = int.MaxValue;
        bPair = false;
        bHasValue = true;
        Rebuild();

        if (false == bWasShown) StartPhase(EPhase.Appearing);
        else if (EPhase.Shown == phase) Apply(contentWidth);
    }

    /// <summary>글자 색 곱(흰 획만 물든다, 검은 외곽선은 그대로).</summary>
    public void SetTint(Color _color)
    {
        baseTint = _color;
        if (EPhase.Shown == phase && null != image) image.color = baseTint;
    }

    /// <summary>퇴장 연출을 시작한다. 끝나면 이미지가 꺼진다(소유 HUD는 ExitSeconds 뒤에 오브젝트를 끈다).</summary>
    public void PlayExit()
    {
        if (EPhase.Hidden == phase || EPhase.Exiting == phase) return;

        StartPhase(EPhase.Exiting);
    }

    /// <summary>퇴장 중이었거나 숨겨져 있었다면 마지막 값으로 등장 연출을 다시 시작한다(HUD가 다시 켜질 때).</summary>
    public void Restore()
    {
        if (false == bHasValue) return;
        if (EPhase.Hidden != phase && EPhase.Exiting != phase) return;

        StartPhase(EPhase.Appearing);
    }

    /// <summary>연출 없이 바로 숨기고 값을 잊는다(런 시작 · 종료 초기화).</summary>
    public void HideImmediate()
    {
        bHasValue = false;
        shownA = int.MinValue;
        shownB = int.MinValue;
        phase = EPhase.Hidden;
        if (null != image) image.enabled = false;
    }

    /// <summary>_dt초만큼 연출을 진행한다(Update가 부른다). 편집기 검증에서도 이 메서드로 시간대별 모습을 만들 수 있다.</summary>
    public void Step(float _dt)
    {
        if (EPhase.Hidden == phase || EPhase.Shown == phase) return;

        phaseClock += _dt;
        int tick = (int)(phaseClock * TicksPerSecond);

        switch (phase)
        {
            case EPhase.Appearing:
                if (AppearTicks <= tick) StartPhase(EPhase.Shown);
                else Apply(Mathf.CeilToInt(contentWidth * (tick + 1) / (float)AppearTicks));
                break;

            case EPhase.Exiting:
                if (ExitTicks <= tick) StartPhase(EPhase.Hidden);
                else Apply(Mathf.FloorToInt(contentWidth * (ExitTicks - 1 - tick) / (float)ExitTicks));
                break;
        }
    }

    // // 내부

    private void StartPhase(EPhase _phase)
    {
        phase = _phase;
        phaseClock = 0f;

        switch (_phase)
        {
            case EPhase.Hidden:
                if (null != image) image.enabled = false;
                break;

            case EPhase.Shown:
                Apply(contentWidth);
                break;

            case EPhase.Appearing:
                Apply(Mathf.CeilToInt(contentWidth / (float)AppearTicks));
                break;

            case EPhase.Exiting:
                Apply(Mathf.FloorToInt(contentWidth * (ExitTicks - 1) / (float)ExitTicks));
                break;
        }
    }

    /// <summary>드러난 폭(_revealPixels)을 이미지에 반영한다. 위치는 가운데 정렬 정수 칸에 고정이다.</summary>
    private void Apply(int _revealPixels)
    {
        if (null == image || false == bReady) return;

        int reveal = Mathf.Clamp(_revealPixels, 0, contentWidth);
        image.enabled = 0 < reveal;
        if (reveal <= 0) return;

        image.uvRect = new Rect(0f, 0f, reveal / (float)TexWidth, 1f);
        image.color = baseTint;

        RectTransform rect = image.rectTransform;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(reveal, Height);

        int anchorX = -(contentWidth / 2);
        if (true == bLeftAlign) anchorX = 0;
        else if (true == bRightAlign) anchorX = -contentWidth;

        rect.anchoredPosition = new Vector2(anchorX, rect.anchoredPosition.y);
    }

    private bool EnsureReady()
    {
        if (true == bReady) return true;
        if (null == image) return false;

        font = new HUD_BuffNumberFont();
        if (false == font.Build(digitSprites)) return false;

        texture = new Texture2D(TexWidth, Height, TextureFormat.RGBA32, false);
        texture.name = "UI_RoguelikeIconNumber";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        buffer = new Color32[TexWidth * Height];
        bufferA = new Color32[TexWidth * Height];
        bufferB = new Color32[TexWidth * Height];
        BuildSlash();
        BuildGridSlash();

        image.texture = texture;
        image.raycastTarget = false;
        image.enabled = false;

        bReady = true;
        return true;
    }

    /// <summary>슬래시 글리프: 흰 대각선(2칸씩 한 칸 이동) + 검은 외곽(8방향 이웃).</summary>
    private void BuildSlash()
    {
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 black = new Color32(0, 0, 0, 255);
        System.Array.Clear(slashCell, 0, slashCell.Length);

        for (int r = 1; r <= 6; r++)
        {
            int x = 1 + (r - 1) / 2;
            slashCell[r * SlashCellWidth + x] = white;
        }

        for (int r = 0; r < Height; r++)
        {
            for (int x = 0; x < SlashCellWidth; x++)
            {
                if (255 == slashCell[r * SlashCellWidth + x].a && 255 == slashCell[r * SlashCellWidth + x].r) continue;

                bool bNear = false;
                for (int dy = -1; dy <= 1 && false == bNear; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int rr = r + dy;
                        int xx = x + dx;
                        if (rr < 0 || Height <= rr || xx < 0 || SlashCellWidth <= xx) continue;
                        if (255 == slashCell[rr * SlashCellWidth + xx].r && 255 == slashCell[rr * SlashCellWidth + xx].a)
                        {
                            bNear = true;
                            break;
                        }
                    }
                }

                if (true == bNear) slashCell[r * SlashCellWidth + x] = black;
            }
        }
    }

    /// <summary>격자 모드 슬래시 5x7: 흰 심 (3,2)(3,3)(2,4)(2,5)(1,6)(8x8 시트 셀 기준 글리프 왼쪽 윤곽이 0열) + 그 주변 8방향 검은 윤곽. 글자와 같은 7행(시트 1~7행)이다.</summary>
    private void BuildGridSlash()
    {
        Color32 white = new Color32(255, 255, 255, 255);
        Color32 black = new Color32(0, 0, 0, 255);
        System.Array.Clear(gridSlashCell, 0, gridSlashCell.Length);

        // 시트 행(위가 0) -> 버퍼 행(아래가 0): Height - 1 - 행.
        for (int i = 0; i < GridSlashX.Length; i++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = GridSlashX[i] + dx;
                    int cr = Height - 1 - (GridSlashRow[i] + dy);
                    if (cx < 0 || GridGlyphWidth <= cx || cr < 0 || Height <= cr) continue;

                    gridSlashCell[cr * GridGlyphWidth + cx] = black;
                }
            }
        }

        for (int i = 0; i < GridSlashX.Length; i++) gridSlashCell[(Height - 1 - GridSlashRow[i]) * GridGlyphWidth + GridSlashX[i]] = white;
    }

    /// <summary>글자를 텍스처에 합성하고 폭(contentWidth)을 구한다. 이미지 표시는 연출(Apply)이 맡는다.</summary>
    private void Rebuild()
    {
        if (false == EnsureReady()) return;

        System.Array.Clear(buffer, 0, buffer.Length);

        contentWidth = true == bGridSpacing ? ComposeGrid() : ComposeTight();

        texture.SetPixelData(buffer, 0);
        texture.Apply(false);
    }

    /// <summary>글자를 붙여 쓴다(기존 방식).</summary>
    private int ComposeTight()
    {
        int widthA = font.Compose(shownA, false, bufferA, TexWidth);
        int x = 0;
        Blit(bufferA, widthA, x);
        x += widthA - 1;

        if (true == bPair)
        {
            Blit(slashCell, SlashCellWidth, x, SlashCellWidth);
            x += SlashCellWidth - 1;

            int widthB = font.Compose(shownB, false, bufferB, TexWidth);
            Blit(bufferB, widthB, x);
            x += widthB;
        }
        else
        {
            x += 1;
        }

        return Mathf.Clamp(x, 1, TexWidth);
    }

    /// <summary>글자를 4px 격자에 고정해 놓는다. 폭이 좁은 글자(1)는 글리프 칸 가운데에 놓인다. 전체 폭 = 4 x 칸 수 + 1.</summary>
    private int ComposeGrid()
    {
        int cell = PutGridNumber(shownA, 0);

        if (true == bPair)
        {
            Blit(gridSlashCell, GridGlyphWidth, cell * GridAdvance, GridGlyphWidth);
            cell++;
            cell = PutGridNumber(shownB, cell);
        }

        return Mathf.Clamp(cell * GridAdvance + 1, 1, TexWidth);
    }

    /// <summary>숫자(0~999)를 자리마다 격자 칸에 놓고 다음 칸 번호를 돌려준다.</summary>
    private int PutGridNumber(int _value, int _cell)
    {
        int v = Mathf.Clamp(_value, 0, 999);
        int cell = _cell;

        if (100 <= v) cell = PutGridDigit(v / 100, cell);
        if (10 <= v) cell = PutGridDigit((v / 10) % 10, cell);

        return PutGridDigit(v % 10, cell);
    }

    private int PutGridDigit(int _digit, int _cell)
    {
        int glyphWidth = font.Compose(_digit, false, bufferA, TexWidth);
        Blit(bufferA, glyphWidth, _cell * GridAdvance + (GridGlyphWidth - glyphWidth) / 2);
        return _cell + 1;
    }

    /// <summary>_src(폭 _srcWidth)를 buffer의 _ox 열부터 합성한다. 이미 흰 획이 있는 칸은 건드리지 않는다.</summary>
    private void Blit(Color32[] _src, int _srcWidth, int _ox, int _stride = TexWidth)
    {
        for (int y = 0; y < Height; y++)
        {
            for (int sx = 0; sx < _srcWidth; sx++)
            {
                int dx = _ox + sx;
                if (TexWidth <= dx) continue;

                Color32 p = _src[y * _stride + sx];
                if (0 == p.a) continue;

                Color32 cur = buffer[y * TexWidth + dx];
                if (0 < cur.a && 255 == cur.r) continue;

                buffer[y * TexWidth + dx] = p;
            }
        }
    }

    private void Update()
    {
        Step(Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds));
    }

    private void OnEnable()
    {
        // 오브젝트가 다시 켜지면(HUD 표시) 마지막 값으로 등장 연출부터 시작한다. 값이 없으면 아무것도 보이지 않는다.
        phase = EPhase.Hidden;
        if (null != image) image.enabled = false;
        if (true == bHasValue) StartPhase(EPhase.Appearing);
    }

    private void OnDisable()
    {
        phase = EPhase.Hidden;
        if (null != image) image.enabled = false;
    }

    private void OnDestroy()
    {
        if (null != texture) Destroy(texture);
    }
}
