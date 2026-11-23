using System;
using UnityEngine;

/// <summary>
/// 월드 HUD 재색 시트(Assets/Graphics/Roguelike/hud/world/, make_world_hud_sprites.py가 만든다). 페인터가 픽셀을 읽으므로 Read/Write가 켜져 있어야 한다.
/// 큰 숫자 시트 = 10x15 칸 12개(0~9 사용), 작은 숫자 시트 = 7x9 칸 14개(0~9 사용). 아이콘 · 슬래시는 잉크로 자른 크기 그대로다.
/// </summary>
[Serializable]
public sealed class RoguelikeWorldHudSheets
{
    public Texture2D bigDigits;
    public Texture2D bigDigitsWarn;
    public Texture2D bigDigitsAmber;
    public Texture2D bigDigitsGold;
    public Texture2D smallDigits;
    public Texture2D slash;
    public Texture2D iconTree;
    public Texture2D iconBolt;
    public Texture2D iconAxe;
    public Texture2D iconFlag;
    public Texture2D iconDash;
    public Texture2D iconInfinity;
}

/// <summary>대쉬 칸 한 개의 표시 상태. bFull이면 가득(DASH 3톤 + 흰 점 2px), 아니면 왼쪽부터 ratio만큼 충전 중 색.</summary>
public readonly struct RoguelikeWorldHudDashCell
{
    public readonly float ratio;
    public readonly bool bFull;

    public RoguelikeWorldHudDashCell(float _ratio, bool _bFull)
    {
        ratio = _ratio;
        bFull = _bFull;
    }
}

/// <summary>큰 숫자 색: 기본(흰) / 경고(빨강, HOT) / 호박(AMB, 나무 · 처치 튐) / 금색(목표 달성).</summary>
public enum RoguelikeWorldHudDigitStyle
{
    White,
    Warn,
    Amber,
    Gold,
}

/// <summary>
/// 임계치 띠 연출 인자(WorldHUD_Plan.md 10-4, world_hud_v7_motion.py frame()).
/// [revealLo, revealHi) = 보이는 A 범위(등장 · 퇴장), revealHi가 A1보다 작으면 끝 6값이 흰 머리, flashMask = 새로 켜진 칸(비트 = 칸 번호, 흰색),
/// bSweep = 돌파 훑기(A 16값 창, 앞 6값 흰색 + 나머지 HOT 0), bRimFlash = 초과 토막 테를 흰 테로, bFillFlash = 초과 토막 채움도 (WHITE, HOT0, HOT1)로(v6 pulse 그림).
/// </summary>
public readonly struct RoguelikeWorldHudThresholdFx
{
    public readonly int revealLo;
    public readonly int revealHi;
    public readonly ulong flashMask;
    public readonly bool bSweep;
    public readonly double sweepA;
    public readonly bool bRimFlash;
    public readonly bool bFillFlash;

    public RoguelikeWorldHudThresholdFx(int _revealLo, int _revealHi, ulong _flashMask, bool _bSweep, double _sweepA, bool _bRimFlash, bool _bFillFlash)
    {
        revealLo = _revealLo;
        revealHi = _revealHi;
        flashMask = _flashMask;
        bSweep = _bSweep;
        sweepA = _sweepA;
        bRimFlash = _bRimFlash;
        bFillFlash = _bFillFlash;
    }
}

/// <summary>
/// 월드 HUD 1배 픽셀 페인터(WorldHUD_Plan.md 2-1). 순수 C# 클래스이고 world_hud_v6.py / world_hud_v6_extras.py의 그리기 규칙을 1:1로 옮겼다.
///   띠(threshold_band / stamina_band + lift), 숫자 명패(number_label + with_shadow), 대쉬 칸(dash_row), 언리밋 표시(unlimit_label).
/// 요소마다 Color32[] 버퍼 하나가 있고(Unity 텍스처 순서 = 아래에서 위 행), 버퍼의 화면 위치는 RoguelikeWorldHudGeometry의 사각형이다.
/// 버퍼 · 픽셀 목록은 생성자에서 한 번만 만들고 Paint 호출은 할당이 없다(숫자는 자릿수 분해로 글리프를 복사한다).
/// 띠 픽셀 목록: 띠 영역(A0 &lt;= A &lt; A1, B0 &lt;= B &lt; B1)에 드는 픽셀을 한 번 모아 두고 그 목록만 다시 칠한다. 임계치 · 스태미너 띠가 같은 목록을 쓴다(스태미너는 x 반전).
/// </summary>
public sealed class RoguelikeWorldHudPainter
{
    private const int BigGlyphWidth = 10;
    private const int BigGlyphHeight = 15;
    private const int BigGlyphCount = 12;
    private const int SmallGlyphWidth = 7;
    private const int SmallGlyphHeight = 9;
    private const int SmallGlyphCount = 14;
    private const int MaxNumber = 999;
    // 아이콘 - 큰 숫자 - 슬래시 - 분모 사이 간격
    private const int IconGap = 2;
    private const int SlashGapAfter = 1;

    // 대쉬 칸: 안쪽 12x5, 칸 사이 칸막이 1px, 트레이 테 · 윤곽 2px씩
    private const int DashCellInnerWidth = 12;
    private const int DashCellInnerHeight = 5;
    private const int DashSeparator = 1;
    private const int DashRowHeight = DashCellInnerHeight + 4;
    private const int DashThickness = 2;
    private const int DashTempWidth = 48;
    private const int DashTempHeight = DashRowHeight + DashThickness;
    private const int DashRowCellMax = 2;

