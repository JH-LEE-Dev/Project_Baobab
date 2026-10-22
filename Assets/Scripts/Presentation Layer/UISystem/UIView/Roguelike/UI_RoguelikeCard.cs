using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 카드 한 장의 그림. 기획서 v2.4 "카드 선택 화면 구성"의 카드 본체 - 프레임 안에 카드 이미지, 하단 중앙에 레벨 표기만 둔다.
/// 카드 본체에는 텍스트를 넣지 않는다(이름·설명은 카드 아래 설명 패널이 맡는다).
///
/// [픽셀 규칙 - UI_WorkList 0장]
///   - 스케일은 항상 1(뒤집힘 연출 중 일시적으로 폭만 줄어드는 것은 허용). 크기는 스프라이트 원본 픽셀 그대로(sizeDelta = sprite.rect.size), 위치는 정수 픽셀.
///   - 호버는 크기가 아니라 Visual을 위로 HoverLiftPixels(6px) 올린다. 마우스를 받는 HitBox는 올라간 범위까지 덮고 움직이지 않는다.
///     움직이는 그림(Visual 아래)은 raycastTarget을 끈다 - 그림이 올라가며 판정이 풀렸다 걸렸다 하는 떨림이 없다.
///   - 뒤집힘은 3D 회전이 아니라 FlipRoot의 폭을 정수 픽셀 단계로 줄였다 늘리는 방식이다(SetFlipWidth). 폭은 짝수 픽셀만 쓴다 - 가운데 기준이라 홀수 폭은 가장자리가 반 픽셀에 걸린다.
///
/// [호버 연출] 마우스 호버와 패드 선택이 같은 경로(SetHovered)를 쓴다. 모두 unscaled 시간이고 트윈 라이브러리 없이 이 클래스 Update에서 계산한다(호출당 힙 할당 없음).
///   들어올림  Visual 높이를 스프링으로 움직인다. 올라갈 때는 목표를 살짝 지나쳤다 돌아오는 탄성(약 7px → 6px), 내려갈 때와 눌릴 때는 지나침 없이 부드럽게.
///             값은 부동소수로 계산하고 화면에는 반올림한 정수 픽셀만 보낸다.
///   누름      마우스를 누르는 동안(SetPressed) 또는 패드 제출 순간(PlayPressPulse) PressDepthPixels만큼 눌린다. 떼면 탄성으로 돌아온다.
///   밝기 대비 SetDimmed(true)면 어두워진다. 화면은 호버한 카드를 뺀 나머지 카드를 어둡게 해 어느 카드가 선택 중인지 밝기로도 보이게 한다.
///   그림자    카드 밑 납작한 그림자는 카드가 올라가면 연해지고 ShadowDropPixels만큼 내려가 떠 있는 느낌을 준다.
///   시차      올라갈 때 아이콘이 프레임보다 IconParallaxPixels만큼 더 떠서 깊이를 준다.
///   반짝      PlayFlash는 프레임 모양 그대로 짧게 하얗게 번쩍인다(선택하는 순간, 뒤집혀 앞면이 드러나는 순간은 더 약하게).
///
/// [뒤집힘 연출] 슬롯이 시간에 맞춰 SetFlipWidth(폭)·SetFlipLift(뜀)를 호출하고, 카드는 그에 맞춰 보이는 것을 정한다.
///   옆면     폭이 EdgeLineMaxWidth 이하로 얇아지면 카드 두께(4px)가 앞면 테두리 색(어두운 갈색 - 밝은 갈색 - 밝은 갈색 - 어두운 갈색)으로 보인다.
///            흰 선이 아니라 카드의 두께처럼 보이게 하려는 것이다. 위아래 끝은 프레임의 가장 밝은 하이라이트색.
///   그림자   떠 있는 높이에 따라 연해지고 좁아진다(호버와 뒤집힘이 같은 규칙).
///   착지     PlayLandingDust는 UI_RoguelikePixelFx로 먼지 알갱이·금빛 반짝을 낸다.
///
/// [아이콘] RoguelikeCardViewData.icon(= CardDefinitionSO.icon)을 그대로 쓴다. 특성 트리와 같은 스프라이트를 카드 에셋의 icon에 넣으면 된다.
/// [레벨] levelAfterPurchase를 I~VI로 그리고, maxLevel에 닿은 레벨은 MAX로 그린다(액티브 7레벨 = I~VI, MAX / 공통 패시브 5레벨 = I~IV, MAX).
///   루비(계약)·프리즘은 레벨이 없어 숨기고, 루비는 rubyBadgeSprite(±)가 있으면 그것을 그린다.
///   MAX 전용 이미지(maxLevelSprite)와 VI 이미지가 아직 없어서, MAX는 V로 대신 그리고 VI는 비어 있으면 레벨 표기를 숨긴다(리소스가 생기면 슬롯에 넣는다).
/// [희귀 등급] 프레임 자체의 색을 등급 팔레트로 바꿔 그린다(셰이더 UI/CardGradeFrame, 팔레트 스왑). 프레임을 덮는 층이 아니다.
///   GradeFrame  프레임 스프라이트 복사본(Card_FrameFull)을 같은 자리에 그리는 층. 프레임의 5톤(안쪽 그림자·몸통·링 둘·하이라이트)을 등급 5톤으로 치환해
///               손으로 그린 명암·모서리가 그대로 남는다. 희귀 카드에서는 원래 프레임 이미지를 끄고 이 층이 대신 보인다. 일반 카드는 원래 프레임 그대로.
///   등급 대응   에메랄드(MAX가 되는 카드) = 실버, 프리즘(조합) = 어두운 보랏빛 + 무지개 링, 루비(계약) = 붉은색.
///   움직임      옅은 해칭·링 맥동·단계식 광택 띠·모서리 반짝(셰이더가 시간으로 그린다).
///   이펙트      UI_RoguelikeCardAura가 등급별 . + x 입자(별가루·불씨·모서리 글린트)를 그린다. 호버로 늘어난다.
///   공개        PlayGradeReveal은 앞면이 드러나는 순간 대각 광택 띠가 왼쪽 위에서 오른쪽 아래로 지나가며 일반 프레임이 레어로 바뀌게 한다(0.5초, 와이프). 띠 위에서 입자가 흩날린다.
///               Image.color의 R 채널 = 공개 진행도(1이면 완전히 레어), G 채널 = 밝기(어두워짐). 공개가 끝나기 전에는 원래 프레임을 켜 둔다(아직 안 지나간 자리가 보여야 한다).
/// [뒷면] SetFaceDown(true)면 프레임만 어둡게 보인다(임시 뒷면 - 뒷면 리소스가 생기면 backSprite에 넣는다). 뒷면에서는 등급 층이 보이지 않는다.
/// </summary>
public class UI_RoguelikeCard : MonoBehaviour
{
    public const int HoverLiftPixels = 6;
    // MAX 전용 이미지가 없을 때 MAX 표기 대신 쓰는 레벨(V). 리소스가 생기면 maxLevelSprite를 채우면 이 대체는 쓰이지 않는다.
    private const int FallbackMaxBadgeLevel = 5;

