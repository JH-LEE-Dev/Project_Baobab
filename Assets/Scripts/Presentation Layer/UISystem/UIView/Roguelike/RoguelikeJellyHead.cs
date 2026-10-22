using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD 가로 막대의 젤리 머리(공용). 스태미너 바(HUD_Stemina.PaintFrontEdge)의 젤리 머리를 그대로 옮겼다.
/// 채움 앞쪽 바깥에 말랑한 둥근 머리를 CPU로 픽셀 단위로 그려 RawImage(Point 필터)에 얹는다. 처치 바와 임계치 바가 같은 클래스를 써서 두 막대의 끝 모양이 같다.
///   스프링(K 200, C 16)  평소 길이 2.2, 늘어남 최대 +2, 줄어듦 1.6, 호흡 0.35. 움직임이 시작될 때 +14, 멈출 때 -6의 충격. 늘어남은 앞쪽 속도(짧은 이동평균)에 비례한다.
///   모양  길이는 소수(앞쪽의 소수 부분 포함)이고 위아래 모서리만 둥글다(가운데는 평평). 줄어드는 동안은 표면 물결과 위/아래 행의 기울기 흔들림, 채움이 1px 줄 때마다 작은 충격.
///   명암  위쪽 행은 림 라이트(밝게), 아래쪽 행은 어둡게, 이음매는 채움과 같은 색. 바깥 1px는 바 프레임 색의 어두운 테두리. 갱신은 30fps이고 채움 앞쪽 픽셀이 바뀐 프레임은 바로 그린다.
/// 호출하는 쪽이 채움 앞쪽 위치(스프라이트 폭 기준 픽셀)를 넘겨 준다. 행 색은 SetRowColors로 바꿀 수 있다(임계치 바가 T를 넘으면 적색으로 입는다).
/// </summary>
public sealed class RoguelikeJellyHead
{
    private const float HeadStepInterval = 1f / 30f;
    private const float SpringK = 200f;
    private const float SpringC = 16f;
    private const float HeadRestLen = 2.2f;
    private const float HeadGainExtraMax = 2f;
    private const float HeadGainSpeedK = 8f;
    private const float HeadFastTau = 0.1f;
    private const float HeadDrainLen = 1.6f;
    private const float HeadBreath = 0.35f;
    private const float HeadGainKick = 14f;
    private const float HeadSettleKick = -6f;
    private const float HeadVelTau = 0.35f;
    private const float HeadGainVel = 0.01f;
    private const float HeadDrainEnter = 0.0006f;
    private const float HeadDrainExit = 0.0002f;
    private const float HeadDrainTau = 0.4f;
    private const float HeadRippleAmp = 0.7f;
    private const float HeadSloshAmp = 0.7f;
    private const float HeadTickKick = 5f;
    // 세로 가운데 이 비율까지는 길이가 일정한 평평한 면이고, 바깥 위/아래 구간에서만 둥글게 말린다.
    private const float HeadFlatRatio = 0.62f;
    // 림 라이트: 머리 길이 방향 위치(0 이음매, 1 바깥 끝)가 이 값을 넘는 위쪽 행은 밝게, 아래쪽 행은 어둡게.
    private const float HeadRimStart = 0.5f;
    private const float HeadRimOuter = 0.85f;
    private const float HeadRimBright1 = 0.14f;
    private const float HeadRimBright2 = 0.28f;
    private const float HeadShadeStart = 0.55f;
    private const float HeadShadeMul = 0.82f;

    //외부 의존성
    private readonly RawImage surface;
    private readonly int barWidth;
    private readonly int barHeight;
    private readonly bool[] insideMask;
    private readonly Color borderColor;
    private Color[] rowColors;

    //상태 변수
    private float headSpring = HeadRestLen;
    private float headVelocity;
    private float headSpeed;
    private float headFastSpeed;
    private float headDrainWeight;
    private float headTime;
    private float headRipplePhase;
    private float headStepTimer;
    private int headPrevFilled = -1;
    private int headLastPaintedFilled = -1;
    private int rowLo;
    private int rowHi = -1;
    private bool bHeadGain;
    private bool bHeadDrain;
    private bool bHeadDrawn;
    private Texture2D headTexture;
    private Color32[] headBuffer;
    private byte[] headMask;
    private float[] headShade;