    private const int ShadowPassCount = 4;
    // 등장 · 퇴장 때 그려지는 끝 머리(흰 줄) 폭(A값)과 돌파 훑기 창 폭 / 흰 앞머리 폭(A값)
    private const int RevealHeadWidth = 6;
    private const double SweepWindow = 16d;
    private const double SweepHeadWidth = 6d;
    private const int StaminaDefaultHeadWidth = 2;

    // 글리프 · 아이콘 한 장(위에서 아래 행 순서)
    private sealed class Bitmap
    {
        public readonly int width;
        public readonly int height;
        public readonly Color32[] pixels;

        public Bitmap(int _width, int _height)
        {
            width = _width;
            height = _height;
            pixels = new Color32[_width * _height];
        }
    }

    //내부 의존성
    private readonly Bitmap[] bigGlyphs = new Bitmap[BigGlyphCount];
    private readonly Bitmap[] bigWarnGlyphs = new Bitmap[BigGlyphCount];
    private readonly Bitmap[] bigAmberGlyphs = new Bitmap[BigGlyphCount];
    private readonly Bitmap[] bigGoldGlyphs = new Bitmap[BigGlyphCount];
    private readonly Bitmap[] smallGlyphs = new Bitmap[SmallGlyphCount];
    private readonly Bitmap slash;
    private readonly Bitmap iconTree;
    private readonly Bitmap iconBolt;
    private readonly Bitmap iconAxe;
    private readonly Bitmap iconFlag;
    private readonly Bitmap iconDash;
    private readonly Bitmap iconInfinity;

    // 요소별 출력 버퍼(아래에서 위 행 순서, 크기 = Geometry 사각형)
    private readonly Color32[] thresholdBandBuffer;
    private readonly Color32[] staminaBandBuffer;
    private readonly Color32[] thresholdLabelBuffer;
    private readonly Color32[] staminaLabelBuffer;
    private readonly Color32[] waveLabelBuffer;
    private readonly Color32[] killLabelBuffer;

    // 작업 버퍼(위에서 아래 행 순서)
    private readonly Color32[] labelTemp = new Color32[RoguelikeWorldHudGeometry.LabelMaxWidth * RoguelikeWorldHudGeometry.LabelBodyHeight];
    private readonly Color32[] dashTemp = new Color32[DashTempWidth * DashTempHeight];

    // 띠 픽셀 목록
    private readonly int bandCount;
    private readonly int[] bandX;
    private readonly int[] bandY;
    private readonly int[] bandA;
    private readonly int[] bandB;
    private readonly Color32[] bandColor;
    private readonly bool[] bandOn;

    public Color32[] ThresholdBandBuffer => thresholdBandBuffer;
    public Color32[] StaminaBandBuffer => staminaBandBuffer;
    public Color32[] ThresholdLabelBuffer => thresholdLabelBuffer;
    public Color32[] StaminaLabelBuffer => staminaLabelBuffer;
    public Color32[] WaveLabelBuffer => waveLabelBuffer;
    public Color32[] KillLabelBuffer => killLabelBuffer;

    public RoguelikeWorldHudPainter(RoguelikeWorldHudSheets _sheets)
    {
        CutGlyphs(_sheets.bigDigits, bigGlyphs, BigGlyphWidth, BigGlyphHeight);
        CutGlyphs(_sheets.bigDigitsWarn, bigWarnGlyphs, BigGlyphWidth, BigGlyphHeight);
        CutGlyphs(_sheets.bigDigitsAmber, bigAmberGlyphs, BigGlyphWidth, BigGlyphHeight);
        CutGlyphs(_sheets.bigDigitsGold, bigGoldGlyphs, BigGlyphWidth, BigGlyphHeight);
        CutGlyphs(_sheets.smallDigits, smallGlyphs, SmallGlyphWidth, SmallGlyphHeight);
        slash = ReadBitmap(_sheets.slash);
        iconTree = ReadBitmap(_sheets.iconTree);
        iconBolt = ReadBitmap(_sheets.iconBolt);
        iconAxe = ReadBitmap(_sheets.iconAxe);
        iconFlag = ReadBitmap(_sheets.iconFlag);
        iconDash = ReadBitmap(_sheets.iconDash);
        iconInfinity = ReadBitmap(_sheets.iconInfinity);

        thresholdBandBuffer = new Color32[RoguelikeWorldHudGeometry.ThresholdBandRect.width * RoguelikeWorldHudGeometry.ThresholdBandRect.height];
        staminaBandBuffer = new Color32[RoguelikeWorldHudGeometry.StaminaBandRect.width * RoguelikeWorldHudGeometry.StaminaBandRect.height];
        thresholdLabelBuffer = new Color32[RoguelikeWorldHudGeometry.ThresholdLabelRect.width * RoguelikeWorldHudGeometry.ThresholdLabelRect.height];
        staminaLabelBuffer = new Color32[RoguelikeWorldHudGeometry.StaminaLabelRect.width * RoguelikeWorldHudGeometry.StaminaLabelRect.height];
        waveLabelBuffer = new Color32[RoguelikeWorldHudGeometry.WaveLabelRect.width * RoguelikeWorldHudGeometry.WaveLabelRect.height];
        killLabelBuffer = new Color32[RoguelikeWorldHudGeometry.KillLabelRect.width * RoguelikeWorldHudGeometry.KillLabelRect.height];

        // 띠 픽셀 목록: 임계치 띠 사각형 안에서 띠 영역에 드는 픽셀만 모은다.
        RoguelikeWorldHudRect rect = RoguelikeWorldHudGeometry.ThresholdBandRect;
        int count = 0;
        for (int sy = rect.y; rect.y + rect.height > sy; sy++)
        {
            for (int sx = rect.x; rect.x + rect.width > sx; sx++)
            {
                if (true == IsBandPixel(sx, sy)) count++;
            }
        }

        bandCount = count;
        bandX = new int[count];
        bandY = new int[count];
        bandA = new int[count];
        bandB = new int[count];
        bandColor = new Color32[count];
        bandOn = new bool[count];

        int n = 0;
        for (int sy = rect.y; rect.y + rect.height > sy; sy++)
        {
            for (int sx = rect.x; rect.x + rect.width > sx; sx++)
            {
                if (false == IsBandPixel(sx, sy)) continue;

                bandX[n] = sx;
                bandY[n] = sy;
                bandA[n] = sx - 2 * sy;
                bandB[n] = sx + 2 * sy;
                n++;
            }
        }
    }

