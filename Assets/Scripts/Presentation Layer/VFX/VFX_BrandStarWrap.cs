using System;
using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 발현 낙인이 찍힌 나무를 별들이 감싸고 도는 유지 이펙트. 스프라이트 시트로는 표현하기 어려운 "앞뒤를 오가는 궤도"를
    /// 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다(레이저와 같은 방식).
    ///
    /// - 캐노피 중심(topRoot)을 기준으로 타원 궤도를 별 N개가 돈다. 화면 아래쪽(앞)을 지나는 별은 나무 앞 메쉬에,
    ///   위쪽(뒤)을 지나는 별은 나무 뒤 메쉬에 그려서 나무를 감싸는 깊이감을 낸다(나무 소팅 오더에 상대적으로 매 프레임 맞춘다).
    /// - 별은 `+`와 `x`로 번갈아 반짝이고 흰색/하늘색/금색 3색만 쓴다. 뒤쪽 별은 작고 파랗게 그려 원근감을 준다.
    /// - 각 별 뒤에 1px 점 3개가 궤적을 따라와 회전 방향이 읽힌다.
    /// - 등장: 줄기 아래에서 나선으로 감싸 올라와 궤도에 안착한다. 퇴장: 궤도 안쪽으로 모이며 작아져 사라진다.
    ///
    /// 정적 버퍼와 Color32만 써서 재생 중 힙 할당이 없다. 나무가 카메라 컬링으로 꺼져 있는 동안은 그리지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_BrandStarWrap : MonoBehaviour
    {
        // 풀 반환 이벤트 - InDungeonVFXManager가 구독해서 풀에 되돌린다
        public event Action<VFX_BrandStarWrap> ReturnToPoolEvent;

        [Header("렌더링")]
        [SerializeField] private Material wrapMaterial;

        [Header("궤도 (월드 유닛, 32 PPU 기준 1유닛 = 32px)")]
        [SerializeField, Tooltip("별 개수")] private int starCount = 8;
        [SerializeField, Tooltip("타원 궤도 가로 반경")] private float orbitRadiusX = 0.85f;
        [SerializeField, Tooltip("타원 궤도 세로 반경(쿼터뷰라 가로의 절반 정도)")] private float orbitRadiusY = 0.4f;
        [SerializeField, Tooltip("캐노피 중심에서 궤도 중심까지의 세로 오프셋")] private float orbitCenterYOffset = -0.15f;
        [SerializeField, Tooltip("궤도 회전 속도(초당 바퀴 수)")] private float orbitRevolutionsPerSec = 0.35f;
        [SerializeField, Tooltip("별 뒤를 따라오는 점 개수")] private int trailDotCount = 2;
        [SerializeField, Tooltip("점 사이의 각도 간격(라디안)")] private float trailAngleStep = 0.14f;

        [Header("반짝임 (블룸)")]
        [SerializeField, Tooltip("별마다 밝기(어두움/보통/밝음)가 바뀌는 속도(초당 단계 수)")] private float twinkleStepsPerSec = 6.0f;
        [SerializeField, Range(0.0f, 1.0f), Tooltip("나무 뒤쪽 별의 발광 배율(앞쪽 대비). 낮출수록 뒤쪽 블룸이 줄어듭니다")] private float backGlowScale = 0.5f;

        [Header("별가루 (궤도를 도는 별에서 흘러 바닥 쪽으로 떨어짐)")]
        [SerializeField, Tooltip("별 하나가 가루를 흘리는 간격(초)")] private float dustSpawnInterval = 0.16f;
        [SerializeField, Tooltip("낙하 가속도(월드 유닛/초^2)")] private float dustGravity = 1.6f;
        [SerializeField, Tooltip("가루 수명 최소(초)")] private float dustLifetimeMin = 0.6f;
        [SerializeField, Tooltip("가루 수명 최대(초)")] private float dustLifetimeMax = 0.9f;
        [SerializeField, Tooltip("좌우 흩날림 속도 최대(월드 유닛/초)")] private float dustDriftSpeed = 0.15f;

        [Header("등장 / 퇴장")]
        [SerializeField] private float enterDuration = 0.5f;
        [SerializeField, Tooltip("등장 중 나선이 도는 바퀴 수")] private float enterSpiralTurns = 1.0f;
        [SerializeField] private float exitDuration = 0.3f;

        [Header("소팅 (나무 캐노피 소팅 오더 기준 상대값)")]
        [SerializeField, Tooltip("나무 뒤 메쉬: 캐노피 소팅 오더 + 이 값")] private int backSortingOffset = -1;
        [SerializeField, Tooltip("나무 앞 메쉬: 캐노피 하이라이트 소팅 오더 + 이 값")] private int frontSortingOffset = 2;

        private const float PixelsPerUnit = 32.0f;
        private const float PixelUnit = 1.0f / PixelsPerUnit;
        private const float TwoPi = Mathf.PI * 2.0f;
        private const int MaxVerticesPerMesh = 60000;
        private const int BackLayer = 0;
        private const int FrontLayer = 1;
        private const int DustCapacity = 48;
        private const int MaxStarCount = 16;

        private enum WrapPhase
        {
            Entering,
            Orbiting,
            Exiting,
        }

        // 별가루 한 알. 고정 크기 링버퍼에 담아 재생 중 힙 할당이 없다.
        private struct DustParticle
        {
            public bool bActive;
            public int layer;
            public float x;
            public float y;
            public float vx;
            public float vy;
            public float age;
            public float life;
            public float wobblePhase;
            public Color32 color;
        }

        // 메쉬 버퍼는 매 프레임 Clear 후 채우므로 인스턴스끼리 공유해도 안전하다(메인 스레드 단일 실행)
        private static readonly List<Vector3>[] meshVertices = { new List<Vector3>(512), new List<Vector3>(512) };
        private static readonly List<Color32>[] meshColors = { new List<Color32>(512), new List<Color32>(512) };
        private static readonly List<int>[] meshTriangles = { new List<int>(768), new List<int>(768) };

        // 팔레트: 흰색 / 하늘색 2색 / 금색 2색 (낙인 각인과 같은 5색)
        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ColorIce = new Color32(170, 220, 255, 255);
        private static readonly Color32 ColorBlue = new Color32(91, 146, 230, 255);
        private static readonly Color32 ColorGold = new Color32(255, 212, 92, 255);

        //내부 의존성
        private readonly MeshFilter[] meshFilters = new MeshFilter[2];
        private readonly MeshRenderer[] meshRenderers = new MeshRenderer[2];
        private readonly Mesh[] meshes = new Mesh[2];

        private readonly DustParticle[] dusts = new DustParticle[DustCapacity];
        private readonly int[] dustLastTick = new int[MaxStarCount];

        //상태 변수
        private int dustCursor;
        private uint dustSeed;
        private TreeVisualComponent visual;
        private WrapPhase phase;
        private float elapsed;
        private float exitElapsed;
        private bool bInitialized;
        private bool bReleased = true;
        private Matrix4x4 worldToLocal;
        private float meshZ;

        // 컬링으로 이미 숨긴 상태인지 - 숨기는 처리(가루 비우기/렌더러 끄기)는 보이다가 꺼지는 순간 한 번이면 충분하다
        private bool bHiddenByCulling;

        // 마지막으로 렌더러에 넣은 소팅 오더 - 값이 바뀔 때만 다시 넣는다(Begin에서 초기화)
        private int appliedBackSortingOrder;
        private int appliedFrontSortingOrder;

        public TreeVisualComponent Visual => visual;

        /// <summary>
        /// 나무를 감싸는 연출을 시작한다. 풀에서 꺼낸 직후 InDungeonVFXManager가 호출한다.
        /// </summary>
        public void Begin(TreeVisualComponent _visual, string _sortingLayerName)
        {
            EnsureInitialized();

            visual = _visual;
            phase = WrapPhase.Entering;
            elapsed = 0.0f;
            exitElapsed = 0.0f;
            bReleased = false;
            ClearDust();

            for (int i = 0; i < 2; i++)
            {
                meshRenderers[i].sortingLayerName = _sortingLayerName;
                meshRenderers[i].enabled = false;
            }

            // 방금 가루를 비우고 렌더러를 껐으므로 "숨긴 상태"로 시작한다. 소팅 오더는 첫 빌드에서 반드시 넣도록 무효값으로 둔다.
            bHiddenByCulling = true;
            appliedBackSortingOrder = int.MinValue;
            appliedFrontSortingOrder = int.MinValue;

            gameObject.SetActive(true);
        }

        /// <summary>
        /// 퇴장 연출을 시작한다(끝나면 스스로 풀에 반환). 이미 퇴장 중이거나 반환된 뒤에는 아무 일도 하지 않는다.
        /// </summary>
        public void End()
        {
            if (true == bReleased || WrapPhase.Exiting == phase) return;

            phase = WrapPhase.Exiting;
            exitElapsed = 0.0f;
        }

        /// <summary>
        /// 연출 없이 즉시 풀에 반환한다(던전 이탈 등).
        /// </summary>
        public void ForceRelease()
        {
            Release();
        }

        private void Release()
        {
            if (true == bReleased) return;

            bReleased = true;
            visual = null;
            ClearDust();

            for (int i = 0; i < 2; i++)
            {
                if (null != meshRenderers[i]) meshRenderers[i].enabled = false;
                if (null != meshes[i]) meshes[i].Clear();
            }

            ReturnToPoolEvent?.Invoke(this);
        }

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            string[] names = { "BrandStarWrap_Back", "BrandStarWrap_Front" };
            for (int i = 0; i < 2; i++)
            {
                GameObject child = new GameObject(names[i]);
                child.transform.SetParent(transform, false);

                meshFilters[i] = child.AddComponent<MeshFilter>();
                meshRenderers[i] = child.AddComponent<MeshRenderer>();

                meshes[i] = new Mesh { name = "VFX_BrandStarWrap_" + names[i] };
                meshes[i].MarkDynamic();
                meshFilters[i].sharedMesh = meshes[i];

                meshRenderers[i].sharedMaterial = wrapMaterial;
                meshRenderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                meshRenderers[i].receiveShadows = false;
                meshRenderers[i].enabled = false;
            }
        }

        // ---------- 별가루 ----------

        private void ClearDust()
        {
            for (int i = 0; i < DustCapacity; i++) dusts[i].bActive = false;
            for (int i = 0; i < MaxStarCount; i++) dustLastTick[i] = 0;
            dustCursor = 0;
        }

        private void UpdateDust(float _dt)
        {
            for (int i = 0; i < DustCapacity; i++)
            {
                if (false == dusts[i].bActive) continue;

                dusts[i].age += _dt;
                if (dusts[i].age >= dusts[i].life)
                {
                    dusts[i].bActive = false;
                    continue;
                }

                dusts[i].vy -= dustGravity * _dt;
                dusts[i].x += dusts[i].vx * _dt;
                dusts[i].y += dusts[i].vy * _dt;
            }
        }

        // 별이 지나온 자리에서 가루를 하나 흘린다. 난수는 정수 해시라 프레임레이트와 무관하게 결정적이다.
        private void SpawnDust(float _x, float _y, int _layer, Color32 _color)
        {
            dustSeed = unchecked(dustSeed * 1664525u + 1013904223u);
            uint h = dustSeed >> 8;

            float rand01 = (h & 0xFF) / 255.0f;
            float randSigned = (((h >> 8) & 0xFF) / 127.5f) - 1.0f;

            DustParticle dust = new DustParticle();
            dust.bActive = true;
            dust.layer = _layer;
            dust.x = _x;
            dust.y = _y;
            dust.vx = randSigned * dustDriftSpeed;
            dust.vy = -0.1f;
            dust.age = 0.0f;
            dust.life = Mathf.Lerp(dustLifetimeMin, dustLifetimeMax, rand01);
            dust.wobblePhase = rand01 * TwoPi;
            dust.color = _color;

            dusts[dustCursor] = dust;
            dustCursor = (dustCursor + 1) % DustCapacity;
        }

        private void DrawDust()
        {
            for (int i = 0; i < DustCapacity; i++)
            {
                if (false == dusts[i].bActive) continue;

                float u = dusts[i].age / dusts[i].life;

                // 후반에는 프레임을 건너뛰며 점멸하다가 사라진다(반투명 페이드 없이 픽셀 단위로 소멸)
                if (0.75f < u && 0 != ((int)(dusts[i].age * 24.0f) & 1)) continue;

                float wx = dusts[i].x + Mathf.Sin(dusts[i].wobblePhase + dusts[i].age * 7.0f) * 0.03f;
                int px = Mathf.FloorToInt(wx * PixelsPerUnit);
                int py = Mathf.FloorToInt(dusts[i].y * PixelsPerUnit);

                // 앞쪽에서 흘린 가루는 나무 앞, 뒤쪽에서 흘린 가루는 나무 뒤(가루가 아래로 떨어져도 층은 유지)
                int layer = dusts[i].layer;
                Color32 baseColor = 0.6f < u ? ColorBlue : dusts[i].color;
                float glow = 0.4f * (1.0f - u * 0.5f) * (BackLayer == layer ? backGlowScale : 1.0f);
                byte glowAlpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(glow) * 255.0f);

                AddPixelRect(layer, px, py, px + 1, py + 1, new Color32(baseColor.r, baseColor.g, baseColor.b, glowAlpha));
            }
        }

        // ---------- 애니메이션 ----------

        private static float EaseOut(float _t)
        {
            return 1.0f - (1.0f - _t) * (1.0f - _t);
        }

        private void RebuildMeshes()
        {
            for (int i = 0; i < 2; i++)
            {
                meshVertices[i].Clear();
                meshColors[i].Clear();
                meshTriangles[i].Clear();
            }

            worldToLocal = transform.worldToLocalMatrix;
            Vector3 top = visual.GetTopRootPosition();
            meshZ = top.z;

            Vector3 bottom = visual.GetBottomRootPosition();
            float centerX = top.x;
            float centerY = top.y + orbitCenterYOffset;
            float baseY = bottom.y;

            float exitU = WrapPhase.Exiting == phase ? Mathf.Clamp01(exitElapsed / Mathf.Max(exitDuration, 0.0001f)) : 0.0f;
            float shrink = 1.0f - exitU * exitU;

            int count = Mathf.Max(1, starCount);
            for (int s = 0; s < count; s++)
            {
                // 별마다 등장 시작을 조금씩 어긋나게 해서 나선으로 이어 올라오는 모양을 만든다
                float stagger = 0.4f * s / count;
                float enterU = Mathf.Clamp01((elapsed - stagger) / Mathf.Max(enterDuration * 0.6f, 0.0001f));
                float enterEase = EaseOut(enterU);

                float baseAngle = TwoPi * s / count;
                float angle = baseAngle + elapsed * orbitRevolutionsPerSec * TwoPi + (1.0f - enterEase) * enterSpiralTurns * TwoPi;

                float radiusScale = enterEase * shrink;
                float cy = Mathf.Lerp(baseY, centerY, enterEase);

                // 크기: 등장 초반과 퇴장 후반에 계단식으로 작아진다(반투명 페이드 없음)
                int sizeStage = 2;
                if (enterU < 0.3f || exitU > 0.8f) sizeStage = 0;
                else if (enterU < 0.7f || exitU > 0.45f) sizeStage = 1;

                bool bPlus = 0 == (((int)(elapsed * 8.0f) + s) & 1);
                bool bGold = 2 == s % 3;
                Color32 baseColor = 0 == s % 3 ? ColorWhite : (1 == s % 3 ? ColorIce : ColorGold);

                // 별마다 위상이 다른 3단계 반짝임(어두움/보통/밝음). 알파 페이드가 아니라 HDR 발광 세기만 바뀌어서
                // 밝은 순간에만 블룸이 터진다. 금색 별은 포인트라 발광을 조금 더 준다.
                int twinkleStep = ((int)(elapsed * twinkleStepsPerSec + s * 1.7f)) % 3;
                float glow = 0 == twinkleStep ? 0.35f : (1 == twinkleStep ? 0.65f : 1.0f);
                if (true == bGold) glow = Mathf.Min(1.0f, glow + 0.15f);

                DrawStarAt(centerX, cy, radiusScale, angle, sizeStage, bPlus, baseColor, glow, true);

                // 궤도에 안착한 별만 가루를 흘린다(퇴장 중에는 새로 흘리지 않는다)
                if (2 == sizeStage && 0.0f == exitU && s < MaxStarCount)
                {
                    int tick = (int)((elapsed + s * 0.037f) / Mathf.Max(dustSpawnInterval, 0.01f));
                    if (tick != dustLastTick[s])
                    {
                        dustLastTick[s] = tick;
                        float sinA = Mathf.Sin(angle);
                        SpawnDust(centerX + orbitRadiusX * radiusScale * Mathf.Cos(angle), cy + orbitRadiusY * radiusScale * sinA, sinA < 0.0f ? FrontLayer : BackLayer, baseColor);
                    }
                }

                for (int k = 1; k <= trailDotCount; k++)
                {
                    float trailAngle = angle - trailAngleStep * k;
                    DrawStarAt(centerX, cy, radiusScale, trailAngle, 0, bPlus, 1 == k ? baseColor : ColorBlue, glow * 0.4f, false);
                }
            }

            DrawDust();

            // 나무 소팅 오더는 거의 바뀌지 않으므로 값이 달라졌을 때만 렌더러에 넣는다
            int backOrder = visual.GetTopSortingOrder() + backSortingOffset;
            if (backOrder != appliedBackSortingOrder)
            {
                meshRenderers[BackLayer].sortingOrder = backOrder;
                appliedBackSortingOrder = backOrder;
            }

            int frontOrder = visual.GetTopHighlightSortingOrder() + frontSortingOffset;
            if (frontOrder != appliedFrontSortingOrder)
            {
                meshRenderers[FrontLayer].sortingOrder = frontOrder;
                appliedFrontSortingOrder = frontOrder;
            }

            for (int i = 0; i < 2; i++)
            {
                meshes[i].Clear();
                meshes[i].SetVertices(meshVertices[i]);
                meshes[i].SetColors(meshColors[i]);
                meshes[i].SetTriangles(meshTriangles[i], 0, false);
                meshes[i].bounds = new Bounds(Vector3.zero, new Vector3(200.0f, 200.0f, 10.0f));
                meshRenderers[i].enabled = true;
            }
        }

        // 궤도 위 한 점에 별(또는 꼬리 점)을 그린다. 화면 아래쪽(sin < 0)은 나무 앞, 위쪽은 나무 뒤 메쉬로 보낸다.
        private void DrawStarAt(float _cx, float _cy, float _radiusScale, float _angle, int _sizeStage, bool _bPlus, Color32 _color, float _glow, bool _bStar)
        {
            float sin = Mathf.Sin(_angle);
            float cos = Mathf.Cos(_angle);
            int layer = sin < 0.0f ? FrontLayer : BackLayer;

            // 정점 알파 = HDR 발광 세기(0~1). 뒤쪽은 절반으로 어둡게 해서 나무 뒤에서 블룸이 번지지 않게 한다.
            byte glowAlpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow * (BackLayer == layer ? backGlowScale : 1.0f)) * 255.0f);

            float wx = _cx + orbitRadiusX * _radiusScale * cos;
            float wy = _cy + orbitRadiusY * _radiusScale * sin;

            int px = Mathf.FloorToInt(wx * PixelsPerUnit);
            int py = Mathf.FloorToInt(wy * PixelsPerUnit);

            if (false == _bStar)
            {
                Color32 dot = layer == BackLayer ? ColorBlue : _color;
                AddPixelRect(layer, px, py, px + 1, py + 1, new Color32(dot.r, dot.g, dot.b, glowAlpha));
                return;
            }

            // 뒤쪽 별은 한 단계 작고 파랗게(원근감)
            int stage = layer == BackLayer ? Mathf.Max(0, _sizeStage - 1) : _sizeStage;
            Color32 baseStarColor = layer == BackLayer ? ColorBlue : _color;
            Color32 color = new Color32(baseStarColor.r, baseStarColor.g, baseStarColor.b, glowAlpha);

            if (0 == stage)
            {
                AddPixelRect(layer, px, py, px + 1, py + 1, color);
                return;
            }

            // 앞쪽 큰 별은 팔 3칸(7px), 중간 단계와 뒤쪽 별은 더 작게
            int arm = 1 == stage ? 1 : (layer == FrontLayer ? 3 : 2);
            if (true == _bPlus)
            {
                AddPixelRect(layer, px - arm, py, px + arm + 1, py + 1, color);
                AddPixelRect(layer, px, py - arm, px + 1, py + arm + 1, color);
            }
            else
            {
                AddPixelRect(layer, px, py, px + 1, py + 1, color);
                for (int d = 1; d <= arm; d++)
                {
                    AddPixelRect(layer, px + d, py + d, px + d + 1, py + d + 1, color);
                    AddPixelRect(layer, px - d, py + d, px - d + 1, py + d + 1, color);
                    AddPixelRect(layer, px + d, py - d, px + d + 1, py - d + 1, color);
                    AddPixelRect(layer, px - d, py - d, px - d + 1, py - d + 1, color);
                }
            }
        }

        // 픽셀 좌표 직사각형 [x0,x1) x [y0,y1)를 쿼드로 추가한다. 월드 -> 로컬은 행렬 하나로 변환한다.
        private void AddPixelRect(int _layer, int _x0, int _y0, int _x1, int _y1, Color32 _color)
        {
            List<Vector3> vertices = meshVertices[_layer];
            if (MaxVerticesPerMesh <= vertices.Count + 4) return;

            int v = vertices.Count;
            vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(_x0 * PixelUnit, _y0 * PixelUnit, meshZ)));
            vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(_x0 * PixelUnit, _y1 * PixelUnit, meshZ)));
            vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(_x1 * PixelUnit, _y1 * PixelUnit, meshZ)));
            vertices.Add(worldToLocal.MultiplyPoint3x4(new Vector3(_x1 * PixelUnit, _y0 * PixelUnit, meshZ)));

            List<Color32> colors = meshColors[_layer];
            colors.Add(_color);
            colors.Add(_color);
            colors.Add(_color);
            colors.Add(_color);

            List<int> triangles = meshTriangles[_layer];
            triangles.Add(v);
            triangles.Add(v + 1);
            triangles.Add(v + 2);
            triangles.Add(v);
            triangles.Add(v + 2);
            triangles.Add(v + 3);
        }

        private void HideRenderers()
        {
            for (int i = 0; i < 2; i++)
            {
                if (null != meshRenderers[i]) meshRenderers[i].enabled = false;
            }
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void LateUpdate()
        {
            if (true == bReleased) return;

            if (null == visual)
            {
                Release();
                return;
            }

            float dt = Time.deltaTime;
            elapsed += dt;

            if (WrapPhase.Entering == phase && elapsed >= enterDuration)
            {
                phase = WrapPhase.Orbiting;
            }

            if (WrapPhase.Exiting == phase)
            {
                exitElapsed += dt;
                if (exitElapsed >= exitDuration)
                {
                    Release();
                    return;
                }
            }

            // 카메라 컬링으로 나무가 꺼져 있는 동안은 그리지 않는다(시간은 계속 흘러 다시 보일 때 자연스럽게 이어진다)
            if (false == visual.gameObject.activeInHierarchy)
            {
                // 다시 보일 때 허공에 굳은 가루가 남지 않도록 비운다. 꺼져 있는 동안은 가루가 새로 생기지 않으므로
                // 보이다가 꺼지는 순간 한 번만 처리하면 된다.
                if (false == bHiddenByCulling)
                {
                    ClearDust();
                    HideRenderers();
                    bHiddenByCulling = true;
                }
                return;
            }

            bHiddenByCulling = false;
            UpdateDust(dt);
            RebuildMeshes();
        }

        private void OnDisable()
        {
            HideRenderers();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < 2; i++)
            {
                if (null != meshes[i])
                {
                    Destroy(meshes[i]);
                    meshes[i] = null;
                }
            }
        }
    }
}
