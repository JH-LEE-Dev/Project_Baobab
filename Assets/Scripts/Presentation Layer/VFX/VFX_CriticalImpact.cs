using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 치명타 전용 임팩트. 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다
    /// (VFX_TownMoney와 같은 방식, 정점 알파 = HDR 발광 세기인 BrandWrapGlow 셰이더). 프리팹 없이 InDungeonVFXManager가 런타임에 만든다.
    /// 인스턴스 하나가 치명타 한 번을 그리고, 나무마다 정렬 순서가 달라 맞은 나무 바로 앞에 그려지도록 인스턴스마다 정렬 순서를 따로 받는다.
    ///
    /// 장면(약 0.45초)
    /// - 베기 줄기: 맞은 자리를 비스듬히 가르는 흰 줄이 순식간에 뻗었다가 꼬리부터 금 -> 주황으로 식으며 사라진다. 처음 잠깐은 아래에 진홍 테두리가 붙는다.
    /// - 별 섬광: 중심에서 큰 4갈래 흰 별 + 금빛 대각 광선이 터졌다가 계단식으로 줄어든다.
    /// - 점선 충격파: 등각 타원 점선 고리가 흰 -> 금 -> 진홍으로 식으며 퍼진다.
    /// - 파편: 흰·금·진홍 불꽃 줄기가 사방으로 튀고 금가루 몇 알이 반짝이며 떨어진다.
    ///
    /// 반투명 페이드 없이 계단식 크기 변화·점멸로만 사라진다. 고정 크기 배열만 써서 재생 중 힙 할당이 없다. 그림일 뿐이라 판정과 무관하다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_CriticalImpact : MonoBehaviour
    {
        // //공용 상수

        private const float PixelsPerUnit = 32f;
        private const float TwoPi = Mathf.PI * 2f;

        private const string ShaderName = "Custom/VFX/BrandWrapGlow";

        // 정점 알파 0~1 -> 발광 배율 GlowMin~GlowMax. 흰 섬광·베기 줄기 머리만 블룸 임계를 넘긴다.
        private const float GlowMin = 1f;
        private const float GlowMax = 2.6f;

        // 팔레트(화면에 보일 sRGB). 리니어 색 공간이면 EnsureShared에서 한 번 변환한다. 금·주황은 VFX_TownMoney와 같은 색.
        private static readonly Color32 SrgbCream = new Color32(255, 241, 176, 255);
        private static readonly Color32 SrgbGold = new Color32(255, 204, 72, 255);
        private static readonly Color32 SrgbOrange = new Color32(238, 136, 38, 255);
        private static readonly Color32 SrgbCrimson = new Color32(232, 64, 48, 255);

        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static Color32 ColorCream;
        private static Color32 ColorGold;
        private static Color32 ColorOrange;
        private static Color32 ColorCrimson;
        private static bool bPaletteReady;

        // //베기 줄기

        private const int SlashLength = 26;          // px
        private const float SlashGrowEnd = 0.035f;   // 이 시각까지 머리가 뻗는다
        private const float SlashEnd = 0.14f;        // 이 시각까지 꼬리가 머리를 따라잡는다
        private const float SlashEdgeEnd = 0.08f;    // 진홍 테두리가 붙어 있는 시간
        private const float SlashSlopeY = 0.55f;     // 비스듬한 정도(가로 1에 세로 몇)

        // //별 섬광

        private const float StarStage0 = 0.035f;
        private const float StarStage1 = 0.07f;
        private const float StarEnd = 0.12f;
        private const int StarArmBig = 13;
        private const int StarArmMid = 8;
        private const int StarArmSmall = 4;
        private const int StarDiagonal = 6;

        // //점선 충격파

        private const float RingDelay = 0.02f;
        private const float RingStepTime = 0.03f;
        private const int RingStepCount = 8;
        private const int RingDotCount = 18;
        private const float RingSquashY = 0.5f;
        private const float RingStartRadius = 8f;
        private const float RingEndRadius = 26f;

        // //파편

        private const int DustCapacity = 24;
        private const int KindSpark = 0;
        private const int KindStreak = 1;
        private const int StreakCount = 10;
        private const int SparkCount = 5;
        private const float DustGravity = 260f;     // px/s^2

        // 전체 길이. 이 시간이 지나도 남은 파편이 있으면 다 사라질 때까지 이어간다.
        private const float MinDuration = 0.3f;

        // //공유 자원

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(512);
        private static readonly PixelPainter painter = new PixelPainter();
        private static Material sharedMaterial;
        private static bool bShaderMissingWarned;

        // //인스턴스 상태

        private struct Dust
        {
            public bool bActive;
            public float x;           // 월드 px
            public float y;
            public float vx;          // px/s
            public float vy;
            public float drag;
            public float age;
            public float life;
            public int kind;
            public int seed;
            public Color32 color;
        }

        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ulong meshHash;
        private int meshVertexCount = -1;

        private readonly Dust[] dusts = new Dust[DustCapacity];

        private bool bPlaying;
        private bool bFresh;
        private float age;
        private int centerX;
        private int centerY;
        private int slashDirX;        // 베기 줄기 방향(가로 부호). 위로 갈수록 오른쪽(1) 또는 왼쪽(-1)
        private uint randomState = 1u;

        public bool IsPlaying => bPlaying;

        // //퍼블릭

        /// <summary>
        /// _parent 아래에 이펙트를 하나 만든다. 셰이더를 찾지 못하면 null(이펙트 없이 진행).
        /// _parent에 SortingGroup이 있으면 그 그룹 정렬에 묶이므로, 그룹이 없는 부모를 넘겨야 한다.
        /// </summary>
        public static VFX_CriticalImpact Create(Transform _parent)
        {
            if (false == EnsureShared()) return null;

            GameObject go = new GameObject("CriticalImpactVFX");
            go.transform.SetParent(_parent, false);
            VFX_CriticalImpact vfx = go.AddComponent<VFX_CriticalImpact>();
            vfx.Initialize();
            return vfx;
        }

        /// <summary>치명타 한 번을 _worldPos(맞은 자리)에서 재생한다. 정렬은 맞은 나무 바로 앞(_sortingOrder)에 둔다.</summary>
        public void Play(Vector3 _worldPos, string _sortingLayerName, int _sortingOrder)
        {
            meshRenderer.sortingLayerName = _sortingLayerName;
            meshRenderer.sortingOrder = _sortingOrder;

            centerX = ToPixel(_worldPos.x);
            centerY = ToPixel(_worldPos.y);
            slashDirX = Rand01() < 0.5f ? 1 : -1;
            age = 0f;
            bFresh = true;
            bPlaying = true;
            meshHash = 0UL;
            meshVertexCount = -1;

            for (int i = 0; i < DustCapacity; i++) dusts[i].bActive = false;
            int cursor = 0;

            // 불꽃 줄기: 사방으로 고르게, 베기 줄기 방향으로 조금 더 빠르게 튄다.
            for (int i = 0; i < StreakCount && cursor < DustCapacity; i++)
            {
                float angle = TwoPi * (i + Rand01() * 0.7f) / StreakCount;
                float along = Mathf.Abs(Mathf.Cos(angle) * slashDirX * 0.88f + Mathf.Sin(angle) * 0.48f);
                float speed = Mathf.Lerp(140f, 240f, Rand01()) * Mathf.Lerp(0.85f, 1.2f, along);
                float roll = Rand01();
                Color32 color = roll < 0.3f ? ColorWhite : (roll < 0.7f ? ColorGold : ColorCrimson);
                AddDust(ref cursor, centerX, centerY, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed * 0.8f + 30f,
                    5f, Mathf.Lerp(0.26f, 0.4f, Rand01()), KindStreak, color);
            }

            // 금가루: 위쪽으로 느리게 튀었다가 반짝이며 떨어진다.
            for (int i = 0; i < SparkCount && cursor < DustCapacity; i++)
            {
                float angle = Mathf.Lerp(0.5f, Mathf.PI - 0.5f, (i + Rand01()) / SparkCount);
                float speed = Mathf.Lerp(60f, 110f, Rand01());
                Color32 color = Rand01() < 0.5f ? ColorGold : ColorCream;
                AddDust(ref cursor, centerX + (Rand01() - 0.5f) * 6f, centerY, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed,
                    2.5f, Mathf.Lerp(0.32f, 0.45f, Rand01()), KindSpark, color);
            }

            meshRenderer.enabled = false;
        }

        /// <summary>재생 중인 것을 바로 끝낸다(던전 이탈 등).</summary>
        public void StopImmediate()
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
                    Debug.LogWarning("[VFX_CriticalImpact] 셰이더 " + ShaderName + "를 찾지 못해 치명타 임팩트를 건너뜁니다.");
                }
                return false;
            }

            sharedMaterial = new Material(shader) { name = "M_CriticalImpact (Runtime)" };
            sharedMaterial.SetFloat("_GlowMin", GlowMin);
            sharedMaterial.SetFloat("_GlowMax", GlowMax);
            return true;
        }

        private void Initialize()
        {
            mesh = new Mesh { name = "VFX_CriticalImpact" };
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
        }

        private static void BuildPalette()
        {
            bool bLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            ColorCream = ToDisplay(SrgbCream, bLinear);
            ColorGold = ToDisplay(SrgbGold, bLinear);
            ColorOrange = ToDisplay(SrgbOrange, bLinear);
            ColorCrimson = ToDisplay(SrgbCrimson, bLinear);
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

        private void AddDust(ref int _cursor, float _x, float _y, float _vx, float _vy, float _drag, float _life, int _kind, Color32 _color)
        {
            dusts[_cursor] = new Dust
            {
                bActive = true,
                x = _x,
                y = _y,
                vx = _vx,
                vy = _vy,
                drag = _drag,
                age = 0f,
                life = _life,
                kind = _kind,
                seed = (int)(Rand01() * 997f),
                color = _color,
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
            age += dt;

            bool bAnyDust = false;
            for (int i = 0; i < DustCapacity; i++)
            {
                if (false == dusts[i].bActive) continue;

                if (dt > 0f)
                {
                    dusts[i].age += dt;
                    float damping = Mathf.Exp(-dusts[i].drag * dt);
                    dusts[i].vx *= damping;
                    dusts[i].vy = dusts[i].vy * damping - DustGravity * dt;
                    dusts[i].x += dusts[i].vx * dt;
                    dusts[i].y += dusts[i].vy * dt;
                }

                if (dusts[i].age >= dusts[i].life)
                {
                    dusts[i].bActive = false;
                    continue;
                }

                bAnyDust = true;
            }

            if (age >= MinDuration && false == bAnyDust)
            {
                StopImmediate();
                return;
            }

            // 화면 밖이면 시간만 흐르게 두고 그리지 않는다. 범위는 파편이 닿을 수 있는 정도로 넉넉히 잡는다.
            Vector3 center = new Vector3(centerX / PixelsPerUnit, centerY / PixelsPerUnit, 0f);
            Vector3 size = new Vector3(4f + PixelVfxCulling.MarginUnits * 2f, 4f + PixelVfxCulling.MarginUnits * 2f, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(center, size))
            {
                meshRenderer.enabled = false;
                return;
            }

            Draw();
        }

        // //그리기

        private void Draw()
        {
            painter.Begin(buffer, transform.worldToLocalMatrix, 0f);

            DrawRing();
            DrawDusts();
            DrawSlash();
            DrawStar();

            painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
        }

        private static Color32 WithGlow(Color32 _color, float _glow)
        {
            return new Color32(_color.r, _color.g, _color.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255f));
        }

        // 베기 줄기: 아래(-) 끝에서 위(+) 끝으로 머리가 뻗고, 그 뒤 꼬리가 머리를 따라잡으며 사라진다.
        // 두 줄(2px) 두께이고, 처음 잠깐은 그 아래로 진홍 테두리가 한 줄 더 붙는다.
        private void DrawSlash()
        {
            if (age >= SlashEnd) return;

            int halfX = SlashLength / 2;
            int halfY = Mathf.RoundToInt(halfX * SlashSlopeY);
            int x0 = centerX - halfX * slashDirX;
            int y0 = centerY - halfY;
            int x1 = centerX + halfX * slashDirX;
            int y1 = centerY + halfY;

            float head = age < SlashGrowEnd ? Mathf.Clamp01((age + 0.012f) / SlashGrowEnd) : 1f;
            float tail = age < SlashGrowEnd ? 0f : Mathf.Clamp01((age - SlashGrowEnd) / (SlashEnd - SlashGrowEnd));
            if (head <= tail) return;

            int hx = x0 + Mathf.RoundToInt((x1 - x0) * head);
            int hy = y0 + Mathf.RoundToInt((y1 - y0) * head);
            int tx = x0 + Mathf.RoundToInt((x1 - x0) * tail);
            int ty = y0 + Mathf.RoundToInt((y1 - y0) * tail);

            float u = age / SlashEnd;
            Color32 core = u < 0.45f ? WithGlow(ColorWhite, 1f) : (u < 0.75f ? WithGlow(ColorGold, 0.6f) : WithGlow(ColorOrange, 0.2f));

            if (age < SlashEdgeEnd)
            {
                painter.Line(tx, ty - 2, hx, hy - 2, 0, 0, 0, WithGlow(ColorCrimson, 0.35f));
            }

            painter.Line(tx, ty - 1, hx, hy - 1, 0, 0, 0, core);
            painter.Line(tx, ty, hx, hy, 0, 0, 0, core);

            // 뻗는 동안 머리에 흰 점이 하나 더 맺힌다.
            if (age < SlashGrowEnd + 0.02f)
            {
                painter.Rect(hx - 1, hy - 1, hx + 2, hy + 2, WithGlow(ColorWhite, 1f));
            }
        }

        // 계단식으로 줄어드는 4갈래 별. 첫 단계는 굵은 십자에 금빛 대각 광선이 함께 터진다.
        private void DrawStar()
        {
            if (age >= StarEnd) return;

            int x = centerX;
            int y = centerY;
            Color32 white = WithGlow(ColorWhite, 1f);

            if (age < StarStage0)
            {
                // 가운데 굵고 끝으로 가며 가늘어지는 십자
                painter.Rect(x - StarArmBig, y, x + StarArmBig + 1, y + 1, white);
                painter.Rect(x, y - StarArmBig, x + 1, y + StarArmBig + 1, white);
                painter.Rect(x - StarArmBig / 2, y - 1, x + StarArmBig / 2 + 1, y + 2, white);
                painter.Rect(x - 1, y - StarArmBig / 2, x + 2, y + StarArmBig / 2 + 1, white);
                painter.Rect(x - 2, y - 2, x + 3, y + 3, white);

                Color32 gold = WithGlow(ColorGold, 0.9f);
                for (int d = 3; d <= StarDiagonal; d++)
                {
                    painter.Dot(x + d, y + d, gold);
                    painter.Dot(x - d, y + d, gold);
                    painter.Dot(x + d, y - d, gold);
                    painter.Dot(x - d, y - d, gold);
                }
            }
            else if (age < StarStage1)
            {
                painter.HLine(x - StarArmMid, x + StarArmMid, y, white);
                painter.Rect(x, y - StarArmMid, x + 1, y + StarArmMid + 1, white);
                painter.Rect(x - 1, y - 1, x + 2, y + 2, white);

                Color32 gold = WithGlow(ColorGold, 0.5f);
                for (int d = 2; d <= StarDiagonal / 2 + 1; d++)
                {
                    painter.Dot(x + d, y + d, gold);
                    painter.Dot(x - d, y + d, gold);
                    painter.Dot(x + d, y - d, gold);
                    painter.Dot(x - d, y - d, gold);
                }
            }
            else
            {
                Color32 dim = WithGlow(ColorCream, 0.6f);
                painter.HLine(x - StarArmSmall, x + StarArmSmall, y, dim);
                painter.Rect(x, y - StarArmSmall, x + 1, y + StarArmSmall + 1, dim);
            }
        }

        // 등각 타원 점선 충격파. 계단식으로 커지고, 바깥으로 갈수록 식으며 점이 하나씩 빠진다.
        private void DrawRing()
        {
            float ringAge = age - RingDelay;
            if (ringAge < 0f) return;

            int step = (int)(ringAge / RingStepTime);
            if (step >= RingStepCount) return;

            float k = 1f - step / (float)(RingStepCount - 1);
            float grow = 1f - k * k;
            float radius = Mathf.Lerp(RingStartRadius, RingEndRadius, grow);
            float survive = 1f - Mathf.Clamp01((ringAge - 0.12f) / 0.12f);

            Color32 baseColor = step < 2 ? ColorWhite : (step < 5 ? ColorGold : ColorCrimson);
            Color32 color = WithGlow(baseColor, step < 2 ? 0.8f : 0.25f);
            float phase = 0 == (step & 1) ? 0f : TwoPi / RingDotCount * 0.5f;
            int size = step < 3 ? 2 : 1;

            for (int i = 0; i < RingDotCount; i++)
            {
                float order = ((i * 7) % RingDotCount) / (float)RingDotCount;
                if (survive <= order) continue;

                float angle = phase + TwoPi * i / RingDotCount;
                int px = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * radius);
                int py = centerY + Mathf.RoundToInt(Mathf.Sin(angle) * radius * RingSquashY);
                painter.Rect(px, py, px + size, py + size, color);
            }
        }

        private void DrawDusts()
        {
            for (int i = 0; i < DustCapacity; i++)
            {
                if (false == dusts[i].bActive) continue;

                float u = dusts[i].age / dusts[i].life;
                int px = Mathf.RoundToInt(dusts[i].x);
                int py = Mathf.RoundToInt(dusts[i].y);

                if (KindStreak == dusts[i].kind)
                {
                    DrawStreak(px, py, dusts[i].vx, dusts[i].vy, dusts[i].age, u, dusts[i].color);
                }
                else
                {
                    DrawSpark(px, py, dusts[i].age, u, dusts[i].seed, dusts[i].color);
                }
            }
        }

        // 불꽃 줄기: 머리는 밝고(처음엔 흰빛), 날아온 방향으로 금 -> 주황 꼬리를 끈다. 빠를수록 꼬리가 길고,
        // 느려지면 점 하나로 줄었다가 끝물에 점멸하며 꺼진다(VFX_TownMoney와 같은 모양).
        private static void DrawStreak(int _x, int _y, float _vx, float _vy, float _age, float _u, Color32 _color)
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

        // 금가루: 3단계로 반짝이고, `+`/`x`가 계단식으로 줄며 번갈아 바뀐다. 끝물에는 점멸하며 사라진다.
        private static void DrawSpark(int _x, int _y, float _age, float _u, int _seed, Color32 _color)
        {
            if (_u > 0.8f && 0 != ((int)(_age * 30f) & 1)) return;

            int twinkle = ((int)(_age * 14f) + _seed) % 3;
            float glow = (0 == twinkle ? 0.35f : (1 == twinkle ? 0.6f : 1f)) * (1f - _u * 0.5f);
            Color32 baseColor = _u > 0.72f ? ColorOrange : _color;
            Color32 color = WithGlow(baseColor, glow);

            int arm = _u < 0.4f ? 2 : (_u < 0.7f ? 1 : 0);
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

        // //수명

        private void OnDisable()
        {
            StopImmediate();
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