    // 들어올림 스프링. 각진동수 30(약 0.12초에 정점), 올라갈 때 감쇠비 0.45(약 20% 지나침), 내려갈 때·눌릴 때 1.0(지나침 없음).
    private const float SpringOmega = 30f;
    private const float RiseDamping = 0.45f;
    private const float FallDamping = 1f;
    private const float SimStep = 1f / 120f;
    private const float MaxFrameSeconds = 1f / 20f;
    private const float RestEpsilon = 0.02f;

    private const int PressDepthPixels = 2;
    private const float PressPulseSeconds = 0.08f;
    private const int IconParallaxPixels = 1;
    private const int ShadowDropPixels = 2;
    private const float ShadowRestAlpha = 0.35f;
    private const float ShadowLiftedAlpha = 0.15f;
    private const float DimBrightness = 0.82f;
    private const float GradeRevealSeconds = 0.5f;
    private const float TintSeconds = 0.12f;
    private const float FlashRiseSeconds = 0.03f;
    private const float FlashSeconds = 0.1f;
    private const float FlashPeakAlpha = 0.7f;
    private const int EdgeLineMaxWidth = 10;
    private const int EdgeLineSolidWidth = 6;
    private const int ShadowShrinkPixels = 6;

    private static readonly Color BackTint = new Color(0.55f, 0.55f, 0.55f, 1f);

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private RectTransform visual;
    [SerializeField] private RectTransform flipRoot;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image profileImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private Image levelImage;
    [SerializeField] private Image shadowImage;
    [SerializeField] private Image flashImage;
    [SerializeField] private Image edgeLineImage;
    [SerializeField] private UI_RoguelikePixelFx pixelFx;
    [SerializeField] private Image gradeFrameImage;
    [SerializeField] private UI_RoguelikeCardAura aura;