    /// <summary>_insideMask는 채움 스프라이트의 윤곽 안쪽 여부(폭 x 높이, 아래 행부터). 머리는 이 안쪽에만 그려진다.</summary>
    public RoguelikeJellyHead(RawImage _surface, int _width, int _height, bool[] _insideMask, Color _borderColor)
    {
        surface = _surface;
        barWidth = _width;
        barHeight = _height;
        insideMask = _insideMask;
        borderColor = _borderColor;
    }

    /// <summary>행(아래부터)별 채움 색. 머리가 바 본체의 명암 행에 맞춰 색을 입는다.</summary>
    public void SetRowColors(Color[] _rowColors)
    {
        rowColors = _rowColors;
    }

    /// <summary>스프링과 속도를 처음 상태로 되돌리고 그려 둔 머리를 지운다.</summary>
    public void Reset()
    {
        headSpring = HeadRestLen;
        headVelocity = 0f;
        headSpeed = 0f;
        headFastSpeed = 0f;
        headDrainWeight = 0f;
        headPrevFilled = -1;
        headLastPaintedFilled = -1;
        bHeadGain = false;
        bHeadDrain = false;
        Clear();
    }

    /// <summary>
    /// _dt초 진행한다. _frontPixels는 채움 앞쪽 위치(스프라이트 폭 기준 픽셀, 소수), _previousPixels는 직전 프레임의 값.
    /// 앞쪽 속도를 시간 보정해 이동평균하고, 움직임이 시작·끝날 때 충격을 주며, 늘어나는 중에는 빠른 속도에 비례해 길어지고,
    /// 줄어드는 중에는 눌리고 물결치며, 멈춰 있으면 느리게 호흡한다.
    /// </summary>
    public void Step(float _dt, float _frontPixels, float _previousPixels)
    {
        headTime += _dt;

        float dtSafe = Mathf.Max(0.0001f, _dt);
        float rawSpeed = (_frontPixels - _previousPixels) / dtSafe / barWidth;

        // 앞쪽 속도(비율/초)를 시간 보정해 이동평균한다. 프레임 레이트나 한 프레임의 튐에 판정이 흔들리지 않는다.
        headSpeed += (rawSpeed - headSpeed) * (1f - Mathf.Exp(-dtSafe / HeadVelTau));

        // 늘어남을 정하는 빠른 속도(짧은 시정수). 차오름이 감속하면 머리도 함께 줄어 멈출 때 갑자기 수축하지 않는다.
        headFastSpeed += (Mathf.Max(0f, rawSpeed) - headFastSpeed) * (1f - Mathf.Exp(-dtSafe / HeadFastTau));

        bool bGain = HeadGainVel < headSpeed;

        if (true == bGain) bHeadDrain = false;
        else if (true == bHeadDrain) bHeadDrain = -HeadDrainExit > headSpeed;
        else bHeadDrain = -HeadDrainEnter > headSpeed;

        // 충격은 늘어남이 시작되고 끝날 때만 준다. 줄어듦과 정지 사이는 가중치로 부드럽게 이어진다.
        if (bGain != bHeadGain)
        {
            headVelocity += true == bGain ? HeadGainKick : HeadSettleKick;
            bHeadGain = bGain;
        }

        headDrainWeight += ((true == bHeadDrain ? 1f : 0f) - headDrainWeight) * (1f - Mathf.Exp(-dtSafe / HeadDrainTau));

        // 줄어서 채움이 한 픽셀 줄 때마다 작은 충격과 새 물결 위상을 준다(같은 패턴이 반복되지 않게 크기와 위상을 흔든다).
        int filledNow = Mathf.Min(barWidth, Mathf.FloorToInt(_frontPixels));

        if (0 <= headPrevFilled && filledNow < headPrevFilled && 0.2f < headDrainWeight)
        {
            headVelocity -= HeadTickKick * (0.5f + Random.value);
            headRipplePhase = Random.value * 6.2832f;
        }

        headPrevFilled = filledNow;

        float rest = HeadRestLen + HeadBreath * (1f + 0.8f * headDrainWeight) * Mathf.Sin(headTime * 2.4f);
        float target = rest + (HeadDrainLen - HeadRestLen) * headDrainWeight;

        if (true == bGain) target = rest + Mathf.Min(HeadGainExtraMax, headFastSpeed * HeadGainSpeedK);

        float h = Mathf.Min(_dt, 0.05f) * 0.25f;

        for (int i = 0; i < 4; i++)
        {
            float acceleration = SpringK * (target - headSpring) - SpringC * headVelocity;
            headVelocity += acceleration * h;
            headSpring += headVelocity * h;
        }

        headSpring = Mathf.Clamp(headSpring, 0.6f, HeadRestLen + HeadGainExtraMax + 0.6f);

        headStepTimer += _dt;

        // 정지/느린 구간은 30fps로, 채움 앞쪽 픽셀이 바뀐 프레임은 간격과 상관없이 바로 그린다.
        if (headStepTimer < HeadStepInterval && filledNow == headLastPaintedFilled) return;

        headStepTimer = 0f;
        Paint(_frontPixels);
    }

