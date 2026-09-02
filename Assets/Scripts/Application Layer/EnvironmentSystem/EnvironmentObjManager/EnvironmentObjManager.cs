using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class EnvironmentObjManager : MonoBehaviour
{
    //외부 의존성
    private ITilemapDataProvider tilemapDataProvider;

    [System.Serializable]
    public struct MapTypeEnvironmentData
    {
        public MapType mapType;
        public StageEnvironmentDataSO envData;
    }

    [Header("Environment Data (Dungeon)")]
    [SerializeField] private List<MapTypeEnvironmentData> mapTypeEnvDatas;
    private StageEnvironmentDataSO currentStageEnvData;

    //내부 의존성
    [Header("Pool Settings (Town / Fallback)")]
    [SerializeField] private List<EnvironmentObj> envObjPrefabs;
    private Dictionary<EnvironmentObjType, IObjectPool<EnvironmentObj>> objPools = new Dictionary<EnvironmentObjType, IObjectPool<EnvironmentObj>>();

    // 현재 objPools가 어떤 프리팹 목록으로 만들어졌는지. SetupPools()가 불필요한 재생성을
    // 건너뛰는 판단에만 쓴다(아래 SetupPools 주석 참고).
    private List<EnvironmentObj> pooledPrefabSource;

    private List<EnvironmentObj> allSpawnedObjs = new List<EnvironmentObj>(SYSTEM_VAR.MAX_ENV_OBJ_CNT);
    public IReadOnlyList<EnvironmentObj> AllSpawnedObjs => allSpawnedObjs;

    private List<EnvironmentObj> activeObjs = new List<EnvironmentObj>(SYSTEM_VAR.MAX_ENV_OBJ_CNT);
    public IReadOnlyList<EnvironmentObj> ActiveObjs => activeObjs;

    [Header("Optimization")]
    [SerializeField] private float cullingDistance = 35f;
    [SerializeField] private float cullingUpdateInterval = 0.2f;
    private float cullingUpdateTimer = 0f;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private float[] cullingDistances;
    private CullingGroup.StateChanged onCullingStateChangedDelegate;

    [SerializeField] private bool collectionCheck = false;
    [SerializeField] private int cloudDefaultCapacity = 500;
    [SerializeField] private int birdShadowDefaultCapacity = 50;
    [SerializeField] private int cloudMaxSize = SYSTEM_VAR.MAX_CLOUD_OBJ_CNT;
    [SerializeField] private int birdShadowMaxSize = SYSTEM_VAR.MAX_BIRDSHADOW_OBJ_CNT;

    [Header("Cloud Settings")]
    [SerializeField] private List<Sprite> cloudSprites;
    [SerializeField] private Color cloudColor = Color.white;
    [SerializeField] private int cloudCnt = 60;
    [SerializeField] private float cloudMinSpeed = 0.02f;
    [SerializeField] private float cloudMaxSpeed = 0.06f;

    [Header("Bird Shadow Settings")]
    [SerializeField] private int birdFlockCnt = 5;
    [SerializeField] private float birdMinSpeed = 2f;
    [SerializeField] private float birdMaxSpeed = 4f;
    [SerializeField] private float birdSpawnRadiusPadding = 10f;
    [SerializeField] private float birdMinDelay = 3f;
    [SerializeField] private float birdMaxDelay = 8f;

    [Header("Town Settings")]
    [SerializeField] private int townCloudCnt = 30;
    [SerializeField] private int townBirdFlockCnt = 2;
    [SerializeField] private float townMinX = -40f;
    [SerializeField] private float townMaxX = 40f;
    [SerializeField] private float townMinY = 0f;
    [SerializeField] private float townMaxY = 40f;

    private List<int> cellIndices = new List<int>(100); // GC Alloc 방지용 재사용 리스트

    [Space]
    [Header("BirdSpawnPoint")]
    [SerializeField] Transform birdSpawnPoint_LU;
    [SerializeField] Transform birdSpawnPoint_LD;
    [SerializeField] Transform birdSpawnPoint_RU;
    [SerializeField] Transform birdSpawnPoint_RD;


    // // 퍼블릭 메서드

    public void SetupForMapType(MapType _mapType)
    {
        if (mapTypeEnvDatas == null) return;

        for (int i = 0; i < mapTypeEnvDatas.Count; i++)
        {
            if (mapTypeEnvDatas[i].mapType == _mapType)
            {
                currentStageEnvData = mapTypeEnvDatas[i].envData;
                break;
            }
        }
        SetupPools();
    }

    public void Initialize(ITilemapDataProvider _tilemapDataProvider)
    {
        tilemapDataProvider = _tilemapDataProvider;

        cullingDistances = new float[] { cullingDistance };
        spheres = new BoundingSphere[cloudMaxSize + birdShadowMaxSize];
        onCullingStateChangedDelegate = OnCullingStateChanged;

        SetupPools();
    }

    public void ReleaseObj(EnvironmentObj _obj)
    {
        if (null == _obj)
            return;

        if (true == objPools.TryGetValue(_obj.envObjType, out IObjectPool<EnvironmentObj> _pool))
        {
            _pool.Release(_obj);
        }
        else
        {
            UpdateObjVisibility(_obj, false);
            RemoveFromMasterList(_obj);
            Destroy(_obj.gameObject);
        }
    }

    public void ReleaseAll()
    {
        if (null != cullingGroup)
        {
            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }

        if (null == allSpawnedObjs)
            return;

        for (int _i = allSpawnedObjs.Count - 1; _i >= 0; _i--)
        {
            EnvironmentObj _obj = allSpawnedObjs[_i];
            if (null != _obj)
            {
                ReleaseObj(_obj);
            }
        }

        allSpawnedObjs.Clear();
        activeObjs.Clear();
    }

    public void SpawnInDungeonEnvironmentObjs()
    {
        ReleaseAll();
        SpawnClouds();
        SpawnBirdShadows();
    }

    public void SpawnTownEnvironmentObjs()
    {
        ReleaseAll();
        SpawnTownClouds();
        SpawnTownBirdShadows();
    }

    private void SpawnTownClouds()
    {
        float paddingX = 3f;
        float minX = townMinX - paddingX;
        float maxX = townMaxX + paddingX;
        float minY = townMinY;
        float maxY = townMaxY;

        float width = Mathf.Max(0.1f, maxX - minX);
        float height = Mathf.Max(0.1f, maxY - minY);

        int cols = Mathf.CeilToInt(Mathf.Sqrt(townCloudCnt * (width / height)));
        cols = Mathf.Max(1, cols);
        int rows = Mathf.CeilToInt((float)townCloudCnt / cols);
        rows = Mathf.Max(1, rows);

        float cellWidth = width / cols;
        float cellHeight = height / rows;

        int totalCells = cols * rows;

        if (cellIndices.Capacity < totalCells)
        {
            cellIndices.Capacity = totalCells;
        }
        cellIndices.Clear();

        for (int i = 0; i < totalCells; i++)
        {
            cellIndices.Add(i);
        }

        for (int i = 0; i < totalCells; i++)
        {
            int temp = cellIndices[i];
            int randomIndex = UnityEngine.Random.Range(i, totalCells);
            cellIndices[i] = cellIndices[randomIndex];
            cellIndices[randomIndex] = temp;
        }

        int toSpawn = Mathf.Min(townCloudCnt, totalCells);
        for (int i = 0; i < toSpawn; i++)
        {
            int cellIdx = cellIndices[i];
            int row = cellIdx / cols;
            int col = cellIdx % cols;

            float centerX = minX + (col + 0.5f) * cellWidth;
            float centerY = minY + (row + 0.5f) * cellHeight;

            float jitterX = UnityEngine.Random.Range(-cellWidth * 0.4f, cellWidth * 0.4f);
            float jitterY = UnityEngine.Random.Range(-cellHeight * 0.4f, cellHeight * 0.4f);

            Vector3 spawnPos = new Vector3(centerX + jitterX, centerY + jitterY, 0f);
            SpawnCloudAt(spawnPos, minX, maxX);
        }
    }

    private void SpawnTownBirdShadows()
    {
        Vector3 mapCenter = new Vector3(
            (townMinX + townMaxX) * 0.5f,
            (townMinY + townMaxY) * 0.5f,
            0f
        );

        float mapWidth = townMaxX - townMinX;
        float mapHeight = townMaxY - townMinY;
        float spawnRadius = Mathf.Max(mapWidth, mapHeight) * 0.5f + birdSpawnRadiusPadding;

        for (int _i = 0; _i < townBirdFlockCnt; _i++)
        {
            // 쐐기 대형을 구성하기 위해 무조건 홀수(3마리 또는 5마리)로 제한
            int _birdCountInFlock = UnityEngine.Random.Range(1, 3) * 2 + 1;
            BirdShadow _leader = null;

            for (int _j = 0; _j < _birdCountInFlock; _j++)
            {
                if (_j == 0)
                {
                    _leader = SpawnBirdShadowAt(_i, 0, mapCenter, spawnRadius, Vector3.zero);
                }
                else
                {
                    BirdShadow _follower = SpawnBirdShadowAt(_i, _j, mapCenter, spawnRadius, Vector3.zero);
                    if (null != _leader && null != _follower)
                    {
                        _follower.transform.SetParent(_leader.transform, false);
                        _follower.SetLeader(_leader);

                        Vector3 _dir = _leader.GetFlightDirection();
                        Vector3 _perp = new Vector3(-_dir.y, _dir.x, 0f);
                        float _side = (_j % 2 == 1) ? -1f : 1f; // 홀수: 왼쪽, 짝수: 오른쪽
                        int _depth = (_j + 1) / 2;
                        float _stepSide = 0.5f;
                        float _stepBack = 0.6f;

                        _follower.transform.localPosition = -_dir * (_depth * _stepBack) + _perp * (_side * _depth * _stepSide);
                        _follower.transform.localRotation = Quaternion.identity;
                    }
                }
            }
        }
    }

    private void SpawnClouds()
    {
        if (tilemapDataProvider == null || currentStageEnvData == null) return;
        if (false == objPools.ContainsKey(EnvironmentObjType.Cloud)) return;

        // 맵의 전체 범위 계산
        Vector3 bottomLeft = new Vector3Int(-tilemapDataProvider.GridWidth / 2, 0, 0);
        Vector3 topRight = new Vector3Int(tilemapDataProvider.GridWidth / 2, tilemapDataProvider.GridHeight / 2, 0);

        // 맵 범위보다 약간 넓게 설정 (좌우 이동을 위함)
        float paddingX = 3f;
        float minX = bottomLeft.x - paddingX;
        float maxX = topRight.x + paddingX;
        float minY = bottomLeft.y;
        float maxY = topRight.y;

        float width = Mathf.Max(0.1f, maxX - minX);
        float height = Mathf.Max(0.1f, maxY - minY);

        int cols = Mathf.CeilToInt(Mathf.Sqrt(cloudCnt * (width / height)));
        cols = Mathf.Max(1, cols);
        int rows = Mathf.CeilToInt((float)cloudCnt / cols);
        rows = Mathf.Max(1, rows);

        float cellWidth = width / cols;
        float cellHeight = height / rows;

        int totalCells = cols * rows;

        // GC Alloc 방지
        if (cellIndices.Capacity < totalCells)
        {
            cellIndices.Capacity = totalCells;
        }
        cellIndices.Clear();

        for (int i = 0; i < totalCells; i++)
        {
            cellIndices.Add(i);
        }

        for (int i = 0; i < totalCells; i++)
        {
            int temp = cellIndices[i];
            int randomIndex = UnityEngine.Random.Range(i, totalCells);
            cellIndices[i] = cellIndices[randomIndex];
            cellIndices[randomIndex] = temp;
        }

        int toSpawn = Mathf.Min(cloudCnt, totalCells);
        for (int i = 0; i < toSpawn; i++)
        {
            int cellIdx = cellIndices[i];
            int row = cellIdx / cols;
            int col = cellIdx % cols;

            float centerX = minX + (col + 0.5f) * cellWidth;
            float centerY = minY + (row + 0.5f) * cellHeight;

            float jitterX = UnityEngine.Random.Range(-cellWidth * 0.4f, cellWidth * 0.4f);
            float jitterY = UnityEngine.Random.Range(-cellHeight * 0.4f, cellHeight * 0.4f);

            Vector3 spawnPos = new Vector3(centerX + jitterX, centerY + jitterY, 0f);
            SpawnCloudAt(spawnPos, minX, maxX);
        }
    }

    private void SpawnCloudAt(Vector3 _pos, float _minX, float _maxX)
    {
        if (false == objPools.TryGetValue(EnvironmentObjType.Cloud, out IObjectPool<EnvironmentObj> _pool))
            return;

        if (null == cullingGroup)
        {
            SetupCullingGroup();
        }

        EnvironmentObj _obj = _pool.Get();
        _obj.transform.position = _pos;
        _obj.Initialize();

        List<Sprite> spritesToUse = (currentStageEnvData != null && currentStageEnvData.CloudSprites != null && currentStageEnvData.CloudSprites.Count > 0)
            ? currentStageEnvData.CloudSprites
            : cloudSprites;

        Color colorToUse = (currentStageEnvData != null) ? currentStageEnvData.CloudColor : cloudColor;

        if (_obj is Cloud cloud)
        {
            float moveSpeed = UnityEngine.Random.Range(cloudMinSpeed, cloudMaxSpeed); // 수평 이동 속도
            cloud.SetupCloud(spritesToUse, colorToUse, moveSpeed, _minX, _maxX);
        }

        _obj.PoolIndex = allSpawnedObjs.Count;
        allSpawnedObjs.Add(_obj);

        if (spheres.Length <= _obj.PoolIndex)
        {
            Array.Resize(ref spheres, Mathf.Max(spheres.Length * 2, _obj.PoolIndex + 1));
            cullingGroup.SetBoundingSpheres(spheres);
        }
        spheres[_obj.PoolIndex] = new BoundingSphere(_pos, 5f);

        cullingGroup.SetBoundingSphereCount(allSpawnedObjs.Count);

        bool _shouldBeActive = cullingGroup.IsVisible(_obj.PoolIndex) && (cullingGroup.GetDistance(_obj.PoolIndex) == 0);
        UpdateObjVisibility(_obj, _shouldBeActive);
    }

    private void SpawnBirdShadows()
    {
        if (null == tilemapDataProvider || currentStageEnvData == null)
            return;
        if (false == objPools.ContainsKey(EnvironmentObjType.BirdShadow))
            return;

        Vector3 _bottomLeft = new Vector3Int(-tilemapDataProvider.GridWidth / 2, 0, 0);
        Vector3 _topRight = new Vector3Int(tilemapDataProvider.GridWidth / 2, tilemapDataProvider.GridHeight / 2, 0);

        Vector3 _mapCenter = new Vector3(
            (_bottomLeft.x + _topRight.x) * 0.5f,
            (_bottomLeft.y + _topRight.y) * 0.5f,
            0f
        );

        float _mapWidth = _topRight.x - _bottomLeft.x;
        float _mapHeight = _topRight.y - _bottomLeft.y;
        float _spawnRadius = Mathf.Max(_mapWidth, _mapHeight) * 0.5f + birdSpawnRadiusPadding;

        for (int _i = 0; _i < birdFlockCnt; _i++)
        {
            // 쐐기 대형을 구성하기 위해 무조건 홀수(3마리 또는 5마리)로 제한
            int _birdCountInFlock = UnityEngine.Random.Range(1, 3) * 2 + 1;
            BirdShadow _leader = null;

            for (int _j = 0; _j < _birdCountInFlock; _j++)
            {
                if (_j == 0)
                {
                    _leader = SpawnBirdShadowAt(_i, 0, _mapCenter, _spawnRadius, Vector3.zero);
                }
                else
                {
                    BirdShadow _follower = SpawnBirdShadowAt(_i, _j, _mapCenter, _spawnRadius, Vector3.zero);
                    if (null != _leader && null != _follower)
                    {
                        _follower.transform.SetParent(_leader.transform, false);
                        _follower.SetLeader(_leader);

                        Vector3 _dir = _leader.GetFlightDirection();
                        Vector3 _perp = new Vector3(-_dir.y, _dir.x, 0f);
                        float _side = (_j % 2 == 1) ? -1f : 1f; // 홀수: 왼쪽, 짝수: 오른쪽
                        int _depth = (_j + 1) / 2;
                        float _stepSide = 0.5f;
                        float _stepBack = 0.6f;

                        _follower.transform.localPosition = -_dir * (_depth * _stepBack) + _perp * (_side * _depth * _stepSide);
                        _follower.transform.localRotation = Quaternion.identity;
                    }
                }
            }
        }
    }

    private BirdShadow SpawnBirdShadowAt(int _flockIndex, int _birdIndexInFlock, Vector3 _mapCenter, float _spawnRadius, Vector3 _flockOffset)
    {
        if (false == objPools.TryGetValue(EnvironmentObjType.BirdShadow, out IObjectPool<EnvironmentObj> _pool))
            return null;

        if (null == cullingGroup)
        {
            SetupCullingGroup();
        }

        EnvironmentObj _obj = _pool.Get();
        BirdShadow _birdShadow = _obj as BirdShadow;

        if (null != _birdShadow)
        {
            _birdShadow.SetupBird(
                _flockIndex,
                _birdIndexInFlock,
                _mapCenter,
                _spawnRadius,
                birdMinSpeed,
                birdMaxSpeed,
                birdMinDelay,
                birdMaxDelay,
                _flockOffset
            );
        }

        Vector3 _initialPos = _obj.GetCurrentPosition();
        _obj.transform.position = _initialPos;
        _obj.Initialize();

        _obj.PoolIndex = allSpawnedObjs.Count;
        allSpawnedObjs.Add(_obj);

        if (spheres.Length <= _obj.PoolIndex)
        {
            Array.Resize(ref spheres, Mathf.Max(spheres.Length * 2, _obj.PoolIndex + 1));
            cullingGroup.SetBoundingSpheres(spheres);
        }
        spheres[_obj.PoolIndex] = new BoundingSphere(_initialPos, 2f);

        cullingGroup.SetBoundingSphereCount(allSpawnedObjs.Count);

        bool _shouldBeActive = cullingGroup.IsVisible(_obj.PoolIndex) && (cullingGroup.GetDistance(_obj.PoolIndex) == 0);
        UpdateObjVisibility(_obj, _shouldBeActive);

        return _birdShadow;
    }

    public void ReleaseAllObjs()
    {
        ReleaseAll();
    }

    // // 내부 메서드

    /// <summary>
    /// SetupForMapType()을 통해 던전 진입마다 호출된다. 그래서 두 가지를 지켜야 한다.
    ///
    /// 1. 프리팹 목록이 그대로면 풀을 다시 만들지 않는다. 풀을 새로 만드는 순간 그 안에서
    ///    대기 중이던(비활성) 오브젝트가 추적에서 떨어져 나가 파괴도 재사용도 되지 않는다.
    ///    같은 맵 타입으로 재진입하는 대부분의 경우가 여기에 해당한다.
    /// 2. 목록이 실제로 바뀌었다면(맵 타입 변경) 풀은 새로 만들어야 한다 - createFunc가
    ///    이전 맵의 프리팹을 캡처하고 있기 때문이다. 다만 버리기 전에 각 풀의 Clear()로
    ///    보관 중인 오브젝트를 반드시 파괴해야 한다. 활성 오브젝트는 풀에 없으므로 영향받지
    ///    않고, 뒤이은 ReleaseAll()이 새 풀로 회수해 그대로 재사용된다.
    /// </summary>
    private void SetupPools()
    {
        List<EnvironmentObj> prefabsToUse = (currentStageEnvData != null) 
            ? currentStageEnvData.EnvObjPrefabs 
            : envObjPrefabs;

        if (objPools.Count > 0 && ReferenceEquals(pooledPrefabSource, prefabsToUse)) return;

        foreach (var _kvp in objPools)
        {
            _kvp.Value?.Clear();
        }
        objPools.Clear();
        pooledPrefabSource = prefabsToUse;

        if (prefabsToUse == null || prefabsToUse.Count == 0) return;

        foreach (EnvironmentObj _prefab in prefabsToUse)
        {
            if (null == _prefab) continue;

            EnvironmentObjType _type = _prefab.envObjType;
            if (true == objPools.ContainsKey(_type)) continue;

            int _capacity = 50;
            int _max = 100;

            if (_type == EnvironmentObjType.Cloud)
            {
                _capacity = cloudDefaultCapacity;
                _max = cloudMaxSize;
            }
            else if (_type == EnvironmentObjType.BirdShadow)
            {
                _capacity = birdShadowDefaultCapacity;
                _max = birdShadowMaxSize;
            }

            IObjectPool<EnvironmentObj> _pool = new ObjectPool<EnvironmentObj>(
                () => Instantiate(_prefab, transform),
                OnGetObj,
                OnReleaseObj,
                OnDestroyObj,
                collectionCheck,
                _capacity,
                _max
            );
            objPools.Add(_type, _pool);
        }
    }

    private void SetupCullingGroup()
    {
        cullingGroup = new CullingGroup();
        cullingGroup.targetCamera = Camera.main;
        cullingGroup.SetBoundingDistances(cullingDistances);
        cullingGroup.SetDistanceReferencePoint(Camera.main.transform);
        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.onStateChanged = onCullingStateChangedDelegate;
    }

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
        if (_ev.index >= allSpawnedObjs.Count) return;

        bool _shouldBeActive = _ev.isVisible && (_ev.currentDistance == 0);
        UpdateObjVisibility(allSpawnedObjs[_ev.index], _shouldBeActive);
    }

    private void UpdateObjVisibility(EnvironmentObj _obj, bool _shouldBeActive)
    {
        if (null == _obj) return;

        if (_obj.bActivated != _shouldBeActive)
        {
            if (true == _shouldBeActive) _obj.Show();
            else _obj.Hide();
        }

        if (true == _shouldBeActive)
        {
            if (-1 == _obj.UpdateIndex)
            {
                _obj.UpdateIndex = activeObjs.Count;
                activeObjs.Add(_obj);
            }
        }
        else
        {
            int _idx = _obj.UpdateIndex;
            if (-1 != _idx)
            {
                int _lastIdx = activeObjs.Count - 1;
                if (_idx != _lastIdx)
                {
                    EnvironmentObj _lastObj = activeObjs[_lastIdx];
                    activeObjs[_idx] = _lastObj;
                    _lastObj.UpdateIndex = _idx;
                }
                activeObjs.RemoveAt(_lastIdx);
                _obj.UpdateIndex = -1;
            }
        }
    }

    private void UpdateCullingSpheres()
    {
        int _count = allSpawnedObjs.Count;
        for (int _i = 0; _i < _count; _i++)
        {
            spheres[_i].position = allSpawnedObjs[_i].GetCurrentPosition();
        }
    }

    private void RemoveFromMasterList(EnvironmentObj _obj)
    {
        int _index = _obj.PoolIndex;
        if (_index >= 0 && _index < allSpawnedObjs.Count)
        {
            int _lastIdx = allSpawnedObjs.Count - 1;
            if (_index != _lastIdx)
            {
                EnvironmentObj _lastObj = allSpawnedObjs[_lastIdx];
                allSpawnedObjs[_index] = _lastObj;
                _lastObj.PoolIndex = _index;
                spheres[_index] = spheres[_lastIdx];
            }
            allSpawnedObjs.RemoveAt(_lastIdx);
            _obj.PoolIndex = -1;

            if (null != cullingGroup)
            {
                cullingGroup.SetBoundingSphereCount(allSpawnedObjs.Count);
            }
        }
    }

    // // 풀링 콜백

    private void OnGetObj(EnvironmentObj _obj)
    {
        _obj.ResetObj();
        _obj.gameObject.SetActive(true);
    }

    private void OnReleaseObj(EnvironmentObj _obj)
    {
        UpdateObjVisibility(_obj, false);
        RemoveFromMasterList(_obj);
        _obj.DeActivate();

        _obj.transform.SetParent(transform, false);
        if (_obj is BirdShadow _birdShadow)
        {
            _birdShadow.SetLeader(null);
        }

        if (true == _obj.gameObject.activeSelf)
        {
            _obj.gameObject.SetActive(false);
        }
    }

    private void OnDestroyObj(EnvironmentObj _obj)
    {
        if (null != _obj && null != _obj.gameObject)
        {
            Destroy(_obj.gameObject);
        }
    }

    // // 유니티 이벤트 함수

    private void Update()
    {
        int _activeCount = activeObjs.Count;
        for (int _i = 0; _i < _activeCount; _i++)
        {
            activeObjs[_i].ManualUpdate();
        }

        if (null != cullingGroup && 0 < allSpawnedObjs.Count)
        {
            cullingUpdateTimer += Time.deltaTime;
            if (cullingUpdateTimer >= cullingUpdateInterval)
            {
                UpdateCullingSpheres();
                cullingUpdateTimer = 0f;
            }
        }
    }

    private void OnDestroy()
    {
        if (null != cullingGroup)
        {
            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }
    }
}