using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 임계점 바. 현재 웨이브의 "현재 처치 수 / 목표 처치 수"(예: 12/40)를 채움과 숫자로 보여 준다.
/// 스태미너 바(MainBar)와 같은 모양에 폭만 120으로 줄이고(오른쪽의 대쉬 게이지와 겹치지 않게), 채움 색은 밝은 파스텔 적색 한 가지다(상태별 색 구분·눈금 없음). 프로젝트의 픽셀 규칙을 따른다.
///   - 채움 폭은 정수 픽셀(Image Filled)이고 반투명이 없다. 값이 바뀌면 지수 감속(시정수 0.12초)으로 따라간다.
///   - 목표를 채우기 전에는 오른쪽 14px(머리 포함)를 항상 비워 둔다(비율 x 106px). 목표를 채웠을 때만 꽉 찬다.
///   - 시간은 unscaled라 ESC로 멈춰도 연출이 이어진다.
///
/// [젤리 움직임] 스태미너 바(HUD_Stemina.PaintFrontEdge)의 젤리 머리를 그대로 옮겼다. 채움 앞쪽 바깥에 말랑한 둥근 머리를 CPU로 픽셀 단위로 그려 얹는다(Head = RawImage 120x18, Point 필터).
///   스프링(K 200, C 16)  평소 길이 2.2, 늘어남 최대 +2, 줄어듦 1.6, 호흡 0.35. 움직임이 시작될 때 +14, 멈출 때 -6의 충격. 늘어남은 앞쪽 속도(짧은 이동평균)에 비례한다.
///   모양  길이는 소수(앞쪽의 소수 부분 포함)이고 위아래 모서리만 둥글다(가운데는 평평). 줄어드는 동안은 표면 물결과 위/아래 행의 기울기 흔들림, 채움이 1px 줄 때마다 작은 충격.
///   명암  위쪽 행은 림 라이트(밝게), 아래쪽 행은 어둡게, 이음매는 채움과 같은 색. 바깥 1px는 바 프레임 색의 어두운 테두리. 갱신은 30fps이고 채움 앞쪽 픽셀이 바뀐 프레임은 바로 그린다.
///
/// [숫자] 바 가운데에 "처치 수/목표 처치 수"(현재/최대)를 그린다. 파스텔 적색·갈색 어느 바탕 위에서도 읽히도록 밝은 크림색에 1px 짙은 갈색 외곽선(네 방향 복사본)이고,
///        목표를 넘으면 41/40처럼 그대로 보여 준다. 값이 바뀔 때만 갱신한다(zero-alloc SetText).
/// [경고] 임계점 경고는 스태미너 바와 붉은 화면(BloodScreen)이 맡는다(UIView_HUD.SetRoguelikePressureWarning).
/// [피해] 임계치 피해의 번쩍임과 흔들림은 임계치 띠(UI_RoguelikePressureStrip)가 맡는다.
/// </summary>
public class UI_RoguelikeThresholdBar : MonoBehaviour
{
    private const int BarWidth = 120;
    private const int BarHeight = 18;

    // 채움: 목표 미달성 구간은 오른쪽 EndReserve만큼 비워 둔다(머리가 바 끝을 덮어 가득 찬 것처럼 보이지 않게). 목표를 채우면 BarWidth까지.
    private const int EndReserve = 14;
    private const float FillTau = 0.12f;
    private const float MaxStepSeconds = 1f / 20f;

    // 젤리 머리(HUD_Stemina와 같은 계수). 길이 단위는 픽셀.
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

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private Image fillImage;
    [SerializeField] private RawImage headSurface;
    [SerializeField, Tooltip("숫자 텍스트. 앞의 넷은 1px 외곽선(네 방향), 마지막이 본문. 모두 같은 문구를 받는다.")]
    private TMP_Text[] valueTexts = new TMP_Text[0];
    [SerializeField, Tooltip("채움 스프라이트의 행별 평균색(아래 행부터 BarHeight개). 머리가 바 본체의 명암 행에 맞춰 색을 입는 데 쓴다.")]
    private Color[] rowColors = new Color[0];
    [SerializeField, Tooltip("채움 스프라이트의 윤곽 안쪽 여부(BarWidth x BarHeight, 아래 행부터). 머리는 이 안쪽에만 그려진다.")]
    private bool[] insideMask = new bool[0];
    [SerializeField, Tooltip("머리 바깥 1px 테두리 색(바 프레임 색).")]
    private Color borderColor = new Color(0.23f, 0.16f, 0.12f, 1f);

