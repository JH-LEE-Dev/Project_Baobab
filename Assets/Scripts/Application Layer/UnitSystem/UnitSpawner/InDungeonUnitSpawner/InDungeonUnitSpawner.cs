using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class InDungeonUnitSpawner : MonoBehaviour
{
    public event Action<Animal> AnimalHitEvent;
    public event Action<Animal> AnimalIsDeadEvent;
    //그룹 정보를 넘기지 않도록 이벤트 수정 (필요 시 Action<IReadOnlyList<Animal>> 등으로 변경 가능)
    public event Action AnimalSpawnedEvent;

    //외부 의존성
    private IEnvironmentProvider environmentProvider;
    private ITilemapDataProvider tilemapDataProvider;

    //내부 의존성
    [Header("Spawn Settings")]
    private Dictionary<AnimalType, IObjectPool<Animal>> animalPools = new Dictionary<AnimalType, IObjectPool<Animal>>();
    private Coroutine growthCoroutine;

    private List<Animal> allSpawnedAnimals = new List<Animal>(SYSTEM_VAR.MAX_ANIMAL_CNT); // 마스터 리스트 (컬링 그룹용)
    public IReadOnlyList<Animal> Animals => allSpawnedAnimals;
    
    private List<Animal> activeAnimals = new List<Animal>(SYSTEM_VAR.MAX_ANIMAL_CNT); // 업데이트 및 가시성 리스트
    public IReadOnlyList<Animal> ActiveAnimals => activeAnimals;

    private List<int> availableIndices = new List<int>(1024); // GC 방지용 캐싱 인덱스 리스트

    [Header("Optimization")]
    [SerializeField] private float cullingDistance = 25f;
    [SerializeField] private float cullingUpdateInterval = 0.1f;
    private float cullingUpdateTimer = 0f;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private float[] cullingDistances;
    private CullingGroup.StateChanged onCullingStateChangedDelegate;
    //private bool isCullingDirty = false;

    // // 풀 설정 변수
    [SerializeField] private bool collectionCheck = false; // 에디터 성능을 위해 false로 설정
    [SerializeField] private int defaultCapacity = 200;
    [SerializeField] private int maxSize = SYSTEM_VAR.MAX_ANIMAL_CNT;

    [SerializeField] private List<Animal> animalsPrefab;

    // // 퍼블릭 메서드
    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        tilemapDataProvider = environmentProvider.tilemapDataProvider;

        cullingDistances = new float[] { cullingDistance };
        spheres = new BoundingSphere[maxSize];
        onCullingStateChangedDelegate = OnCullingStateChanged;

        animalPools.Clear();
        foreach (var prefab in animalsPrefab)
        {
            if (prefab == null) continue;
            
            AnimalType type = prefab.animalType;
            if (animalPools.ContainsKey(type)) continue;

            var pool = new ObjectPool<Animal>(
                () => Instantiate(prefab, transform),
                OnGetAnimal,
                OnReleaseAnimal,
                OnDestroyAnimal,
                collectionCheck,
                defaultCapacity,
                maxSize
            );
            animalPools.Add(type, pool);
        }
    }

    public void SpawnAnimals()
    {
        if (tilemapDataProvider == null || animalsPrefab == null || animalsPrefab.Count == 0 || environmentProvider.densityProvider == null)
        {
            return;
        }

        SetupCullingGroup();
        StopGrowth();

        // 1. 전체 가용 타일 가져오기 (원본 리스트)
        List<Vector3> walkablePositions = tilemapDataProvider.GetWalkableTileWorldPositions();
        if (walkablePositions == null || walkablePositions.Count == 0) return;

        int totalToSpawn = environmentProvider.densityProvider.GetAnimalStartCnt();
        int spawnLimit = Mathf.Min(totalToSpawn, walkablePositions.Count);

        // 2. 컬렉션 재사용: 원본 리스트 보존 및 할당 방지를 위해 인덱스만 활용
        availableIndices.Clear();
        if (availableIndices.Capacity < walkablePositions.Count)
        {
            availableIndices.Capacity = walkablePositions.Count;
        }

        for (int i = 0; i < walkablePositions.Count; i++)
        {
            availableIndices.Add(i);
        }

        // 3. 필요한 수만큼만 부분 셔플(Partial Fisher-Yates) 및 순차 스폰
        for (int i = 0; i < spawnLimit; i++)
        {
            int rnd = UnityEngine.Random.Range(i, availableIndices.Count);
            int selectedIndex = availableIndices[rnd];

            availableIndices[rnd] = availableIndices[i];
            availableIndices[i] = selectedIndex;

            Vector3 spawnPos = walkablePositions[selectedIndex];
            SpawnAnimalAt(spawnPos);
        }

        AnimalSpawnedEvent?.Invoke();
        
        RefreshCullingGroup();

        // 4. 5초 후 점진적 스폰 루틴 시작
        growthCoroutine = StartCoroutine(StartGrowthAfterDelay());
    }

    private IEnumerator StartGrowthAfterDelay()
    {
        yield return new WaitForSeconds(5f);
        growthCoroutine = StartCoroutine(GrowthRoutine());
    }

    private IEnumerator GrowthRoutine()
    {
        while (true)
        {
            float interval = environmentProvider.densityProvider.GetAnimalRegenTime();
            yield return new WaitForSeconds(interval);

            if (environmentProvider.densityProvider.CanCreateAnimal())
            {
                SpawnOneAnimalFromAvailable();
            }
        }
    }

    private bool SpawnOneAnimalFromAvailable()
    {
        List<Vector3> walkablePositions = tilemapDataProvider.GetWalkableTileWorldPositions();
        int count = walkablePositions.Count;
        if (count == 0) return false;

        int startIdx = UnityEngine.Random.Range(0, count);
        for (int i = 0; i < count; i++)
        {
            int checkIdx = (startIdx + i) % count;
            Vector3 spawnPos = walkablePositions[checkIdx];
            Vector3Int cellPos = tilemapDataProvider.WorldToCell(spawnPos);

            if (!environmentProvider.pathfindGridProvider.IsOccupied(cellPos))
            {
                SpawnAnimalAt(spawnPos);
                return true;
            }
        }
        return false;
    }

    private void SetupCullingGroup()
    {
        if (cullingGroup == null)
        {
            cullingGroup = new CullingGroup();
            cullingGroup.onStateChanged = onCullingStateChangedDelegate;
        }

        cullingGroup.targetCamera = Camera.main;
        cullingGroup.SetBoundingDistances(cullingDistances);
        cullingGroup.SetDistanceReferencePoint(Camera.main.transform);
        cullingGroup.SetBoundingSpheres(spheres);
    }

    public void RefreshCullingGroup()
    {
        if (cullingGroup == null) return;

        int count = allSpawnedAnimals.Count;
        if (spheres == null || spheres.Length < count)
        {
            spheres = new BoundingSphere[Mathf.Max(count + 100, maxSize)];
        }

        for (int i = 0; i < count; i++)
        {
            spheres[i].position = allSpawnedAnimals[i].transform.position;
            spheres[i].radius = 3f; 
            allSpawnedAnimals[i].PoolIndex = i;
        }

        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.SetBoundingSphereCount(count);

        activeAnimals.Clear();
        for (int i = 0; i < count; i++)
        {
            bool isVisible = cullingGroup.IsVisible(i);
            bool isNear = cullingGroup.GetDistance(i) == 0;
            bool shouldBeActive = isVisible && isNear;

            allSpawnedAnimals[i].UpdateIndex = -1;
            UpdateAnimalVisibility(allSpawnedAnimals[i], shouldBeActive);
        }
    }

    private void UpdateCullingSpheres()
    {
        int count = allSpawnedAnimals.Count;
        for (int i = 0; i < count; i++)
        {
            spheres[i].position = allSpawnedAnimals[i].transform.position;
        }
    }

    private void OnCullingStateChanged(CullingGroupEvent _ev)
    {
        if (_ev.index >= allSpawnedAnimals.Count) return;

        bool shouldBeActive = _ev.isVisible && (_ev.currentDistance == 0);
        UpdateAnimalVisibility(allSpawnedAnimals[_ev.index], shouldBeActive);
    }

    private void UpdateAnimalVisibility(Animal _animal, bool _shouldBeActive)
    {
        if (_animal == null) return;

        if (_animal.bActivated != _shouldBeActive)
        {
            if (_shouldBeActive) _animal.Show();
            else _animal.Hide();
        }

        if (_shouldBeActive)
        {
            if (_animal.UpdateIndex == -1)
            {
                _animal.UpdateIndex = activeAnimals.Count;
                activeAnimals.Add(_animal);
            }
        }
        else
        {
            int idx = _animal.UpdateIndex;
            if (idx != -1)
            {
                int lastIdx = activeAnimals.Count - 1;
                if (idx != lastIdx)
                {
                    Animal lastAnimal = activeAnimals[lastIdx];
                    activeAnimals[idx] = lastAnimal;
                    lastAnimal.UpdateIndex = idx;
                }
                activeAnimals.RemoveAt(lastIdx);
                _animal.UpdateIndex = -1;
            }
        }
    }

    private void Update()
    {
        float deltaTime = Time.deltaTime;

        if (cullingGroup != null && allSpawnedAnimals.Count > 0)
        {
            cullingUpdateTimer += deltaTime;
            if (cullingUpdateTimer >= cullingUpdateInterval)
            {
                UpdateCullingSpheres();
                cullingUpdateTimer = 0f;
            }
        }
    }

    private void SpawnAnimalAt(Vector3 _pos)
    {
        AnimalType typeToSpawn = environmentProvider.densityProvider.GetAnimalTypeToSpawn();
        if (!animalPools.TryGetValue(typeToSpawn, out var pool))
        {
            // 해당 타입의 풀이 없는 경우 첫 번째 가용 풀 사용 (폴백)
            if (animalPools.Count > 0)
            {
                var enumerator = animalPools.Values.GetEnumerator();
                enumerator.MoveNext();
                pool = enumerator.Current;
            }
            else return;
        }

        Animal animal = pool.Get();
        animal.transform.position = _pos;
        animal.gameObject.SetActive(true);
        animal.Initialize(environmentProvider);

        // 최적화: 마스터 리스트 증분 업데이트 (O(1))
        animal.PoolIndex = allSpawnedAnimals.Count;
        allSpawnedAnimals.Add(animal);

        if (spheres.Length <= animal.PoolIndex)
        {
            Array.Resize(ref spheres, Mathf.Max(spheres.Length * 2, animal.PoolIndex + 1));
            cullingGroup.SetBoundingSpheres(spheres);
        }
        spheres[animal.PoolIndex] = new BoundingSphere(_pos, 3f);

        environmentProvider.densityProvider.UpdateAnimalCnt(true);
        
        if (cullingGroup != null)
        {
            cullingGroup.SetBoundingSphereCount(allSpawnedAnimals.Count);
            // 즉시 가시성 체크 및 초기 상태 설정
            bool shouldBeActive = cullingGroup.IsVisible(animal.PoolIndex) && (cullingGroup.GetDistance(animal.PoolIndex) == 0);
            UpdateAnimalVisibility(animal, shouldBeActive);
        }
        else
        {
            animal.Show();
            animal.UpdateIndex = activeAnimals.Count;
            activeAnimals.Add(animal);
        }

        animal.AnimalIsDeadEvent -= AnimalIsDead;
        animal.AnimalIsDeadEvent += AnimalIsDead;

        animal.AnimalHitEvent -= AnimalHit;
        animal.AnimalHitEvent += AnimalHit;
    }

    public void ReleaseAnimal(Animal _animal)
    {
        _animal.AnimalIsDeadEvent -= AnimalIsDead;
        _animal.AnimalHitEvent -= AnimalHit;

        if (animalPools.TryGetValue(_animal.animalType, out var pool))
        {
            pool.Release(_animal);
        }
        else
        {
            // 풀이 없는 예외 상황
            UpdateAnimalVisibility(_animal, false);
            RemoveFromMasterList(_animal);
            Destroy(_animal.gameObject);
        }
    }

    public void ReleaseAllAnimals()
    {
        StopGrowth();

        if (cullingGroup != null)
        {
            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }

        if (allSpawnedAnimals == null) return;

        // [최적화 핵심] 부모를 잠시 비활성화하여 에디터 Hierarchy 갱신 이벤트를 1번으로 압축
        this.gameObject.SetActive(false);

        // 리스트를 역순으로 순회하며 안전하게 해제
        for (int i = allSpawnedAnimals.Count - 1; i >= 0; i--)
        {
            Animal animal = allSpawnedAnimals[i];
            if (animal != null)
            {
                ReleaseAnimal(animal);
                environmentProvider.densityProvider.UpdateAnimalCnt(false);
            }
        }

        allSpawnedAnimals.Clear();
        activeAnimals.Clear();

        // 작업 완료 후 부모 재활성화
        this.gameObject.SetActive(true);
    }

    private void RemoveFromMasterList(Animal _animal)
    {
        int index = _animal.PoolIndex;
        if (index >= 0 && index < allSpawnedAnimals.Count)
        {
            int lastIdx = allSpawnedAnimals.Count - 1;
            if (index != lastIdx)
            {
                Animal lastAnimal = allSpawnedAnimals[lastIdx];
                allSpawnedAnimals[index] = lastAnimal;
                lastAnimal.PoolIndex = index;
                spheres[index] = spheres[lastIdx];
            }
            allSpawnedAnimals.RemoveAt(lastIdx);
            _animal.PoolIndex = -1;

            if (cullingGroup != null)
            {
                cullingGroup.SetBoundingSphereCount(allSpawnedAnimals.Count);
            }
        }
    }

    private void StopGrowth()
    {
        if (growthCoroutine != null)
        {
            StopCoroutine(growthCoroutine);
            growthCoroutine = null;
        }
        StopAllCoroutines();
    }

    // // 풀링 콜백 메서드

    private void OnGetAnimal(Animal _animal)
    {
        _animal.Reset();
    }

    private void OnReleaseAnimal(Animal _animal)
    {
        // 최적화: 업데이트 리스트에서 제거
        UpdateAnimalVisibility(_animal, false);

        // 최적화: 마스터 리스트에서 Swap-with-last O(1) 증분 업데이트로 제거
        RemoveFromMasterList(_animal);

        _animal.PoolIndex = -1;
        _animal.UpdateIndex = -1;
        _animal.DeActivate();
        
        // 이미 부모가 꺼진 상태에서 호출되어도 안전함
        if (_animal.gameObject.activeSelf)
        {
            _animal.gameObject.SetActive(false);
        }
    }

    private void OnDestroyAnimal(Animal _animal)
    {
        if (_animal != null && _animal.gameObject != null)
        {
            Destroy(_animal.gameObject);
        }
    }

    private void OnDestroy()
    {
        if (cullingGroup != null)
        {
            cullingGroup.onStateChanged = null;
            cullingGroup.Dispose();
            cullingGroup = null;
        }
    }

    private void AnimalIsDead(Animal _animal)
    {
        environmentProvider.densityProvider.UpdateAnimalCnt(false);
        AnimalIsDeadEvent?.Invoke(_animal);
        ReleaseAnimal(_animal);
    }

    private void AnimalHit(Animal _animal)
    {
        AnimalHitEvent?.Invoke(_animal);
    }
}