    /// <summary>스태미너 비율 -> 채움 길이(A값 0 ~ StaminaInner). 파이썬 round와 같은 은행가 반올림(짝수로).</summary>
    public static int ToStaminaPixels(double _value, double _max)
    {
        if (0d >= _max) return 0;

        return Mathf.Clamp((int)Math.Round(RoguelikeWorldHudGeometry.StaminaInner * _value / _max), 0, RoguelikeWorldHudGeometry.StaminaInner);
    }

    /// <summary>잔상 비율 -> 길이. 위와 같은 반올림.</summary>
    public static int ToStaminaPixels(double _ratio)
    {
        return Mathf.Clamp((int)Math.Round(RoguelikeWorldHudGeometry.StaminaInner * _ratio), 0, RoguelikeWorldHudGeometry.StaminaInner);
    }

    // ------------------------------------------------------------------ 띠

    /// <summary>
    /// 임계치 띠(좌상단, T3 분리 띠). 토막 1 = 칸 0 ~ T/2 - 1, 토막 2 = 나머지, 사이 틈 14. 두 띠의 전체 길이는 T와 상관없이 426이다.
    /// _bPulse이고 초과(나무 수 >= T)이면 초과 토막 테와 채움이 흰색으로 번쩍인다(v6 pulse=True 그림).
    /// </summary>
    public void PaintThresholdBand(int _trees, int _threshold, bool _bPulse)
    {
        bool bOver = _threshold <= _trees;
        bool bFlash = true == _bPulse && true == bOver;
        RoguelikeWorldHudThresholdFx fx = new RoguelikeWorldHudThresholdFx(RoguelikeWorldHudGeometry.A0, RoguelikeWorldHudGeometry.A1, 0UL, false, 0d, bFlash, bFlash);
        PaintThresholdBand(_trees, _threshold, bOver, in fx);
    }

    /// <summary>
    /// 임계치 띠 연출판(world_hud_v7_motion.py): _trees = 보이는 나무 수(등장 중에는 켜진 칸만큼), _bOver = 실제 나무 수가 T 이상인지(채움 색 선택).
    /// 연출 인자는 RoguelikeWorldHudThresholdFx.
    /// </summary>
    public void PaintThresholdBand(int _trees, int _threshold, bool _bOver, in RoguelikeWorldHudThresholdFx _fx)
    {
        bool bOver = _bOver;
        int cellsFirst = Mathf.Max(0, _threshold >> 1);
        int lead = (_trees - 1) >> 1;
        int firstEnd = RoguelikeWorldHudGeometry.A0 + cellsFirst * RoguelikeWorldHudGeometry.Pitch - 2 + 4 * RoguelikeWorldHudGeometry.RimThickness;
        int secondStart = firstEnd + RoguelikeWorldHudGeometry.Split;
        RoguelikeWorldHudTone fillTone = true == bOver ? RoguelikeWorldHudPalette.Hot : RoguelikeWorldHudPalette.Amber;

        for (int i = 0; bandCount > i; i++)
        {
            int a = bandA[i];
            int b = bandB[i];
            int a0;
            int a1;
            int firstCell;
            RoguelikeWorldHudTone rim;

            if (a < firstEnd)
            {
                a0 = RoguelikeWorldHudGeometry.A0;
                a1 = firstEnd;
                firstCell = 0;
                rim = RoguelikeWorldHudPalette.Rim;
            }
            else if (secondStart <= a)
            {
                a0 = secondStart;
                a1 = RoguelikeWorldHudGeometry.A1;
                firstCell = cellsFirst;
                rim = true == _fx.bRimFlash ? RoguelikeWorldHudPalette.RimFlash : RoguelikeWorldHudPalette.RimOver;
            }
            else
            {
                bandOn[i] = false;
                continue;
            }

            if (a < _fx.revealLo || _fx.revealHi <= a)
            {
                bandOn[i] = false;
                continue;
            }

            bandOn[i] = true;

            Color32 color;
            bool bOutline = false;
            if (true == TryRimColor(a, b, a0, a1, in rim, out Color32 rimColor, out bOutline)) color = rimColor;
            else color = ThresholdCellColor(a - a0 - 2 * RoguelikeWorldHudGeometry.RimThickness, b - RoguelikeWorldHudGeometry.B0 - 2 * RoguelikeWorldHudGeometry.RimThickness, a, firstCell, _trees, lead, cellsFirst, in fillTone, in _fx);

            // 그려지는 머리: 보이는 범위 끝 6값은 윤곽을 뺀 모든 픽셀이 흰색
            if (RoguelikeWorldHudGeometry.A1 > _fx.revealHi && _fx.revealHi - RevealHeadWidth <= a && false == bOutline) color = RoguelikeWorldHudPalette.White;

            bandColor[i] = color;
        }

        ScatterBand(thresholdBandBuffer, in RoguelikeWorldHudGeometry.ThresholdBandRect, false);
    }

