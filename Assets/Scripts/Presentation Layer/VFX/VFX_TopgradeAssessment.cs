using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 제재소 특등품 평가(가격 2배) 이펙트. 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다
    /// (VFX_BrandStampBurst, WhirlwindVFX와 같은 방식, 정점 알파 = HDR 발광 세기인 BrandWrapGlow 셰이더).
    /// 가공 라인 하나에 하나씩, TopgradeAssessmentPresenter가 런타임에 자식으로 만들어 부른다.
    ///
    /// 장면
    /// - 등장(PlayReveal): 커터 톱날이 금을 긁은 듯 톱날에서 불티 줄기가 벨트 쪽으로 튀고, 쌍둥이가 나오는 출구에서는
    ///   금빛 톱밥이 뿜어진다. 동시에 커터 윗면에서 숫자 시트의 "×2"와 움직이는 폭발 도형이 각각 튀어나온 뒤
    ///   1초 안에 빠르게 페이드아웃한다. 다시 나오면 팝 연출을 처음부터 재생한다.
    /// - 착지(PlayLand): 원목 등 위에 내려앉는 순간 작은 별이 반짝이고 양옆으로 먼지가 튄다.
    /// - 업혀 가기(EmitRideSparkle): 쌍둥이 둘레에서 금빛 반짝임이 하나씩 피어올라 사라진다(절제해서 드문드문).
    /// - 쾅(PlayStamp): 평가기에서 큰 십자 섬광 → 점선 충격파 → 금화·금가루 분수가 솟았다 떨어진다.
    ///   콤보(연달아 터짐)일수록 분수와 충격파가 조금씩 커진다.
    ///
    /// 글자는 숫자와 곱셈 기호뿐이라 언어와 무관하다(현지화 대상 아님).
    /// 반투명 페이드 없이 계단식 크기 변화·점멸로만 사라진다. 고정 크기 배열만 써서 재생 중 힙 할당이 없다.
    /// 그림일 뿐이라 판정·돈·벨트와는 무관하다. 모든 요소는 나무·원목 위(FlyingItem)에 그린다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_TopgradeAssessment : MonoBehaviour
    {
        // //공용 상수

        private const float PixelsPerUnit = 32f;
        private const float TwoPi = Mathf.PI * 2f;

        private const string ShaderName = "Custom/VFX/BrandWrapGlow";
        private const string SortingLayerName = "FlyingItem";
        private const int SortingOrder = 0;

        // 정점 알파 0~1 -> 발광 배율 GlowMin~GlowMax. 카메라 블룸 임계(1.345)를 흰색 섬광·반짝임 중심만 넘기고
        // 글자 본체(금색)는 넘지 않게 해서 번지지 않고 또렷하게 읽히도록 한다.
        private const float GlowMin = 1f;
        private const float GlowMax = 2.4f;

        // 팔레트: 흰 -> 크림 -> 금 -> 주황 -> 진한 테두리. 화면에 보일 색(sRGB)으로 적고, 프로젝트가 리니어 색 공간이면
        // 셰이더가 정점 색을 변환 없이 쓰므로 처음 한 번 리니어 값으로 바꿔 둔다(안 바꾸면 금색이 크림색처럼 바래 보인다).
        // Unity API를 쓰므로 정적 초기화가 아니라 EnsureShared(메인 스레드)에서 채운다.
        private static readonly Color32 SrgbCream = new Color32(255, 241, 176, 255);
        private static readonly Color32 SrgbGold = new Color32(255, 204, 72, 255);
        private static readonly Color32 SrgbOrange = new Color32(238, 136, 38, 255);
        private static readonly Color32 SrgbCoinEdge = new Color32(206, 128, 28, 255);
        private static readonly Color32 SrgbOutline = new Color32(82, 34, 16, 255);

        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static Color32 ColorCream;
        private static Color32 ColorGold;
        private static Color32 ColorOrange;
        private static Color32 ColorCoinEdge;
        private static Color32 ColorOutline;
        private static bool bPaletteReady;

        // //섬광

        private const int FlashCapacity = 8;
        private const float FlashStage0 = 0.03f;
        private const float FlashStage1 = 0.06f;
        private const float FlashEnd = 0.09f;

        // //점선 충격파

        private const int RingCapacity = 4;
        private const float RingStepTime = 0.035f;
        private const int RingStepCount = 10;
        private const int RingDotCount = 20;
        private const float RingSquashY = 0.55f;
        private const float RingStartRadius = 8f;
        private const float RingBaseMaxRadius = 34f;
        private const float RingRadiusPerCombo = 3f;

        // //금가루·금화

        private const int DustCapacity = 192;
        private const int KindDot = 0;
        private const int KindSparkle = 1;
        private const int KindCoin = 2;
        private const int KindTwinkle = 3;
        private const int KindStreak = 4;   // 불티 줄기: 날아가는 방향 반대로 꼬리를 끈다
        private const int KindChip = 5;     // 톱밥 조각: 2x1 막대가 돌며 떨어진다

        private const int FountainBaseCount = 18;
        private const int FountainPerCombo = 3;
        private const int FountainComboCap = 4;
        private const int FountainSecondWaveCount = 8;
        private const float FountainSecondWaveDelay = 0.06f;
        private const float FountainGravity = 300f;     // px/s^2

        // 커터 등장: 톱날 불티 줄기 + 출구 톱밥
        private const int RevealStreakCount = 12;
        private const float RevealStreakSpread = 0.75f;  // 진행 방향 좌우로 이만큼(라디안) 부채꼴
        private const float RevealStreakGravity = 260f;
        private const int RevealChipCount = 9;
        private const int RevealSparkleCount = 4;
        private const float RevealChipGravity = 240f;

        // 업혀 가기 반짝임: 쌍둥이 둘레에서 피어올라 계단식으로 커졌다 작아진다.
        private const float TwinkleLife = 0.34f;
        private const float TwinkleRise = 14f;          // px/s

        // //×2 글자

        private TopgradePopup popup;

        public void ConfigurePopup(Sprite _times, Sprite _two)
        {
            if (popup != null || _times == null || _two == null) return;
            GameObject child = new GameObject("TopgradePopup");
            child.transform.SetParent(transform, false);
            popup = child.AddComponent<TopgradePopup>();
            popup.Initialize(_times, _two);
        }

        // //공유 자원

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(1024);
        private static readonly PixelPainter painter = new PixelPainter();
        private static Material sharedMaterial;
        private static bool bShaderMissingWarned;

        // //인스턴스 상태

        private struct Flash
        {
            public bool bActive;
            public bool bFresh;
            public bool bBig;
            public int x;
            public int y;
            public float age;
        }

        private struct Ring
        {
            public bool bActive;
            public bool bFresh;
            public int x;
            public int y;
            public float age;
            public float maxRadius;
        }

        private struct Dust
        {
            public bool bActive;
            public bool bFresh;
            public float x;           // 월드 px
            public float y;
            public float vx;          // px/s
            public float vy;
            public float drag;
            public float gravity;
            public float age;         // 음수면 아직 나오기 전(지연)
            public float life;
            public int kind;
            public int seed;
            public Color32 color;
        }

        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ulong meshHash;
        private int meshVertexCount = -1;

        private readonly Flash[] flashes = new Flash[FlashCapacity];
        private readonly Ring[] rings = new Ring[RingCapacity];
        private readonly Dust[] dusts = new Dust[DustCapacity];
        private int flashCursor;
        private int ringCursor;
        private int dustCursor;


        private bool bAnyActive;
        private uint randomState = 1u;

        // //퍼블릭

        /// <summary>
        /// _parent 아래에 이펙트를 하나 만든다. 셰이더를 찾지 못하면 null(이펙트 없이 진행).
        /// </summary>
        public static VFX_TopgradeAssessment Create(Transform _parent)
        {
            if (false == EnsureShared()) return null;

            GameObject go = new GameObject("TopgradeAssessmentVFX");
            go.transform.SetParent(_parent, false);
            VFX_TopgradeAssessment vfx = go.AddComponent<VFX_TopgradeAssessment>();
            vfx.Initialize();
            return vfx;
        }

        /// <summary>
        /// 쌍둥이 원목이 커터에서 튀어나오는 순간.
        /// _twinPos: 쌍둥이가 출발하는 자리(커터 출구), _bladePos: 커터 톱날(불티가 튀는 자리),
        /// _labelPos: ×2 글자 바닥 가운데(커터 윗면 바로 위), _outDir: 출고 방향(커터 → 평가기, 길이 무관).
        /// </summary>
        public void PlayReveal(Vector3 _twinPos, Vector3 _bladePos, Vector3 _labelPos, Vector2 _outDir)
        {
            Vector2 dir = _outDir.sqrMagnitude > 0.0001f ? _outDir.normalized : new Vector2(0.9f, 0.45f);

            // 1) 톱날: 짧은 섬광 + 불티 줄기. 출고 방향과 위쪽 사이로 부채꼴로 튄다(그라인더 불꽃처럼 꼬리를 끈다).
            int bx = ToPixel(_bladePos.x);
            int by = ToPixel(_bladePos.y);
            AddFlash(bx, by, false);

            float baseAngle = Mathf.Atan2(dir.y + 0.9f, dir.x);
            for (int i = 0; i < RevealStreakCount; i++)
            {
                float u = (i + Rand01()) / RevealStreakCount;
                float angle = baseAngle + Mathf.Lerp(-RevealStreakSpread, RevealStreakSpread, u);
                float speed = Mathf.Lerp(120f, 230f, Rand01());
                Color32 color = Rand01() < 0.35f ? ColorWhite : (Rand01() < 0.6f ? ColorCream : ColorGold);
                AddDust(bx, by, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 3f, RevealStreakGravity,
                    Rand01() * 0.03f, Mathf.Lerp(0.24f, 0.42f, Rand01()), KindStreak, color);
            }

            // 2) 출구: 작은 섬광 + 금빛 톱밥이 출고 방향으로 뿜어졌다 떨어지고, 반짝이 몇 알이 섞인다.
            int x = ToPixel(_twinPos.x);
            int y = ToPixel(_twinPos.y);
            AddFlash(x, y, false);

            float exitAngle = Mathf.Atan2(dir.y + 0.5f, dir.x);
            for (int i = 0; i < RevealChipCount; i++)
            {
                float angle = exitAngle + Mathf.Lerp(-0.7f, 0.7f, (i + Rand01()) / RevealChipCount);
                float speed = Mathf.Lerp(50f, 110f, Rand01());
                float roll = Rand01();
                Color32 color = roll < 0.45f ? ColorGold : (roll < 0.8f ? ColorCream : ColorOrange);
                AddDust(x + (Rand01() - 0.5f) * 4f, y, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 2.2f, RevealChipGravity,
                    0f, Mathf.Lerp(0.34f, 0.52f, Rand01()), KindChip, color);
            }

            for (int i = 0; i < RevealSparkleCount; i++)
            {
                float angle = Mathf.Lerp(0.35f, Mathf.PI - 0.35f, (i + Rand01()) / RevealSparkleCount);
                float speed = Mathf.Lerp(45f, 85f, Rand01());
                AddDust(x, y, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 2.5f, 160f,
                    0.02f, Mathf.Lerp(0.3f, 0.45f, Rand01()), KindSparkle, PickSparkColor());
            }

            // 3) ×2: 커터 윗면에서 솟아 나온다.
            if (popup != null) popup.Play(_labelPos);
            bAnyActive = true;
        }

        /// <summary>
        /// ×2 글자만 띄운다(섬광·가루 없이). _labelPos는 글자 바닥 가운데. 원목 드랍 잭팟(LogItemController)이
        /// 특등품과 같은 글자를 쓰려고 부른다. 이미 떠 있으면 처음부터 다시 솟는다.
        /// </summary>
        public void PlayLabel(Vector3 _labelPos)
        {
            if (popup != null) popup.Play(_labelPos, false);
        }

        /// <summary>쌍둥이가 원목 등 위에 내려앉는 순간. _worldPos는 쌍둥이 중심.</summary>
        public void PlayLand(Vector3 _worldPos)
        {
            int x = ToPixel(_worldPos.x);
            int y = ToPixel(_worldPos.y);
            AddFlash(x, y + 4, false);

            // 양옆으로 먼지 두 줄기
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 2; k++)
                {
                    AddDust(x + side * 6, y - 3, side * Mathf.Lerp(30f, 55f, Rand01()), Mathf.Lerp(8f, 22f, Rand01()), 6f, 40f,
                        0f, Mathf.Lerp(0.16f, 0.24f, Rand01()), KindDot, ColorCream);
                }
            }
        }

        /// <summary>
        /// 업혀 가는 쌍둥이 둘레에 반짝임 하나. _worldPos는 쌍둥이 중심, _halfWidth/_halfHeight(유닛)는 반짝임이 생길 범위.
        /// </summary>
        public void EmitRideSparkle(Vector3 _worldPos, float _halfWidth, float _halfHeight)
        {
            float x = _worldPos.x * PixelsPerUnit + (Rand01() * 2f - 1f) * _halfWidth * PixelsPerUnit;
            float y = _worldPos.y * PixelsPerUnit + (Rand01() * 2f - 1f) * _halfHeight * PixelsPerUnit;
            Color32 color = Rand01() < 0.55f ? ColorGold : ColorWhite;
            AddDust(x, y, 0f, TwinkleRise, 0f, 0f, 0f, TwinkleLife * Mathf.Lerp(0.85f, 1.15f, Rand01()), KindTwinkle, color);
        }

        /// <summary>
        /// 평가기 쾅. _impactPos는 섬광·충격파의 중심(평가기 몸통), _fountainPos는 금화 분수가 솟는 자리(평가기 윗면). _combo는 1부터.
        /// </summary>
        public void PlayStamp(Vector3 _impactPos, Vector3 _fountainPos, int _combo)
        {
            int x = ToPixel(_impactPos.x);
            int y = ToPixel(_impactPos.y);
            int comboStep = Mathf.Clamp(_combo - 1, 0, FountainComboCap);

            AddFlash(x, y, true);
            AddRing(x, y, RingBaseMaxRadius + RingRadiusPerCombo * comboStep);

            int fountainY = ToPixel(_fountainPos.y);
            int count = FountainBaseCount + FountainPerCombo * comboStep;
            SpawnFountain(x, fountainY, count, 0f, 1f);
            SpawnFountain(x, fountainY, FountainSecondWaveCount, FountainSecondWaveDelay, 0.75f);
        }

        /// <summary>그리던 것을 모두 지운다(맵을 떠나거나 라인이 꺼질 때).</summary>
        public void ClearAll()
        {
            for (int i = 0; i < FlashCapacity; i++) flashes[i].bActive = false;
            for (int i = 0; i < RingCapacity; i++) rings[i].bActive = false;
            for (int i = 0; i < DustCapacity; i++) dusts[i].bActive = false;
            if (popup != null) popup.Clear();
            bAnyActive = false;
            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        // //초기화

        private static bool EnsureShared()
        {

            if (false == bPaletteReady) BuildPalette();
            if (null != sharedMaterial) return true;

            Shader shader = Shader.Find(ShaderName);
            if (null == shader)
            {
                if (false == bShaderMissingWarned)
                {
                    bShaderMissingWarned = true;
                    Debug.LogWarning("[VFX_TopgradeAssessment] 셰이더 " + ShaderName + "를 찾지 못해 특등품 이펙트를 건너뜁니다.");
                }
                return false;
            }

            sharedMaterial = new Material(shader) { name = "M_TopgradeAssessment (Runtime)" };
            sharedMaterial.SetFloat("_GlowMin", GlowMin);
            sharedMaterial.SetFloat("_GlowMax", GlowMax);
            return true;
        }

        private void Initialize()
        {
            mesh = new Mesh { name = "VFX_TopgradeAssessment" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = sharedMaterial;
            meshRenderer.sortingLayerName = SortingLayerName;
            meshRenderer.sortingOrder = SortingOrder;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;

            randomState = unchecked((uint)Random.Range(1, int.MaxValue) * 2654435761u) | 1u;
        }

        private static void BuildPalette()
        {
            bool bLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            ColorCream = ToDisplay(SrgbCream, bLinear);
            ColorGold = ToDisplay(SrgbGold, bLinear);
            ColorOrange = ToDisplay(SrgbOrange, bLinear);
            ColorCoinEdge = ToDisplay(SrgbCoinEdge, bLinear);
            ColorOutline = ToDisplay(SrgbOutline, bLinear);
            bPaletteReady = true;
        }

        private static Color32 ToDisplay(Color32 _srgb, bool _bLinear)
        {
            if (false == _bLinear) return _srgb;
            return new Color32(ToLinearByte(_srgb.r), ToLinearByte(_srgb.g), ToLinearByte(_srgb.b), _srgb.a);
        }

        private static byte ToLinearByte(byte _value)
        {
            return (byte)Mathf.RoundToInt(Mathf.GammaToLinearSpace(_value / 255f) * 255f);
        }

        // //생성 도우미

        private void AddFlash(int _x, int _y, bool _bBig)
        {
            flashes[flashCursor] = new Flash { bActive = true, bFresh = true, bBig = _bBig, x = _x, y = _y, age = 0f };
            flashCursor = (flashCursor + 1) % FlashCapacity;
            bAnyActive = true;
        }

        private void AddRing(int _x, int _y, float _maxRadius)
        {
            rings[ringCursor] = new Ring { bActive = true, bFresh = true, x = _x, y = _y, age = 0f, maxRadius = _maxRadius };
            ringCursor = (ringCursor + 1) % RingCapacity;
            bAnyActive = true;
        }

        private void AddDust(float _x, float _y, float _vx, float _vy, float _drag, float _gravity, float _delay, float _life, int _kind, Color32 _color)
        {
            dusts[dustCursor] = new Dust
            {
                bActive = true,
                bFresh = true,
                x = _x,
                y = _y,
                vx = _vx,
                vy = _vy,
                drag = _drag,
                gravity = _gravity,
                age = -_delay,
                life = _life,
                kind = _kind,
                seed = (int)(Rand01() * 997f),
                color = _color,
            };
            dustCursor = (dustCursor + 1) % DustCapacity;
            bAnyActive = true;
        }

        // 위로 솟는 부채꼴 분수. 금화가 섞여 떨어지며 돌고, 금가루는 반짝이며 흩어진다.
        private void SpawnFountain(int _x, int _y, int _count, float _delay, float _speedScale)
        {
            for (int i = 0; i < _count; i++)
            {
                // 위쪽 부채꼴(약 35°~145°)에 고르게. 가운데로 갈수록 빠르게 솟는다.
                float u = (i + Rand01()) / _count;
                float angle = Mathf.Lerp(0.6f, Mathf.PI - 0.6f, u);
                float center = 1f - Mathf.Abs(u - 0.5f) * 2f;
                float speed = Mathf.Lerp(95f, 190f, Mathf.Lerp(Rand01(), 1f, center * 0.4f)) * _speedScale;

                float roll = Rand01();
                int kind = roll < 0.28f ? KindCoin : (roll < 0.66f ? KindSparkle : KindDot);
                Color32 color = KindCoin == kind ? ColorGold : PickSparkColor();
                float life = KindCoin == kind ? Mathf.Lerp(0.7f, 0.95f, Rand01()) : Mathf.Lerp(0.5f, 0.85f, Rand01());

                AddDust(_x + (Rand01() - 0.5f) * 6f, _y, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed,
                    KindCoin == kind ? 1.2f : 2.4f, FountainGravity, _delay, life, kind, color);
            }
        }

        private Color32 PickSparkColor()
        {
            float roll = Rand01();
            return roll < 0.45f ? ColorGold : (roll < 0.75f ? ColorCream : ColorWhite);
        }

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535f;
        }

        private static int ToPixel(float _world)
        {
            return Mathf.RoundToInt(_world * PixelsPerUnit);
        }

        // //매 프레임

        private void LateUpdate()
        {
            if (false == bAnyActive) return;

            float dt = Time.deltaTime;
            bool bActive = false;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            for (int i = 0; i < FlashCapacity; i++)
            {
                if (false == flashes[i].bActive) continue;
                if (flashes[i].bFresh) flashes[i].bFresh = false;
                else flashes[i].age += dt;

                if (flashes[i].age >= FlashEnd)
                {
                    flashes[i].bActive = false;
                    continue;
                }

                bActive = true;
                Encapsulate(flashes[i].x, flashes[i].y, ref minX, ref minY, ref maxX, ref maxY);
            }

            for (int i = 0; i < RingCapacity; i++)
            {
                if (false == rings[i].bActive) continue;
                if (rings[i].bFresh) rings[i].bFresh = false;
                else rings[i].age += dt;

                if (rings[i].age >= RingStepTime * RingStepCount)
                {
                    rings[i].bActive = false;
                    continue;
                }

                bActive = true;
                Encapsulate(rings[i].x, rings[i].y, ref minX, ref minY, ref maxX, ref maxY);
            }

            for (int i = 0; i < DustCapacity; i++)
            {
                if (false == dusts[i].bActive) continue;

                if (dusts[i].bFresh)
                {
                    dusts[i].bFresh = false;
                }
                else
                {
                    float before = dusts[i].age;
                    dusts[i].age += dt;
                    // 지연이 끝난 뒤의 시간만 움직인다.
                    float moveDt = before < 0f ? Mathf.Max(0f, dusts[i].age) : dt;
                    if (moveDt > 0f)
                    {
                        float damping = Mathf.Exp(-dusts[i].drag * moveDt);
                        dusts[i].vx *= damping;
                        dusts[i].vy = dusts[i].vy * damping - dusts[i].gravity * moveDt;
                        dusts[i].x += dusts[i].vx * moveDt;
                        dusts[i].y += dusts[i].vy * moveDt;
                    }
                }

                if (dusts[i].age >= dusts[i].life)
                {
                    dusts[i].bActive = false;
                    continue;
                }

                bActive = true;
                Encapsulate(dusts[i].x, dusts[i].y, ref minX, ref minY, ref maxX, ref maxY);
            }

            if (false == bActive)
            {
                bAnyActive = false;
                meshRenderer.enabled = false;
                return;
            }

            // 화면 밖이면 시간만 흐르게 두고 그리지 않는다(평가기는 고정이라 마을의 다른 곳에 있을 때가 많다).
            Vector3 center = new Vector3((minX + maxX) * 0.5f / PixelsPerUnit, (minY + maxY) * 0.5f / PixelsPerUnit, 0f);
            Vector3 size = new Vector3((maxX - minX) / PixelsPerUnit + PixelVfxCulling.MarginUnits * 2f,
                (maxY - minY) / PixelsPerUnit + PixelVfxCulling.MarginUnits * 2f, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(center, size))
            {
                meshRenderer.enabled = false;
                return;
            }

            Draw();
        }

        private static void Encapsulate(float _x, float _y, ref float _minX, ref float _minY, ref float _maxX, ref float _maxY)
        {
            if (_x < _minX) _minX = _x;
            if (_y < _minY) _minY = _y;
            if (_x > _maxX) _maxX = _x;
            if (_y > _maxY) _maxY = _y;
        }

        // //그리기

        private void Draw()
        {
            painter.Begin(buffer, transform.worldToLocalMatrix, 0f);

            DrawRings();
            DrawDusts();
            DrawFlashes();


            painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
        }

        private static Color32 WithGlow(Color32 _color, float _glow)
        {
            return new Color32(_color.r, _color.g, _color.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255f));
        }

        // 계단식으로 줄어드는 십자 섬광. 큰 섬광(쾅)은 금색 대각 광선이 함께 터진다.
        private void DrawFlashes()
        {
            for (int i = 0; i < FlashCapacity; i++)
            {
                if (false == flashes[i].bActive) continue;

                int x = flashes[i].x;
                int y = flashes[i].y;
                float age = flashes[i].age;
                bool bBig = flashes[i].bBig;
                Color32 white = ColorWhite;

                if (age < FlashStage0)
                {
                    int arm = bBig ? 16 : 6;
                    int half = bBig ? 1 : 0;
                    painter.Rect(x - arm, y - half, x + arm + 1, y + half + 1, white);
                    painter.Rect(x - half, y - arm, x + half + 1, y + arm + 1, white);

                    if (bBig)
                    {
                        Color32 gold = WithGlow(ColorGold, 0.9f);
                        for (int d = 3; d <= 9; d++)
                        {
                            painter.Dot(x + d, y + d, gold);
                            painter.Dot(x - d, y + d, gold);
                            painter.Dot(x + d, y - d, gold);
                            painter.Dot(x - d, y - d, gold);
                        }
                    }
                }
                else if (age < FlashStage1)
                {
                    int arm = bBig ? 10 : 4;
                    painter.HLine(x - arm, x + arm, y, white);
                    painter.Rect(x, y - arm, x + 1, y + arm + 1, white);
                    if (bBig) painter.Rect(x - 1, y - 1, x + 2, y + 2, white);
                }
                else
                {
                    int arm = bBig ? 5 : 2;
                    Color32 dim = WithGlow(ColorCream, 0.7f);
                    painter.HLine(x - arm, x + arm, y, dim);
                    painter.Rect(x, y - arm, x + 1, y + arm + 1, dim);
                }
            }
        }

        // 점선 타원 충격파. 계단식으로 커지고, 바깥으로 갈수록 식으며 점이 하나씩 빠진다.
        private void DrawRings()
        {
            for (int r = 0; r < RingCapacity; r++)
            {
                if (false == rings[r].bActive) continue;

                int step = (int)(rings[r].age / RingStepTime);
                if (step >= RingStepCount) continue;

                float grow = 1f - (1f - step / (float)(RingStepCount - 1)) * (1f - step / (float)(RingStepCount - 1));
                float radius = Mathf.Lerp(RingStartRadius, rings[r].maxRadius, grow);
                float survive = 1f - Mathf.Clamp01((rings[r].age - 0.15f) / 0.2f);

                Color32 baseColor = step < 3 ? ColorWhite : (step < 6 ? ColorGold : ColorOrange);
                Color32 color = WithGlow(baseColor, step < 3 ? 0.75f : 0.25f);
                float phase = 0 == (step & 1) ? 0f : TwoPi / RingDotCount * 0.5f;
                int size = step < 3 ? 2 : 1;

                for (int i = 0; i < RingDotCount; i++)
                {
                    float order = ((i * 7) % RingDotCount) / (float)RingDotCount;
                    if (survive <= order) continue;

                    float angle = phase + TwoPi * i / RingDotCount;
                    int px = rings[r].x + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                    int py = rings[r].y + Mathf.RoundToInt(Mathf.Sin(angle) * radius * RingSquashY);
                    painter.Rect(px, py, px + size, py + size, color);
                }
            }
        }

        private void DrawDusts()
        {
            for (int i = 0; i < DustCapacity; i++)
            {
                if (false == dusts[i].bActive || dusts[i].age < 0f) continue;

                float u = dusts[i].age / dusts[i].life;
                int px = Mathf.RoundToInt(dusts[i].x);
                int py = Mathf.RoundToInt(dusts[i].y);
                int seed = dusts[i].seed;

                switch (dusts[i].kind)
                {
                    case KindCoin:
                        DrawCoin(px, py, dusts[i].age, u, seed);
                        break;

                    case KindTwinkle:
                        DrawTwinkle(px, py, u, dusts[i].color);
                        break;

                    case KindStreak:
                        DrawStreak(px, py, dusts[i].vx, dusts[i].vy, dusts[i].age, u, dusts[i].color);
                        break;

                    case KindChip:
                        DrawChip(px, py, dusts[i].age, u, seed, dusts[i].color);
                        break;

                    default:
                        DrawSpark(px, py, dusts[i].age, u, seed, dusts[i].kind, dusts[i].color);
                        break;
                }
            }
        }

        // 금가루: 3단계로 반짝이고, `+`/`x`는 크기가 계단식으로 줄며 서로 번갈아 바뀐다. 끝물에는 점멸하며 사라진다.
        private void DrawSpark(int _x, int _y, float _age, float _u, int _seed, int _kind, Color32 _color)
        {
            if (_u > 0.8f && 0 != ((int)(_age * 30f) & 1)) return;

            int twinkle = ((int)(_age * 14f) + _seed) % 3;
            float glow = (0 == twinkle ? 0.35f : (1 == twinkle ? 0.6f : 1f)) * (1f - _u * 0.5f);
            Color32 baseColor = _u > 0.72f ? ColorOrange : _color;
            Color32 color = WithGlow(baseColor, glow);

            int arm = KindDot == _kind ? 0 : (_u < 0.4f ? 2 : (_u < 0.7f ? 1 : 0));
            if (0 == arm)
            {
                painter.Dot(_x, _y, color);
                return;
            }

            bool bPlus = 0 == (((int)(_age * 12f) + _seed) & 1);
            if (bPlus)
            {
                painter.HLine(_x - arm, _x + arm, _y, color);
                painter.Rect(_x, _y - arm, _x + 1, _y + arm + 1, color);
            }
            else
            {
                for (int d = -arm; d <= arm; d++)
                {
                    painter.Dot(_x + d, _y + d, color);
                    if (0 != d) painter.Dot(_x + d, _y - d, color);
                }
            }
        }

        // 금화: 돌면서(앞면 원 -> 비스듬 -> 옆면 막대) 떨어지고, 앞면일 때 흰 반짝임이 맺힌다.
        private void DrawCoin(int _x, int _y, float _age, float _u, int _seed)
        {
            if (_u > 0.82f && 0 != ((int)(_age * 30f) & 1)) return;

            Color32 face = WithGlow(ColorGold, 0.1f);
            Color32 edge = WithGlow(ColorCoinEdge, 0f);
            int frame = ((int)(_age * 16f) + _seed) & 3;

            switch (frame)
            {
                case 0: // 앞면
                    painter.HLine(_x - 1, _x, _y + 1, face);
                    painter.HLine(_x - 2, _x + 1, _y, face);
                    painter.HLine(_x - 2, _x + 1, _y - 1, face);
                    painter.HLine(_x - 1, _x, _y - 2, edge);
                    painter.Dot(_x - 1, _y, WithGlow(ColorWhite, 0.85f));
                    break;

                case 1: // 비스듬
                case 3:
                    painter.HLine(_x - 1, _x, _y + 1, face);
                    painter.HLine(_x - 1, _x, _y, face);
                    painter.HLine(_x - 1, _x, _y - 1, edge);
                    break;

                default: // 옆면
                    painter.Rect(_x, _y - 1, _x + 1, _y + 2, edge);
                    break;
            }
        }

        // 불티 줄기: 머리는 밝고(처음엔 흰빛), 날아온 방향으로 금 -> 주황 꼬리를 끈다. 빠를수록 꼬리가 길고,
        // 느려지면 점 하나로 줄었다가 끝물에 점멸하며 꺼진다.
        private void DrawStreak(int _x, int _y, float _vx, float _vy, float _age, float _u, Color32 _color)
        {
            if (_u > 0.78f && 0 != ((int)(_age * 30f) & 1)) return;

            float speed = Mathf.Sqrt(_vx * _vx + _vy * _vy);
            int tail = speed < 1f ? 0 : Mathf.Clamp(Mathf.RoundToInt(speed / 40f), 0, 4);
            if (_u > 0.6f) tail = Mathf.Min(tail, 1);

            Color32 head = _u < 0.3f ? WithGlow(ColorWhite, 1f) : WithGlow(_u < 0.65f ? _color : ColorOrange, _u < 0.65f ? 0.7f : 0.3f);
            if (tail > 0)
            {
                float bx = -_vx / speed;
                float by = -_vy / speed;
                Color32 near = WithGlow(ColorGold, 0.55f);
                Color32 far = WithGlow(ColorOrange, 0.15f);
                for (int k = tail; k >= 1; k--)
                {
                    painter.Dot(_x + Mathf.RoundToInt(bx * k), _y + Mathf.RoundToInt(by * k), k <= tail / 2 + 1 ? near : far);
                }
            }

            painter.Dot(_x, _y, head);
        }

        // 톱밥 조각: 가로 2칸 <-> 세로 2칸으로 돌며 떨어진다. 발광 없이 칠해 불티와 구분되고, 끝물에 점멸한다.
        private void DrawChip(int _x, int _y, float _age, float _u, int _seed, Color32 _color)
        {
            if (_u > 0.8f && 0 != ((int)(_age * 30f) & 1)) return;

            Color32 color = WithGlow(_color, _u < 0.25f ? 0.3f : 0f);
            if (0 == (((int)(_age * 14f) + _seed) & 1)) painter.Rect(_x, _y, _x + 2, _y + 1, color);
            else painter.Rect(_x, _y, _x + 1, _y + 2, color);
        }

        // 업혀 가기 반짝임: 점 -> 작은 + -> 큰 + -> 작은 + -> 점으로 피었다 진다.
        private void DrawTwinkle(int _x, int _y, float _u, Color32 _color)
        {
            int arm = _u < 0.15f ? 0 : (_u < 0.35f ? 1 : (_u < 0.6f ? 2 : (_u < 0.82f ? 1 : 0)));
            Color32 color = WithGlow(_color, 2 == arm ? 0.9f : 0.45f);

            if (0 == arm)
            {
                painter.Dot(_x, _y, color);
                return;
            }

            painter.HLine(_x - arm, _x + arm, _y, color);
            painter.Rect(_x, _y - arm, _x + 1, _y + arm + 1, color);
            if (2 == arm) painter.Dot(_x, _y, WithGlow(ColorWhite, 1f));
        }

        // //수명

        private void OnDisable()
        {
            ClearAll();
        }

        private void OnDestroy()
        {
            if (null != mesh)
            {
                Destroy(mesh);
                mesh = null;
            }
        }
    }
}
