using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 전리품 상세 HUD(HUD_LootReveal)의 발광. 승인된 시뮬레이션 시안 2(https://claude.ai/artifact/7ohbbwSAE8RV4UPFrbUqMf)를 그대로 옮겼다.
    ///  ① 등장(뽀잉 시작 순간부터): 전리품 백열(0.15초 유지 후 0.7초에 원색), 0 / 0.18 / 0.36초 세 번 번쩍이는 리듬,
    ///     큰 오라 섬광, 가로·세로 렌즈 줄, 회전하는 4갈래 별빛, 광선 개선판(12줄, 리듬에 맞춰 길어졌다 사라짐)
    ///  ② 평상시: 마을 기둥 빛을 HUD 크기로 옮긴 오라(바닥 웅덩이는 은은하게, 빛 줄기·원반·실루엣 후광·떠오르는 단색 입자·가장자리 반짝임).
    ///     등장 빛이 줄어드는 동안 0.05~0.7초에 걸쳐 차오른다.
    ///
    /// 그리는 방식(성능): 모든 빛은 빌더가 구운 텍스처 Image라서 매 프레임 계산은 위치·회전·크기·색 쓰기뿐이다(GPU가 그린다).
    /// - HDR 세기는 정점 알파에 싣는다(UIGlow 가산: rgb = 정점 rgb x 알파 x 2.5). 머티리얼 인스턴스를 만들지 않아 배칭이 유지되고 머티리얼 더티가 없다.
    /// - 크기는 localScale로 바꿔 메쉬를 다시 만들지 않는다. 값이 바뀐 Image만 쓴다(색 바뀜 = 그 Image의 4정점만 다시 만듦).
    /// - 이펙트는 자식 Canvas(뒤 층 / 앞 층 / 백열)에 묶여 있어, 매 프레임 변화가 HUD 나머지(텍스트·배경)를 다시 배칭하지 않는다.
    /// - 픽셀 입자·반짝임은 30fps 틱이 바뀔 때만 다시 채운다. 등장 빛은 1.25초가 지나면 Image를 꺼서 그리지 않는다.
    /// - 초기화 이후 힙 할당 없음.
    ///
    /// 좌표: 뒤 층·앞 층 루트는 기둥 Image(IMG_Pillar)와 같은 사각형·피벗이라, 자식 위치는 기둥 기준 px다(캔버스 1단위 = 게임 1px).
    /// 색: HUD가 넘긴 성장 단계 HDR 색을 선형 값으로 해석한다(마을 기둥 빛과 같다). UI 정점 색은 감마로 넘긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class UI_LootRevealGlow : MonoBehaviour
    {
        private const float GlowMax = 2.5f;          // M_UILootGlowAdd / BeamFlow / Particles의 _GlowMax와 같아야 한다
        private const float FlashTintScale = 2.0f;   // M_UILootGlowFlash의 _TintScale과 같아야 한다
        private const float TickRate = 30.0f;
        private const float BreathPeriod = 2.6f;
        private const float BurstEnd = 1.25f;
        private const int MoteCount = 10;
        private const int TwinkleSpotCount = 5;
        private const int TwinklePeriod = 66;
        private const int TwinkleSpotStep = 13;
        private const float MoteMinGlow = 0.3f;

        // Image 슬롯(뒤 층 그리는 순서와 같다)
        public const int SlotPoolTint = 0;
        public const int SlotPoolAdd = 1;
        public const int SlotBeamTint = 2;
        public const int SlotBeamCore = 3;
        public const int SlotCoreTint = 4;
        public const int SlotCoreAdd = 5;
        public const int SlotBurstTint = 6;
        public const int SlotBurstAdd = 7;
        public const int SlotBurstCore = 8;
        public const int SlotRays = 9;
        public const int SlotLensWide = 10;
        public const int SlotLensCore = 11;
        public const int SlotLensV = 12;
        public const int SlotStar = 13;
        public const int SlotHalo = 14;
        public const int SlotCount = 15;

        /// <summary>슬롯별 기준 크기(px, 빌더가 sizeDelta로 넣는다). 실제 크기는 localScale = 원하는 크기 / 기준 크기.</summary>
        public static readonly Vector2[] SlotBaseSize =
        {
            new Vector2(96.0f, 40.0f), new Vector2(88.0f, 36.0f), new Vector2(40.0f, 96.0f), new Vector2(40.0f, 96.0f),
            new Vector2(120.0f, 120.0f), new Vector2(104.0f, 104.0f), new Vector2(128.0f, 128.0f), new Vector2(120.0f, 120.0f),
            new Vector2(52.0f, 52.0f), new Vector2(140.0f, 140.0f), new Vector2(140.0f, 12.0f), new Vector2(220.0f, 5.2f),
            new Vector2(5.2f, 88.0f), new Vector2(116.0f, 116.0f), new Vector2(96.0f, 96.0f),
        };

        /// <summary>빛 줄기 피벗: 아래 4px(기둥 윗면 아래) / 전체 96px.</summary>
        public const float BeamPivotY = 4.0f / 96.0f;

        private static readonly float[] PulseTime = { 0.0f, 0.18f, 0.36f };
        private static readonly float[] PulseStrength = { 1.0f, 0.7f, 0.45f };
        private const float PulsePeak = 0.04f;
        private const float PulseFall = 0.14f;

        private const int TwinkleNone = -1;
        private const int TwinklePlus5 = 5;
        private static readonly int[] TwinkleSequence =
        {
            PixelPainter.ShapeDot, PixelPainter.ShapePlus, TwinklePlus5, TwinklePlus5, TwinklePlus5, PixelPainter.ShapePlus, PixelPainter.ShapePlus,
            PixelPainter.ShapeCross, PixelPainter.ShapeCross, PixelPainter.ShapeDot, PixelPainter.ShapeDot, PixelPainter.ShapeDot, PixelPainter.ShapeDot, PixelPainter.ShapeDot,
        };

        private struct SlotState
        {
            public bool bOn;
            public Vector2 position;
            public Vector2 scale;
            public float angle;
            public Color32 color;
        }

        //외부 의존성
        [Header("참조 (빌더가 채움)")]
        [SerializeField, Tooltip("확대 스프라이트별 후광·중심·경계(LootRevealGlowData)")]
        private LootPillarGlowData data;
        [SerializeField, Tooltip("기둥 Image(IMG_Pillar). 뒤/앞 층 루트와 같은 사각형이다.")]
        private RectTransform pillarRect;
        [SerializeField, Tooltip("전리품 Image(IMG_Loot). 원반·후광·반짝임 위치와 백열 스프라이트 기준.")]
        private Image lootImage;
        [SerializeField, Tooltip("뒤 층 Image 15개. 순서는 Slot* 상수와 같다.")]
        private Image[] slots = new Image[SlotCount];
        [SerializeField, Tooltip("떠오르는 입자(뒤 층 맨 위)")]
        private UI_LootGlowParticles moteGraphic;
        [SerializeField, Tooltip("가장자리 반짝임(앞 층)")]
        private UI_LootGlowParticles twinkleGraphic;
        [SerializeField, Tooltip("전리품 백열(IMG_Loot 자식, 같은 스프라이트를 덮는다)")]
        private Image flashImage;

        [Header("기둥 윗면 · 색")]
        [SerializeField, Tooltip("기둥 Image 안에서 흙 윗면 중심의 위치(0~1, 왼쪽 아래 기준). Big_PillarGrassShadow 80x80의 위에서 27번째 줄.")]
        private Vector2 pillarTopUv = new Vector2(0.5f, 53.0f / 80.0f);
        [SerializeField, ColorUsage(true, true), Tooltip("성장 단계 색이 없을 때 쓰는 색(선형 HDR). 기본은 나무 단계 금색.")]
        private Color fallbackColor = new Color(2.0f, 1.576f, 0.580f, 1.0f);

        //내부 의존성
        private readonly SlotState[] states = new SlotState[SlotCount];
        private RectTransform[] slotRects;
        private RectTransform lootRect;
        private RectTransform flashRect;

        //상태 변수
        private bool bInitialized;
        private bool bPlaying;
        private float elapsed;
        private Color hdrColor;
        private Color tintColor;
        private Color coreColor;
        private Color normalColor;
        private float hdrMax;
        private float coreMax;
        private Color32 particleMainRgb;
        private Color32 particleCoreRgb;
        private int entryIndex = -1;
        private Sprite lastLootSprite;
        private int lastTick = int.MinValue;
        private int lastAnchorX;
        private int lastAnchorY;
        private int lastPivotX;
        private int lastPivotY;
        private bool bFlashOn;
        private bool bTwinklesOn;
        private Color32 flashColor;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 등장 발광을 시작한다(HUD_LootReveal.PlayVFX, 전리품 뽀잉 시작 순간). _bHasColor가 false면 fallbackColor를 쓴다.
        /// </summary>
        public void Play(Color _auraColor, bool _bHasColor)
        {
            EnsureInitialized();

            LootGlowShared.BuildPalette(true == _bHasColor ? _auraColor : fallbackColor, out hdrColor, out tintColor, out coreColor, out normalColor, out hdrMax, out coreMax);
            particleMainRgb = ToGammaRgb(normalColor, 255);
            particleCoreRgb = ToGammaRgb(new Color(coreColor.r / coreMax, coreColor.g / coreMax, coreColor.b / coreMax, 1.0f), 255);

            // 백열: 덮기 셰이더(_TintScale 2)라 원하는 HDR 색의 절반을 정점 rgb로 넣는다
            Color flash = coreColor * (0.85f / FlashTintScale);
            flashColor = ToGammaRgb(new Color(Mathf.Clamp01(flash.r), Mathf.Clamp01(flash.g), Mathf.Clamp01(flash.b), 1.0f), 0);

            elapsed = 0.0f;
            lastTick = int.MinValue;
            lastLootSprite = null;
            entryIndex = -1;
            bPlaying = true;
            enabled = true;
            HideAll();
        }

        /// <summary>즉시 멈추고 모두 숨긴다(Hide/ResetState/OnDestroy).</summary>
        public void Stop()
        {
            bPlaying = false;
            if (true == bInitialized) HideAll();
            enabled = false;
        }

        // 일반 비즈니스 로직 및 내부 메서드

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            slotRects = new RectTransform[SlotCount];
            for (int i = 0; i < SlotCount && i < slots.Length; i++)
            {
                if (null == slots[i]) continue;
                slotRects[i] = slots[i].rectTransform;
                slots[i].raycastTarget = false;
            }

            lootRect = null != lootImage ? lootImage.rectTransform : null;
            flashRect = null != flashImage ? flashImage.rectTransform : null;
            if (null != flashImage) flashImage.raycastTarget = false;
        }

        private static Color32 ToGammaRgb(Color _linear, byte _alpha)
        {
            bool bLinear = ColorSpace.Linear == QualitySettings.activeColorSpace;
            float r = true == bLinear ? Mathf.LinearToGammaSpace(Mathf.Clamp01(_linear.r)) : Mathf.Clamp01(_linear.r);
            float g = true == bLinear ? Mathf.LinearToGammaSpace(Mathf.Clamp01(_linear.g)) : Mathf.Clamp01(_linear.g);
            float b = true == bLinear ? Mathf.LinearToGammaSpace(Mathf.Clamp01(_linear.b)) : Mathf.Clamp01(_linear.b);
            return new Color32((byte)Mathf.RoundToInt(r * 255.0f), (byte)Mathf.RoundToInt(g * 255.0f), (byte)Mathf.RoundToInt(b * 255.0f), _alpha);
        }

        private static byte ToByte(float _v)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(_v) * 255.0f);
        }

        // 가산 슬롯 색: HDR 색 x 세기 → 정규화 rgb + 발광 알파
        private static Color32 AddColor(Color _hdr, float _hdrMax, float _intensity)
        {
            Color32 c = ToGammaRgb(new Color(_hdr.r / _hdrMax, _hdr.g / _hdrMax, _hdr.b / _hdrMax, 1.0f), 0);
            c.a = ToByte(_intensity * _hdrMax / GlowMax);
            return c;
        }

        // 덮기 슬롯 색: 색 + 덮는 알파
        private static Color32 TintColor(Color _tint, float _alpha)
        {
            return ToGammaRgb(_tint, ToByte(_alpha));
        }

        private static float Pulse(float _t)
        {
            float best = 0.0f;
            for (int i = 0; i < PulseTime.Length; i++)
            {
                float x = _t - PulseTime[i];
                if (0.0f > x) continue;

                float v = PulsePeak > x ? 1.0f : Mathf.Max(0.0f, 1.0f - (x - PulsePeak) / PulseFall);
                best = Mathf.Max(best, v * PulseStrength[i]);
            }

            return best;
        }

        // 전리품 프레임이 바뀌면 데이터 항목과 후광·백열 스프라이트를 바꾼다
        private void SyncLootSprite()
        {
            Sprite sprite = lootImage.sprite;
            if (sprite == lastLootSprite) return;

            lastLootSprite = sprite;
            entryIndex = null != data ? data.IndexOf(sprite) : -1;

            Image halo = slots[SlotHalo];
            if (0 <= entryIndex && null != halo)
            {
                Sprite haloSprite = data.Get(entryIndex).halo;
                if (null == haloSprite) entryIndex = -1;
                else if (halo.sprite != haloSprite) halo.sprite = haloSprite;
            }

            if (null != flashImage && flashImage.sprite != sprite) flashImage.sprite = sprite;
        }

        private void UpdateGlow()
        {
            float tv = elapsed;
            float breathe = 0.5f + 0.5f * Mathf.Sin(elapsed * (Mathf.PI * 2.0f / BreathPeriod));
            float steady = LootGlowShared.Smooth(0.05f, 0.7f, tv);

            // 기둥 윗면(기둥 기준 px)
            Rect pr = pillarRect.rect;
            float topX = pr.xMin + pr.width * pillarTopUv.x;
            float topY = pr.yMin + pr.height * pillarTopUv.y;

            // 전리품 사각형(기둥 기준 px): IMG_Loot는 기둥의 자식이다
            Vector2 lootScale = lootRect.localScale;
            Rect lr = lootRect.rect;
            Vector2 lootPos = lootRect.localPosition;
            float lootMinX = lootPos.x + lr.xMin * lootScale.x;
            float lootMinY = lootPos.y + lr.yMin * lootScale.y;
            float lootCenterX = lootPos.x + lr.center.x * lootScale.x;
            float lootCenterY = lootPos.y + lr.center.y * lootScale.y;

            // 스프라이트 1px = 사각형 몇 px인지(확대 스프라이트 64px를 64x64 Image에 그린다)
            Sprite sprite = lootImage.sprite;
            float pxScaleX = null != sprite && 0.0f < sprite.rect.width ? lr.width / sprite.rect.width * lootScale.x : lootScale.x;
            float pxScaleY = null != sprite && 0.0f < sprite.rect.height ? lr.height / sprite.rect.height * lootScale.y : lootScale.y;
            float pivotX = lootMinX + (null != sprite ? sprite.pivot.x : 0.0f) * pxScaleX;
            float pivotY = lootMinY + (null != sprite ? sprite.pivot.y : 0.0f) * pxScaleY;

            float itemX = lootCenterX;
            float itemY = lootCenterY;
            if (0 <= entryIndex)
            {
                Vector2 centroid = data.Get(entryIndex).centroid;
                itemX = pivotX + centroid.x * pxScaleX;
                itemY = pivotY + centroid.y * pxScaleY;
            }

            // ② 평상시 오라
            if (0.0f < steady)
            {
                float beamLevel = (0.85f + 0.15f * breathe) * steady;
                SetSlot(SlotPoolTint, topX, topY, 96.0f, 40.0f, 0.0f, TintColor(tintColor, 0.10f * steady));
                SetSlot(SlotPoolAdd, topX, topY, 88.0f, 36.0f, 0.0f, AddColor(hdrColor, hdrMax, (0.12f + 0.04f * breathe) * steady));
                SetSlot(SlotBeamTint, topX, topY, 40.0f, 96.0f, 0.0f, TintColor(tintColor, 0.42f * beamLevel));
                SetSlot(SlotBeamCore, topX, topY, 40.0f, 96.0f, 0.0f, AddColor(hdrColor * 0.675f, hdrMax * 0.675f, beamLevel));
                SetSlot(SlotCoreTint, itemX, itemY, 120.0f, 120.0f, 0.0f, TintColor(tintColor, 0.18f * steady));
                SetSlot(SlotCoreAdd, itemX, itemY, 104.0f, 104.0f, 0.0f, AddColor(hdrColor, hdrMax, (0.17f + 0.07f * breathe) * steady));

                if (0 <= entryIndex)
                {
                    // 후광 조각 = 스프라이트 + 여백 16px. 전리품 사각형 중심에 같은 배율로 겹친다.
                    SetSlot(SlotHalo, lootCenterX, lootCenterY, SlotBaseSize[SlotHalo].x * pxScaleX, SlotBaseSize[SlotHalo].y * pxScaleY, 0.0f,
                        AddColor(hdrColor, hdrMax, (0.3f + 0.2f * breathe) * steady));
                }
                else
                {
                    HideSlot(SlotHalo);
                }
            }

            // ① 등장 빛
            if (BurstEnd > tv)
            {
                float pulse = Pulse(tv);
                float flash = LootGlowShared.PeakDecay(tv, 0.15f, 1.2f);
                float level = Mathf.Max(flash, pulse * 0.8f);
                float auraTint = 2.0f * (64.0f + 8.0f * pulse);
                float auraAdd = 2.0f * (60.0f + 8.0f * pulse);
                SetSlot(SlotBurstTint, itemX, itemY, auraTint, auraTint, 0.0f, TintColor(tintColor, 0.22f * level));
                SetSlot(SlotBurstAdd, itemX, itemY, auraAdd, auraAdd, 0.0f, AddColor(hdrColor, hdrMax, 0.4f * level));
                SetSlot(SlotBurstCore, itemX, itemY, 52.0f, 52.0f, 0.0f, AddColor(coreColor, coreMax, 0.35f * level));

                float rayLevel = Mathf.Max(LootGlowShared.PeakDecay(tv, 0.15f, 1.1f), pulse * 0.9f);
                float raySize = 140.0f * (1.0f + 0.18f * pulse);
                SetSlot(SlotRays, itemX, itemY, raySize, raySize, tv * 0.25f * Mathf.Rad2Deg, AddColor(hdrColor, hdrMax, 0.4f * rayLevel));

                float lens = Mathf.Max(LootGlowShared.PeakDecay(tv, 0.15f, 0.9f), pulse * 0.8f);
                float lensGrow = 1.0f + 0.15f * pulse;
                SetSlot(SlotLensWide, itemX, itemY, 140.0f * lensGrow, 12.0f, 0.0f, AddColor(hdrColor, hdrMax, 0.25f * lens));
                SetSlot(SlotLensCore, itemX, itemY, 220.0f * lensGrow, 5.2f, 0.0f, AddColor(coreColor, coreMax, 0.75f * lens));
                SetSlot(SlotLensV, itemX, itemY, 5.2f, 88.0f * lensGrow, 0.0f, AddColor(coreColor, coreMax, 0.45f * lens));

                float starPulse = 1.0f + 0.15f * Mathf.Sin(tv * Mathf.PI * 2.0f * 3.0f);
                SetSlot(SlotStar, itemX, itemY, 116.0f * starPulse, 116.0f * starPulse, tv * 120.0f, AddColor(coreColor, coreMax, 0.6f * lens));

                // 백열: 전리품 위를 원하는 HDR 색으로 덮는다
                float white = Mathf.Max(LootGlowShared.PeakDecay(tv, 0.15f, 0.7f), pulse * 0.85f);
                SetFlash(white);
            }
            else
            {
                HideSlot(SlotBurstTint);
                HideSlot(SlotBurstAdd);
                HideSlot(SlotBurstCore);
                HideSlot(SlotRays);
                HideSlot(SlotLensWide);
                HideSlot(SlotLensCore);
                HideSlot(SlotLensV);
                HideSlot(SlotStar);
                SetFlash(0.0f);
            }

            // 픽셀 입자·반짝임: 30fps 틱 또는 기준 칸이 바뀔 때만
            int tick = Mathf.FloorToInt(elapsed * TickRate);
            int anchorX = Mathf.FloorToInt(topX + 0.5f);
            int anchorY = Mathf.FloorToInt(topY + 0.5f);
            int pivotCellX = Mathf.FloorToInt(pivotX + 0.5f);
            int pivotCellY = Mathf.FloorToInt(pivotY + 0.5f);
            if (tick == lastTick && anchorX == lastAnchorX && anchorY == lastAnchorY && pivotCellX == lastPivotX && pivotCellY == lastPivotY) return;

            lastTick = tick;
            lastAnchorX = anchorX;
            lastAnchorY = anchorY;
            lastPivotX = pivotCellX;
            lastPivotY = pivotCellY;
            RebuildParticles(tick, steady, anchorX, anchorY, pivotCellX, pivotCellY, 0.999f <= lootScale.x && 1.001f >= lootScale.x);
        }

        private void RebuildParticles(int _tick, float _steady, int _anchorX, int _anchorY, int _pivotX, int _pivotY, bool _bLootSettled)
        {
            if (null != moteGraphic)
            {
                moteGraphic.BeginQuads();
                if (0.0f < _steady)
                {
                    for (int i = 0; i < MoteCount; i++)
                    {
                        LootGlowShared.MoteState m = LootGlowShared.EvaluateMote(i, _tick, 3.7f, LootGlowShared.RevealMotes);
                        if (false == m.bVisible || MoteMinGlow > m.bright * _steady) continue;

                        Color32 color = particleMainRgb;
                        color.a = ToByte(hdrMax * LootGlowShared.ShapeGlow(m.shape) * 1.1f * m.bright * _steady / GlowMax);
                        moteGraphic.Shape(m.shape, _anchorX + m.x, _anchorY - m.yDown, color);
                    }
                }
                moteGraphic.Commit();
            }

            if (null != twinkleGraphic)
            {
                // 반짝임은 뽀잉이 끝난 뒤(배율 1)에만 그린다(시안과 같음)
                bool bShow = 0.0f < _steady && 0 <= entryIndex && true == _bLootSettled;
                if (true == bShow || true == bTwinklesOn)
                {
                    twinkleGraphic.BeginQuads();
                    if (true == bShow) DrawTwinkles(_tick, _steady, _pivotX, _pivotY);
                    twinkleGraphic.Commit();
                    bTwinklesOn = bShow;
                }
            }
        }

        private void DrawTwinkles(int _tick, float _steady, int _pivotX, int _pivotY)
        {
            LootPillarGlowData.Entry entry = data.Get(entryIndex);
            int minX = (int)entry.bounds.x;
            int minY = (int)entry.bounds.y;
            int maxX = (int)entry.bounds.z;
            int maxY = (int)entry.bounds.w;
            int centerX = LootGlowShared.RoundHalfUp(entry.centroid.x);
            float bright = 1.1f * _steady;

            for (int k = 0; k < TwinkleSpotCount; k++)
            {
                int lt = (_tick + k * TwinkleSpotStep) % TwinklePeriod;
                int shape = lt < TwinkleSequence.Length ? TwinkleSequence[lt] : TwinkleNone;
                if (TwinkleNone == shape) continue;

                int sx;
                int sy;
                switch (k)
                {
                    case 0: sx = minX - 2; sy = maxY - 4; break;
                    case 1: sx = maxX + 3; sy = maxY - 8; break;
                    case 2: sx = minX - 3; sy = Mathf.FloorToInt((minY + maxY) * 0.5f); break;
                    case 3: sx = centerX + 2; sy = maxY + 3; break;
                    default: sx = maxX + 2; sy = minY + 6; break;
                }

                DrawTwinkle(shape, _pivotX + sx, _pivotY + sy, bright);
            }
        }

        // 반짝임: `+` `x` 5x5`+`만 가운데 한 칸을 밝은 색으로(마을 기둥 빛과 같은 밝기 규칙)
        private void DrawTwinkle(int _shape, int _x, int _y, float _bright)
        {
            if (TwinklePlus5 == _shape)
            {
                Color32 center = WithGlow(particleCoreRgb, coreMax * _bright * 1.1f);
                Color32 near = WithGlow(particleMainRgb, hdrMax * _bright * 0.85f);
                Color32 far = WithGlow(particleMainRgb, hdrMax * _bright * 0.55f);
                twinkleGraphic.Dot(_x, _y + 2, far);
                twinkleGraphic.Dot(_x, _y + 1, near);
                twinkleGraphic.Dot(_x - 2, _y, far);
                twinkleGraphic.Dot(_x - 1, _y, near);
                twinkleGraphic.Dot(_x, _y, center);
                twinkleGraphic.Dot(_x + 1, _y, near);
                twinkleGraphic.Dot(_x + 2, _y, far);
                twinkleGraphic.Dot(_x, _y - 1, near);
                twinkleGraphic.Dot(_x, _y - 2, far);
                return;
            }

            float g = LootGlowShared.ShapeGlow(_shape) * _bright;
            if (PixelPainter.ShapeDot == _shape)
            {
                twinkleGraphic.Dot(_x, _y, WithGlow(particleMainRgb, hdrMax * g * 1.1f));
                return;
            }

            Color32 edge = WithGlow(particleMainRgb, hdrMax * g * 0.9f);
            Color32 core = WithGlow(particleCoreRgb, coreMax * g * 1.1f);
            if (PixelPainter.ShapePlus == _shape)
            {
                twinkleGraphic.Dot(_x, _y + 1, edge);
                twinkleGraphic.Dot(_x - 1, _y, edge);
                twinkleGraphic.Dot(_x, _y, core);
                twinkleGraphic.Dot(_x + 1, _y, edge);
                twinkleGraphic.Dot(_x, _y - 1, edge);
            }
            else
            {
                twinkleGraphic.Dot(_x - 1, _y + 1, edge);
                twinkleGraphic.Dot(_x + 1, _y + 1, edge);
                twinkleGraphic.Dot(_x, _y, core);
                twinkleGraphic.Dot(_x - 1, _y - 1, edge);
                twinkleGraphic.Dot(_x + 1, _y - 1, edge);
            }
        }

        private static Color32 WithGlow(Color32 _rgb, float _glow)
        {
            _rgb.a = ToByte(_glow / GlowMax);
            return _rgb;
        }

        // 슬롯 하나를 기둥 기준 위치(px)·크기(px)·각도(도)·정점 색으로 켠다. 지난번과 같은 값은 쓰지 않는다.
        private void SetSlot(int _slot, float _x, float _y, float _width, float _height, float _angle, Color32 _color)
        {
            Image image = slots[_slot];
            if (null == image) return;

            if (0 == _color.a)
            {
                HideSlot(_slot);
                return;
            }

            ref SlotState s = ref states[_slot];
            RectTransform rt = slotRects[_slot];

            if (false == s.bOn)
            {
                s.bOn = true;
                image.enabled = true;
            }

            Vector2 position = new Vector2(_x, _y);
            if (position.x != s.position.x || position.y != s.position.y)
            {
                s.position = position;
                rt.localPosition = new Vector3(_x, _y, 0.0f);
            }

            Vector2 baseSize = SlotBaseSize[_slot];
            Vector2 scale = new Vector2(_width / baseSize.x, _height / baseSize.y);
            if (scale.x != s.scale.x || scale.y != s.scale.y)
            {
                s.scale = scale;
                rt.localScale = new Vector3(scale.x, scale.y, 1.0f);
            }

            if (_angle != s.angle)
            {
                s.angle = _angle;
                rt.localRotation = Quaternion.Euler(0.0f, 0.0f, _angle);
            }

            if (_color.r != s.color.r || _color.g != s.color.g || _color.b != s.color.b || _color.a != s.color.a)
            {
                s.color = _color;
                image.color = _color;
            }
        }

        private void HideSlot(int _slot)
        {
            if (false == states[_slot].bOn) return;
            states[_slot].bOn = false;
            if (null != slots[_slot]) slots[_slot].enabled = false;
        }

        private void SetFlash(float _white)
        {
            if (null == flashImage) return;

            byte alpha = ToByte(_white);
            if (0 == alpha)
            {
                if (true == bFlashOn)
                {
                    bFlashOn = false;
                    flashImage.enabled = false;
                }
                return;
            }

            if (false == bFlashOn)
            {
                bFlashOn = true;
                flashImage.enabled = true;
            }

            Color32 color = flashColor;
            color.a = alpha;
            Color32 current = flashImage.color;
            if (current.r != color.r || current.g != color.g || current.b != color.b || current.a != color.a) flashImage.color = color;
        }

        private void HideAll()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                states[i].bOn = true;
                states[i].position = new Vector2(float.NaN, 0.0f);
                states[i].scale = new Vector2(float.NaN, 0.0f);
                states[i].angle = float.NaN;
                states[i].color = new Color32(0, 0, 0, 0);
                HideSlot(i);
            }

            bFlashOn = true;
            SetFlash(0.0f);
            lastTick = int.MinValue;
            if (null != moteGraphic) moteGraphic.ClearQuads();
            if (null != twinkleGraphic) twinkleGraphic.ClearQuads();
            bTwinklesOn = false;
        }

        // 유니티 라이프사이클 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
            if (false == bPlaying)
            {
                HideAll();
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (false == bPlaying || null == pillarRect || null == lootRect || null == lootImage) return;

            // 전리품이 꺼져 있으면(뽀잉 전) 기다린다
            if (false == lootRect.gameObject.activeInHierarchy) return;

            elapsed += Time.unscaledDeltaTime;
            SyncLootSprite();
            UpdateGlow();
        }

        private void OnDisable()
        {
            if (true == bInitialized) HideAll();
        }
    }
}