    /// <summary>
    /// 스태미너 띠(우상단). 임계치 기하를 좌우 반전해 그린다(오른쪽 끝 = 숫자 쪽이 채움 시작). 길이는 StaminaInner(418)를 10칸으로 나눈다.
    /// _fill = 채움 길이, _trail = 잔상 끝 길이(채움보다 길 때만 보임), _tone = 채움 3톤,
    /// [_flashFrom, _flashTo) = 흰색으로 덮는 구간(회복), _bAlternateRim = 테를 흰 테(강제 경고 점멸 위상)로.
    /// </summary>
    public void PaintStaminaBand(int _fill, int _trail, in RoguelikeWorldHudTone _tone, int _flashFrom, int _flashTo, bool _bAlternateRim)
    {
        RoguelikeWorldHudTone rim = true == _bAlternateRim ? RoguelikeWorldHudPalette.RimFlash : RoguelikeWorldHudPalette.Rim;
        PaintStaminaBand(_fill, _trail, in _tone, _flashFrom, _flashTo, in rim, StaminaDefaultHeadWidth, RoguelikeWorldHudGeometry.A0, RoguelikeWorldHudGeometry.A1);
    }

    /// <summary>
    /// 스태미너 띠 연출판: _rim = 테 3톤(펄스 때 (흰, RIM_FAR, RIM)), _headWidth = 채움 끝 흰 머리 폭(차오르는 동안 4, 평소 2),
    /// [_revealLo, _revealHi) = 보이는 A 범위(등장 · 퇴장). revealHi가 A1보다 작으면 끝 6값이 흰 머리.
    /// </summary>
    public void PaintStaminaBand(int _fill, int _trail, in RoguelikeWorldHudTone _tone, int _flashFrom, int _flashTo, in RoguelikeWorldHudTone _rim, int _headWidth, int _revealLo, int _revealHi)
    {
        for (int i = 0; bandCount > i; i++)
        {
            int a = bandA[i];
            int b = bandB[i];

            if (a < _revealLo || _revealHi <= a)
            {
                bandOn[i] = false;
                continue;
            }

            bandOn[i] = true;

            Color32 color;
            bool bOutline = false;
            if (true == TryRimColor(a, b, RoguelikeWorldHudGeometry.A0, RoguelikeWorldHudGeometry.A1, in _rim, out Color32 rimColor, out bOutline)) color = rimColor;
            else color = StaminaCellColor(a - RoguelikeWorldHudGeometry.A0 - 2 * RoguelikeWorldHudGeometry.RimThickness, b - RoguelikeWorldHudGeometry.B0 - 2 * RoguelikeWorldHudGeometry.RimThickness, _fill, _trail, in _tone, _flashFrom, _flashTo, _headWidth);

            if (RoguelikeWorldHudGeometry.A1 > _revealHi && _revealHi - RevealHeadWidth <= a && false == bOutline) color = RoguelikeWorldHudPalette.White;

            bandColor[i] = color;
        }

        ScatterBand(staminaBandBuffer, in RoguelikeWorldHudGeometry.StaminaBandRect, true);
    }

    private static bool IsBandPixel(int _sx, int _sy)
    {
        int a = _sx - 2 * _sy;
        int b = _sx + 2 * _sy;
        return RoguelikeWorldHudGeometry.A0 <= a && a < RoguelikeWorldHudGeometry.A1 && RoguelikeWorldHudGeometry.B0 <= b && b < RoguelikeWorldHudGeometry.B1;
    }

    /// <summary>
    /// 윤곽 · 테 색(decal). 경계에서 2값 = 1px 2:1 선: d = min(dA, dBf, dBn) / 2가 0이면 윤곽, 1이면 테(먼 쪽 B0 쪽 밝게, 가까운 쪽 어둡게, 마구리 쪽 중간).
    /// 안쪽(d >= 2)이면 false.
    /// </summary>
    private static bool TryRimColor(int _a, int _b, int _a0, int _a1, in RoguelikeWorldHudTone _rim, out Color32 _color, out bool _bOutline)
    {
        int dA = Mathf.Min(_a - _a0, _a1 - 1 - _a);
        int dBf = _b - RoguelikeWorldHudGeometry.B0;
        int dBn = RoguelikeWorldHudGeometry.B1 - 1 - _b;
        int d = Mathf.Min(dA, Mathf.Min(dBf, dBn)) / 2;
        _bOutline = false;

        if (0 == d)
        {
            _color = RoguelikeWorldHudPalette.Outline;
            _bOutline = true;
            return true;
        }

        if (1 == d)
        {
            if (1 == dBf / 2 && dBf <= dA) _color = _rim.light;
            else if (1 == dBn / 2 && dBn <= dA) _color = _rim.shadow;
            else _color = _rim.main;
            return true;
        }

        _color = default;
        return false;
    }

    /// <summary>안쪽 b(0 ~ Inner - 1)에 따른 3톤: 위 2행 = 밝음, 아래 2행 = 그림자, 나머지 = 기본.</summary>
    private static Color32 PickTone(in RoguelikeWorldHudTone _tone, int _b)
    {
        if (2 > _b) return _tone.light;
        if (RoguelikeWorldHudGeometry.Inner - 2 <= _b) return _tone.shadow;
        return _tone.main;
    }

