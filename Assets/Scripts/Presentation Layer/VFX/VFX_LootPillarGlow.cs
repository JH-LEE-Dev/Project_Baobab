using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 마을 전리품 기둥 빛. 기둥 위 전리품 뒤에서 계속 은은하게 빛나는 유지 이펙트로, 기존 '등'(ItemAuraOrbitController 중앙 글로우)을 대신한다.
    /// 승인된 시뮬레이션 시안 2(https://claude.ai/artifact/4sMdnLqzcyVM8bs7L6BHQ5)를 그대로 옮겼다.
    ///  - 기둥 윗면 빛 웅덩이: 흙 윗면 중심의 납작한 빛(반투명 덮기 24x10px + 가산 22x9px, 가산은 숨쉬기)
    ///  - 위로 솟는 빛 줄기: 폭 20px, 높이 46px. 반투명 덮기 + 안쪽 결이 위로 흐르는 가산 심(SpriteBeamFlow)
    ///  - 뒤 빛 원반: 그림 중심의 반투명 덮기(지름 30px) + 가산(지름 26px)
    ///  - 실루엣 후광: 전시 프레임마다 구운 실루엣 블러를 가산, 2.6초 주기로 숨쉬기
    ///  - 떠오르는 픽셀 입자: 기둥마다 7개. 웅덩이에서 18~34px 떠오르며 `.` → `+`/`●` → `◆` `+` `●` `x` → `.`로 바뀐다. 입자 하나는 한 색·한 밝기
    ///  - 가장자리 반짝임: 그림 경계 바깥 5곳에서 2.2초마다 `.` `+` 5x5`+` `x` 순서로 켜졌다 줄어든다(가운데 한 칸만 밝게)
    ///
    /// 연결: LootDisplayObject가 아우라를 켜고 끄는 자리(SetLootDisplay, PrepareFirstReveal, PlayRevealAura)에서 Play/Stop을,
    /// 단계 색을 정하는 자리(ApplyGrowthVisual)에서 SetColor/ClearColor를 부른다. 진화 연출(VFX_LootEvolve)은 SetEvolveModulation으로
    /// 원반·후광을 조였다가 터뜨린다(기존 중앙 글로우와 같은 곡선).
    ///
    /// 그리는 층과 정렬: 기둥(P)과 전리품(P+4) 사이에 SortingGroup(Back, P+2) 하나로 묶어, 안쪽 순서를
    /// 웅덩이 덮기 → 웅덩이 가산 → 빛 줄기 덮기 → 빛 줄기 심 → 원반 덮기 → 원반 가산 → 후광 → 입자로 고정한다.
    /// 반짝임 메쉬만 전리품 앞(P+5)이다.
    ///
    /// 위치 기준: 부드러운 빛은 반올림 없이 기둥·전리품의 소수 위치를 따라간다. 입자·반짝임은 다른 절차적 이펙트처럼 월드 32PPU 격자에 그린다.
    /// 색: LootDisplayObject가 넘기는 HDR 아우라 색을 선형 값 그대로 쓴다(시뮬레이션과 같은 해석). 렌더러 색은 Unity가 선형으로 바꾸므로 gamma로 넘긴다.
    ///
    /// 픽셀 규칙: 선·링·윤곽을 픽셀로 그리지 않는다(부드러운 빛은 텍스처). 입자 모양만 가이드 6.5 예외 범위다.
    /// 성능: 입자·반짝임은 30fps 틱이 바뀔 때만 다시 그리고, 같은 그림이면 업로드를 건너뛴다. 화면 밖이면 갱신하지 않는다.
    /// 렌더러 Transform·색·소팅은 바뀔 때만 쓴다. 힙 할당은 초기화(메쉬) 때뿐이다.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(110)] // LootDisplayObject(100)가 전리품 위치를 쓴 뒤에 읽는다
    public class VFX_LootPillarGlow : MonoBehaviour
    {
        private const float PixelsPerUnit = 32.0f;
        private const float PixelUnit = 1.0f / 32.0f;
        private const float TickRate = 30.0f;
        private const float BreathPeriod = 2.6f;
        private const int MoteCount = 7;
        private const int TwinkleSpotCount = 5;
        private const int TwinklePeriod = 66;
        private const int TwinkleSpotStep = 13;
        private const int QuadCapacity = 128;
        private const float CullWidthPx = 48.0f;
        private const float CullHeightPx = 96.0f;

        /// <summary>입자 머티리얼(BrandWrapGlow)의 _GlowMax. 정점 알파 1이 이 배율이다. 빌더가 같은 값으로 머티리얼을 맞춘다.</summary>
        public const float ParticleGlowMax = 2.5f;

        // 부드러운 빛 슬롯 (SortingGroup 안 순서와 같다)
        public const int SoftPoolTint = 0;
        public const int SoftPoolAdd = 1;
        public const int SoftBeamTint = 2;
        public const int SoftBeamCore = 3;
        public const int SoftCoreTint = 4;
        public const int SoftCoreAdd = 5;
        public const int SoftHalo = 6;
        public const int SoftCount = 7;

        /// <summary>Back 그룹 안에서 입자 메쉬의 순서(부드러운 빛 다음).</summary>
        public const int MoteOrderInGroup = SoftCount;

        // 기둥 정렬(P) 기준 오프셋. 전리품은 LootDisplayObject.ApplySortingBasis가 P+4로 둔다.
        private const int BackGroupOrderOffset = 2;
        private const int TwinkleOrderOffset = 5;

        // 크기(px). 시뮬레이션 값 그대로.
        private const float PoolTintWidth = 24.0f;
        private const float PoolTintHeight = 10.0f;
        private const float PoolAddWidth = 22.0f;
        private const float PoolAddHeight = 9.0f;
        public const float BeamWidthPx = 20.0f;
        public const float BeamHeightPx = 46.0f;
        public const float BeamBelowPx = 2.0f;       // 빛 줄기 텍스처가 기둥 윗면 아래로 내려오는 길이
        private const float CoreTintDiameter = 30.0f;
        private const float CoreAddDiameter = 26.0f;

        private static readonly Color EvolveWhite = new Color(2.5f, 2.5f, 2.5f, 1.0f);

        private const int TwinkleNone = -1;
        private const int TwinklePlus5 = 5;
        // 반짝임 14틱 순서: . + +5 +5 +5 + + x x . . . . .
        private static readonly int[] TwinkleSequence =
        {
            PixelPainter.ShapeDot, PixelPainter.ShapePlus, TwinklePlus5, TwinklePlus5, TwinklePlus5, PixelPainter.ShapePlus, PixelPainter.ShapePlus,
            PixelPainter.ShapeCross, PixelPainter.ShapeCross, PixelPainter.ShapeDot, PixelPainter.ShapeDot, PixelPainter.ShapeDot, PixelPainter.ShapeDot, PixelPainter.ShapeDot,
        };


        private struct SoftState
        {
            public bool bOn;
            public Vector3 position;
            public Vector3 scale;
            public Color color;
        }

        private static PixelQuadBuffer sharedBuffer;
        private static readonly PixelPainter sharedPainter = new PixelPainter();

        //외부 의존성
        [Header("참조 (빌더가 채움)")]
        [SerializeField, Tooltip("전시 프레임별 후광·중심·경계 데이터")]
        private LootPillarGlowData data;
        [SerializeField, Tooltip("기둥 위 전리품 스프라이트. 후광 프레임 조회 키이자 원반·반짝임 위치 기준.")]
        private SpriteRenderer itemRenderer;
        [SerializeField, Tooltip("기둥 스프라이트. 정렬 기준(P).")]
        private SpriteRenderer pillarRenderer;
        [SerializeField, Tooltip("흙 윗면 중심(LootShadow). 웅덩이·빛 줄기·입자의 기준점.")]
        private Transform poolAnchor;
        [SerializeField, Tooltip("기둥과 전리품 사이 층을 묶는 SortingGroup(Back).")]
        private SortingGroup backGroup;
        [SerializeField, Tooltip("부드러운 빛 스프라이트 7개. 순서는 SoftPoolTint ~ SoftHalo 상수와 같다.")]
        private SpriteRenderer[] softRenderers = new SpriteRenderer[SoftCount];
        [SerializeField, Tooltip("떠오르는 입자(전리품 뒤, Back 그룹 안)")]
        private MeshRenderer moteRenderer;
        [SerializeField, Tooltip("가장자리 반짝임(전리품 앞)")]
        private MeshRenderer twinkleRenderer;

        [Header("색")]
        [SerializeField, ColorUsage(true, true), Tooltip("LootDisplayObject가 색을 정하지 못했을 때 쓰는 색(선형 HDR). 기본은 나무 단계 금색.")]
        private Color fallbackColor = new Color(2.0f, 1.576f, 0.580f, 1.0f);
        [SerializeField, Range(0.0f, 2.0f), Tooltip("입자·반짝임 발광에 곱하는 배율.")]
        private float particleGlow = 1.0f;

        //내부 의존성
        private Mesh moteMesh;
        private Mesh twinkleMesh;
        private readonly SoftState[] softStates = new SoftState[SoftCount];
        private readonly Vector2[] softSpriteSize = new Vector2[SoftCount];

        //상태 변수
        private bool bInitialized;
        private bool bPlaying;
        private bool bBegun;
        private bool bHasColor;
        private Color auraColor;
        private Color hdrColor;
        private Color tintColor;
        private Color coreColor;
        private Color32 moteRgb;
        private Color32 twinkleMainRgb;
        private Color32 twinkleCoreRgb;
        private float hdrMax;
        private float coreMax;
        private float elapsed;
        private float breathPhase;
        private float moteSeed;
        private int twinkleTickOffset;
        private int lastTick = int.MinValue;
        private int lastAnchorCellX;
        private int lastAnchorCellY;
        private int lastPivotCellX;
        private int lastPivotCellY;
        private float evolveScale = 1.0f;
        private float evolveIntensity = 1.0f;
        private float evolveWhite;
        private Sprite lastItemSprite;
        private int entryIndex = -1;
        private int appliedSortingLayerID = int.MinValue;
        private int appliedSortingOrder = int.MinValue;
        private ulong moteHash;
        private int moteVertexCount = -1;
        private ulong twinkleHash;
        private int twinkleVertexCount = -1;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>재생을 시작한다(이미 재생 중이면 그대로 둔다). 꺼진 오브젝트에서 불려도 켜질 때 이어서 재생한다.</summary>
        public void Play()
        {
            if (true == bPlaying) return;

            bPlaying = true;
            bBegun = false;
        }

        /// <summary>재생을 멈추고 모두 숨긴다.</summary>
        public void Stop()
        {
            bPlaying = false;
            bBegun = false;
            HideAll();
        }

        /// <summary>아우라 색(HDR, LootDisplayObject의 단계 색 또는 종류별 색)을 정한다.</summary>
        public void SetColor(Color _auraColor)
        {
            bHasColor = true;
            auraColor = _auraColor;
            BuildPalette();
        }

        /// <summary>색을 정하지 못했을 때: 인스펙터 fallbackColor로 돌아간다.</summary>
        public void ClearColor()
        {
            if (false == bHasColor && 0.0f < hdrMax) return;

            bHasColor = false;
            BuildPalette();
        }

        /// <summary>
        /// 진화 연출용 변조(ItemAuraOrbitController.SetEvolveModulation과 같은 뜻). _scale은 원반·후광 크기 배율, _intensity는 발광 배율,
        /// _whiteMix(0~1)는 원반·후광 색을 흰색 쪽으로 섞는 정도다. 매 프레임 불러도 할당이 없다.
        /// </summary>
        public void SetEvolveModulation(float _scale, float _intensity, float _whiteMix)
        {
            evolveScale = Mathf.Max(0.05f, _scale);
            evolveIntensity = Mathf.Max(0.0f, _intensity);
            evolveWhite = Mathf.Clamp01(_whiteMix);
        }

        public void ClearEvolveModulation()
        {
            evolveScale = 1.0f;
            evolveIntensity = 1.0f;
            evolveWhite = 0.0f;
        }

        // 일반 비즈니스 로직 및 내부 메서드

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            if (null == sharedBuffer) sharedBuffer = new PixelQuadBuffer(QuadCapacity);

            moteMesh = CreateMesh(moteRenderer, "LootPillarGlow_Motes");
            twinkleMesh = CreateMesh(twinkleRenderer, "LootPillarGlow_Twinkle");

            for (int i = 0; i < SoftCount && i < softRenderers.Length; i++)
            {
                SpriteRenderer sr = softRenderers[i];
                softSpriteSize[i] = null != sr && null != sr.sprite ? (Vector2)sr.sprite.bounds.size : Vector2.one;
            }

            BuildPalette();
        }

        private static Mesh CreateMesh(MeshRenderer _renderer, string _name)
        {
            if (null == _renderer) return null;

            MeshFilter filter = _renderer.GetComponent<MeshFilter>();
            if (null == filter) return null;

            Mesh mesh = new Mesh { name = _name };
            mesh.MarkDynamic();
            filter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, QuadCapacity);

            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            return mesh;
        }

        // 재생 시작: 숨쉬기 위상·입자 난수를 기둥마다 다르게 정하고 캐시를 비운다
        private void Begin()
        {
            bBegun = true;
            elapsed = 0.0f;
            lastTick = int.MinValue;
            moteVertexCount = -1;
            twinkleVertexCount = -1;
            appliedSortingLayerID = int.MinValue;
            appliedSortingOrder = int.MinValue;
            lastItemSprite = null;
            entryIndex = -1;

            Vector3 pos = transform.position;
            uint state = unchecked((uint)Mathf.FloorToInt(pos.x * 97.0f) * 2654435761u ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            breathPhase = (state & 0xFFFF) / 65535.0f * Mathf.PI * 2.0f;
            moteSeed = ((state >> 16) & 0x3FF) / 1023.0f * 10.0f;
            twinkleTickOffset = (int)((state >> 8) % TwinklePeriod);
        }

        // 아우라 색에서 시뮬레이션의 팔레트를 만든다: hdr(원색), tint(덮기, 정규화 색을 흰색 쪽으로 55%), core(밝은 심, 2.2 흰색 쪽으로 45%)
        private void BuildPalette()
        {
            Color c = true == bHasColor ? auraColor : fallbackColor;
            LootGlowShared.BuildPalette(c, out hdrColor, out tintColor, out coreColor, out Color normal, out hdrMax, out coreMax);

            // 정점 색은 셰이더가 그대로 출력하므로 선형 값을 그대로 넣고, 세기는 정점 알파(발광 배율)에 싣는다
            moteRgb = new Color(normal.r, normal.g, normal.b, 1.0f);
            twinkleMainRgb = moteRgb;
            twinkleCoreRgb = new Color(coreColor.r / coreMax, coreColor.g / coreMax, coreColor.b / coreMax, 1.0f);
            lastTick = int.MinValue;
        }

        private void SyncSorting()
        {
            int layer = pillarRenderer.sortingLayerID;
            int order = pillarRenderer.sortingOrder;
            if (layer == appliedSortingLayerID && order == appliedSortingOrder) return;

            appliedSortingLayerID = layer;
            appliedSortingOrder = order;

            if (null != backGroup)
            {
                backGroup.sortingLayerID = layer;
                backGroup.sortingOrder = order + BackGroupOrderOffset;
            }

            // 그룹 안 순서는 고정값(슬롯 번호). 레이어만 그룹과 맞춘다.
            for (int i = 0; i < SoftCount && i < softRenderers.Length; i++)
            {
                if (null == softRenderers[i]) continue;
                softRenderers[i].sortingLayerID = layer;
                softRenderers[i].sortingOrder = i;
            }

            if (null != moteRenderer)
            {
                moteRenderer.sortingLayerID = layer;
                moteRenderer.sortingOrder = MoteOrderInGroup;
            }

            if (null != twinkleRenderer)
            {
                twinkleRenderer.sortingLayerID = layer;
                twinkleRenderer.sortingOrder = order + TwinkleOrderOffset;
            }
        }

        // 전리품 프레임이 바뀌면 데이터 항목과 후광 스프라이트를 바꾼다
        private void SyncItemFrame()
        {
            Sprite sprite = itemRenderer.sprite;
            if (sprite == lastItemSprite) return;

            lastItemSprite = sprite;
            entryIndex = null != data ? data.IndexOf(sprite) : -1;

            SpriteRenderer halo = softRenderers[SoftHalo];
            if (0 <= entryIndex && null != halo)
            {
                Sprite haloSprite = data.Get(entryIndex).halo;
                if (null != haloSprite && halo.sprite != haloSprite) halo.sprite = haloSprite;
                if (null == haloSprite) entryIndex = -1;
            }
        }

        // ---------- 부드러운 빛 ----------

        private void UpdateSoftLights(float _anchorX, float _anchorY, float _centerX, float _centerY, Vector3 _itemPivot, Vector3 _itemScale)
        {
            float breathe = 0.5f + 0.5f * Mathf.Sin(elapsed * (Mathf.PI * 2.0f / BreathPeriod) + breathPhase);
            float beamLevel = 0.85f + 0.15f * breathe;

            // 웅덩이
            SetSoft(SoftPoolTint, _anchorX, _anchorY, PoolTintWidth, PoolTintHeight, WithAlpha(tintColor, 0.45f));
            SetSoft(SoftPoolAdd, _anchorX, _anchorY, PoolAddWidth, PoolAddHeight, WithAlpha(hdrColor, 0.55f + 0.15f * breathe));

            // 빛 줄기(피벗 = 기둥 윗면)
            SetSoft(SoftBeamTint, _anchorX, _anchorY, BeamWidthPx, BeamHeightPx + BeamBelowPx, WithAlpha(tintColor, 0.42f * beamLevel));
            SetSoft(SoftBeamCore, _anchorX, _anchorY, BeamWidthPx, BeamHeightPx + BeamBelowPx, WithAlpha(hdrColor * 0.675f, beamLevel));

            // 원반·후광: 진화 변조(크기·세기·흰색)를 받는다
            Color coreTint = Color.Lerp(tintColor, Color.white, evolveWhite);
            Color glow = Color.Lerp(hdrColor * evolveIntensity, EvolveWhite, evolveWhite);
            SetSoft(SoftCoreTint, _centerX, _centerY, CoreTintDiameter * evolveScale, CoreTintDiameter * evolveScale, WithAlpha(coreTint, 0.28f));
            SetSoft(SoftCoreAdd, _centerX, _centerY, CoreAddDiameter * evolveScale, CoreAddDiameter * evolveScale, WithAlpha(glow, 0.28f + 0.12f * breathe));

            if (0 <= entryIndex)
            {
                // 후광 스프라이트는 전시 프레임과 같은 PPU·피벗이라 전리품과 같은 자리·크기로 겹친다
                SetSoftRaw(SoftHalo, new Vector3(_itemPivot.x, _itemPivot.y, transform.position.z),
                    new Vector3(_itemScale.x * evolveScale, _itemScale.y * evolveScale, 1.0f), WithAlpha(glow, 0.42f + 0.28f * breathe));
            }
            else
            {
                HideSoft(SoftHalo);
            }
        }

        // 슬롯 하나를 월드 위치(px)·크기(px)·선형 색으로 켠다
        private void SetSoft(int _slot, float _xPx, float _yPx, float _widthPx, float _heightPx, Color _linearColor)
        {
            Vector2 spriteSize = softSpriteSize[_slot];
            Vector3 position = new Vector3(_xPx * PixelUnit, _yPx * PixelUnit, transform.position.z);
            Vector3 scale = new Vector3(_widthPx * PixelUnit / spriteSize.x, _heightPx * PixelUnit / spriteSize.y, 1.0f);
            SetSoftRaw(_slot, position, scale, _linearColor);
        }

        // 지난번과 같은 값은 쓰지 않는다(가이드 10.6)
        private void SetSoftRaw(int _slot, Vector3 _position, Vector3 _scale, Color _linearColor)
        {
            SpriteRenderer sr = softRenderers[_slot];
            if (null == sr) return;

            if (0.002f >= _linearColor.a)
            {
                HideSoft(_slot);
                return;
            }

            ref SoftState s = ref softStates[_slot];

            if (false == s.bOn)
            {
                s.bOn = true;
                sr.enabled = true;
            }

            if (_position.x != s.position.x || _position.y != s.position.y || _position.z != s.position.z)
            {
                s.position = _position;
                sr.transform.position = _position;
            }

            if (_scale.x != s.scale.x || _scale.y != s.scale.y)
            {
                s.scale = _scale;
                sr.transform.localScale = _scale;
            }

            if (_linearColor.r != s.color.r || _linearColor.g != s.color.g || _linearColor.b != s.color.b || _linearColor.a != s.color.a)
            {
                s.color = _linearColor;
                sr.color = ToRendererColor(_linearColor);
            }
        }

        // 선형 색 공간에서는 Unity가 렌더러 색을 선형으로 바꾸므로, 원하는 선형 값이 나오도록 gamma로 바꿔 넘긴다
        private static Color ToRendererColor(Color _linear)
        {
            if (ColorSpace.Linear != QualitySettings.activeColorSpace) return _linear;

            return new Color(Mathf.LinearToGammaSpace(_linear.r), Mathf.LinearToGammaSpace(_linear.g), Mathf.LinearToGammaSpace(_linear.b), _linear.a);
        }

        private static Color WithAlpha(Color _color, float _alpha)
        {
            _color.a = _alpha;
            return _color;
        }

        private void HideSoft(int _slot)
        {
            if (false == softStates[_slot].bOn) return;
            softStates[_slot].bOn = false;
            if (null != softRenderers[_slot]) softRenderers[_slot].enabled = false;
        }

        private void HideAll()
        {
            for (int i = 0; i < SoftCount; i++)
            {
                softStates[i].bOn = true;
                softStates[i].position = new Vector3(float.NaN, 0.0f, 0.0f);
                softStates[i].color = new Color(float.NaN, 0.0f, 0.0f, 0.0f);
                HideSoft(i);
            }

            moteVertexCount = -1;
            twinkleVertexCount = -1;
            lastTick = int.MinValue;
            if (null != moteRenderer && true == moteRenderer.enabled) moteRenderer.enabled = false;
            if (null != twinkleRenderer && true == twinkleRenderer.enabled) twinkleRenderer.enabled = false;
        }

        // ---------- 픽셀 입자 ----------

        // 입자·반짝임을 다시 그린다. _anchorCellX/_anchorCellY: 흙 윗면 기준 칸(그 칸의 위 변이 기둥 윗면), _pivotCellX/_pivotCellY: 전리품 피벗 칸
        private void RebuildPixels(int _tick, int _anchorCellX, int _anchorCellY, int _pivotCellX, int _pivotCellY)
        {
            // 떠오르는 입자(전리품 뒤)
            if (null != moteMesh)
            {
                sharedPainter.Begin(sharedBuffer, moteRenderer.transform.worldToLocalMatrix, moteRenderer.transform.position.z);
                for (int i = 0; i < MoteCount; i++) DrawMote(i, _tick, _anchorCellX, _anchorCellY);
                sharedPainter.Commit(moteMesh, moteRenderer, ref moteHash, ref moteVertexCount);
            }

            // 가장자리 반짝임(전리품 앞)
            if (null != twinkleMesh)
            {
                sharedPainter.Begin(sharedBuffer, twinkleRenderer.transform.worldToLocalMatrix, twinkleRenderer.transform.position.z);
                if (0 <= entryIndex) DrawTwinkles(_tick, _pivotCellX, _pivotCellY);
                sharedPainter.Commit(twinkleMesh, twinkleRenderer, ref twinkleHash, ref twinkleVertexCount);
            }
        }

        // 떠오르는 입자: 공용 닫힌 식(LootGlowShared.EvaluateMote, 마을 기둥 크기)
        private void DrawMote(int _index, int _tick, int _anchorCellX, int _anchorCellY)
        {
            LootGlowShared.MoteState m = LootGlowShared.EvaluateMote(_index, _tick, moteSeed, LootGlowShared.VillageMotes);
            if (false == m.bVisible) return;

            // 입자 하나 = 한 색·한 밝기
            float glow = hdrMax * LootGlowShared.ShapeGlow(m.shape) * 1.1f * m.bright * particleGlow;
            Color32 color = moteRgb;
            color.a = ToGlowAlpha(glow);

            DrawSimShape(m.shape, _anchorCellX + m.x, _anchorCellY - m.yDown, color);
        }

        private void DrawTwinkles(int _tick, int _pivotCellX, int _pivotCellY)
        {
            LootPillarGlowData.Entry entry = data.Get(entryIndex);
            int minX = (int)entry.bounds.x;
            int minY = (int)entry.bounds.y;
            int maxX = (int)entry.bounds.z;
            int maxY = (int)entry.bounds.w;
            int centerX = LootGlowShared.RoundHalfUp(entry.centroid.x);

            for (int k = 0; k < TwinkleSpotCount; k++)
            {
                int lt = (_tick + twinkleTickOffset + k * TwinkleSpotStep) % TwinklePeriod;
                int shape = lt < TwinkleSequence.Length ? TwinkleSequence[lt] : TwinkleNone;
                if (TwinkleNone == shape) continue;

                int sx;
                int sy;
                switch (k)
                {
                    case 0: sx = minX - 1; sy = maxY - 2; break;
                    case 1: sx = maxX + 2; sy = maxY - 4; break;
                    case 2: sx = minX - 2; sy = Mathf.FloorToInt((minY + maxY) * 0.5f); break;
                    case 3: sx = centerX + 1; sy = maxY + 2; break;
                    default: sx = maxX + 1; sy = minY + 3; break;
                }

                DrawTwinkle(shape, _pivotCellX + sx, _pivotCellY + sy);
            }
        }

        // 반짝임: 시뮬레이션 drawShape/drawPlus5와 같은 밝기(세기 1.1). `+` `x` 5x5`+`만 가운데 한 칸을 밝은 색으로.
        private void DrawTwinkle(int _shape, int _x, int _y)
        {
            const float Bright = 1.1f;

            if (TwinklePlus5 == _shape)
            {
                Color32 center = WithGlow(twinkleCoreRgb, coreMax * Bright * 1.1f);
                Color32 near = WithGlow(twinkleMainRgb, hdrMax * Bright * 0.85f);
                Color32 far = WithGlow(twinkleMainRgb, hdrMax * Bright * 0.55f);
                sharedPainter.Dot(_x, _y + 2, far);
                sharedPainter.Dot(_x, _y + 1, near);
                sharedPainter.Dot(_x - 2, _y, far);
                sharedPainter.Dot(_x - 1, _y, near);
                sharedPainter.Dot(_x, _y, center);
                sharedPainter.Dot(_x + 1, _y, near);
                sharedPainter.Dot(_x + 2, _y, far);
                sharedPainter.Dot(_x, _y - 1, near);
                sharedPainter.Dot(_x, _y - 2, far);
                return;
            }

            float g = LootGlowShared.ShapeGlow(_shape) * Bright;
            if (PixelPainter.ShapeDot == _shape)
            {
                sharedPainter.Dot(_x, _y, WithGlow(twinkleMainRgb, hdrMax * g * 1.1f));
                return;
            }

            Color32 edge = WithGlow(twinkleMainRgb, hdrMax * g * 0.9f);
            Color32 core = WithGlow(twinkleCoreRgb, coreMax * g * 1.1f);
            if (PixelPainter.ShapePlus == _shape)
            {
                sharedPainter.Dot(_x, _y + 1, edge);
                sharedPainter.Dot(_x - 1, _y, edge);
                sharedPainter.Dot(_x, _y, core);
                sharedPainter.Dot(_x + 1, _y, edge);
                sharedPainter.Dot(_x, _y - 1, edge);
            }
            else
            {
                sharedPainter.Dot(_x - 1, _y + 1, edge);
                sharedPainter.Dot(_x + 1, _y + 1, edge);
                sharedPainter.Dot(_x, _y, core);
                sharedPainter.Dot(_x - 1, _y - 1, edge);
                sharedPainter.Dot(_x + 1, _y - 1, edge);
            }
        }

        // 시뮬레이션 모양: ◆ 5x5 마름모, + x 3x3, ● 2x2(왼쪽 위 칸 기준으로 오른쪽·아래), . 1px
        private static void DrawSimShape(int _shape, int _x, int _y, Color32 _color)
        {
            if (PixelPainter.ShapeCircle == _shape)
            {
                sharedPainter.Rect(_x, _y - 1, _x + 2, _y + 1, _color);
                return;
            }

            sharedPainter.Shape(_shape, _x, _y, _color);
        }

        private Color32 WithGlow(Color32 _rgb, float _glow)
        {
            _rgb.a = ToGlowAlpha(_glow * particleGlow);
            return _rgb;
        }

        // 발광 배율(BrandWrapGlow: 0 ~ ParticleGlowMax)을 정점 알파로
        private static byte ToGlowAlpha(float _glow)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow / ParticleGlowMax) * 255.0f);
        }

        // 유니티 라이프사이클 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            EnsureInitialized();
            bBegun = false;
            HideAll();
        }

        private void LateUpdate()
        {
            if (false == bPlaying || null == itemRenderer || null == pillarRenderer || null == poolAnchor) return;

            if (false == bBegun) Begin();
            else elapsed += Time.deltaTime;

            SyncSorting();
            SyncItemFrame();

            Vector3 anchor = poolAnchor.position;
            float anchorX = anchor.x * PixelsPerUnit;
            float anchorY = anchor.y * PixelsPerUnit;

            Transform itemTransform = itemRenderer.transform;
            Vector3 itemPivot = itemTransform.position;
            Vector3 itemScale = itemTransform.lossyScale;
            float centerX;
            float centerY;
            if (0 <= entryIndex)
            {
                Vector2 centroid = data.Get(entryIndex).centroid;
                centerX = itemPivot.x * PixelsPerUnit + centroid.x * itemScale.x;
                centerY = itemPivot.y * PixelsPerUnit + centroid.y * itemScale.y;
            }
            else
            {
                Vector3 c = itemRenderer.bounds.center;
                centerX = c.x * PixelsPerUnit;
                centerY = c.y * PixelsPerUnit;
            }

            // 화면 밖이면 갱신하지 않는다(스프라이트는 렌더러 컬링이 처리하고, 픽셀 메쉬는 끈다)
            Vector3 cullCenter = new Vector3(anchor.x, anchor.y + CullHeightPx * 0.5f * PixelUnit - 4.0f * PixelUnit, anchor.z);
            Vector3 cullSize = new Vector3(CullWidthPx * PixelUnit + 2.0f * PixelVfxCulling.MarginUnits, CullHeightPx * PixelUnit + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(cullCenter, cullSize))
            {
                moteVertexCount = -1;
                twinkleVertexCount = -1;
                lastTick = int.MinValue;
                if (null != moteRenderer && true == moteRenderer.enabled) moteRenderer.enabled = false;
                if (null != twinkleRenderer && true == twinkleRenderer.enabled) twinkleRenderer.enabled = false;
                return;
            }

            UpdateSoftLights(anchorX, anchorY, centerX, centerY, itemPivot, itemScale);

            // 입자·반짝임은 30fps 틱이나 기준 칸(진화 흔들림·뽀잉으로 전리품·기둥이 움직일 때)이 바뀔 때만 다시 그린다
            int tick = Mathf.FloorToInt(elapsed * TickRate);
            int anchorCellX = Mathf.FloorToInt(anchorX + 0.5f);
            int anchorCellY = Mathf.FloorToInt(anchorY + 0.5f) - 1;
            int pivotCellX = Mathf.FloorToInt(itemPivot.x * PixelsPerUnit + 0.5f);
            int pivotCellY = Mathf.FloorToInt(itemPivot.y * PixelsPerUnit + 0.5f);
            if (tick == lastTick && anchorCellX == lastAnchorCellX && anchorCellY == lastAnchorCellY && pivotCellX == lastPivotCellX && pivotCellY == lastPivotCellY) return;

            lastTick = tick;
            lastAnchorCellX = anchorCellX;
            lastAnchorCellY = anchorCellY;
            lastPivotCellX = pivotCellX;
            lastPivotCellY = pivotCellY;
            RebuildPixels(tick, anchorCellX, anchorCellY, pivotCellX, pivotCellY);
        }

        private void OnDisable()
        {
            HideAll();
        }

        private void OnDestroy()
        {
            DestroyMesh(ref moteMesh);
            DestroyMesh(ref twinkleMesh);
        }

        private static void DestroyMesh(ref Mesh _mesh)
        {
            if (null == _mesh) return;
            Destroy(_mesh);
            _mesh = null;
        }

        private void OnValidate()
        {
            particleGlow = Mathf.Clamp(particleGlow, 0.0f, 2.0f);

#if UNITY_EDITOR
            if (true == bInitialized) BuildPalette();
#endif
        }
    }
}