    [Header("에셋")]
    [Tooltip("레벨 1~6(I~VI) 표기. 인덱스 0이 레벨 1. 비어 있는 칸은 그 레벨의 표기를 숨긴다.")]
    [SerializeField] private Sprite[] levelSprites = new Sprite[6];
    [Tooltip("MAX 표기. 비어 있으면 V 이미지로 대신한다.")]
    [SerializeField] private Sprite maxLevelSprite;
    [Tooltip("루비 카드의 ± 표기. 비어 있으면 루비는 레벨 표기를 숨긴다.")]
    [SerializeField] private Sprite rubyBadgeSprite;
    [Tooltip("희귀 등급의 프레임 팔레트 스왑 머티리얼. 순서: 에메랄드(실버), 프리즘, 루비. 비어 있는 칸은 그 등급의 층을 끈다.")]
    [SerializeField] private Material[] gradeFrameMaterials = new Material[3];
    [Tooltip("뒷면 스프라이트. 비어 있으면 기본 프레임을 어둡게 해서 뒷면으로 쓴다.")]
    [SerializeField] private Sprite backSprite;

    [Header("배치 (프레임 왼쪽 아래 기준 정수 픽셀)")]
    [SerializeField] private Vector2Int iconCenter = new Vector2Int(25, 55);
    [SerializeField] private Vector2Int levelCenter = new Vector2Int(25, 21);

    //상태 변수
    private Sprite defaultFrameSprite;
    private int gradeIndex = -1;
    private float gradeRevealTime = -1f;
    private float gradeRevealProgress = 1f;
    private bool bHovered;
    private bool bPressed;
    private bool bFaceDown;
    private bool bIconVisible;
    private bool bLevelVisible;
    private int shakeOffsetX;

    private float liftPosition;
    private float liftVelocity;
    private float simAccumulator;
    private float pressPulseRemaining;
    private float dimAmount;
    private bool bDimTarget;
    private float flashTime = -1f;
    private float flashPeak = FlashPeakAlpha;
    private int flipLift;
    private float shadowBaseWidth;
    private bool bMotionActive;
    private Vector2 iconBasePosition;
    private Vector2 shadowBasePosition;
    private bool bShadowBaseCaptured;
    private Color tint = Color.white;

    public bool IsHovered => bHovered;
    public bool IsFaceDown => bFaceDown;

    /// <summary>카드 그림 영역(호버로 올라가는 부분). 커서 박스가 이 위치를 따라간다.</summary>
    public RectTransform VisualRect => visual;

    public void SetData(in RoguelikeCardViewData _data)
    {
        SetCard(_data.icon, _data.kind, _data.displayGrade, _data.levelAfterPurchase, _data.maxLevel, _data.bRuby);
    }

    public void SetCard(Sprite _icon, ECardKind _kind, ECardDisplayGrade _grade, int _level, int _maxLevel, bool _bRuby)
    {
        ApplyFrame(_grade);
        ApplyIcon(_icon);
        ApplyLevel(_kind, _level, _maxLevel, _bRuby);
        ApplyFace();
        ApplyVisualPosition();
    }