    /// <summary>
    /// 임계치 띠 칸 색. _a = 토막 시작 기준 안쪽 A, _b = 안쪽 B, _rawA = 화면 기하 A(훑기 창 비교용), _firstCell = 토막 첫 칸 번호.
    /// 순서(world_hud_v7_motion.py cell_fn): 칸막이 -> 훑기 창(초과 토막) -> 채움(새 칸 흰색 -> 선두 -> 3톤) -> 빈 칸.
    /// </summary>
    private static Color32 ThresholdCellColor(int _a, int _b, int _rawA, int _firstCell, int _trees, int _lead, int _cellsFirst, in RoguelikeWorldHudTone _fillTone, in RoguelikeWorldHudThresholdFx _fx)
    {
        int cell = _a / RoguelikeWorldHudGeometry.Pitch;
        int k = _a % RoguelikeWorldHudGeometry.Pitch;
        cell += _firstCell;
        if (RoguelikeWorldHudGeometry.Pitch - 2 <= k) return RoguelikeWorldHudPalette.Segment;

        bool bBeyond = _cellsFirst <= cell;

        if (true == _fx.bSweep && true == bBeyond && _fx.sweepA - SweepWindow <= _rawA && _rawA < _fx.sweepA)
        {
            return _fx.sweepA - SweepHeadWidth <= _rawA ? RoguelikeWorldHudPalette.White : RoguelikeWorldHudPalette.Hot.light;
        }

        bool bFilled = 2 * cell + 2 <= _trees || (2 * cell + 1 == _trees && RoguelikeWorldHudGeometry.Inner / 2 <= _b);

        if (true == bFilled)
        {
            if (64 > cell && 0UL != (_fx.flashMask & (1UL << cell))) return RoguelikeWorldHudPalette.White;

            RoguelikeWorldHudTone tone = true == bBeyond ? (true == _fx.bFillFlash ? RoguelikeWorldHudPalette.HotFlash : RoguelikeWorldHudPalette.Hot) : _fillTone;

            if (cell == _lead && _b < RoguelikeWorldHudGeometry.Inner - 2) return 2 > _b ? RoguelikeWorldHudPalette.White : tone.light;
            return PickTone(in tone, _b);
        }

        return true == bBeyond ? PickTone(in RoguelikeWorldHudPalette.EmptyOver, _b) : PickTone(in RoguelikeWorldHudPalette.Empty, _b);
    }

    /// <summary>스태미너 띠 칸 색(반전 전 좌표). _a = 안쪽 A(0 ~ 417), _b = 안쪽 B.</summary>
    private static Color32 StaminaCellColor(int _a, int _b, int _fill, int _trail, in RoguelikeWorldHudTone _tone, int _flashFrom, int _flashTo, int _headWidth)
    {
        if (RoguelikeWorldHudGeometry.StaminaPitch - 2 <= _a % RoguelikeWorldHudGeometry.StaminaPitch) return RoguelikeWorldHudPalette.Segment;

        if (_a < _fill)
        {
            if (_flashFrom <= _a && _a < _flashTo) return RoguelikeWorldHudPalette.White;
            if (_fill - _headWidth <= _a && _b < RoguelikeWorldHudGeometry.Inner - 2) return RoguelikeWorldHudPalette.White;
            return PickTone(in _tone, _b);
        }

        if (_a < _trail) return PickTone(in RoguelikeWorldHudPalette.Trail, _b);
        return PickTone(in RoguelikeWorldHudPalette.Empty, _b);
    }

    /// <summary>
    /// lift: 실루엣을 (0, 5) 그림자, (0, 1) SIDE1, (0, 2) SIDE2로 깐 뒤 띠를 얹는다. 위에 놓이는 쪽이 이기므로 그림자 -> SIDE1 -> SIDE2 -> 띠 순서로 덮어쓴다.
    /// _bMirror면 x를 좌우 반전해 쓴다(스태미너 띠).
    /// </summary>
    private void ScatterBand(Color32[] _buffer, in RoguelikeWorldHudRect _rect, bool _bMirror)
    {
        Array.Clear(_buffer, 0, _buffer.Length);

        for (int pass = 0; ShadowPassCount > pass; pass++)
        {
            int dropY = 0;
            Color32 passColor = RoguelikeWorldHudPalette.BandShadow;

            if (0 == pass) dropY = 5;
            else if (1 == pass)
            {
                dropY = 1;
                passColor = RoguelikeWorldHudPalette.Side1;
            }
            else if (2 == pass)
            {
                dropY = 2;
                passColor = RoguelikeWorldHudPalette.Side2;
            }

            for (int i = 0; bandCount > i; i++)
            {
                if (false == bandOn[i]) continue;

                int sx = true == _bMirror ? RoguelikeWorldHudGeometry.ScreenWidth - 1 - bandX[i] : bandX[i];
                int index = _rect.IndexOf(sx, bandY[i] + dropY);
                if (0 > index) continue;

                _buffer[index] = 3 == pass ? bandColor[i] : passColor;
            }
        }
    }

    // ------------------------------------------------------------------ 숫자 명패

    /// <summary>왼쪽 숫자: 나무 아이콘 + 나무 수 / 임계치. 오른쪽 끝이 마구리 바깥 끝에서 4px 떨어진다. warn = 큰 숫자 경고색.</summary>
    public void PaintThresholdLabel(int _trees, int _threshold, RoguelikeWorldHudDigitStyle _style)
    {
        int width = ComposeLabel(iconTree, _trees, _threshold, true, _style);

        Array.Clear(thresholdLabelBuffer, 0, thresholdLabelBuffer.Length);
        BlitWithShadow(labelTemp, RoguelikeWorldHudGeometry.LabelMaxWidth, width, RoguelikeWorldHudGeometry.LabelBodyHeight, thresholdLabelBuffer, in RoguelikeWorldHudGeometry.ThresholdLabelRect, RoguelikeWorldHudGeometry.CapLeftX - RoguelikeWorldHudGeometry.LabelGap - width, RoguelikeWorldHudGeometry.LabelTop);
    }