    /// <summary>머리를 그린 픽셀을 지우고 오버레이를 끈다.</summary>
    public void Clear()
    {
        if (true == bHeadDrawn && null != headTexture)
        {
            System.Array.Clear(headBuffer, 0, headBuffer.Length);
            headTexture.SetPixelData(headBuffer, 0);
            headTexture.Apply(false);
        }

        if (null != surface) surface.enabled = false;

        bHeadDrawn = false;
    }

    /// <summary>만든 텍스처를 없앤다(소유 컴포넌트가 파괴될 때).</summary>
    public void Dispose()
    {
        if (null != headTexture) Object.Destroy(headTexture);
        headTexture = null;
    }

    // // 내부

    /// <summary>
    /// 채움 앞쪽 바깥에 말랑한 둥근 머리를 그린다. 불투명 도트이고, 길이는 스프링 값이다.
    /// 앞쪽의 소수 부분을 길이에 더해 머리 끝이 픽셀 경계에서 점프하지 않게 한다. 위아래 끝까지 이어지는 캡 모양(가운데는 평평, 위/아래 모서리만 둥글게),
    /// 줄어드는 중 표면 물결과 위/아래 행 길이 차(기울기 흔들림), 가장자리 림 라이트 명암(이음매는 채움과 같은 색), 바깥 1px 테두리를 더한다.
    /// </summary>
    private void Paint(float _frontPixels)
    {
        int filled = Mathf.Min(barWidth, Mathf.FloorToInt(_frontPixels));
        headLastPaintedFilled = filled;

        // 비었거나 가득 찼으면 그릴 머리가 없다.
        if (0 >= filled || barWidth <= filled || false == EnsureSurface())
        {
            Clear();
            return;
        }

        int fl = filled - 1;
        float len = Mathf.Max(0.6f, headSpring) + (_frontPixels - filled);

        // 세로 범위: 바 윤곽이 실제로 차지하는 행 전체. 늘어나도 높이를 줄이지 않아 윗줄/아랫줄이 머리로 이어진다.
        // 줄어드는 중 느린 흔들림은 중심을 옮기지 않고 위/아래 행의 길이 차(기울기)로 준다.
        float tilt = HeadSloshAmp * headDrainWeight * Mathf.Sin(headTime * 1.7f + headRipplePhase);
        float half = (rowHi - rowLo + 1) * 0.5f;
        float cy = (rowLo + rowHi) * 0.5f;

        float ripple = HeadRippleAmp * headDrainWeight;
        int maxX = Mathf.Min(barWidth - 1, fl + Mathf.CeilToInt(len + ripple * 1.2f + Mathf.Abs(tilt)) + 1);

        System.Array.Clear(headBuffer, 0, headBuffer.Length);

        // 머리 영역의 마스크와 명암을 구한다(바 높이 방향 위쪽이 +y, 빛은 왼쪽 위).
        for (int y = 0; y < barHeight; y++)
        {
            // 행의 세로 위치(위쪽이 +). 평평한 면 밖에서만 둥글게 말려 길이 비율이 줄어든다.
            float v = (y - cy) / half;
            float vAbs = Mathf.Abs(v);
            float w = HeadFlatRatio < vAbs ? Mathf.Min(1f, (vAbs - HeadFlatRatio) / (1f - HeadFlatRatio)) : 0f;
            float f = Mathf.Sqrt(1f - w * w);

            float ay = len + tilt * v;

            if (0f < ripple)
            {
                ay += ripple * (Mathf.Sin(y * 0.9f - headTime * 5.2f + headRipplePhase) * 0.7f
                    + Mathf.Sin(y * 0.45f + headTime * 2.3f + headRipplePhase * 1.7f) * 0.4f);
            }

            ay = Mathf.Max(0.5f, ay);

            for (int x = fl + 1; x <= maxX + 1 && x < barWidth; x++)
            {
                int i = y * barWidth + x;
                headMask[i] = 0;
                headShade[i] = 0f;

                if (x > maxX) continue;

                float u = (x - (fl + 0.5f)) / ay;

                if (u > f) continue;

                headMask[i] = 1;
                headShade[i] = u / Mathf.Max(0.0001f, f);
            }
        }

        for (int y = 0; y < barHeight; y++)
        {
            Color rowColor = null != rowColors && y < rowColors.Length ? rowColors[y] : borderColor;
            float vRow = (y - cy) / half;

            for (int x = fl + 1; x <= maxX + 1 && x < barWidth; x++)
            {
                int i = y * barWidth + x;

                if (false == insideMask[i]) continue;

                float r;
                float g;
                float b;

                if (0 != headMask[i])
                {
                    float t = headShade[i];

                    r = rowColor.r;
                    g = rowColor.g;
                    b = rowColor.b;

                    if (0.15f < vRow && HeadRimStart < t)
                    {
                        float m = HeadRimOuter < t ? HeadRimBright2 : HeadRimBright1;

                        r = Mathf.Lerp(r, 1f, m);
                        g = Mathf.Lerp(g, 1f, m);
                        b = Mathf.Lerp(b, 0.92f, m);
                    }
                    else if (-0.3f > vRow && HeadShadeStart < t)
                    {
                        r *= HeadShadeMul;
                        g *= HeadShadeMul;
                        b *= HeadShadeMul;
                    }
                }
                else
                {
                    // 마스크 바깥에서 마스크와 맞닿은 1px는 어두운 테두리
                    bool bAdj = (x - 1 > fl && 0 != headMask[i - 1])
                        || (x + 1 <= maxX && 0 != headMask[i + 1])
                        || (y > 0 && 0 != headMask[i - barWidth])
                        || (y + 1 < barHeight && 0 != headMask[i + barWidth]);

                    if (false == bAdj) continue;

                    r = borderColor.r;
                    g = borderColor.g;
                    b = borderColor.b;
                }

                headBuffer[i] = new Color32(
                    (byte)Mathf.Clamp(Mathf.RoundToInt(r * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(g * 255f), 0, 255),
                    (byte)Mathf.Clamp(Mathf.RoundToInt(b * 255f), 0, 255),
                    255);
            }
        }

        headTexture.SetPixelData(headBuffer, 0);
        headTexture.Apply(false);
        surface.enabled = true;
        bHeadDrawn = true;
    }

    /// <summary>머리 텍스처·버퍼를 처음 필요할 때 만든다. 윤곽 데이터가 없으면 머리를 그리지 않는다(false).</summary>
    private bool EnsureSurface()
    {
        if (null == surface || null == insideMask || barWidth * barHeight != insideMask.Length) return false;

        if (null != headTexture) return true;

        headTexture = new Texture2D(barWidth, barHeight, TextureFormat.RGBA32, false);
        headTexture.name = "RoguelikeJellyHead";
        headTexture.filterMode = FilterMode.Point;
        headTexture.wrapMode = TextureWrapMode.Clamp;

        headBuffer = new Color32[barWidth * barHeight];
        headMask = new byte[barWidth * barHeight];
        headShade = new float[barWidth * barHeight];

        // 머리의 세로 범위: 바 윤곽이 실제로 차지하는 행(중앙 열 기준).
        rowLo = barHeight;
        rowHi = -1;

        for (int y = 0; y < barHeight; y++)
        {
            if (false == insideMask[y * barWidth + barWidth / 2]) continue;

            if (y < rowLo) rowLo = y;
            if (y > rowHi) rowHi = y;
        }

        if (0 > rowHi)
        {
            rowLo = 0;
            rowHi = barHeight - 1;
        }

        surface.texture = headTexture;
        return true;
    }
}
