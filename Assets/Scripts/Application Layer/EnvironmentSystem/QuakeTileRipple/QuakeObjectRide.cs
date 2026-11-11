using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 지진 물결 위의 물체(나무·원목·아이템·차량·캐릭터 등 바닥 기준으로 정렬되는 것)를 선 칸의 높이만큼 함께 출렁이게 보이게 한다.
/// 물체는 모두 타일보다 위 정렬 층에 그려져 타일 복제층과 같이 움직이지 않는다. 옮기지 않으면 바닥만 물체 밑으로 미끄러져 보인다.
///
/// [그리는 동안에만 옮긴다 - 게임은 원래 자리만 본다]
///   렌더 파이프라인이 화면을 그리기 직전(beginContextRendering)에 옮기고, 다 그린 직후(endContextRendering)에 원래 자리로 되돌린다.
///   Update·LateUpdate·물리·트윈 등 게임 코드는 언제나 원래 자리만 본다. 충돌·판정·카메라 추적이 바뀌지 않는다.
///   - 정렬 순서는 CustomSortable이 원래 자리로 미리 정해 둔 값이라 옮겨도 앞뒤가 그대로다.
///   - 충돌체(Collider2D·Rigidbody2D)가 달린 트랜스폼은 옮기지 않는다. 옮겼다 되돌려도 물리는 바뀐 것으로 보고 자세·모양을 다시 맞추고,
///     정적 충돌체는 모양을 다시 만들며 트리거 판정이 다시 날 수 있다. 충돌체가 달린 물체(캐릭터·차량)는 충돌체가 없는 자식만 옮긴다.
///   - 그림이 충돌체와 같은 트랜스폼에 있으면(차량 짐칸 상자) 원본은 그 자리에서 숨기고(forceRenderingOff) 같은 모습의 대역을 옮겨 그린다.
///     원본 렌더러의 켜짐·재질·정렬 값은 건드리지 않는다. 스프라이트 색(SpriteRenderer.color)은 렌더러 갱신 단계에서만 GPU로 넘어가
///     그리기 직전에 바꾸면 반영되지 않으므로, 대역의 색은 흰색 그대로 두고 원본 색을 대역 전용 재질의 _Color에 곱해 넣는다(재질 값은 그릴 때 읽힌다).
///   - 물체 안에서 정렬기가 여럿이면(차량: 차체·바퀴·짐칸 상자) 부분마다 제 지면 위치의 칸 높이를 따른다. 정렬기가 없는 부분(차량 그림자)은
///     가장 가까운 정렬기를 따른다 - 짐칸 상자 그림자는 상자와, 차량 그림자는 차체와 같이 움직인다.
///   - 물(용암) 칸은 출렁이지 않으므로 그 위의 물체도 그대로다.
/// 연출 중 새로 나타난 물체(쓰러진 나무의 원목 등)는 잠깐마다 켜진 정렬기 목록(CustomSortable.CollectEnabled)에서 찾아 더한다.
///
/// [나무 - 옮긴 높이를 셰이더에 알린다]
///   나무 셰이더(TreeWindSway.hlsl)는 피봇(트랜스폼 자리)으로 바람·지진 떨림 위상과 진원 거리를 정한다. 옮긴 채로 그리면 위상이 튀고
///   거리가 실제 자리(피해 판정)와 어긋난다. 그래서 나무(TreeObj 루트)를 옮길 때 옮긴 픽셀과, 지진에 맞은 나무(MarkStruck)가 튀는
///   픽셀을 루트 z에 담는다(RideZStep 단위, 옮긴 픽셀 × 16 + 튀는 픽셀). 셰이더가 이걸 풀어 원래 자리로 위상·거리를 구하고 튀는 높이만큼
///   그림을 올린다(그림자는 바닥에 남는다). z는 그리는 순서(정렬 축 y)·화면에 영향이 없고 다 그린 뒤 되돌린다. 나무는 z 0에 놓인다.
/// </summary>
public sealed class QuakeObjectRide
{
    private const float PixelsPerUnit = 32f;
    // 새로 나타난 물체를 찾는 간격(초). 켜진 정렬기를 전부 훑으므로 매 프레임 하지 않는다. 원목은 떨어지는 데 이보다 오래 걸린다.
    private const float CollectInterval = 0.1f;
    // 나무 루트 z에 담는 한 단위(TreeWindSway.hlsl QUAKE_RIDE_Z_STEP과 같다). 담는 값은 옮긴 픽셀 × RideCodeBase + 튀는 픽셀.
    private const float RideZStep = 1f / 4096f;
    private const int RideCodeBase = 16;
    private const int MaxPopPixels = RideCodeBase - 1;

    private sealed class Part
    {
        public Transform transform;     // 옮길 트랜스폼. 대역이면 원본 렌더러의 트랜스폼(옮기지 않는다)
        public SpriteRenderer source;   // 대역으로 그릴 원본(충돌체와 같은 트랜스폼의 그림). 없으면 transform을 옮긴다
        public CustomSortable sample;   // 칸을 고를 지면 위치. 없으면 transform 자리
        public SpriteRenderer standIn;  // source의 대역(source가 있을 때만)
        public Material standInMaterial; // 대역 전용 재질(원본 재질 값 + 원본 색). 원본 재질에 _Color가 없으면 비어 있다
    }