    //상태 변수
    private float targetRatio;
    private float displayPixels;
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
    private int shownKilled = -1;
    private int shownTarget = -1;

    /// <summary>처치 진행도. 채움 = _killed / _target, 숫자 = "_killed/_target".</summary>
    public void SetProgress(int _killed, int _target)
    {
        ApplyValueText(_killed, _target);
        targetRatio = 0 < _target ? Mathf.Clamp01((float)_killed / _target) : 0f;
    }

    public void SetVisible(bool _bVisible)
    {
        gameObject.SetActive(_bVisible);
    }

    /// <summary>처음 모습으로 되돌린다(런이 시작·종료될 때).</summary>
    public void ResetBar()
    {
        targetRatio = 0f;
        displayPixels = 0f;
        headSpring = HeadRestLen;
        headVelocity = 0f;
        headSpeed = 0f;
        headFastSpeed = 0f;
        headDrainWeight = 0f;
        headPrevFilled = -1;
        headLastPaintedFilled = -1;
        bHeadGain = false;
        bHeadDrain = false;
        shownKilled = -1;
        shownTarget = -1;
        ClearHeadPixels();
        ApplyVisuals();
    }

    /// <summary>
    /// _dt초만큼 진행한다(Update가 부른다). 편집기 검증에서도 실제 시간 없이 이 메서드로 시간대별 모습을 만들 수 있다.
    /// </summary>
    public void Step(float _dt)
    {
        headTime += _dt;

        float previous = displayPixels;
        float targetPixels = 1f <= targetRatio ? BarWidth : targetRatio * (BarWidth - EndReserve);
        displayPixels = Mathf.Abs(targetPixels - displayPixels) < 0.01f ? targetPixels : displayPixels + (targetPixels - displayPixels) * (1f - Mathf.Exp(-_dt / FillTau));

        StepHead(previous, _dt);
        ApplyVisuals();
    }

    // // 내부

    /// <summary>
    /// 머리 스프링(HUD_Stemina의 젤리 머리와 같은 규칙). 앞쪽 속도를 시간 보정해 이동평균하고, 움직임이 시작·끝날 때 충격을 주며,
    /// 늘어나는 중에는 빠른 속도에 비례해 길어지고, 줄어드는 중에는 눌리고 물결치며, 멈춰 있으면 느리게 호흡한다.
    /// </summary>
    private void StepHead(float _previousPixels, float _dt)
    {
        float dtSafe = Mathf.Max(0.0001f, _dt);
        float rawSpeed = (displayPixels - _previousPixels) / dtSafe / BarWidth;

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
        int filledNow = Mathf.Min(BarWidth, Mathf.FloorToInt(displayPixels));

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
        PaintHead();
    }

    /// <summary>
    /// 채움 앞쪽 바깥에 말랑한 둥근 머리를 그린다(HUD_Stemina.PaintFrontEdge와 같은 모양). 불투명 도트이고, 길이는 스프링 값이다.
    /// 앞쪽의 소수 부분을 길이에 더해 머리 끝이 픽셀 경계에서 점프하지 않게 한다. 위아래 끝까지 이어지는 캡 모양(가운데는 평평, 위/아래 모서리만 둥글게),
    /// 줄어드는 중 표면 물결과 위/아래 행 길이 차(기울기 흔들림), 가장자리 림 라이트 명암(이음매는 채움과 같은 색), 바깥 1px 테두리를 더한다.
    /// </summary>
    private void PaintHead()
    {
        float frontPx = displayPixels;
        int filled = Mathf.Min(BarWidth, Mathf.FloorToInt(frontPx));
        headLastPaintedFilled = filled;

        // 비었거나 가득 찼으면 그릴 머리가 없다.
        if (0 >= filled || BarWidth <= filled || false == EnsureHeadSurface())
        {
            ClearHeadPixels();
            return;
        }

        int fl = filled - 1;
        float len = Mathf.Max(0.6f, headSpring) + (frontPx - filled);

        // 세로 범위: 바 윤곽이 실제로 차지하는 행 전체. 늘어나도 높이를 줄이지 않아 윗줄/아랫줄이 머리로 이어진다.
        // 줄어드는 중 느린 흔들림은 중심을 옮기지 않고 위/아래 행의 길이 차(기울기)로 준다.
        float tilt = HeadSloshAmp * headDrainWeight * Mathf.Sin(headTime * 1.7f + headRipplePhase);
        float half = (rowHi - rowLo + 1) * 0.5f;
        float cy = (rowLo + rowHi) * 0.5f;

        float ripple = HeadRippleAmp * headDrainWeight;
        int maxX = Mathf.Min(BarWidth - 1, fl + Mathf.CeilToInt(len + ripple * 1.2f + Mathf.Abs(tilt)) + 1);

        System.Array.Clear(headBuffer, 0, headBuffer.Length);

        // 머리 영역의 마스크와 명암을 구한다(바 높이 방향 위쪽이 +y, 빛은 왼쪽 위).
        for (int y = 0; y < BarHeight; y++)
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

            for (int x = fl + 1; x <= maxX + 1 && x < BarWidth; x++)
            {
                int i = y * BarWidth + x;
                headMask[i] = 0;
                headShade[i] = 0f;

                if (x > maxX) continue;

                float u = (x - (fl + 0.5f)) / ay;

                if (u > f) continue;

                headMask[i] = 1;
                headShade[i] = u / Mathf.Max(0.0001f, f);
            }
        }

