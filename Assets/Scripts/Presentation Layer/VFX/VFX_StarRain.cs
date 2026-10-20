using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 낙하 한 개의 충돌 정보. 데미지 판정 쪽(Application Layer)이 읽는다. position은 월드 좌표(충돌 중심), time은 재생 시작 후 충돌까지의 초.
    /// </summary>
    public struct StarRainImpact
    {
        public Vector3 position;
        public float time;
    }

    /// <summary>
    /// 별 비(유성우). 캐릭터 주변에 별이 비스듬히 여러 개 떨어진다. 낙하 한 개는 "예고(디더 원판 + 점선 고리) -> 낙하(굵은 꼬리) ->
    /// 충돌(도트 별 섬광 + 빛기둥 + 8방향 줄기 + 파편) -> 바닥 별 자국이 입자로 풀림" 순서로 진행하는 독립된 단위이고,
    /// 이 이펙트 하나가 낙하 N개를 정해진 시각에 돌려 같은 메쉬 두 개(바닥 / 공중)에 함께 그린다. 낙하마다 인스턴스를 만들지 않는다.
    ///
    /// 픽셀 규칙
    /// - 모든 그림은 32 PPU 정수 픽셀이다. 한 줄에서 이어지는 같은 톤은 쿼드 하나로 합친다. 반투명이 없고 알파는 발광 세기다(BrandWrapGlow).
    /// - 색 계열마다 최대 3톤: 푸른 계열(옅음/중간/진함), 흰색(섬광 전용), 단계 강조색(씨앗 푸른 옅음, 꽃 보라, 나무 금색).
    /// - 낙하/줄기/파편/솟는 별은 렌더 프레임마다 시간으로 다시 계산해(연속) 부드럽게 움직이고, 섬광 단계/점멸/디더는 24Hz 단계로 끊어서 도트 느낌을 지킨다.
    /// - 위치는 경과 시간과 해시만으로 계산하는 닫힌 식이라 프레임 속도와 무관하고, 낙하 위치/시각은 시드로 정해져 판정 쪽과 같은 값을 얻을 수 있다.
    ///
    /// 낙하 수는 [최소(초당 3개 x 지속시간), 최대 40]로 제한된다(ClampCount). 풀 인스턴스로 쓰이며 루트 ParticleSystem 수명이 끝나면 풀로 돌아간다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_StarRain : MonoBehaviour
    {
        /// <summary>낙하 수 상한. 판정 쪽도 같은 값으로 제한한다.</summary>
        public const int MaxCount = 40;

        /// <summary>낙하 수 하한을 정하는 초당 낙하 수(지속시간 x 이 값, 올림).</summary>
        public const float MinPerSecond = 3.0f;

        [Header("기본값 (Play를 부르지 않았을 때)")]
        [SerializeField, Range(1, 3), Tooltip("1 씨앗(하늘색), 2 꽃(보라), 3 나무(금색)")]
        private int defaultTier = 2;
        [SerializeField, Range(2.0f, 8.0f), Tooltip("낙하가 이어지는 시간(초)")]
        private float defaultDuration = 4.0f;
        [SerializeField, Range(1.0f, 6.0f), Tooltip("낙하 범위(가로 반지름, 월드 유닛). 세로는 절반이다.")]
        private float defaultRadius = 2.4f;

        [Header("쿼드 예산")]
        [SerializeField, Tooltip("두 메쉬를 합친 한 프레임 쿼드 상한. 넘으면 더 그리지 않는다.")]
        private int quadBudget = 2400;

        [Header("공중 메쉬 소팅")]
        [SerializeField] private int sortingOrderOffset = 0;

        [Header("바닥 메쉬 (예고/줄기/별 자국이 나무와 캐릭터 뒤에 그려지도록 분리)")]
        [SerializeField] private MeshFilter groundMeshFilter;
        [SerializeField] private MeshRenderer groundMeshRenderer;
        [SerializeField, Tooltip("바닥 메쉬 소팅 레이어. 나무/캐릭터가 있는 Objects보다 아래여야 한다.")]
        private string groundSortingLayerName = "ShockWave";
        [SerializeField] private int groundSortingOrder = 0;

        private const float PixelsPerUnit = 32.0f;
        private const float TicksPerSecond = 24.0f;
        private const float GroundSquashY = 0.5f;
        private const float FirstImpactTime = 0.9f;     // 첫 낙하가 닿는 시각(초)
        private const float TailMargin = 0.2f;          // 지속시간 끝에서 마지막 낙하까지 남기는 시간(초)
        private const float MinSpacing = 22.0f;         // 낙하끼리 최소 간격(바닥 거리 환산 px)
        private const int PlaceAttempts = 80;
        private const int FallTicks = 12;
        private const int TeleTicks = 17;
        private const int ImpactTicks = 16;             // 충돌 후 연출이 끝나는 틱
        private const float OffsetX = 72.0f;            // 낙하 시작점(충돌점 기준, 오른쪽 위에서 비스듬히)
        private const float OffsetY = -165.0f;          // 화면 아래가 +인 좌표에서 위쪽 = 음수
        private const float FallCurve = 1.6f;

        private const byte ToneNone = 0;
        private const byte ToneWhite = 1;
        private const byte ToneIce = 2;
        private const byte ToneBlue = 3;
        private const byte ToneDeep = 4;
        private const byte ToneAccent = 5;

        private struct Unit
        {
            public int x;           // 시전자 발밑 기준 화면 픽셀(오른쪽 +)
            public int y;           // 시전자 발밑 기준 화면 픽셀(아래쪽 +)
            public int impactTick;
            public uint seed;
            public bool bBig;
        }

        private sealed class Layer
        {
            public PixelQuadBuffer buffer;
            public byte[] cells = new byte[0];
            public int minX;
            public int minY;
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

        private static readonly Layer groundLayer = new Layer { buffer = new PixelQuadBuffer(1600) };
        private static readonly Layer airLayer = new Layer { buffer = new PixelQuadBuffer(1600) };
        private static readonly Unit[] scratchUnits = new Unit[MaxCount];
        private static readonly int[] FlashArms = { 16, 11, 6, 3 };
        private static readonly int[] PillarHeights = { 36, 24, 10 };
        private static readonly int[] CallArmX = { 12, 9, 5, 2 };
        private static readonly int[] CallArmY = { 4, 3, 2, 1 };

        private readonly Unit[] units = new Unit[MaxCount];
        private readonly Color32[] toneColors = new Color32[6];

        // 내부 의존성
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh airMesh;
        private Mesh groundMesh;
        private ParticleSystemRenderer rootParticleRenderer;

        // 상태 변수
        private bool bInitialized;
        private bool bBeginPending;
        private bool bFirstFrame;
        private bool bPlaySet;
        private float elapsed;
        private float totalLength;
        private float frameTicks = 0.5f;
        private int unitCount;
        private int requestedCount;
        private float requestedDuration;
        private float requestedRadius;
        private int requestedSeed;
        private int requestedTier;
        private int originPixelX;
        private int originPixelY;
        private int groundSortingLayerId;
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;
        private int airUploadedVertexCount = -1;
        private ulong airUploadedHash;
        private int groundUploadedVertexCount = -1;
        private ulong groundUploadedHash;
        private Vector3 cullingCenter;
        private Vector3 cullingSize = new Vector3(10.0f, 10.0f, PixelVfxCulling.BoundsDepth);

        public int ImpactCount => unitCount;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 낙하 수를 허용 범위로 제한한다. 최소는 ceil(지속시간 x 초당 3), 최대는 MaxCount. 판정 쪽도 같은 함수로 맞춘다.
        /// </summary>
        public static int ClampCount(int _count, float _duration)
        {
            int min = Mathf.Min(MaxCount, Mathf.CeilToInt(Mathf.Max(0.0f, _duration) * MinPerSecond));
            return Mathf.Clamp(_count, min, MaxCount);
        }

        /// <summary>
        /// 이번 재생의 낙하 수/지속시간/범위/시드/단계를 정한다. 풀에서 꺼내져 활성화된 직후(첫 LateUpdate 전)에 호출해야 한다.
        /// 위치는 이 오브젝트의 월드 위치(시전자 발밑)가 낙하 범위 중심이다.
        /// </summary>
        /// <param name="_count">낙하 수(ClampCount로 제한됨)</param>
        /// <param name="_duration">낙하가 이어지는 시간(초)</param>
        /// <param name="_radius">낙하 범위 가로 반지름(월드 유닛)</param>
        /// <param name="_seed">낙하 위치/시각을 정하는 시드. 판정 쪽이 ComputeImpacts에 같은 값을 쓰면 같은 결과를 얻는다.</param>
        /// <param name="_tier">1 씨앗, 2 꽃, 3 나무(강조색과 큰 별 개수)</param>
        public void Play(int _count, float _duration, float _radius, int _seed, int _tier)
        {
            requestedDuration = Mathf.Clamp(_duration, 2.0f, 8.0f);
            requestedCount = ClampCount(_count, requestedDuration);
            requestedRadius = Mathf.Clamp(_radius, 1.0f, 6.0f);
            requestedSeed = _seed;
            requestedTier = Mathf.Clamp(_tier, 1, 3);
            bPlaySet = true;
        }

        /// <summary>
        /// 낙하의 충돌 위치/시각을 계산해 돌려준다(이펙트 인스턴스 없이). Play에 같은 인자를 쓰면 이펙트가 그리는 낙하와 정확히 일치한다.
        /// _count는 ClampCount로 제한한 값을 써야 한다. 돌려주는 값은 채운 개수.
        /// </summary>
        public static int ComputeImpacts(Vector3 _origin, int _seed, int _count, float _duration, float _radius, StarRainImpact[] _results)
        {
            float duration = Mathf.Clamp(_duration, 2.0f, 8.0f);
            int count = ClampCount(_count, duration);
            float radius = Mathf.Clamp(_radius, 1.0f, 6.0f);

            BuildUnits(_seed, count, duration, radius, scratchUnits);

            int originX = Mathf.FloorToInt(_origin.x * PixelsPerUnit);
            int originY = Mathf.FloorToInt(_origin.y * PixelsPerUnit);
            int n = Mathf.Min(count, null != _results ? _results.Length : 0);

            for (int i = 0; i < n; i++)
            {
                _results[i].position = new Vector3((originX + scratchUnits[i].x + 0.5f) / PixelsPerUnit, (originY - scratchUnits[i].y + 0.5f) / PixelsPerUnit, _origin.z);
                _results[i].time = scratchUnits[i].impactTick / TicksPerSecond;
            }

            return n;
        }

        /// <summary>
        /// 이번 재생의 낙하 _index번째 충돌 정보. Begin 이후(첫 LateUpdate 이후)에 유효하다.
        /// </summary>
        public bool TryGetImpact(int _index, out StarRainImpact _impact)
        {
            if (_index < 0 || unitCount <= _index)
            {
                _impact = default;
                return false;
            }

            _impact.position = new Vector3((originPixelX + units[_index].x + 0.5f) / PixelsPerUnit, (originPixelY - units[_index].y + 0.5f) / PixelsPerUnit, transform.position.z);
            _impact.time = units[_index].impactTick / TicksPerSecond;
            return true;
        }

        // 일반 비즈니스 로직 및 내부 메서드

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            airMesh = CreateMesh(meshFilter, meshRenderer, "VFX_StarRain_Air");

            if (null != groundMeshFilter && null != groundMeshRenderer)
            {
                groundMesh = CreateMesh(groundMeshFilter, groundMeshRenderer, "VFX_StarRain_Ground");
                groundSortingLayerId = SortingLayer.NameToID(groundSortingLayerName);
            }
        }

        private static Mesh CreateMesh(MeshFilter _filter, MeshRenderer _renderer, string _name)
        {
            Mesh mesh = new Mesh { name = _name };
            mesh.MarkDynamic();
            PixelQuadBuffer.ReserveMesh(mesh, 2400);
            _filter.sharedMesh = mesh;

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            return mesh;
        }

        // 풀에서 꺼내져 활성화되면 Play가 불릴 시간을 주기 위해 시작을 첫 LateUpdate로 미룬다.
        private void Begin()
        {
            EnsureInitialized();

            if (false == bPlaySet)
            {
                requestedDuration = defaultDuration;
                requestedCount = ClampCount(Mathf.RoundToInt(defaultDuration * MinPerSecond), defaultDuration);
                requestedRadius = defaultRadius;
                requestedSeed = Time.frameCount * 7919;
                requestedTier = defaultTier;
            }

            elapsed = 0.0f;
            bFirstFrame = true;
            airUploadedVertexCount = -1;
            groundUploadedVertexCount = -1;

            Vector3 pos = transform.position;
            originPixelX = Mathf.FloorToInt(pos.x * PixelsPerUnit);
            originPixelY = Mathf.FloorToInt(pos.y * PixelsPerUnit);

            unitCount = requestedCount;
            BuildUnits(requestedSeed, unitCount, requestedDuration, requestedRadius, units);

            if (3 == requestedTier)
            {
                // 큰 별 3개(순서가 고정된 자리)
                for (int i = 0; i < unitCount; i++)
                {
                    units[i].bBig = 4 == i || 9 == i || 13 == i || (unitCount > 15 && 0 == i % 5 && 0 < i);
                }
            }

            int maxTick = 0;
            for (int i = 0; i < unitCount; i++)
            {
                if (maxTick < units[i].impactTick) maxTick = units[i].impactTick;
            }

            totalLength = (maxTick + ImpactTicks) / TicksPerSecond + 0.1f;

            SetupTones(requestedTier);
            SetupLayers(requestedRadius * PixelsPerUnit);
        }

        private void SetupTones(int _tier)
        {
            // (기본색 x 발광 배율). 알파 = (배율 - 1.2) / 4.8. 한 색 계열 최대 3톤.
            toneColors[ToneWhite] = new Color32(255, 255, 255, 74);
            toneColors[ToneIce] = new Color32(140, 199, 235, 13);
            toneColors[ToneBlue] = new Color32(77, 133, 255, 0);
            toneColors[ToneDeep] = new Color32(43, 71, 184, 0);

            if (1 == _tier) toneColors[ToneAccent] = toneColors[ToneIce];
            else if (2 == _tier) toneColors[ToneAccent] = new Color32(184, 158, 255, 13);
            else toneColors[ToneAccent] = new Color32(255, 214, 107, 16);
        }

        private void SetupLayers(float _radiusPx)
        {
            float ry = _radiusPx * GroundSquashY;
            int minX = -Mathf.CeilToInt(_radiusPx) - 90;
            int maxX = Mathf.CeilToInt(_radiusPx + OffsetX) + 90;
            int minY = -Mathf.CeilToInt(-OffsetY + ry) - 90;
            int maxY = Mathf.CeilToInt(ry) + 50;

            PrepareLayer(groundLayer, minX, minY, maxX, maxY);
            PrepareLayer(airLayer, minX, minY, maxX, maxY);

            // 화면 밖 판단 범위(시전자 발밑 기준 월드 유닛). y는 위쪽이 +이므로 부호를 뒤집는다.
            float centerX = originPixelX + (minX + maxX) * 0.5f;
            float centerY = originPixelY - (minY + maxY) * 0.5f;
            cullingCenter = new Vector3(centerX / PixelsPerUnit, centerY / PixelsPerUnit, transform.position.z);
            cullingSize = new Vector3((maxX - minX) / PixelsPerUnit, (maxY - minY) / PixelsPerUnit, PixelVfxCulling.BoundsDepth);
        }

        private static void PrepareLayer(Layer _layer, int _minX, int _minY, int _maxX, int _maxY)
        {
            _layer.minX = _minX;
            _layer.minY = _minY;
            _layer.width = _maxX - _minX + 1;
            _layer.height = _maxY - _minY + 1;

            int needed = _layer.width * _layer.height;
            if (_layer.cells.Length < needed) _layer.cells = new byte[needed];

            ResetDirty(_layer);
        }

        private static void ResetDirty(Layer _layer)
        {
            _layer.dirtyMinX = int.MaxValue;
            _layer.dirtyMinY = int.MaxValue;
            _layer.dirtyMaxX = int.MinValue;
            _layer.dirtyMaxY = int.MinValue;
        }

        // ---------- 해시 / 배치 ----------

        // 상태 없는 해시. 프레임마다 같은 입력이면 같은 값(0~1)이라 입자가 떨리지 않는다.
        private static float Hash(uint _a, uint _b, uint _c)
        {
            uint v = 2166136261u;
            v = unchecked((v ^ _a) * 16777619u);
            v = unchecked((v ^ _b) * 16777619u);
            v = unchecked((v ^ _c) * 16777619u);
            return v / 4294967295.0f;
        }

        private static float Hash(uint _a, uint _b, uint _c, uint _d)
        {
            uint v = 2166136261u;
            v = unchecked((v ^ _a) * 16777619u);
            v = unchecked((v ^ _b) * 16777619u);
            v = unchecked((v ^ _c) * 16777619u);
            v = unchecked((v ^ _d) * 16777619u);
            return v / 4294967295.0f;
        }

        // 낙하 위치/시각을 시드로 정한다(ComputeImpacts와 Begin이 같은 함수를 쓴다).
        private static void BuildUnits(int _seed, int _count, float _duration, float _radius, Unit[] _units)
        {
            float rangeX = _radius * PixelsPerUnit;
            float rangeY = rangeX * GroundSquashY;
            float span = Mathf.Max(0.5f, _duration - FirstImpactTime - TailMargin);

            // 낙하가 많으면 간격을 줄인다(범위 안에 모두 놓이도록). 거리는 바닥 비율로 환산한 값이다.
            float spacing = Mathf.Min(MinSpacing, 1.55f * rangeX / Mathf.Sqrt(Mathf.Max(1, _count)));

            uint seedU = unchecked((uint)_seed);
            int placed = 0;

            for (int k = 0; k < _count; k++)
            {
                float x = 0.0f;
                float y = 0.0f;
                float currentSpacing = spacing;

                for (int a = 0; a < PlaceAttempts; a++)
                {
                    if (0 < a && 0 == a % 20) currentSpacing *= 0.9f;

                    float ang = Hash(seedU, (uint)k, (uint)a, 1u) * 2.0f * Mathf.PI;
                    float r = Mathf.Sqrt(Hash(seedU, (uint)k, (uint)a, 2u));
                    x = Mathf.Cos(ang) * r * rangeX;
                    y = Mathf.Sin(ang) * r * rangeY;

                    if (Mathf.Abs(x) < 14.0f && Mathf.Abs(y) < 9.0f) continue;

                    bool bFree = true;
                    for (int p = 0; p < placed; p++)
                    {
                        float dx = x - _units[p].x;
                        float dy = (y - _units[p].y) * 2.0f;
                        if (Mathf.Sqrt(dx * dx + dy * dy) <= currentSpacing)
                        {
                            bFree = false;
                            break;
                        }
                    }

                    if (true == bFree) break;
                }

                _units[k].x = Mathf.RoundToInt(x);
                _units[k].y = Mathf.RoundToInt(y);
                float t = FirstImpactTime + span * (k + Hash(seedU, (uint)k, 3u) * 0.8f) / _count;
                _units[k].impactTick = Mathf.RoundToInt(t * TicksPerSecond);
                _units[k].seed = unchecked(seedU * 100u + (uint)k);
                _units[k].bBig = false;
                placed++;
            }
        }

        // ---------- 그리기 도우미 ----------

        private static void Plot(Layer _layer, int _sx, int _sy, byte _tone)
        {
            int gx = _sx - _layer.minX;
            int gy = _sy - _layer.minY;
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

        // 바닥에 눕힌 별(가로 팔이 길고 세로 팔은 절반). 쿼터뷰 바닥면.
        private static void FlatStar(Layer _layer, int _x, int _y, int _ax, int _ay, byte _tone)
        {
            Plot(_layer, _x, _y, _tone);
            for (int k = 1; k <= _ax; k++)
            {
                Plot(_layer, _x + k, _y, _tone);
                Plot(_layer, _x - k, _y, _tone);
            }

            for (int k = 1; k <= _ay; k++)
            {
                Plot(_layer, _x, _y + k, _tone);
                Plot(_layer, _x, _y - k, _tone);
            }
        }

        private static void LinePx(Layer _layer, float _x0, float _y0, float _x1, float _y1, byte _tone, int _w)
        {
            int n = (int)Mathf.Max(Mathf.Abs(_x1 - _x0), Mathf.Abs(_y1 - _y0));
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)Mathf.Max(1, n);
                float x = _x0 + (_x1 - _x0) * t;
                float y = _y0 + (_y1 - _y0) * t;
                P(_layer, x, y, _tone);
                if (2 <= _w) P(_layer, x + 1, y, _tone);
                if (3 <= _w)
                {
                    P(_layer, x - 1, y, _tone);
                    P(_layer, x, y + 1, _tone);
                }
            }
        }

        private static float EaseOutQuart(float _u)
        {
            float u = Mathf.Clamp01(_u);
            float v = 1.0f - u;
            return 1.0f - v * v * v * v;
        }

        // ---------- 낙하 한 개 ----------

        private static void FallPosition(in Unit _m, float _u, out float _x, out float _y)
        {
            float f = 1.0f - Mathf.Pow(Mathf.Max(0.0f, _u), FallCurve);
            _x = _m.x + OffsetX * f;
            _y = _m.y + OffsetY * f;
        }

        private void DrawUnit(in Unit _m, float _ft)
        {
            Layer g = groundLayer;
            Layer a = airLayer;
            int tick = (int)_ft;
            float dt = _ft - _m.impactTick;
            int dti = Mathf.FloorToInt(dt);
            uint s = _m.seed;

            if (-TeleTicks <= dt && dt < 0.0f) DrawTelegraph(g, _m, tick, dt, s);
            if (-FallTicks <= dt && dt < 0.0f) DrawFall(a, _m, tick, dt);
            if (0.0f <= dt) DrawImpact(g, a, _m, tick, dt, dti, s);
        }

        // 예고: 디더 원판 + 점선 고리 + 중심 별 표식(커짐). 불투명 픽셀만 쓴다.
        private static void DrawTelegraph(Layer _g, in Unit _m, int _tick, float _dt, uint _s)
        {
            float u = (_dt + TeleTicks) / TeleTicks;
            float inv = 1.0f - u;
            float rx = 6.0f + 9.0f * (1.0f - inv * inv);
            float ry = rx * 0.5f;

            byte tone = u < 0.5f ? ToneBlue : (u < 0.82f ? ToneIce : ToneAccent);
            if (0.92f < u && 0 == _tick % 2) tone = ToneWhite;
            byte disc = u < 0.5f ? ToneDeep : (u < 0.82f ? ToneBlue : ToneIce);
            int ph = _tick / 2;

            int iy = (int)ry;
            int ix = (int)rx;
            float irx = rx - 1.0f;
            float iry = Mathf.Max(0.5f, ry - 0.5f);

            for (int j = -iy - 1; j <= iy + 1; j++)
            {
                for (int i = -ix - 1; i <= ix + 1; i++)
                {
                    float ex = i / irx;
                    float ey = j / iry;
                    if (ex * ex + ey * ey <= 1.0f && 0 == ((i + j + ph) & 1)) Plot(_g, _m.x + i, _m.y + j, disc);
                }
            }

            const int ringDots = 26;
            float keep = 0.5f + 0.5f * u;
            for (int i = 0; i < ringDots; i++)
            {
                if (keep < Hash(_s, (uint)i, 41u)) continue;
                float ang = 2.0f * Mathf.PI * (i + ph * 0.5f) / ringDots;
                P(_g, _m.x + Mathf.Cos(ang) * rx, _m.y + Mathf.Sin(ang) * ry, tone);
            }

            int arm = 2 + (int)(u * 4.0f);
            if (0 == _tick % 2 || 0.6f < u)
            {
                Plus(_g, _m.x, _m.y, u < 0.8f ? ToneIce : ToneWhite, arm);
                if (0.5f < u) XMark(_g, _m.x, _m.y, ToneBlue);
            }
        }

        // 낙하: 머리가 한 프레임 동안 지나간 길을 굵은 흰 선으로 이어 끊김을 없앤다.
        private void DrawFall(Layer _a, in Unit _m, int _tick, float _dt)
        {
            float u = (_dt + FallTicks) / FallTicks;

            FallPosition(_m, u, out float hx, out float hy);
            FallPosition(_m, u - frameTicks / FallTicks, out float px0, out float py0);
            float sweep = Mathf.Sqrt((hx - px0) * (hx - px0) + (hy - py0) * (hy - py0));

            float prevX = hx;
            float prevY = hy;
            float dist = 0.0f;

            for (int j = 1; ; j++)
            {
                float uj = u - j * 0.012f;
                if (uj < 0.0f) break;

                FallPosition(_m, uj, out float px, out float py);
                dist += Mathf.Sqrt((px - prevX) * (px - prevX) + (py - prevY) * (py - prevY));
                if (sweep + 78.0f < dist) break;

                byte tone;
                int w;
                if (dist <= sweep) { tone = ToneWhite; w = 3; }
                else if (dist <= sweep + 10.0f) { tone = ToneIce; w = 3; }
                else if (dist <= sweep + 28.0f) { tone = ToneBlue; w = 2; }
                else { tone = ToneDeep; w = 1; }

                if (sweep + 40.0f < dist && 0 != (j + _tick) % 2)
                {
                    prevX = px;
                    prevY = py;
                    continue;
                }

                LinePx(_a, prevX, prevY, px, py, tone, w);
                prevX = px;
                prevY = py;

                if ((14 == j || 26 == j || 38 == j) && 0 != (_tick + j) % 3) Plus(_a, px + 4.0f, py - 2.0f, 14 < j ? ToneBlue : ToneAccent, 1);
            }

            DotStar(_a, hx, hy, u < 0.85f ? 6 : 9, ToneWhite, ToneIce);
            P(_a, hx - 2.0f, hy - 2.0f, ToneAccent);
            P(_a, hx + 2.0f, hy - 2.0f, ToneAccent);
            P(_a, hx - 2.0f, hy + 2.0f, ToneAccent);
            P(_a, hx + 2.0f, hy + 2.0f, ToneAccent);
        }

        private void DrawImpact(Layer _g, Layer _a, in Unit _m, int _tick, float _dt, int _dti, uint _s)
        {
            float sc = true == _m.bBig ? 1.4f : 1.0f;

            // 섬광 4단계(24Hz)
            if (_dti < 4)
            {
                int arm = (int)(FlashArms[_dti] * sc);
                DotStar(_a, _m.x, _m.y - 1, arm, ToneWhite, ToneAccent);
            }

            // 짧은 빛기둥 3단계
            if (_dti < 3)
            {
                int hh = (int)(PillarHeights[_dti] * sc);
                for (int yy = 0; yy < hh; yy++)
                {
                    int wd = yy < hh * 0.55f ? 2 : 1;
                    for (int xx = -wd; xx <= wd; xx++)
                    {
                        Plot(_a, _m.x + xx, _m.y - 2 - yy, Mathf.Abs(xx) < wd || yy < 3 ? ToneWhite : ToneIce);
                    }
                }

                Plot(_a, _m.x, _m.y - 3 - hh, ToneAccent);
            }

            // 바닥 방사 줄기: 8방향 같은 길이, 일정한 토막이 같은 속도로 뻗었다가 끝에서 한꺼번에 줄어든다
            if (_dt < 8.0f)
            {
                float u = _dt / 8.0f;
                float len = 24.0f * sc;
                float head = EaseOutQuart(Mathf.Min(1.0f, u / 0.6f)) * len;
                const float seg = 9.0f;
                float tail = u <= 0.6f ? Mathf.Max(0.0f, head - seg) : head - seg * (1.0f - (u - 0.6f) / 0.4f);

                for (int k = 0; k < 8; k++)
                {
                    float ang = k * Mathf.PI * 0.25f;
                    float dx = Mathf.Cos(ang);
                    float dy = Mathf.Sin(ang) * 0.5f;
                    int from = (int)Mathf.Max(0.0f, tail);
                    int to = (int)head;

                    for (int i = from; i <= to; i++)
                    {
                        float d = head - i;
                        byte tone = d < 2.0f ? ToneWhite : (d < 5.0f ? ToneIce : ToneBlue);
                        P(_g, _m.x + dx * i, _m.y + dy * i, tone);
                    }
                }
            }

            // 파편 8개: 45도 간격(줄기 사이), 속도/높이/수명 두 가지 패턴으로 정돈
            for (int i = 0; i < 8; i++)
            {
                float ang = (i + 0.5f) * Mathf.PI * 0.25f;
                bool bFar = 0 == i % 2;
                float spd = bFar ? 84.0f : 52.0f;
                float vz = bFar ? 150.0f : 110.0f;
                int life = bFar ? 12 : 10;
                if (life <= _dt) continue;

                float ts = _dt / TicksPerSecond;
                float gx = _m.x + Mathf.Cos(ang) * spd * ts * sc;
                float gy = _m.y + Mathf.Sin(ang) * spd * ts * 0.5f * sc;
                float z = Mathf.Max(0.0f, vz * ts - 0.5f * 520.0f * ts * ts);
                byte baseTone = bFar ? ToneWhite : ToneIce;
                byte tone = _dt < life * 0.6f ? baseTone : ToneBlue;

                if (life - 2 <= _dt && 0 != _tick % 2) continue;

                if (true == bFar) XMark(_a, gx, gy - z, tone);
                else Plus(_a, gx, gy - z, tone, 1);
            }

            // 불씨 4개(12Hz 점멸, 강조색)
            for (int i = 0; i < 4; i++)
            {
                if (2.0f <= _dt && _dt < 14.0f && 0 == (_tick + i) % 2)
                {
                    int ox = (int)((Hash(_s, (uint)i, 21u) - 0.5f) * 20.0f * sc);
                    float oy = (_dt - 2.0f) * (1.0f + Hash(_s, (uint)i, 22u)) * 1.3f;
                    P(_a, _m.x + ox, _m.y - oy, ToneAccent);
                }
            }

            // 바닥 별 자국: 평평한 별 하나가 깜박이다 입자로 풀린다(총 0.5초)
            if (3.0f <= _dt && _dt < 15.0f)
            {
                float keep = _dt < 8.0f ? 1.0f : Mathf.Max(0.0f, 1.0f - (_dt - 8.0f) / 7.0f);
                int ax = (int)(6.0f * sc);
                const int ay = 2;
                int cellCount = 1 + 2 * ax + 2 * ay;

                for (int idx = 0; idx < cellCount; idx++)
                {
                    ScarCell(idx, ax, out int cx, out int cy);

                    if (keep < Hash(_s, (uint)idx, 31u))
                    {
                        if (8.0f <= _dt && Hash(_s, (uint)idx, 32u) < 0.5f)
                        {
                            float rise = (_dt - 8.0f) * (1.0f + Hash(_s, (uint)idx, 33u) * 1.5f);
                            int shape = (int)(Hash(_s, (uint)idx, 34u) * 3.0f);
                            float px = _m.x + cx;
                            float py = _m.y + cy - rise;

                            if (0 == shape) P(_g, px, py, ToneBlue);
                            else if (1 == shape) Plus(_g, px, py, ToneBlue, 1);
                            else XMark(_g, px, py, ToneBlue);
                        }

                        continue;
                    }

                    if (2 == (_dti / 2) % 3 && _dt < 8.0f) continue;
                    Plot(_g, _m.x + cx, _m.y + cy, Mathf.Abs(cx) <= 1 ? ToneIce : ToneBlue);
                }
            }
        }

        // 별 자국 칸 목록(가로 팔 -> 세로 팔)의 _idx번째 칸
        private static void ScarCell(int _idx, int _ax, out int _cx, out int _cy)
        {
            _cx = 0;
            _cy = 0;
            if (0 == _idx) return;

            if (_idx <= 2 * _ax)
            {
                int k = (_idx + 1) / 2;
                _cx = 1 == _idx % 2 ? k : -k;
                return;
            }

            int j = _idx - 2 * _ax;
            int kk = (j + 1) / 2;
            _cy = 1 == j % 2 ? kk : -kk;
        }

        // 발동: 발밑 별 섬광 -> 별 6개가 나선으로 솟아오른다
        private void DrawCall(float _ft)
        {
            Layer g = groundLayer;
            Layer a = airLayer;
            int tick = (int)_ft;

            if (tick < 4)
            {
                FlatStar(g, 0, 1, CallArmX[tick], CallArmY[tick], tick < 2 ? ToneWhite : ToneIce);
            }

            for (int i = 0; i < 6; i++)
            {
                float tt = _ft - (1 + i);
                if (tt < 0.0f || 14.0f < tt) continue;

                float u = tt / 14.0f;
                float ang = i * 2.2f + u * 3.2f;
                float x = Mathf.Cos(ang) * 11.0f * (1.0f - 0.4f * u);
                float y = -6.0f - u * u * 118.0f + Mathf.Sin(ang) * 3.0f;
                byte tone = u < 0.5f ? ToneWhite : ToneIce;

                int shape = i % 3;
                if (0 == shape) Plus(a, x, y, tone, 1);
                else if (1 == shape) XMark(a, x, y, tone);
                else
                {
                    P(a, x, y, tone);
                    P(a, x, y + 1.0f, tone);
                }

                if (1.0f < tt)
                {
                    P(a, x, y + 3.0f, ToneBlue);
                    P(a, x, y + 6.0f, ToneDeep);
                }
            }
        }

        // ---------- 한 프레임 ----------

        private void RebuildMesh()
        {
            float ft = elapsed * TicksPerSecond;
            Vector3 pos = transform.position;

            DrawCall(ft);
            for (int i = 0; i < unitCount; i++)
            {
                DrawUnit(units[i], ft);
            }

            bool bGround = null != groundMesh;

            Layer skyTarget = airLayer;
            Layer groundTarget = true == bGround ? groundLayer : airLayer;

            airLayer.buffer.Clear();
            airLayer.buffer.SetWorldTransform(transform.worldToLocalMatrix, pos.z);
            ResetWorldBounds(airLayer);

            if (true == bGround)
            {
                groundLayer.buffer.Clear();
                groundLayer.buffer.SetWorldTransform(groundMeshRenderer.transform.worldToLocalMatrix, pos.z);
                ResetWorldBounds(groundLayer);
            }

            // 바닥 메쉬가 없으면 모두 공중 메쉬 하나로 그린다(폴백). 바닥을 먼저 넣어 나중에 그리는 공중이 위에 오게 한다.
            EmitLayer(groundLayer, groundTarget.buffer, quadBudget);
            EmitLayer(airLayer, skyTarget.buffer, quadBudget);

            UploadMesh(airLayer.buffer, airMesh, meshRenderer, transform.position, airLayer, ref airUploadedVertexCount, ref airUploadedHash);

            if (true == bGround)
            {
                UploadMesh(groundLayer.buffer, groundMesh, groundMeshRenderer, groundMeshRenderer.transform.position, groundLayer, ref groundUploadedVertexCount, ref groundUploadedHash);
            }
        }

        private static void ResetWorldBounds(Layer _layer)
        {
            _layer.worldMinX = int.MaxValue;
            _layer.worldMinY = int.MaxValue;
            _layer.worldMaxX = int.MinValue;
            _layer.worldMaxY = int.MinValue;
        }

        // 격자에 모은 톤을 가로로 이어진 같은 톤끼리 쿼드 하나로 합쳐 버퍼에 넣고, 격자를 비운다.
        private void EmitLayer(Layer _layer, PixelQuadBuffer _buffer, int _budget)
        {
            if (_layer.dirtyMaxY < _layer.dirtyMinY)
            {
                return;
            }

            int width = _layer.width;
            Layer boundsOwner = _buffer == groundLayer.buffer ? groundLayer : airLayer;

            for (int gy = _layer.dirtyMinY; gy <= _layer.dirtyMaxY; gy++)
            {
                int row = gy * width;
                int x = _layer.dirtyMinX;

                while (x <= _layer.dirtyMaxX)
                {
                    byte tone = _layer.cells[row + x];
                    if (ToneNone == tone)
                    {
                        x++;
                        continue;
                    }

                    int start = x;
                    while (x + 1 <= _layer.dirtyMaxX && tone == _layer.cells[row + x + 1]) x++;
                    int end = x;

                    for (int k = start; k <= end; k++) _layer.cells[row + k] = ToneNone;

                    x++;

                    if (_budget * 4 <= _buffer.VertexCount) continue;

                    int wx0 = originPixelX + _layer.minX + start;
                    int wx1 = originPixelX + _layer.minX + end + 1;
                    int wy = originPixelY - (_layer.minY + gy);
                    _buffer.AddWorldRect(wx0, wy, wx1, wy + 1, toneColors[tone]);

                    if (wx0 < boundsOwner.worldMinX) boundsOwner.worldMinX = wx0;
                    if (wy < boundsOwner.worldMinY) boundsOwner.worldMinY = wy;
                    if (boundsOwner.worldMaxX < wx1) boundsOwner.worldMaxX = wx1;
                    if (boundsOwner.worldMaxY < wy + 1) boundsOwner.worldMaxY = wy + 1;
                }
            }

            ResetDirty(_layer);
        }

        private static void UploadMesh(PixelQuadBuffer _buffer, Mesh _mesh, MeshRenderer _renderer, Vector3 _objectPosition, Layer _boundsSource, ref int _uploadedVertexCount, ref ulong _uploadedHash)
        {
            if (0 == _buffer.VertexCount)
            {
                if (true == _renderer.enabled) _renderer.enabled = false;
                _uploadedVertexCount = -1;
                return;
            }

            if (_buffer.VertexCount != _uploadedVertexCount || _buffer.ContentHash != _uploadedHash)
            {
                _buffer.Upload(_mesh, 0);
                _uploadedVertexCount = _buffer.VertexCount;
                _uploadedHash = _buffer.ContentHash;

                float minX = _boundsSource.worldMinX / PixelsPerUnit - _objectPosition.x;
                float minY = _boundsSource.worldMinY / PixelsPerUnit - _objectPosition.y;
                float maxX = _boundsSource.worldMaxX / PixelsPerUnit - _objectPosition.x;
                float maxY = _boundsSource.worldMaxY / PixelsPerUnit - _objectPosition.y;

                Bounds bounds = new Bounds(
                    new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0.0f),
                    new Vector3(maxX - minX + 0.1f, maxY - minY + 0.1f, PixelVfxCulling.BoundsDepth));

                Bounds current = _mesh.bounds;
                if (current.center != bounds.center || current.size != bounds.size) _mesh.bounds = bounds;
            }

            if (false == _renderer.enabled) _renderer.enabled = true;
        }

        private void HideAll()
        {
            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != groundMeshRenderer) groundMeshRenderer.enabled = false;
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            appliedRootSortingLayerID = int.MinValue;
            appliedRootSortingOrder = int.MinValue;
            bBeginPending = true;
            elapsed = 0.0f;
            HideAll();
        }

        private void LateUpdate()
        {
            if (true == bBeginPending)
            {
                bBeginPending = false;
                Begin();
            }

            if (totalLength <= elapsed)
            {
                HideAll();
                return;
            }

            // 첫 프레임은 시간을 더하지 않아서 t=0 장면이 반드시 한 번 그려진다
            float dt = true == bFirstFrame ? 0.0f : Time.deltaTime;
            bFirstFrame = false;
            elapsed += dt;
            frameTicks = Mathf.Clamp(dt * TicksPerSecond, 0.25f, 1.5f);

            // 소팅은 VFXComponent가 자식 렌더러 전체에 같은 값으로 덮어쓰므로, 루트 렌더러 기준으로 다시 맞춘다
            if (null != rootParticleRenderer)
            {
                int rootLayer = rootParticleRenderer.sortingLayerID;
                int rootOrder = rootParticleRenderer.sortingOrder;
                if (rootLayer != appliedRootSortingLayerID || rootOrder != appliedRootSortingOrder)
                {
                    appliedRootSortingLayerID = rootLayer;
                    appliedRootSortingOrder = rootOrder;
                    meshRenderer.sortingLayerID = rootLayer;
                    meshRenderer.sortingOrder = rootOrder + sortingOrderOffset;
                }
            }

            if (null != groundMesh)
            {
                if (groundMeshRenderer.sortingLayerID != groundSortingLayerId) groundMeshRenderer.sortingLayerID = groundSortingLayerId;
                if (groundMeshRenderer.sortingOrder != groundSortingOrder) groundMeshRenderer.sortingOrder = groundSortingOrder;
            }

            float margin = 2.0f * PixelVfxCulling.MarginUnits;
            Vector3 size = new Vector3(cullingSize.x + margin, cullingSize.y + margin, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(cullingCenter, size))
            {
                HideAll();
                return;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            bPlaySet = false;
            HideAll();
        }

        private void OnDestroy()
        {
            if (null != airMesh)
            {
                Destroy(airMesh);
                airMesh = null;
            }

            if (null != groundMesh)
            {
                Destroy(groundMesh);
                groundMesh = null;
            }
        }

        private void OnValidate()
        {
            defaultDuration = Mathf.Clamp(defaultDuration, 2.0f, 8.0f);
            defaultRadius = Mathf.Clamp(defaultRadius, 1.0f, 6.0f);
            quadBudget = Mathf.Clamp(quadBudget, 400, 6000);
        }
    }
}