    private sealed class Rider
    {
        public Transform root;
        public Part[] parts;
        public bool bTree;   // 나무 셰이더에 옮긴 높이를 알린다([나무])
        public PopKind pop;  // 앞머리가 지날 때 통 튀는 물체([튐])
        public bool[] popParts;      // 부분마다 튀는지(바닥 층 그림만 있는 부분 - 그림자·범위 표시 - 은 튀지 않는다)
        public Transform[] grounded; // 튀는 부분 안의 바닥 층 그림(그림자). 튄 만큼 도로 내려 바닥에 남긴다
        public Transform squash;     // 캐릭터 그림 묶음(터지는 순간 눌렸다 늘어난다)
        public CustomSortable ground; // 튀는 때를 정할 지면 위치(루트 정렬기). 없으면 루트 자리
        public Renderer look;        // 튀는 물체의 대표 그림. 날아가는 중(FlyingItem 층)이면 튀지 않는다
    }

    private enum PopKind { None, Item, Character }

    // [나눠 모으기] 처음 모으기에서 Tick마다 더하는 물체 수. 물체 하나 더하는 데 수십 µs라 한 프레임에 1ms 남짓이다.
    // 전조(약 0.35초 = 20프레임 남짓) 동안 수백 개를 다 모은다. 전조가 없어도 가까운 것부터라 앞머리보다 빠르다.
    private const int GatherBudget = 40;
    // 나무 칸 높이를 읽을지 거를 때 쓰는 여유(바닥 평면 거리). 칸 안의 한 점과 칸 가운데의 거리 차(최대 0.5)보다 넉넉하다.
    private const float PosedKeyMargin = 1f;

    private QuakeTileRipple ripple;
    private Rect area;
    private Vector3 center;
    private CustomSortable[] gatherSortables;
    private float[] gatherKeys;
    private int gatherCount;
    private int gatherIndex;
    // false면 칸 높이만큼 옮기지 않고 나무만 모아 맞은 나무가 튀는 것만 그린다(QuakeTileRippleSettings.rideObjects 꺼짐).
    private bool bRideHeights;
    private bool bActive;
    private bool bSubscribed;
    private bool bApplied;
    private float collectTimer;
    // 물결 시작부터 흐른 게임 시간과, 지진에 맞은 나무 루트 → 맞은 시각. 튀는 높이는 맞은 뒤 지난 시간으로 정한다.
    private float clock;
    private float popHeight;
    private float popSeconds;
    private readonly Dictionary<Transform, float> struckAt = new Dictionary<Transform, float>(64);
    // [튐] 앞머리가 지날 때 원목·아이템·캐릭터가 통 튀는 높이(월드)·시간(초)과 캐릭터 눌림(ConfigurePop). 높이가 모두 0이면 튀지 않는다.
    private float itemPopHeight;
    private float characterPopHeight;
    private float objectPopSeconds = 0.3f;
    private float squashAmount;
    private float squashSeconds = 0.3f;
    private bool PopEnabled => itemPopHeight > 0f || characterPopHeight > 0f || squashAmount > 0f;
    // 지금 크기를 바꿔 둔 트랜스폼과 바꾸기 전 크기.
    private readonly List<Transform> scaled = new List<Transform>(2);
    private readonly List<Vector3> savedScale = new List<Vector3>(2);
    private static readonly List<Renderer> rendererBuffer = new List<Renderer>(16);
    private static readonly List<Transform> groundedBuffer = new List<Transform>(4);
    // 바닥에 남는 그림 층(그림자·범위 표시)과 날아가는 아이템 층.
    private static int[] groundLayerIds;
    private static int flyingLayerId;

    private readonly List<Rider> riders = new List<Rider>(512);
    private readonly HashSet<Transform> roots = new HashSet<Transform>();
    private readonly HashSet<CustomSortable> known = new HashSet<CustomSortable>();
    // 지금 옮겨 둔 트랜스폼과 옮기기 전 로컬 자리(되돌릴 때 그대로 쓴다).
    private readonly List<Transform> shifted = new List<Transform>(512);
    private readonly List<Vector3> savedLocal = new List<Vector3>(512);
    // 대역 렌더러 전부와, 지금 숨겨 둔 원본·드러낸 대역.
    private readonly List<SpriteRenderer> standIns = new List<SpriteRenderer>(4);
    private readonly List<Material> standInMaterials = new List<Material>(4);
    private readonly List<SpriteRenderer> hiddenSources = new List<SpriteRenderer>(4);
    private readonly List<SpriteRenderer> shownStandIns = new List<SpriteRenderer>(4);
    private MaterialPropertyBlock propertyBlock;
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private static readonly List<Part> partBuffer = new List<Part>(16);
    private static readonly List<int> pixelBuffer = new List<int>(16);
    private static readonly List<CustomSortable> sortableBuffer = new List<CustomSortable>(1024);
    private static readonly List<CustomSortable> innerSortables = new List<CustomSortable>(8);