    /// <summary>호버(마우스·커서 박스 공통). 그림만 정수 픽셀 위로 올리고 판정은 그대로다.</summary>
    public void SetHovered(bool _bHovered)
    {
        bHovered = _bHovered;
        bMotionActive = true;

        if (null != aura) aura.SetIntensity(_bHovered ? 1f : 0f);
    }

    /// <summary>마우스를 누르고 있는 동안 true. 눌린 만큼 카드가 내려간다.</summary>
    public void SetPressed(bool _bPressed)
    {
        bPressed = _bPressed;
        bMotionActive = true;
    }

    /// <summary>패드 제출처럼 누름과 뗌이 한 번에 오는 입력용. 짧게 눌렸다가 탄성으로 돌아온다.</summary>
    public void PlayPressPulse()
    {
        pressPulseRemaining = PressPulseSeconds;
        bMotionActive = true;
    }

    /// <summary>true면 어두워진다(다른 카드가 선택 중일 때). 밝기 대비로 선택 중인 카드를 구분한다.</summary>
    public void SetDimmed(bool _bDimmed)
    {
        bDimTarget = _bDimmed;
        bMotionActive = true;
    }

    /// <summary>프레임 모양 그대로 짧게 하얗게 번쩍인다. 선택하는 순간은 기본 세기, 뒤집혀 앞면이 드러나는 순간은 더 약하게 부른다.</summary>
    public void PlayFlash(float _peakAlpha = FlashPeakAlpha)
    {
        if (null == flashImage) return;

        flashPeak = _peakAlpha;
        flashTime = 0f;
        flashImage.sprite = frameImage.sprite;
        flashImage.enabled = true;
        bMotionActive = true;
    }

    /// <summary>
    /// 희귀 등급 공개: 대각 광택 띠가 지나가며 일반 프레임이 레어로 바뀐다. 앞면이 드러나는 순간에 부른다. 일반 카드·뒷면에서는 아무 일도 없다.
    /// </summary>
    public void PlayGradeReveal()
    {
        if (gradeIndex < 0 || bFaceDown) return;

        gradeRevealTime = 0f;
        gradeRevealProgress = 0f;
        ApplyGradeLayers();
        bMotionActive = true;
    }

    /// <summary>호버·누름·밝기·반짝을 모두 처음 상태로 즉시 되돌린다(카드를 새로 채울 때, 꺼질 때).</summary>
    public void ResetMotion()
    {
        bHovered = false;
        bPressed = false;
        bDimTarget = false;
        pressPulseRemaining = 0f;
        liftPosition = 0f;
        liftVelocity = 0f;
        simAccumulator = 0f;
        dimAmount = 0f;
        flashTime = -1f;
        flipLift = 0;
        gradeRevealTime = -1f;
        gradeRevealProgress = 1f;
        bMotionActive = false;

        if (null != aura) aura.Clear();

        if (null != flashImage) flashImage.enabled = false;
        if (null != edgeLineImage) edgeLineImage.enabled = false;
        if (null != pixelFx) pixelFx.Clear();

        tint = Color.white;
        ApplyFace();
        ApplyVisualPosition();
    }

    /// <summary>거절 반응용 좌우 흔들림(정수 픽셀). 0이면 제자리.</summary>
    public void SetShakeOffset(int _offsetX)
    {
        shakeOffsetX = _offsetX;
        ApplyVisualPosition();
    }

    /// <summary>뒷면(true)이면 프레임만 어둡게 보이고 프로필·아이콘·레벨은 숨긴다.</summary>
    public void SetFaceDown(bool _bFaceDown)
    {
        bFaceDown = _bFaceDown;
        ApplyFace();
    }