    /// <summary>
    /// 오른쪽 숫자: 번개 아이콘 + 스태미너 현재 / 최대(왼쪽 끝 고정). 아래 3px에 대쉬 칸 트레이(_dashCount 0 ~ 2)가 붙는다. _dashCount가 0이면 숨김.
    /// </summary>
    public void PaintStaminaLabel(int _current, int _max, RoguelikeWorldHudDigitStyle _style, int _dashCount, in RoguelikeWorldHudDashCell _dash0, in RoguelikeWorldHudDashCell _dash1)
    {
        int labelX = RoguelikeWorldHudGeometry.LabelRightX;
        ComposeLabel(iconBolt, _current, _max, true, _style);

        Array.Clear(staminaLabelBuffer, 0, staminaLabelBuffer.Length);
        BlitWithShadow(labelTemp, RoguelikeWorldHudGeometry.LabelMaxWidth, RoguelikeWorldHudGeometry.LabelMaxWidth, RoguelikeWorldHudGeometry.LabelBodyHeight, staminaLabelBuffer, in RoguelikeWorldHudGeometry.StaminaLabelRect, labelX, RoguelikeWorldHudGeometry.LabelTop);

        if (0 >= _dashCount) return;

        // 대쉬 칸: 번개 아이콘 열 가운데에 대쉬 아이콘, 칸은 숫자 시작 열부터(번개 아이콘 폭 + 2).
        int dashY = RoguelikeWorldHudGeometry.LabelTop + RoguelikeWorldHudGeometry.LabelBodyHeight + RoguelikeWorldHudGeometry.DashGap;
        int originX = RoguelikeWorldHudGeometry.StaminaLabelRect.x;
        int rowHeight = DashRowHeight + DashThickness;

        Array.Clear(dashTemp, 0, dashTemp.Length);
        BlitBitmap(iconDash, dashTemp, DashTempWidth, DashTempHeight, labelX - originX + ((iconBolt.width - iconDash.width) >> 1), (rowHeight - DashThickness - iconDash.height) >> 1);
        PaintDashRow(Mathf.Min(_dashCount, DashRowCellMax), in _dash0, in _dash1, labelX - originX + iconBolt.width + IconGap);
        BlitWithShadow(dashTemp, DashTempWidth, DashTempWidth, DashTempHeight, staminaLabelBuffer, in RoguelikeWorldHudGeometry.StaminaLabelRect, originX, dashY);
    }

    /// <summary>남은 벌목 수(위 가운데, v8): 도끼 아이콘 + 남은 수 max(0, 목표 - 처치), 분모 없음. x 320 가운데, y WaveY.</summary>
    public void PaintKillLabel(int _remaining, RoguelikeWorldHudDigitStyle _style)
    {
        int width = ComposeLabel(iconAxe, _remaining, 0, false, _style);

        Array.Clear(killLabelBuffer, 0, killLabelBuffer.Length);
        BlitWithShadow(labelTemp, RoguelikeWorldHudGeometry.LabelMaxWidth, width, RoguelikeWorldHudGeometry.LabelBodyHeight, killLabelBuffer, in RoguelikeWorldHudGeometry.KillLabelRect, RoguelikeWorldHudGeometry.ScreenWidth / 2 - width / 2, RoguelikeWorldHudGeometry.WaveY);
    }

    /// <summary>웨이브 숫자(좌상단, v8): 깃발 아이콘 + 현재 웨이브, 분모 없음. 왼쪽 끝 x WaveX, y WaveY.</summary>
    public void PaintWaveLabel(int _wave)
    {
        int width = ComposeLabel(iconFlag, _wave, 0, false, RoguelikeWorldHudDigitStyle.White);

        Array.Clear(waveLabelBuffer, 0, waveLabelBuffer.Length);
        BlitWithShadow(labelTemp, RoguelikeWorldHudGeometry.LabelMaxWidth, width, RoguelikeWorldHudGeometry.LabelBodyHeight, waveLabelBuffer, in RoguelikeWorldHudGeometry.WaveLabelRect, RoguelikeWorldHudGeometry.WaveX, RoguelikeWorldHudGeometry.WaveY);
    }

    /// <summary>언리밋 웨이브 표시(좌상단, v8): 무한대 아이콘 + 단계 번호, 분모 없음. 웨이브 숫자와 같은 버퍼 · 같은 위치를 쓴다.</summary>
    public void PaintUnlimitLabel(int _index)
    {
        int width = ComposeLabel(iconInfinity, _index, 0, false, RoguelikeWorldHudDigitStyle.White);

        Array.Clear(waveLabelBuffer, 0, waveLabelBuffer.Length);
        BlitWithShadow(labelTemp, RoguelikeWorldHudGeometry.LabelMaxWidth, width, RoguelikeWorldHudGeometry.LabelBodyHeight, waveLabelBuffer, in RoguelikeWorldHudGeometry.WaveLabelRect, RoguelikeWorldHudGeometry.WaveX, RoguelikeWorldHudGeometry.WaveY);
    }

    /// <summary>남은 벌목 수 숫자 폭(그림자 제외, 아이콘 + 큰 숫자). 목표 달성 반짝이가 숫자 위 어디에서 나올지 정하는 데 쓴다. ComposeLabel의 폭 계산과 같다.</summary>
    public int MeasureKillLabelWidth(int _remaining)
    {
        int mainCount = CountDigits(Mathf.Clamp(_remaining, 0, MaxNumber));

        return iconAxe.width + IconGap + BigGlyphWidth + (BigGlyphWidth - 1) * (mainCount - 1);
    }

