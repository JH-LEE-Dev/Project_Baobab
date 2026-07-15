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

    [Header("Constellation Ground Mark")]
    [SerializeField] private TreeStarMarkGroundAnimator treeStarMarkGroundPrefab;
    [SerializeField] private int treeStarMarkGroundPoolDefaultCapacity = 8;
    [SerializeField] private int treeStarMarkGroundPoolMaxSize = 64;

    private IObjectPool<TreeStarMarkGroundAnimator> treeStarMarkGroundPool;

    // 그룹(별자리)별로 아직 발현되지 않아 살아있는 그라운드 마크 인스턴스들을 추적한다.
    private readonly Dictionary<int, List<TreeStarMarkGroundAnimator>> activeGroundMarksByGroup = new Dictionary<int, List<TreeStarMarkGroundAnimator>>();

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
    /// 별 표식 나무가 죽은 자리에 TreeStarMark_Ground 마크를 스폰합니다. Instantiate/Destroy 대신
    /// ObjectPool로 재사용되며, sortingOrder와 HDR 강도는 죽은 나무의 topRenderer/constellationRenderer 값을 그대로 물려받습니다.
    /// 소속 그룹(_groupId)의 별자리 발현이 트리거되기 전까지는 자동으로 사라지지 않고 Loop 재생됩니다.
    /// </summary>
    public void PlayConstellationGroundMarkVFX(Vector3 _position, int _sortingOrder, int _groupId, float _hdrIntensity)
    {
        if (treeStarMarkGroundPool == null) return;

        TreeStarMarkGroundAnimator _instance = treeStarMarkGroundPool.Get();
        _instance.transform.position = _position;
        _instance.SetSortingOrder(_sortingOrder);
        _instance.SetGroupId(_groupId);
        _instance.SetHDRIntensity(_hdrIntensity);
        _instance.Play();

        if (!activeGroundMarksByGroup.TryGetValue(_groupId, out List<TreeStarMarkGroundAnimator> _list))
        {
            _list = new List<TreeStarMarkGroundAnimator>();
            activeGroundMarksByGroup[_groupId] = _list;
        }
        _list.Add(_instance);
    }

    /// <summary>
    /// 그룹의 별자리 발현이 트리거되면 호출되어, 그 그룹에서 아직 표시 중인 그라운드 마크를 전부 즉시 회수합니다.
    /// </summary>
    public void ClearConstellationGroundMarks(int _groupId)
    {
        if (!activeGroundMarksByGroup.TryGetValue(_groupId, out List<TreeStarMarkGroundAnimator> _list)) return;

        activeGroundMarksByGroup.Remove(_groupId);

        for (int i = 0; i < _list.Count; i++)
        {
            _list[i].ForceReturnToPool();
        }
    }

    /// <summary>
    /// 그룹 구분 없이 현재 살아있는 그라운드 마크를 전부 즉시 회수합니다. 던전을 나가거나(ClearObjManager)
    /// 새 스테이지로 나무를 재생성(SpawnInitialTrees)할 때 호출되어야 합니다 - Stage3TreeGenerationStrategySO의
    /// groupId 카운터가 매번 0부터 다시 시작되므로, 이전 런에서 미발현 상태로 남은 마크를 여기서 정리해두지
    /// 않으면 다음 런의 그룹이 같은 groupId를 받았을 때 서로 다른 런의 마크가 뒤섞이게 된다.
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
    }

    private TreeStarMarkGroundAnimator CreateTreeStarMarkGround()
    {
        TreeStarMarkGroundAnimator _instance = Instantiate(treeStarMarkGroundPrefab, transform);
        _instance.SetPool(treeStarMarkGroundPool);
        return _instance;
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
    /// 발현 낙인이 찍힌 나무 위에서 일정 인터벌마다 재생되는 스파크 VFX(VFX_Spark)입니다.
    /// parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
    /// </summary>
    public void PlayManifestationBrandVFX(TreeVisualComponent _visual)
    {
        if (vfxComponent == null || _visual == null) return;

        int sortingOrder = _visual.GetTopHighlightSortingOrder() + 1;

        vfxComponent.Play(new VFXPlaySettings(
            "ManifestationBrandSparkEffect",
            _visual.GetTopRootPosition(),
            _visual.GetTopRootRotation(),
            sortingOrder,
            null
        ));
    }
}
