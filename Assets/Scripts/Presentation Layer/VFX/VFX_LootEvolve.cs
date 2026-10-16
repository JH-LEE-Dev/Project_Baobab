using System;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 전리품 진화(레벨업) 연출. 마을 LootPillar의 전리품이 한 단계 올라갈 때 기둥에서 재생된다.
    /// 흐름(총 3.0초): 모으기(룬 고리가 차오르고 빛 알갱이가 소용돌이로 전리품에 빨려 들어감) -> 임계(작은 흰 구체) ->
    /// 폭발(도트 별 섬광, 빛기둥 + 헬릭스, 16방향 광선, 바닥 충격파/누운 별, 꽃잎/잎, 단계 마름모, 위로 솟는 ^ 화살표) -> 마무리(별이 돌다 흡수, 룬 고리가 입자로 풀림).
    ///
    /// 픽셀 규칙
    /// - 모든 그림은 32 PPU 정수 픽셀이다. 톤 격자에 그린 뒤 가로로 이어진 같은 톤을 쿼드 하나로 합친다. 반투명이 없고 알파는 발광 세기다(BrandWrapGlow).
    /// - 색 계열마다 3톤: 이전 단계 계열, 새 단계 계열, 중립색(단계 마름모), 흰색(섬광 전용).
    /// - 낙하/줄기/입자는 렌더 프레임마다 시간으로 다시 계산하고(연속), 섬광 단계/점멸/고리 회전은 24Hz 틱으로 끊는다. 위치는 시간과 해시로만 정한다.
    ///
    /// 기둥 프리팹의 자식으로 있고 풀을 쓰지 않는다. LootDisplayObject.SetGrowthTier가 단계가 오를 때 Play를 부르고,
    /// 폭발 시각(0.8초)에 콜백으로 알리면 그때 후광 색/스프라이트를 새 단계로 바꾼다. 재생 중 힙 할당이 없다(버퍼/격자는 Awake에서 확보).
    /// 좌표는 전리품 중심 기준 화면 픽셀(오른쪽 +, 아래쪽 +)이다.
    ///
    /// 아우라(전리품 뒤 중앙 글로우)도 같은 시간표로 변조한다: 모으기에서 0.55배까지 조여들며 뜨거워지고(흰색이 섞임),
    /// 폭발에서 1.5배로 터졌다가 흰색이 새 단계 색으로 풀리며 안정된다(UpdateAura).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_LootEvolve : MonoBehaviour
    {
        [Header("참조")]
        [SerializeField, Tooltip("전리품 스프라이트 렌더러. 받침대와 전리품 사이 층의 소팅이 이 렌더러를 따라간다.")]
        private SpriteRenderer itemRenderer;
        [SerializeField, Tooltip("전리품 Transform. 모으기 동안 떨리고 떠오르는 움직임을 준다. 비우면 움직이지 않는다.")]
        private Transform itemTransform;

        [Header("아우라 (중앙 글로우) 연동")]
        [SerializeField, Tooltip("전리품 뒤 중앙 글로우를 진화 타이밍에 맞춰 조였다가 터뜨린다. 비워 두면 아우라는 그대로다.")]
        private ItemAuraOrbitController auraController;

        [Header("쿼드 예산")]
        [SerializeField] private int quadBudget = 1800;

        [Header("공중 메쉬 소팅")]
        [SerializeField] private string airSortingLayerName = "FlyingItem";
        [SerializeField] private int airSortingOrder = 10;

        [Header("받침대와 전리품 사이 메쉬")]
        [SerializeField] private MeshFilter haloMeshFilter;
        [SerializeField] private MeshRenderer haloMeshRenderer;

        [Header("바닥 메쉬")]
        [SerializeField] private MeshFilter groundMeshFilter;
        [SerializeField] private MeshRenderer groundMeshRenderer;
        [SerializeField] private string groundSortingLayerName = "ShockWave";
        [SerializeField] private int groundSortingOrder = 0;

        private const float PixelsPerUnit = 32.0f;
        private const float TicksPerSecond = 24.0f;
        private const int TotalTicks = 72;
        private const int GatherTicks = 15;
        private const int BurstTick = 19;
        private const int GroundY = 29;         // 전리품 중심 기준 바닥 고리 중심(아래 +)
        private const int BeamHeight = 128;

        private const int GridMinX = -100;
        private const int GridMaxX = 100;
        private const int GridMinY = -140;
        private const int GridMaxY = 76;

        private const byte ToneNone = 0;
        private const byte ToneWhite = 1;
        private const byte ToneNeutralL = 2;
        private const byte ToneNeutralD = 3;
        private const byte ToneOldL = 4;
        private const byte ToneOldM = 5;
        private const byte ToneOldD = 6;
        private const byte ToneNewL = 7;
        private const byte ToneNewM = 8;
        private const byte ToneNewD = 9;
        private const int ToneCount = 10;

        private sealed class Layer
        {
            public PixelQuadBuffer buffer;
            public byte[] cells;
            public int width;
            public int height;
            public int dirtyMinX;
            public int dirtyMinY;
            public int dirtyMaxX;
            public int dirtyMaxY;
            public int worldMinX;
            public int worldMinY;
            public int worldMaxX;
            public int worldMaxY;
        }

        private static readonly Layer groundLayer = CreateLayer(900);
        private static readonly Layer haloLayer = CreateLayer(300);
        private static readonly Layer airLayer = CreateLayer(1500);

        // 톤 정의: 기본색 r,g,b(0~1)와 발광 배율. 알파 = (배율 - 1.2) / 4.8. 색 계열마다 3톤.
        private static readonly float[][] FamilyTones =
        {
            null,
            // 1 초록
            new[] { 0.55f, 0.88f, 0.58f, 1.65f, 0.28f, 0.82f, 0.38f, 1.40f, 0.14f, 0.50f, 0.24f, 1.25f },
            // 2 보라
            new[] { 0.72f, 0.62f, 1.00f, 1.65f, 0.52f, 0.38f, 0.98f, 1.40f, 0.30f, 0.22f, 0.62f, 1.25f },
            // 3 금
            new[] { 1.00f, 0.84f, 0.42f, 1.70f, 1.00f, 0.70f, 0.20f, 1.45f, 0.70f, 0.40f, 0.10f, 1.25f },
        };

        private static readonly int[] FlashArms = { 30, 20, 12, 6 };
        private static readonly float[] BeamGrow = { 0.4f, 0.8f, 1.0f };
        private static readonly int[] GroundStarArms = { 46, 36, 24, 12, 5 };
        private static readonly int[] PopSizes = { 0, 2, 4, 5, 4, 3 };

        private readonly Color32[] toneColors = new Color32[ToneCount];

        // 내부 의존성
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh airMesh;
        private Mesh haloMesh;
        private Mesh groundMesh;

        // 상태 변수
        private bool bInitialized;
        private bool bPlaying;
        private bool bBeginPending;
        private bool bBurstNotified;
        private float elapsed;
        private int fromTier;
        private int toTier;
        private Action onBurst;
        private int originPixelX;
        private int originPixelY;
        private Vector3 itemBaseLocalPosition;
        private bool bItemMoved;
        private int groundSortingLayerId;
        private int airSortingLayerId;
        private int appliedHaloLayerId = int.MinValue;
        private int appliedHaloOrder = int.MinValue;
        private int airUploadedVertexCount = -1;
        private ulong airUploadedHash;
        private int haloUploadedVertexCount = -1;
        private ulong haloUploadedHash;
        private int groundUploadedVertexCount = -1;
        private ulong groundUploadedHash;
        private Vector3 cullingCenter;
        private Vector3 cullingSize = new Vector3(8.0f, 8.0f, PixelVfxCulling.BoundsDepth);

        public bool IsPlaying => bPlaying;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 진화 연출을 재생한다. _fromTier(1~2)에서 _toTier(2~3)로 오르는 연출이다. _onBurst는 폭발 시각(약 0.8초)에 한 번 불린다.
        /// 재생 중에 다시 부르면 처음부터 다시 시작한다(이전 연출의 폭발 콜백이 아직 안 불렸다면 먼저 부른다).
        /// </summary>
        public void Play(int _fromTier, int _toTier, Action _onBurst)
        {
            EnsureInitialized();

            if (true == bPlaying) FinishPlayback(true);

            fromTier = Mathf.Clamp(_fromTier, 1, 3);
            toTier = Mathf.Clamp(_toTier, 1, 3);
            onBurst = _onBurst;
            bBurstNotified = false;
            bBeginPending = true;
            bPlaying = true;
            elapsed = 0.0f;
            enabled = true;
        }

        // 일반 비즈니스 로직 및 내부 메서드

        private static Layer CreateLayer(int _quadCapacity)
        {
            Layer layer = new Layer();
            layer.buffer = new PixelQuadBuffer(_quadCapacity);
            layer.width = GridMaxX - GridMinX + 1;
            layer.height = GridMaxY - GridMinY + 1;
            layer.cells = new byte[layer.width * layer.height];
            ResetDirty(layer);
            return layer;
        }

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            airMesh = CreateMesh(meshFilter, meshRenderer, "VFX_LootEvolve_Air");

            if (null != haloMeshFilter && null != haloMeshRenderer)
            {
                haloMesh = CreateMesh(haloMeshFilter, haloMeshRenderer, "VFX_LootEvolve_Halo");
            }

            if (null != groundMeshFilter && null != groundMeshRenderer)
            {
                groundMesh = CreateMesh(groundMeshFilter, groundMeshRenderer, "VFX_LootEvolve_Ground");
                groundSortingLayerId = SortingLayer.NameToID(groundSortingLayerName);
            }

            airSortingLayerId = SortingLayer.NameToID(airSortingLayerName);
            meshRenderer.sortingLayerID = airSortingLayerId;
            meshRenderer.sortingOrder = airSortingOrder;

            if (null != groundMeshRenderer)
            {
                groundMeshRenderer.sortingLayerID = groundSortingLayerId;
                groundMeshRenderer.sortingOrder = groundSortingOrder;
            }

            // 이 이펙트는 재생할 때만 켜 둔다(Play가 enabled를 켜고, 끝나면 끈다).
            enabled = false;
        }

        private static Mesh CreateMesh(MeshFilter _filter, MeshRenderer _renderer, string _name)
        {
            Mesh mesh = new Mesh { name = _name };
            mesh.MarkDynamic();
            PixelQuadBuffer.ReserveMesh(mesh, 1800);
            _filter.sharedMesh = mesh;

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            return mesh;
        }

        private static Color32 MakeTone(float _r, float _g, float _b, float _multiplier)
        {
            float alpha = Mathf.Clamp01((_multiplier - 1.2f) / 4.8f);
            return new Color32(
                (byte)Mathf.RoundToInt(Mathf.Clamp01(_r) * 255.0f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(_g) * 255.0f),
                (byte)Mathf.RoundToInt(Mathf.Clamp01(_b) * 255.0f),
                (byte)Mathf.RoundToInt(alpha * 255.0f));
        }

        private void SetupTones()
        {
            toneColors[ToneWhite] = MakeTone(1.0f, 1.0f, 1.0f, 2.6f);
            toneColors[ToneNeutralL] = MakeTone(0.95f, 0.96f, 1.0f, 1.7f);
            toneColors[ToneNeutralD] = MakeTone(0.45f, 0.47f, 0.56f, 1.25f);

            float[] old = FamilyTones[fromTier];
            float[] neu = FamilyTones[toTier];
            toneColors[ToneOldL] = MakeTone(old[0], old[1], old[2], old[3]);
            toneColors[ToneOldM] = MakeTone(old[4], old[5], old[6], old[7]);
            toneColors[ToneOldD] = MakeTone(old[8], old[9], old[10], old[11]);
            toneColors[ToneNewL] = MakeTone(neu[0], neu[1], neu[2], neu[3]);
            toneColors[ToneNewM] = MakeTone(neu[4], neu[5], neu[6], neu[7]);
            toneColors[ToneNewD] = MakeTone(neu[8], neu[9], neu[10], neu[11]);
        }

        private void Begin()
        {
            airUploadedVertexCount = -1;
            haloUploadedVertexCount = -1;
            groundUploadedVertexCount = -1;
            appliedHaloLayerId = int.MinValue;
            appliedHaloOrder = int.MinValue;
            bBurstNotified = false;

            Vector3 origin = null != itemTransform ? itemTransform.position : transform.position;
            originPixelX = Mathf.FloorToInt(origin.x * PixelsPerUnit);
            originPixelY = Mathf.FloorToInt(origin.y * PixelsPerUnit);

            if (null != itemTransform) itemBaseLocalPosition = itemTransform.localPosition;
            bItemMoved = false;

            SetupTones();

            float minX = originPixelX + GridMinX;
            float maxX = originPixelX + GridMaxX;
            float minY = originPixelY - GridMaxY;
            float maxY = originPixelY - GridMinY;
            cullingCenter = new Vector3((minX + maxX) * 0.5f / PixelsPerUnit, (minY + maxY) * 0.5f / PixelsPerUnit, origin.z);
            cullingSize = new Vector3((maxX - minX) / PixelsPerUnit, (maxY - minY) / PixelsPerUnit, PixelVfxCulling.BoundsDepth);
        }

        private void FinishPlayback(bool _bNotifyBurst)
        {
            if (true == _bNotifyBurst && false == bBurstNotified)
            {
                bBurstNotified = true;
                Action callback = onBurst;
                onBurst = null;
                if (null != callback) callback();
            }

            RestoreItem();
            if (null != auraController) auraController.ClearEvolveModulation();
            HideAll();
            bPlaying = false;
            onBurst = null;
            enabled = false;
        }

        private void RestoreItem()
        {
            if (true == bItemMoved && null != itemTransform)
            {
                itemTransform.localPosition = itemBaseLocalPosition;
            }

            bItemMoved = false;
        }

        // 아우라 변조 곡선(틱 단위, 시뮬레이션에서 확정한 값). 크기 배율 / 발광 배율 / 흰색 섞기
        private void UpdateAura(float _ft)
        {
            if (null == auraController) return;

            float scale;
            float intensity;
            float white;

            if (_ft < GatherTicks)
            {
                float u = (_ft / GatherTicks) * (_ft / GatherTicks);
                scale = 1.0f - 0.45f * u;
                intensity = 1.0f + 1.2f * u;
                white = 0.4f * u;
            }
            else if (_ft < BurstTick)
            {
                float v = (_ft - GatherTicks) / (BurstTick - GatherTicks);
                scale = 0.55f - 0.15f * v;
                intensity = 2.2f + 0.4f * v;
                white = 0.4f + 0.35f * v;
            }
            else
            {
                float p = _ft - BurstTick;
                scale = 1.0f + 0.5f * (1.0f - EaseOut(p / 12.0f, 2.0f));
                intensity = 1.0f + 1.3f * (1.0f - EaseOut(p / 24.0f, 2.0f));
                white = 0.75f * Mathf.Max(0.0f, 1.0f - EaseOut(p / 7.0f, 2.0f));

                if (14.0f < p)
                {
                    float breath = Mathf.Max(0.0f, 1.0f - (p - 14.0f) / 34.0f);
                    float wave = Mathf.Sin((p - 14.0f) * 0.5f) * breath;
                    scale += 0.05f * wave;
                    intensity += 0.2f * wave;
                }
            }

            auraController.SetEvolveModulation(scale, intensity, white);
        }

        private void HideAll()
        {
            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != haloMeshRenderer) haloMeshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
        }

        // ---------- 해시 ----------

        // 상태 없는 해시(0~1). 마지막에 값을 섞어서 연속한 번호가 사다리처럼 오르지 않게 한다(고리가 호 단위로 끊기던 문제 방지).
        private static float Hash(int _a, int _b)
        {
            uint v = 2166136261u;
            v = unchecked((v ^ (uint)_a) * 16777619u);
            v = unchecked((v ^ (uint)_b) * 16777619u);
            return Finish(v);
        }

        private static float Hash(int _a, int _b, int _c)
        {
            uint v = 2166136261u;
            v = unchecked((v ^ (uint)_a) * 16777619u);
            v = unchecked((v ^ (uint)_b) * 16777619u);
            v = unchecked((v ^ (uint)_c) * 16777619u);
            return Finish(v);
        }

        private static float Finish(uint _v)
        {
            uint v = _v;
            v ^= v >> 16;
            v = unchecked(v * 0x85ebca6bu);
            v ^= v >> 13;
            v = unchecked(v * 0xc2b2ae35u);
            v ^= v >> 16;
            return v / 4294967295.0f;
        }

        // ---------- 그리기 도우미 ----------

        private static void Plot(Layer _layer, int _sx, int _sy, byte _tone)
        {
            int gx = _sx - GridMinX;
            int gy = _sy - GridMinY;
            if (gx < 0 || _layer.width <= gx || gy < 0 || _layer.height <= gy) return;

            _layer.cells[gy * _layer.width + gx] = _tone;

            if (gx < _layer.dirtyMinX) _layer.dirtyMinX = gx;
            if (gy < _layer.dirtyMinY) _layer.dirtyMinY = gy;
            if (_layer.dirtyMaxX < gx) _layer.dirtyMaxX = gx;
            if (_layer.dirtyMaxY < gy) _layer.dirtyMaxY = gy;
        }

        private static void P(Layer _layer, float _x, float _y, byte _tone)
        {
            Plot(_layer, Mathf.RoundToInt(_x), Mathf.RoundToInt(_y), _tone);
        }

        private static void Plus(Layer _layer, float _x, float _y, byte _tone, int _r)
        {
            P(_layer, _x, _y, _tone);
            for (int k = 1; k <= _r; k++)
            {
                P(_layer, _x + k, _y, _tone);
                P(_layer, _x - k, _y, _tone);
                P(_layer, _x, _y + k, _tone);
                P(_layer, _x, _y - k, _tone);
            }
        }

        private static void XMark(Layer _layer, float _x, float _y, byte _tone)
        {
            P(_layer, _x, _y, _tone);
            P(_layer, _x + 1, _y + 1, _tone);
            P(_layer, _x - 1, _y + 1, _tone);
            P(_layer, _x + 1, _y - 1, _tone);
            P(_layer, _x - 1, _y - 1, _tone);
        }

        // 굵기가 줄어드는 도트 별: 중심 3x3, 팔 앞쪽은 2px 두께, 끝은 1px
        private static void DotStar(Layer _layer, float _fx, float _fy, int _arm, byte _core, byte _tip)
        {
            if (_arm <= 0) return;

            int x = Mathf.RoundToInt(_fx);
            int y = Mathf.RoundToInt(_fy);

            for (int j = -1; j <= 1; j++)
            {
                for (int i = -1; i <= 1; i++) Plot(_layer, x + i, y + j, _core);
            }

            int coreEnd = Mathf.Max(2, _arm / 3);
            for (int k = 2; k <= _arm; k++)
            {
                byte tone = k <= coreEnd ? _core : _tip;
                bool bThick = k <= _arm * 0.5f;

                for (int s = -1; s <= 1; s += 2)
                {
                    Plot(_layer, x + s * k, y, tone);
                    Plot(_layer, x, y + s * k, tone);
                    if (true == bThick)
                    {
                        Plot(_layer, x + s * k, y - 1, tone);
                        Plot(_layer, x - 1, y + s * k, tone);
                    }
                }
            }
        }

        private static void FillDisc(Layer _layer, float _cx, float _cy, float _rx, float _ry, byte _tone)
        {
            int ry = (int)_ry;
            int rx = (int)_rx;
            float srx = Mathf.Max(0.5f, _rx);
            float sry = Mathf.Max(0.5f, _ry);

            for (int j = -ry - 1; j <= ry + 1; j++)
            {
                for (int i = -rx - 1; i <= rx + 1; i++)
                {
                    float ex = i / srx;
                    float ey = j / sry;
                    if (ex * ex + ey * ey <= 1.0f) P(_layer, _cx + i, _cy + j, _tone);
                }
            }
        }

        // 점 고리. thick이면 점 4개마다 + 와 x 글리프가 번갈아 들어간다(. + . x 순서).
        private static void RingDots(Layer _layer, float _cx, float _cy, float _rx, float _ry, int _n, byte _tone, float _phase, float _keep, int _seed, bool _bGlyph)
        {
            for (int i = 0; i < _n; i++)
            {
                if (_keep < 1.0f && _keep < Hash(_seed, i)) continue;

                float ang = 2.0f * Mathf.PI * (i + _phase) / _n;
                float x = _cx + Mathf.Cos(ang) * _rx;
                float y = _cy + Mathf.Sin(ang) * _ry;
                int kind = i % 4;

                if (true == _bGlyph && 0 == kind) Plus(_layer, x, y, _tone, 1);
                else if (true == _bGlyph && 2 == kind) XMark(_layer, x, y, _tone);
                else P(_layer, x, y, _tone);
            }
        }

        private static float EaseOutQuart(float _u)
        {
            float v = 1.0f - Mathf.Clamp01(_u);
            return 1.0f - v * v * v * v;
        }

        private static float EaseIn(float _u, float _p)
        {
            return Mathf.Pow(Mathf.Clamp01(_u), _p);
        }

        private static float EaseOut(float _u, float _p)
        {
            return 1.0f - Mathf.Pow(1.0f - Mathf.Clamp01(_u), _p);
        }

        // ---------- 한 프레임 ----------

        private void DrawFrame(float _ft)
        {
            Layer G = groundLayer;
            Layer H = haloLayer;
            Layer A = airLayer;

            int tick = (int)_ft;
            float g = _ft < GatherTicks ? EaseIn(_ft / GatherTicks, 2.0f) : 1.0f;
            float p = _ft - BurstTick;

            int hover = 0;
            int tremble = 0;
            if (_ft < BurstTick)
            {
                hover = -Mathf.RoundToInt(3.0f * Mathf.Min(1.0f, _ft / GatherTicks));
                int amp = 1 + (int)(2.0f * g);
                tremble = Mathf.RoundToInt((Hash(tick / 2, 1) - 0.5f) * 2.0f * amp * 0.5f);
            }

            int ix = tremble;
            int iy = hover;
            if (0.0f <= p)
            {
                iy = -3 + (int)(3.0f * EaseOut(p / 9.0f, 3.0f));
                ix = 0;
            }

            MoveItem(ix, iy);

            // ================= 모으기 =================
            if (_ft < BurstTick)
            {
                DrawGather(G, H, A, _ft, tick, g, ix, iy);
            }

            // ================= 임계: 작은 흰 구체 =================
            if (GatherTicks <= _ft && _ft < BurstTick + 2)
            {
                float u = Mathf.Min(1.0f, (_ft - GatherTicks) / (BurstTick - GatherTicks));
                int rr = Mathf.RoundToInt(3.0f + 4.0f * u);
                FillDisc(A, ix, iy, rr + 1, rr + 1, ToneOldL);
                FillDisc(A, ix, iy, rr, rr, ToneWhite);

                byte sideTone = BurstTick <= _ft ? ToneNewL : ToneOldL;
                for (int yy = -BeamHeight; yy < iy - rr; yy++)
                {
                    Plot(A, ix, yy, ToneWhite);
                    if (0.6f < u)
                    {
                        Plot(A, ix + 1, yy, sideTone);
                        Plot(A, ix - 1, yy, sideTone);
                    }
                }
            }

            // ================= 폭발 =================
            if (0.0f <= p)
            {
                DrawBurst(G, A, p, tick, ix, iy, toTier);
            }
        }

        private static void DrawGather(Layer _g, Layer _h, Layer _a, float _ft, int _tick, float _gather, int _ix, int _iy)
        {
            int ph = _tick / 2;
            byte ringTone = _gather < 0.35f ? ToneOldM : ToneOldL;
            float keep = 0.3f + 0.7f * _gather;

            RingDots(_g, 0, GroundY, 30, 15, 34, ringTone, ph * 0.7f, keep, 3, true);
            RingDots(_g, 0, GroundY, 20, 10, 24, _gather > 0.5f ? ToneOldL : ToneOldM, -ph * 1.0f, keep, 4, false);

            for (int r = 0; r < 8; r++)
            {
                if (_gather * 8.0f > r)
                {
                    float a = r * Mathf.PI * 0.25f + ph * 0.04f;
                    Plus(_g, Mathf.Cos(a) * 34, GroundY + Mathf.Sin(a) * 17, ToneOldL, 2);
                }
            }

            // 후광 수축: 얇은 점선 고리 두 겹이 전리품으로 조여든다
            float rad = 20.0f - 11.0f * _gather;
            RingDots(_h, _ix, _iy + 2, rad, rad * 0.9f, 30, _gather < 0.5f ? ToneOldM : ToneOldL, ph * 0.8f, 0.5f + 0.5f * _gather, 61, false);
            RingDots(_h, _ix, _iy + 2, rad * 0.62f, rad * 0.56f, 22, ToneOldL, -ph * 1.1f, 0.4f + 0.6f * _gather, 62, false);
            if (0.55f < _gather)
            {
                int cr = 3 + (int)(2.0f * _gather);
                FillDisc(_h, _ix, _iy + 2, cr, cr, ToneOldL);
            }

            // 빛 알갱이 소용돌이(30개): 닫힌 식, 꼬리는 톤 계단
            for (int i = 0; i < 30; i++)
            {
                float start = (i / 30.0f) * 10.0f;
                float u = (_ft - start) / 5.0f;
                if (u < 0.0f || 1.0f < u) continue;

                float a0 = Hash(i, 7) * 2.0f * Mathf.PI;
                float r0 = 62.0f + Hash(i, 8) * 34.0f;
                float e = EaseIn(u, 1.7f);
                float r = r0 * (1.0f - e);
                float ang = a0 + e * 3.4f;
                float x = _ix + Mathf.Cos(ang) * r;
                float y = _iy + 6 + Mathf.Sin(ang) * r * 0.5f;

                for (int tl = 4; tl >= 1; tl--)
                {
                    float e2 = Mathf.Max(0.0f, e - tl * 0.06f);
                    float r2 = r0 * (1.0f - e2);
                    float a2 = a0 + e2 * 3.4f;
                    P(_a, _ix + Mathf.Cos(a2) * r2, _iy + 6 + Mathf.Sin(a2) * r2 * 0.5f, 2 < tl ? ToneOldM : ToneOldL);
                }

                int shape = (int)(Hash(i, 9) * 3.0f);
                byte head = 0.8f < e ? ToneWhite : ToneOldL;
                if (0 == shape) P(_a, x, y, head);
                else if (1 == shape) Plus(_a, x, y, head, 1);
                else
                {
                    P(_a, x, y, head);
                    P(_a, x + 1, y, ToneOldM);
                }
            }

            // 받침대 윗면에서 솟는 점선 빛줄기 7개(12Hz 점멸)
            const int surfaceY = 11;
            for (int k = 0; k < 7; k++)
            {
                float a = k * 2.0f * Mathf.PI / 7.0f + ph * 0.5f;
                float x = Mathf.Cos(a) * 9.0f;
                int top = surfaceY - 6 - (int)(_gather * 26.0f);
                for (int yy = top; yy < surfaceY; yy++)
                {
                    if (0 == (yy + k) % 2) P(_a, x, yy + Mathf.Sin(a) * 2.0f, 8 < surfaceY - yy ? ToneOldM : ToneOldL);
                }
            }
        }

        private static void DrawBurst(Layer _g, Layer _a, float _p, int _tick, int _ix, int _iy, int _toTier)
        {
            int pt = (int)_p;

            // 섬광 4단계
            if (pt < 4)
            {
                int arm = FlashArms[pt];
                DotStar(_a, _ix, _iy, arm, ToneWhite, ToneNewL);
                if (pt < 3) DotStar(_a, _ix, _iy, (int)(arm * 0.6f), ToneWhite, ToneWhite);
            }

            // 빛기둥: 단계별로 좁아지며 톤이 식는다
            if (_p < 30.0f)
            {
                int hh;
                int wd;
                byte core;
                byte edge;

                if (_p < 3.0f) { hh = (int)(BeamHeight * BeamGrow[pt]); wd = 2; core = ToneWhite; edge = ToneNewL; }
                else if (_p < 10.0f) { hh = BeamHeight; wd = 2; core = ToneWhite; edge = ToneNewL; }
                else if (_p < 20.0f) { hh = BeamHeight; wd = 1; core = ToneNewL; edge = ToneNewM; }
                else { hh = BeamHeight; wd = 0; core = ToneNewM; edge = ToneNewD; }

                for (int yy = _iy - hh; yy < _iy - 6; yy++)
                {
                    Plot(_a, _ix, yy, core);
                    for (int xx = 1; xx <= wd; xx++)
                    {
                        byte tone = xx == wd ? edge : core;
                        if (2 <= wd || 0 == (yy + xx) % 2 || _p < 10.0f)
                        {
                            Plot(_a, _ix + xx, yy, tone);
                            Plot(_a, _ix - xx, yy, tone);
                        }
                    }
                }
            }

            // 빛기둥을 감아 오르는 점 가닥 두 줄(헬릭스)
            if (3.0f <= _p && _p < 24.0f)
            {
                for (int strand = 0; strand < 2; strand++)
                {
                    for (int yy = _iy - 70; yy < _iy - 8; yy++)
                    {
                        if (0 != yy % 3) continue;

                        float phi = yy * 0.33f - _tick * 0.5f + strand * Mathf.PI;
                        float x = _ix + Mathf.Sin(phi) * 6.0f;
                        bool bFront = 0.0f < Mathf.Cos(phi);
                        byte tone = bFront ? ToneNewL : ToneNewM;
                        if (14.0f < _p) tone = bFront ? ToneNewM : ToneNewD;
                        P(_a, x, yy, tone);
                    }
                }
            }

            // 새 모습으로 바뀌는 스캔선(아래에서 위로 훑음)
            if (_p < 6.0f)
            {
                int ly = _iy + 9 - (int)(_p * 3.6f);
                for (int xx = -9; xx <= 9; xx++)
                {
                    Plot(_a, _ix + xx, ly, ToneWhite);
                    Plot(_a, _ix + xx, ly + 1, ToneNewL);
                }
            }

            // 16방향 광선: 같은 길이의 토막이 뻗었다가 줄어듦
            if (_p < 15.0f)
            {
                for (int k = 0; k < 16; k++)
                {
                    float ang = k * Mathf.PI / 8.0f;
                    float len = 0 == k % 2 ? 40.0f : 26.0f;
                    float head = EaseOutQuart(_p / 5.0f) * len;
                    float seg = 0 == k % 2 ? 12.0f : 9.0f;
                    float tail = _p <= 8.0f ? Mathf.Max(0.0f, head - seg) : head - seg * (1.0f - (_p - 8.0f) / 7.0f);

                    int from = (int)Mathf.Max(0.0f, tail) + 6;
                    int to = (int)head + 6;
                    for (int i = from; i <= to; i++)
                    {
                        float d = head + 6.0f - i;
                        byte tone = d < 2.0f ? ToneWhite : (d < 6.0f ? ToneNewL : ToneNewM);
                        P(_a, _ix + Mathf.Cos(ang) * i, _iy + Mathf.Sin(ang) * i, tone);
                    }
                }
            }

            // 바닥 이중 충격파(점선 고리 + 안쪽 고리), 톤이 식고 점이 줄어듦
            for (int ring = 0; ring < 2; ring++)
            {
                int delay = 0 == ring ? 0 : 4;
                float maxR = 0 == ring ? 74.0f : 52.0f;
                float u = (_p - delay) / 18.0f;
                if (u < 0.0f || 1.0f <= u) continue;

                float e = EaseOut(u, 3.0f);
                float rx = maxR * e;
                float ry = rx * 0.5f;
                byte tone = u < 0.12f ? ToneWhite : (u < 0.45f ? ToneNewL : (u < 0.75f ? ToneNewM : ToneNewD));
                float keep = 1.0f - 0.75f * u;
                int nn = (int)(24.0f + rx * 0.9f);

                RingDots(_g, 0, GroundY, rx, ry, nn, tone, 0.0f, keep, 11 + delay, true);
                RingDots(_g, 0, GroundY, rx * 0.93f, ry * 0.93f, nn, u < 0.6f ? ToneNewM : ToneNewD, 0.0f, keep * 0.8f, 21 + delay, false);
            }

            // 바닥에 누운 별 섬광(쿼터뷰 바닥면): 가로 팔이 길고 세로 팔은 절반. 4단계로 줄어든다
            if (pt < 5)
            {
                int ax = GroundStarArms[pt];
                int ay = ax / 2;
                for (int k = 1; k <= ax; k++)
                {
                    byte tone = k <= 3 ? ToneWhite : (k < ax * 0.6f ? ToneNewL : ToneNewM);
                    Plot(_g, k, GroundY, tone);
                    Plot(_g, -k, GroundY, tone);

                    if (k <= ay)
                    {
                        Plot(_g, 0, GroundY + k, tone);
                        Plot(_g, 0, GroundY - k, tone);
                        if (k <= 6)
                        {
                            Plot(_g, k, GroundY + 1, ToneNewL);
                            Plot(_g, -k, GroundY + 1, ToneNewL);
                            Plot(_g, k, GroundY - 1, ToneNewL);
                            Plot(_g, -k, GroundY - 1, ToneNewL);
                        }
                    }
                }
            }

            // 바닥 방사 줄기 8개(같은 길이 토막이 뻗었다가 줄어듦)
            if (_p < 9.0f)
            {
                float head = EaseOutQuart(_p / 5.0f) * 34.0f;
                const float seg = 10.0f;
                float tail = _p <= 5.0f ? Mathf.Max(0.0f, head - seg) : head - seg * (1.0f - (_p - 5.0f) / 4.0f);

                for (int k = 0; k < 8; k++)
                {
                    float ang = k * Mathf.PI * 0.25f + Mathf.PI * 0.125f;
                    int from = (int)Mathf.Max(0.0f, tail) + 8;
                    int to = (int)head + 8;
                    for (int i = from; i <= to; i++)
                    {
                        float d = head + 8.0f - i;
                        byte tone = d < 2.0f ? ToneWhite : (d < 5.0f ? ToneNewL : ToneNewM);
                        P(_g, Mathf.Cos(ang) * i, GroundY + Mathf.Sin(ang) * i * 0.5f, tone);
                    }
                }
            }

            // 꽃잎/잎 입자 30개
            for (int i = 0; i < 30; i++)
            {
                float u0 = Hash(i, 21) * 6.0f;
                float life = 31.0f + Hash(i, 22) * 22.0f;
                float u = (_p - u0) / life;
                if (u < 0.0f || 1.0f < u) continue;

                float a = Hash(i, 23) * 2.0f * Mathf.PI;
                float sp = 28.0f + Hash(i, 24) * 40.0f;
                float x = _ix + Mathf.Cos(a) * sp * EaseOut(u, 2.0f) + Mathf.Sin((_tick / 2) * 0.8f + i) * 3.0f;
                float y = _iy - 8 - Hash(i, 25) * 28.0f * EaseOut(u, 2.0f) + u * u * (30.0f + 26.0f * Hash(i, 26));

                int pick = (int)(Hash(i, 27) * 3.0f);
                byte tone = 0 == pick ? ToneNewL : (1 == pick ? ToneNewM : ToneWhite);
                if (0.72f < u)
                {
                    if (0 == (_tick + i) % 2) tone = ToneNewD;
                    if (0 == (_tick + i) % 3) continue;
                }

                int shape = (int)(Hash(i, 28) * 3.0f);
                if (0 == shape)
                {
                    P(_a, x, y, tone);
                    P(_a, x + 1, y, tone);
                }
                else if (1 == shape)
                {
                    P(_a, x, y, tone);
                    P(_a, x, y + 1, tone);
                    P(_a, x + 1, y + 1, tone);
                }
                else Plus(_a, x, y, tone, 1);
            }

            // 별가루 18개(12Hz 점멸)
            for (int i = 0; i < 18; i++)
            {
                float u0 = Hash(i, 31) * 10.0f;
                float life = 22.0f + Hash(i, 32) * 16.0f;
                float u = (_p - u0) / life;
                if (u < 0.0f || 1.0f < u) continue;

                float x = _ix + (Hash(i, 33) - 0.5f) * 36.0f;
                float y = _iy + 4 - u * (30.0f + Hash(i, 34) * 50.0f);
                if (0 == (_tick + i) % 2) Plus(_a, x, y, ToneNewL, u < 0.6f ? 2 : 1);
            }

            DrawPips(_a, _p, _tick, _toTier);
            DrawChevrons(_a, _p, _tick, _ix, _iy);

            // 둘레를 도는 별 4개 -> 흡수
            if (24.0f < _p && _p < 50.0f)
            {
                float ou = (_p - 24.0f) / 26.0f;
                for (int k = 0; k < 4; k++)
                {
                    float a = k * Mathf.PI * 0.5f + ou * 9.0f;
                    float rr = 20.0f * (1.0f - EaseIn(ou, 2.2f));
                    float x = _ix + Mathf.Cos(a) * rr;
                    float y = _iy + Mathf.Sin(a) * rr * 0.5f;
                    Plus(_a, x, y, ToneNewL, 2);

                    for (int tl = 1; tl <= 3; tl++)
                    {
                        float a2 = a - tl * 0.16f;
                        P(_a, _ix + Mathf.Cos(a2) * rr, _iy + Mathf.Sin(a2) * rr * 0.5f, tl < 3 ? ToneNewM : ToneNewD);
                    }
                }

                if (0.92f < ou) DotStar(_a, _ix, _iy, 8, ToneWhite, ToneNewL);
            }

            // 남는 룬 고리 -> 입자로 풀림(12Hz 단계)
            if (12.0f < _p && _p < 52.0f)
            {
                float du = (_p - 12.0f) / 40.0f;
                float keep = Mathf.Max(0.0f, 1.0f - (int)(du * 10.0f) / 10.0f);
                int ph = _tick / 2;

                RingDots(_g, 0, GroundY, 30, 15, 34, ToneNewM, ph * 0.7f, keep, 3, true);
                for (int i = 0; i < 34; i++)
                {
                    if (keep < Hash(3, i) && Hash(i, 41) < 0.5f)
                    {
                        float a = 2.0f * Mathf.PI * (i + ph * 0.7f) / 34.0f;
                        float rise = du * (14.0f + Hash(i, 42) * 20.0f);
                        P(_a, Mathf.Cos(a) * 30.0f, GroundY + Mathf.Sin(a) * 15.0f - rise, ToneNewL);
                    }
                }
            }
        }

        // 단계 마름모 3개: 채워진 칸/비어 있는 칸은 중립색 하나로 통일, 새 칸만 크기가 튀며 켜진다
        private static void DrawPips(Layer _a, float _p, int _tick, int _toTier)
        {
            if (_p <= 3.0f) return;

            bool bShow = _p < 48.0f || (_p < 54.0f && 0 == _tick % 2);
            if (false == bShow) return;

            const int pipY = -26;
            float pu = _p - 3.0f;
            int newIndex = _toTier - 1;

            for (int k = 0; k < 3; k++)
            {
                int px = (k - 1) * 10;
                bool bFilled = k < _toTier;
                bool bNew = k == newIndex;

                int r;
                byte col;
                if (true == bNew)
                {
                    r = pu < 6.0f ? PopSizes[Mathf.Min(5, (int)pu)] : 3;
                    col = pu < 3.0f ? ToneWhite : ToneNeutralL;
                }
                else
                {
                    r = 3;
                    col = true == bFilled ? ToneNeutralL : ToneNeutralD;
                }

                for (int j = -r; j <= r; j++)
                {
                    int wd = r - Mathf.Abs(j);
                    for (int i2 = -wd; i2 <= wd; i2++)
                    {
                        if (true == bNew || true == bFilled || Mathf.Abs(i2) == wd) Plot(_a, px + i2, pipY + j, col);
                    }
                }

                if (true == bNew && pu < 3.0f) DotStar(_a, px, pipY, 5, ToneWhite, ToneNeutralL);
            }
        }

        // 레벨업 화살표: 이전의 아래로 향한 V 모양을 위로 뒤집은 ^ 모양이 빛기둥 양옆에서 위로 솟는다
        private static void DrawChevrons(Layer _a, float _p, int _tick, int _ix, int _iy)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                for (int k = 0; k < 2; k++)
                {
                    float u0 = 4.0f + k * 6.0f + (side < 0 ? 0.0f : 3.0f);
                    float u = (_p - u0) / 22.0f;
                    if (u < 0.0f || 1.0f < u) continue;

                    float cy = _iy - 4 - EaseOut(u, 2.0f) * 54.0f;
                    float ax = side * 17;
                    byte tone = u < 0.45f ? ToneNewL : (u < 0.8f ? ToneNewM : ToneNewD);
                    if (0.8f < u && 0 != _tick % 2) continue;

                    for (int j = 0; j < 5; j++)
                    {
                        for (int i2 = -j; i2 <= j; i2++)
                        {
                            if (Mathf.Abs(i2) >= j - 1) P(_a, ax + i2, cy + j, tone);
                        }
                    }

                    P(_a, ax, cy - 1, u < 0.6f ? ToneWhite : tone);
                }
            }
        }

                private void MoveItem(int _ix, int _iy)
        {
            if (null == itemTransform) return;

            if (0 == _ix && 0 == _iy)
            {
                if (true == bItemMoved)
                {
                    itemTransform.localPosition = itemBaseLocalPosition;
                    bItemMoved = false;
                }

                return;
            }

            itemTransform.localPosition = itemBaseLocalPosition + new Vector3(_ix / PixelsPerUnit, -_iy / PixelsPerUnit, 0.0f);
            bItemMoved = true;
        }

        private void RebuildMesh(float _ft)
        {
            DrawFrame(_ft);

            bool bHalo = null != haloMesh;
            bool bGround = null != groundMesh;

            groundLayer.buffer.Clear();
            haloLayer.buffer.Clear();
            airLayer.buffer.Clear();

            groundLayer.buffer.SetWorldTransform(null != groundMeshRenderer ? groundMeshRenderer.transform.worldToLocalMatrix : transform.worldToLocalMatrix, transform.position.z);
            haloLayer.buffer.SetWorldTransform(null != haloMeshRenderer ? haloMeshRenderer.transform.worldToLocalMatrix : transform.worldToLocalMatrix, transform.position.z);
            airLayer.buffer.SetWorldTransform(transform.worldToLocalMatrix, transform.position.z);

            ResetWorldBounds(groundLayer);
            ResetWorldBounds(haloLayer);
            ResetWorldBounds(airLayer);

            // 쿼드 예산은 바닥, 받침대 사이, 공중 순으로 나눠 쓴다. 메쉬가 없는 층은 공중 메쉬로 합친다(폴백).
            EmitLayer(groundLayer, true == bGround ? groundLayer : airLayer);
            EmitLayer(haloLayer, true == bHalo ? haloLayer : airLayer);
            EmitLayer(airLayer, airLayer);

            if (true == bGround) UploadMesh(groundLayer, groundMesh, groundMeshRenderer, groundMeshRenderer.transform.position, ref groundUploadedVertexCount, ref groundUploadedHash);
            if (true == bHalo) UploadMesh(haloLayer, haloMesh, haloMeshRenderer, haloMeshRenderer.transform.position, ref haloUploadedVertexCount, ref haloUploadedHash);
            UploadMesh(airLayer, airMesh, meshRenderer, transform.position, ref airUploadedVertexCount, ref airUploadedHash);
        }

        private static void ResetWorldBounds(Layer _layer)
        {
            _layer.worldMinX = int.MaxValue;
            _layer.worldMinY = int.MaxValue;
            _layer.worldMaxX = int.MinValue;
            _layer.worldMaxY = int.MinValue;
        }

        private static void ResetDirty(Layer _layer)
        {
            _layer.dirtyMinX = int.MaxValue;
            _layer.dirtyMinY = int.MaxValue;
            _layer.dirtyMaxX = int.MinValue;
            _layer.dirtyMaxY = int.MinValue;
        }

        // 격자에 모은 톤을 가로로 이어진 같은 톤끼리 쿼드 하나로 합쳐 버퍼에 넣고, 격자를 비운다.
        private void EmitLayer(Layer _source, Layer _target)
        {
            if (_source.dirtyMaxY < _source.dirtyMinY) return;

            int width = _source.width;
            PixelQuadBuffer buffer = _target.buffer;

            for (int gy = _source.dirtyMinY; gy <= _source.dirtyMaxY; gy++)
            {
                int row = gy * width;
                int x = _source.dirtyMinX;

                while (x <= _source.dirtyMaxX)
                {
                    byte tone = _source.cells[row + x];
                    if (ToneNone == tone)
                    {
                        x++;
                        continue;
                    }

                    int start = x;
                    while (x + 1 <= _source.dirtyMaxX && tone == _source.cells[row + x + 1]) x++;
                    int end = x;

                    for (int k = start; k <= end; k++) _source.cells[row + k] = ToneNone;
                    x++;

                    if (quadBudget * 4 <= buffer.VertexCount) continue;

                    int wx0 = originPixelX + GridMinX + start;
                    int wx1 = originPixelX + GridMinX + end + 1;
                    int wy = originPixelY - (GridMinY + gy);
                    buffer.AddWorldRect(wx0, wy, wx1, wy + 1, toneColors[tone]);

                    if (wx0 < _target.worldMinX) _target.worldMinX = wx0;
                    if (wy < _target.worldMinY) _target.worldMinY = wy;
                    if (_target.worldMaxX < wx1) _target.worldMaxX = wx1;
                    if (_target.worldMaxY < wy + 1) _target.worldMaxY = wy + 1;
                }
            }

            ResetDirty(_source);
        }

        private static void UploadMesh(Layer _layer, Mesh _mesh, MeshRenderer _renderer, Vector3 _objectPosition, ref int _uploadedVertexCount, ref ulong _uploadedHash)
        {
            PixelQuadBuffer buffer = _layer.buffer;

            if (0 == buffer.VertexCount)
            {
                if (true == _renderer.enabled) _renderer.enabled = false;
                _uploadedVertexCount = -1;
                return;
            }

            if (buffer.VertexCount != _uploadedVertexCount || buffer.ContentHash != _uploadedHash)
            {
                buffer.Upload(_mesh, 0);
                _uploadedVertexCount = buffer.VertexCount;
                _uploadedHash = buffer.ContentHash;

                float minX = _layer.worldMinX / PixelsPerUnit - _objectPosition.x;
                float minY = _layer.worldMinY / PixelsPerUnit - _objectPosition.y;
                float maxX = _layer.worldMaxX / PixelsPerUnit - _objectPosition.x;
                float maxY = _layer.worldMaxY / PixelsPerUnit - _objectPosition.y;

                Bounds bounds = new Bounds(
                    new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0.0f),
                    new Vector3(maxX - minX + 0.1f, maxY - minY + 0.1f, PixelVfxCulling.BoundsDepth));

                Bounds current = _mesh.bounds;
                if (current.center != bounds.center || current.size != bounds.size) _mesh.bounds = bounds;
            }

            if (false == _renderer.enabled) _renderer.enabled = true;
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void LateUpdate()
        {
            if (false == bPlaying) return;

            if (true == bBeginPending)
            {
                bBeginPending = false;
                Begin();
            }

            elapsed += Time.deltaTime;
            float ft = elapsed * TicksPerSecond;

            if (BurstTick <= ft && false == bBurstNotified)
            {
                bBurstNotified = true;
                Action callback = onBurst;
                onBurst = null;
                if (null != callback) callback();
            }

            if (TotalTicks <= ft)
            {
                FinishPlayback(false);
                return;
            }

            UpdateAura(ft);

            // 받침대와 전리품 사이 층: 전리품 렌더러보다 한 칸 아래(받침대 위)
            if (null != itemRenderer && null != haloMeshRenderer)
            {
                int layerId = itemRenderer.sortingLayerID;
                int order = itemRenderer.sortingOrder - 1;
                if (layerId != appliedHaloLayerId || order != appliedHaloOrder)
                {
                    appliedHaloLayerId = layerId;
                    appliedHaloOrder = order;
                    haloMeshRenderer.sortingLayerID = layerId;
                    haloMeshRenderer.sortingOrder = order;
                }
            }

            float margin = 2.0f * PixelVfxCulling.MarginUnits;
            Vector3 size = new Vector3(cullingSize.x + margin, cullingSize.y + margin, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(cullingCenter, size))
            {
                HideAll();
                return;
            }

            RebuildMesh(ft);
        }

        private void OnDisable()
        {
            if (true == bPlaying) FinishPlayback(true);
            else HideAll();
        }

        private void OnDestroy()
        {
            RestoreItem();
            if (null != auraController) auraController.ClearEvolveModulation();

            if (null != airMesh)
            {
                Destroy(airMesh);
                airMesh = null;
            }

            if (null != haloMesh)
            {
                Destroy(haloMesh);
                haloMesh = null;
            }

            if (null != groundMesh)
            {
                Destroy(groundMesh);
                groundMesh = null;
            }
        }

        private void OnValidate()
        {
            quadBudget = Mathf.Clamp(quadBudget, 400, 6000);
        }
    }
}
