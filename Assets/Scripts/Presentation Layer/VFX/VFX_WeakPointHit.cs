using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// "약점 타격" 특성 연출. 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다
    /// (VFX_CriticalImpact와 같은 방식, 정점 알파 = HDR 발광 세기인 BrandWrapGlow 셰이더). 프리팹 없이 InDungeonVFXManager가 런타임에 만든다.
    /// 치명타(흰·금·진홍)와 겹쳐 나와도 갈리도록 분홍·자홍 계열만 쓰고, 조준 꺾쇠가 공통 표식이다.
    ///
    /// 노출(Expose, 약 0.34초) - 나무 HP가 절반 위에서 절반 이하로 처음 내려온 순간(쓰러지지 않았을 때). "이제 약점이 드러났다".
    /// - 조준 꺾쇠: 네 모서리 꺾쇠가 밑동 둘레로 크게 떴다가 계단식으로 조여 붙고, 두 번 깜빡인 뒤 꺼진다. 가운데 작은 마름모가 함께 깜빡인다.
    ///
    /// 관통(Strike, 약 0.35초) - 약점 보너스가 실제로 들어간 도끼 타격.
    /// - 조준(0~0.06초): 꺾쇠가 세 단계로 맞은 자리에 조여 든다(분홍 -> 옅은 분홍). 같은 순간 나무가 하얗게 번쩍이므로 흰색은 쓰지 않는다.
    /// - 꿰뚫림(0.06초): 맞은 자리에 마름모 섬광이 터지고, 공격자 반대편으로 바늘 같은 관통 줄기가 뻗었다가 밑동 쪽부터 사라진다.
    ///   들어온 쪽에는 짧은 줄기가 한 번 비친다. 맞은 자리에서 갈라진 금 네 갈래가 뻗었다가 식으며 깜빡여 꺼진다.
    /// - 파편: 관통한 쪽(나무 뒤)으로 분홍 줄기 파편이 부채꼴로 튀고, 자홍 조각 몇 개가 굴러 떨어진다.
    ///
    /// 반투명 페이드 없이 계단식 크기 변화·점멸로만 사라진다. 고정 크기 배열만 써서 재생 중 힙 할당이 없다. 그림일 뿐이라 판정과 무관하다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_WeakPointHit : MonoBehaviour
    {
        // //공용 상수

        public const int ModeExpose = 0;
        public const int ModeStrike = 1;

        private const float PixelsPerUnit = 32f;
        private const float TwoPi = Mathf.PI * 2f;

        private const string ShaderName = "Custom/VFX/BrandWrapGlow";

        // 정점 알파 0~1 -> 발광 배율 GlowMin~GlowMax(VFX_CriticalImpact와 같은 범위). 흰 섬광과 관통 줄기 머리만 블룸 임계를 넘긴다.
        private const float GlowMin = 1f;
        private const float GlowMax = 2.6f;

        // 팔레트(화면에 보일 sRGB). 리니어 색 공간이면 EnsureShared에서 한 번 변환한다.
        private static readonly Color32 SrgbPinkLight = new Color32(255, 178, 206, 255);
        private static readonly Color32 SrgbPink = new Color32(255, 74, 138, 255);
        private static readonly Color32 SrgbMagenta = new Color32(214, 36, 112, 255);
        private static readonly Color32 SrgbWine = new Color32(128, 22, 72, 255);

        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static Color32 ColorPinkLight;
        private static Color32 ColorPink;
        private static Color32 ColorMagenta;
        private static Color32 ColorWine;
        private static bool bPaletteReady;

        // 그릴 때 쓰는 "색 + 발광" 조합. 매 프레임 WithGlow를 다시 계산하지 않도록 팔레트를 만들 때 한 번 채운다(같은 함수라 값이 같다).
        // 이름 끝 숫자는 발광 세기 x100.
        private static Color32 White100;
        private static Color32 White70;
        private static Color32 PinkLight100;
        private static Color32 PinkLight80;
        private static Color32 PinkLight70;
        private static Color32 Pink100;
        private static Color32 Pink90;
        private static Color32 Pink85;
        private static Color32 Pink80;
        private static Color32 Pink75;
        private static Color32 Pink70;
        private static Color32 Pink60;
        private static Color32 Pink55;
        private static Color32 Pink50;
        private static Color32 Magenta50;
        private static Color32 Magenta40;
        private static Color32 Magenta35;
        private static Color32 Magenta30;
        private static Color32 Magenta25;
        private static Color32 Magenta20;
        private static Color32 Wine50;
        private static Color32 Wine25;
        private static Color32 Wine20;
        private static Color32 Wine15;

        // 꺾쇠 상자는 가로 반폭에 이 비율을 곱한 만큼만 세로로 연다(등각 화면에서 납작하게 보이도록).
        private const float BracketSquashY = 0.75f;

        // //노출

        private const float ExposeStep0 = 0.03f;     // 크게 뜬 꺾쇠
        private const float ExposeStep1 = 0.06f;     // 한 번 조인 분홍 꺾쇠
        private const float ExposeOff0 = 0.15f;      // 첫 깜빡임(꺼짐) 시작
        private const float ExposeOn1 = 0.19f;
        private const float ExposeOff1 = 0.27f;
        private const float ExposeOn2 = 0.30f;       // 마지막으로 흐리게 한 번 더 비친다
        private const float ExposeEnd = 0.34f;
        private const int ExposeLiftY = 3;           // 밑동(톱밥 자리)보다 이만큼 위를 꺾쇠 중심으로 잡아 줄기를 감싼다

        // //관통

        private const float LockStep = 0.02f;        // 꺾쇠가 한 단계 조이는 시간
        private const float LockEnd = 0.06f;         // 꺾쇠가 다 조인 순간 = 꿰뚫리는 순간
        private const int StrikeBracketLiftY = 2;    // 조준 꺾쇠는 맞은 자리보다 조금 위(줄기 쪽)를 감싼다

        private const float FlashStep = 0.03f;       // 마름모 섬광이 한 단계 줄어드는 시간(3단계)

        private const int PierceLength = 16;         // px, 관통 줄기 전체 길이
        private const int PierceGap = 2;             // 맞은 자리에서 줄기가 시작하는 거리
        private const int PierceThickLength = 6;     // 밑동 쪽 이 길이까지는 3줄 두께(바늘처럼 끝이 가늘다)
        private const float PierceGrowTime = 0.03f;  // 머리가 끝까지 뻗는 시간
        private const float PierceTailStart = 0.10f; // 이 시각부터 꼬리가 머리를 따라잡는다
        private const float PierceEnd = 0.19f;
        private const float EntryEnd = 0.10f;        // 들어온 쪽 짧은 줄기
        private const int EntryLength = 6;

        private const int CrackCount = 4;
        private const int CrackSegments = 2;
        private const float CrackRevealStep = 0.015f; // 마디가 하나씩 뻗는 간격
        private const float CrackHotEnd = 0.11f;
        private const float CrackCoolEnd = 0.15f;
        private const float CrackEnd = 0.18f;

        private const int DustCapacity = 16;
        private const int KindShard = 0;             // 분홍 줄기 파편(속도 방향 꼬리)
        private const int KindChip = 1;              // 자홍 조각(꼬리 없이 2x2 -> 1px)
        private const int ShardCount = 7;
        private const int ChipCount = 4;
        private const float DustGravity = 300f;      // px/s^2

        // 관통 전체 길이. 이 시간이 지나도 남은 파편이 있으면 다 사라질 때까지 이어간다.
        private const float StrikeMinDuration = 0.3f;

        // //공유 자원

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(256);
        private static readonly PixelPainter painter = new PixelPainter();
        private static Material sharedMaterial;
        private static bool bShaderMissingWarned;

        // //인스턴스 상태

        private struct Dust
        {
            public bool bActive;
            public float delay;       // 이 나이가 될 때까지는 움직이지도 그리지도 않는다(꿰뚫리는 순간에 튀게)
            public float x;           // 월드 px
            public float y;
            public float vx;          // px/s
            public float vy;
            public float drag;
            public float age;
            public float life;
            public int kind;
            public int seed;
            public Color32 colorMid;  // 줄기 파편: 머리 중간 단계(제 색 x0.7), 조각: 앞 단계(제 색 x0.5)
            public Color32 colorLow;  // 조각: 뒷 단계(제 색 x0.2). 줄기 파편은 쓰지 않는다.
        }

        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ulong meshHash;
        private int meshVertexCount = -1;

        private readonly Dust[] dusts = new Dust[DustCapacity];

        // 금 네 갈래의 마디 끝점(맞은 자리 기준 px). 재생할 때 한 번 뽑는다.
        private readonly int[] crackX = new int[CrackCount * CrackSegments];
        private readonly int[] crackY = new int[CrackCount * CrackSegments];

        // 마디별 픽셀(맞은 자리 기준). 마디 하나는 길어야 5px 안팎이라 칸을 넉넉히 잡는다.
        private const int CrackPixelCapacity = 16;
        private readonly int[] crackPixelX = new int[CrackCount * CrackSegments * CrackPixelCapacity];
        private readonly int[] crackPixelY = new int[CrackCount * CrackSegments * CrackPixelCapacity];
        private readonly int[] crackPixelCount = new int[CrackCount * CrackSegments];

        private bool bPlaying;
        private bool bFresh;
        private int mode;
        private float age;
        private int centerX;
        private int centerY;
        private int exitDirX;         // 관통 줄기·파편이 빠지는 쪽(공격자 반대편). 1 = 오른쪽, -1 = 왼쪽
        private uint randomState = 1u;

        // 화면 안/밖 판단 경계(재생 중 중심이 움직이지 않으므로 재생할 때 한 번 정한다).
        private Vector3 cullCenter;
        private Vector3 cullSize;

        // 노출은 계단식 단계로만 바뀌므로, 직전에 그린 단계와 같으면 다시 그리지 않는다(메쉬도 그대로 둔다). int.MinValue = 아직 안 그림.
        private int lastExposeStage = int.MinValue;

        // 파편 감속(Exp) 값 기억. 파편 감속 세기는 두 종류이고 프레임마다 같은 시간 간격이라, 같은 입력이면 다시 계산하지 않는다(값은 같다).
        private float dampDrag0 = -1f;
        private float dampStep0 = -1f;
        private float dampValue0;
        private float dampDrag1 = -1f;
        private float dampStep1 = -1f;
        private float dampValue1;
        private bool bDampNext1;

        public bool IsPlaying => bPlaying;

        // //퍼블릭

        /// <summary>
        /// _parent 아래에 이펙트를 하나 만든다. 셰이더를 찾지 못하면 null(이펙트 없이 진행).
        /// _parent에 SortingGroup이 있으면 그 그룹 정렬에 묶이므로, 그룹이 없는 부모를 넘겨야 한다.
        /// </summary>
        public static VFX_WeakPointHit Create(Transform _parent)
        {
            if (false == EnsureShared()) return null;

            GameObject go = new GameObject("WeakPointHitVFX");
            go.transform.SetParent(_parent, false);
            VFX_WeakPointHit vfx = go.AddComponent<VFX_WeakPointHit>();
            vfx.Initialize();
            return vfx;
        }

        /// <summary>
        /// _mode(ModeExpose / ModeStrike)를 _worldPos(밑동, 기본 피격 톱밥이 터지는 자리)에서 재생한다.
        /// _exitDirX는 공격자 반대편(관통 줄기가 빠지는 쪽, 1 또는 -1)이고 노출에서는 쓰지 않는다. 정렬은 맞은 나무 바로 앞(_sortingOrder)에 둔다.
        /// </summary>
        public void Play(int _mode, Vector3 _worldPos, int _exitDirX, string _sortingLayerName, int _sortingOrder)
        {
            meshRenderer.sortingLayerName = _sortingLayerName;
            meshRenderer.sortingOrder = _sortingOrder;

            mode = _mode;
            centerX = ToPixel(_worldPos.x);
            centerY = ToPixel(_worldPos.y) + (ModeExpose == _mode ? ExposeLiftY : 0);
            exitDirX = _exitDirX < 0 ? -1 : 1;
            age = 0f;
            bFresh = true;
            bPlaying = true;
            meshHash = 0UL;
            meshVertexCount = -1;
            lastExposeStage = int.MinValue;

            // 범위는 파편이 닿을 수 있는 정도로 넉넉히 잡는다.
            cullCenter = new Vector3(centerX / PixelsPerUnit, centerY / PixelsPerUnit, 0f);
            cullSize = new Vector3(4f + PixelVfxCulling.MarginUnits * 2f, 4f + PixelVfxCulling.MarginUnits * 2f, PixelVfxCulling.BoundsDepth);

            for (int i = 0; i < DustCapacity; i++) dusts[i].bActive = false;

            if (ModeStrike == _mode)
            {
                BuildCracks();
                SpawnDebris();
            }

            meshRenderer.enabled = false;

            // 쉬는 동안 꺼 둔 LateUpdate를 다시 켠다.
            enabled = true;
        }

        /// <summary>재생 중인 것을 바로 끝낸다(던전 이탈 등).</summary>
        public void StopImmediate()
        {
            StopInternal();

            // 쉬는 동안에는 LateUpdate가 불리지 않게 꺼 둔다(미리 만들어 둔 인스턴스가 매 프레임 빈 호출을 받지 않게).
            if (enabled) enabled = false;
        }

        private void StopInternal()
        {
            bPlaying = false;
            for (int i = 0; i < DustCapacity; i++) dusts[i].bActive = false;
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
                    Debug.LogWarning("[VFX_WeakPointHit] 셰이더 " + ShaderName + "를 찾지 못해 약점 타격 연출을 건너뜁니다.");
                }
                return false;
            }

            sharedMaterial = new Material(shader) { name = "M_WeakPointHit (Runtime)" };
            sharedMaterial.SetFloat("_GlowMin", GlowMin);
            sharedMaterial.SetFloat("_GlowMax", GlowMax);
            return true;
        }

        private void Initialize()
        {
            mesh = new Mesh { name = "VFX_WeakPointHit" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = sharedMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;

            randomState = unchecked((uint)Random.Range(1, int.MaxValue) * 2654435761u) | 1u;

            // 재생(Play) 전까지 LateUpdate를 돌리지 않는다.
            enabled = false;
        }

        private static void BuildPalette()
        {
            bool bLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            ColorPinkLight = ToDisplay(SrgbPinkLight, bLinear);
            ColorPink = ToDisplay(SrgbPink, bLinear);
            ColorMagenta = ToDisplay(SrgbMagenta, bLinear);
            ColorWine = ToDisplay(SrgbWine, bLinear);

            White100 = WithGlow(ColorWhite, 1f);
            White70 = WithGlow(ColorWhite, 0.7f);
            PinkLight100 = WithGlow(ColorPinkLight, 1f);
            PinkLight80 = WithGlow(ColorPinkLight, 0.8f);
            PinkLight70 = WithGlow(ColorPinkLight, 0.7f);
            Pink100 = WithGlow(ColorPink, 1f);
            Pink90 = WithGlow(ColorPink, 0.9f);
            Pink85 = WithGlow(ColorPink, 0.85f);
            Pink80 = WithGlow(ColorPink, 0.8f);
            Pink75 = WithGlow(ColorPink, 0.75f);
            Pink70 = WithGlow(ColorPink, 0.7f);
            Pink60 = WithGlow(ColorPink, 0.6f);
            Pink55 = WithGlow(ColorPink, 0.55f);
            Pink50 = WithGlow(ColorPink, 0.5f);
            Magenta50 = WithGlow(ColorMagenta, 0.5f);
            Magenta40 = WithGlow(ColorMagenta, 0.4f);
            Magenta35 = WithGlow(ColorMagenta, 0.35f);
            Magenta30 = WithGlow(ColorMagenta, 0.3f);
            Magenta25 = WithGlow(ColorMagenta, 0.25f);
            Magenta20 = WithGlow(ColorMagenta, 0.2f);
            Wine50 = WithGlow(ColorWine, 0.5f);
            Wine25 = WithGlow(ColorWine, 0.25f);
            Wine20 = WithGlow(ColorWine, 0.2f);
            Wine15 = WithGlow(ColorWine, 0.15f);

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

        // 금 네 갈래: 둘레에 고르게 흩되, 관통한 쪽으로 난 갈래가 조금 더 길다. 마디마다 꺾여 번개처럼 갈라진다.
        private void BuildCracks()
        {
            for (int c = 0; c < CrackCount; c++)
            {
                float angle = TwoPi * (c + 0.2f + Rand01() * 0.6f) / CrackCount;
                float toward = Mathf.Max(0f, Mathf.Cos(angle) * exitDirX);
                float x = 0f;
                float y = 0f;
                for (int s = 0; s < CrackSegments; s++)
                {
                    float segAngle = angle + (Rand01() - 0.5f) * 1.1f;
                    float segLength = Mathf.Lerp(2.2f, 3.2f, Rand01()) * (1f + toward * 0.35f);
                    x += Mathf.Cos(segAngle) * segLength;
                    y += Mathf.Sin(segAngle) * segLength * 0.8f;
                    crackX[c * CrackSegments + s] = Mathf.RoundToInt(x);
                    crackY[c * CrackSegments + s] = Mathf.RoundToInt(y);
                }
            }

            // 금의 모양은 재생 내내 같으므로 마디별 픽셀을 여기서 한 번만 뽑아 둔다(매 프레임 직선을 다시 긋지 않는다).
            for (int c = 0; c < CrackCount; c++)
            {
                int px = 0;
                int py = 0;
                for (int s = 0; s < CrackSegments; s++)
                {
                    int seg = c * CrackSegments + s;
                    int nx = crackX[seg];
                    int ny = crackY[seg];
                    crackPixelCount[seg] = BuildLinePixels(px, py, nx, ny, seg * CrackPixelCapacity);
                    px = nx;
                    py = ny;
                }
            }
        }

        // PixelPainter.Line(점선 없음)이 찍는 픽셀과 같은 순서·같은 위치를 뽑는다: Bresenham 직선 + L자 모서리 제거(픽셀 차이만 쓰므로 평행이동해도 같다).
        // crackPixelX/Y의 _offset부터 채우고 개수를 돌려준다. 칸이 모자라면 -1(그릴 때 PixelPainter.Line으로 대신 긋는다).
        private int BuildLinePixels(int _x0, int _y0, int _x1, int _y1, int _offset)
        {
            int dx = Mathf.Abs(_x1 - _x0);
            int dy = Mathf.Abs(_y1 - _y0);
            int sx = _x0 < _x1 ? 1 : -1;
            int sy = _y0 < _y1 ? 1 : -1;
            int error = dx - dy;
            int x = _x0;
            int y = _y0;
            int n = 0;

            while (true)
            {
                if (CrackPixelCapacity <= n) return -1;

                crackPixelX[_offset + n] = x;
                crackPixelY[_offset + n] = y;
                n++;

                if (x == _x1 && y == _y1) break;

                int doubled = 2 * error;
                if (doubled > -dy)
                {
                    error -= dy;
                    x += sx;
                }

                if (doubled < dx)
                {
                    error += dx;
                    y += sy;
                }
            }

            int w = 0;
            for (int i = 0; i < n; i++)
            {
                if (0 < i && i < n - 1 && 0 < w)
                {
                    int prevX = crackPixelX[_offset + w - 1];
                    int prevY = crackPixelY[_offset + w - 1];
                    if (Mathf.Abs(crackPixelX[_offset + i + 1] - prevX) <= 1 && Mathf.Abs(crackPixelY[_offset + i + 1] - prevY) <= 1) continue;
                }

                crackPixelX[_offset + w] = crackPixelX[_offset + i];
                crackPixelY[_offset + w] = crackPixelY[_offset + i];
                w++;
            }

            return w;
        }

        // 파편은 꿰뚫리는 순간(LockEnd)에 관통한 쪽으로 튄다.
        private void SpawnDebris()
        {
            int cursor = 0;
            float exitAngle = exitDirX > 0 ? 0f : Mathf.PI;

            for (int i = 0; i < ShardCount && cursor < DustCapacity; i++)
            {
                // 부채꼴로 고르게, 살짝 위로 치우치게
                float spread = Mathf.Lerp(-0.85f, 0.85f, (i + 0.15f + Rand01() * 0.7f) / ShardCount);
                float angle = exitAngle + spread * exitDirX + 0.18f * exitDirX;
                float speed = Mathf.Lerp(150f, 250f, Rand01()) * (1f - Mathf.Abs(spread) * 0.25f);
                float roll = Rand01();
                Color32 mid = roll < 0.3f ? White70 : (roll < 0.75f ? Pink70 : PinkLight70);
                AddDust(ref cursor, centerX + exitDirX * PierceGap, centerY, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed * 0.8f + 40f,
                    5f, Mathf.Lerp(0.22f, 0.34f, Rand01()), KindShard, mid, mid);
            }

            for (int i = 0; i < ChipCount && cursor < DustCapacity; i++)
            {
                float angle = exitAngle + (Rand01() - 0.5f) * 2.2f * exitDirX;
                float speed = Mathf.Lerp(60f, 120f, Rand01());
                bool bMagenta = Rand01() < 0.5f;
                AddDust(ref cursor, centerX + exitDirX * (PierceGap + 1), centerY + 1, Mathf.Cos(angle) * speed, Mathf.Abs(Mathf.Sin(angle)) * speed + 50f,
                    2.5f, Mathf.Lerp(0.3f, 0.42f, Rand01()), KindChip, bMagenta ? Magenta50 : Wine50, bMagenta ? Magenta20 : Wine20);
            }
        }

        private void AddDust(ref int _cursor, float _x, float _y, float _vx, float _vy, float _drag, float _life, int _kind, Color32 _colorMid, Color32 _colorLow)
        {
            dusts[_cursor] = new Dust
            {
                bActive = true,
                delay = LockEnd,
                x = _x,
                y = _y,
                vx = _vx,
                vy = _vy,
                drag = _drag,
                age = 0f,
                life = _life,
                kind = _kind,
                seed = (int)(Rand01() * 997f),
                colorMid = _colorMid,
                colorLow = _colorLow,
            };
            _cursor++;
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
            if (false == bPlaying) return;

            // 재생을 건 프레임에는 시간을 흘리지 않고 첫 장면을 그린다.
            float dt = bFresh ? 0f : Time.deltaTime;
            bFresh = false;
            if (false == Step(dt))
            {
                StopImmediate();
                return;
            }

            // 화면 밖이면 시간만 흐르게 두고 그리지 않는다. 다시 들어오면 단계가 같아도 새로 그린다(렌더러를 껐으므로).
            if (false == PixelVfxCulling.IsVisible(cullCenter, cullSize))
            {
                if (meshRenderer.enabled) meshRenderer.enabled = false;
                lastExposeStage = int.MinValue;
                return;
            }

            Draw();
        }

        // 감속 배율 exp(-_drag * _step). 최근 두 입력을 기억해 같으면 그대로 돌려준다.
        private float Damping(float _drag, float _step)
        {
            if (_drag == dampDrag0 && _step == dampStep0) return dampValue0;
            if (_drag == dampDrag1 && _step == dampStep1) return dampValue1;

            float value = Mathf.Exp(-_drag * _step);
            if (bDampNext1)
            {
                dampDrag1 = _drag; dampStep1 = _step; dampValue1 = value;
            }
            else
            {
                dampDrag0 = _drag; dampStep0 = _step; dampValue0 = value;
            }
            bDampNext1 = !bDampNext1;
            return value;
        }

        // 시간을 _dt만큼 흘리고 파편을 움직인다. 다 끝났으면 false.
        private bool Step(float _dt)
        {
            float dt = _dt;
            age += dt;

            bool bAnyDust = false;
            for (int i = 0; i < DustCapacity; i++)
            {
                if (false == dusts[i].bActive) continue;
                bAnyDust = true;
                if (age < dusts[i].delay || dt <= 0f) continue;

                // 대기가 끝난 프레임에는 넘친 시간만큼만 흘린다.
                float step = Mathf.Min(dt, age - dusts[i].delay);
                dusts[i].age += step;
                float damping = Damping(dusts[i].drag, step);
                dusts[i].vx *= damping;
                dusts[i].vy = dusts[i].vy * damping - DustGravity * step;
                dusts[i].x += dusts[i].vx * step;
                dusts[i].y += dusts[i].vy * step;

                if (dusts[i].age >= dusts[i].life) dusts[i].bActive = false;
            }

            float minDuration = ModeExpose == mode ? ExposeEnd : StrikeMinDuration;
            return age < minDuration || bAnyDust;
        }

        private void Draw()
        {
            if (ModeExpose == mode)
            {
                int stage = ExposeStage();
                if (stage == lastExposeStage) return;
                lastExposeStage = stage;

                painter.Begin(buffer, transform.worldToLocalMatrix, 0f);
                DrawExpose(stage);
            }
            else
            {
                painter.Begin(buffer, transform.worldToLocalMatrix, 0f);
                DrawCracks();
                DrawDusts();
                DrawEntry();
                DrawPierce();
                DrawFlash();
                DrawLockBrackets();
            }
            painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
        }

        // //그리기

        private static Color32 WithGlow(Color32 _color, float _glow)
        {
            return new Color32(_color.r, _color.g, _color.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255f));
        }

        // 네 모서리 꺾쇠. 가로 반폭 _halfX, 팔 길이 _arm(모서리 칸 포함), 1px 두께. 팔은 모서리에서 상자 안쪽으로 뻗는다.
        private static void DrawBrackets(int _cx, int _cy, int _halfX, int _arm, Color32 _color)
        {
            int halfY = Mathf.Max(1, Mathf.RoundToInt(_halfX * BracketSquashY));
            for (int sy = -1; sy <= 1; sy += 2)
            {
                int py = _cy + sy * halfY;
                int vy0 = sy > 0 ? py - (_arm - 1) : py;
                for (int sx = -1; sx <= 1; sx += 2)
                {
                    int px = _cx + sx * _halfX;
                    if (sx > 0) painter.HLine(px - (_arm - 1), px, py, _color);
                    else painter.HLine(px, px + (_arm - 1), py, _color);
                    painter.Rect(px, vy0, px + 1, vy0 + _arm, _color);
                }
            }
        }

        // 꽉 찬 마름모(반지름 _r, 1:1).
        private static void FillDiamond(int _cx, int _cy, int _r, Color32 _color)
        {
            for (int dy = -_r; dy <= _r; dy++)
            {
                int w = _r - Mathf.Abs(dy);
                painter.HLine(_cx - w, _cx + w, _cy + dy, _color);
            }
        }

        // 마름모 테두리(반지름 _r, 1px).
        private static void OutlineDiamond(int _cx, int _cy, int _r, Color32 _color)
        {
            for (int dy = -_r; dy <= _r; dy++)
            {
                int w = _r - Mathf.Abs(dy);
                painter.Dot(_cx - w, _cy + dy, _color);
                if (0 != w) painter.Dot(_cx + w, _cy + dy, _color);
            }
        }

        // 노출 단계(-1 = 끝남, 그 밖에는 화면에 보이는 장면 번호). 같은 번호면 같은 그림이다.
        private int ExposeStage()
        {
            if (age >= ExposeEnd) return -1;
            if (age < ExposeStep0) return 0;
            if (age < ExposeStep1) return 1;
            if (age < ExposeOff0) return 2;
            if (age < ExposeOn1) return 3;   // 꺼짐
            if (age < ExposeOff1) return 4;
            if (age < ExposeOn2) return 5;   // 꺼짐
            return 6;
        }

        private void DrawExpose(int _stage)
        {
            int halfX;
            int arm;
            Color32 color;
            bool bCore;

            switch (_stage)
            {
                case 0: halfX = 15; arm = 4; color = Pink100; bCore = false; break;
                case 1: halfX = 11; arm = 3; color = Pink90; bCore = true; break;
                case 2: halfX = 9; arm = 3; color = Pink75; bCore = true; break;
                case 4: halfX = 9; arm = 3; color = Pink60; bCore = true; break;
                case 6: halfX = 8; arm = 2; color = Magenta35; bCore = false; break;
                default: return;   // 끝남 또는 깜빡임의 꺼진 순간
            }

            DrawBrackets(centerX, centerY, halfX, arm, color);
            if (bCore) FillDiamond(centerX, centerY, 1, Pink50);
        }

        // 조준: 세 단계로 맞은 자리에 조여 든다. 꿰뚫리는 순간(LockEnd)에 사라지고 섬광이 자리를 넘겨받는다.
        private void DrawLockBrackets()
        {
            if (age >= LockEnd) return;

            int step = Mathf.Min(2, (int)(age / LockStep));
            if (0 == step) DrawBrackets(centerX, centerY + StrikeBracketLiftY, 9, 3, Pink100);
            else if (1 == step) DrawBrackets(centerX, centerY + StrikeBracketLiftY, 6, 3, Pink80);
            else DrawBrackets(centerX, centerY + StrikeBracketLiftY, 4, 2, PinkLight100);
        }

        // 꿰뚫리는 순간의 마름모 섬광: 흰 속 + 분홍 테두리 -> 분홍 속 + 흰 점 -> 작은 자홍 점.
        private void DrawFlash()
        {
            float t = age - LockEnd;
            if (t < 0f || t >= FlashStep * 3f) return;

            int step = (int)(t / FlashStep);
            if (0 == step)
            {
                OutlineDiamond(centerX, centerY, 5, Pink90);
                FillDiamond(centerX, centerY, 4, White100);
            }
            else if (1 == step)
            {
                FillDiamond(centerX, centerY, 3, Pink85);
                painter.Dot(centerX, centerY, White100);
            }
            else
            {
                FillDiamond(centerX, centerY, 1, Magenta40);
            }
        }

        // 관통 줄기: 맞은 자리에서 공격자 반대편으로 머리가 순식간에 뻗고, 꼬리(밑동 쪽)부터 머리를 따라잡으며 사라진다.
        // 밑동 쪽 PierceThickLength까지는 위아래 테두리가 붙은 3줄, 그 너머는 1줄이라 바늘처럼 끝이 가늘다.
        private void DrawPierce()
        {
            float t = age - LockEnd;
            if (t < 0f || age >= PierceEnd) return;

            float head = Mathf.Clamp01((t + 0.008f) / PierceGrowTime);
            float tail = Mathf.Clamp01((age - PierceTailStart) / (PierceEnd - PierceTailStart));
            int k0 = Mathf.RoundToInt(PierceLength * tail);
            int k1 = Mathf.RoundToInt(PierceLength * head);
            if (k1 <= k0) return;

            float u = t / (PierceEnd - LockEnd);
            Color32 core = u < 0.35f ? White100 : (u < 0.6f ? PinkLight80 : (u < 0.8f ? Pink50 : Magenta25));
            Color32 edge = u < 0.5f ? Pink70 : Magenta30;

            int baseX = centerX + exitDirX * PierceGap;

            // 두꺼운 구간 [k0, min(k1, PierceThickLength))의 위아래 테두리
            int thickEnd = Mathf.Min(k1, PierceThickLength);
            if (thickEnd > k0)
            {
                int ex0 = baseX + exitDirX * k0;
                int ex1 = baseX + exitDirX * (thickEnd - 1);
                painter.HLine(Mathf.Min(ex0, ex1), Mathf.Max(ex0, ex1), centerY + 1, edge);
                painter.HLine(Mathf.Min(ex0, ex1), Mathf.Max(ex0, ex1), centerY - 1, edge);
            }

            int cx0 = baseX + exitDirX * k0;
            int cx1 = baseX + exitDirX * (k1 - 1);
            painter.HLine(Mathf.Min(cx0, cx1), Mathf.Max(cx0, cx1), centerY, core);

            // 뻗는 동안 머리에 흰 점이 하나 더 맺힌다.
            if (head < 1f || t < PierceGrowTime + 0.02f)
            {
                painter.Rect(cx1 - (exitDirX > 0 ? 0 : 1), centerY - 1, cx1 + (exitDirX > 0 ? 2 : 1), centerY + 1, White100);
            }
        }

        // 들어온 쪽(공격자 쪽) 짧은 줄기: 꿰뚫리는 순간 한 번 비치고 꺼진다.
        private void DrawEntry()
        {
            if (age < LockEnd || age >= EntryEnd) return;

            int x0 = centerX - exitDirX * PierceGap;
            int x1 = centerX - exitDirX * (PierceGap + EntryLength - 1);
            painter.HLine(Mathf.Min(x0, x1), Mathf.Max(x0, x1), centerY, Pink70);
        }

        // 갈라진 금: 마디가 하나씩 뻗어 나가고, 뜨거운 분홍 -> 자홍 -> 포도주색으로 식으며 끝물에 깜빡여 꺼진다.
        private void DrawCracks()
        {
            if (age < LockEnd || age >= CrackEnd) return;

            int reveal = Mathf.Min(CrackSegments, 1 + (int)((age - LockEnd) / CrackRevealStep));
            Color32 color;
            if (age < CrackHotEnd) color = Pink90;
            else if (age < CrackCoolEnd) color = Magenta50;
            else
            {
                if (0 != ((int)(age * 30f) & 1)) return;
                color = Wine25;
            }

            for (int c = 0; c < CrackCount; c++)
            {
                int px = centerX;
                int py = centerY;
                for (int s = 0; s < reveal; s++)
                {
                    int seg = c * CrackSegments + s;
                    int nx = centerX + crackX[seg];
                    int ny = centerY + crackY[seg];
                    int count = crackPixelCount[seg];
                    if (count < 0)
                    {
                        painter.Line(px, py, nx, ny, 0, 0, 0, color);
                    }
                    else
                    {
                        int offset = seg * CrackPixelCapacity;
                        for (int k = 0; k < count; k++) painter.Dot(centerX + crackPixelX[offset + k], centerY + crackPixelY[offset + k], color);
                    }
                    px = nx;
                    py = ny;
                }
            }
        }

        private void DrawDusts()
        {
            for (int i = 0; i < DustCapacity; i++)
            {
                if (false == dusts[i].bActive || age < dusts[i].delay) continue;

                float u = dusts[i].age / dusts[i].life;
                int px = Mathf.RoundToInt(dusts[i].x);
                int py = Mathf.RoundToInt(dusts[i].y);

                if (KindShard == dusts[i].kind)
                {
                    DrawShard(px, py, dusts[i].vx, dusts[i].vy, dusts[i].age, u, dusts[i].colorMid);
                }
                else
                {
                    DrawChip(px, py, dusts[i].age, u, dusts[i].colorMid, dusts[i].colorLow);
                }
            }
        }

        // 줄기 파편: 머리는 밝고(처음엔 흰빛), 날아온 방향으로 분홍 -> 포도주색 꼬리를 끈다. 빠를수록 꼬리가 길고,
        // 느려지면 점 하나로 줄었다가 끝물에 점멸하며 꺼진다(VFX_CriticalImpact 불꽃 줄기와 같은 모양, 색만 다르다).
        private static void DrawShard(int _x, int _y, float _vx, float _vy, float _age, float _u, Color32 _colorMid)
        {
            if (_u > 0.78f && 0 != ((int)(_age * 30f) & 1)) return;

            float speed = Mathf.Sqrt(_vx * _vx + _vy * _vy);
            int tail = speed < 1f ? 0 : Mathf.Clamp(Mathf.RoundToInt(speed / 35f), 0, 5);
            if (_u > 0.6f) tail = Mathf.Min(tail, 1);

            Color32 head = _u < 0.3f ? White100 : (_u < 0.65f ? _colorMid : Magenta30);
            if (tail > 0)
            {
                float bx = -_vx / speed;
                float by = -_vy / speed;
                Color32 near = Pink55;
                Color32 far = Wine15;
                for (int k = tail; k >= 1; k--)
                {
                    painter.Dot(_x + Mathf.RoundToInt(bx * k), _y + Mathf.RoundToInt(by * k), k <= tail / 2 + 1 ? near : far);
                }
            }

            painter.Dot(_x, _y, head);
        }

        // 조각: 2x2로 튀어 1px로 줄고, 끝물에 점멸하며 꺼진다.
        private static void DrawChip(int _x, int _y, float _age, float _u, Color32 _colorMid, Color32 _colorLow)
        {
            if (_u > 0.75f && 0 != ((int)(_age * 30f) & 1)) return;

            Color32 color = _u < 0.4f ? _colorMid : _colorLow;
            if (_u < 0.5f) painter.Rect(_x, _y, _x + 2, _y + 2, color);
            else painter.Dot(_x, _y, color);
        }

        // //수명

        private void OnDisable()
        {
            // 스스로 꺼질 때(StopImmediate)도 불리므로 여기서는 enabled를 건드리지 않는다.
            StopInternal();
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
