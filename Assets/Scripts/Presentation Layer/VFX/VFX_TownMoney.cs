using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 마을 돈 흐름 이펙트. 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다
    /// (VFX_TopgradeAssessment와 같은 방식, 정점 알파 = HDR 발광 세기인 BrandWrapGlow 셰이더).
    /// 상점 하나에 하나, ShopNPC가 런타임에 만들어 부른다.
    ///
    /// 장면
    /// - 입금 출발(PlayDepositPop): 평가기 윗면에서 금화가 튀어 나올 때 작은 섬광과 금가루 몇 알.
    /// - 입금 도착(PlayDepositLand): 금화가 상점 계산대에 쏙 들어갈 때 "짤랑" - 작은 섬광, 위로 튀는 금가루, 반짝임 하나.
    /// - 상점 대기(EmitShopTwinkle): 수금 안 한 돈이 있으면 상점 둘레에 반짝임이 피었다 진다. 쌓일수록 자주 핀다.
    /// - 비행 꼬리(EmitCoinTrail): 날아가는 금화 뒤로 금가루가 흩날리며 남는다.
    /// - 도착(PlayCoinArrive): 캐릭터에 박힐 때마다 작은 섬광·금가루. 첫 금화(실제 지급 순간)는 충격파가 더해지고,
    ///   마지막 금화는 큰 충격파·불꽃 줄기와 함께 반짝임이 캐릭터 둘레로 피어오른다.
    /// - 주괴 판매(PlayIngotSale): 용광로 주괴가 상점에 꽂힐 때 섬광·충격파·작은 금화 분수.
    ///
    /// 반투명 페이드 없이 계단식 크기 변화·점멸로만 사라진다. 고정 크기 배열만 써서 재생 중 힙 할당이 없다.
    /// 그림일 뿐이라 돈·판정·타이밍과는 무관하다. 모든 요소는 날아가는 아이템 층(FlyingItem)에 그린다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_TownMoney : MonoBehaviour
    {
        // //공용 상수

        private const float PixelsPerUnit = 32f;
        private const float TwoPi = Mathf.PI * 2f;

        private const string ShaderName = "Custom/VFX/BrandWrapGlow";
        private const string SortingLayerName = "FlyingItem";
        private const int SortingOrder = 0;

        // 정점 알파 0~1 -> 발광 배율 GlowMin~GlowMax. 흰 섬광·반짝임 중심만 블룸 임계를 넘기고 금화 본체는 넘지 않게 한다.
        private const float GlowMin = 1f;
        private const float GlowMax = 2.4f;

        // 팔레트(화면에 보일 sRGB). 리니어 색 공간이면 EnsureShared에서 한 번 변환한다(VFX_TopgradeAssessment와 같은 색).
        private static readonly Color32 SrgbCream = new Color32(255, 241, 176, 255);
        private static readonly Color32 SrgbGold = new Color32(255, 204, 72, 255);
        private static readonly Color32 SrgbOrange = new Color32(238, 136, 38, 255);
        private static readonly Color32 SrgbCoinEdge = new Color32(206, 128, 28, 255);
        private static readonly Color32 SrgbCoinDark = new Color32(150, 82, 20, 255);

        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static Color32 ColorCream;
        private static Color32 ColorGold;
        private static Color32 ColorOrange;
        private static Color32 ColorCoinEdge;
        private static Color32 ColorCoinDark;
        private static bool bPaletteReady;

        // //섬광

        private const int FlashCapacity = 24;
        private const int FlashSmall = 0;
        private const int FlashMedium = 1;
        private const int FlashBig = 2;
        private const float FlashStage0 = 0.03f;
        private const float FlashStage1 = 0.06f;
        private const float FlashEnd = 0.09f;

        // //점선 충격파

        private const int RingCapacity = 12;
        private const float RingStepTime = 0.035f;
        private const int RingStepCount = 10;
        private const int RingDotCount = 20;
        private const float RingSquashY = 0.55f;
        private const float RingStartRadius = 6f;

        // //조각

        private const int DustCapacity = 640;
        private const int KindDot = 0;
        private const int KindSparkle = 1;
        private const int KindCoin = 2;
        private const int KindTwinkle = 3;
        private const int KindStreak = 4;   // 불꽃 줄기: 날아가는 방향 반대로 꼬리를 끈다
        private const int KindTrail = 5;    // 비행 꼬리 금가루: 제자리에서 흩날리며 식는다

        private const float FountainGravity = 300f;     // px/s^2

        // //공유 자원

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(2048);
        private static readonly PixelPainter painter = new PixelPainter();
        private static Material sharedMaterial;
        private static bool bShaderMissingWarned;

        // //인스턴스 상태

        private struct Flash
        {
            public bool bActive;
            public bool bFresh;
            public int size;
            public int x;
            public int y;
            public float age;       // 음수면 아직 나오기 전(지연)
        }

        private struct Ring
        {
            public bool bActive;
            public bool bFresh;
            public int x;
            public int y;
            public float age;       // 음수면 아직 나오기 전(지연)
            public float maxRadius;
            public bool bBold;
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
        /// _parent에 SortingGroup이 있으면 그 그룹 정렬에 묶이므로, 그룹이 없는 부모를 넘겨야 한다.
        /// </summary>
        public static VFX_TownMoney Create(Transform _parent)
        {
            if (false == EnsureShared()) return null;

            GameObject go = new GameObject("TownMoneyVFX");
            go.transform.SetParent(_parent, false);
            VFX_TownMoney vfx = go.AddComponent<VFX_TownMoney>();
            vfx.Initialize();
            return vfx;
        }

        /// <summary>입금 금화가 평가기에서 튀어 나오는 순간. _bTopgrade면 조금 더 화려하다.</summary>
        public void PlayDepositPop(Vector3 _worldPos, bool _bTopgrade)
        {
            int x = ToPixel(_worldPos.x);
            int y = ToPixel(_worldPos.y);
            AddFlash(x, y, FlashSmall, 0f);

            int count = _bTopgrade ? 6 : 3;
            for (int i = 0; i < count; i++)
            {
                float angle = Mathf.Lerp(0.5f, Mathf.PI - 0.5f, (i + Rand01()) / count);
                float speed = Mathf.Lerp(40f, 80f, Rand01());
                AddDust(x, y, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 3f, 120f,
                    0f, Mathf.Lerp(0.22f, 0.34f, Rand01()), KindSparkle, PickSparkColor());
            }
        }

        /// <summary>입금 금화가 상점에 쏙 들어가는 순간("짤랑").</summary>
        public void PlayDepositLand(Vector3 _worldPos)
        {
            int x = ToPixel(_worldPos.x);
            int y = ToPixel(_worldPos.y);
            AddFlash(x, y, FlashSmall, 0f);

            for (int i = 0; i < 4; i++)
            {
                float angle = Mathf.Lerp(0.35f, Mathf.PI - 0.35f, (i + Rand01()) / 4f);
                float speed = Mathf.Lerp(45f, 90f, Rand01());
                AddDust(x, y, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 3f, 160f,
                    0f, Mathf.Lerp(0.24f, 0.36f, Rand01()), 0 == i ? KindDot : KindSparkle, PickSparkColor());
            }

            AddDust(x + (Rand01() - 0.5f) * 8f, y + 4f + Rand01() * 4f, 0f, 16f, 0f, 0f,
                0.04f, 0.3f, KindTwinkle, ColorWhite);
        }

        /// <summary>
        /// 상점에 수금 안 한 돈이 있을 때 둘레에 반짝임 하나. _center/_extents(유닛)는 반짝임이 생길 범위,
        /// _heat(0~1)는 쌓인 정도. 많이 쌓일수록 _coinHopPos(계산대)에서 작은 금화가 톡 튀어 올랐다 떨어지는 일이 잦다.
        /// </summary>
        public void EmitShopTwinkle(Vector3 _center, Vector2 _extents, Vector3 _coinHopPos, float _heat)
        {
            float x = _center.x * PixelsPerUnit + (Rand01() * 2f - 1f) * _extents.x * PixelsPerUnit;
            float y = _center.y * PixelsPerUnit + (Rand01() * 2f - 1f) * _extents.y * PixelsPerUnit;
            Color32 color = Rand01() < 0.6f ? ColorGold : ColorWhite;
            AddDust(x, y, 0f, 10f, 0f, 0f, 0f, Mathf.Lerp(0.3f, 0.42f, Rand01()), KindTwinkle, color);

            if (Rand01() < Mathf.Clamp01(_heat) * 0.35f)
            {
                float hx = _coinHopPos.x * PixelsPerUnit + (Rand01() - 0.5f) * 10f;
                float hy = _coinHopPos.y * PixelsPerUnit;
                AddDust(hx, hy, (Rand01() - 0.5f) * 30f, Mathf.Lerp(80f, 105f, Rand01()), 0.5f, FountainGravity,
                    0f, Mathf.Lerp(0.5f, 0.6f, Rand01()), KindCoin, ColorGold);
            }
        }

        /// <summary>
        /// 날아가는 금화 꼬리 금가루 하나. _worldPos는 금화 중심.
        /// </summary>
        public void EmitCoinTrail(Vector3 _worldPos)
        {
            float x = _worldPos.x * PixelsPerUnit + (Rand01() - 0.5f) * 3f;
            float y = _worldPos.y * PixelsPerUnit + (Rand01() - 0.5f) * 3f;

            if (Rand01() < 0.18f)
            {
                AddDust(x, y, 0f, 6f, 0f, 0f, 0f, Mathf.Lerp(0.22f, 0.3f, Rand01()), KindTwinkle, Rand01() < 0.5f ? ColorWhite : ColorGold);
                return;
            }

            float roll = Rand01();
            Color32 color = roll < 0.3f ? ColorWhite : (roll < 0.75f ? ColorCream : ColorGold);
            AddDust(x, y, (Rand01() - 0.5f) * 16f, Mathf.Lerp(-6f, 8f, Rand01()), 4f, 40f,
                0f, Mathf.Lerp(0.2f, 0.32f, Rand01()), KindTrail, color);
        }

        /// <summary>
        /// 금화가 캐릭터(또는 원격 입금 금화가 캐릭터)에 박히는 순간. _bFirst는 실제 지급 순간(첫 금화),
        /// _bLast는 이번 수금의 마지막 금화. 둘 다 아니면 작게만 터진다.
        /// </summary>
        public void PlayCoinArrive(Vector3 _worldPos, bool _bFirst, bool _bLast)
        {
            int x = ToPixel(_worldPos.x);
            int y = ToPixel(_worldPos.y);

            // 금화가 연달아(최대 20개) 박히므로 매번 섬광을 그리면 캐릭터 얼굴이 계속 가려진다.
            // 섬광은 첫 금화·마지막 금화에만 주고, 매번은 몸 바깥쪽에서 금가루가 위로 톡 튀기만 한다.
            if (_bFirst || _bLast) AddFlash(x, y, FlashMedium, 0f);

            // 양옆 하나씩, 수평에서 20~50° 위로. 몸(머리 폭 약 14px) 바깥에서 출발해 얼굴을 지나지 않는다.
            for (int i = 0; i < 2; i++)
            {
                float side = 0 == i ? -1f : 1f;
                float elevation = Mathf.Lerp(0.35f, 0.87f, Rand01());
                float cx = Mathf.Cos(elevation) * side;
                float cy = Mathf.Sin(elevation);
                float speed = Mathf.Lerp(80f, 120f, Rand01());
                AddDust(x + side * 9f, y + 1f, cx * speed, cy * speed, 4f, 150f,
                    0f, Mathf.Lerp(0.2f, 0.28f, Rand01()), KindSparkle, PickSparkColor());
            }

            if (_bFirst)
            {
                AddRing(x, y, 22f, false, 0f);
            }

            if (_bLast)
            {
                AddRing(x, y, 32f, true, 0f);
                AddFlash(x, y, FlashBig, 0.02f);

                // 불꽃 줄기
                float phase = Rand01() * TwoPi;
                for (int i = 0; i < 10; i++)
                {
                    float angle = phase + TwoPi * (i + Rand01() * 0.4f) / 10f;
                    float speed = Mathf.Lerp(130f, 200f, Rand01());
                    AddDust(x, y, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed * 0.8f, 5f, 60f,
                        0f, Mathf.Lerp(0.24f, 0.34f, Rand01()), KindStreak, Rand01() < 0.5f ? ColorWhite : ColorCream);
                }

                // 반짝임이 캐릭터 둘레(몸 바깥 양옆)에서 번갈아 차례로 피어오른다.
                for (int i = 0; i < 10; i++)
                {
                    float side = 0 == (i & 1) ? -1f : 1f;
                    float px = x + side * Mathf.Lerp(9f, 16f, Rand01());
                    float py = y - 10f + Rand01() * 14f;
                    Color32 color = Rand01() < 0.55f ? ColorGold : ColorWhite;
                    AddDust(px, py, 0f, Mathf.Lerp(30f, 55f, Rand01()), 0.5f, 0f,
                        0.05f + i * 0.035f, Mathf.Lerp(0.32f, 0.42f, Rand01()), KindTwinkle, color);
                }
            }
        }

        /// <summary>용광로 주괴가 상점에 꽂히는 순간. _worldPos는 상점 몸통.</summary>
        public void PlayIngotSale(Vector3 _worldPos)
        {
            int x = ToPixel(_worldPos.x);
            int y = ToPixel(_worldPos.y);

            AddFlash(x, y, FlashMedium, 0f);
            AddRing(x, y, 26f, true, 0f);
            SpawnFountain(x, y + 4, 12, 0f, 0.85f, 0.3f);
        }

        /// <summary>그리던 것을 모두 지운다(맵을 떠날 때 등).</summary>
        public void ClearAll()
        {
            for (int i = 0; i < FlashCapacity; i++) flashes[i].bActive = false;
            for (int i = 0; i < RingCapacity; i++) rings[i].bActive = false;
            for (int i = 0; i < DustCapacity; i++) dusts[i].bActive = false;
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
                    Debug.LogWarning("[VFX_TownMoney] 셰이더 " + ShaderName + "를 찾지 못해 마을 돈 이펙트를 건너뜁니다.");
                }
                return false;
            }

            sharedMaterial = new Material(shader) { name = "M_TownMoney (Runtime)" };
            sharedMaterial.SetFloat("_GlowMin", GlowMin);
            sharedMaterial.SetFloat("_GlowMax", GlowMax);
            return true;
        }

        private void Initialize()
        {
            mesh = new Mesh { name = "VFX_TownMoney" };
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
            ColorCoinDark = ToDisplay(SrgbCoinDark, bLinear);
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

        private void AddFlash(int _x, int _y, int _size, float _delay)
        {
            flashes[flashCursor] = new Flash { bActive = true, bFresh = true, size = _size, x = _x, y = _y, age = -_delay };
            flashCursor = (flashCursor + 1) % FlashCapacity;
            bAnyActive = true;
        }

        private void AddRing(int _x, int _y, float _maxRadius, bool _bBold, float _delay)
        {
            rings[ringCursor] = new Ring { bActive = true, bFresh = true, x = _x, y = _y, age = -_delay, maxRadius = _maxRadius, bBold = _bBold };
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
        private void SpawnFountain(int _x, int _y, int _count, float _delay, float _speedScale, float _coinRatio)
        {
            for (int i = 0; i < _count; i++)
            {
                // 위쪽 부채꼴(약 30°~150°)에 고르게. 가운데로 갈수록 빠르게 솟는다.
                float u = (i + Rand01()) / _count;
                float angle = Mathf.Lerp(0.52f, Mathf.PI - 0.52f, u);
                float center = 1f - Mathf.Abs(u - 0.5f) * 2f;
                float speed = Mathf.Lerp(100f, 200f, Mathf.Lerp(Rand01(), 1f, center * 0.4f)) * _speedScale;

                float roll = Rand01();
                int kind = roll < _coinRatio ? KindCoin : (roll < _coinRatio + 0.4f ? KindSparkle : KindDot);
                Color32 color = KindCoin == kind ? ColorGold : PickSparkColor();
                float life = KindCoin == kind ? Mathf.Lerp(0.75f, 1.0f, Rand01()) : Mathf.Lerp(0.5f, 0.85f, Rand01());

                AddDust(_x + (Rand01() - 0.5f) * 8f, _y, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed,
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
            Step(Time.deltaTime);
        }

        // 한 프레임 진행 + 그리기. 에디터 미리보기에서 고정 간격으로 돌려 볼 수 있도록 시간을 인자로 받는다.
        private void Step(float dt)
        {
            if (false == bAnyActive) return;

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
                Encapsulate(rings[i].x - rings[i].maxRadius, rings[i].y, ref minX, ref minY, ref maxX, ref maxY);
                Encapsulate(rings[i].x + rings[i].maxRadius, rings[i].y, ref minX, ref minY, ref maxX, ref maxY);
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

            // 화면 밖이면 시간만 흐르게 두고 그리지 않는다.
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

        // 계단식으로 줄어드는 십자 섬광. 중간 이상은 금색 대각 광선이 함께 터진다.
        private void DrawFlashes()
        {
            for (int i = 0; i < FlashCapacity; i++)
            {
                if (false == flashes[i].bActive || flashes[i].age < 0f) continue;

                int x = flashes[i].x;
                int y = flashes[i].y;
                float age = flashes[i].age;
                int size = flashes[i].size;
                Color32 white = ColorWhite;

                if (age < FlashStage0)
                {
                    int arm = FlashBig == size ? 16 : (FlashMedium == size ? 9 : 5);
                    int half = FlashBig == size ? 1 : 0;
                    painter.Rect(x - arm, y - half, x + arm + 1, y + half + 1, white);
                    painter.Rect(x - half, y - arm, x + half + 1, y + arm + 1, white);
                    if (FlashSmall != size) painter.Rect(x - 1, y - 1, x + 2, y + 2, white);

                    if (FlashSmall != size)
                    {
                        Color32 gold = WithGlow(ColorGold, 0.9f);
                        int dEnd = FlashBig == size ? 9 : 5;
                        for (int d = 3; d <= dEnd; d++)
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
                    int arm = FlashBig == size ? 10 : (FlashMedium == size ? 6 : 3);
                    painter.HLine(x - arm, x + arm, y, white);
                    painter.Rect(x, y - arm, x + 1, y + arm + 1, white);
                    if (FlashSmall != size) painter.Rect(x - 1, y - 1, x + 2, y + 2, white);
                }
                else
                {
                    int arm = FlashBig == size ? 5 : (FlashMedium == size ? 3 : 1);
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
                if (false == rings[r].bActive || rings[r].age < 0f) continue;

                int step = (int)(rings[r].age / RingStepTime);
                if (step >= RingStepCount) continue;

                float k = 1f - step / (float)(RingStepCount - 1);
                float grow = 1f - k * k;
                float radius = Mathf.Lerp(RingStartRadius, rings[r].maxRadius, grow);
                float survive = 1f - Mathf.Clamp01((rings[r].age - 0.15f) / 0.2f);

                Color32 baseColor = step < 3 ? ColorWhite : (step < 6 ? ColorGold : ColorOrange);
                Color32 color = WithGlow(baseColor, step < 3 ? 0.75f : 0.25f);
                float phase = 0 == (step & 1) ? 0f : TwoPi / RingDotCount * 0.5f;
                int size = (rings[r].bBold && step < 5) || step < 2 ? 2 : 1;

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

                    case KindTrail:
                        DrawTrail(px, py, dusts[i].age, u, dusts[i].color);
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

        // 금화(5px): 돌면서(앞면 원 -> 비스듬 -> 옆면 막대) 떨어지고, 앞면일 때 흰 반짝임이 맺힌다.
        private void DrawCoin(int _x, int _y, float _age, float _u, int _seed)
        {
            if (_u > 0.82f && 0 != ((int)(_age * 30f) & 1)) return;

            Color32 face = WithGlow(ColorGold, 0.12f);
            Color32 edge = WithGlow(ColorCoinEdge, 0f);
            Color32 dark = WithGlow(ColorCoinDark, 0f);
            Color32 shine = WithGlow(ColorWhite, 0.85f);
            int frame = ((int)(_age * 14f) + _seed) & 3;

            switch (frame)
            {
                case 0: // 앞면
                    painter.HLine(_x - 1, _x + 1, _y + 2, edge);
                    painter.HLine(_x - 2, _x + 2, _y + 1, face);
                    painter.HLine(_x - 2, _x + 2, _y, face);
                    painter.HLine(_x - 2, _x + 2, _y - 1, edge);
                    painter.HLine(_x - 1, _x + 1, _y - 2, dark);
                    painter.Dot(_x - 1, _y + 1, shine);
                    break;

                case 1: // 비스듬
                case 3:
                    painter.Dot(_x, _y + 2, edge);
                    painter.HLine(_x - 1, _x + 1, _y + 1, face);
                    painter.HLine(_x - 1, _x + 1, _y, face);
                    painter.HLine(_x - 1, _x + 1, _y - 1, edge);
                    painter.Dot(_x, _y - 2, dark);
                    break;

                default: // 옆면
                    painter.Rect(_x, _y - 2, _x + 1, _y + 3, edge);
                    painter.Dot(_x, _y + 1, WithGlow(ColorCream, 0.3f));
                    break;
            }
        }

        // 불꽃 줄기: 머리는 밝고(처음엔 흰빛), 날아온 방향으로 금 -> 주황 꼬리를 끈다. 빠를수록 꼬리가 길고,
        // 느려지면 점 하나로 줄었다가 끝물에 점멸하며 꺼진다.
        private void DrawStreak(int _x, int _y, float _vx, float _vy, float _age, float _u, Color32 _color)
        {
            if (_u > 0.78f && 0 != ((int)(_age * 30f) & 1)) return;

            float speed = Mathf.Sqrt(_vx * _vx + _vy * _vy);
            int tail = speed < 1f ? 0 : Mathf.Clamp(Mathf.RoundToInt(speed / 35f), 0, 5);
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

        // 비행 꼬리 금가루: 작은 + 로 반짝였다가 점으로 줄고, 금 -> 주황으로 식으며 끝물에 점멸한다.
        private void DrawTrail(int _x, int _y, float _age, float _u, Color32 _color)
        {
            if (_u > 0.7f && 0 != ((int)(_age * 30f) & 1)) return;

            if (_u < 0.28f)
            {
                Color32 bright = WithGlow(_color, 0.8f);
                painter.HLine(_x - 1, _x + 1, _y, bright);
                painter.Dot(_x, _y - 1, bright);
                painter.Dot(_x, _y + 1, bright);
                return;
            }

            Color32 color = _u < 0.6f ? WithGlow(ColorGold, 0.45f) : WithGlow(ColorOrange, 0.15f);
            painter.Dot(_x, _y, color);
        }

        // 반짝임: 점 -> 작은 + -> 큰 + -> 작은 + -> 점으로 피었다 진다.
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
