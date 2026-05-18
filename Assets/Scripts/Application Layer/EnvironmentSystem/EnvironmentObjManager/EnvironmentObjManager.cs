using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.Pool;

public class EnvironmentObjManager : MonoBehaviour
{
    //외부 의존성
    private ITilemapDataProvider tilemapDataProvider;

    //내부 의존성
    [Header("Pool Settings")]
    [SerializeField] private List<EnvironmentObj> envObjPrefabs;
    private Dictionary<EnvironmentObjType, IObjectPool<EnvironmentObj>> objPools = new Dictionary<EnvironmentObjType, IObjectPool<EnvironmentObj>>();

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
    [SerializeField] private int defaultCapacity = 50;
    [SerializeField] private int maxSize = SYSTEM_VAR.MAX_ENV_OBJ_CNT;

    [Header("Cloud Settings")]
    [SerializeField] private List<Sprite> cloudSprites;
    [SerializeField] private int cloudCnt = 60;
    [SerializeField] private float cloudMinSpeed = 0.02f;
    [SerializeField] private float cloudMaxSpeed = 0.06f;
    private List<int> cellIndices = new List<int>(100); // GC Alloc 방지용 재사용 리스트


    // // 퍼블릭 메서드

    public void Initialize(ITilemapDataProvider _tilemapDataProvider)
    {
        tilemapDataProvider = _tilemapDataProvider;

        cullingDistances = new float[] { cullingDistance };
        spheres = new BoundingSphere[maxSize];
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

    public void SpawnEnvironmentObjs()
    {
        ReleaseAll();
        SpawnClouds();
    }

    private void SpawnClouds()
    {
        if (tilemapDataProvider == null) return;

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

        if (_obj is Cloud cloud)
        {
            Sprite sprite = cloudSprites[UnityEngine.Random.Range(0, cloudSprites.Count)];
            float moveSpeed = UnityEngine.Random.Range(cloudMinSpeed, cloudMaxSpeed); // 수평 이동 속도
            cloud.SetupCloud(sprite, moveSpeed, _minX, _maxX);
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

    public void ReleaseAllObjs()
    {
        ReleaseAll();
    }

    // // 내부 메서드

    private void SetupPools()
    {
        objPools.Clear();
        foreach (EnvironmentObj _prefab in envObjPrefabs)
        {
            if (null == _prefab) continue;

            EnvironmentObjType _type = _prefab.envObjType;
            if (true == objPools.ContainsKey(_type)) continue;

            IObjectPool<EnvironmentObj> _pool = new ObjectPool<EnvironmentObj>(
                () => Instantiate(_prefab, transform),
                OnGetObj,
                OnReleaseObj,
                OnDestroyObj,
                collectionCheck,
                defaultCapacity,
                maxSize
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
    }

    private void OnReleaseObj(EnvironmentObj _obj)
    {
        UpdateObjVisibility(_obj, false);
        RemoveFromMasterList(_obj);
        _obj.DeActivate();

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