    /// <summary>뒤집힘 연출용 폭(픽셀). 프레임 폭이면 정상, 0이면 안 보인다. 짝수 픽셀만 쓰는 것이 호출 쪽 규칙이다.</summary>
    public void SetFlipWidth(int _width)
    {
        if (null == flipRoot) return;

        float frameWidth = flipRoot.rect.width;
        float scaleX = 0f < frameWidth ? Mathf.Max(0f, _width / frameWidth) : 1f;
        flipRoot.localScale = new Vector3(scaleX, 1f, 1f);

        if (null == edgeLineImage) return;

        bool bEdge = _width < frameWidth && _width <= EdgeLineMaxWidth;
        edgeLineImage.enabled = bEdge;
        if (bEdge) edgeLineImage.color = new Color(1f, 1f, 1f, Mathf.Clamp01(1f - (_width - EdgeLineSolidWidth) / (float)(EdgeLineMaxWidth - EdgeLineSolidWidth)));
    }

    /// <summary>뒤집힘 연출의 뜀(정수 픽셀, 음수면 눌림). 호버 높이에 더해져 그림과 그림자에 반영된다.</summary>
    public void SetFlipLift(int _pixels)
    {
        flipLift = _pixels;
        ApplyVisualPosition();
    }

    /// <summary>착지 순간 그림자 자리에서 먼지·지면 링·금빛 반짝이 퍼진다.</summary>
    public void PlayLandingDust()
    {
        if (null == pixelFx || null == shadowImage) return;

        RectTransform shadowRect = shadowImage.rectTransform;
        pixelFx.EmitLanding(FrameWidth * 0.5f, shadowBasePosition.y + shadowRect.sizeDelta.y * 0.5f);
    }

    /// <summary>프레임 폭(픽셀). 뒤집힘 폭 계산용.</summary>
    public int FrameWidth => null != flipRoot ? Mathf.RoundToInt(flipRoot.rect.width) : 0;

    // // 움직임

    /// <summary>dt초만큼 스프링·밝기·반짝을 진행한다. 아직 움직이는 것이 남았으면 true.</summary>
    private bool StepMotion(float _dt)
    {
        bool bActive = false;

        if (0f < pressPulseRemaining) pressPulseRemaining = Mathf.Max(0f, pressPulseRemaining - _dt);

        float target = (bHovered ? HoverLiftPixels : 0f) - (bPressed || 0f < pressPulseRemaining ? PressDepthPixels : 0f);

        simAccumulator += _dt;
        while (SimStep <= simAccumulator)
        {
            simAccumulator -= SimStep;
            IntegrateSpring(target);
        }

        if (Mathf.Abs(target - liftPosition) < RestEpsilon && Mathf.Abs(liftVelocity) < RestEpsilon)
        {
            liftPosition = target;
            liftVelocity = 0f;
        }
        else
        {
            bActive = true;
        }

        float dimTarget = bDimTarget ? 1f : 0f;
        if (dimAmount != dimTarget)
        {
            dimAmount = Mathf.MoveTowards(dimAmount, dimTarget, _dt / TintSeconds);
            float brightness = Mathf.Lerp(1f, DimBrightness, dimAmount);
            tint = new Color(brightness, brightness, brightness, 1f);
            ApplyFace();
            bActive = true;
        }

        if (0f <= gradeRevealTime)
        {
            gradeRevealTime += _dt;
            if (GradeRevealSeconds <= gradeRevealTime)
            {
                gradeRevealTime = -1f;
                gradeRevealProgress = 1f;
            }
            else
            {
                gradeRevealProgress = gradeRevealTime / GradeRevealSeconds;
                bActive = true;
            }

            ApplyGradeLayers();
        }

        if (0f <= flashTime)
        {
            flashTime += _dt;
            if (FlashSeconds <= flashTime)
            {
                flashTime = -1f;
                flashImage.enabled = false;
            }
            else
            {
                float alpha = flashTime < FlashRiseSeconds ? flashTime / FlashRiseSeconds : 1f - (flashTime - FlashRiseSeconds) / (FlashSeconds - FlashRiseSeconds);
                flashImage.color = new Color(1f, 1f, 1f, flashPeak * Mathf.Clamp01(alpha));
                bActive = true;
            }
        }

        ApplyVisualPosition();
        return bActive;
    }

