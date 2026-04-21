using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class InDungeonUnitSpawner : MonoBehaviour
{
    public event Action<Animal> AnimalIsDeadEvent;
    //그룹 정보를 넘기지 않도록 이벤트 수정 (필요 시 Action<IReadOnlyList<Animal>> 등으로 변경 가능)
    public event Action AnimalSpawnedEvent;

    //외부 의존성
    private IEnvironmentProvider environmentProvider;
    private ITilemapDataProvider tilemapDataProvider;

    //내부 의존성
    [Header("Spawn Settings")]
    [SerializeField] private Animal animalPrefab;
    [SerializeField] private float spawnInterval = 2.0f;

    private IObjectPool<Animal> animalPool;
    private Coroutine growthCoroutine;
    private WaitForSeconds spawnYield;

    private List<Animal> allSpawnedAnimals = new List<Animal>(SYSTEM_VAR.MAX_ANIMAL_CNT);
    private List<int> availableIndices = new List<int>(1024); // GC 방지용 캐싱 인덱스 리스트

    [Header("Optimization")]
    [SerializeField] private float cullingDistance = 25f;
    private CullingGroup cullingGroup;
    private BoundingSphere[] spheres;
    private float[] cullingDistances;
    private CullingGroup.StateChanged onCullingStateChangedDelegate;
    private bool isCullingDirty = false;

    // // 풀 설정 변수
    [SerializeField] private bool collectionCheck = false; // 에디터 성능을 위해 false로 설정
    [SerializeField] private int defaultCapacity = 200;
    [SerializeField] private int maxSize = SYSTEM_VAR.MAX_ANIMAL_CNT;

    // // 퍼블릭 메서드
    public void Initialize(IEnvironmentProvider _environmentProvider)
    {
        environmentProvider = _environmentProvider;
        tilemapDataProvider = environmentProvider.tilemapDataProvider;

        cullingDistances = new float[] { cullingDistance };
        spheres = new BoundingSphere[maxSize];
        onCullingStateChangedDelegate = OnCullingStateChanged;

        spawnYield = new WaitForSeconds(spawnInterval);

        animalPool = new ObjectPool<Animal>(
            CreateAnimal,
            OnGetAnimal,
            OnReleaseAnimal,
            OnDestroyAnimal,
            collectionCheck,
            defaultCapacity,
            maxSize
        );
    }

    public void SpawnAnimals()
    {
        if (tilemapDataProvider == null || animalPrefab == null || environmentProvider.densityProvider == null)
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
            yield return spawnYield;

            if (environmentProvider.densityProvider.CanCreateAnimal())
            {
                if (SpawnOneAnimalFromAvailable())
                {
                    isCullingDirty = true;
                }
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
        }

        cullingGroup.SetBoundingSpheres(spheres);
        cullingGroup.SetBoundingSphereCount(count);

        for (int i = 0; i < count; i++)
        {
            bool isVisible = cullingGroup.IsVisible(i);
            bool isNear = cullingGroup.GetDistance(i) == 0;
            bool shouldBeActive = isVisible && isNear;

            if (shouldBeActive == false)
            {
                allSpawnedAnimals[i].Hide();
            }
            else
            {    
                allSpawnedAnimals[i].Show();
            }
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
        Animal animal = allSpawnedAnimals[_ev.index];

        if (animal != null)
        {
            if (shouldBeActive == false)
            { 
                animal.Hide();
            }
            else
            { 
                animal.Show();
            }
        }
    }

    private void Update()
    {
        if (cullingGroup != null && allSpawnedAnimals.Count > 0)
        {
            UpdateCullingSpheres();
        }

        if (isCullingDirty)
        {
            RefreshCullingGroup();
            isCullingDirty = false;
        }
    }

    private void SpawnAnimalAt(Vector3 _pos)
    {
        Animal animal = animalPool.Get();
        animal.transform.position = _pos;
        animal.gameObject.SetActive(true);
        animal.Initialize(environmentProvider);

        allSpawnedAnimals.Add(animal);
        environmentProvider.densityProvider.UpdateAnimalCnt(true);
        isCullingDirty = true;
        animal.AnimalIsDeadEvent -= AnimalIsDead;
        animal.AnimalIsDeadEvent += AnimalIsDead;
    }

    public void ReleaseAnimal(Animal _animal)
    {
        _animal.AnimalIsDeadEvent -= AnimalIsDead;

        if (_animal.gameObject.activeSelf)
        {
            animalPool.Release(_animal);
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

        if (allSpawnedAnimals == null || animalPool == null) return;

        // [최적화 핵심] 부모를 잠시 비활성화하여 에디터 Hierarchy 갱신 이벤트를 1번으로 압축
        this.gameObject.SetActive(false);

        // 리스트를 역순으로 순회하며 안전하게 해제
        for (int i = allSpawnedAnimals.Count - 1; i >= 0; i--)
        {
            Animal animal = allSpawnedAnimals[i];
            if (animal != null)
            {
                animal.AnimalIsDeadEvent -= AnimalIsDead;

                animal.DeActivate();
                animalPool.Release(animal);
                environmentProvider.densityProvider.UpdateAnimalCnt(false);
            }
        }

        allSpawnedAnimals.Clear();
        isCullingDirty = true;

        // 작업 완료 후 부모 재활성화
        this.gameObject.SetActive(true);
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

    private Animal CreateAnimal()
    {
        return Instantiate(animalPrefab, transform);
    }

    private void OnGetAnimal(Animal _animal)
    {
        _animal.Reset();
    }

    private void OnReleaseAnimal(Animal _animal)
    {
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
        allSpawnedAnimals.Remove(_animal);
        isCullingDirty = true;
        ReleaseAnimal(_animal);
    }
}
