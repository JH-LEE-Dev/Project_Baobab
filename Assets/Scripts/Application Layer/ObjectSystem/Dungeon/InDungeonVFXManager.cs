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

    // 픽셀 레이저 도달 좌표와 그라운드 마크 기준 좌표(AnchorPosition)의 공간 매칭 허용 거리(제곱, 1m 이내).
    // 레이저의 starIndex는 원주 각도 순이고 마크 목록은 벌목 순이라 인덱스로는 짝을 맞출 수 없다.
    private const float GroundMarkMatchMaxSqrDistance = 1.0f;

    [Header("Constellation Dotted Line")]
    [SerializeField] private ConstellationDottedLine constellationDottedLinePrefab;
    [SerializeField] private int constellationDottedLinePoolDefaultCapacity = 8;
    // 별자리 그룹 하나당 점선 1개를 쓰므로, 맵이 커서 그룹 수가 이 값을 넘으면 던전 이탈 시 넘친 점선이
    // 파괴되고 다음 런에서 다시 생성된다. 풀은 미리 만들어두지 않으므로 넉넉히 잡아도 메모리 비용은 없다.
    [SerializeField] private int constellationDottedLinePoolMaxSize = 128;

    private IObjectPool<ConstellationDottedLine> constellationDottedLinePool;

    // 그룹(별자리)별로 평상시 표시 중인 점선 이음선과, 그 점선이 잇고 있는 노드 좌표(정돈된 순서).
    // 노드 좌표는 별 표식 나무가 벌목될 때 해당 노드를 푸른 큰 별 색으로 바꾸기 위한 공간 매칭에 쓴다.
    private readonly Dictionary<int, ConstellationDottedLine> activeDottedLinesByGroup = new Dictionary<int, ConstellationDottedLine>();
    private readonly Dictionary<int, List<Vector3>> dottedLineNodesByGroup = new Dictionary<int, List<Vector3>>();

    // 점선을 거둘 때 반납된 노드 좌표 리스트 - 다음 런의 점선이 재사용해 런마다 새로 할당하지 않는다.
    private readonly Stack<List<Vector3>> spareDottedLineNodeLists = new Stack<List<Vector3>>();

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
    /// 별 표식 나무가 죽은 자리에 TreeStarMark_Ground 마크를 스폰합니다. Instantiate/Destroy 대신
    /// ObjectPool로 재사용되며, sortingOrder는 죽은 나무의 topRenderer 값을 그대로 물려받습니다.
    /// HDR 강도는 TreeStarMarkGroundAnimator 자체 인스펙터 값을 사용합니다.
    /// 소속 그룹(_groupId)의 별자리 픽셀 레이저가 이 마크에 도달하기 전까지는 사라지지 않고 Loop 재생됩니다.
    /// _anchorPosition은 레이저 도달 좌표와 짝을 맞출 기준 좌표(별자리 경로에 들어간 나무 top 좌표)입니다.
    /// </summary>
    public void PlayConstellationGroundMarkVFX(Vector3 _position, Vector3 _anchorPosition, int _sortingOrder, int _groupId)
    {
        if (treeStarMarkGroundPool == null) return;

        TreeStarMarkGroundAnimator _instance = treeStarMarkGroundPool.Get();
        _instance.transform.position = _position;
        _instance.SetSortingOrder(_sortingOrder);
        _instance.SetGroupId(_groupId);
        _instance.SetAnchorPosition(_anchorPosition);
        _instance.Play();

        if (!activeGroundMarksByGroup.TryGetValue(_groupId, out List<TreeStarMarkGroundAnimator> _list))
        {
            _list = new List<TreeStarMarkGroundAnimator>();
            activeGroundMarksByGroup[_groupId] = _list;
        }
        _list.Add(_instance);
    }

    /// <summary>
    /// 별자리 픽셀 레이저의 선단이 별에 도달한 순간(OnStarReachedEvent) 호출되어, 그 좌표에 있는 그룹의
    /// 그라운드 마크 하나를 즉시 폭발(PlayManifestEffect)시킵니다. starIndex가 아니라 좌표로 짝을 맞추며
    /// (공간 매칭), 폭발시킨 마크는 대기 목록에서 빠지므로 같은 별에 두 번째 빔이 도착해도 다시 터지지 않습니다.
    /// 매칭되는 마크가 없으면(이미 터졌거나, 스킬 해금 전에 벌목되어 마크가 없던 별) false를 반환합니다.
    /// </summary>
    public bool ManifestConstellationGroundMarkAt(int _groupId, Vector3 _worldPos)
    {
        if (!activeGroundMarksByGroup.TryGetValue(_groupId, out List<TreeStarMarkGroundAnimator> _list)) return false;

        int _bestIndex = -1;
        float _bestSqrDistance = GroundMarkMatchMaxSqrDistance;
        for (int i = 0; i < _list.Count; i++)
        {
            Vector2 _delta = _list[i].AnchorPosition - _worldPos;
            float _sqrDistance = _delta.sqrMagnitude;
            if (_sqrDistance <= _bestSqrDistance)
            {
                _bestSqrDistance = _sqrDistance;
                _bestIndex = i;
            }
        }

        if (0 > _bestIndex) return false;

        TreeStarMarkGroundAnimator _instance = _list[_bestIndex];
        _list.RemoveAt(_bestIndex);
        if (0 == _list.Count) activeGroundMarksByGroup.Remove(_groupId);

        // 대기 목록에서는 빠지지만, 연출이 끝나기 전에 던전을 나가는 경우를 대비해
        // pendingManifestInstances로 계속 추적해야 ClearAllConstellationGroundMarks가 강제로 회수할 수 있다.
        pendingManifestInstances.Add(_instance);
        // 레이저가 닿는 순간에 맞춰야 하므로 랜덤 지연 없이 즉시 터뜨린다. (한꺼번에 정리하는
        // ClearConstellationGroundMarks는 동시에 터지는 것을 흩뜨리도록 기존 랜덤 지연을 유지한다)
        _instance.PlayManifestEffectImmediate();
        return true;
    }

    /// <summary>
    /// 그룹에서 아직 대기 중인 그라운드 마크를 전부 소멸 연출로 보냅니다. 별자리 레이저 연출이 끝났는데도
    /// 레이저와 짝이 맞지 않아 남은 마크가 있거나, 레이저 프리팹이 없어 광선을 쏘지 못한 경우의 정리용입니다.
    /// 풀 반환은 각 인스턴스가 연출을 마치고 ManifestFinishedEvent를 발생시킬 때 처리됩니다.
    /// </summary>
    public void ClearConstellationGroundMarks(int _groupId)
    {
        if (!activeGroundMarksByGroup.TryGetValue(_groupId, out List<TreeStarMarkGroundAnimator> _list)) return;

        activeGroundMarksByGroup.Remove(_groupId);

        for (int i = 0; i < _list.Count; i++)
        {
            pendingManifestInstances.Add(_list[i]);
            _list[i].PlayManifestEffect();
        }
    }

    /// <summary>
    /// 그룹 구분 없이 현재 살아있는 그라운드 마크를 전부 즉시 회수합니다. 던전을 나가거나(ClearObjManager)
    /// 새 스테이지로 나무를 재생성(SpawnInitialTrees)할 때 호출되어야 합니다 - Stage3TreeGenerationStrategySO의
    /// groupId 카운터가 매번 0부터 다시 시작되므로, 이전 런에서 미발현 상태로 남은 마크를 여기서 정리해두지
    /// 않으면 다음 런의 그룹이 같은 groupId를 받았을 때 서로 다른 런의 마크가 뒤섞이게 된다.
    /// 아직 발현되지 않은 마크(activeGroundMarksByGroup)뿐 아니라, 발현 연출이 끝나지 않은 채 남아있는
    /// 마크(pendingManifestInstances)도 함께 강제 회수한다.
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

        for (int i = 0; i < pendingManifestInstances.Count; i++)
        {
            pendingManifestInstances[i].ForceReturnToPool();
        }
        pendingManifestInstances.Clear();
    }

    private TreeStarMarkGroundAnimator CreateTreeStarMarkGround()
    {
        TreeStarMarkGroundAnimator _instance = Instantiate(treeStarMarkGroundPrefab, transform);
        _instance.SetPool(treeStarMarkGroundPool);

        // 이벤트 바인딩 (생성 시 한 번만) - LootManager.CreateLootItem과 동일한 패턴
        _instance.ManifestFinishedEvent -= OnGroundMarkManifestFinished;
        _instance.ManifestFinishedEvent += OnGroundMarkManifestFinished;

        return _instance;
    }

    // 그라운드 마크의 소멸 연출이 끝났을 때 호출되어 풀로 반환한다.
    private void OnGroundMarkManifestFinished(TreeStarMarkGroundAnimator _instance)
    {
        pendingManifestInstances.Remove(_instance);
        _instance.ForceReturnToPool();
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
            Destroy(_instance.gameObject);
        }
    }

    /// <summary>
    /// 별자리 그룹의 별 표식 나무들(top 좌표)을 잇는 평상시 점선 이음선을 표시합니다.
    /// 픽셀 레이저와 같은 꼬임 풀기(UntanglePoints)로 정돈해, 평상시 점선과 발현 레이저가 같은 외곽선을 그리게 합니다.
    /// 같은 그룹에 다시 호출되면 기존 점선을 반환하고 새로 그립니다.
    /// </summary>
    public void ShowConstellationDottedLine(int _groupId, IReadOnlyList<Vector3> _points)
    {
        if (null == constellationDottedLinePool || null == _points || 2 > _points.Count) return;

        HideConstellationDottedLine(_groupId);

        List<Vector3> _nodes = 0 < spareDottedLineNodeLists.Count ? spareDottedLineNodeLists.Pop() : new List<Vector3>(_points.Count);
        _nodes.Clear();
        for (int i = 0; i < _points.Count; i++)
        {
            _nodes.Add(_points[i]);
        }
        PresentationLayer.VFX.ConstellationPixelLaser.UntanglePoints(_nodes, 0);

        ConstellationDottedLine _line = constellationDottedLinePool.Get();
        // 점선 메쉬는 노드 월드 좌표를 그대로 정점으로 쓰므로, 인스턴스 자체는 월드 원점에 둔다.
        _line.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        _line.SetPoints(_nodes, true);

        activeDottedLinesByGroup[_groupId] = _line;
        dottedLineNodesByGroup[_groupId] = _nodes;
    }

    /// <summary>
    /// 별 표식 나무가 벌목되었을 때 호출되어, 점선에서 그 나무(_worldPos와 가장 가까운 노드)를
    /// 푸른색 큰 별 노드로 전환합니다. 연결선이 황금빛 ↔ 푸른빛 그라데이션으로 바뀝니다.
    /// </summary>
    public void SetConstellationDottedLineNodeFelled(int _groupId, Vector3 _worldPos)
    {
        if (!activeDottedLinesByGroup.TryGetValue(_groupId, out ConstellationDottedLine _line)) return;
        if (!dottedLineNodesByGroup.TryGetValue(_groupId, out List<Vector3> _nodes)) return;

        int _bestIndex = -1;
        float _bestSqrDistance = GroundMarkMatchMaxSqrDistance;
        for (int i = 0; i < _nodes.Count; i++)
        {
            Vector2 _delta = _nodes[i] - _worldPos;
            float _sqrDistance = _delta.sqrMagnitude;
            if (_sqrDistance <= _bestSqrDistance)
            {
                _bestSqrDistance = _sqrDistance;
                _bestIndex = i;
            }
        }

        if (0 > _bestIndex) return;

        _line.SetNodeAsBigStar(_bestIndex, true);
    }

    /// <summary>
    /// 그룹의 점선 이음선을 풀로 반환합니다. 발현 레이저가 발사되거나(레이저가 점선을 대체),
    /// 발현 스킬 없이 그룹의 별 표식 나무가 모두 벌목되었을 때 호출됩니다.
    /// </summary>
    public void HideConstellationDottedLine(int _groupId)
    {
        if (activeDottedLinesByGroup.TryGetValue(_groupId, out ConstellationDottedLine _line))
        {
            activeDottedLinesByGroup.Remove(_groupId);
            _line.ReturnToPool();
        }

        if (dottedLineNodesByGroup.TryGetValue(_groupId, out List<Vector3> _nodes))
        {
            dottedLineNodesByGroup.Remove(_groupId);
            spareDottedLineNodeLists.Push(_nodes);
        }
    }

    /// <summary>
    /// 그룹 구분 없이 표시 중인 점선 이음선을 전부 회수합니다. ClearAllConstellationGroundMarks와 같은
    /// 시점(던전 이탈/나무 재생성)에 호출되어야 다음 런의 같은 groupId와 뒤섞이지 않습니다.
    /// </summary>
    public void ClearAllConstellationDottedLines()
    {
        foreach (ConstellationDottedLine _line in activeDottedLinesByGroup.Values)
        {
            _line.ReturnToPool();
        }
        activeDottedLinesByGroup.Clear();

        foreach (List<Vector3> _nodes in dottedLineNodesByGroup.Values)
        {
            spareDottedLineNodeLists.Push(_nodes);
        }
        dottedLineNodesByGroup.Clear();
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