        for (int y = 0; y < BarHeight; y++)
        {
            Color rowColor = y < rowColors.Length ? rowColors[y] : borderColor;
            float vRow = (y - cy) / half;

            for (int x = fl + 1; x <= maxX + 1 && x < BarWidth; x++)
            {
                int i = y * BarWidth + x;

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
                        || (y > 0 && 0 != headMask[i - BarWidth])
                        || (y + 1 < BarHeight && 0 != headMask[i + BarWidth]);

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
        headSurface.enabled = true;
        bHeadDrawn = true;
    }

    /// <summary>머리 텍스처·버퍼를 처음 필요할 때 만든다. 윤곽·행 색 데이터가 없으면 머리를 그리지 않는다(false).</summary>
    private bool EnsureHeadSurface()
    {
        if (null == headSurface || BarWidth * BarHeight != insideMask.Length || BarHeight > rowColors.Length) return false;

        if (null != headTexture) return true;

        headTexture = new Texture2D(BarWidth, BarHeight, TextureFormat.RGBA32, false);
        headTexture.name = "UI_RoguelikeThresholdBar_Head";
        headTexture.filterMode = FilterMode.Point;
        headTexture.wrapMode = TextureWrapMode.Clamp;

        headBuffer = new Color32[BarWidth * BarHeight];
        headMask = new byte[BarWidth * BarHeight];
        headShade = new float[BarWidth * BarHeight];

        // 머리의 세로 범위: 바 윤곽이 실제로 차지하는 행(중앙 열 기준).
        rowLo = BarHeight;
        rowHi = -1;

        for (int y = 0; y < BarHeight; y++)
        {
            if (false == insideMask[y * BarWidth + BarWidth / 2]) continue;

            if (y < rowLo) rowLo = y;
            if (y > rowHi) rowHi = y;
        }

        if (0 > rowHi)
        {
            rowLo = 0;
            rowHi = BarHeight - 1;
        }

        headSurface.texture = headTexture;
        return true;
    }

    /// <summary>머리를 그린 픽셀을 지우고 오버레이를 끈다.</summary>
    private void ClearHeadPixels()
    {
        if (true == bHeadDrawn && null != headTexture)
        {
            System.Array.Clear(headBuffer, 0, headBuffer.Length);
            headTexture.SetPixelData(headBuffer, 0);
            headTexture.Apply(false);
        }

        if (null != headSurface) headSurface.enabled = false;

        bHeadDrawn = false;
    }

    /// <summary>"처치 수/목표 처치 수" 숫자를 갱신한다. 값이 그대로면 아무것도 하지 않는다.</summary>
    private void ApplyValueText(int _killed, int _target)
    {
        if (_killed == shownKilled && _target == shownTarget) return;

        shownKilled = _killed;
        shownTarget = _target;

        for (int i = 0; i < valueTexts.Length; i++)
        {
            if (null != valueTexts[i]) valueTexts[i].SetText("{0}/{1}", _killed, _target);
        }
    }

    /// <summary>채움을 정수 픽셀로 반영한다(채움 앞쪽 열은 머리와 같은 Floor 규칙).</summary>
    private void ApplyVisuals()
    {
        int fillPixels = Mathf.Min(BarWidth, Mathf.FloorToInt(displayPixels));

        if (null != fillImage) fillImage.fillAmount = fillPixels / (float)BarWidth;
    }

    private void Update()
    {
        Step(Mathf.Min(Time.unscaledDeltaTime, MaxStepSeconds));
    }

    private void OnDestroy()
    {
        if (null != headTexture) Destroy(headTexture);
    }
}