    /// <summary>
    /// 물결 시작. _area(월드) 안의 물체를 모아 그릴 때마다 _ripple의 칸 높이만큼 옮긴다(_bRideHeights가 false면 옮기지 않고 나무만 모은다).
    /// 지진에 맞은 나무(MarkStruck)는 _popSeconds 동안 _popHeight까지 한 번 튄다(그리는 동안만).
    /// </summary>
    public void Begin(QuakeTileRipple _ripple, Rect _area, Vector3 _center, bool _bRideHeights, float _popHeight, float _popSeconds)
    {
        Stop();

        ripple = _ripple;
        area = _area;
        center = _center;
        bRideHeights = _bRideHeights;
        bActive = true;
        collectTimer = 0f;
        clock = 0f;
        popHeight = Mathf.Max(0f, _popHeight);
        popSeconds = Mathf.Max(0.01f, _popSeconds);
        // 칸 높이를 따르지 않고 튀는 물체도 없으면 맞은 나무만 MarkStruck에서 그때그때 더한다(미리 모을 물체가 없다).
        // 처음 모으기는 한 번에 하지 않는다. 진원에서 가까운 것부터 Tick마다 GatherBudget개씩 더한다([나눠 모으기]).
        if (bCollect) StartGather();
        Subscribe(true);
    }

    /// <summary>
    /// [튐] 앞머리가 지날 때 원목·아이템(_itemHeight)과 캐릭터(_characterHeight)가 _seconds 동안 한 번 튄다(월드 높이, 그리는 동안만 -
    /// 그림자는 바닥에 남는다). 캐릭터는 터지는 순간 _squash만큼 눌렸다 늘어나며 _squashSeconds에 돌아온다. Begin 전에 부른다. 모두 0이면 끈다.
    /// </summary>
    public void ConfigurePop(float _itemHeight, float _characterHeight, float _seconds, float _squash, float _squashSeconds)
    {
        itemPopHeight = Mathf.Max(0f, _itemHeight);
        characterPopHeight = Mathf.Max(0f, _characterHeight);
        objectPopSeconds = Mathf.Max(0.05f, _seconds);
        squashAmount = Mathf.Clamp(_squash, 0f, 0.5f);
        squashSeconds = Mathf.Max(0.05f, _squashSeconds);
    }

    // 나무 말고도 모을 물체가 있는지(칸 높이를 따르거나 튀는 물체가 있다).
    private bool bCollect => bRideHeights || PopEnabled;

    /// <summary>물결이 도는 동안 매 프레임(게임 시간). 새로 나타난 물체를 잠깐마다 더한다.</summary>
    public void Tick(float _deltaTime)
    {
        if (false == bActive) return;

        // 그리던 중 예외로 되돌리기를 못 했으면 게임 코드가 돌기 전에 되돌린다.
        Restore();

        clock += _deltaTime;
        if (false == bCollect) return;

        // 나눠 모으는 중이면 그것부터 마저 한다(그동안은 새로 나타난 물체를 찾지 않는다 - 다 모은 뒤 찾으면 함께 들어온다).
        if (gatherIndex < gatherCount)
        {
            GatherStep(GatherBudget);
            return;
        }

        collectTimer += _deltaTime;
        if (collectTimer < CollectInterval) return;

        collectTimer = 0f;
        StartGather();
        GatherStep(int.MaxValue);
    }

    /// <summary>끝낸다. 옮겨 둔 것을 되돌리고 그리기 콜백을 뗀다. 대역은 지운다.</summary>
    public void Stop()
    {
        Restore();
        Subscribe(false);

        bActive = false;
        ripple = null;
        ClearGather();
        riders.Clear();
        roots.Clear();
        known.Clear();
        struckAt.Clear();

        for (int i = 0; i < standIns.Count; i++)
        {
            if (null != standIns[i]) DestroyObject(standIns[i].gameObject);
        }
        standIns.Clear();

        for (int i = 0; i < standInMaterials.Count; i++)
        {
            if (null != standInMaterials[i]) DestroyObject(standInMaterials[i]);
        }
        standInMaterials.Clear();
    }

    /// <summary>
    /// 지진에 맞은 나무 루트를 알린다. 지금부터 popSeconds 동안 튄다. 다시 맞으면 처음부터 다시 튄다.
    /// 아직 모으지 않은 나무(영역 밖·다음 모으기 전에 나타난 나무)면 지금 더한다.
    /// </summary>
    public void MarkStruck(Transform _root)
    {
        if (false == bActive || null == _root || popHeight <= 0f) return;

        if (false == roots.Contains(_root))
        {
            CustomSortable sortable = _root.GetComponent<CustomSortable>();
            if (null == sortable || false == sortable.isActiveAndEnabled) return;

            Track(sortable);
            partBuffer.Clear();
            if (false == roots.Contains(_root)) return;
        }

        struckAt[_root] = clock;
    }

    // // 내부

    private void Subscribe(bool _bOn)
    {
        if (_bOn == bSubscribed) return;

        bSubscribed = _bOn;
        if (_bOn)
        {
            RenderPipelineManager.beginContextRendering += OnBeginRendering;
            RenderPipelineManager.endContextRendering += OnEndRendering;
        }
        else
        {
            RenderPipelineManager.beginContextRendering -= OnBeginRendering;
            RenderPipelineManager.endContextRendering -= OnEndRendering;
        }
    }

