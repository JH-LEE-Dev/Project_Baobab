using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 보석 단계(1 황금, 2 다이아, 3 무지개) 공용 팔레트. 화면에 보일 sRGB로 적어 두고, 리니어 색 공간이면 처음 쓸 때 한 번 변환한다.
    /// 색은 보석 사망 이펙트(TreeDeadStageVfxBuilder.BuildGemSpec)의 팔레트와 같은 계열이다.
    /// </summary>
    public static class GemTierPalette
    {
        public const int HueCount = 6;

        // [단계][0 가장 밝음 ~ 3 가장 진함]
        private static readonly Color32[][] SrgbTiers =
        {
            new[] { C(0xFFFFFF), C(0xFFFFFF), C(0xFFFFFF), C(0xFFFFFF) },
            new[] { C(0xFFF6C8), C(0xFFE45C), C(0xF2C64A), C(0xD99A2E) },
            new[] { C(0xF0FCFF), C(0xC8FFFF), C(0xA8E4FF), C(0x6FB4F0) },
            new[] { C(0xFFFFFF), C(0xFFE8F8), C(0xE8C8FF), C(0xC8A8FF) },
        };

        // 무지개 띠 색(분홍 -> 주황 -> 노랑 -> 초록 -> 하늘 -> 보라). 보석 사망 이펙트의 무지개 팔레트보다 한 단계 짙게 잡아 풀밭 위에서도 띠가 읽히게 한다.
        private static readonly Color32[] SrgbHues =
        {
            C(0xFF8CC8), C(0xFFB870), C(0xFFEC78), C(0x8CEBA0), C(0x80CCFF), C(0xBC9CFF),
        };

        private static readonly Color32 SrgbBarkLight = C(0x9A7048);
        private static readonly Color32 SrgbBarkDark = C(0x5E4128);

        private static Color32[][] tiers;
        private static Color32[] hues;
        private static Color32 barkLight;
        private static Color32 barkDark;

        public static readonly Color32 White = new Color32(255, 255, 255, 255);

        public static Color32 Tier(int _tier, int _shade)
        {
            EnsureReady();
            return tiers[Mathf.Clamp(_tier, 0, 3)][Mathf.Clamp(_shade, 0, 3)];
        }

        public static Color32 Hue(int _index)
        {
            EnsureReady();
            int i = _index % HueCount;
            if (i < 0) i += HueCount;
            return hues[i];
        }

        /// <summary>무지개면 _seed로 고른 띠 색, 아니면 단계의 _shade 색.</summary>
        public static Color32 TierOrHue(int _tier, int _shade, int _seed)
        {
            return 3 <= _tier ? Hue(_seed) : Tier(_tier, _shade);
        }

        public static Color32 BarkLight { get { EnsureReady(); return barkLight; } }
        public static Color32 BarkDark { get { EnsureReady(); return barkDark; } }

        public static Color32 WithGlow(Color32 _color, float _glow)
        {
            return new Color32(_color.r, _color.g, _color.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255f));
        }

        private static void EnsureReady()
        {
            if (null != tiers) return;

            bool bLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Color32[][] built = new Color32[SrgbTiers.Length][];
            for (int t = 0; t < SrgbTiers.Length; t++)
            {
                built[t] = new Color32[SrgbTiers[t].Length];
                for (int s = 0; s < SrgbTiers[t].Length; s++) built[t][s] = ToDisplay(SrgbTiers[t][s], bLinear);
            }

            hues = new Color32[SrgbHues.Length];
            for (int i = 0; i < SrgbHues.Length; i++) hues[i] = ToDisplay(SrgbHues[i], bLinear);

            barkLight = ToDisplay(SrgbBarkLight, bLinear);
            barkDark = ToDisplay(SrgbBarkDark, bLinear);
            tiers = built;
        }

        /// <summary>무지개 띠 여섯 색에 같은 발광 세기를 입혀 _out(길이 HueCount 이상)에 채운다. 둘레를 따라 점마다 색을 바꾸는 그리기에서 점마다 계산하지 않게 미리 만든다.</summary>
        public static void FillHuesWithGlow(Color32[] _out, float _glow)
        {
            EnsureReady();
            byte alpha = GlowByte(_glow);
            for (int i = 0; i < HueCount; i++) _out[i] = new Color32(hues[i].r, hues[i].g, hues[i].b, alpha);
        }

        /// <summary>WithGlow가 쓰는 것과 같은 발광 세기 바이트.</summary>
        public static byte GlowByte(float _glow)
        {
            return (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255f);
        }

        /// <summary>_color의 RGB에 이미 바이트로 정한 발광 세기를 입힌다(WithGlow와 같은 결과, 반올림 없이 그대로 쓴다).</summary>
        public static Color32 WithAlpha(Color32 _color, byte _alpha)
        {
            return new Color32(_color.r, _color.g, _color.b, _alpha);
        }

        private static Color32 C(int _rgb)
        {
            return new Color32((byte)((_rgb >> 16) & 0xFF), (byte)((_rgb >> 8) & 0xFF), (byte)(_rgb & 0xFF), 255);
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
    }

    /// <summary>
    /// 보석 연출 두 가지(VFX_GemTierBurst, VFX_GemLogTrail)가 함께 쓰는 런타임 머티리얼과 정렬 층 ID.
    /// 셰이더·발광 범위가 모두 같으므로 머티리얼 하나를 나눠 써서 SetPass·머티리얼 상수 갱신을 줄이고, 정렬 층은 이름 대신 ID를 한 번만 찾는다.
    /// </summary>
    public static class GemVfxShared
    {
        private const string ShaderName = "Custom/VFX/BrandWrapGlow";
        private const float GlowMin = 1f;
        private const float GlowMax = 2.6f;

        private static Material material;
        private static bool bShaderMissingWarned;

        public static readonly int FlyingItemLayerId = SortingLayer.NameToID("FlyingItem");
        public static readonly int ShockWaveLayerId = SortingLayer.NameToID("ShockWave");

        /// <summary>공용 머티리얼. 셰이더를 찾지 못하면 null(한 번만 경고하고 보석 연출을 건너뛴다).</summary>
        public static Material Material
        {
            get
            {
                if (null != material) return material;

                Shader shader = Shader.Find(ShaderName);
                if (null == shader)
                {
                    if (false == bShaderMissingWarned)
                    {
                        bShaderMissingWarned = true;
                        Debug.LogWarning("[GemVfx] 셰이더 " + ShaderName + "를 찾지 못해 보석 단계 연출을 건너뜁니다.");
                    }
                    return null;
                }

                material = new Material(shader) { name = "M_GemVfx (Runtime)" };
                material.SetFloat("_GlowMin", GlowMin);
                material.SetFloat("_GlowMax", GlowMax);
                return material;
            }
        }

        /// <summary>절차적 쿼드 메쉬 하나를 그릴 렌더러를 _go에 붙인다(그림자·프로브 끔, 꺼진 채로 시작). _quadReserve만큼 정점·인덱스 버퍼를 미리 잡아 재생 중에 다시 잡지 않게 한다.</summary>
        public static MeshRenderer CreateMeshRenderer(GameObject _go, string _meshName, int _quadReserve, out Mesh _mesh)
        {
            _mesh = new Mesh { name = _meshName };
            _mesh.MarkDynamic();
            PixelQuadBuffer.ReserveMesh(_mesh, _quadReserve);
            _go.AddComponent<MeshFilter>().sharedMesh = _mesh;

            MeshRenderer renderer = _go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = Material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.enabled = false;
            return renderer;
        }
    }

    /// <summary>
    /// 보석 나무가 변신할 때와 쓰러질 때 기존 이펙트 위에 단계별로 더하는 연출. 32 PPU 픽셀 격자의 절차적 쿼드 메쉬로 그린다
    /// (VFX_CriticalImpact와 같은 방식, 정점 알파 = 발광 세기인 BrandWrapGlow 셰이더). 프리팹 없이 InDungeonVFXManager가 런타임에 만든다.
    /// 윗 단계는 아랫 단계의 것을 모두 포함한다.
    ///
    /// 변신(Transform, 약 0.6초) - 일반 나무 사망 이펙트 대신 터진다. 보석 변신 스프라이트(TreeTransformVFX)는 그대로 함께 나온다.
    /// - 황금 이상: 나무 가운데서 껍질이 쩍 갈라지는 금이 뻗고 그 틈으로 보석 빛줄기가 새어 나오며, 껍질 조각이 튄다. 밑동에 보석 빛 고리 하나.
    /// - 다이아 이상: 각진 결정 조각이 사방으로 튀고, 나무 둘레에 반짝이 별이 차례로 켜진다(다이아 2개, 무지개 3개).
    /// - 무지개: 무지개 고리가 시차를 두고 하나 더 번진다.
    ///
    /// 사망(Death, 약 1초) - 보석 사망 이펙트(파티클) 위에 더한다.
    /// - 황금 이상: 쓰러진 자리 바닥에 반짝이가 안쪽부터 바깥으로 짧게 깔린다.
    /// - 다이아 이상: 큰 반짝이 별이 수관 둘레에서 더 터지고, 밑동 고리가 한 겹 더 번진다.
    /// - 무지개: 하늘로 솟는 무지개 빛기둥, 보석 가루 분수 한 번 더, 무지개 고리 한 겹 더(기존 포함 3겹).
    ///
    /// 반투명 페이드 없이 계단식 크기 변화·점멸로만 사라진다. 고정 크기 배열만 써서 재생 중 힙 할당이 없다. 그림일 뿐이라 판정과 무관하다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_GemTierBurst : MonoBehaviour
    {
        // //공용 상수

        private const float PixelsPerUnit = 32f;
        private const float TwoPi = Mathf.PI * 2f;

        // 바닥에 깔리는 것(고리·바닥 반짝이)은 나무 아래 바닥 충격파 층(ShockWave)에, 나머지는 날아다니는 것 층(FlyingItem, 나무 앞)에 그린다.
        private const int GroundSortingOrder = 1;
        private const int FrontSortingPrecision = 100;
        private const int FrontSortingOffset = 100;

        // 메쉬 버퍼를 미리 잡아 둘 크기(쿼드). 단계·모드별로 가장 많이 그리는 프레임(앞 약 250, 바닥 약 390)을 덮는다. 넘으면 그때 늘린다.
        private const int FrontQuadReserve = 256;
        private const int GroundQuadReserve = 400;

        private static readonly Color32 WhiteFull = new Color32(255, 255, 255, 255);

        public const int ModeTransform = 0;
        public const int ModeDeath = 1;

        // //입자

        private const int ParticleCapacity = 64;
        private const int KindBark = 0;          // 껍질 조각: 2x2 -> 1px, 중력으로 떨어진다
        private const int KindShard = 1;         // 결정 조각: 진행 방향으로 뾰족한 마름모
        private const int KindFountain = 2;      // 보석 가루 분수: ◆ -> + -> . 반짝이며 쏟아진다
        private const int KindGroundSparkle = 3; // 바닥 반짝이: 제자리에서 + / x 가 번갈아 반짝인다(바닥 층)
        private const int KindMote = 4;          // 빛기둥 안을 타고 오르는 빛 알갱이

        private struct Particle
        {
            public bool bActive;
            public int kind;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float drag;
            public float gravity;
            public float delay;
            public float age;
            public float life;
            public int seed;
        }

        // //반짝이 별

        private const int StarCapacity = 8;

        private struct Star
        {
            public bool bActive;
            public int x;
            public int y;
            public float delay;
            public float life;
            public int arm;
        }

        // //고리

        private const int RingCapacity = 3;

        private struct Ring
        {
            public bool bActive;
            public float delay;
            public float duration;
            public float startRadius;
            public float endRadius;
            public bool bRainbow;
        }

        // //금(변신)

        private const int CrackCapacity = 8;
        private const int CrackPoints = 4;              // 금 하나 = 꺾인 선 3마디
        private const float CrackGrowEnd = 0.06f;       // 이 시각까지 금이 뻗는다
        private const float CrackEnd = 0.44f;
        private const float LeakEnd = 0.3f;             // 빛줄기가 새어 나오는 시간
        private const int LeakLength = 9;

        // //빛기둥(무지개 사망)

        private const float PillarRiseEnd = 0.09f;
        private const float PillarHoldEnd = 0.36f;
        private const float PillarEnd = 0.62f;
        private const int PillarHeight = 340;           // px. 화면 위로 넘어갈 만큼
        private const int PillarWidth = 11;

        // //공유 자원

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(1024);
        private static readonly PixelPainter painter = new PixelPainter();

        // 무지개 고리 둘레 점마다 쓰는 띠 색(발광 세기를 입힌 것). 고리 하나 그리기 전에 한 번 채운다.
        private static readonly Color32[] ringHues = new Color32[GemTierPalette.HueCount];

        // //인스턴스 상태

        private MeshRenderer frontRenderer;
        private Mesh frontMesh;
        private ulong frontHash;
        private int frontVertexCount = -1;

        private Transform groundTransform;
        private MeshRenderer groundRenderer;
        private Mesh groundMesh;
        private ulong groundHash;
        private int groundVertexCount = -1;

        private readonly Particle[] particles = new Particle[ParticleCapacity];
        private readonly Star[] stars = new Star[StarCapacity];
        private readonly Ring[] rings = new Ring[RingCapacity];
        private readonly int[] crackX = new int[CrackCapacity * CrackPoints];
        private readonly int[] crackY = new int[CrackCapacity * CrackPoints];
        private int crackCount;

        private bool bPlaying;
        private bool bFresh;
        private float age;
        private float endTime;
        private int mode;
        private int tier;
        private int topX;
        private int topY;
        private int bottomX;
        private int bottomY;
        private int centerX;      // 변신: 금이 갈라지는 자리(줄기 가운데)
        private int centerY;
        private bool bPillar;
        private uint randomState = 1u;

        public bool IsPlaying => bPlaying;

        // //퍼블릭

        /// <summary>_parent 아래에 이펙트를 하나 만든다. 셰이더를 찾지 못하면 null(이펙트 없이 진행). SortingGroup이 없는 부모를 넘겨야 한다.</summary>
        public static VFX_GemTierBurst Create(Transform _parent)
        {
            if (null == GemVfxShared.Material) return null;

            GameObject go = new GameObject("GemTierBurstVFX");
            go.transform.SetParent(_parent, false);
            VFX_GemTierBurst vfx = go.AddComponent<VFX_GemTierBurst>();
            vfx.Initialize();
            return vfx;
        }

        /// <summary>
        /// 보석 변신(ModeTransform) 또는 보석 나무 사망(ModeDeath) 추가 연출을 재생한다. _top/_bottom은 나무 수관·밑동 자리(TreeVisualComponent의 루트),
        /// _tier는 1 황금 / 2 다이아 / 3 무지개(변신은 바뀐 뒤 단계, 사망은 최종 단계).
        /// </summary>
        public void Play(int _mode, int _tier, Vector3 _top, Vector3 _bottom)
        {
            mode = _mode;
            tier = Mathf.Clamp(_tier, 1, 3);
            topX = ToPixel(_top.x);
            topY = ToPixel(_top.y);
            bottomX = ToPixel(_bottom.x);
            bottomY = ToPixel(_bottom.y);
            centerX = bottomX + Mathf.RoundToInt((topX - bottomX) * 0.5f);
            centerY = bottomY + Mathf.RoundToInt((topY - bottomY) * 0.5f);

            frontRenderer.sortingOrder = -Mathf.RoundToInt(_bottom.y * FrontSortingPrecision) + FrontSortingOffset;

            for (int i = 0; i < ParticleCapacity; i++) particles[i].bActive = false;
            for (int i = 0; i < StarCapacity; i++) stars[i].bActive = false;
            for (int i = 0; i < RingCapacity; i++) rings[i].bActive = false;
            crackCount = 0;
            bPillar = false;
            endTime = 0f;

            if (ModeTransform == mode) SetupTransform();
            else SetupDeath();

            age = 0f;
            bFresh = true;
            bPlaying = true;
            frontHash = 0UL;
            frontVertexCount = -1;
            groundHash = 0UL;
            groundVertexCount = -1;
            frontRenderer.enabled = false;
            groundRenderer.enabled = false;

            // 쉬는 동안은 컴포넌트를 꺼 두어 매 프레임 LateUpdate 호출 자체가 없게 한다.
            enabled = true;
        }

        /// <summary>재생 중인 것을 바로 끝낸다(던전 이탈 등).</summary>
        public void StopImmediate()
        {
            Halt();
            if (enabled) enabled = false;
        }

        private void Halt()
        {
            bPlaying = false;
            for (int i = 0; i < ParticleCapacity; i++) particles[i].bActive = false;
            if (null != frontRenderer) frontRenderer.enabled = false;
            if (null != groundRenderer) groundRenderer.enabled = false;
        }

        // //장면 구성

        private void SetupTransform()
        {
            int particleCursor = 0;
            int starCursor = 0;

            // 금: 줄기 가운데서 사방으로 3마디씩 꺾이며 뻗는다. 위아래(줄기 결)로 조금 더 길다.
            crackCount = 4 + tier;
            for (int c = 0; c < crackCount; c++)
            {
                float angle = TwoPi * (c + 0.15f + Rand01() * 0.7f) / crackCount;
                float px = centerX;
                float py = centerY;
                crackX[c * CrackPoints] = centerX;
                crackY[c * CrackPoints] = centerY;
                for (int p = 1; p < CrackPoints; p++)
                {
                    angle += (Rand01() - 0.5f) * 1.1f;
                    float length = Mathf.Lerp(2.5f, 4f, Rand01());
                    float vertical = Mathf.Abs(Mathf.Sin(angle));
                    length *= Mathf.Lerp(0.85f, 1.25f, vertical);
                    px += Mathf.Cos(angle) * length;
                    py += Mathf.Sin(angle) * length;
                    crackX[c * CrackPoints + p] = Mathf.RoundToInt(px);
                    crackY[c * CrackPoints + p] = Mathf.RoundToInt(py);
                }
            }

            // 껍질 조각: 갈라진 자리에서 튀어 떨어진다. 몇 개는 보석 빛을 머금은 조각이다.
            int barkCount = 7 + tier;
            for (int i = 0; i < barkCount; i++)
            {
                float angle = TwoPi * (i + Rand01() * 0.8f) / barkCount;
                float speed = Mathf.Lerp(70f, 130f, Rand01());
                AddParticle(ref particleCursor, KindBark, centerX, centerY,
                    Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed * 0.7f + 60f, 2.5f, 320f, 0f, Mathf.Lerp(0.38f, 0.52f, Rand01()));
            }

            // 밑동 고리 하나(무지개는 무지개 고리).
            AddRing(0.02f, 0.32f, 6f, 28f, 3 <= tier);

            if (2 <= tier)
            {
                // 결정 조각: 사방으로 빠르게 튀어 감속하며 식는다.
                int shardCount = 3 <= tier ? 12 : 9;
                for (int i = 0; i < shardCount; i++)
                {
                    float angle = TwoPi * (i + Rand01() * 0.6f) / shardCount;
                    float speed = Mathf.Lerp(170f, 250f, Rand01());
                    AddParticle(ref particleCursor, KindShard, centerX, centerY,
                        Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed * 0.8f + 20f, 4.5f, 140f, 0.01f, Mathf.Lerp(0.4f, 0.55f, Rand01()));
                }

                // 반짝이 별: 수관 둘레에서 차례로 켜진다.
                int starCount = 3 <= tier ? 3 : 2;
                int treeHeight = Mathf.Max(24, topY - bottomY);
                for (int i = 0; i < starCount; i++)
                {
                    float angle = Mathf.Lerp(0.35f, Mathf.PI - 0.35f, (i + 0.5f) / starCount) + (Rand01() - 0.5f) * 0.4f;
                    if (1 == (i & 1)) angle = Mathf.PI - angle;
                    int sx = topX + Mathf.RoundToInt(Mathf.Cos(angle) * Mathf.Lerp(14f, 20f, Rand01()));
                    int sy = topY + Mathf.RoundToInt(Mathf.Sin(angle) * treeHeight * 0.35f) - 2;
                    AddStar(ref starCursor, sx, sy, 0.1f + i * 0.11f, 0.26f, 7);
                }
            }

            if (3 <= tier)
            {
                AddRing(0.13f, 0.36f, 8f, 38f, true);
            }

            endTime = Mathf.Max(endTime, CrackEnd);
        }

        private void SetupDeath()
        {
            int particleCursor = 0;
            int starCursor = 0;

            // 바닥 반짝이: 쓰러진 자리 둘레 바닥에 안쪽부터 바깥으로 짧게 깔린다.
            int sparkleCount = 10 + tier * 4;
            for (int i = 0; i < sparkleCount; i++)
            {
                float angle = TwoPi * (i + Rand01() * 0.7f) / sparkleCount;
                float radius = Mathf.Lerp(8f, 42f, Mathf.Sqrt(Rand01()));
                float x = bottomX + Mathf.Cos(angle) * radius;
                float y = bottomY + Mathf.Sin(angle) * radius * 0.5f;
                float delay = 0.04f + radius / 42f * 0.22f;
                AddParticle(ref particleCursor, KindGroundSparkle, x, y, 0f, 0f, 0f, 0f, delay, Mathf.Lerp(0.34f, 0.48f, Rand01()));
            }

            if (2 <= tier)
            {
                // 큰 반짝이 별: 수관 둘레에서 기존 세 개에 더해 터진다.
                int starCount = 3 <= tier ? 5 : 3;
                int treeHeight = Mathf.Max(24, topY - bottomY);
                for (int i = 0; i < starCount; i++)
                {
                    float angle = TwoPi * (i + 0.25f + Rand01() * 0.5f) / starCount;
                    int sx = topX + Mathf.RoundToInt(Mathf.Cos(angle) * Mathf.Lerp(16f, 26f, Rand01()));
                    int sy = topY + Mathf.RoundToInt(Mathf.Sin(angle) * Mathf.Lerp(0.3f, 0.45f, Rand01()) * treeHeight);
                    AddStar(ref starCursor, sx, sy, 0.06f + i * 0.08f + Rand01() * 0.03f, 0.3f, 3 <= tier ? 9 : 8);
                }

                // 밑동 고리 한 겹 더(기존 고리 바로 뒤를 따라 번진다).
                AddRing(0.1f, 0.4f, 8f, 40f, false);
            }

            if (3 <= tier)
            {
                // 무지개 고리 한 겹 더(기존 포함 3겹).
                AddRing(0.2f, 0.44f, 10f, 50f, true);

                // 무지개 빛기둥.
                bPillar = true;
                endTime = Mathf.Max(endTime, PillarEnd);

                // 빛기둥 안을 타고 오르는 빛 알갱이.
                for (int i = 0; i < 10; i++)
                {
                    float x = bottomX + (Rand01() - 0.5f) * (PillarWidth - 3);
                    AddParticle(ref particleCursor, KindMote, x, bottomY + Rand01() * 20f, 0f, Mathf.Lerp(260f, 420f, Rand01()), 0f, 0f,
                        0.02f + i * 0.025f, Mathf.Lerp(0.35f, 0.5f, Rand01()));
                }

                // 보석 가루 분수 한 번 더: 수관에서 높이 솟았다가 반짝이며 쏟아진다.
                for (int i = 0; i < 12; i++)
                {
                    float spread = (i + Rand01()) / 12f - 0.5f;
                    AddParticle(ref particleCursor, KindFountain, topX + spread * 10f, topY,
                        spread * 150f, Mathf.Lerp(190f, 270f, Rand01()), 0.6f, 330f, 0.05f + Rand01() * 0.06f, Mathf.Lerp(0.8f, 1.0f, Rand01()));
                }
            }
        }

        private void AddParticle(ref int _cursor, int _kind, float _x, float _y, float _vx, float _vy, float _drag, float _gravity, float _delay, float _life)
        {
            if (_cursor >= ParticleCapacity) return;

            particles[_cursor] = new Particle
            {
                bActive = true,
                kind = _kind,
                x = _x,
                y = _y,
                vx = _vx,
                vy = _vy,
                drag = _drag,
                gravity = _gravity,
                delay = _delay,
                age = 0f,
                life = _life,
                seed = (int)(Rand01() * 997f),
            };
            _cursor++;
            endTime = Mathf.Max(endTime, _delay + _life);
        }

        private void AddStar(ref int _cursor, int _x, int _y, float _delay, float _life, int _arm)
        {
            if (_cursor >= StarCapacity) return;

            stars[_cursor] = new Star { bActive = true, x = _x, y = _y, delay = _delay, life = _life, arm = _arm };
            _cursor++;
            endTime = Mathf.Max(endTime, _delay + _life);
        }

        private void AddRing(float _delay, float _duration, float _startRadius, float _endRadius, bool _bRainbow)
        {
            for (int i = 0; i < RingCapacity; i++)
            {
                if (rings[i].bActive) continue;

                rings[i] = new Ring { bActive = true, delay = _delay, duration = _duration, startRadius = _startRadius, endRadius = _endRadius, bRainbow = _bRainbow };
                endTime = Mathf.Max(endTime, _delay + _duration);
                return;
            }
        }

        // //초기화

        private void Initialize()
        {
            frontRenderer = GemVfxShared.CreateMeshRenderer(gameObject, "VFX_GemTierBurst_Front", FrontQuadReserve, out frontMesh);
            frontRenderer.sortingLayerID = GemVfxShared.FlyingItemLayerId;

            GameObject ground = new GameObject("Ground");
            groundTransform = ground.transform;
            groundTransform.SetParent(transform, false);
            groundRenderer = GemVfxShared.CreateMeshRenderer(ground, "VFX_GemTierBurst_Ground", GroundQuadReserve, out groundMesh);
            groundRenderer.sortingLayerID = GemVfxShared.ShockWaveLayerId;
            groundRenderer.sortingOrder = GroundSortingOrder;

            randomState = unchecked((uint)Random.Range(1, int.MaxValue) * 2654435761u) | 1u;
            enabled = false;
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
            if (false == bPlaying)
            {
                enabled = false;
                return;
            }

            if (false == Simulate(bFresh ? 0f : Time.deltaTime)) return;

            // 화면 밖이면 시간만 흐르게 두고 그리지 않는다. 빛기둥은 위로 길게 솟으므로 범위를 위로 넉넉히 잡는다.
            float height = bPillar ? PillarHeight / PixelsPerUnit : 4f;
            Vector3 center = new Vector3(bottomX / PixelsPerUnit, bottomY / PixelsPerUnit + height * 0.5f, 0f);
            Vector3 size = new Vector3(4f + PixelVfxCulling.MarginUnits * 2f, height + 2f + PixelVfxCulling.MarginUnits * 2f, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(center, size))
            {
                frontRenderer.enabled = false;
                groundRenderer.enabled = false;
                return;
            }

            Render();
        }

        // 시간을 _dt만큼 흘린다. 끝났으면 정리하고 false.
        private bool Simulate(float _dt)
        {
            float dt = _dt;
            bFresh = false;
            age += dt;

            if (dt > 0f)
            {
                for (int i = 0; i < ParticleCapacity; i++)
                {
                    if (false == particles[i].bActive) continue;
                    if (age < particles[i].delay) continue;

                    particles[i].age += dt;
                    float damping = Mathf.Exp(-particles[i].drag * dt);
                    particles[i].vx *= damping;
                    particles[i].vy = particles[i].vy * damping - particles[i].gravity * dt;
                    particles[i].x += particles[i].vx * dt;
                    particles[i].y += particles[i].vy * dt;
                    if (particles[i].age >= particles[i].life) particles[i].bActive = false;
                }
            }

            if (age >= endTime)
            {
                StopImmediate();
                return false;
            }

            return true;
        }

        private void Render()
        {
            // 바닥 층
            painter.Begin(buffer, groundTransform.worldToLocalMatrix, 0f);
            DrawRings();
            DrawParticles(true);
            painter.Commit(groundMesh, groundRenderer, ref groundHash, ref groundVertexCount);

            // 앞 층
            painter.Begin(buffer, transform.worldToLocalMatrix, 0f);
            if (bPillar) DrawPillar();
            if (ModeTransform == mode) DrawCracks();
            DrawParticles(false);
            DrawStars();
            painter.Commit(frontMesh, frontRenderer, ref frontHash, ref frontVertexCount);
        }

        // //그리기

        // 껍질이 갈라지는 금과 그 틈에서 새는 빛. 금은 흰빛으로 번쩍 갈라졌다가 보석 색 -> 진한 색으로 식고 점멸하며 닫힌다.
        // 빛줄기는 금 끝에서 같은 방향으로 실선으로 뻗었다가 짧아지며, 가운데에는 처음 잠깐 흰 빛 덩이가 맺힌다.
        private void DrawCracks()
        {
            if (age >= CrackEnd) return;
            if (age > CrackEnd - 0.08f && 0 != ((int)(age * 30f) & 1)) return;

            float grow = Mathf.Clamp01((age + 0.015f) / CrackGrowEnd);
            float u = age / CrackEnd;
            Color32 crackColor = u < 0.2f ? WhiteFull
                : (u < 0.5f ? GemTierPalette.WithGlow(GemTierPalette.Tier(tier, 1), 0.75f)
                : (u < 0.78f ? GemTierPalette.WithGlow(GemTierPalette.Tier(tier, 2), 0.45f)
                : GemTierPalette.WithGlow(GemTierPalette.Tier(tier, 3), 0.2f)));

            for (int c = 0; c < crackCount; c++)
            {
                // 금이 뻗는 동안에는 마디를 하나씩 드러낸다.
                float reach = grow * (CrackPoints - 1);
                int baseIndex = c * CrackPoints;
                int lastX = crackX[baseIndex];
                int lastY = crackY[baseIndex];
                int prevX = lastX;
                int prevY = lastY;
                for (int p = 1; p < CrackPoints; p++)
                {
                    float segment = Mathf.Clamp01(reach - (p - 1));
                    if (segment <= 0f) break;

                    int x0 = crackX[baseIndex + p - 1];
                    int y0 = crackY[baseIndex + p - 1];
                    int x1 = x0 + Mathf.RoundToInt((crackX[baseIndex + p] - x0) * segment);
                    int y1 = y0 + Mathf.RoundToInt((crackY[baseIndex + p] - y0) * segment);
                    Color32 color = 3 <= tier && u >= 0.2f ? GemTierPalette.WithAlpha(GemTierPalette.Hue(c + p), crackColor.a) : crackColor;
                    painter.Line(x0, y0, x1, y1, 0, 0, 0, color);
                    prevX = x0;
                    prevY = y0;
                    lastX = x1;
                    lastY = y1;
                }

                // 새어 나오는 빛줄기: 금 끝에서 마지막 마디 방향으로 뻗는다.
                if (grow >= 1f && age < LeakEnd)
                {
                    float dx = lastX - prevX;
                    float dy = lastY - prevY;
                    float length = Mathf.Sqrt(dx * dx + dy * dy);
                    if (length > 0.5f)
                    {
                        float leakU = (age - CrackGrowEnd) / (LeakEnd - CrackGrowEnd);
                        float extend = leakU < 0.4f ? Mathf.Lerp(3f, LeakLength, leakU / 0.4f) : Mathf.Lerp(LeakLength, 2f, (leakU - 0.4f) / 0.6f);
                        int ex = lastX + Mathf.RoundToInt(dx / length * extend);
                        int ey = lastY + Mathf.RoundToInt(dy / length * extend);
                        Color32 leak = 3 <= tier ? GemTierPalette.Hue(c * 2 + (int)(age * 20f)) : GemTierPalette.Tier(tier, leakU < 0.5f ? 0 : 1);
                        painter.Line(lastX, lastY, ex, ey, 0, 0, 0, GemTierPalette.WithGlow(leak, leakU < 0.5f ? 0.85f : 0.5f));
                    }
                }
            }

            // 가운데 빛 덩이
            if (age < 0.05f)
            {
                painter.Shape(PixelPainter.ShapeCircle, centerX - 2, centerY - 2, WhiteFull);
                painter.Rect(centerX - 3, centerY, centerX + 4, centerY + 1, WhiteFull);
                painter.Rect(centerX, centerY - 3, centerX + 1, centerY + 4, WhiteFull);
            }
            else if (age < 0.12f)
            {
                painter.Shape(PixelPainter.ShapePlus, centerX, centerY, GemTierPalette.WithGlow(GemTierPalette.Tier(tier, 0), 0.8f));
            }
        }

        // 바닥 고리: 끊기지 않는 실선으로 계단식으로 커지며 흰빛 -> 보석 색 -> 진한 색으로 식고, 마지막에 깜빡이며 사라진다. 처음 잠깐은 두 겹(2px)이다.
        // 무지개 고리는 둘레를 따라 무지개 띠가 세 번 돌고, 띠가 시계 방향으로 흐른다.
        private void DrawRings()
        {
            for (int r = 0; r < RingCapacity; r++)
            {
                if (false == rings[r].bActive) continue;

                float ringAge = age - rings[r].delay;
                if (ringAge < 0f || ringAge >= rings[r].duration) continue;

                const int Steps = 9;
                float u = Mathf.Floor(ringAge / rings[r].duration * Steps) / (Steps - 1);
                float grow = 1f - (1f - u) * (1f - u);
                int rx = Mathf.RoundToInt(Mathf.Lerp(rings[r].startRadius, rings[r].endRadius, grow));
                int ry = Mathf.Max(1, Mathf.RoundToInt(rx * 0.5f));
                float glow = u < 0.25f ? 0.9f : (u < 0.6f ? 0.5f : 0.2f);

                // 끝까지 이어진 실선으로 퍼지고, 마지막 잠깐은 깜빡이며 사라진다.
                if (ringAge / rings[r].duration > 0.82f && 0 != ((int)(ringAge * 30f) & 1)) continue;

                DrawRingOutline(bottomX, bottomY, rx, ry, rings[r].bRainbow, u, glow, ringAge);
                if (u < 0.35f && rx > 2) DrawRingOutline(bottomX, bottomY, rx - 1, Mathf.Max(1, ry - 1), rings[r].bRainbow, u, glow, ringAge);
            }
        }

        private void DrawRingOutline(int _cx, int _cy, int _rx, int _ry, bool _bRainbow, float _u, float _glow, float _ringAge)
        {
            if (false == _bRainbow)
            {
                Color32 color = _u < 0.25f ? GemTierPalette.White : GemTierPalette.Tier(tier, _u < 0.6f ? 1 : 3);
                painter.RingArc(_cx, _cy, _rx, _ry, 1, 0, 0f, GemTierPalette.WithGlow(color, _glow));
                return;
            }

            int count = PixelEllipse.Build(_rx, _ry);
            if (count <= 0) return;

            // 처음 잠깐은 흰빛 한 색이므로 실선 고리와 같은 경로로 그린다.
            if (_u < 0.12f)
            {
                Color32 white = GemTierPalette.WithGlow(GemTierPalette.White, _glow);
                for (int i = 0; i < count; i++) painter.Dot(_cx + PixelEllipse.OutX[i], _cy + PixelEllipse.OutY[i], white);
                return;
            }

            // 띠 색은 여섯 가지뿐이라 발광 세기를 입힌 색을 먼저 만들어 두고, 점마다 띠 번호만 센다(나눗셈 없이 누적).
            GemTierPalette.FillHuesWithGlow(ringHues, _glow);
            int bands = GemTierPalette.HueCount * 3;
            int flow = (int)(_ringAge * 24f);
            int band = flow;            // (i * bands) / count + flow
            int remainder = 0;          // (i * bands) % count
            for (int i = 0; i < count; i++)
            {
                painter.Dot(_cx + PixelEllipse.OutX[i], _cy + PixelEllipse.OutY[i], ringHues[band % GemTierPalette.HueCount]);

                remainder += bands;
                while (remainder >= count)
                {
                    remainder -= count;
                    band++;
                }
            }
        }

        // 무지개 빛기둥: 밑동에서 하늘로 순식간에 솟고(머리에 흰 빛), 잠시 버틴 뒤 가늘어지며 아래부터 하늘로 떠올라 사라진다.
        // 가운데 줄은 흰빛, 바깥 줄들은 무지개 띠이고 띠가 위로 흐른다.
        private void DrawPillar()
        {
            if (age >= PillarEnd) return;

            int bottom = bottomY;
            int top;
            int width;
            if (age < PillarRiseEnd)
            {
                float rise = (age + 0.02f) / (PillarRiseEnd + 0.02f);
                top = bottomY + Mathf.RoundToInt(PillarHeight * rise * rise);
                width = PillarWidth - 4;
            }
            else if (age < PillarHoldEnd)
            {
                top = bottomY + PillarHeight;
                float hold = (age - PillarRiseEnd) / (PillarHoldEnd - PillarRiseEnd);
                width = hold < 0.4f ? PillarWidth : (hold < 0.75f ? PillarWidth - 2 : PillarWidth - 4);
            }
            else
            {
                float lift = (age - PillarHoldEnd) / (PillarEnd - PillarHoldEnd);
                top = bottomY + PillarHeight;
                bottom = bottomY + Mathf.RoundToInt(PillarHeight * lift * lift);
                width = lift < 0.35f ? PillarWidth - 6 : (lift < 0.7f ? 3 : 1);
            }

            if (top <= bottom) return;

            int half = width / 2;
            int flow = (int)(age * 30f);
            for (int dx = -half; dx <= half; dx++)
            {
                int column = Mathf.Abs(dx);
                Color32 color;
                float glow;
                if (0 == column)
                {
                    color = GemTierPalette.White;
                    glow = 1f;
                }
                else if (1 == column && width >= 5)
                {
                    color = GemTierPalette.Tier(3, 1);
                    glow = 0.8f;
                }
                else
                {
                    color = GemTierPalette.Hue(column + flow / 3);
                    glow = column == half ? 0.3f : 0.6f;
                }

                // 바깥 줄은 아래쪽 끝을 계단처럼 조금씩 올려, 기둥 밑이 뾰족하게 모인다.
                int columnBottom = bottom + column * 2;
                painter.Rect(bottomX + dx, columnBottom, bottomX + dx + 1, top, GemTierPalette.WithGlow(color, glow));
            }

            // 솟는 동안 머리에 맺히는 흰 빛
            if (age < PillarRiseEnd + 0.03f)
            {
                painter.Rect(bottomX - half - 1, top - 3, bottomX + half + 2, top, WhiteFull);
            }

            // 처음 잠깐 밑동이 번쩍인다
            if (age < 0.08f)
            {
                painter.Rect(bottomX - half - 4, bottomY - 1, bottomX + half + 5, bottomY + 2, WhiteFull);
                painter.Rect(bottomX - half - 2, bottomY - 2, bottomX + half + 3, bottomY + 3, WhiteFull);
            }
        }

        private void DrawParticles(bool _bGroundLayer)
        {
            for (int i = 0; i < ParticleCapacity; i++)
            {
                if (false == particles[i].bActive) continue;
                if (age < particles[i].delay) continue;

                bool bGround = KindGroundSparkle == particles[i].kind;
                if (bGround != _bGroundLayer) continue;

                float u = particles[i].age / particles[i].life;
                int px = Mathf.RoundToInt(particles[i].x);
                int py = Mathf.RoundToInt(particles[i].y);
                int seed = particles[i].seed;
                float pAge = particles[i].age;

                switch (particles[i].kind)
                {
                    case KindBark: DrawBark(px, py, u, pAge, seed); break;
                    case KindShard: DrawShard(px, py, particles[i].vx, particles[i].vy, u, pAge, seed); break;
                    case KindFountain: DrawFountain(px, py, u, pAge, seed); break;
                    case KindGroundSparkle: DrawSparkle(px, py, u, pAge, seed); break;
                    case KindMote: DrawMote(px, py, u, seed); break;
                }
            }
        }

        // 껍질 조각: 2x2 덩이로 튀다가 1px로 줄고, 끝물에 점멸한다. 셋 중 하나는 보석 빛을 머금어 반짝인다.
        private void DrawBark(int _x, int _y, float _u, float _age, int _seed)
        {
            if (_u > 0.8f && 0 != ((int)(_age * 30f) & 1)) return;

            bool bGlowing = 0 == (_seed % 3);
            Color32 color = bGlowing
                ? GemTierPalette.WithGlow(GemTierPalette.TierOrHue(tier, 1, _seed), _u < 0.5f ? 0.7f : 0.35f)
                : GemTierPalette.WithGlow(0 == (_seed & 1) ? GemTierPalette.BarkLight : GemTierPalette.BarkDark, 0f);

            if (_u < 0.55f) painter.Rect(_x, _y, _x + 2, _y + 2, color);
            else painter.Dot(_x, _y, color);
        }

        // 결정 조각: 진행 방향으로 뾰족한 마름모(머리 흰빛). 느려질수록 짧아지고 진한 색으로 식는다.
        private void DrawShard(int _x, int _y, float _vx, float _vy, float _u, float _age, int _seed)
        {
            if (_u > 0.78f && 0 != ((int)(_age * 30f) & 1)) return;

            float speed = Mathf.Sqrt(_vx * _vx + _vy * _vy);
            int length = _u < 0.3f ? 4 : (_u < 0.6f ? 3 : 1);
            Color32 body = GemTierPalette.WithGlow(GemTierPalette.TierOrHue(tier, _u < 0.5f ? 1 : 2, _seed), _u < 0.5f ? 0.7f : 0.35f);
            Color32 head = _u < 0.4f ? WhiteFull : body;

            if (speed < 1f || length <= 1)
            {
                painter.Dot(_x, _y, head);
                return;
            }

            float bx = -_vx / speed;
            float by = -_vy / speed;
            int tx = _x + Mathf.RoundToInt(bx * length);
            int ty = _y + Mathf.RoundToInt(by * length);
            painter.Line(tx, ty, _x, _y, 0, 0, 0, body);

            // 가운데 마디 양옆에 한 칸씩 붙여 각진 결정 모양을 만든다.
            int mx = _x + Mathf.RoundToInt(bx * length * 0.5f);
            int my = _y + Mathf.RoundToInt(by * length * 0.5f);
            int sx = Mathf.RoundToInt(-by);
            int sy = Mathf.RoundToInt(bx);
            if (0 == sx && 0 == sy) sx = 1;
            painter.Dot(mx + sx, my + sy, body);
            painter.Dot(mx - sx, my - sy, body);
            painter.Dot(_x, _y, head);
        }

        // 보석 가루: ◆ -> + / x -> . 로 줄며 3단계로 반짝인다.
        private void DrawFountain(int _x, int _y, float _u, float _age, int _seed)
        {
            if (_u > 0.82f && 0 != ((int)(_age * 30f) & 1)) return;

            int twinkle = ((int)(_age * 14f) + _seed) % 3;
            float glow = (0 == twinkle ? 0.35f : (1 == twinkle ? 0.6f : 1f)) * (1f - _u * 0.5f);
            Color32 color = GemTierPalette.WithGlow(2 == twinkle && _u < 0.5f ? GemTierPalette.White : GemTierPalette.TierOrHue(tier, 1, _seed), glow);

            if (_u < 0.3f) painter.Shape(PixelPainter.ShapeDiamond, _x, _y, color);
            else if (_u < 0.65f) painter.Shape(0 == (((int)(_age * 12f) + _seed) & 1) ? PixelPainter.ShapePlus : PixelPainter.ShapeCross, _x, _y, color);
            else painter.Dot(_x, _y, color);
        }

        // 바닥 반짝이: 점 -> + / x 번갈아 -> 점으로 짧게 반짝인다.
        private void DrawSparkle(int _x, int _y, float _u, float _age, int _seed)
        {
            if (_u > 0.75f && 0 != ((int)(_age * 30f) & 1)) return;

            Color32 color = _u < 0.2f
                ? WhiteFull
                : GemTierPalette.WithGlow(GemTierPalette.TierOrHue(tier, _u < 0.55f ? 1 : 2, _seed), _u < 0.55f ? 0.7f : 0.35f);

            if (_u < 0.12f || _u > 0.7f)
            {
                painter.Dot(_x, _y, color);
                return;
            }

            bool bPlus = 0 == (((int)(_age * 12f) + _seed) & 1);
            painter.Shape(bPlus ? PixelPainter.ShapePlus : PixelPainter.ShapeCross, _x, _y, color);
        }

        // 빛 알갱이: 세로로 긴 두 칸 점이 기둥을 타고 오른다.
        private void DrawMote(int _x, int _y, float _u, int _seed)
        {
            Color32 color = GemTierPalette.WithGlow(_u < 0.5f ? GemTierPalette.White : GemTierPalette.Hue(_seed), _u < 0.5f ? 1f : 0.6f);
            painter.Rect(_x, _y, _x + 1, _y + (_u < 0.6f ? 3 : 1), color);
        }

        // 반짝이 별: 작은 + 로 켜져 큰 4갈래 별(가운데 흰 3x3 + 대각 광선)로 번쩍였다가 계단식으로 줄어든다.
        private void DrawStars()
        {
            for (int s = 0; s < StarCapacity; s++)
            {
                if (false == stars[s].bActive) continue;

                float starAge = age - stars[s].delay;
                if (starAge < 0f || starAge >= stars[s].life) continue;

                float u = starAge / stars[s].life;
                int x = stars[s].x;
                int y = stars[s].y;
                int armMax = stars[s].arm;
                Color32 white = WhiteFull;
                Color32 armColor = GemTierPalette.WithGlow(GemTierPalette.TierOrHue(tier, 1, s * 2 + (int)(starAge * 20f)), 0.8f);

                if (u < 0.15f)
                {
                    painter.Shape(PixelPainter.ShapePlus, x, y, white);
                }
                else if (u < 0.45f)
                {
                    painter.HLine(x - armMax, x + armMax, y, armColor);
                    painter.Rect(x, y - armMax, x + 1, y + armMax + 1, armColor);
                    painter.HLine(x - armMax / 2, x + armMax / 2, y, white);
                    painter.Rect(x, y - armMax / 2, x + 1, y + armMax / 2 + 1, white);
                    painter.Rect(x - 1, y - 1, x + 2, y + 2, white);
                    for (int d = 2; d <= armMax / 3 + 1; d++)
                    {
                        painter.Dot(x + d, y + d, armColor);
                        painter.Dot(x - d, y + d, armColor);
                        painter.Dot(x + d, y - d, armColor);
                        painter.Dot(x - d, y - d, armColor);
                    }
                }
                else if (u < 0.7f)
                {
                    int arm = Mathf.Max(2, armMax * 3 / 5);
                    painter.HLine(x - arm, x + arm, y, armColor);
                    painter.Rect(x, y - arm, x + 1, y + arm + 1, armColor);
                    painter.Dot(x, y, white);
                }
                else
                {
                    if (0 != ((int)(starAge * 30f) & 1)) continue;
                    painter.Shape(PixelPainter.ShapePlus, x, y, armColor);
                }
            }
        }

        // //수명

        private void OnDisable()
        {
            Halt();
        }

        private void OnDestroy()
        {
            if (null != frontMesh)
            {
                Destroy(frontMesh);
                frontMesh = null;
            }

            if (null != groundMesh)
            {
                Destroy(groundMesh);
                groundMesh = null;
            }
        }
    }
}
