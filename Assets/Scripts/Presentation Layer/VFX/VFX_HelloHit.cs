using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// "첫 인사" 특성 연출(아직 한 번도 맞지 않은 나무에 들어간 도끼 첫 타 보너스). 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다
    /// (VFX_WeakPointHit와 같은 방식, 정점 알파 = HDR 발광 세기인 BrandWrapGlow 셰이더). 프리팹 없이 InDungeonVFXManager가 런타임에 만든다.
    /// 치명타(흰·금·진홍), 약점 타격(분홍·자홍), 가속·별(얼음·파랑)과 갈리도록 민트·청록 계열만 쓰고, 네 갈래 반짝임이 공통 표식이다.
    /// 나무마다 첫 타에 나오므로 자주 보인다. 그래서 짧고(약 0.4초) 가볍게, 떨어지는 파편 없이 위로 떠오르는 움직임만 쓴다.
    ///
    /// - 똑똑(0~0.10초): 맞은 자리에 네 갈래 반짝임이 크게 터졌다가 두 단계로 줄어든다(흰 한가운데, 민트 팔, 청록 끝, 짧은 대각 팔).
    /// - 화답(0.09~0.25초): 수관 윗부분에서 작은 반짝임이 피었다 진다. 나무가 인사를 받는 느낌. 이 타격으로 쓰러진 나무에는 없다.
    /// - 반가움: 맞은 자리에서 작은 반짝이 다섯 개가 공격자 반대쪽으로 비스듬히 떠올라 멈칫하다 깜빡이며 꺼진다.
    ///
    /// 반투명 페이드 없이 계단식 크기 변화·점멸로만 사라진다. 고정 크기 배열만 써서 재생 중 힙 할당이 없다. 그림일 뿐이라 판정과 무관하다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_HelloHit : MonoBehaviour
    {
        // //공용 상수

        private const float PixelsPerUnit = 32f;

        private const string ShaderName = "Custom/VFX/BrandWrapGlow";

        // 정점 알파 0~1 -> 발광 배율 GlowMin~GlowMax(VFX_WeakPointHit와 같은 범위). 흰 속만 블룸 임계를 넘긴다.
        private const float GlowMin = 1f;
        private const float GlowMax = 2.6f;

        // 팔레트(화면에 보일 sRGB). 리니어 색 공간이면 EnsureShared에서 한 번 변환한다.
        private static readonly Color32 SrgbMintLight = new Color32(200, 255, 226, 255);
        private static readonly Color32 SrgbMint = new Color32(96, 240, 166, 255);
        private static readonly Color32 SrgbTeal = new Color32(32, 182, 134, 255);
        private static readonly Color32 SrgbDeepTeal = new Color32(16, 104, 88, 255);

        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static Color32 ColorMintLight;
        private static Color32 ColorMint;
        private static Color32 ColorTeal;
        private static Color32 ColorDeepTeal;
        private static bool bPaletteReady;

        // 그릴 때 쓰는 "색 + 발광" 조합. 매 프레임 WithGlow를 다시 계산하지 않도록 팔레트를 만들 때 한 번 채운다(같은 함수라 값이 같다).
        // 이름 끝 숫자는 발광 세기 x100.
        private static Color32 White100;
        private static Color32 MintLight50;
        private static Color32 MintLight45;
        private static Color32 MintLight40;
        private static Color32 Mint35;
        private static Color32 Mint30;
        private static Color32 Teal30;
        private static Color32 Teal25;
        private static Color32 DeepTeal20;

        // //똑똑(맞은 자리)

        private const int KnockLiftY = 2;            // 밑동(톱밥 자리)보다 이만큼 위가 반짝임 중심
        private const float KnockStep0 = 0.035f;     // 크게 터진 반짝임
        private const float KnockStep1 = 0.07f;      // 한 번 줄어든 민트 반짝임
        private const float KnockEnd = 0.10f;        // 작은 청록 반짝임까지

        // //화답(꼭대기)

        // 꼭대기 기준점(topRoot)은 수관 가운데쯤(나무 원점 위 24px)이고, 나무 종류별 수관 끝은 원점 위 40~49px다.
        // 나무 체력 바가 원점 위 1.5칸(48px)에 떠서 수관 끝을 덮으므로, 화답은 그 아래 수관 윗부분 안쪽(어두운 잎 위라 잘 보인다)에 둔다.
        // 떠오르는 반짝이와 겹치지 않게 공격자 쪽으로 조금 비켜 둔다(반짝이는 공격자 반대쪽으로 간다).
        private const int ReplyLiftY = 9;
        private const int ReplySideX = 4;
        private const float ReplyStart = 0.09f;      // 똑똑이 잦아들 무렵 피어난다
        private const float ReplyStep0 = 0.12f;      // 작게 맺힘
        private const float ReplyStep1 = 0.16f;      // 활짝
        private const float ReplyStep2 = 0.20f;      // 다시 작게
        private const float ReplyBlinkOn = 0.22f;    // 마지막 점 하나가 한 번 비친다
        private const float ReplyEnd = 0.25f;

        // //반가움(떠오르는 반짝이)

        private const int MoteCount = 5;
        private const float MoteDelay = 0.02f;       // 똑똑이 터진 직후에 첫 반짝이가 떠오른다
        private const float MoteStagger = 0.018f;    // 다음 반짝이는 이만큼씩 늦게 떠오른다(한 점에서 뭉쳐 덩어리로 보이지 않게)
        private const float MoteDrag = 4.5f;         // 1/s, 빨리 멈칫해 그 자리에 잠깐 머문다

        // 전체 최소 길이. 이 시간이 지나도 남은 반짝이가 있으면 다 사라질 때까지 이어간다.
        private const float MinDuration = 0.25f;

        // //공유 자원

        private static readonly PixelQuadBuffer buffer = new PixelQuadBuffer(128);
        private static readonly PixelPainter painter = new PixelPainter();
        private static Material sharedMaterial;
        private static bool bShaderMissingWarned;

        // //인스턴스 상태

        private struct Mote
        {
            public bool bActive;
            public float x;           // 월드 px
            public float y;
            public float vx;          // px/s
            public float vy;
            public float age;
            public float life;
            public float delay;       // 이 나이가 될 때까지는 움직이지도 그리지도 않는다
            public bool bBright;      // 옅은 민트로 시작하는 반짝이(나머지는 민트)
        }

        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ulong meshHash;
        private int meshVertexCount = -1;

        private readonly Mote[] motes = new Mote[MoteCount];

        private bool bPlaying;
        private bool bFresh;
        private bool bReply;
        private float age;
        private int knockX;
        private int knockY;
        private int replyX;
        private int replyY;
        private int awayDirX;         // 공격자 반대편. 1 = 오른쪽, -1 = 왼쪽
        private uint randomState = 1u;

        // 화면 안/밖 판단 경계(재생 중 움직이지 않으므로 재생할 때 한 번 정한다).
        private Vector3 cullCenter;
        private Vector3 cullSize;

        // 반짝이 감속(Exp) 값 기억. 감속 세기는 하나뿐이고 프레임마다 같은 시간 간격이라, 같은 간격이면 다시 계산하지 않는다(값은 같다).
        private float dampStep = -1f;
        private float dampValue;

        public bool IsPlaying => bPlaying;

        // //퍼블릭

        /// <summary>
        /// _parent 아래에 이펙트를 하나 만든다. 셰이더를 찾지 못하면 null(이펙트 없이 진행).
        /// _parent에 SortingGroup이 있으면 그 그룹 정렬에 묶이므로, 그룹이 없는 부모를 넘겨야 한다.
        /// </summary>
        public static VFX_HelloHit Create(Transform _parent)
        {
            if (false == EnsureShared()) return null;

            GameObject go = new GameObject("HelloHitVFX");
            go.transform.SetParent(_parent, false);
            VFX_HelloHit vfx = go.AddComponent<VFX_HelloHit>();
            vfx.Initialize();
            return vfx;
        }

        /// <summary>
        /// _impactPos(밑동, 기본 피격 톱밥이 터지는 자리)에서 재생한다. _bReply면 _crownPos(나무 꼭대기 기준점)에 화답 반짝임을 더한다.
        /// _awayDirX는 공격자 반대편(1 또는 -1)으로, 반짝이가 그쪽으로 비스듬히 떠오른다.
        /// </summary>
        public void Play(Vector3 _impactPos, Vector3 _crownPos, bool _bReply, int _awayDirX, string _sortingLayerName, int _sortingOrder)
        {
            meshRenderer.sortingLayerName = _sortingLayerName;
            meshRenderer.sortingOrder = _sortingOrder;

            knockX = ToPixel(_impactPos.x);
            knockY = ToPixel(_impactPos.y) + KnockLiftY;
            awayDirX = _awayDirX < 0 ? -1 : 1;
            replyX = ToPixel(_crownPos.x) - awayDirX * ReplySideX;
            replyY = ToPixel(_crownPos.y) + ReplyLiftY;
            bReply = _bReply;
            age = 0f;
            bFresh = true;
            bPlaying = true;
            meshHash = 0UL;
            meshVertexCount = -1;

            // 범위는 밑동부터 수관 화답까지 감싼다(반짝이는 여유 안에서 움직인다).
            float lowY = Mathf.Min(knockY, replyY) / PixelsPerUnit;
            float highY = Mathf.Max(knockY, replyY) / PixelsPerUnit;
            cullCenter = new Vector3(knockX / PixelsPerUnit, (lowY + highY) * 0.5f, 0f);
            cullSize = new Vector3(3f + PixelVfxCulling.MarginUnits * 2f, (highY - lowY) + 3f + PixelVfxCulling.MarginUnits * 2f, PixelVfxCulling.BoundsDepth);

            SpawnMotes();

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
            for (int i = 0; i < MoteCount; i++) motes[i].bActive = false;
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
                    Debug.LogWarning("[VFX_HelloHit] 셰이더 " + ShaderName + "를 찾지 못해 첫 인사 연출을 건너뜁니다.");
                }
                return false;
            }

            sharedMaterial = new Material(shader) { name = "M_HelloHit (Runtime)" };
            sharedMaterial.SetFloat("_GlowMin", GlowMin);
            sharedMaterial.SetFloat("_GlowMax", GlowMax);
            return true;
        }

        private void Initialize()
        {
            mesh = new Mesh { name = "VFX_HelloHit" };
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
            ColorMintLight = ToDisplay(SrgbMintLight, bLinear);
            ColorMint = ToDisplay(SrgbMint, bLinear);
            ColorTeal = ToDisplay(SrgbTeal, bLinear);
            ColorDeepTeal = ToDisplay(SrgbDeepTeal, bLinear);

            White100 = WithGlow(ColorWhite, 1f);
            MintLight50 = WithGlow(ColorMintLight, 0.5f);
            MintLight45 = WithGlow(ColorMintLight, 0.45f);
            MintLight40 = WithGlow(ColorMintLight, 0.4f);
            Mint35 = WithGlow(ColorMint, 0.35f);
            Mint30 = WithGlow(ColorMint, 0.3f);
            Teal30 = WithGlow(ColorTeal, 0.3f);
            Teal25 = WithGlow(ColorTeal, 0.25f);
            DeepTeal20 = WithGlow(ColorDeepTeal, 0.2f);

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

        // 떠오르는 순서별 부채꼴 칸. 이어 나오는 반짝이가 서로 다른 쪽으로 가서 막 떠오를 때 붙지 않는다.
        private static readonly int[] MoteSlotOrder = { 2, 0, 4, 1, 3 };

        // 반짝이 다섯 개: 위쪽 부채꼴(공격자 반대쪽으로 기운다)에 고르게 흩어 하나씩 떠오른다. 가운데일수록 높이, 바깥일수록 옆으로 간다.
        private void SpawnMotes()
        {
            for (int i = 0; i < MoteCount; i++)
            {
                float slot = (MoteSlotOrder[i] + 0.2f + Rand01() * 0.6f) / MoteCount;   // 0..1
                float spread = Mathf.Lerp(-0.9f, 0.9f, slot);            // 위쪽 기준 좌우 각도(rad)
                // 꼭대기 화답과 겹치지 않게 공격자 반대쪽으로 기울이고, 수관 가운데(약 20px 위)쯤에서 멈추게 한다.
                float angle = Mathf.PI * 0.5f - (spread + 0.45f) * awayDirX;
                float speed = Mathf.Lerp(80f, 115f, Rand01()) * (1f - Mathf.Abs(spread) * 0.3f);
                motes[i] = new Mote
                {
                    bActive = true,
                    x = knockX + awayDirX,
                    y = knockY + 1,
                    vx = Mathf.Cos(angle) * speed,
                    vy = Mathf.Sin(angle) * speed,
                    age = 0f,
                    life = Mathf.Lerp(0.26f, 0.36f, Rand01()),
                    delay = MoteDelay + MoteStagger * i,
                    bBright = Rand01() < 0.4f,
                };
            }
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

            // 화면 밖이면 시간만 흐르게 두고 그리지 않는다.
            if (false == PixelVfxCulling.IsVisible(cullCenter, cullSize))
            {
                if (meshRenderer.enabled) meshRenderer.enabled = false;
                return;
            }

            Draw();
        }

        // 시간을 _dt만큼 흘리고 반짝이를 움직인다. 다 끝났으면 false.
        private bool Step(float _dt)
        {
            float dt = _dt;
            age += dt;

            bool bAnyMote = false;
            for (int i = 0; i < MoteCount; i++)
            {
                if (false == motes[i].bActive) continue;
                bAnyMote = true;
                if (age < motes[i].delay || dt <= 0f) continue;

                // 대기가 끝난 프레임에는 넘친 시간만큼만 흘린다.
                float step = Mathf.Min(dt, age - motes[i].delay);
                motes[i].age += step;
                if (step != dampStep)
                {
                    dampStep = step;
                    dampValue = Mathf.Exp(-MoteDrag * step);
                }
                float damping = dampValue;
                motes[i].vx *= damping;
                motes[i].vy *= damping;
                motes[i].x += motes[i].vx * step;
                motes[i].y += motes[i].vy * step;

                if (motes[i].age >= motes[i].life) motes[i].bActive = false;
            }

            return age < MinDuration || bAnyMote;
        }

        private void Draw()
        {
            painter.Begin(buffer, transform.worldToLocalMatrix, 0f);
            DrawMotes();
            DrawKnock();
            if (bReply) DrawReply();
            painter.Commit(mesh, meshRenderer, ref meshHash, ref meshVertexCount);
        }

        // //그리기

        private static Color32 WithGlow(Color32 _color, float _glow)
        {
            return new Color32(_color.r, _color.g, _color.b, (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255f));
        }

        // 네 갈래 반짝임: 가로 팔 _armX, 세로 팔 _armY(1px 두께). 팔 끝 _tip칸은 끝 색, 그 안쪽은 팔 색, 가운데 _core칸까지는 속 색,
        // 대각으로 _diag칸 짧은 팔, 한가운데 한 칸은 _centerColor. 바깥부터 그리고 안쪽을 덮어 그린다(뒤에 그린 쿼드가 위에 온다).
        private static void DrawSparkle(int _cx, int _cy, int _armX, int _armY, int _tip, int _core, int _diag,
            Color32 _tipColor, Color32 _armColor, Color32 _coreColor, Color32 _diagColor, Color32 _centerColor)
        {
            painter.HLine(_cx - _armX, _cx + _armX, _cy, _tipColor);
            painter.Rect(_cx, _cy - _armY, _cx + 1, _cy + _armY + 1, _tipColor);

            int innerX = _armX - _tip;
            int innerY = _armY - _tip;
            if (innerX > 0) painter.HLine(_cx - innerX, _cx + innerX, _cy, _armColor);
            if (innerY > 0) painter.Rect(_cx, _cy - innerY, _cx + 1, _cy + innerY + 1, _armColor);

            for (int k = 1; k <= _diag; k++)
            {
                painter.Dot(_cx - k, _cy - k, _diagColor);
                painter.Dot(_cx + k, _cy - k, _diagColor);
                painter.Dot(_cx - k, _cy + k, _diagColor);
                painter.Dot(_cx + k, _cy + k, _diagColor);
            }

            if (_core > 0)
            {
                painter.HLine(_cx - _core, _cx + _core, _cy, _coreColor);
                painter.Rect(_cx, _cy - _core, _cx + 1, _cy + _core + 1, _coreColor);
            }

            painter.Dot(_cx, _cy, _centerColor);
        }

        // 색이 있는 부분은 발광을 낮게 둔다. 높이면 HDR 배율에 민트가 하얗게 바래 흰 섬광(나무 피격 번쩍임·사망 이펙트)에 묻힌다.
        // 한가운데 한 칸만 흰빛으로 세게 빛난다.

        // 똑똑: 크게 터진 반짝임(청록 끝 + 민트 팔 + 옅은 민트 속) -> 한 단계 작은 민트 반짝임 -> 작은 청록 반짝임.
        private void DrawKnock()
        {
            if (age >= KnockEnd) return;

            if (age < KnockStep0)
            {
                DrawSparkle(knockX, knockY, 7, 5, 3, 2, 2, Teal30, Mint30, MintLight45, Teal30, White100);
            }
            else if (age < KnockStep1)
            {
                DrawSparkle(knockX, knockY, 5, 4, 2, 1, 1, Teal25, Mint30, MintLight40, Teal25, White100);
            }
            else
            {
                DrawSparkle(knockX, knockY, 2, 2, 1, 0, 0, DeepTeal20, Teal25, Teal25, Teal25, Mint35);
            }
        }

        // 화답: 작게 맺혔다가 활짝 피고 다시 작아진 뒤, 점 하나가 한 번 비치고 꺼진다.
        private void DrawReply()
        {
            if (age < ReplyStart || age >= ReplyEnd) return;

            if (age < ReplyStep0)
            {
                DrawSparkle(replyX, replyY, 2, 2, 1, 0, 0, Mint30, MintLight40, MintLight40, Mint30, White100);
            }
            else if (age < ReplyStep1)
            {
                DrawSparkle(replyX, replyY, 5, 4, 2, 1, 1, Teal30, Mint30, MintLight45, Teal30, White100);
            }
            else if (age < ReplyStep2)
            {
                DrawSparkle(replyX, replyY, 2, 2, 1, 0, 0, Teal25, Mint30, Mint30, Teal25, MintLight50);
            }
            else if (age >= ReplyBlinkOn)
            {
                painter.Dot(replyX, replyY, Teal25);
            }
        }

        // 반짝이: 처음엔 흰 속 `+`(민트 또는 옅은 민트 팔)로 떠오르다, 멈칫할 즈음 민트 점으로 줄고, 끝물에 청록으로 깜빡이며 꺼진다.
        // `+`는 SpacedShape로 그려 서로 맞닿으면 점으로 줄인다(붙어서 한 덩어리로 보이지 않게).
        private void DrawMotes()
        {
            for (int i = 0; i < MoteCount; i++)
            {
                if (false == motes[i].bActive || age < motes[i].delay) continue;

                float u = motes[i].age / motes[i].life;
                int px = Mathf.RoundToInt(motes[i].x);
                int py = Mathf.RoundToInt(motes[i].y);

                if (u < 0.45f)
                {
                    Color32 color = motes[i].bBright ? MintLight40 : Mint35;
                    painter.SpacedShape(PixelPainter.ShapePlus, px, py, color);
                    painter.Dot(px, py, White100);
                }
                else if (u < 0.75f)
                {
                    painter.Dot(px, py, Mint35);
                }
                else
                {
                    if (0 != ((int)(motes[i].age * 30f) & 1)) continue;
                    painter.Dot(px, py, Teal25);
                }
            }
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