    private void OnBeginRendering(ScriptableRenderContext _context, List<Camera> _cameras) => Apply();

    private void OnEndRendering(ScriptableRenderContext _context, List<Camera> _cameras) => Restore();

    /// <summary>
    /// [나눠 모으기] 영역 안에서 켜져 있는, 아직 모르는 정렬기를 진원에서 가까운 순(바닥 평면 거리)으로 줄 세운다. 더하기는 GatherStep이 한다.
    /// 물결은 진원에서 퍼지므로 가까운 것부터 더하면 앞머리가 닿기 전에 더해진다. 정렬기를 모두 한 번에 더하면(물체마다 부품·그림 훑기)
    /// 물체가 수백 개일 때 한 프레임이 수 ms 무거워진다.
    /// </summary>
    private void StartGather()
    {
        CustomSortable.CollectEnabled(sortableBuffer);
        int count = sortableBuffer.Count;
        if (null == gatherSortables || gatherSortables.Length < count)
        {
            int size = Mathf.NextPowerOfTwo(Mathf.Max(64, count));
            gatherSortables = new CustomSortable[size];
            gatherKeys = new float[size];
        }

        gatherCount = 0;
        for (int i = 0; i < count; i++)
        {
            CustomSortable sortable = sortableBuffer[i];
            if (null == sortable || known.Contains(sortable)) continue;

            Vector3 position = sortable.transform.position;
            if (false == area.Contains(position)) continue;

            float dx = position.x - center.x;
            float dy = (position.y - center.y) * 2f;
            gatherSortables[gatherCount] = sortable;
            gatherKeys[gatherCount] = dx * dx + dy * dy;
            gatherCount++;
        }
        sortableBuffer.Clear();

        if (gatherCount > 1) System.Array.Sort(gatherKeys, gatherSortables, 0, gatherCount);
        gatherIndex = 0;
    }

    /// <summary>줄 세운 정렬기를 _budget개까지 더한다. 그사이 꺼졌거나 영역을 벗어난 것은 건너뛴다(다시 켜지면 다음 찾기에 들어온다).</summary>
    private void GatherStep(int _budget)
    {
        while (gatherIndex < gatherCount && _budget > 0)
        {
            CustomSortable sortable = gatherSortables[gatherIndex];
            gatherSortables[gatherIndex] = null;
            gatherIndex++;

            if (null == sortable || false == sortable.isActiveAndEnabled || known.Contains(sortable)) continue;
            if (false == area.Contains(sortable.transform.position)) continue;

            Track(sortable);
            _budget--;
        }

        if (gatherIndex >= gatherCount)
        {
            gatherIndex = 0;
            gatherCount = 0;
        }
        partBuffer.Clear();
    }

    private void ClearGather()
    {
        if (null != gatherSortables) System.Array.Clear(gatherSortables, 0, gatherSortables.Length);
        gatherIndex = 0;
        gatherCount = 0;
    }

    /// <summary>정렬기 하나를 알고 있는 것으로 두고, 그 물체(차량이면 차량 전체)를 아직 모르면 옮길 물체로 더한다. partBuffer를 쓴다.</summary>
    private void Track(CustomSortable _sortable)
    {
        known.Add(_sortable);

        // 차량은 부품(차체·바퀴·짐칸 상자·수리 상자)마다 정렬기가 있고 그림자는 차량 바로 아래에 있다. 차량 전체를 한 물체로 모은다.
        OffroadVehicleObj vehicle = _sortable.GetComponentInParent<OffroadVehicleObj>();
        Transform root = null != vehicle ? vehicle.transform : _sortable.transform;
        if (roots.Contains(root) || HasTrackedAncestor(root)) return;
        // 칸 높이를 따르지 않으면 튈 나무·물체만 있으면 된다.
        bool bTreeRoot = null != root.GetComponent<TreeObj>();
        PopKind pop = PopKindOf(root);
        if (false == bRideHeights && false == bTreeRoot && PopKind.None == pop) return;

        partBuffer.Clear();
        CollectParts(root, null, partBuffer);
        if (partBuffer.Count == 0) return;

        // 나무는 루트 하나가 통째로 한 부분이다(충돌체 없음·정렬기 하나). 그 부분에만 높이를 담는다.
        bool bTree = bTreeRoot && partBuffer.Count == 1 && partBuffer[0].transform == root && null == partBuffer[0].source;
        // 칸 높이를 따르지 않으면 높이를 담을 수 있는 나무와 튀는 물체만 둔다(다른 모양은 움직일 일이 없다).
        if (false == bRideHeights && false == bTree && PopKind.None == pop) return;

        SampleNearest(root, partBuffer);
        for (int p = 0; p < partBuffer.Count; p++)
        {
            Part part = partBuffer[p];
            if (null == part.source) continue;

            part.standIn = CreateStandIn();
            standIns.Add(part.standIn);
        }

        Rider rider = new Rider { root = root, parts = partBuffer.ToArray(), bTree = bTree };
        if (PopKind.None != pop && false == bTree) SetupPop(rider, pop);

        roots.Add(root);
        riders.Add(rider);
    }

