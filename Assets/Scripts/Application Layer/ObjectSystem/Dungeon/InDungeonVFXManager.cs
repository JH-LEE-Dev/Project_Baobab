using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 던전 내 모든 VFX를 중앙에서 관리하는 매니저입니다.
/// 나무 프리팹이 아닌 씬 레벨의 독립 오브젝트에서 VFXComponent를 소유하므로,
/// 나무가 비활성화되어도 파티클이 끊기지 않고 끝까지 재생됩니다.
/// </summary>
public class InDungeonVFXManager : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private VFXComponent vfxComponent;

    // top 기준 위치가 나무 꼭대기보다 한참 위에서 생성되는 것을 보정하기 위한 하향 오프셋 (인스펙터에서 조정 가능)
    [SerializeField] private float shieldBrokenVfxYOffset = -0.5f;

    // 나무가 그려지는 정렬 레이어. 보석 임팩트 VFX를 나무와 같은 레이어에 명시적으로 올릴 때 쓴다.
    private const string TreeSortingLayerName = "Objects";

    [Header("Constellation Ground Mark")]
    [SerializeField] private TreeStarMarkGroundAnimator treeStarMarkGroundPrefab;
    [SerializeField] private int treeStarMarkGroundPoolDefaultCapacity = 8;
    [SerializeField] private int treeStarMarkGroundPoolMaxSize = 64;

    private IObjectPool<TreeStarMarkGroundAnimator> treeStarMarkGroundPool;

    // 그룹(별자리)별로 아직 발현되지 않아 살아있는 그라운드 마크 인스턴스들을 추적한다.
    private readonly Dictionary<int, List<TreeStarMarkGroundAnimator>> activeGroundMarksByGroup = new Dictionary<int, List<TreeStarMarkGroundAnimator>>();

    // 발현이 트리거되어 소멸 연출 중이지만 아직 NotifyManifestFinished가 호출되지 않은 인스턴스들.
    // activeGroundMarksByGroup에서는 이미 제거된 상태이므로, ClearAllConstellationGroundMarks가
    // 이 목록도 함께 정리해야 던전 이탈 시 연출 도중이던 마크가 풀로 반환되지 않고 고아로 남는 것을 막는다.
    private readonly List<TreeStarMarkGroundAnimator> pendingManifestInstances = new List<TreeStarMarkGroundAnimator>();

    // 발현이 요청됐지만 그룹의 별 중 아직 낙하(Spawn) 연출 중인 것이 있어, 전부 안착하기를 기다리는 그룹들.
    // 마지막 별 표식 나무를 베는 순간 발현이 요청되므로 마지막 별은 거의 항상 이 대기를 거친다.
    private readonly Dictionary<int, List<TreeStarMarkGroundAnimator>> landingWaitMarksByGroup = new Dictionary<int, List<TreeStarMarkGroundAnimator>>();

    // 그룹별로 아직 사라짐 연출이 끝나지 않은 그라운드 마크 개수. 0이 되면(그룹의 모든 별이 사라지면)
    // ConstellationManifestReadyEvent를 발생시켜 그때 비로소 별자리 VFX(레이저)를 시작하게 한다.
    private readonly Dictionary<int, int> pendingManifestCountByGroup = new Dictionary<int, int>();

    // 그룹의 별이 전부 안착 -> 전부 사라짐 연출을 마쳤을 때 발생 - InDungeonObjectManager가 구독해 별자리 레이저를 쏜다.
    public event Action<int> ConstellationManifestReadyEvent;

    // 점선 노드 매칭 허용 거리(제곱, 1m 이내). 벌목된 나무의 top 좌표와 점선 노드(같은 top 좌표)를 짝짓는다.
    private const float DottedLineNodeMatchMaxSqrDistance = 1.0f;

    [Header("Constellation Star Appear Aura")]
    // 별이 등장할 때 한 번 터지는 아우라 - 보석 원목이 땅에 놓일 때 쓰는 아우라 프리셋(Gold/Diamond/RainbowPreset)을
    // 그대로 빌려 쓴다. 원목 쪽(LogItemController) 풀과 섞이지 않도록 여기서 별도 풀로 관리한다.
    [SerializeField] private ItemAuraEffectController starAppearAuraPrefab;
    [SerializeField] private int starAppearAuraPoolDefaultCapacity = 4;
    [SerializeField] private int starAppearAuraPoolMaxSize = 16;

    private IObjectPool<ItemAuraEffectController> starAppearAuraPool;

    // 초기화 시 미리 만들어 둘 등장 아우라 수 - 별은 보통 한 번에 하나씩 등장하므로 몇 개면 충분하다.
    private const int StarAppearAuraPrewarmCount = 2;

    // 재생 중인 등장 아우라와 끝나는 시각 - 버스트가 끝나면 Update에서 풀로 돌려놓는다.
    private struct ActiveStarAppearAura
    {
        public ItemAuraEffectController aura;
        public float endTime;
    }

    private readonly List<ActiveStarAppearAura> activeStarAppearAuras = new List<ActiveStarAppearAura>(8);

    [Header("Constellation Dotted Line")]
    [SerializeField] private ConstellationDottedLine constellationDottedLinePrefab;
    [SerializeField] private int constellationDottedLinePoolDefaultCapacity = 8;
    // 별자리 그룹 하나당 점선 1개를 쓰므로, 맵이 커서 그룹 수가 이 값을 넘으면 던전 이탈 시 넘친 점선이
    // 파괴되고 다음 런에서 다시 생성된다. 풀은 미리 만들어두지 않으므로 넉넉히 잡아도 메모리 비용은 없다.
    [SerializeField] private int constellationDottedLinePoolMaxSize = 128;

    private IObjectPool<ConstellationDottedLine> constellationDottedLinePool;

    // 별자리 그룹 하나의 점선 정보. 점선은 "별자리 발현" 특성이 있을 때만 보이지만, 특성은 이어하기 로드처럼
    // 나무 스폰보다 늦게 적용될 수도 있으므로, 표시 여부와 무관하게 노드 좌표와 벌목 여부를 기록해 두었다가
    // 보이게 되는 순간 그대로 그린다.
    private sealed class DottedLineGroup
    {
        public readonly List<Vector3> nodes = new List<Vector3>(8);   // 정돈된 순서의 노드(나무 top) 좌표
        public readonly List<bool> felled = new List<bool>(8);        // 노드별 벌목 여부(푸른 큰 별 노드)
        public ConstellationDottedLine line;                           // 표시 중일 때만 존재
    }

    private readonly Dictionary<int, DottedLineGroup> dottedLineGroups = new Dictionary<int, DottedLineGroup>();

    // 그룹을 거둘 때 반납된 기록 - 다음 런의 그룹이 재사용해 런마다 새로 할당하지 않는다.
    private readonly Stack<DottedLineGroup> spareDottedLineGroups = new Stack<DottedLineGroup>();

    // 점선 표시 여부 - InDungeonObjectManager가 "별자리 발현" 특성 해금 상태에 맞춰 설정한다.
    private bool bConstellationDottedLinesVisible = false;

    // 점선을 그릴 때 노드(좌표 + 벌목 여부)를 한 번에 넘기기 위한 버퍼 - SetNodes가 내부로 복사하므로 공유해도 안전하다.
    private readonly List<ConstellationNode> dottedLineNodeBuffer = new List<ConstellationNode>(8);

    [Header("Shooting Star")]
    [SerializeField] private ShootingStarVFX shootingStarVfxPrefab;
    [SerializeField] private int shootingStarVfxPoolDefaultCapacity = 4;
    [SerializeField] private int shootingStarVfxPoolMaxSize = 16;

    private IObjectPool<ShootingStarVFX> shootingStarVfxPool;

    [Header("Spore Explosion")]
    [SerializeField] private SporeExplosionVFX sporeExplosionVfxPrefab;
    [SerializeField] private int sporeExplosionVfxPoolDefaultCapacity = 8;
    [SerializeField] private int sporeExplosionVfxPoolMaxSize = 64;

    private IObjectPool<SporeExplosionVFX> sporeExplosionVfxPool;

    [Header("Fire Explosion")]
    [SerializeField] private FireExplosionVFX fireExplosionVfxPrefab;
    [SerializeField] private int fireExplosionVfxPoolDefaultCapacity = 8;
    [SerializeField] private int fireExplosionVfxPoolMaxSize = 64;

    private IObjectPool<FireExplosionVFX> fireExplosionVfxPool;

    [Header("Tree Transform (보석 단계 변환)")]
    [SerializeField] private TreeTransformVFX treeTransformVfxPrefab;
    [SerializeField] private int treeTransformVfxPoolDefaultCapacity = 4;
    [SerializeField] private int treeTransformVfxPoolMaxSize = 32;

    // Top 루트가 실제 나무 꼭대기보다 약간 위라, 이펙트의 원 중심을 조금 내려 맞추기 위한 오프셋
    [SerializeField] private float treeTransformVfxYOffset = -0.25f;

    private IObjectPool<TreeTransformVFX> treeTransformVfxPool;

    [Header("Tree Heat Indicator (열기 방출 예고)")]
    [SerializeField] private Material treeHeatIndicatorMaterial;
    // 열기 판정은 나무를 둘러싼 인접 8타일(3x3)이다. 등각 타일(1 x 0.5) 기준으로 그 마름모의 꼭짓점을
    // 지나는 타원(가로 반지름 1.5, 세로 0.75)이면 판정 범위 전체를 덮는다.
    [SerializeField] private float treeHeatIndicatorRadius = 1.5f;
    // 방출 몇 초 전부터 보여줄지. 카운트다운(3~5초)보다 길게 잡으면 카운트다운 내내 보인다.
    [SerializeField] private float treeHeatIndicatorLeadTime = 1.5f;
    // 캐릭터 도끼 인디케이터(Indicators 레이어, order 0)가 위에 그려지도록 한 칸 아래에 둔다.
    [SerializeField] private int treeHeatIndicatorSortingOrder = -1;
    [SerializeField] private int treeHeatIndicatorPoolDefaultCapacity = 4;
    [SerializeField] private int treeHeatIndicatorPoolMaxSize = 32;

    // 캐릭터 도끼 인디케이터(RadiusIndicator)와 같은 정렬 레이어
    private const string IndicatorSortingLayerName = "Indicators";

    private IObjectPool<TreeHeatIndicator> treeHeatIndicatorPool;

    [Header("Manifestation Brand Star Wrap (낙인 나무를 감싸는 별)")]
    [SerializeField] private PresentationLayer.VFX.VFX_BrandStarWrap brandStarWrapPrefab;
    [SerializeField] private int brandStarWrapPoolDefaultCapacity = 8;
    [SerializeField] private int brandStarWrapPoolMaxSize = 32;
    // 화면 가득 낙인 나무일 때도 부하가 커지지 않도록 동시에 별이 감싸는 나무 수의 상한
    [SerializeField] private int brandStarWrapMaxActive = 20;

    private IObjectPool<PresentationLayer.VFX.VFX_BrandStarWrap> brandStarWrapPool;
    private readonly List<PresentationLayer.VFX.VFX_BrandStarWrap> activeBrandStarWraps = new List<PresentationLayer.VFX.VFX_BrandStarWrap>(32);

    // 초기화 시 미리 만들어 둘 별 감싸기 수 - 광선 한 번에 여러 나무가 낙인이 찍히므로, 첫 광선 도중에
    // 인스턴스(자식 오브젝트 2개 + 메쉬 2개) 생성이 몰리지 않도록 몇 개를 미리 만든다.
    private const int BrandStarWrapPrewarmCount = 4;

    public void Initialize()
    {
        if (vfxComponent != null)
            vfxComponent.Initialize();

        if (treeStarMarkGroundPool == null && treeStarMarkGroundPrefab != null)
        {
            treeStarMarkGroundPool = new ObjectPool<TreeStarMarkGroundAnimator>(
                createFunc: CreateTreeStarMarkGround,
                actionOnGet: OnGetTreeStarMarkGround,
                actionOnRelease: OnReleaseTreeStarMarkGround,
                actionOnDestroy: OnDestroyTreeStarMarkGround,
                collectionCheck: true,
                defaultCapacity: treeStarMarkGroundPoolDefaultCapacity,
                maxSize: treeStarMarkGroundPoolMaxSize
            );
        }

        if (starAppearAuraPool == null && starAppearAuraPrefab != null)
        {
            starAppearAuraPool = new ObjectPool<ItemAuraEffectController>(
                createFunc: CreateStarAppearAura,
                actionOnGet: OnGetStarAppearAura,
                actionOnRelease: OnReleaseStarAppearAura,
                actionOnDestroy: OnDestroyStarAppearAura,
                collectionCheck: true,
                defaultCapacity: starAppearAuraPoolDefaultCapacity,
                maxSize: starAppearAuraPoolMaxSize
            );

            PrewarmStarAppearAuras();
        }

        if (constellationDottedLinePool == null && constellationDottedLinePrefab != null)
        {
            constellationDottedLinePool = new ObjectPool<ConstellationDottedLine>(
                createFunc: CreateConstellationDottedLine,
                actionOnGet: OnGetConstellationDottedLine,
                actionOnRelease: OnReleaseConstellationDottedLine,
                actionOnDestroy: OnDestroyConstellationDottedLine,
                collectionCheck: true,
                defaultCapacity: constellationDottedLinePoolDefaultCapacity,
                maxSize: constellationDottedLinePoolMaxSize
            );
        }

        if (brandStarWrapPool == null && brandStarWrapPrefab != null)
        {
            brandStarWrapPool = new ObjectPool<PresentationLayer.VFX.VFX_BrandStarWrap>(
                createFunc: CreateBrandStarWrap,
                actionOnGet: OnGetBrandStarWrap,
                actionOnRelease: OnReleaseBrandStarWrap,
                actionOnDestroy: OnDestroyBrandStarWrap,
                collectionCheck: true,
                defaultCapacity: brandStarWrapPoolDefaultCapacity,
                maxSize: brandStarWrapPoolMaxSize
            );

            PrewarmBrandStarWraps();
        }

        if (shootingStarVfxPool == null && shootingStarVfxPrefab != null)
        {
            shootingStarVfxPool = new ObjectPool<ShootingStarVFX>(
                createFunc: CreateShootingStarVfx,
                actionOnGet: OnGetShootingStarVfx,
                actionOnRelease: OnReleaseShootingStarVfx,
                actionOnDestroy: OnDestroyShootingStarVfx,
                collectionCheck: true,
                defaultCapacity: shootingStarVfxPoolDefaultCapacity,
                maxSize: shootingStarVfxPoolMaxSize
            );
        }

        if (sporeExplosionVfxPool == null && sporeExplosionVfxPrefab != null)
        {
            sporeExplosionVfxPool = new ObjectPool<SporeExplosionVFX>(
                createFunc: CreateSporeExplosionVfx,
                actionOnGet: OnGetSporeExplosionVfx,
                actionOnRelease: OnReleaseSporeExplosionVfx,
                actionOnDestroy: OnDestroySporeExplosionVfx,
                collectionCheck: true,
                defaultCapacity: sporeExplosionVfxPoolDefaultCapacity,
                maxSize: sporeExplosionVfxPoolMaxSize
            );
        }

        if (fireExplosionVfxPool == null && fireExplosionVfxPrefab != null)
        {
            fireExplosionVfxPool = new ObjectPool<FireExplosionVFX>(
                createFunc: CreateFireExplosionVfx,
                actionOnGet: OnGetFireExplosionVfx,
                actionOnRelease: OnReleaseFireExplosionVfx,
                actionOnDestroy: OnDestroyFireExplosionVfx,
                collectionCheck: true,
                defaultCapacity: fireExplosionVfxPoolDefaultCapacity,
                maxSize: fireExplosionVfxPoolMaxSize
            );
        }

        if (treeTransformVfxPool == null && treeTransformVfxPrefab != null)
        {
            treeTransformVfxPool = new ObjectPool<TreeTransformVFX>(
                createFunc: CreateTreeTransformVfx,
                actionOnGet: OnGetTreeTransformVfx,
                actionOnRelease: OnReleaseTreeTransformVfx,
                actionOnDestroy: OnDestroyTreeTransformVfx,
                collectionCheck: true,
                defaultCapacity: treeTransformVfxPoolDefaultCapacity,
                maxSize: treeTransformVfxPoolMaxSize
            );
        }

        if (treeHeatIndicatorPool == null && treeHeatIndicatorMaterial != null)
        {
            treeHeatIndicatorPool = new ObjectPool<TreeHeatIndicator>(
                createFunc: CreateTreeHeatIndicator,
                actionOnGet: OnGetTreeHeatIndicator,
                actionOnRelease: OnReleaseTreeHeatIndicator,
                actionOnDestroy: OnDestroyTreeHeatIndicator,
                collectionCheck: true,
                defaultCapacity: treeHeatIndicatorPoolDefaultCapacity,
                maxSize: treeHeatIndicatorPoolMaxSize
            );
        }
    }

    /// <summary>
    /// 나무가 열기 카운트다운을 시작하면 방출 범위를 바닥에 예고합니다. 방출 _delay초 전에 붙지만
    /// 실제로는 마지막 treeHeatIndicatorLeadTime초 동안만 보이며, 나무가 먼저 죽으면 스스로 사라집니다.
    /// </summary>
    public void PlayTreeHeatIndicator(TreeObj _tree, float _delay)
    {
        if (treeHeatIndicatorPool == null || _tree == null) return;

        float showDuration = Mathf.Min(Mathf.Max(0.01f, treeHeatIndicatorLeadTime), _delay);
        float hiddenDuration = Mathf.Max(0f, _delay - showDuration);

        TreeHeatIndicator instance = treeHeatIndicatorPool.Get();
        instance.Play(_tree, hiddenDuration, showDuration, treeHeatIndicatorRadius);
    }

    private TreeHeatIndicator CreateTreeHeatIndicator()
    {
        GameObject indicatorObject = new GameObject("TreeHeatIndicator");
        indicatorObject.transform.SetParent(transform, false);
        indicatorObject.AddComponent<SpriteRenderer>();
        TreeHeatIndicator instance = indicatorObject.AddComponent<TreeHeatIndicator>();
        instance.Initialize(
            treeHeatIndicatorMaterial,
            SortingLayer.NameToID(IndicatorSortingLayerName),
            treeHeatIndicatorSortingOrder,
            ReleaseTreeHeatIndicator);
        return instance;
    }

    private void ReleaseTreeHeatIndicator(TreeHeatIndicator _instance)
    {
        treeHeatIndicatorPool?.Release(_instance);
    }

    private void OnGetTreeHeatIndicator(TreeHeatIndicator _instance)
    {
        _instance.gameObject.SetActive(true);
    }

    private void OnReleaseTreeHeatIndicator(TreeHeatIndicator _instance)
    {
        _instance.gameObject.SetActive(false);
    }

    private void OnDestroyTreeHeatIndicator(TreeHeatIndicator _instance)
    {
        if (_instance != null) Destroy(_instance.gameObject);
    }

    /// <summary>
    /// 나무가 보석 단계로 변할 때의 VFX를 재생합니다.
    /// 스프라이트 피벗이 이펙트의 원 중심에 맞춰져 있어, 나무 Top 위치에 그대로 놓으면 정렬됩니다.
    /// parent를 두지 않으므로 나무가 풀로 반환되어도 연출이 끊기지 않습니다.
    /// </summary>
    public void PlayTreeTransformVFX(TreeVisualComponent _visual, int _gemStage, int _sortingOrderOffset = 100)
    {
        if (treeTransformVfxPool == null || _visual == null) return;

        TreeTransformVFX instance = treeTransformVfxPool.Get();
        instance.transform.position = _visual.GetTopRootPosition() + new Vector3(0f, treeTransformVfxYOffset, 0f);
        instance.Play(_sortingOrderOffset, _gemStage);
    }

    private TreeTransformVFX CreateTreeTransformVfx()
    {
        TreeTransformVFX instance = Instantiate(treeTransformVfxPrefab, transform);
        instance.SetPool(treeTransformVfxPool);
        return instance;
    }

    private void OnGetTreeTransformVfx(TreeTransformVFX _instance)
    {
        _instance.gameObject.SetActive(true);
    }

    private void OnReleaseTreeTransformVfx(TreeTransformVFX _instance)
    {
        _instance.gameObject.SetActive(false);
    }

    private void OnDestroyTreeTransformVfx(TreeTransformVFX _instance)
    {
        if (_instance != null) Destroy(_instance.gameObject);
    }

    /// <summary>
    /// 낙인이 찍힌 나무를 별들이 감싸고 도는 유지 이펙트를 시작합니다. 동시 개수 상한을 넘거나 풀/프리팹이 없으면 null을
    /// 반환합니다. 반환된 인스턴스는 낙인이 풀릴 때(나무가 풀로 돌아갈 때) ReleaseBrandStarWrap으로 회수해야 합니다.
    /// </summary>
    public PresentationLayer.VFX.VFX_BrandStarWrap BeginBrandStarWrap(TreeVisualComponent _visual)
    {
        if (brandStarWrapPool == null || _visual == null) return null;
        if (activeBrandStarWraps.Count >= brandStarWrapMaxActive) return null;

        PresentationLayer.VFX.VFX_BrandStarWrap _wrap = brandStarWrapPool.Get();
        activeBrandStarWraps.Add(_wrap);
        _wrap.Begin(_visual, TreeSortingLayerName);
        return _wrap;
    }

    /// <summary>
    /// 별 감싸기를 즉시 회수합니다. 감싸던 나무가 이미 사라지는 중이라 퇴장 연출은 보이지 않으므로 연출 없이 반환합니다.
    /// </summary>
    public void ReleaseBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _wrap)
    {
        if (_wrap != null) _wrap.ForceRelease();
    }

    /// <summary>
    /// 던전 이탈 등으로 재생 중인 별 감싸기를 연출 없이 전부 풀에 되돌립니다.
    /// </summary>
    public void ReleaseAllBrandStarWraps()
    {
        // ForceRelease -> ReturnToPoolEvent -> OnBrandStarWrapReturned가 순회 중인 리스트에서 원소를 빼므로 뒤에서부터 돈다.
        for (int i = activeBrandStarWraps.Count - 1; i >= 0; i--)
        {
            if (i >= activeBrandStarWraps.Count) continue;

            PresentationLayer.VFX.VFX_BrandStarWrap _wrap = activeBrandStarWraps[i];
            if (_wrap != null) _wrap.ForceRelease();
        }
        activeBrandStarWraps.Clear();
    }

    // ObjectPool은 미리 만들어두지 않으므로 초기화 시점에 몇 개를 꺼냈다가 돌려놓는다.
    // (꺼낸 동안 동시에 들고 있어야 서로 다른 인스턴스가 생성된다. Begin 전이라 아무것도 그리지 않는다)
    private void PrewarmBrandStarWraps()
    {
        int _count = Mathf.Min(BrandStarWrapPrewarmCount, brandStarWrapPoolMaxSize);
        if (0 >= _count) return;

        PresentationLayer.VFX.VFX_BrandStarWrap[] _prewarmed = new PresentationLayer.VFX.VFX_BrandStarWrap[_count];
        for (int i = 0; i < _prewarmed.Length; i++)
        {
            _prewarmed[i] = brandStarWrapPool.Get();
        }
        for (int i = 0; i < _prewarmed.Length; i++)
        {
            brandStarWrapPool.Release(_prewarmed[i]);
        }
    }

    private PresentationLayer.VFX.VFX_BrandStarWrap CreateBrandStarWrap()
    {
        PresentationLayer.VFX.VFX_BrandStarWrap _instance = Instantiate(brandStarWrapPrefab, transform);
        _instance.ReturnToPoolEvent -= OnBrandStarWrapReturned;
        _instance.ReturnToPoolEvent += OnBrandStarWrapReturned;
        return _instance;
    }

    private void OnGetBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
    {
        _instance.gameObject.SetActive(true);
    }

    private void OnReleaseBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
    {
        _instance.gameObject.SetActive(false);
    }

    private void OnDestroyBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
    {
        if (_instance != null)
        {
            _instance.ReturnToPoolEvent -= OnBrandStarWrapReturned;
            Destroy(_instance.gameObject);
        }
    }

    // 별 감싸기가 반환될 때(강제 회수, 또는 감싸던 비주얼이 사라져 스스로 반환) 활성 목록에서 빼고 풀에 되돌린다.
    private void OnBrandStarWrapReturned(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
    {
        activeBrandStarWraps.Remove(_instance);
        brandStarWrapPool?.Release(_instance);
    }

    /// <summary>
    /// 별똥별 VFX를 스폰합니다. 하늘 위에서 낙하 후 착지 시 _onLanded 콜백을 호출합니다.
    /// Instantiate/Destroy 대신 ObjectPool로 재사용됩니다.
    /// </summary>
    public void PlayShootingStarVFX(Vector3 _landingPos, int _sortingOrder, Action _onLanded)
    {
        if (shootingStarVfxPool == null)
        {
            _onLanded?.Invoke();
            return;
        }

        ShootingStarVFX instance = shootingStarVfxPool.Get();
        instance.Begin(_landingPos, _sortingOrder, _onLanded);
    }

    private ShootingStarVFX CreateShootingStarVfx()
    {
        ShootingStarVFX instance = Instantiate(shootingStarVfxPrefab, transform);
        instance.SetPool(shootingStarVfxPool);
        return instance;
    }

    private void OnGetShootingStarVfx(ShootingStarVFX _instance)
    {
        _instance.gameObject.SetActive(true);
    }

    private void OnReleaseShootingStarVfx(ShootingStarVFX _instance)
    {
        _instance.gameObject.SetActive(false);
    }

    private void OnDestroyShootingStarVfx(ShootingStarVFX _instance)
    {
        if (_instance != null) Destroy(_instance.gameObject);
    }

    /// <summary>
    /// 포자 폭발 VFX를 스폰합니다. 별도 프리팹 없이 코드에서 직접 생성하던 기존 방식 대신,
    /// 프리팹을 ObjectPool로 재사용합니다.
    /// </summary>
    public void PlaySporeExplosionVFX(Vector3 _position, Vector2 _outwardDirection, int _sortingOrderOffset = 100)
    {
        if (sporeExplosionVfxPool == null) return;

        SporeExplosionVFX instance = sporeExplosionVfxPool.Get();
        instance.transform.position = _position;
        instance.Play(_sortingOrderOffset, _outwardDirection);
    }

    private SporeExplosionVFX CreateSporeExplosionVfx()
    {
        SporeExplosionVFX instance = Instantiate(sporeExplosionVfxPrefab, transform);
        instance.SetPool(sporeExplosionVfxPool);
        return instance;
    }

    private void OnGetSporeExplosionVfx(SporeExplosionVFX _instance)
    {
        _instance.gameObject.SetActive(true);
    }

    private void OnReleaseSporeExplosionVfx(SporeExplosionVFX _instance)
    {
        _instance.gameObject.SetActive(false);
    }

    private void OnDestroySporeExplosionVfx(SporeExplosionVFX _instance)
    {
        if (_instance != null) Destroy(_instance.gameObject);
    }

    /// <summary>
    /// 과열 강화 ShockWave 폭발 VFX를 스폰합니다. 포자 폭발 VFX와 완전히 동일한 방식(프리팹 + ObjectPool)입니다.
    /// </summary>
    public void PlayFireExplosionVFX(Vector3 _position, Vector2 _outwardDirection, int _sortingOrderOffset = 100)
    {
        if (fireExplosionVfxPool == null) return;

        FireExplosionVFX instance = fireExplosionVfxPool.Get();
        instance.transform.position = _position;
        instance.Play(_sortingOrderOffset, _outwardDirection);
    }

    private FireExplosionVFX CreateFireExplosionVfx()
    {
        FireExplosionVFX instance = Instantiate(fireExplosionVfxPrefab, transform);
        instance.SetPool(fireExplosionVfxPool);
        return instance;
    }

    private void OnGetFireExplosionVfx(FireExplosionVFX _instance)
    {
        _instance.gameObject.SetActive(true);
    }

    private void OnReleaseFireExplosionVfx(FireExplosionVFX _instance)
    {
        _instance.gameObject.SetActive(false);
    }

    private void OnDestroyFireExplosionVfx(FireExplosionVFX _instance)
    {
        if (_instance != null) Destroy(_instance.gameObject);
    }

    /// <summary>
    /// 별 표식 나무가 죽은 자리에 TreeStarMark_Ground 마크(별)를 스폰합니다. Instantiate/Destroy 대신
    /// ObjectPool로 재사용되며, sortingOrder는 죽은 나무의 topRenderer 값을 그대로 물려받습니다.
    /// HDR 강도는 TreeStarMarkGroundAnimator 자체 인스펙터 값을 사용합니다.
    /// 소속 그룹(_groupId)의 별자리 발현이 요청되기 전까지는 사라지지 않고 Loop 재생됩니다.
    /// </summary>
    public void PlayConstellationGroundMarkVFX(Vector3 _position, int _sortingOrder, int _groupId)
    {
        if (treeStarMarkGroundPool == null) return;

        TreeStarMarkGroundAnimator _instance = treeStarMarkGroundPool.Get();
        _instance.transform.position = _position;
        _instance.SetSortingOrder(_sortingOrder);
        _instance.SetGroupId(_groupId);
        _instance.Play();

        PlayStarAppearAura(_instance);

        if (!activeGroundMarksByGroup.TryGetValue(_groupId, out List<TreeStarMarkGroundAnimator> _list))
        {
            _list = new List<TreeStarMarkGroundAnimator>();
            activeGroundMarksByGroup[_groupId] = _list;
        }
        _list.Add(_instance);
    }

    /// <summary>
    /// 그룹의 별자리 발현이 확정되면 호출됩니다. 그룹의 별이 전부 땅에 안착할 때까지 기다렸다가(방금 벤
    /// 마지막 별은 아직 떨어지는 중이다), 전부 한꺼번에 회전하며 작아지는 사라짐 연출을 재생시킵니다.
    /// 모든 별의 사라짐 연출이 끝나면 ConstellationManifestReadyEvent가 발생해 그때 별자리 VFX가 시작됩니다.
    /// 그룹에 별이 하나도 없으면(스킬 해금 전에 벌목된 경우 등) 기다릴 대상이 없으므로 즉시 이벤트를 보냅니다.
    /// </summary>
    public void BeginConstellationGroundMarkManifest(int _groupId)
    {
        // 여기서 조용히 리턴하면 ConstellationManifestReadyEvent가 영원히 발생하지 않아 발현이 멈춰버린다.
        if (!activeGroundMarksByGroup.TryGetValue(_groupId, out List<TreeStarMarkGroundAnimator> _list) || _list.Count == 0)
        {
            activeGroundMarksByGroup.Remove(_groupId);
            ConstellationManifestReadyEvent?.Invoke(_groupId);
            return;
        }

        activeGroundMarksByGroup.Remove(_groupId);
        landingWaitMarksByGroup[_groupId] = _list;

        // 이미 전부 안착해 있으면 바로 시작, 아니면 마지막 별의 LandedEvent(OnGroundMarkLanded)에서 다시 확인한다.
        TryStartGroupManifest(_groupId);
    }

    // 그룹의 별이 전부 안착했으면 한꺼번에 사라짐 연출을 시작한다.
    private void TryStartGroupManifest(int _groupId)
    {
        if (!landingWaitMarksByGroup.TryGetValue(_groupId, out List<TreeStarMarkGroundAnimator> _list)) return;

        for (int i = 0; i < _list.Count; i++)
        {
            if (!_list[i].IsLanded) return;
        }

        landingWaitMarksByGroup.Remove(_groupId);
        pendingManifestCountByGroup[_groupId] = _list.Count;

        for (int i = 0; i < _list.Count; i++)
        {
            // 연출이 끝나기 전에 던전을 나가는 경우를 대비해 pendingManifestInstances로 계속 추적해야
            // ClearAllConstellationGroundMarks가 강제로 회수할 수 있다.
            pendingManifestInstances.Add(_list[i]);
            _list[i].PlayManifestEffect();
        }
    }

    /// <summary>
    /// 그룹 구분 없이 현재 살아있는 그라운드 마크를 전부 즉시 회수합니다. 던전을 나가거나(ClearObjManager)
    /// 새 스테이지로 나무를 재생성(SpawnInitialTrees)할 때 호출되어야 합니다 - Stage3TreeGenerationStrategySO의
    /// groupId 카운터가 매번 0부터 다시 시작되므로, 이전 런에서 미발현 상태로 남은 마크를 여기서 정리해두지
    /// 않으면 다음 런의 그룹이 같은 groupId를 받았을 때 서로 다른 런의 마크가 뒤섞이게 된다.
    /// 대기 중인 마크, 안착을 기다리는 마크, 사라짐 연출 중인 마크를 모두 강제 회수한다.
    /// </summary>
    public void ClearAllConstellationGroundMarks()
    {
        foreach (List<TreeStarMarkGroundAnimator> _list in activeGroundMarksByGroup.Values)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                _list[i].ForceReturnToPool();
            }
        }
        activeGroundMarksByGroup.Clear();

        foreach (List<TreeStarMarkGroundAnimator> _list in landingWaitMarksByGroup.Values)
        {
            for (int i = 0; i < _list.Count; i++)
            {
                _list[i].ForceReturnToPool();
            }
        }
        landingWaitMarksByGroup.Clear();

        for (int i = 0; i < pendingManifestInstances.Count; i++)
        {
            pendingManifestInstances[i].ForceReturnToPool();
        }
        pendingManifestInstances.Clear();

        // 강제 회수는 OnGroundMarkManifestFinished를 거치지 않으므로, 남아있던 카운트도 직접 정리한다.
        // 이 시점에는 ConstellationManifestReadyEvent를 발생시키지 않는다(던전을 나가는 중이므로
        // 별자리 VFX/데미지를 시작하면 안 된다).
        pendingManifestCountByGroup.Clear();

        // 재생 중이던 별 등장 아우라도 함께 회수한다.
        for (int i = 0; i < activeStarAppearAuras.Count; i++)
        {
            starAppearAuraPool?.Release(activeStarAppearAuras[i].aura);
        }
        activeStarAppearAuras.Clear();
    }

    // 별이 등장하는 순간 별 중심에서 보석 원목 아우라를 한 번 터뜨린다(원목처럼 별 뒤에 그린다).
    private void PlayStarAppearAura(TreeStarMarkGroundAnimator _star)
    {
        if (starAppearAuraPool == null) return;

        ItemAuraEffectController _aura = starAppearAuraPool.Get();
        _aura.transform.position = _star.VisualWorldPosition;

        // 프리셋 프리팹은 Default 레이어(맨 뒤)라 맞추지 않으면 가려진다. 별 바로 뒤에 그린다.
        _aura.SetSortingLayer(_star.SortingLayerID);
        _aura.SetSortingOrder(_star.SortingOrder - 1);
        _aura.Play();

        activeStarAppearAuras.Add(new ActiveStarAppearAura { aura = _aura, endTime = Time.time + _aura.BurstDuration });
    }

    private void Update()
    {
        // 버스트가 끝난 등장 아우라를 풀로 돌려놓는다(끝나면 아우라가 스스로 렌더러를 끄므로 보이지 않는 상태다).
        for (int i = activeStarAppearAuras.Count - 1; i >= 0; i--)
        {
            if (Time.time < activeStarAppearAuras[i].endTime) continue;

            starAppearAuraPool?.Release(activeStarAppearAuras[i].aura);
            int _last = activeStarAppearAuras.Count - 1;
            activeStarAppearAuras[i] = activeStarAppearAuras[_last];
            activeStarAppearAuras.RemoveAt(_last);
        }
    }

    // ObjectPool은 미리 만들어두지 않으므로, 첫 별이 등장하는 프레임에 아우라 생성이 몰리지 않도록 초기화 시점에
    // 몇 개를 미리 꺼냈다가 돌려놓는다. (꺼낸 동안 동시에 들고 있어야 서로 다른 인스턴스가 생성된다)
    private void PrewarmStarAppearAuras()
    {
        int _count = Mathf.Min(StarAppearAuraPrewarmCount, starAppearAuraPoolMaxSize);
        if (0 >= _count) return;

        ItemAuraEffectController[] _prewarmed = new ItemAuraEffectController[_count];
        for (int i = 0; i < _prewarmed.Length; i++)
        {
            _prewarmed[i] = starAppearAuraPool.Get();
        }
        for (int i = 0; i < _prewarmed.Length; i++)
        {
            starAppearAuraPool.Release(_prewarmed[i]);
        }
    }

    private ItemAuraEffectController CreateStarAppearAura()
    {
        return Instantiate(starAppearAuraPrefab, transform);
    }

    private void OnGetStarAppearAura(ItemAuraEffectController _aura)
    {
        _aura.gameObject.SetActive(true);
    }

    private void OnReleaseStarAppearAura(ItemAuraEffectController _aura)
    {
        _aura.Stop();
        _aura.gameObject.SetActive(false);
    }

    private void OnDestroyStarAppearAura(ItemAuraEffectController _aura)
    {
        if (_aura != null) Destroy(_aura.gameObject);
    }

    private TreeStarMarkGroundAnimator CreateTreeStarMarkGround()
    {
        TreeStarMarkGroundAnimator _instance = Instantiate(treeStarMarkGroundPrefab, transform);
        _instance.SetPool(treeStarMarkGroundPool);

        // 이벤트 바인딩 (생성 시 한 번만) - LootManager.CreateLootItem과 동일한 패턴
        _instance.ManifestFinishedEvent -= OnGroundMarkManifestFinished;
        _instance.ManifestFinishedEvent += OnGroundMarkManifestFinished;
        _instance.LandedEvent -= OnGroundMarkLanded;
        _instance.LandedEvent += OnGroundMarkLanded;
        _instance.ManifestStartedEvent -= OnGroundMarkManifestStarted;
        _instance.ManifestStartedEvent += OnGroundMarkManifestStarted;

        return _instance;
    }

    // 별이 실제로 회전하며 작아지기 시작한 순간 - 별자리 발현 직전 별 사라짐 사운드(Stardisappear1/2 중 랜덤)를
    // 그 별 자리에서 울린다. 별마다 사라짐 시작 시각이 조금씩 달라(랜덤 지연) 소리도 자연스럽게 흩어진다.
    private void OnGroundMarkManifestStarted(TreeStarMarkGroundAnimator _instance)
    {
        Sound.Play(SoundID.Stardisappear, _instance.VisualWorldPosition);
    }

    // 별이 낙하를 마치고 안착했을 때 - 그 그룹이 안착 대기 중이면 전부 안착했는지 다시 확인한다.
    private void OnGroundMarkLanded(TreeStarMarkGroundAnimator _instance)
    {
        if (landingWaitMarksByGroup.ContainsKey(_instance.GroupId))
        {
            TryStartGroupManifest(_instance.GroupId);
        }
    }

    // 그라운드 마크의 사라짐 연출이 끝났을 때 호출되어 풀로 반환하고, 그룹의 남은 개수를 갱신한다.
    // 그룹의 모든 별이 연출을 마치면 ConstellationManifestReadyEvent를 발생시켜 별자리 VFX를 시작하게 한다.
    private void OnGroundMarkManifestFinished(TreeStarMarkGroundAnimator _instance)
    {
        pendingManifestInstances.Remove(_instance);
        int _groupId = _instance.GroupId;
        _instance.ForceReturnToPool();

        if (!pendingManifestCountByGroup.TryGetValue(_groupId, out int _remaining)) return;

        _remaining--;
        if (_remaining <= 0)
        {
            pendingManifestCountByGroup.Remove(_groupId);
            ConstellationManifestReadyEvent?.Invoke(_groupId);
        }
        else
        {
            pendingManifestCountByGroup[_groupId] = _remaining;
        }
    }

    private void OnGetTreeStarMarkGround(TreeStarMarkGroundAnimator _instance)
    {
        _instance.gameObject.SetActive(true);
    }

    private void OnReleaseTreeStarMarkGround(TreeStarMarkGroundAnimator _instance)
    {
        _instance.gameObject.SetActive(false);
    }

    private void OnDestroyTreeStarMarkGround(TreeStarMarkGroundAnimator _instance)
    {
        if (_instance != null)
        {
            _instance.ManifestFinishedEvent -= OnGroundMarkManifestFinished;
            _instance.LandedEvent -= OnGroundMarkLanded;
            _instance.ManifestStartedEvent -= OnGroundMarkManifestStarted;
            Destroy(_instance.gameObject);
        }
    }

    /// <summary>
    /// 별자리 그룹의 별 표식 나무들(top 좌표)을 잇는 점선 이음선을 등록합니다. 점선은 "별자리 발현" 특성이
    /// 있을 때만 보이며(SetConstellationDottedLinesVisible), 지금 보이지 않더라도 등록해 두면 특성이 적용되는
    /// 순간 그려집니다. 픽셀 레이저와 같은 꼬임 풀기(UntanglePoints)로 정돈해 레이저와 같은 외곽선을 그립니다.
    /// 같은 그룹에 다시 호출되면 기존 등록을 거두고 새로 등록합니다.
    /// </summary>
    public void RegisterConstellationDottedLine(int _groupId, IReadOnlyList<Vector3> _points)
    {
        if (null == _points || 2 > _points.Count) return;

        RemoveConstellationDottedLine(_groupId);

        DottedLineGroup _group = 0 < spareDottedLineGroups.Count ? spareDottedLineGroups.Pop() : new DottedLineGroup();
        _group.nodes.Clear();
        _group.felled.Clear();
        _group.line = null;

        for (int i = 0; i < _points.Count; i++)
        {
            _group.nodes.Add(_points[i]);
        }
        PresentationLayer.VFX.ConstellationPixelLaser.UntanglePoints(_group.nodes, 0);

        for (int i = 0; i < _group.nodes.Count; i++)
        {
            _group.felled.Add(false);
        }

        dottedLineGroups[_groupId] = _group;

        if (bConstellationDottedLinesVisible) ShowDottedLine(_group);
    }

    /// <summary>
    /// 별 표식 나무가 벌목되었을 때 호출되어, 점선에서 그 나무(_worldPos와 가장 가까운 노드)를 푸른색 큰 별
    /// 노드로 기록합니다. 점선이 보이는 중이면 즉시 황금빛 ↔ 푸른빛 그라데이션으로 바뀌고, 숨겨져 있으면
    /// 나중에 보이게 될 때 반영됩니다.
    /// </summary>
    public void SetConstellationDottedLineNodeFelled(int _groupId, Vector3 _worldPos)
    {
        if (!dottedLineGroups.TryGetValue(_groupId, out DottedLineGroup _group)) return;

        int _bestIndex = -1;
        float _bestSqrDistance = DottedLineNodeMatchMaxSqrDistance;
        for (int i = 0; i < _group.nodes.Count; i++)
        {
            Vector2 _delta = _group.nodes[i] - _worldPos;
            float _sqrDistance = _delta.sqrMagnitude;
            if (_sqrDistance <= _bestSqrDistance)
            {
                _bestSqrDistance = _sqrDistance;
                _bestIndex = i;
            }
        }

        if (0 > _bestIndex) return;

        _group.felled[_bestIndex] = true;
        if (null != _group.line) _group.line.SetNodeAsBigStar(_bestIndex, true);
    }

    /// <summary>
    /// 그룹의 점선 등록을 거두고 표시 중이면 풀로 반환합니다. 별자리 VFX가 시작되거나(레이저가 점선을 대체),
    /// 발현 특성 없이 그룹의 별 표식 나무가 모두 벌목되었을 때 호출됩니다.
    /// </summary>
    public void RemoveConstellationDottedLine(int _groupId)
    {
        if (!dottedLineGroups.TryGetValue(_groupId, out DottedLineGroup _group)) return;

        dottedLineGroups.Remove(_groupId);
        HideDottedLine(_group);
        spareDottedLineGroups.Push(_group);
    }

    /// <summary>
    /// 점선 표시 여부를 바꿉니다. "별자리 발현" 특성이 적용/해제될 때 호출되며, 등록된 그룹 전체에 즉시 반영됩니다.
    /// </summary>
    public void SetConstellationDottedLinesVisible(bool _visible)
    {
        if (bConstellationDottedLinesVisible == _visible) return;
        bConstellationDottedLinesVisible = _visible;

        foreach (DottedLineGroup _group in dottedLineGroups.Values)
        {
            if (_visible) ShowDottedLine(_group);
            else HideDottedLine(_group);
        }
    }

    /// <summary>
    /// 그룹 구분 없이 등록된 점선을 전부 회수합니다. ClearAllConstellationGroundMarks와 같은
    /// 시점(던전 이탈/나무 재생성)에 호출되어야 다음 런의 같은 groupId와 뒤섞이지 않습니다.
    /// </summary>
    public void ClearAllConstellationDottedLines()
    {
        foreach (DottedLineGroup _group in dottedLineGroups.Values)
        {
            HideDottedLine(_group);
            spareDottedLineGroups.Push(_group);
        }
        dottedLineGroups.Clear();
    }

    private void ShowDottedLine(DottedLineGroup _group)
    {
        if (null != _group.line || null == constellationDottedLinePool) return;

        ConstellationDottedLine _line = constellationDottedLinePool.Get();
        // 점선 메쉬는 노드 월드 좌표를 그대로 정점으로 쓰므로, 인스턴스 자체는 월드 원점에 둔다.
        _line.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // 벌목된 노드(푸른 큰 별)까지 담아 메쉬를 한 번에 빌드한다. 좌표 설정 후 노드마다 SetNodeAsBigStar를
        // 부르면 그때마다 메쉬 전체를 다시 빌드하게 된다.
        dottedLineNodeBuffer.Clear();
        for (int i = 0; i < _group.nodes.Count; i++)
        {
            dottedLineNodeBuffer.Add(new ConstellationNode(_group.nodes[i], _group.felled[i]));
        }
        _line.SetNodes(dottedLineNodeBuffer, true);

        _group.line = _line;
    }

    private void HideDottedLine(DottedLineGroup _group)
    {
        if (null == _group.line) return;

        _group.line.ReturnToPool();
        _group.line = null;
    }

    private ConstellationDottedLine CreateConstellationDottedLine()
    {
        ConstellationDottedLine _instance = Instantiate(constellationDottedLinePrefab, transform);
        _instance.SetPool(constellationDottedLinePool);
        return _instance;
    }

    private void OnGetConstellationDottedLine(ConstellationDottedLine _instance)
    {
        _instance.gameObject.SetActive(true);
    }

    private void OnReleaseConstellationDottedLine(ConstellationDottedLine _instance)
    {
        _instance.gameObject.SetActive(false);
    }

    private void OnDestroyConstellationDottedLine(ConstellationDottedLine _instance)
    {
        if (_instance != null) Destroy(_instance.gameObject);
    }

    /// <summary>
    /// 나무 피격 VFX를 재생합니다. parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
    /// Top/Bottom 이펙트는 각각 설정된 컬러를 공유합니다.
    /// </summary>
    public void PlayTreeHitVFX(TreeVisualComponent _visual)
    {
        if (vfxComponent == null || _visual == null) return;

        ParticleColorSet topColor = _visual.GetTopVfxColor();
        vfxComponent.Play(new VFXPlaySettings(
            "TreeHitEffect_Top",
            _visual.GetTopRootPosition(),
            _visual.GetTopRootRotation(),
            topColor.startColor,
            topColor.overrideChildrenColor,
            null
        ));

        ParticleColorSet bottomColor = _visual.GetBottomVfxColor();
        vfxComponent.Play(new VFXPlaySettings(
            "TreeHitEffect_Bottom",
            _visual.GetBottomRootPosition(),
            _visual.GetBottomRootRotation(),
            bottomColor.startColor,
            bottomColor.overrideChildrenColor,
            null
        ));
    }

    /// <summary>
    /// 보석 단계 나무를 때렸을 때의 전용 임팩트 VFX를 재생합니다. 일반 피격 VFX 대신 단독으로 터집니다.
    ///
    /// 정렬: 나무의 모든 렌더러 중 가장 앞에 그려지는 하이라이트보다 한 단계 더 앞에 둡니다.
    /// 나무 렌더러들은 body -> outline -> shield -> highlight 순으로 order가 쌓이므로,
    /// top 기준으로 잡으면 아웃라인/실드에 가려집니다. 레이어도 나무와 같은 Objects로 명시합니다.
    /// </summary>
    public void PlayTreeGemHitVFX(TreeVisualComponent _visual, int _gemStage)
    {
        if (vfxComponent == null || _visual == null) return;

        string tag = GetGemImpactTag(_gemStage);
        if (string.IsNullOrEmpty(tag)) return;

        vfxComponent.Play(new VFXPlaySettings(
            tag,
            _visual.GetTopRootPosition(),
            _visual.GetTopRootRotation(),
            TreeSortingLayerName,
            _visual.GetTopHighlightSortingOrder() + 1,
            null
        ));
    }

    private static string GetGemImpactTag(int _gemStage)
    {
        switch (_gemStage)
        {
            case 1: return "RareImpact_Gold";
            case 2: return "RareImpact_Diamond";
            case 3: return "RareImpact_Rainbow";
            default: return null;
        }
    }

    /// <summary>
    /// 나무 사망 VFX를 재생합니다. parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
    /// Top/Bottom 이펙트는 각각 설정된 컬러를 공유합니다.
    /// </summary>
    public void PlayTreeDeadVFX(TreeVisualComponent _visual)
    {
        if (vfxComponent == null || _visual == null) return;

        ParticleColorSet topColor = _visual.GetTopVfxColor();
        vfxComponent.Play(new VFXPlaySettings(
            "TreeDeadEffect_Top",
            _visual.GetTopRootPosition(),
            _visual.GetTopRootRotation(),
            topColor.startColor,
            topColor.overrideChildrenColor,
            null
        ));

        ParticleColorSet bottomColor = _visual.GetBottomVfxColor();
        vfxComponent.Play(new VFXPlaySettings(
            "TreeDeadEffect_Bottom",
            _visual.GetBottomRootPosition(),
            _visual.GetBottomRootRotation(),
            bottomColor.startColor,
            bottomColor.overrideChildrenColor,
            null
        ));
    }

    /// <summary>
    /// 포자막(Shield)이 파괴되었을 때의 VFX를 재생합니다. parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
    /// 나무 종류별로 이펙트가 다를 수 있어 TreeType에 따라 태그를 분기합니다.
    /// </summary>
    public void PlayShieldBrokenVFX(TreeVisualComponent _visual, TreeType _treeType)
    {
        if (vfxComponent == null || _visual == null) return;

        // BellpineTree는 전용 이펙트가 아직 제작되지 않아 빈 슬롯(SporeShieldBrokenEffect_Bellpine)만
        // 만들어둔 상태입니다. 이펙트가 준비되면 vfxPoolDataList에 프리팹만 연결하면 됩니다.
        string tag = _treeType == TreeType.BellpineTree
            ? "SporeShieldBrokenEffect_Bellpine"
            : "SporeShieldBrokenEffect";

        // 실드가 깨지는 순간의 이펙트이므로, 나무 top이 아니라 실드 스프라이트보다 한 단계 앞에 그려져야 한다.
        int sortingOrder = _visual.GetTopShieldSortingOrder() + 1;

        // 위치만 밑둥 쪽으로 내리고(정렬 순서는 그대로 top 기준 유지)
        Vector3 position = _visual.GetTopRootPosition() + new Vector3(0f, shieldBrokenVfxYOffset, 0f);

        vfxComponent.Play(new VFXPlaySettings(
            tag,
            position,
            _visual.GetTopRootRotation(),
            sortingOrder,
            null
        ));
    }

    /// <summary>
    /// 별똥별이 착탄했을 때의 폭발 VFX를 재생합니다. parent는 null로 고정하여 완전히 분리합니다.
    /// </summary>
    public void PlayStarImpactExplosionVFX(Vector3 _position, int _sortingOrder)
    {
        if (vfxComponent == null) return;

        vfxComponent.Play(new VFXPlaySettings(
            "StarImpactExplosionEffect",
            _position,
            Quaternion.identity,
            _sortingOrder,
            null
        ));
    }

    /// <summary>
    /// 발현 낙인이 나무에 처음 찍히는 순간 1회 재생되는 각인 VFX(VFX_BrandStamp)입니다. 이후 낙인이 유지되는 동안은
    /// BeginBrandStarWrap의 별 감싸기가 이어받는다. parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
    /// </summary>
    public void PlayManifestationBrandStampVFX(TreeVisualComponent _visual)
    {
        if (vfxComponent == null || _visual == null) return;

        // 각인이 캐노피 하이라이트 위에 확실히 보이도록 한 단계 더 올린다(자식 폭발 메쉬는 VFX_BrandStampBurst가 +1로 따라온다).
        int sortingOrder = _visual.GetTopHighlightSortingOrder() + 2;

        vfxComponent.Play(new VFXPlaySettings(
            "ManifestationBrandStampEffect",
            _visual.GetTopRootPosition(),
            _visual.GetTopRootRotation(),
            sortingOrder,
            null
        ));
    }

    /// <summary>
    /// MainMenu → Dungeon 튜토리얼 인트로에서 캐릭터가 차량에서 내릴 때 재생되는 VFX입니다.
    /// 튜토리얼 하차 연출(InDungeonSystem.TutorialRideExitCoroutine)에서만 호출됩니다.
    /// </summary>
    public void PlayCharacterGetOffVFX(Vector3 _position)
    {
        if (vfxComponent == null) return;

        vfxComponent.Play(new VFXPlaySettings("CharacterGetOffEffect", _position, Quaternion.identity));
    }

    /// <summary>
    /// MagmaForest 등에서 나무가 열기를 방출할 때의 VFX를 재생합니다.
    /// parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
    /// </summary>
    public void PlayTreeHeatEmitVFX(TreeVisualComponent _visual)
    {
        if (vfxComponent == null || _visual == null) return;

        // 나무 하단(위치) 기준으로 y축 0.5 위로 오프셋
        Vector3 position = _visual.GetBottomRootPosition() + new Vector3(0f, 0.5f, 0f);
        int sortingOrder = _visual.GetTopHighlightSortingOrder() + 1;

        vfxComponent.Play(new VFXPlaySettings(
            "TreeHeatEmitEffect",
            position,
            _visual.GetBottomRootRotation(),
            sortingOrder,
            null
        ));
    }
}
