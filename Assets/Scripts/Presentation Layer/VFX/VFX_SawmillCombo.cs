using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 제재소 가공 콤보(가공 성공마다 파이프라인 속도 증가, 최대 10중첩, 상자가 비면 초기화) 이펙트.
    /// 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다(VFX_TopgradeAssessment와 같은 방식, 정점 알파 = HDR 발광 세기인
    /// BrandWrapGlow 셰이더). 상점 하나에 하나, SawmillComboPresenter가 런타임에 만들어 부른다.
    ///
    /// 장면
    /// - 톱밥(EmitSawdust): 켜는 중인 커터에서 원목 색 톱밥이 더 튄다. 콤보가 쌓일수록 많아진다.
    /// - 과열(EmitHeat, PlayOverdrive): 최대 중첩에 닿는 순간 커터마다 섬광·주황 충격파·불티가 터지고, 그 뒤로 열기 점이 피어오른다.
    /// - 초기화(PlaySmoke): 상자가 비어 콤보가 풀리면 커터에서 회색 연기가 퍽 솟는다.
    ///
    /// 글자 없이 도형만 쓰므로 현지화 대상이 아니다. 반투명 없이 계단식 크기 변화·점멸로만 사라진다.
    /// 고정 크기 배열만 써서 재생 중 힙 할당이 없다. 그림일 뿐이라 가공·벨트·판정과는 무관하다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_SawmillCombo : MonoBehaviour
    {
        // //공용 상수

        private const float PixelsPerUnit = 32f;
        private const float TwoPi = Mathf.PI * 2f;

        private const string ShaderName = "Custom/VFX/BrandWrapGlow";
        private const string SortingLayerName = "FlyingItem";
        private const int SortingOrder = 0;

        private const float GlowMin = 1f;
        private const float GlowMax = 2.4f;

        // 팔레트(화면에 보일 sRGB). 리니어 색 공간이면 처음 한 번 리니어 값으로 바꿔 둔다(VFX_TopgradeAssessment와 같은 규칙).
        private static readonly Color32 SrgbWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 SrgbCream = new Color32(255, 241, 176, 255);
        private static readonly Color32 SrgbGold = new Color32(255, 204, 72, 255);
        private static readonly Color32 SrgbOrange = new Color32(238, 136, 38, 255);
        private static readonly Color32 SrgbRed = new Color32(226, 70, 44, 255);
        private static readonly Color32 SrgbEmber = new Color32(150, 46, 30, 255);
        private static readonly Color32 SrgbSmokeLight = new Color32(206, 202, 194, 255);
        private static readonly Color32 SrgbSmokeMid = new Color32(158, 152, 146, 255);
        private static readonly Color32 SrgbSmokeDark = new Color32(112, 106, 102, 255);

        private static Color32 ColorWhite;
        private static Color32 ColorCream;
        private static Color32 ColorGold;
        private static Color32 ColorOrange;
        private static Color32 ColorRed;
        private static Color32 ColorEmber;
        private static Color32 ColorSmokeLight;
        private static Color32 ColorSmokeMid;
        private static Color32 ColorSmokeDark;
        private static bool bPaletteReady;

        // //입자(톱밥·열기·불티·연기)

        private const int ParticleCapacity = 320;
        private const int KindChip = 0;
        private const int KindHeat = 1;
        private const int KindEmber = 2;
        private const int KindSmoke = 3;

        // //섬광·충격파(최대 중첩 순간)

        private const int FlashCapacity = 6;
        private const float FlashStage0 = 0.04f;
        private const float FlashStage1 = 0.08f;
        private const float FlashEnd = 0.12f;

        private const int RingCapacity = 6;
        private const float RingStepTime = 0.035f;
        private const int RingStepCount = 9;
        private const int RingDotCount = 18;
        private const float RingSquashY = 0.5f;
        private const float RingStartRadius = 6f;
        private const float RingMaxRadius = 26f;

        // //공유 자원

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(4096);
        private static readonly PixelPainter painter = new PixelPainter();
        private static Material sharedMaterial;
        private static bool bShaderMissingWarned;

        // //인스턴스 상태

        private struct Particle
        {
            public bool bActive;
            public bool bFresh;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float drag;
            public float gravity;
            public float age;     // 음수면 아직 나오기 전(지연)
            public float life;
            public int kind;
            public int seed;
            public Color32 color;
        }

        private struct Flash
        {
            public bool bActive;
            public bool bFresh;
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
        }

        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ulong meshHash;
        private int meshVertexCount = -1;

        private readonly Particle[] particles = new Particle[ParticleCapacity];
        private readonly Flash[] flashes = new Flash[FlashCapacity];
        private readonly Ring[] rings = new Ring[RingCapacity];
        private int particleCursor;
        private int flashCursor;
        private int ringCursor;

        private bool bAnyParticles;
        private uint randomState = 1u;

        // //퍼블릭

        /// <summary>_parent 아래에 이펙트를 하나 만든다. 셰이더를 찾지 못하면 null(이펙트 없이 진행).</summary>
        public static VFX_SawmillCombo Create(Transform _parent)
        {
            if (false == EnsureShared()) return null;

            GameObject go = new GameObject("SawmillComboVFX");
            go.transform.SetParent(_parent, false);
            VFX_SawmillCombo vfx = go.AddComponent<VFX_SawmillCombo>();
            vfx.Initialize();
            return vfx;
        }

        /// <summary>커터 톱밥 한 알. _pos는 톱날 자리(월드), _color는 원목 톱밥 색(sRGB 그대로 넘긴다).</summary>
        public void EmitSawdust(Vector3 _pos, Color _color)
        {
            // 위쪽 부채꼴로 튀었다가 떨어진다.
            float angle = Mathf.Lerp(0.35f, Mathf.PI - 0.35f, Rand01());
            float speed = Mathf.Lerp(40f, 95f, Rand01());
            Color32 color = ToDisplay((Color32)_color, QualitySettings.activeColorSpace == ColorSpace.Linear);
            color.a = 0;
            AddParticle(_pos.x * PixelsPerUnit + (Rand01() - 0.5f) * 6f, _pos.y * PixelsPerUnit,
                Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 1.5f, 260f, 0f, Mathf.Lerp(0.3f, 0.5f, Rand01()), KindChip, color);
        }

        /// <summary>달아오른 커터 위로 열기 점 하나. _pos는 커터 윗면 가운데(월드), _halfWidth는 퍼지는 폭(유닛).</summary>
        public void EmitHeat(Vector3 _pos, float _halfWidth)
        {
            float x = _pos.x * PixelsPerUnit + (Rand01() * 2f - 1f) * _halfWidth * PixelsPerUnit;
            float y = _pos.y * PixelsPerUnit + (Rand01() - 0.5f) * 4f;
            AddParticle(x, y, 0f, Mathf.Lerp(16f, 28f, Rand01()), 0f, 0f, 0f, Mathf.Lerp(0.55f, 0.85f, Rand01()), KindHeat, ColorGold);
        }

        /// <summary>최대 중첩에 닿은 순간 커터 하나. _pos는 커터 윗면 가운데(월드).</summary>
        public void PlayOverdrive(Vector3 _pos)
        {
            int x = ToPixel(_pos.x);
            int y = ToPixel(_pos.y);

            flashes[flashCursor] = new Flash { bActive = true, bFresh = true, x = x, y = y, age = 0f };
            flashCursor = (flashCursor + 1) % FlashCapacity;

            rings[ringCursor] = new Ring { bActive = true, bFresh = true, x = x, y = y - 6, age = 0f };
            ringCursor = (ringCursor + 1) % RingCapacity;

            const int emberCount = 12;
            for (int i = 0; i < emberCount; i++)
            {
                float angle = Mathf.Lerp(0.5f, Mathf.PI - 0.5f, (i + Rand01()) / emberCount);
                float speed = Mathf.Lerp(70f, 140f, Rand01());
                Color32 color = Rand01() < 0.4f ? ColorGold : (Rand01() < 0.6f ? ColorOrange : ColorCream);
                AddParticle(x + (Rand01() - 0.5f) * 8f, y, Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed, 2f, 220f,
                    Rand01() * 0.03f, Mathf.Lerp(0.35f, 0.55f, Rand01()), KindEmber, color);
            }

            bAnyParticles = true;
        }

        /// <summary>콤보가 풀린 순간 커터 하나에서 연기가 솟는다. _pos는 커터 윗면 가운데(월드), _halfWidth는 퍼지는 폭(유닛).</summary>
        public void PlaySmoke(Vector3 _pos, float _halfWidth)
        {
            const int puffCount = 7;
            for (int i = 0; i < puffCount; i++)
            {
                float u = (i + Rand01()) / puffCount;
                float x = _pos.x * PixelsPerUnit + Mathf.Lerp(-_halfWidth, _halfWidth, u) * PixelsPerUnit;
                float y = _pos.y * PixelsPerUnit + (Rand01() - 0.5f) * 4f;
                float vx = (u - 0.5f) * Mathf.Lerp(10f, 22f, Rand01());
                AddParticle(x, y, vx, Mathf.Lerp(14f, 26f, Rand01()), 1.2f, -4f, Rand01() * 0.08f,
                    Mathf.Lerp(0.6f, 0.85f, Rand01()), KindSmoke, ColorSmokeLight);
            }
        }

        /// <summary>그리던 것을 모두 지운다(맵을 떠날 때).</summary>
        public void ClearAll()
        {
            for (int i = 0; i < ParticleCapacity; i++) particles[i].bActive = false;
            for (int i = 0; i < FlashCapacity; i++) flashes[i].bActive = false;
            for (int i = 0; i < RingCapacity; i++) rings[i].bActive = false;
            bAnyParticles = false;
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
                    Debug.LogWarning("[VFX_SawmillCombo] 셰이더 " + ShaderName + "를 찾지 못해 가공 콤보 이펙트를 건너뜁니다.");
                }
                return false;
            }

            sharedMaterial = new Material(shader) { name = "M_SawmillCombo (Runtime)" };
            sharedMaterial.SetFloat("_GlowMin", GlowMin);
            sharedMaterial.SetFloat("_GlowMax", GlowMax);
            return true;
        }

        private void Initialize()
        {
            mesh = new Mesh { name = "VFX_SawmillCombo" };
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
            ColorWhite = Flat(SrgbWhite, bLinear);
            ColorCream = Flat(SrgbCream, bLinear);
            ColorGold = Flat(SrgbGold, bLinear);
            ColorOrange = Flat(SrgbOrange, bLinear);
            ColorRed = Flat(SrgbRed, bLinear);
            ColorEmber = Flat(SrgbEmber, bLinear);
            ColorSmokeLight = Flat(SrgbSmokeLight, bLinear);
            ColorSmokeMid = Flat(SrgbSmokeMid, bLinear);
            ColorSmokeDark = Flat(SrgbSmokeDark, bLinear);
            bPaletteReady = true;
        }

        // 발광 없는(정점 알파 0) 화면 색.
        private static Color32 Flat(Color32 _srgb, bool _bLinear)
        {
            Color32 c = ToDisplay(_srgb, _bLinear);
            c.a = 0;
            return c;
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

        private static Color32 WithGlow(Color32 _color, float _glow)
        {
            return new Color32(_color.r, _color.g, _color.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255f));
        }

        // //생성 도우미

        private void AddParticle(float _x, float _y, float _vx, float _vy, float _drag, float _gravity, float _delay, float _life, int _kind, Color32 _color)
        {
            particles[particleCursor] = new Particle
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
            particleCursor = (particleCursor + 1) % ParticleCapacity;
            bAnyParticles = true;
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
            float dt = Time.deltaTime;
            bool bActive = false;
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;

            if (bAnyParticles)
            {
                bool bAnyLeft = false;

                for (int i = 0; i < ParticleCapacity; i++)
                {
                    if (false == particles[i].bActive) continue;

                    if (particles[i].bFresh)
                    {
                        particles[i].bFresh = false;
                    }
                    else
                    {
                        float before = particles[i].age;
                        particles[i].age += dt;
                        float moveDt = before < 0f ? Mathf.Max(0f, particles[i].age) : dt;
                        if (moveDt > 0f)
                        {
                            float damping = Mathf.Exp(-particles[i].drag * moveDt);
                            particles[i].vx *= damping;
                            particles[i].vy = particles[i].vy * damping - particles[i].gravity * moveDt;
                            particles[i].x += particles[i].vx * moveDt;
                            particles[i].y += particles[i].vy * moveDt;
                        }
                    }

                    if (particles[i].age >= particles[i].life)
                    {
                        particles[i].bActive = false;
                        continue;
                    }

                    bAnyLeft = true;
                    Encapsulate(particles[i].x, particles[i].y, ref minX, ref minY, ref maxX, ref maxY);
                }

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

                    bAnyLeft = true;
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

                    bAnyLeft = true;
                    Encapsulate(rings[i].x - RingMaxRadius, rings[i].y, ref minX, ref minY, ref maxX, ref maxY);
                    Encapsulate(rings[i].x + RingMaxRadius, rings[i].y, ref minX, ref minY, ref maxX, ref maxY);
                }

                bAnyParticles = bAnyLeft;
                bActive |= bAnyLeft;
            }

            if (false == bActive)
            {
                meshRenderer.enabled = false;
                return;
            }

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

            DrawParticles();
            DrawRings();
            DrawFlashes();

            painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
        }

        private void DrawParticles()
        {
            for (int i = 0; i < ParticleCapacity; i++)
            {
                if (false == particles[i].bActive || particles[i].age < 0f) continue;

                float u = particles[i].age / particles[i].life;
                int px = Mathf.RoundToInt(particles[i].x);
                int py = Mathf.RoundToInt(particles[i].y);

                switch (particles[i].kind)
                {
                    case KindChip:
                        DrawChip(px, py, u, particles[i].seed, particles[i].color);
                        break;

                    case KindHeat:
                        DrawHeat(px, py, particles[i].age, u, particles[i].seed);
                        break;

                    case KindEmber:
                        DrawEmber(px, py, u, particles[i].color);
                        break;

                    case KindSmoke:
                        DrawSmoke(px, py, u, particles[i].seed);
                        break;
                }
            }
        }

        // 톱밥: 처음엔 2px 조각, 떨어지면서 1px, 끝물엔 점멸.
        private static void DrawChip(int _x, int _y, float _u, int _seed, Color32 _color)
        {
            if (_u > 0.8f && 0 == (((int)(_u * 30f) + _seed) & 1)) return;
            if (_u < 0.45f && 0 == (_seed & 1)) painter.Rect(_x, _y, _x + 2, _y + 1, _color);
            else painter.Dot(_x, _y, _color);
        }

        // 열기: 좌우로 일렁이며 오르고, 노랑 → 주황 → 빨강 → 검붉은 색으로 식는다.
        private static void DrawHeat(int _x, int _y, float _age, float _u, int _seed)
        {
            int wobble = Mathf.RoundToInt(Mathf.Sin(_age * 9f + _seed) * 1.5f);
            Color32 color = _u < 0.25f ? WithGlow(ColorGold, 0.5f)
                : (_u < 0.5f ? WithGlow(ColorOrange, 0.3f)
                : (_u < 0.78f ? ColorRed : ColorEmber));
            if (_u < 0.25f && 0 == (_seed % 3)) painter.Rect(_x + wobble, _y, _x + wobble + 1, _y + 2, color);
            else painter.Dot(_x + wobble, _y, color);
        }

        // 불티: 밝은 2px → 1px, 끝물에 주황으로 식으며 점멸.
        private static void DrawEmber(int _x, int _y, float _u, Color32 _color)
        {
            if (_u < 0.35f)
            {
                painter.Rect(_x, _y, _x + 2, _y + 2, WithGlow(_color, 0.8f));
            }
            else if (_u < 0.75f)
            {
                painter.Dot(_x, _y, WithGlow(_color, 0.5f));
            }
            else if (0 == ((int)(_u * 40f) & 1))
            {
                painter.Dot(_x, _y, ColorOrange);
            }
        }

        // 연기: 동그란 덩어리가 계단식으로 커지며 옅은 회색 → 짙은 회색, 끝에는 바둑판 무늬로 흩어진다.
        private static void DrawSmoke(int _x, int _y, float _u, int _seed)
        {
            int radius = _u < 0.15f ? 1 : (_u < 0.4f ? 2 : (_u < 0.7f ? 3 : 4));
            Color32 color = _u < 0.4f ? ColorSmokeLight : (_u < 0.7f ? ColorSmokeMid : ColorSmokeDark);
            bool bDither = _u >= 0.7f;
            int parity = _seed & 1;

            for (int dy = -radius; dy <= radius; dy++)
            {
                int half = Mathf.RoundToInt(Mathf.Sqrt(radius * radius - dy * dy + 0.5f));
                if (false == bDither)
                {
                    painter.HLine(_x - half, _x + half, _y + dy, color);
                    continue;
                }
                for (int dx = -half; dx <= half; dx++)
                {
                    if (((_x + dx + _y + dy + parity) & 1) == 0) continue;
                    painter.Dot(_x + dx, _y + dy, color);
                }
            }
        }

        // 계단식으로 줄어드는 하얀 십자 섬광(커터마다 한 번).
        private void DrawFlashes()
        {
            for (int i = 0; i < FlashCapacity; i++)
            {
                if (false == flashes[i].bActive) continue;

                int x = flashes[i].x;
                int y = flashes[i].y;
                float age = flashes[i].age;

                if (age < FlashStage0)
                {
                    painter.Rect(x - 12, y, x + 13, y + 1, ColorWhite);
                    painter.Rect(x, y - 7, x + 1, y + 8, ColorWhite);
                    painter.Rect(x - 1, y - 1, x + 2, y + 2, WithGlow(ColorWhite, 1f));
                }
                else if (age < FlashStage1)
                {
                    painter.HLine(x - 7, x + 7, y, WithGlow(ColorCream, 0.8f));
                    painter.Rect(x, y - 4, x + 1, y + 5, WithGlow(ColorCream, 0.8f));
                }
                else
                {
                    painter.HLine(x - 3, x + 3, y, WithGlow(ColorGold, 0.6f));
                    painter.Rect(x, y - 2, x + 1, y + 3, WithGlow(ColorGold, 0.6f));
                }
            }
        }

        // 주황 점선 타원 충격파. 계단식으로 커지고 바깥으로 갈수록 붉게 식으며 점이 빠진다.
        private void DrawRings()
        {
            for (int r = 0; r < RingCapacity; r++)
            {
                if (false == rings[r].bActive) continue;

                int step = (int)(rings[r].age / RingStepTime);
                if (step >= RingStepCount) continue;

                float t = step / (float)(RingStepCount - 1);
                float grow = 1f - (1f - t) * (1f - t);
                float radius = Mathf.Lerp(RingStartRadius, RingMaxRadius, grow);
                float survive = 1f - Mathf.Clamp01((rings[r].age - 0.12f) / 0.2f);

                Color32 color = step < 3 ? WithGlow(ColorGold, 0.7f) : (step < 6 ? WithGlow(ColorOrange, 0.35f) : ColorRed);
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
    }
}