    private void IntegrateSpring(float _target)
    {
        float damping = _target > liftPosition ? RiseDamping : FallDamping;
        float acceleration = SpringOmega * SpringOmega * (_target - liftPosition) - 2f * damping * SpringOmega * liftVelocity;
        liftVelocity += acceleration * SimStep;
        liftPosition += liftVelocity * SimStep;
    }

    /// <summary>스프링 값과 뒤집힘 뜀을 정수 픽셀로 반올림해 그림·그림자·아이콘에 반영한다.</summary>
    private void ApplyVisualPosition()
    {
        float totalLift = liftPosition + flipLift;
        int lift = Mathf.RoundToInt(totalLift);
        if (null != visual) visual.anchoredPosition = new Vector2(shakeOffsetX, lift);

        float liftRatio = Mathf.Clamp01(totalLift / HoverLiftPixels);

        if (null != shadowImage)
        {
            RectTransform shadowRect = shadowImage.rectTransform;
            if (false == bShadowBaseCaptured)
            {
                shadowBasePosition = shadowRect.anchoredPosition;
                shadowBaseWidth = shadowRect.sizeDelta.x;
                bShadowBaseCaptured = true;
            }

            // 떠 있을수록 그림자가 연해지고 아래로 내려가며 좁아진다(폭은 짝수 픽셀 단위로 줄여 가운데를 유지한다).
            int shrink = Mathf.RoundToInt(ShadowShrinkPixels * 0.5f * liftRatio) * 2;
            shadowRect.sizeDelta = new Vector2(shadowBaseWidth - shrink, shadowRect.sizeDelta.y);
            shadowRect.anchoredPosition = new Vector2(shadowBasePosition.x + shrink * 0.5f, shadowBasePosition.y - Mathf.RoundToInt(ShadowDropPixels * liftRatio));
            shadowImage.color = new Color(0f, 0f, 0f, Mathf.Lerp(ShadowRestAlpha, ShadowLiftedAlpha, liftRatio));
        }

        if (null != iconImage && bIconVisible)
        {
            iconImage.rectTransform.anchoredPosition = new Vector2(iconBasePosition.x, iconBasePosition.y + Mathf.RoundToInt(IconParallaxPixels * Mathf.Clamp01(liftPosition / HoverLiftPixels)));
        }
    }

    private void ApplyFrame(ECardDisplayGrade _grade)
    {
        if (null != frameImage && null == defaultFrameSprite) defaultFrameSprite = frameImage.sprite;

        int index = GetGradeIndex(_grade);
        bool bHasLayer = 0 <= index && index < gradeFrameMaterials.Length && null != gradeFrameMaterials[index];
        gradeIndex = bHasLayer ? index : -1;

        if (null != gradeFrameImage && bHasLayer) gradeFrameImage.material = gradeFrameMaterials[index];
    }

    /// <summary>희귀 등급 → 머티리얼 칸. 에메랄드 0 · 프리즘 1 · 루비 2, 일반(브론즈, 더는 나오지 않는 실버·골드)은 -1.</summary>
    private static int GetGradeIndex(ECardDisplayGrade _grade)
    {
        switch (_grade)
        {
            case ECardDisplayGrade.Emerald: return 0;
            case ECardDisplayGrade.Prism: return 1;
            case ECardDisplayGrade.Ruby: return 2;
            default: return -1;
        }
    }

    /// <summary>
    /// 등급 층의 표시와 색. 희귀 카드(앞면)에서는 이 층이 원래 프레임을 대신하지만, 공개 연출이 끝나기 전에는 원래 프레임도 켜 둔다(아직 안 지나간 자리).
    /// 셰이더 약속: R = 공개 진행도(1이면 완전히 레어), G = 밝기(다른 카드가 선택 중일 때 어두워짐). 입자(Aura)도 같은 등급·어두워짐·진행도를 따른다.
    /// </summary>
    private void ApplyGradeLayers()
    {
        bool bShow = 0 <= gradeIndex && false == bFaceDown;
        bool bRevealDone = gradeRevealProgress >= 1f;

        if (null != gradeFrameImage)
        {
            float brightness = Mathf.Lerp(1f, DimBrightness, dimAmount);
            gradeFrameImage.enabled = bShow;
            gradeFrameImage.color = new Color(gradeRevealProgress, brightness, brightness, 1f);
        }

        if (null != aura)
        {
            aura.SetGrade(bShow ? gradeIndex : -1);
            aura.SetDim(dimAmount);
            aura.SetRevealProgress(gradeRevealProgress);
        }

        if (null != frameImage) frameImage.enabled = false == (bShow && bRevealDone);
    }