    /// <summary>[튐] 튀는 물체 종류. 아이템(원목 등)과 캐릭터만 튄다.</summary>
    private PopKind PopKindOf(Transform _root)
    {
        if (false == PopEnabled) return PopKind.None;
        if (null != _root.GetComponent<Character>()) return characterPopHeight > 0f || squashAmount > 0f ? PopKind.Character : PopKind.None;
        if (null != _root.GetComponent<Item>()) return itemPopHeight > 0f ? PopKind.Item : PopKind.None;
        return PopKind.None;
    }

    /// <summary>
    /// [튐] 부분마다 튀는지 정한다. 바닥 층이 아닌 그림이 있는 부분만 튄다(그림자·범위 표시만 있는 부분은 바닥에 남는다).
    /// 튀는 부분 안의 바닥 층 그림(원목 그림자 등)은 튄 만큼 도로 내린다. 대역으로 그리는 부분(충돌체와 같은 트랜스폼의 그림)은 튀지 않는다.
    /// </summary>
    private static void SetupPop(Rider _rider, PopKind _pop)
    {
        EnsureLayerIds();

        _rider.pop = _pop;
        _rider.ground = _rider.root.GetComponent<CustomSortable>();
        _rider.popParts = new bool[_rider.parts.Length];
        groundedBuffer.Clear();

        for (int p = 0; p < _rider.parts.Length; p++)
        {
            Part part = _rider.parts[p];
            if (null != part.source || null == part.transform) continue;

            part.transform.GetComponentsInChildren(true, rendererBuffer);
            bool bLifted = false;
            for (int r = 0; r < rendererBuffer.Count; r++)
            {
                Renderer renderer = rendererBuffer[r];
                if (IsGroundLayer(renderer.sortingLayerID)) continue;

                bLifted = true;
                if (null == _rider.look && renderer is SpriteRenderer) _rider.look = renderer;
            }

            _rider.popParts[p] = bLifted;
            if (false == bLifted)
            {
                rendererBuffer.Clear();
                continue;
            }

            // 튀는 부분 안의 바닥 층 그림. 그 아래에 바닥 층이 아닌 그림이 있으면(함께 튀어야 한다) 내리지 않는다.
            for (int r = 0; r < rendererBuffer.Count; r++)
            {
                Transform grounded = rendererBuffer[r].transform;
                if (false == IsGroundLayer(rendererBuffer[r].sortingLayerID) || grounded == part.transform || groundedBuffer.Contains(grounded)) continue;
                if (HasLiftedRenderer(grounded)) continue;

                groundedBuffer.Add(grounded);
            }

            rendererBuffer.Clear();
        }

        _rider.grounded = groundedBuffer.ToArray();
        groundedBuffer.Clear();

        if (PopKind.Character == _pop)
        {
            Character character = _rider.root.GetComponent<Character>();
            _rider.squash = null != character ? character.VisualRoot : null;
        }
    }