    /// <summary>
    /// number_label: 아이콘(큰 숫자 세로 가운데) + 2 + 큰 숫자 + (2 + 슬래시(큰 숫자 바닥선) + 1 + 분모(큰 숫자 바닥선)). 큰 숫자 · 작은 숫자는 글리프 사이가 -1(겹침)이다.
    /// labelTemp(위에서 아래)에 쓰고 라벨 폭을 돌려준다.
    /// </summary>
    private int ComposeLabel(Bitmap _icon, int _main, int _sub, bool _bHasSub, RoguelikeWorldHudDigitStyle _style)
    {
        Array.Clear(labelTemp, 0, labelTemp.Length);

        Bitmap[] bigSet = bigGlyphs;
        if (RoguelikeWorldHudDigitStyle.Warn == _style) bigSet = bigWarnGlyphs;
        else if (RoguelikeWorldHudDigitStyle.Amber == _style) bigSet = bigAmberGlyphs;
        else if (RoguelikeWorldHudDigitStyle.Gold == _style) bigSet = bigGoldGlyphs;

        int main = Mathf.Clamp(_main, 0, MaxNumber);
        int mainCount = CountDigits(main);

        int x = 0;
        BlitBitmap(_icon, labelTemp, RoguelikeWorldHudGeometry.LabelMaxWidth, RoguelikeWorldHudGeometry.LabelBodyHeight, x, (RoguelikeWorldHudGeometry.LabelBodyHeight - _icon.height) >> 1);
        x += _icon.width + IconGap;

        for (int i = 0; mainCount > i; i++)
        {
            BlitBitmap(bigSet[DigitAt(main, mainCount, i)], labelTemp, RoguelikeWorldHudGeometry.LabelMaxWidth, RoguelikeWorldHudGeometry.LabelBodyHeight, x + i * (BigGlyphWidth - 1), 0);
        }

        x += BigGlyphWidth + (BigGlyphWidth - 1) * (mainCount - 1);

        if (false == _bHasSub) return x;

        x += IconGap;
        BlitBitmap(slash, labelTemp, RoguelikeWorldHudGeometry.LabelMaxWidth, RoguelikeWorldHudGeometry.LabelBodyHeight, x, RoguelikeWorldHudGeometry.LabelBodyHeight - slash.height);
        x += slash.width + SlashGapAfter;

        int sub = Mathf.Clamp(_sub, 0, MaxNumber);
        int subCount = CountDigits(sub);

        for (int i = 0; subCount > i; i++)
        {
            BlitBitmap(smallGlyphs[DigitAt(sub, subCount, i)], labelTemp, RoguelikeWorldHudGeometry.LabelMaxWidth, RoguelikeWorldHudGeometry.LabelBodyHeight, x + i * (SmallGlyphWidth - 1), RoguelikeWorldHudGeometry.LabelBodyHeight - SmallGlyphHeight);
        }

        return x + SmallGlyphWidth + (SmallGlyphWidth - 1) * (subCount - 1);
    }

    private static int CountDigits(int _value)
    {
        if (100 <= _value) return 3;
        if (10 <= _value) return 2;
        return 1;
    }

    /// <summary>_count자리 수에서 왼쪽 _position번째(0부터) 자릿수.</summary>
    private static int DigitAt(int _value, int _count, int _position)
    {
        int value = _value;
        for (int i = _count - 1; i > _position; i--) value /= 10;
        return value % 10;
    }

    // ------------------------------------------------------------------ 대쉬 칸

    /// <summary>
    /// dash_row: 한 트레이 안에 칸을 붙이고 사이는 1px 칸막이. 윤곽 + 테 + 칸 + 테 + 윤곽, 위쪽 행일수록 오른쪽으로 밀려 2:1 기울고(행 y -> (8 - y) / 2),
    /// 아래로 두께 2(SIDE1, SIDE2). _originX는 dashTemp 안 트레이 왼쪽 위 모서리 x(y는 0)이다.
    /// </summary>
    private void PaintDashRow(int _count, in RoguelikeWorldHudDashCell _dash0, in RoguelikeWorldHudDashCell _dash1, int _originX)
    {
        int innerWidth = _count * DashCellInnerWidth + (_count - 1) * DashSeparator;
        int rowWidth = innerWidth + 4;

        for (int pass = 0; 3 > pass; pass++)
        {
            for (int y = 0; DashRowHeight > y; y++)
            {
                int shift = (DashRowHeight - 1 - y) >> 1;

                for (int x = 0; rowWidth > x; x++)
                {
                    int tx = _originX + shift + x;

                    if (0 == pass) SetDashTemp(tx, y + 1, RoguelikeWorldHudPalette.Side1);
                    else if (1 == pass) SetDashTemp(tx, y + 2, RoguelikeWorldHudPalette.Side2);
                    else SetDashTemp(tx, y, DashRowColor(x, y, rowWidth, _count, in _dash0, in _dash1));
                }
            }
        }
    }

    private void SetDashTemp(int _x, int _y, in Color32 _color)
    {
        if (0 > _x || DashTempWidth <= _x || 0 > _y || DashTempHeight <= _y) return;

        dashTemp[_y * DashTempWidth + _x] = _color;
    }