    private void ApplyIcon(Sprite _icon)
    {
        if (null == iconImage) return;

        bIconVisible = null != _icon;
        if (false == bIconVisible) return;

        iconImage.sprite = _icon;
        PlaceCentered(iconImage.rectTransform, _icon, iconCenter);
        iconBasePosition = iconImage.rectTransform.anchoredPosition;
    }

    private void ApplyLevel(ECardKind _kind, int _level, int _maxLevel, bool _bRuby)
    {
        if (null == levelImage) return;

        Sprite sprite = null;
        if (_bRuby || ECardKind.Ruby == _kind)
        {
            sprite = rubyBadgeSprite;
        }
        else if (ECardKind.Prism != _kind)
        {
            bool bMax = 0 < _maxLevel && _level >= _maxLevel;
            if (bMax) sprite = null != maxLevelSprite ? maxLevelSprite : GetLevelSprite(Mathf.Min(_level, FallbackMaxBadgeLevel));
            else sprite = GetLevelSprite(_level);
        }

        bLevelVisible = null != sprite;
        if (false == bLevelVisible) return;

        levelImage.sprite = sprite;
        PlaceCentered(levelImage.rectTransform, sprite, levelCenter);
    }

    private Sprite GetLevelSprite(int _level)
    {
        int index = _level - 1;
        return null != levelSprites && 0 <= index && index < levelSprites.Length ? levelSprites[index] : null;
    }

    /// <summary>앞면/뒷면 상태와 밝기(tint)를 그림에 반영한다(프레임 스프라이트·색, 아이콘·레벨·프로필 표시).</summary>
    private void ApplyFace()
    {
        bool bFront = false == bFaceDown;

        if (null != frameImage)
        {
            if (null == defaultFrameSprite) defaultFrameSprite = frameImage.sprite;

            if (bFront)
            {
                frameImage.sprite = defaultFrameSprite;
                frameImage.color = tint;
            }
            else
            {
                frameImage.sprite = null != backSprite ? backSprite : defaultFrameSprite;
                frameImage.color = (null != backSprite ? Color.white : BackTint) * tint;
            }
        }

        if (null != profileImage)
        {
            profileImage.enabled = bFront;
            profileImage.color = tint;
        }

        if (null != iconImage)
        {
            iconImage.enabled = bFront && bIconVisible;
            iconImage.color = tint;
        }

        if (null != levelImage)
        {
            levelImage.enabled = bFront && bLevelVisible;
            levelImage.color = tint;
        }

        ApplyGradeLayers();
    }

    /// <summary>
    /// 스프라이트 원본 픽셀 크기로 _center에 가운데 맞춘다. 크기가 홀수면 내림해서 항상 정수 픽셀에 놓는다.
    /// 대상 RectTransform은 앵커·피벗이 왼쪽 아래(0, 0)여야 한다(프리팹 빌더가 그렇게 만든다).
    /// </summary>
    private static void PlaceCentered(RectTransform _rect, Sprite _sprite, Vector2Int _center)
    {
        Vector2 size = _sprite.rect.size;
        _rect.sizeDelta = size;
        _rect.anchoredPosition = new Vector2(Mathf.Floor(_center.x - size.x * 0.5f), Mathf.Floor(_center.y - size.y * 0.5f));
    }

    private void Update()
    {
        if (false == bMotionActive) return;

        bMotionActive = StepMotion(Mathf.Min(Time.unscaledDeltaTime, MaxFrameSeconds));
    }

    private void OnDisable()
    {
        shakeOffsetX = 0;
        ResetMotion();
    }
}