    private static bool HasLiftedRenderer(Transform _transform)
    {
        Renderer[] renderers = _transform.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (false == IsGroundLayer(renderers[i].sortingLayerID)) return true;
        }
        return false;
    }

    private static void EnsureLayerIds()
    {
        if (null != groundLayerIds) return;

        groundLayerIds = new[]
        {
            SortingLayer.NameToID("TreeShadow"),
            SortingLayer.NameToID("ItemShadow"),
            SortingLayer.NameToID("CharacterShadow"),
            SortingLayer.NameToID("Indicators"),
        };
        flyingLayerId = SortingLayer.NameToID("FlyingItem");
    }

    private static bool IsGroundLayer(int _layerId)
    {
        for (int i = 0; i < groundLayerIds.Length; i++)
        {
            if (groundLayerIds[i] == _layerId) return true;
        }
        return false;
    }

    private bool HasTrackedAncestor(Transform _transform)
    {
        for (Transform parent = _transform.parent; null != parent; parent = parent.parent)
        {
            if (roots.Contains(parent)) return true;
        }
        return false;
    }

    /// <summary>
    /// _transform 아래가 한 덩어리(충돌체 없음·정렬기 하나 이하)면 통째로 담는다. 아니면 자식들로 나눠 담고,
    /// 이 트랜스폼 자신의 그림은 대역으로 담는다. 그림이 없는 부분은 뺀다. _inherited는 가장 가까운 위쪽 정렬기다.
    /// </summary>
    private static void CollectParts(Transform _transform, CustomSortable _inherited, List<Part> _into)
    {
        CustomSortable own = _transform.GetComponent<CustomSortable>();
        if (null != own) _inherited = own;

        _transform.GetComponentsInChildren(true, innerSortables);
        int sortableCount = innerSortables.Count;
        CustomSortable single = sortableCount == 1 ? innerSortables[0] : null;
        innerSortables.Clear();

        bool bPhysics = null != _transform.GetComponentInChildren<Collider2D>(true) || null != _transform.GetComponentInChildren<Rigidbody2D>(true);
        if (false == bPhysics && sortableCount <= 1)
        {
            if (null != _transform.GetComponentInChildren<Renderer>(true))
            {
                _into.Add(new Part { transform = _transform, sample = null != _inherited ? _inherited : single });
            }
            return;
        }

        // 자신의 그림(정렬 그룹 밖의 스프라이트만 - 대역은 정렬 그룹에 들어가지 못한다).
        SpriteRenderer sprite = _transform.GetComponent<SpriteRenderer>();
        if (null != sprite && null == sprite.GetComponentInParent<SortingGroup>(true))
        {
            _into.Add(new Part { transform = _transform, source = sprite, sample = _inherited });
        }

        for (int c = 0; c < _transform.childCount; c++)
        {
            CollectParts(_transform.GetChild(c), _inherited, _into);
        }
    }

    /// <summary>정렬기를 못 정한 부분(차량 그림자 등)은 물체 안에서 가장 가까운 정렬기를 따른다.</summary>
    private static void SampleNearest(Transform _root, List<Part> _parts)
    {
        bool bMissing = false;
        for (int p = 0; p < _parts.Count; p++) bMissing |= null == _parts[p].sample;
        if (false == bMissing) return;

        _root.GetComponentsInChildren(true, innerSortables);
        if (innerSortables.Count > 0)
        {
            for (int p = 0; p < _parts.Count; p++)
            {
                Part part = _parts[p];
                if (null != part.sample) continue;

                Vector3 position = part.transform.position;
                float best = float.MaxValue;
                for (int s = 0; s < innerSortables.Count; s++)
                {
                    float distance = (innerSortables[s].GroundPosition - position).sqrMagnitude;
                    if (distance >= best) continue;

                    best = distance;
                    part.sample = innerSortables[s];
                }
            }
        }
        innerSortables.Clear();
    }

    /// <summary>대역은 평소 숨겨 둔다(forceRenderingOff). 색은 흰색 그대로 둔다 - 원본 색은 재질로 넣는다.</summary>
    private static SpriteRenderer CreateStandIn()
    {
        GameObject standIn = new GameObject("QuakeRideStandIn");
        SpriteRenderer renderer = standIn.AddComponent<SpriteRenderer>();
        renderer.forceRenderingOff = true;
        return renderer;
    }

    private static void DestroyObject(UnityEngine.Object _object)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(_object);
        else UnityEngine.Object.DestroyImmediate(_object);
    }

    /// <summary>대역의 겉모습을 원본에 맞춘다(그리기 직전). 원본 색은 재질(또는 속성 블록)의 _Color에 곱해 넣는다.</summary>
    private void CopyLook(Part _part)
    {
        SpriteRenderer source = _part.source;
        SpriteRenderer standIn = _part.standIn;
        standIn.gameObject.layer = source.gameObject.layer;
        standIn.sprite = source.sprite;
        standIn.flipX = source.flipX;
        standIn.flipY = source.flipY;
        standIn.drawMode = source.drawMode;
        if (SpriteDrawMode.Simple != source.drawMode)
        {
            standIn.size = source.size;
            standIn.tileMode = source.tileMode;
        }
        standIn.maskInteraction = source.maskInteraction;
        standIn.spriteSortPoint = source.spriteSortPoint;
        standIn.sortingLayerID = source.sortingLayerID;
        standIn.sortingOrder = source.sortingOrder;
        standIn.renderingLayerMask = source.renderingLayerMask;

        propertyBlock ??= new MaterialPropertyBlock();
        source.GetPropertyBlock(propertyBlock);

        Material material = source.sharedMaterial;
        Color tint = source.color;
        if (null == material || false == material.HasProperty(ColorId))
        {
            // _Color가 없는 셰이더 - 색을 곱할 곳이 없어 원본 재질 그대로 그린다(색은 흰색).
            standIn.sharedMaterial = material;
        }
        else if (propertyBlock.HasColor(ColorId))
        {
            standIn.sharedMaterial = material;
            propertyBlock.SetColor(ColorId, propertyBlock.GetColor(ColorId) * tint);
        }
        else
        {
            if (null == _part.standInMaterial)
            {
                _part.standInMaterial = new Material(material) { name = "QuakeRideStandIn", hideFlags = HideFlags.DontSave };
                standInMaterials.Add(_part.standInMaterial);
            }
            else if (_part.standInMaterial.shader != material.shader)
            {
                _part.standInMaterial.shader = material.shader;
            }

            _part.standInMaterial.CopyPropertiesFromMaterial(material);
            _part.standInMaterial.SetColor(ColorId, material.GetColor(ColorId) * tint);
            standIn.sharedMaterial = _part.standInMaterial;
        }

        standIn.SetPropertyBlock(propertyBlock);
    }

    private void Apply()
    {
        // 물결이 돌기 전(전조)에는 칸 높이가 모두 0이고 앞머리도 아무 데도 닿지 않아 옮길 것이 없다.
        if (bApplied || false == bActive || null == ripple || false == ripple.IsPlaying) return;
        bApplied = true;

        // 나무는 지금 움직이는 칸 거리 범위 밖이면 칸 높이를 읽지 않는다(그 밖의 칸은 모두 0 - 결과가 같다). 나무가 대부분이라 여기서 크게 준다.
        ripple.GetPosedKeyRange(out float posedMin, out float posedMax);
        float posedLow = Mathf.Max(0f, posedMin - PosedKeyMargin);
        float posedLowSq = posedLow * posedLow;
        float posedHigh = posedMax + PosedKeyMargin;
        float posedHighSq = posedMax < posedMin ? -1f : posedHigh * posedHigh;

        for (int r = 0; r < riders.Count; r++)
        {
            Rider rider = riders[r];
            if (null == rider.root) continue;
            if (false == rider.root.gameObject.activeInHierarchy)
            {
                // 튀는 도중 꺼진(풀로 돌아간) 나무는 튐을 끝낸다. 다시 켜져도 새로 맞기 전에는 튀지 않는다.
                if (struckAt.Count > 0) struckAt.Remove(rider.root);
                continue;
            }

            if (rider.bTree)
            {
                // 나무는 루트 하나가 한 부분이다(Track). 아래 일반 경로와 같은 값을 읽되, 칸 높이는 움직이는 칸 범위 안일 때만 읽는다.
                int treePixels = 0;
                if (bRideHeights)
                {
                    Part part = rider.parts[0];
                    Vector3 ground = null != part.sample ? part.sample.GroundPosition : part.transform.position;
                    float dx = ground.x - center.x;
                    float dy = (ground.y - center.y) * 2f;
                    float distanceSq = dx * dx + dy * dy;
                    if (distanceSq >= posedLowSq && distanceSq <= posedHighSq) treePixels = ripple.PixelsAt(ground);
                }

                ApplyTree(rider, treePixels);
                continue;
            }

            // 높이를 모두 읽은 뒤에 옮긴다. 먼저 옮긴 부분의 정렬기를 다른 부분이 따르면 옮긴 자리로 칸을 고르게 된다.
            pixelBuffer.Clear();
            for (int p = 0; p < rider.parts.Length; p++)
            {
                Part part = rider.parts[p];
                int pixels = 0;
                if (bRideHeights && null != part.transform && part.transform.gameObject.activeInHierarchy)
                {
                    pixels = ripple.PixelsAt(null != part.sample ? part.sample.GroundPosition : part.transform.position);
                }
                pixelBuffer.Add(pixels);
            }

            // [튐] 앞머리가 이 물체 칸을 지난 뒤 지난 시간으로 튀는 높이·눌림을 정한다.
            int pop = 0;
            float age = float.NegativeInfinity;
            if (PopKind.None != rider.pop)
            {
                age = ripple.FrontAgeAt(null != rider.ground ? rider.ground.GroundPosition : rider.root.position);
                pop = ObjectPopPixels(rider, age);
            }

            for (int p = 0; p < rider.parts.Length; p++)
            {
                int pixels = pixelBuffer[p];
                if (0 != pop && rider.popParts[p]) pixels += pop;
                if (0 == pixels) continue;

                Part part = rider.parts[p];
                Vector3 offset = new Vector3(0f, pixels / PixelsPerUnit, 0f);
                if (null != part.source)
                {
                    DrawStandIn(part, offset);
                    continue;
                }

                shifted.Add(part.transform);
                savedLocal.Add(part.transform.localPosition);
                part.transform.position += offset;
            }

            // 튄 부분 안의 그림자는 튄 만큼 도로 내려 바닥(칸 높이)에 남긴다.
            if (0 != pop)
            {
                Vector3 down = new Vector3(0f, -pop / PixelsPerUnit, 0f);
                for (int g = 0; g < rider.grounded.Length; g++)
                {
                    Transform grounded = rider.grounded[g];
                    if (null == grounded) continue;

                    shifted.Add(grounded);
                    savedLocal.Add(grounded.localPosition);
                    grounded.position += down;
                }
            }

            if (null != rider.squash && squashAmount > 0f && age >= 0f && age < squashSeconds) ApplySquash(rider.squash, age / squashSeconds);
        }
    }

    /// <summary>[튐] 지금 튀는 높이(픽셀). 앞머리가 칸을 지난 뒤 objectPopSeconds 동안 한 번 튄다. 날아가는 아이템(FlyingItem 층)은 튀지 않는다.</summary>
    private int ObjectPopPixels(Rider _rider, float _age)
    {
        if (_age < 0f || _age >= objectPopSeconds) return 0;

        float height = PopKind.Character == _rider.pop ? characterPopHeight : itemPopHeight;
        if (height <= 0f) return 0;
        if (null != _rider.look && _rider.look.sortingLayerID == flyingLayerId) return 0;

        return Mathf.Max(0, Mathf.RoundToInt(height * PopShape(_age / objectPopSeconds) * PixelsPerUnit));
    }

    // 캐릭터 눌림 곡선(가로로 늘어나는 몫, 세로는 그만큼 준다). 터지는 순간 확 눌리고, 튀어 오르며 세로로 늘어났다가 한 번 더 작게 출렁이고 돌아온다.
    private static readonly float[] SquashKeyTimes = { 0f, 0.14f, 0.42f, 0.7f, 1f };
    private static readonly float[] SquashKeyValues = { 0.55f, 1f, -0.55f, 0.18f, 0f };

    /// <summary>캐릭터 그림 묶음 크기를 눌림 곡선(_t 0~1)대로 바꾼다(그리는 동안만). 지금 크기에 곱해 좌우 방향(음수 크기)이 그대로다.</summary>
    private void ApplySquash(Transform _visual, float _t)
    {
        int k = 1;
        while (k < SquashKeyTimes.Length - 1 && _t > SquashKeyTimes[k]) k++;
        float span = Mathf.Max(0.0001f, SquashKeyTimes[k] - SquashKeyTimes[k - 1]);
        float f = Mathf.Lerp(SquashKeyValues[k - 1], SquashKeyValues[k], Mathf.SmoothStep(0f, 1f, (_t - SquashKeyTimes[k - 1]) / span));
        float amount = squashAmount * f;
        if (Mathf.Abs(amount) < 0.005f) return;

        scaled.Add(_visual);
        savedScale.Add(_visual.localScale);
        Vector3 scale = _visual.localScale;
        _visual.localScale = new Vector3(scale.x * (1f + amount), scale.y * (1f - amount), scale.z);
    }

    /// <summary>나무: 칸 높이(_pixels)만큼 옮기고, 옮긴 픽셀과 튀는 픽셀을 루트 z에 담는다([나무]). 둘 다 0이면 그대로 둔다.</summary>
    private void ApplyTree(Rider _rider, int _pixels)
    {
        Transform root = _rider.root;
        int pop = PopPixels(root);
        if (0 == _pixels && 0 == pop) return;

        shifted.Add(root);
        savedLocal.Add(root.localPosition);
        Vector3 position = root.position;
        position.y += _pixels / PixelsPerUnit;
        position.z += (_pixels * RideCodeBase + pop) * RideZStep;
        root.position = position;
    }

    // 튀는 시간 중 올라가는 몫. 땅에 차여 빠르게 솟고(감속), 나머지 시간 동안 떨어진다(가속) - 던져 올린 물체처럼 보인다.
    private const float PopRiseShare = 0.3f;

    /// <summary>
    /// 지진에 맞은 나무가 지금 튀는 높이(픽셀). 맞은 뒤 popSeconds 동안, 처음 PopRiseShare 몫에 꼭대기까지 차여 오르고
    /// 남은 시간에 떨어진다(둘 다 포물선이라 꼭대기에서 매끄럽게 이어진다). 끝난 나무는 목록에서 뺀다.
    /// </summary>
    private int PopPixels(Transform _root)
    {
        if (struckAt.Count == 0 || false == struckAt.TryGetValue(_root, out float hitAt)) return 0;

        float age = clock - hitAt;
        if (age >= popSeconds)
        {
            struckAt.Remove(_root);
            return 0;
        }

        return Mathf.Clamp(Mathf.RoundToInt(popHeight * PopShape(Mathf.Clamp01(age / popSeconds)) * PixelsPerUnit), 0, MaxPopPixels);
    }

    /// <summary>튀는 높이 비율(0~1, _t는 튀는 시간 비율). 처음 PopRiseShare 몫에 꼭대기까지 감속하며 솟고, 남은 시간에 가속하며 떨어진다.</summary>
    private static float PopShape(float _t)
    {
        if (_t < PopRiseShare)
        {
            float rise = 1f - _t / PopRiseShare;
            return 1f - rise * rise;
        }

        float fall = (_t - PopRiseShare) / (1f - PopRiseShare);
        return 1f - fall * fall;
    }

    /// <summary>원본을 그 자리에서 숨기고, 같은 모습의 대역을 _offset만큼 옮겨 드러낸다.</summary>
    private void DrawStandIn(Part _part, Vector3 _offset)
    {
        SpriteRenderer source = _part.source;
        SpriteRenderer standIn = _part.standIn;
        if (null == source || null == standIn) return;
        if (false == source.enabled || source.forceRenderingOff || null == source.sprite) return;

        CopyLook(_part);
        Transform sourceTransform = source.transform;
        standIn.transform.SetPositionAndRotation(sourceTransform.position + _offset, sourceTransform.rotation);
        standIn.transform.localScale = sourceTransform.lossyScale;

        standIn.forceRenderingOff = false;
        shownStandIns.Add(standIn);
        source.forceRenderingOff = true;
        hiddenSources.Add(source);
    }

    private void Restore()
    {
        if (false == bApplied) return;
        bApplied = false;

        // 나중에 옮긴 것부터 되돌린다(같은 트랜스폼을 두 번 옮겼으면 처음 자리로 돌아간다).
        for (int i = shifted.Count - 1; i >= 0; i--)
        {
            if (null != shifted[i]) shifted[i].localPosition = savedLocal[i];
        }

        for (int i = scaled.Count - 1; i >= 0; i--)
        {
            if (null != scaled[i]) scaled[i].localScale = savedScale[i];
        }

        for (int i = 0; i < hiddenSources.Count; i++)
        {
            if (null != hiddenSources[i]) hiddenSources[i].forceRenderingOff = false;
        }

        for (int i = 0; i < shownStandIns.Count; i++)
        {
            if (null != shownStandIns[i]) shownStandIns[i].forceRenderingOff = true;
        }

        shifted.Clear();
        savedLocal.Clear();
        scaled.Clear();
        savedScale.Clear();
        hiddenSources.Clear();
        shownStandIns.Clear();
    }
}
