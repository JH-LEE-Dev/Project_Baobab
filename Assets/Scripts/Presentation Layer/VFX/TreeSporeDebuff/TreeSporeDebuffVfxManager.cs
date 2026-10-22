using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 포자 대폭발에 맞아 받는 피해가 늘어난(증폭) 나무의 상시 디버프 이펙트를 한곳에서 그리는 관리자. 씬에 하나만 있고 첫 Apply 때 프리팹에서 만들어진다.
    ///
    /// 구성(나무가 많아져도 비용이 늘지 않게 한곳으로 모은다)
    /// - 나무 오버레이(나무 Top의 자식 SpriteRenderer 1개): 어두운 체커 디더, 각진 얼룩, 줄무늬 몸통, 균열 선을 미리 구운 스프라이트. 나무 색은 바꾸지 않는다.
    /// - 바닥 링(스프라이트 1개) + 공유 바닥 메쉬: 점선 링은 구운 스프라이트, 링을 따라 도는 밝은 구간과 바닥 싹은 공유 메쉬.
    /// - 공유 상단 메쉬: 아래를 가리키는 표식(단계 수만큼), 맺혔다 떨어지는 독액, 떨어지는 부패 조각, 줄무늬/균열의 흐르는 빛, 타격 반응.
    ///
    /// 비용 관리
    /// - 모든 갱신은 24Hz 틱에서만 한다(틱이 같고 변화가 없으면 아무것도 하지 않는다).
    /// - 화면 안(+여백) 나무만 그리고, 카메라 중심에 가까운 순으로 maxFullTrees 그루만 전체 연출, 나머지는 링과 표식만 그린다.
    /// - 위치는 틱 시각과 나무별 해시만으로 정하는 닫힌 식이라 입자 목록이 없다. 재생 중 힙 할당이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class TreeSporeDebuffVfxManager : MonoBehaviour
    {
        private const string ResourcePath = "TreeSporeDebuff/TreeSporeDebuffVfxManager";
        private const float PixelsPerUnit = 32.0f;
        private const float TickRate = 24.0f;
        private const int MaxEntries = 128;
        private const int QuadBudget = 6000;

        private const float FallGravity = 360.0f;
        private const int FlakeSlotsMax = 7;

        private static readonly int[] ZigZag = { 0, 1, 1, 0, -1, -1 };
        private static readonly int[] DirX = { -1, 0, 1, -1, 1, -1, 0, 1 };
        private static readonly int[] DirY = { 1, 1, 1, 0, 0, -1, -1, -1 };
        private static readonly int[] SproutSlots = { 0, 3, 5, 7 };
        private static readonly int[] FlakeSlots = { 0, 3, 5, 7 };

        private static readonly PixelQuadBuffer groundBuffer = new PixelQuadBuffer(QuadBudget / 3);
        private static readonly PixelQuadBuffer topBuffer = new PixelQuadBuffer(QuadBudget);

        private sealed class Entry
        {
            public TreeObj tree;
            public SpriteRenderer topSprite;
            public Sprite lastSprite;
            public int tier;
            public int variant;
            public uint seed;
            public TreeSilhouetteData mask;
            public TreeDebuffLayout layout;
            public SpriteRenderer overlay;
            public SpriteRenderer ground;
            public float hitTime = -100.0f;
            public int hitSequence;
            public int baseX;
            public int baseY;
            public bool bVisible;
            public bool bFull;
            public float distance;
        }

        public static TreeSporeDebuffVfxManager Instance { get; private set; }

        // 외부 의존성
        [Header("렌더")]
        [SerializeField] private MeshFilter groundMeshFilter;
        [SerializeField] private MeshRenderer groundMeshRenderer;
        [SerializeField] private MeshFilter topMeshFilter;
        [SerializeField] private MeshRenderer topMeshRenderer;
        [SerializeField, Tooltip("오버레이/바닥 링 스프라이트용 머티리얼(무조명 스프라이트)")]
        private Material spriteMaterial;

        [Header("정렬")]
        [SerializeField] private string groundSortingLayerName = "ShockWave";
        [SerializeField] private int groundSpriteOrder = 0;
        [SerializeField] private int groundMeshOrder = 1;
        [SerializeField] private string topSortingLayerName = "FlyingItem";
        [SerializeField] private int topMeshOrder = 12;

        [Header("예산")]
        [SerializeField, Tooltip("전체 연출을 그리는 최대 나무 수(카메라 중심에 가까운 순). 나머지 화면 안 나무는 링과 표식만 그린다.")]
        private int maxFullTrees = 16;
        [SerializeField, Tooltip("화면 밖 판정에 더하는 여백(월드 유닛)")]
        private float cullMargin = 4.0f;

        // 내부 의존성
        private readonly List<Entry> entries = new List<Entry>(32);
        private readonly Stack<SpriteRenderer> overlayPool = new Stack<SpriteRenderer>(16);
        private readonly Stack<SpriteRenderer> groundPool = new Stack<SpriteRenderer>(16);
        private Mesh groundMesh;
        private Mesh topMesh;
        private int groundLayerId;
        private int topLayerId;
        private Color32[] lightColors;
        private Color32[] midColors;
        private Color32[] deepColors;
        private Color32 outlineColor;

        // 상태 변수
        private int lastTick = -1;
        private bool bDirty;
        private bool bWarnedMissing;

        private PixelQuadBuffer activeBuffer;
        private int activeBounds;
        private bool bRunActive;
        private int runX0;
        private int runX1;
        private int runY;
        private Color32 runColor;
        private readonly int[] boundsMinX = new int[2];
        private readonly int[] boundsMinY = new int[2];
        private readonly int[] boundsMaxX = new int[2];
        private readonly int[] boundsMaxY = new int[2];

        // 퍼블릭 초기화 및 제어 메서드

        public static TreeSporeDebuffVfxManager GetOrCreate()
        {
            if (null != Instance) return Instance;

            GameObject prefab = Resources.Load<GameObject>(ResourcePath);
            if (null == prefab)
            {
                Debug.LogError("[TreeSporeDebuffVfxManager] 프리팹을 찾지 못했습니다: Resources/" + ResourcePath);
                return null;
            }

            GameObject go = Instantiate(prefab);
            go.name = "TreeSporeDebuffVfxManager";
            return Instance;
        }

        public void Register(TreeObj _tree, int _tier)
        {
            if (null == _tree) return;

            Entry existing = Find(_tree);
            if (null != existing)
            {
                if (existing.tier < _tier)
                {
                    existing.tier = _tier;
                    existing.lastSprite = null; // 다음 틱에 새 단계 배치/스프라이트로 다시 만든다
                    bDirty = true;
                }

                return;
            }

            if (MaxEntries <= entries.Count) return;

            SpriteRenderer top = FindTopRenderer(_tree);
            if (null == top)
            {
                if (false == bWarnedMissing)
                {
                    bWarnedMissing = true;
                    Debug.LogWarning("[TreeSporeDebuffVfxManager] 나무의 BaseVisual/Top 렌더러를 찾지 못해 이펙트를 건너뜁니다: " + _tree.name);
                }

                return;
            }

            Entry e = new Entry();
            e.tree = _tree;
            e.topSprite = top;
            e.tier = Mathf.Clamp(_tier, 1, 3);
            uint h = Hash(unchecked((uint)_tree.GetHashCode()));
            e.seed = h;
            e.variant = (int)(h & 1u);
            entries.Add(e);
            bDirty = true;
        }

        public void Unregister(TreeObj _tree)
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                if (_tree == entries[i].tree)
                {
                    ReleaseEntry(entries[i]);
                    entries.RemoveAt(i);
                    bDirty = true;
                    return;
                }
            }
        }

        public void NotifyHit(TreeObj _tree)
        {
            Entry e = Find(_tree);
            if (null == e) return;

            e.hitTime = Mathf.Floor(Time.time * TickRate) / TickRate;
            e.hitSequence++;
            bDirty = true;
        }

        // 일반 비즈니스 로직 및 내부 메서드

        private Entry Find(TreeObj _tree)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (_tree == entries[i].tree) return entries[i];
            }

            return null;
        }

        private static SpriteRenderer FindTopRenderer(TreeObj _tree)
        {
            TreeVisualComponent visual = _tree.treeVisualComponent;
            if (null == visual || null == visual.baseVisualObj) return null;

            Transform top = visual.baseVisualObj.transform.Find("Top");
            return null != top ? top.GetComponent<SpriteRenderer>() : null;
        }

        private static uint Hash(uint _x)
        {
            unchecked
            {
                _x ^= _x >> 16;
                _x *= 0x7feb352dU;
                _x ^= _x >> 15;
                _x *= 0x846ca68bU;
                _x ^= _x >> 16;
                return _x;
            }
        }

        private void ReleaseEntry(Entry _e)
        {
            if (null != _e.overlay)
            {
                _e.overlay.enabled = false;
                _e.overlay.transform.SetParent(transform, false);
                overlayPool.Push(_e.overlay);
                _e.overlay = null;
            }

            if (null != _e.ground)
            {
                _e.ground.enabled = false;
                groundPool.Push(_e.ground);
                _e.ground = null;
            }
        }

        private SpriteRenderer GetRenderer(Stack<SpriteRenderer> _pool, string _name)
        {
            if (0 < _pool.Count) return _pool.Pop();

            GameObject go = new GameObject(_name, typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.sharedMaterial = spriteMaterial;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;
            sr.enabled = false;
            return sr;
        }

        private void CleanupDead()
        {
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                Entry e = entries[i];
                if (null == e.tree || false == e.tree.isActiveAndEnabled || null == e.topSprite)
                {
                    ReleaseEntry(e);
                    entries.RemoveAt(i);
                }
            }
        }

        private void CollectVisible()
        {
            Camera cam = Camera.main;
            bool bHasCam = null != cam;
            Vector3 c = bHasCam ? cam.transform.position : Vector3.zero;
            float hh = bHasCam ? cam.orthographicSize + cullMargin : 0.0f;
            float hw = bHasCam ? cam.orthographicSize * cam.aspect + cullMargin : 0.0f;

            int visibleCount = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                Vector3 p = e.tree.transform.position;
                float dx = p.x - c.x;
                float dy = p.y - c.y;
                e.bVisible = false == bHasCam || (Mathf.Abs(dx) <= hw && Mathf.Abs(dy) <= hh);
                e.bFull = false;
                e.distance = dx * dx + dy * dy;
                if (true == e.bVisible) visibleCount++;
            }

            // 카메라 중심에 가까운 순으로 maxFullTrees 그루를 전체 연출로 고른다(작은 N이라 선택 정렬로 충분하다)
            int picks = Mathf.Min(visibleCount, Mathf.Max(0, maxFullTrees));
            for (int k = 0; k < picks; k++)
            {
                Entry best = null;
                for (int i = 0; i < entries.Count; i++)
                {
                    Entry e = entries[i];
                    if (false == e.bVisible || true == e.bFull) continue;
                    if (null == best || e.distance < best.distance) best = e;
                }

                if (null != best) best.bFull = true;
            }
        }

        private void ApplyRenderers()
        {
            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                Sprite sprite = e.topSprite.sprite;

                if (false == e.bVisible || null == sprite)
                {
                    if (null != e.overlay) e.overlay.enabled = false;
                    if (null != e.ground) e.ground.enabled = false;
                    continue;
                }

                if (sprite != e.lastSprite)
                {
                    e.lastSprite = sprite;
                    e.mask = TreeSilhouetteMask.Get(sprite);
                    e.layout = TreeDebuffLayout.Get(sprite, e.mask, e.tier, e.variant);
                    if (null != e.overlay) e.overlay.sprite = null;
                }

                Bounds b = e.topSprite.bounds;
                e.baseX = Mathf.RoundToInt(b.min.x * PixelsPerUnit);
                e.baseY = Mathf.RoundToInt(b.min.y * PixelsPerUnit);

                int gx = e.baseX + Mathf.RoundToInt(e.mask.pivot.x);
                int gy = e.baseY + Mathf.RoundToInt(e.mask.pivot.y);

                if (null == e.ground)
                {
                    e.ground = GetRenderer(groundPool, "DebuffGround");
                    e.ground.sortingLayerID = groundLayerId;
                    e.ground.sortingOrder = groundSpriteOrder;
                }

                e.ground.sprite = TreeDebuffOverlayBaker.GetGround(e.tier);
                e.ground.transform.position = new Vector3(gx / PixelsPerUnit, gy / PixelsPerUnit, e.tree.transform.position.z);
                e.ground.enabled = true;

                if (true == e.bFull)
                {
                    if (null == e.overlay)
                    {
                        e.overlay = GetRenderer(overlayPool, "DebuffOverlay");
                        e.overlay.transform.SetParent(e.topSprite.transform, false);
                        e.overlay.transform.localPosition = Vector3.zero;
                        e.overlay.transform.localRotation = Quaternion.identity;
                        e.overlay.transform.localScale = Vector3.one;
                    }

                    if (null == e.overlay.sprite)
                    {
                        e.overlay.sprite = TreeDebuffOverlayBaker.GetOverlay(sprite, e.mask, e.layout, e.tier, e.variant);
                    }

                    e.overlay.sortingLayerID = e.topSprite.sortingLayerID;
                    e.overlay.sortingOrder = e.tree.treeVisualComponent.GetTopHighlightSortingOrder() + 2;
                    e.overlay.enabled = true;
                }
                else if (null != e.overlay)
                {
                    e.overlay.enabled = false;
                }
            }
        }

        // ---------- 그리기 도우미(런 병합) ----------

        private void SetActive(PixelQuadBuffer _buffer, int _boundsIndex)
        {
            FlushRun();
            activeBuffer = _buffer;
            activeBounds = _boundsIndex;
        }

        private void FlushRun()
        {
            if (false == bRunActive) return;

            bRunActive = false;
            activeBuffer.AddWorldRect(runX0, runY, runX1, runY + 1, runColor);

            int b = activeBounds;
            if (runX0 < boundsMinX[b]) boundsMinX[b] = runX0;
            if (runY < boundsMinY[b]) boundsMinY[b] = runY;
            if (boundsMaxX[b] < runX1) boundsMaxX[b] = runX1;
            if (boundsMaxY[b] < runY + 1) boundsMaxY[b] = runY + 1;
        }

        private static bool SameColor(Color32 _a, Color32 _b)
        {
            return _a.r == _b.r && _a.g == _b.g && _a.b == _b.b && _a.a == _b.a;
        }

        private void Dot(int _x, int _y, Color32 _color)
        {
            if (true == bRunActive && runY == _y && runX1 == _x && true == SameColor(runColor, _color))
            {
                runX1 = _x + 1;
                return;
            }

            FlushRun();
            bRunActive = true;
            runX0 = _x;
            runX1 = _x + 1;
            runY = _y;
            runColor = _color;
        }

        private void Plus(int _x, int _y, Color32 _color)
        {
            Dot(_x, _y, _color);
            Dot(_x + 1, _y, _color);
            Dot(_x - 1, _y, _color);
            Dot(_x, _y + 1, _color);
            Dot(_x, _y - 1, _color);
        }

        private void Cross(int _x, int _y, Color32 _color)
        {
            Dot(_x, _y, _color);
            Dot(_x + 1, _y + 1, _color);
            Dot(_x - 1, _y - 1, _color);
            Dot(_x + 1, _y - 1, _color);
            Dot(_x - 1, _y + 1, _color);
        }

        // ---------- 나무별 그리기 ----------

        private void DrawEntry(Entry _e, float _t)
        {
            int tier = _e.tier;
            Color32 light = lightColors[tier];
            Color32 mid = midColors[tier];
            Color32 deep = deepColors[tier];
            TreeDebuffLayout layout = _e.layout;
            int gx = _e.baseX + Mathf.RoundToInt(_e.mask.pivot.x);
            int gy = _e.baseY + Mathf.RoundToInt(_e.mask.pivot.y);

            // ---- 바닥 메쉬: 링을 따라 도는 밝은 구간, 싹 ----
            SetActive(groundBuffer, 0);

            int ringCount = TreeDebuffRing.PointCount(tier);
            int head = (int)(_t * 20.0f) % ringCount;
            for (int j = 0; j < 6; j++)
            {
                int idx = ((head - j) % ringCount + ringCount) % ringCount;
                if (true == TreeDebuffRing.IsGap(idx)) continue;
                Dot(gx + TreeDebuffRing.X(tier, idx), gy + TreeDebuffRing.Y(tier, idx), light);
            }

            if (true == _e.bFull)
            {
                int rx = TreeDebuffRing.RadiusX[tier];
                int ry = TreeDebuffRing.RadiusY(tier);
                int slots = SproutSlots[tier];
                for (int s = 0; s < slots; s++)
                {
                    uint h0 = Hash(_e.seed + (uint)(s * 7919));
                    float period = 1.0f + (h0 % 7u) * 0.1f;
                    float offset = ((h0 >> 8) % 100u) / 100.0f * period;
                    float shifted = _t + offset;
                    int cycle = (int)(shifted / period);
                    int frame = (int)((shifted - cycle * period) * 12.0f);
                    if (frame < 1 || 5 < frame) continue;

                    uint h = Hash(_e.seed + (uint)(s * 131) + (uint)(cycle * 977));
                    float a = (h & 1023u) / 1023.0f * Mathf.PI * 2.0f;
                    float r = Mathf.Sqrt(((h >> 10) & 255u) / 255.0f) * 0.8f;
                    int x = gx + (int)(Mathf.Cos(a) * rx * r);
                    int y = gy + (int)(Mathf.Sin(a) * ry * r);

                    if (1 == frame) Dot(x, y, mid);
                    else if (2 == frame) { Plus(x, y, mid); Dot(x, y, light); }
                    else if (3 == frame) Cross(x, y, light);
                    else if (4 == frame) { Plus(x, y, mid); Dot(x, y, deep); }
                    else Dot(x, y, deep);
                }
            }

            // ---- 상단 메쉬 ----
            SetActive(topBuffer, 1);

            float hitAge = _t - _e.hitTime;
            int hitFrame = (0.0f <= hitAge && hitAge < 0.8f) ? (int)(hitAge * 24.0f) : -1;

            if (true == _e.bFull)
            {
                // 줄무늬를 따라 흘러내리는 빛
                for (int s = 0; s < layout.streakCount; s++)
                {
                    int len = layout.streakLength[s];
                    int fl = (int)(_t * 12.0f) % (len + 5);
                    if (len <= fl) continue;
                    int sx = layout.streakX[s];
                    int sy = layout.streakTopY[s] - fl;
                    if (true == _e.mask.IsSolid(sx, sy)) Dot(_e.baseX + sx, _e.baseY + sy, light);
                }

                // 균열: 빛이 지나가고, 타격 직후에는 전체가 번쩍인다
                for (int c = 0; c < layout.crackCount; c++)
                {
                    int count = layout.crackPixelCount[c];
                    if (0 == count) continue;

                    if (0 <= hitFrame && hitFrame < 4)
                    {
                        for (int p = 0; p < count; p++) Dot(_e.baseX + layout.crackX[c, p], _e.baseY + layout.crackY[c, p], light);
                    }
                    else
                    {
                        int hi = ((int)(_t * 21.6f) + c * 5) % (count + 8);
                        if (hi < count) Dot(_e.baseX + layout.crackX[c, hi], _e.baseY + layout.crackY[c, hi], light);
                    }
                }

                DrawDrips(_e, _t, gy, light, mid, deep);
                DrawFlakes(_e, _t, mid, deep);

                if (0 <= hitFrame) DrawBurst(_e, hitAge, light, mid, deep);
            }

            DrawMark(_e, _t, hitFrame, light, mid);
        }

        private void DrawDrips(Entry _e, float _t, int _groundY, Color32 _light, Color32 _mid, Color32 _deep)
        {
            TreeDebuffLayout layout = _e.layout;
            for (int d = 0; d < layout.dripCount; d++)
            {
                uint h = Hash(_e.seed + (uint)(d * 4241));
                float period = 1.4f + (h % 6u) * 0.1f;
                float offset = ((h >> 8) % 100u) / 100.0f * period;
                float p = (_t + offset) % period;
                int x = _e.baseX + layout.dripX[d];
                int y0 = _e.baseY + layout.dripY[d];
                int yl = _groundY + (int)((h >> 16) % 4u) - 1;

                if (p < 0.2f)
                {
                    int len = (int)(p / 0.2f * 3.0f) + 1;
                    for (int k = 0; k <= len; k++) Dot(x, y0 - k, k == len ? _light : _mid);
                    continue;
                }

                float f = p - 0.2f;
                float dist = (y0 - 3) - yl;
                if (dist <= 0.0f) continue;

                float tLand = Mathf.Sqrt(2.0f * dist / FallGravity);
                if (f < tLand)
                {
                    int y = Mathf.RoundToInt(y0 - 3 - 0.5f * FallGravity * f * f);
                    Dot(x, y, _light);
                    Dot(x, y + 1, _mid);
                    Dot(x, y + 3, _deep);
                }
                else
                {
                    int s = (int)((f - tLand) * 12.0f);
                    if (0 == s)
                    {
                        for (int k = -2; k <= 2; k++) Dot(x + k, yl, 2 == Mathf.Abs(k) ? _mid : _light);
                        Dot(x, yl + 1, _light);
                    }
                    else if (1 == s)
                    {
                        Cross(x - 3, yl + 1, _mid);
                        Cross(x + 3, yl + 1, _mid);
                    }
                    else if (2 == s)
                    {
                        Dot(x - 4, yl, _deep);
                        Dot(x + 4, yl, _deep);
                        Dot(x - 2, yl + 2, _mid);
                        Dot(x + 2, yl + 2, _mid);
                    }
                }
            }
        }

        private void DrawFlakes(Entry _e, float _t, Color32 _mid, Color32 _deep)
        {
            TreeDebuffLayout layout = _e.layout;
            if (layout.flakeSpotCount <= 0) return;

            int slots = Mathf.Min(FlakeSlotsMax, FlakeSlots[_e.tier]);
            for (int s = 0; s < slots; s++)
            {
                uint h0 = Hash(_e.seed + (uint)(s * 31) + 17u);
                float period = 1.8f + (h0 % 7u) * 0.1f;
                float offset = ((h0 >> 6) % 100u) / 100.0f * period;
                float shifted = _t + offset;
                int cycle = (int)(shifted / period);
                float a = shifted - cycle * period;
                if (1.8f <= a) continue;

                uint h = Hash(_e.seed + (uint)(s * 977) + (uint)(cycle * 131));
                int spot = (int)(h % (uint)layout.flakeSpotCount);
                int x = _e.baseX + layout.flakeX[spot] + ZigZag[((int)(a * 7.0f) + (int)((h >> 5) % 6u)) % 6];
                int y = _e.baseY + layout.flakeY[spot] - 2 - (int)(a * 10.0f);

                Dot(x, y, a < 1.1f ? _mid : _deep);
                if (0u != ((h >> 9) & 1u)) Dot(x, y + 1, _deep);
            }
        }

        private void DrawBurst(Entry _e, float _age, Color32 _light, Color32 _mid, Color32 _deep)
        {
            TreeDebuffLayout layout = _e.layout;
            if (layout.flakeSpotCount <= 0) return;

            for (int i = 0; i < 12; i++)
            {
                uint h = Hash(_e.seed + (uint)(_e.hitSequence * 131) + (uint)(i * 17));
                float a = _age - ((h >> 14) % 3u) * 0.03f;
                if (a < 0.0f || 0.8f <= a) continue;

                int spot = (int)(h % (uint)layout.flakeSpotCount);
                int dir = (int)((h >> 8) & 7u);
                int dist = Mathf.RoundToInt(6.0f * (1.0f - Mathf.Exp(-a * 9.0f)));
                int grav = Mathf.RoundToInt(9.0f * (a / 0.8f) * (a / 0.8f));
                int x = _e.baseX + layout.flakeX[spot] + DirX[dir] * dist;
                int y = _e.baseY + layout.flakeY[spot] + DirY[dir] * dist - grav;
                Color32 c = a < 0.1f ? _light : (a < 0.4f ? _mid : _deep);

                bool bBig = 0u == ((h >> 12) & 3u) && a < 0.4f;
                if (true == bBig)
                {
                    if (0 != ((int)(a * 24.0f) & 1)) Cross(x, y, c);
                    else Plus(x, y, c);
                }
                else
                {
                    Dot(x, y, c);
                }
            }
        }

        // 아래를 가리키는 표식(칸 수 = 단계). 타격하면 아래로 내리꽂힌다.
        private void DrawMark(Entry _e, float _t, int _hitFrame, Color32 _light, Color32 _mid)
        {
            int n = _e.tier;
            int slam = (0 <= _hitFrame && _hitFrame < 2) ? 3 : ((0 <= _hitFrame && _hitFrame < 4) ? 1 : 0);
            int bob = 2 <= ((int)(_t * 5.0f) & 3) ? 1 : 0;   // 0, 0, 1, 1: 한 칸 단위로 위아래
            int x = _e.baseX + _e.layout.centerX;
            int baseY = _e.baseY + _e.layout.topY + 9;

            for (int i = 0; i < n; i++)
            {
                bool bAct = (int)(_t * 6.0f) % (n + 2) == i;
                Color32 c = true == bAct ? _light : _mid;
                int y = baseY + (n - 1 - i) * 5 - bob - slam;

                for (int k = 0; k < 4; k++)
                {
                    int a = -3 + k;
                    int b = 3 - k;
                    Dot(x + a - 1, y - k, outlineColor);
                    Dot(x + a + 1, y - k, outlineColor);
                    Dot(x + b - 1, y - k, outlineColor);
                    Dot(x + b + 1, y - k, outlineColor);
                    Dot(x + a, y - k + 1, outlineColor);
                    Dot(x + b, y - k + 1, outlineColor);
                    Dot(x + a, y - k - 1, outlineColor);
                    Dot(x + b, y - k - 1, outlineColor);
                }

                for (int k = 0; k < 4; k++)
                {
                    Dot(x - 3 + k, y - k, c);
                    Dot(x + 3 - k, y - k, c);
                }
            }
        }

        private void RebuildMeshes(int _tick)
        {
            float t = _tick / TickRate;

            groundBuffer.Clear();
            groundBuffer.SetWorldTransform(Matrix4x4.identity, 0.0f);
            topBuffer.Clear();
            topBuffer.SetWorldTransform(Matrix4x4.identity, 0.0f);

            bRunActive = false;
            for (int b = 0; b < 2; b++)
            {
                boundsMinX[b] = int.MaxValue;
                boundsMinY[b] = int.MaxValue;
                boundsMaxX[b] = int.MinValue;
                boundsMaxY[b] = int.MinValue;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                Entry e = entries[i];
                if (false == e.bVisible || null == e.layout) continue;
                DrawEntry(e, t);
            }

            FlushRun();

            groundBuffer.Upload(groundMesh, 0);
            ApplyTightBounds(groundMesh, 0);
            groundMeshRenderer.enabled = 0 < groundBuffer.VertexCount;

            topBuffer.Upload(topMesh, 0);
            ApplyTightBounds(topMesh, 1);
            topMeshRenderer.enabled = 0 < topBuffer.VertexCount;
        }

        private void ApplyTightBounds(Mesh _mesh, int _index)
        {
            Bounds bounds;
            if (boundsMaxX[_index] < boundsMinX[_index])
            {
                bounds = new Bounds(Vector3.zero, new Vector3(0.1f, 0.1f, PixelVfxCulling.BoundsDepth));
            }
            else
            {
                float minX = boundsMinX[_index] / PixelsPerUnit;
                float minY = boundsMinY[_index] / PixelsPerUnit;
                float maxX = boundsMaxX[_index] / PixelsPerUnit;
                float maxY = boundsMaxY[_index] / PixelsPerUnit;
                bounds = new Bounds(new Vector3((minX + maxX) * 0.5f, (minY + maxY) * 0.5f, 0.0f), new Vector3(maxX - minX + 0.1f, maxY - minY + 0.1f, PixelVfxCulling.BoundsDepth));
            }

            Bounds current = _mesh.bounds;
            if (current.center != bounds.center || current.size != bounds.size) _mesh.bounds = bounds;
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            if (null != Instance && this != Instance)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            groundMesh = new Mesh { name = "TreeSporeDebuff_Ground" };
            groundMesh.MarkDynamic();
            groundMeshFilter.sharedMesh = groundMesh;
            topMesh = new Mesh { name = "TreeSporeDebuff_Top" };
            topMesh.MarkDynamic();
            topMeshFilter.sharedMesh = topMesh;

            groundLayerId = SortingLayer.NameToID(groundSortingLayerName);
            topLayerId = SortingLayer.NameToID(topSortingLayerName);

            groundMeshRenderer.sortingLayerID = groundLayerId;
            groundMeshRenderer.sortingOrder = groundMeshOrder;
            groundMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            groundMeshRenderer.receiveShadows = false;
            groundMeshRenderer.enabled = false;
            topMeshRenderer.sortingLayerID = topLayerId;
            topMeshRenderer.sortingOrder = topMeshOrder;
            topMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            topMeshRenderer.receiveShadows = false;
            topMeshRenderer.enabled = false;

            lightColors = new Color32[4];
            midColors = new Color32[4];
            deepColors = new Color32[4];
            for (int t = 1; t <= 3; t++)
            {
                deepColors[t] = TreeDebuffPalette.Vertex(t, 0);
                midColors[t] = TreeDebuffPalette.Vertex(t, 1);
                lightColors[t] = TreeDebuffPalette.Vertex(t, 2);
            }

            outlineColor = TreeDebuffPalette.VertexOutline();
        }

        private void LateUpdate()
        {
            if (0 == entries.Count)
            {
                if (true == groundMeshRenderer.enabled) groundMeshRenderer.enabled = false;
                if (true == topMeshRenderer.enabled) topMeshRenderer.enabled = false;
                return;
            }

            int tick = Mathf.FloorToInt(Time.time * TickRate);
            if (tick == lastTick && false == bDirty) return;

            lastTick = tick;
            bDirty = false;

            CleanupDead();
            CollectVisible();
            ApplyRenderers();
            RebuildMeshes(tick);
        }

        private void OnDestroy()
        {
            // 파괴 중인 자신을 부모로 다시 지정하면 오류가 나므로 풀로 되돌리지 않는다. 렌더러는 자식이라 함께 파괴된다.
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].overlay = null;
                entries[i].ground = null;
            }

            entries.Clear();

            if (this == Instance) Instance = null;
            if (null != groundMesh) Destroy(groundMesh);
            if (null != topMesh) Destroy(topMesh);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }
    }
}