    /// <summary>트레이 한 픽셀(기울이기 전 좌표). 그리는 순서는 dash_row의 사각형 순서와 같다(뒤에 그린 것이 위).</summary>
    private static Color32 DashRowColor(int _x, int _y, int _rowWidth, int _count, in RoguelikeWorldHudDashCell _dash0, in RoguelikeWorldHudDashCell _dash1)
    {
        Color32 color = RoguelikeWorldHudPalette.Outline;
        if (1 > _x || _rowWidth - 2 < _x || 1 > _y || DashRowHeight - 2 < _y) return color;

        color = RoguelikeWorldHudPalette.Rim.main;
        if (2 <= _x && _x <= _rowWidth - 3)
        {
            if (1 == _y) color = RoguelikeWorldHudPalette.Rim.light;
            else if (DashRowHeight - 2 == _y) color = RoguelikeWorldHudPalette.Rim.shadow;
            else color = RoguelikeWorldHudPalette.Segment;
        }

        if (2 > _y || DashRowHeight - 3 < _y) return color;

        for (int i = 0; _count > i; i++)
        {
            int x0 = 2 + i * (DashCellInnerWidth + DashSeparator);
            if (_x < x0 || x0 + DashCellInnerWidth - 1 < _x) continue;

            color = DashToneColor(in RoguelikeWorldHudPalette.Empty, _y);

            RoguelikeWorldHudDashCell cell = 0 == i ? _dash0 : _dash1;
            int fillWidth = true == cell.bFull ? DashCellInnerWidth : (int)(DashCellInnerWidth * (double)cell.ratio);
            if (_x - x0 < fillWidth)
            {
                color = true == cell.bFull ? DashToneColor(in RoguelikeWorldHudPalette.Dash, _y) : DashToneColor(in RoguelikeWorldHudPalette.DashCharging, _y);
                if (true == cell.bFull && 2 == _y && 1 <= _x - x0 && 2 >= _x - x0) color = RoguelikeWorldHudPalette.White;
            }

            break;
        }

        return color;
    }

    /// <summary>칸 안 세로 3톤: 윗행 = 밝음, 아랫행 = 그림자, 사이 = 기본.</summary>
    private static Color32 DashToneColor(in RoguelikeWorldHudTone _tone, int _y)
    {
        if (2 == _y) return _tone.light;
        if (DashRowHeight - 3 == _y) return _tone.shadow;
        return _tone.main;
    }

    // ------------------------------------------------------------------ 복사

    /// <summary>비트맵을 위에서 아래 작업 버퍼에 덮어쓴다(알파가 있는 픽셀만).</summary>
    private static void BlitBitmap(Bitmap _bitmap, Color32[] _target, int _targetWidth, int _targetHeight, int _x, int _y)
    {
        for (int sy = 0; _bitmap.height > sy; sy++)
        {
            int ty = _y + sy;
            if (0 > ty || _targetHeight <= ty) continue;

            for (int sx = 0; _bitmap.width > sx; sx++)
            {
                int tx = _x + sx;
                if (0 > tx || _targetWidth <= tx) continue;

                Color32 pixel = _bitmap.pixels[sy * _bitmap.width + sx];
                if (0 == pixel.a) continue;

                _target[ty * _targetWidth + tx] = pixel;
            }
        }
    }

    /// <summary>
    /// with_shadow: 작업 버퍼(위에서 아래, 폭 _srcWidth)를 출력 버퍼의 화면 위치 (_screenX, _screenY)에 복사하고 실루엣을 (0, 2) 아래에 알파 90 그림자로 깐다.
    /// 그림자 -> 본체 순서로 덮어쓴다.
    /// </summary>
    private static void BlitWithShadow(Color32[] _src, int _srcStride, int _srcWidth, int _srcHeight, Color32[] _dst, in RoguelikeWorldHudRect _rect, int _screenX, int _screenY)
    {
        for (int pass = 0; 2 > pass; pass++)
        {
            for (int y = 0; _srcHeight > y; y++)
            {
                for (int x = 0; _srcWidth > x; x++)
                {
                    Color32 pixel = _src[y * _srcStride + x];
                    if (0 == pixel.a) continue;

                    int index = _rect.IndexOf(_screenX + x, _screenY + y + (0 == pass ? RoguelikeWorldHudGeometry.LabelShadowDrop : 0));
                    if (0 > index) continue;

                    _dst[index] = 0 == pass ? RoguelikeWorldHudPalette.LabelShadow : pixel;
                }
            }
        }
    }

    // ------------------------------------------------------------------ 시트 읽기

    private static Bitmap ReadBitmap(Texture2D _texture)
    {
        Bitmap bitmap = new Bitmap(_texture.width, _texture.height);
        CopyRegion(_texture, 0, bitmap);
        return bitmap;
    }

    private static void CutGlyphs(Texture2D _sheet, Bitmap[] _glyphs, int _glyphWidth, int _glyphHeight)
    {
        for (int i = 0; _glyphs.Length > i; i++)
        {
            _glyphs[i] = new Bitmap(_glyphWidth, _glyphHeight);
            CopyRegion(_sheet, i * _glyphWidth, _glyphs[i]);
        }
    }

    /// <summary>텍스처(아래에서 위 행)의 _left 열부터 비트맵 크기만큼을 위에서 아래 행으로 복사한다. 초기화 때 한 번만 부른다.</summary>
    private static void CopyRegion(Texture2D _texture, int _left, Bitmap _bitmap)
    {
        Color32[] source = _texture.GetPixels32();

        for (int y = 0; _bitmap.height > y; y++)
        {
            int sourceRow = (_texture.height - 1 - y) * _texture.width;
            for (int x = 0; _bitmap.width > x; x++) _bitmap.pixels[y * _bitmap.width + x] = source[sourceRow + _left + x];
        }
    }
}
