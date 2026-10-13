using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[CreateAssetMenu(fileName = "Stage3TreeGenerationStrategy", menuName = "Baobab/ObjectSystem/TreeGenerationStrategy/Stage3")]
public class Stage3TreeGenerationStrategySO : TreeGenerationStrategySO
{
    private Dictionary<TreeObj, int> treeTriggerGroupDict = new Dictionary<TreeObj, int>();
    private Dictionary<int, List<Vector3>> groupStarPositions = new Dictionary<int, List<Vector3>>();
    private Dictionary<int, int> groupRemainingStarCount = new Dictionary<int, int>();
    private int nextGroupId = 0;

    public override IEnumerator SpawnInitialTrees(InDungeonObjectManager _manager, List<Vector3> _grassTilePositions)
    {
        _manager.ClearAvailablePositions();
        treeTriggerGroupDict.Clear();
        groupStarPositions.Clear();
        groupRemainingStarCount.Clear();
        nextGroupId = 0;

        // 별이 넘지 못하는 링 반지름 - 생성 시점의 발현 폭발 범위 기준이라, 범위 증가 특성이 있으면 별자리도 그만큼 넓게 구성될 수 있다.
        starMaxReach = _manager.CurrentConstellationBlastRadius * StarMaxReachRatio;

        List<Vector3Int> availableList = new List<Vector3Int>();
        HashSet<Vector3Int> availableSet = new HashSet<Vector3Int>();

        for (int i = 0; i < _grassTilePositions.Count; i++)
        {
            Vector3Int cellPos = _manager.EnvironmentProvider.tilemapDataProvider.WorldToCell(_grassTilePositions[i]);
            availableList.Add(cellPos);
            availableSet.Add(cellPos);
        }

        int startCount = _manager.EnvironmentProvider.densityProvider.GetTreeStartCnt(currentMapType);
        int estimatedSpawnCount = 0;

        List<Vector3Int> clusterSeeds = new List<Vector3Int>();
        List<List<Vector3Int>> clusterGroups = new List<List<Vector3Int>>();

        int currentClusterSpacing = 12;
        int minClusterSize = 6;
        int maxSeedTries = 30;

        // 1 & 2단계: 간격을 줄여가며 최대한 많은 군집(최소 6그루 이상) 확보
        while (estimatedSpawnCount < startCount && currentClusterSpacing >= 6)
        {
            Vector3Int seed = Vector3Int.zero;
            bool seedFound = false;

            for (int t = 0; t < maxSeedTries; t++)
            {
                int r = UnityEngine.Random.Range(0, availableList.Count);
                Vector3Int candidate = availableList[r];

                if (!availableSet.Contains(candidate)) continue;

                bool tooClose = false;
                for (int i = 0; i < clusterSeeds.Count; i++)
                {
                    if (Vector3.Distance(candidate, clusterSeeds[i]) < currentClusterSpacing)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    seed = candidate;
                    seedFound = true;
                    break;
                }
            }

            if (!seedFound)
            {
                currentClusterSpacing -= 3; // 간격 완화 (12 -> 9 -> 6)
                continue;
            }

            List<Vector3Int> currentClusterPositions = new List<Vector3Int>();
            List<Vector3Int> candidatesInRadius = new List<Vector3Int>();
            int radius = 5; 
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    Vector3Int pos = new Vector3Int(seed.x + dx, seed.y + dy, 0);
                    if (Vector3.Distance(seed, pos) <= radius && availableSet.Contains(pos))
                    {
                        candidatesInRadius.Add(pos);
                    }
                }
            }

            ShuffleList(candidatesInRadius);
            int targetClusterSize = UnityEngine.Random.Range(8, 13);
            
            for (int i = 0; i < candidatesInRadius.Count; i++)
            {
                Vector3Int candidate = candidatesInRadius[i];
                bool tooCloseToOtherTree = false;

                for (int j = 0; j < currentClusterPositions.Count; j++)
                {
                    if (Vector3.Distance(candidate, currentClusterPositions[j]) < 1.4f)
                    {
                        tooCloseToOtherTree = true;
                        break;
                    }
                }

                if (!tooCloseToOtherTree)
                {
                    currentClusterPositions.Add(candidate);
                    
                    if (currentClusterPositions.Count >= targetClusterSize)
                        break;
                }
            }

            if (currentClusterPositions.Count < minClusterSize)
            {
                availableSet.Remove(seed);
                continue;
            }

            // 군집 확정
            clusterSeeds.Add(seed);
            clusterGroups.Add(currentClusterPositions);
            foreach (var pos in currentClusterPositions)
            {
                availableSet.Remove(pos);
            }
            
            estimatedSpawnCount += currentClusterPositions.Count;
        }

        // 3단계: 남은 개수가 있다면 기존 군집 살찌우기 (외곽 반경 확장)
        if (estimatedSpawnCount < startCount && clusterSeeds.Count > 0)
        {
            int maxFatTries = 50;
            int fatTries = 0;

            while (estimatedSpawnCount < startCount && fatTries < maxFatTries)
            {
                fatTries++;
                
                int clusterIdx = UnityEngine.Random.Range(0, clusterSeeds.Count);
                Vector3Int seed = clusterSeeds[clusterIdx];
                List<Vector3Int> groupPositions = clusterGroups[clusterIdx];

                int expandRadius = 7; 
                List<Vector3Int> candidatesInRadius = new List<Vector3Int>();
                for (int dx = -expandRadius; dx <= expandRadius; dx++)
                {
                    for (int dy = -expandRadius; dy <= expandRadius; dy++)
                    {
                        Vector3Int pos = new Vector3Int(seed.x + dx, seed.y + dy, 0);
                        if (Vector3.Distance(seed, pos) <= expandRadius && availableSet.Contains(pos))
                        {
                            candidatesInRadius.Add(pos);
                        }
                    }
                }

                ShuffleList(candidatesInRadius);

                foreach (var candidate in candidatesInRadius)
                {
                    bool tooCloseToOtherTree = false;
                    for (int j = 0; j < groupPositions.Count; j++)
                    {
                        if (Vector3.Distance(candidate, groupPositions[j]) < 1.4f)
                        {
                            tooCloseToOtherTree = true;
                            break;
                        }
                    }

                    if (!tooCloseToOtherTree)
                    {
                        groupPositions.Add(candidate);
                        availableSet.Remove(candidate);
                        estimatedSpawnCount++;
                        
                        if (estimatedSpawnCount >= startCount) break;
                    }
                }
            }
        }

        // 최종 생성 및 트리거 그룹 부여
        float frameStart = Time.realtimeSinceStartup;
        int countThisFrame = 0;

        // 군집 씨앗의 월드 좌표 - 별자리 영역(어느 씨앗에 가장 가까운가) 판정에 쓴다. clusterSeeds와 clusterGroups는 같은 순서다.
        clusterSeedWorldPositions.Clear();
        for (int i = 0; i < clusterSeeds.Count; i++)
        {
            clusterSeedWorldPositions.Add(_manager.EnvironmentProvider.tilemapDataProvider.CellToWorld(clusterSeeds[i]));
        }

        for (int i = 0; i < clusterGroups.Count; i++)
        {
            List<Vector3Int> group = clusterGroups[i];
            if (group.Count == 0) continue;

            int groupId = nextGroupId++;
            int triggerCount = UnityEngine.Random.Range(group.Count / 3, group.Count / 2 + 1);
            triggerCount = Mathf.Clamp(triggerCount, MinStarsPerConstellation, MaxStarsPerConstellation);

            ShuffleList(group);

            // 그룹의 나무를 먼저 전부 스폰한 뒤, 실제로 스폰된 나무 중에서 별 표식 나무를 고른다
            // (스폰이 막힌 칸이 별로 뽑혀 별자리가 비는 일이 없게 한다).
            spawnedGroupTrees.Clear();
            spawnedGroupRootPositions.Clear();
            spawnedGroupTopPositions.Clear();

            for (int j = 0; j < group.Count; j++)
            {
                Vector3 worldPos = _manager.EnvironmentProvider.tilemapDataProvider.CellToWorld(group[j]);
                TreeObj spawnedTree = _manager.SpawnTreeAt(worldPos, false);

                if (spawnedTree != null)
                {
                    // 밑동(worldPos) 대신 top 위치를 스폰 시점에 딱 한 번 캐싱해서 저장한다. 나무는
                    // 스폰된 뒤 움직이지 않고 topRoot 오프셋도 성장 단계와 무관하게 고정이므로, 이
                    // 값은 나무가 나중에 죽어서 풀로 반환/재사용되더라도(TreeObj 참조와 달리) 계속
                    // 유효하다 - 별자리 발현이 언제(때렸을 때/그룹 전체 벌목 시) 트리거되든 안전하다.
                    Vector3 topPos = spawnedTree.treeVisualComponent != null
                        ? spawnedTree.treeVisualComponent.GetTopRootPosition()
                        : worldPos;

                    spawnedGroupTrees.Add(spawnedTree);
                    spawnedGroupRootPositions.Add(worldPos);
                    spawnedGroupTopPositions.Add(topPos);

                    countThisFrame++;
                    bool overBudget = (Time.realtimeSinceStartup - frameStart) * 1000f >= _manager.TreeSpawnFrameTimeBudgetMs;
                    if (countThisFrame >= _manager.TreeSpawnMinPerFrame && (countThisFrame >= _manager.TreeSpawnMaxPerFrame || overBudget))
                    {
                        yield return null;
                        frameStart = Time.realtimeSinceStartup;
                        countThisFrame = 0;
                    }
                }
            }

            // 스폰을 여러 프레임에 나눠 하는 사이 이미 풀로 돌아간 나무는 별 후보에서 뺀다(별이 3개 아래로 줄지 않게, 고르기 전에 거른다).
            RemovePooledSpawnedTrees();

            // 심어진 나무가 3그루도 안 되는 군집(칸이 막힌 경우)은 별자리를 만들 수 없다.
            triggerCount = Mathf.Min(triggerCount, spawnedGroupTrees.Count);
            if (triggerCount < MinStarsPerConstellation) continue;

            SelectConstellationStarsInTerritory(i, triggerCount, selectedStarIndices);

            if (selectedStarIndices.Count >= MinStarsPerConstellation)
            {
                AssignConstellationStars(_manager, groupId, selectedStarIndices);
            }
            else
            {
                // 자기 영역 안에 나무가 3그루도 없는 군집(아주 드묾)은 다른 별자리를 모두 놓은 뒤 따로 고른다.
                deferredConstellations.Add(new DeferredConstellation
                {
                    groupId = groupId,
                    starCount = triggerCount,
                    trees = new List<TreeObj>(spawnedGroupTrees),
                    rootPositions = new List<Vector3>(spawnedGroupRootPositions),
                    topPositions = new List<Vector3>(spawnedGroupTopPositions)
                });
            }
        }

        // 영역 안에서 별을 못 고른 군집 - 군집 나무 전체에서 고르되, 이미 놓인 별자리 다각형과 겹치지 않는 조합을 찾는다.
        // 별 개수(3~5)가 겹침 회피보다 우선이라, 겹치지 않는 조합이 끝내 없으면 가장 덜 겹치는 조합을 쓴다.
        if (0 < deferredConstellations.Count)
        {
            BuildPlacedConstellationPolygons();

            for (int i = 0; i < deferredConstellations.Count; i++)
            {
                DeferredConstellation deferred = deferredConstellations[i];

                spawnedGroupTrees.Clear();
                spawnedGroupTrees.AddRange(deferred.trees);
                spawnedGroupRootPositions.Clear();
                spawnedGroupRootPositions.AddRange(deferred.rootPositions);
                spawnedGroupTopPositions.Clear();
                spawnedGroupTopPositions.AddRange(deferred.topPositions);
                RemovePooledSpawnedTrees();

                int starCount = Mathf.Min(deferred.starCount, spawnedGroupTrees.Count);
                if (starCount < MinStarsPerConstellation) continue;

                // 링 안에 드는 별 3개 묶음조차 없는 군집(나무가 너무 흩어진 경우)은 별자리를 만들지 않는다.
                SelectConstellationStarsAvoidingPlaced(starCount, selectedStarIndices);
                if (selectedStarIndices.Count < MinStarsPerConstellation) continue;

                AssignConstellationStars(_manager, deferred.groupId, selectedStarIndices);
                AddPlacedConstellationPolygon(spawnedGroupTopPositions, selectedStarIndices);
            }

            deferredConstellations.Clear();
        }
    }

    // 고른 나무들에 별 표식을 달고 그룹 정보를 기록한 뒤 별자리 점선 이음선을 등록한다("별자리 발현" 특성이 있을 때만 보인다).
    // 고르기와 이 함수 사이에는 프레임 양보가 없어 고른 나무가 그 사이 풀로 돌아가지 않는다.
    private void AssignConstellationStars(InDungeonObjectManager _manager, int _groupId, List<int> _starIndices)
    {
        List<Vector3> positions = new List<Vector3>(_starIndices.Count);
        groupStarPositions[_groupId] = positions;

        for (int j = 0; j < _starIndices.Count; j++)
        {
            int index = _starIndices[j];
            TreeObj starTree = spawnedGroupTrees[index];

            treeTriggerGroupDict[starTree] = _groupId;
            starTree.SetStarMarked(true);
            starTree.SetStarGroupId(_groupId);
            positions.Add(spawnedGroupTopPositions[index]);
        }

        groupRemainingStarCount[_groupId] = positions.Count;
        _manager.RegisterConstellationDottedLine(_groupId, positions);
    }

    // 풀로 돌아갔거나(IsPooled) 풀에서 다른 자리로 재사용된 나무를 스폰 목록에서 뺀다.
    private void RemovePooledSpawnedTrees()
    {
        for (int i = spawnedGroupTrees.Count - 1; i >= 0; i--)
        {
            TreeObj tree = spawnedGroupTrees[i];
            bool bGone = tree == null || tree.IsPooled
                || (tree.transform.position - spawnedGroupRootPositions[i]).sqrMagnitude > 0.0001f;
            if (!bGone) continue;

            spawnedGroupTrees.RemoveAt(i);
            spawnedGroupRootPositions.RemoveAt(i);
            spawnedGroupTopPositions.RemoveAt(i);
        }
    }

    public override IEnumerator GrowthRoutine(InDungeonObjectManager _manager)
    {
        yield break;
    }

    public override void OnTreeDead(InDungeonObjectManager _manager, TreeObj _treeObj, Vector3 _deadPos)
    {
        if (!treeTriggerGroupDict.TryGetValue(_treeObj, out int groupId)) return;

        treeTriggerGroupDict.Remove(_treeObj);

        // 별길 걸음: 별 표식 나무를 벌목할 때마다 발동
        _manager.TriggerStarPathSpeedBoost();

        if (!groupRemainingStarCount.TryGetValue(groupId, out int remaining)) return;

        remaining--;
        groupRemainingStarCount[groupId] = remaining;

        if (remaining <= 0 && groupStarPositions.TryGetValue(groupId, out List<Vector3> positions))
        {
            // 별자리 발현: 그룹의 모든 별 표식 나무가 벌목됨
            _manager.TriggerConstellationManifestation(groupId, positions);
        }
    }

    // OnTreeGetHit은 별 표식 나무를 "때릴 때마다" 호출된다(쇼크웨이브/부메랑으로 여러 그루가 동시에
    // 맞으면 한 프레임에 수십 번). 예전엔 여기서 보간 문자열 + _treeObj.name(네이티브 마샬링)으로
    // Debug.Log를 찍고 있었는데, 출시 빌드에서는 순수 비용이자 로그 노이즈라 제거했다.
    // 별자리 발현은 OnTreeDead에서만 판정하므로 이 훅에서 할 일은 없다(베이스의 빈 구현을 그대로 쓴다).

    // 별자리 모양 제약 - 별자리 발현은 별 좌표 평균(중심)에서 모든 별자리에 같은 반지름의 타원
    // (InDungeonObjectManager.CurrentConstellationBlastRadius)으로 터지므로, 별이 그 마법진 링 안쪽에 들어오고(필수)
    // 별자리가 너무 작거나 한 점에 뭉치지 않게 별 표식 나무를 고른다.
    // 거리는 모두 쿼터뷰 Y축 2배 보정 거리(월드 유닛)다. 군집(반경 5~7칸) 안에서 대부분 첫 몇 번의 시도로 조건을 만족한다.
    private const int MinStarsPerConstellation = 3;      // 별 2개는 선분이라 다각형 중심이 성립하지 않는다
    private const int MaxStarsPerConstellation = 5;
    private const float StarMinSpacing = 1.8f;           // 별끼리 최소 간격
    private const float StarMinReach = 2.0f;             // 중심에서 가장 먼 별까지 거리 하한 - 별자리가 너무 작지 않게(완화 가능)
    private const float StarMaxReachRatio = 0.85f;       // 상한(필수) - 링 반지름 대비. 별 표식이 링 선에 걸치지 않게 여유를 둔다
    private const int StarSelectAttempts = 40;

    // 이번 생성의 별 거리 상한(필수) - SpawnInitialTrees 시작 시 발현 폭발 범위 x StarMaxReachRatio로 정한다.
    private float starMaxReach = 3.4f;
    // 별 하나가 모자라는 것은 거리 조건을 얼마나 벗어나든 그보다 나쁘게 친다.
    private const float MissingStarPenalty = 100f;

    // 별자리 다각형 겹침 방지 - 별은 자기 군집 씨앗이 다른 모든 씨앗보다 가까운 나무(씨앗 기준 보로노이 영역)에서만 고른다.
    // 영역은 볼록하고 서로 겹치지 않으므로, 꼭짓점이 모두 자기 영역 안이면 다각형(꼭짓점의 볼록 껍질 안에 있음)도
    // 자기 영역 안에 있어 다른 별자리 다각형과 절대 겹치지 않는다. 영역 판정은 밑동 좌표로 하는데, 점선과 레이저가 쓰는
    // 윗부분 좌표는 모든 나무가 같은 topRoot 오프셋만큼 평행이동한 것이라 겹치지 않는다는 결론이 그대로 유지된다.
    // 별 개수(3~5)와 링 안쪽(starMaxReach)은 모든 단계에서 필수이고, 군집이 촘촘한 맵에서 영역이 좁으면 아래 순서로 나머지 조건을 완화한다.
    //   1) 영역 경계에서 TerritoryMargin만큼 안쪽(이웃 별자리와 최소 이 간격만큼 떨어짐) + 별 간격 StarMinSpacing
    //   2) 영역 경계까지 허용
    //   3) 별 간격을 StarFallbackSpacing으로 완화
    //   4) 별 간격 조건 없이 영역 안 나무 아무거나
    //   5) 영역 안에서 못 고르면(아주 드묾) 다른 별자리를 모두 놓은 뒤, 군집 나무 전체에서 이미 놓인 다각형과 실제로
    //      겹치지 않는 조합을 찾는다. 끝내 없으면 가장 덜 겹치는 조합을 쓴다(별 개수와 링 조건이 겹침 회피보다 우선).
    //      무작위 시도로도 못 고르면 가까운 나무끼리 묶는 확정 탐색(SelectCompactStars)을 하고, 링 안에 드는 3그루
    //      묶음조차 없을 때만 그 군집은 별자리를 만들지 않는다.
    private const float TerritoryMargin = 0.5f;
    private const float StarFallbackSpacing = 1.4f;
    // 5)단계에서 이미 놓인 다각형 하나와 겹칠 때마다 더하는 벌점 - 별 부족(MissingStarPenalty)보다 가볍고 거리 조건보다 무겁다.
    private const float OverlapPenalty = 10f;

    // 별 표식 나무 선택용 버퍼 - 생성 코루틴 안에서만 쓰고 그룹마다 비운다.
    private readonly List<Vector3> clusterSeedWorldPositions = new List<Vector3>(32);
    private readonly List<TreeObj> spawnedGroupTrees = new List<TreeObj>(16);
    private readonly List<Vector3> spawnedGroupRootPositions = new List<Vector3>(16);
    private readonly List<Vector3> spawnedGroupTopPositions = new List<Vector3>(16);
    private readonly List<int> selectedStarIndices = new List<int>(MaxStarsPerConstellation);
    private readonly List<int> fallbackStarIndices = new List<int>(MaxStarsPerConstellation);
    private readonly List<int> starCandidateOrder = new List<int>(16);
    private readonly List<int> starAttemptIndices = new List<int>(MaxStarsPerConstellation);
    private readonly List<int> compactOrder = new List<int>(16);
    private readonly List<float> compactDistances = new List<float>(16);

    // 5)단계로 미룬 군집. 생성 중 아주 드물게만 생기므로 목록을 그때그때 만든다.
    private class DeferredConstellation
    {
        public int groupId;
        public int starCount;
        public List<TreeObj> trees;
        public List<Vector3> rootPositions;
        public List<Vector3> topPositions;
    }
    private readonly List<DeferredConstellation> deferredConstellations = new List<DeferredConstellation>();

    // 이미 놓인 별자리 다각형(바닥 좌표 = Y 2배 보정, 점선/레이저와 같은 각도 순서). 꼭짓점을 한 목록에 이어 붙이고
    // 다각형마다 시작 위치와 개수를 따로 둔다. 5)단계에서만 쓴다.
    private readonly List<Vector2> placedPolygonPoints = new List<Vector2>(128);
    private readonly List<int> placedPolygonStarts = new List<int>(32);
    private readonly List<int> placedPolygonCounts = new List<int>(32);
    private readonly List<Vector2> candidatePolygon = new List<Vector2>(MaxStarsPerConstellation);
    private readonly List<float> polygonAngleBuffer = new List<float>(MaxStarsPerConstellation);

    // 이번 군집(_ownSeedIndex)의 스폰된 나무 중에서 별 표식 나무를 골라 _result에 인덱스로 채운다(위 1~4단계).
    // 영역 안에서 별을 MinStarsPerConstellation개 이상 고르지 못하면 _result를 비운다(5단계로 미룸).
    private void SelectConstellationStarsInTerritory(int _ownSeedIndex, int _count, List<int> _result)
    {
        _result.Clear();
        if (_count < MinStarsPerConstellation) return;

        CollectTerritoryCandidates(_ownSeedIndex, TerritoryMargin);
        SelectConstellationStars(spawnedGroupTopPositions, _count, StarMinSpacing, false, _result);
        if (_result.Count >= MinStarsPerConstellation) return;

        CollectTerritoryCandidates(_ownSeedIndex, 0f);
        SelectConstellationStars(spawnedGroupTopPositions, _count, StarMinSpacing, false, _result);
        if (_result.Count >= MinStarsPerConstellation) return;

        SelectConstellationStars(spawnedGroupTopPositions, _count, StarFallbackSpacing, false, _result);
        if (_result.Count >= MinStarsPerConstellation) return;

        SelectConstellationStars(spawnedGroupTopPositions, _count, 0f, false, _result);
        if (_result.Count >= MinStarsPerConstellation) return;

        _result.Clear();
    }

    // 5)단계 - 스폰된 나무 전체를 후보로, 별 간격을 단계적으로 완화하며 이미 놓인 다각형과 겹치지 않는 조합을 찾는다.
    // 마지막 단계(간격 조건 없음)는 후보가 _count개 이상이면 항상 _count개를 채우므로 별 개수가 보장된다.
    private void SelectConstellationStarsAvoidingPlaced(int _count, List<int> _result)
    {
        _result.Clear();
        starCandidateOrder.Clear();
        for (int i = 0; i < spawnedGroupTopPositions.Count; i++) starCandidateOrder.Add(i);

        float bestPenalty = SelectConstellationStars(spawnedGroupTopPositions, _count, StarMinSpacing, true, _result);
        if (bestPenalty <= 0f) return;

        float penalty = SelectConstellationStars(spawnedGroupTopPositions, _count, StarFallbackSpacing, true, fallbackStarIndices);
        if (penalty < bestPenalty)
        {
            bestPenalty = penalty;
            _result.Clear();
            _result.AddRange(fallbackStarIndices);
        }
        if (bestPenalty <= 0f) return;

        penalty = SelectConstellationStars(spawnedGroupTopPositions, _count, 0f, true, fallbackStarIndices);
        if (penalty < bestPenalty)
        {
            _result.Clear();
            _result.AddRange(fallbackStarIndices);
        }
        if (_result.Count >= MinStarsPerConstellation) return;

        // 무작위 시도로 링 안에 별 3개를 못 모았다면, 나무마다 가장 가까운 이웃들로 묶어 보는 확정 탐색을 한다.
        SelectCompactStars(_count, _result);
    }

    // 확정 탐색 - 각 나무를 기준으로 가장 가까운 나무들을 묶어 링 안에 드는 묶음 중 가장 작은 것을 고른다.
    // 별 개수를 _count부터 3까지 줄여 가며 찾고, 링 안에 드는 3그루 묶음조차 없으면 _result를 비운다(별자리를 만들지 않음).
    private void SelectCompactStars(int _count, List<int> _result)
    {
        _result.Clear();
        int candidateCount = spawnedGroupTopPositions.Count;

        for (int size = Mathf.Min(_count, candidateCount); size >= MinStarsPerConstellation; size--)
        {
            float bestReach = float.MaxValue;

            for (int anchor = 0; anchor < candidateCount; anchor++)
            {
                SortByDistanceFrom(anchor);

                starAttemptIndices.Clear();
                for (int i = 0; i < size; i++) starAttemptIndices.Add(compactOrder[i]);

                float reach = GetReachFromCenter(spawnedGroupTopPositions, starAttemptIndices);
                if (reach <= starMaxReach && reach < bestReach)
                {
                    bestReach = reach;
                    _result.Clear();
                    _result.AddRange(starAttemptIndices);
                }
            }

            if (_result.Count >= MinStarsPerConstellation) return;
        }

        _result.Clear();
    }

    // compactOrder를 _anchor(자기 자신 포함)에서 가까운 순서로 채운다. 군집 나무가 많아야 십수 그루라 삽입 정렬로 충분하다.
    private void SortByDistanceFrom(int _anchor)
    {
        compactOrder.Clear();
        compactDistances.Clear();
        Vector3 anchorPos = spawnedGroupTopPositions[_anchor];

        for (int i = 0; i < spawnedGroupTopPositions.Count; i++)
        {
            float distance = GetGroundDistance(spawnedGroupTopPositions[i], anchorPos);
            int insertAt = compactDistances.Count;
            while (insertAt > 0 && compactDistances[insertAt - 1] > distance) insertAt--;
            compactOrder.Insert(insertAt, i);
            compactDistances.Insert(insertAt, distance);
        }
    }

    private static float GetReachFromCenter(List<Vector3> _positions, List<int> _selected)
    {
        Vector3 center = Vector3.zero;
        for (int i = 0; i < _selected.Count; i++) center += _positions[_selected[i]];
        center /= _selected.Count;

        float reach = 0f;
        for (int i = 0; i < _selected.Count; i++)
        {
            reach = Mathf.Max(reach, GetGroundDistance(_positions[_selected[i]], center));
        }
        return reach;
    }

    // 스폰된 나무 중 자기 씨앗까지 거리가 다른 어떤 씨앗까지 거리보다 _margin 넘게 가까운 나무를 starCandidateOrder에 모은다.
    // (다른 씨앗까지 거리 - 자기 씨앗까지 거리 > _margin 인 점은 두 씨앗의 수직이등분선에서 _margin / 2 이상 떨어져 있다)
    private void CollectTerritoryCandidates(int _ownSeedIndex, float _margin)
    {
        starCandidateOrder.Clear();
        Vector3 ownSeed = clusterSeedWorldPositions[_ownSeedIndex];

        for (int i = 0; i < spawnedGroupRootPositions.Count; i++)
        {
            Vector3 rootPos = spawnedGroupRootPositions[i];
            float ownDistance = GetGroundDistance(rootPos, ownSeed);
            bool bInside = true;

            for (int s = 0; s < clusterSeedWorldPositions.Count; s++)
            {
                if (s == _ownSeedIndex) continue;
                if (GetGroundDistance(rootPos, clusterSeedWorldPositions[s]) - ownDistance <= _margin)
                {
                    bInside = false;
                    break;
                }
            }

            if (bInside) starCandidateOrder.Add(i);
        }
    }

    // starCandidateOrder(후보 인덱스) 중 _count개를 별자리 모양 제약에 맞게 골라 _result에 인덱스로 채우고 그 벌점을 돌려준다.
    // 무작위 순서로 간격 조건을 지키며 뽑는 시도를 반복하고, 조건을 모두 만족하면(벌점 0) 바로 채택한다. 끝내 만족하지 못하면
    // 가장 덜 벗어난 결과를 쓴다(별이 _count보다 적을 수 있다). 후보 밖의 나무로 채우지 않으므로 영역 제약은 항상 지켜진다.
    // _bAvoidPlaced면 이미 놓인 별자리 다각형과 겹치는 개수만큼 벌점을 더한다(5단계).
    private float SelectConstellationStars(List<Vector3> _positions, int _count, float _minSpacing, bool _bAvoidPlaced, List<int> _result)
    {
        _result.Clear();
        if (_count <= 0) return 0f;

        float bestPenalty = float.MaxValue;

        for (int attempt = 0; attempt < StarSelectAttempts; attempt++)
        {
            ShuffleList(starCandidateOrder);
            starAttemptIndices.Clear();

            for (int i = 0; i < starCandidateOrder.Count && starAttemptIndices.Count < _count; i++)
            {
                int candidate = starCandidateOrder[i];
                if (IsFarFromSelectedStars(_positions, starAttemptIndices, candidate, _minSpacing)
                    && FitsInsideRing(_positions, starAttemptIndices, candidate))
                {
                    starAttemptIndices.Add(candidate);
                }
            }

            float penalty = EvaluateStarSelectionPenalty(_positions, starAttemptIndices, _count);
            if (_bAvoidPlaced && starAttemptIndices.Count >= 2)
            {
                BuildGroundPolygon(_positions, starAttemptIndices, candidatePolygon);
                penalty += CountPlacedPolygonOverlaps(candidatePolygon) * OverlapPenalty;
            }

            if (penalty < bestPenalty)
            {
                bestPenalty = penalty;
                _result.Clear();
                _result.AddRange(starAttemptIndices);
            }

            if (penalty <= 0f) break;
        }

        return bestPenalty;
    }

    private static bool IsFarFromSelectedStars(List<Vector3> _positions, List<int> _selected, int _candidate, float _minSpacing)
    {
        for (int i = 0; i < _selected.Count; i++)
        {
            if (GetGroundDistance(_positions[_selected[i]], _positions[_candidate]) < _minSpacing) return false;
        }
        return true;
    }

    // _candidate를 _selected에 더했을 때, 그 별들의 중심(발현 폭발 중심과 같은 평균)에서 모든 별이 starMaxReach 안에 있으면 true.
    // 별을 하나 더할 때마다 전체를 다시 확인하므로, 이 검사를 거쳐 모인 별들은 항상 마법진 링 안쪽에 있다(필수 조건).
    private bool FitsInsideRing(List<Vector3> _positions, List<int> _selected, int _candidate)
    {
        Vector3 center = _positions[_candidate];
        for (int i = 0; i < _selected.Count; i++) center += _positions[_selected[i]];
        center /= _selected.Count + 1;

        if (GetGroundDistance(_positions[_candidate], center) > starMaxReach) return false;
        for (int i = 0; i < _selected.Count; i++)
        {
            if (GetGroundDistance(_positions[_selected[i]], center) > starMaxReach) return false;
        }
        return true;
    }

    // 0이면 모든 조건 만족. 모자란 별 개수와, 중심에서 가장 먼 별까지 거리가 StarMinReach에 못 미친 정도를 합한다.
    // (상한 starMaxReach는 고르는 단계에서 FitsInsideRing으로 이미 지켜진다)
    private static float EvaluateStarSelectionPenalty(List<Vector3> _positions, List<int> _selected, int _count)
    {
        float penalty = (_count - _selected.Count) * MissingStarPenalty;
        if (_selected.Count == 0) return penalty;

        Vector3 center = Vector3.zero;
        for (int i = 0; i < _selected.Count; i++) center += _positions[_selected[i]];
        center /= _selected.Count;

        float reach = 0f;
        for (int i = 0; i < _selected.Count; i++)
        {
            reach = Mathf.Max(reach, GetGroundDistance(_positions[_selected[i]], center));
        }

        penalty += Mathf.Max(0f, StarMinReach - reach);
        return penalty;
    }

    // 지금까지 놓인 모든 별자리를 바닥 좌표 다각형으로 모은다(5단계 시작 시 한 번).
    private void BuildPlacedConstellationPolygons()
    {
        placedPolygonPoints.Clear();
        placedPolygonStarts.Clear();
        placedPolygonCounts.Clear();

        foreach (KeyValuePair<int, List<Vector3>> pair in groupStarPositions)
        {
            starAttemptIndices.Clear();
            for (int i = 0; i < pair.Value.Count; i++) starAttemptIndices.Add(i);
            AddPlacedConstellationPolygon(pair.Value, starAttemptIndices);
        }
    }

    private void AddPlacedConstellationPolygon(List<Vector3> _positions, List<int> _indices)
    {
        BuildGroundPolygon(_positions, _indices, candidatePolygon);
        placedPolygonStarts.Add(placedPolygonPoints.Count);
        placedPolygonCounts.Add(candidatePolygon.Count);
        placedPolygonPoints.AddRange(candidatePolygon);
    }

    // _indices의 별을 바닥 좌표(Y 2배 보정)로 바꿔 중심 기준 각도 순으로 _output에 채운다. 점선과 레이저가 꼭짓점을
    // 잇는 순서(ConstellationPixelLaser.UntanglePoints의 각도 정렬)와 같아, 화면에 그려지는 다각형 그대로다.
    private void BuildGroundPolygon(List<Vector3> _positions, List<int> _indices, List<Vector2> _output)
    {
        _output.Clear();
        polygonAngleBuffer.Clear();

        Vector2 center = Vector2.zero;
        for (int i = 0; i < _indices.Count; i++)
        {
            Vector3 p = _positions[_indices[i]];
            Vector2 ground = new Vector2(p.x, p.y * 2f);
            _output.Add(ground);
            center += ground;
        }
        center /= _output.Count;

        for (int i = 0; i < _output.Count; i++)
        {
            polygonAngleBuffer.Add(Mathf.Atan2(_output[i].y - center.y, _output[i].x - center.x));
        }

        // 꼭짓점이 5개 이하라 삽입 정렬로 충분하다.
        for (int i = 1; i < _output.Count; i++)
        {
            Vector2 point = _output[i];
            float angle = polygonAngleBuffer[i];
            int j = i - 1;
            while (j >= 0 && polygonAngleBuffer[j] > angle)
            {
                _output[j + 1] = _output[j];
                polygonAngleBuffer[j + 1] = polygonAngleBuffer[j];
                j--;
            }
            _output[j + 1] = point;
            polygonAngleBuffer[j + 1] = angle;
        }
    }

    // _polygon과 겹치거나 TerritoryMargin보다 가까운 이미 놓인 다각형 개수.
    private int CountPlacedPolygonOverlaps(List<Vector2> _polygon)
    {
        int overlaps = 0;
        for (int k = 0; k < placedPolygonStarts.Count; k++)
        {
            if (ArePolygonsTooClose(_polygon, placedPolygonStarts[k], placedPolygonCounts[k])) overlaps++;
        }
        return overlaps;
    }

    // 두 다각형의 변이 교차하거나 TerritoryMargin보다 가까우면, 또는 한쪽이 다른 쪽 안에 들어 있으면 true.
    private bool ArePolygonsTooClose(List<Vector2> _polygon, int _placedStart, int _placedCount)
    {
        int count = _polygon.Count;

        for (int i = 0; i < count; i++)
        {
            Vector2 a0 = _polygon[i];
            Vector2 a1 = _polygon[(i + 1) % count];

            for (int j = 0; j < _placedCount; j++)
            {
                Vector2 b0 = placedPolygonPoints[_placedStart + j];
                Vector2 b1 = placedPolygonPoints[_placedStart + (j + 1) % _placedCount];

                if (DoSegmentsCross(a0, a1, b0, b1)) return true;

                float distance = Mathf.Min(
                    Mathf.Min(DistancePointToSegment(a0, b0, b1), DistancePointToSegment(a1, b0, b1)),
                    Mathf.Min(DistancePointToSegment(b0, a0, a1), DistancePointToSegment(b1, a0, a1)));
                if (distance < TerritoryMargin) return true;
            }
        }

        // 변끼리 만나지 않으면 한쪽이 통째로 다른 쪽 안에 있거나 완전히 떨어져 있다 - 꼭짓점 하나씩만 확인하면 된다.
        return IsPointInPolygon(_polygon[0], placedPolygonPoints, _placedStart, _placedCount)
            || IsPointInPolygon(placedPolygonPoints[_placedStart], _polygon, 0, count);
    }

    private static bool DoSegmentsCross(Vector2 _a0, Vector2 _a1, Vector2 _b0, Vector2 _b1)
    {
        float d1 = Cross(_b0, _b1, _a0);
        float d2 = Cross(_b0, _b1, _a1);
        float d3 = Cross(_a0, _a1, _b0);
        float d4 = Cross(_a0, _a1, _b1);
        return (d1 > 0f) != (d2 > 0f) && (d3 > 0f) != (d4 > 0f);
    }

    private static float Cross(Vector2 _origin, Vector2 _a, Vector2 _b)
    {
        return (_a.x - _origin.x) * (_b.y - _origin.y) - (_a.y - _origin.y) * (_b.x - _origin.x);
    }

    private static float DistancePointToSegment(Vector2 _point, Vector2 _a, Vector2 _b)
    {
        Vector2 ab = _b - _a;
        float abLenSq = ab.sqrMagnitude;
        float t = abLenSq < 0.0001f ? 0f : Mathf.Clamp01(Vector2.Dot(_point - _a, ab) / abLenSq);
        return Vector2.Distance(_point, _a + ab * t);
    }

    private static bool IsPointInPolygon(Vector2 _point, List<Vector2> _points, int _start, int _count)
    {
        bool bInside = false;
        for (int i = 0, j = _count - 1; i < _count; j = i++)
        {
            Vector2 a = _points[_start + i];
            Vector2 b = _points[_start + j];
            if ((a.y > _point.y) != (b.y > _point.y)
                && _point.x < (b.x - a.x) * (_point.y - a.y) / (b.y - a.y) + a.x)
            {
                bInside = !bInside;
            }
        }
        return bInside;
    }

    // 쿼터뷰 바닥 거리 - Y축 차이를 2배로 보정한 거리. 별자리 발현 폭발 판정과 같은 기준이다.
    private static float GetGroundDistance(Vector3 _a, Vector3 _b)
    {
        float dx = _a.x - _b.x;
        float dy = (_a.y - _b.y) * 2f;
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    private void ShuffleList(List<int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, list.Count);
            int temp = list[i];
            list[i] = list[r];
            list[r] = temp;
        }
    }

    private void ShuffleArray(Vector3Int[] array)
    {
        for (int i = 0; i < array.Length; i++)
        {
            int r = UnityEngine.Random.Range(i, array.Length);
            Vector3Int temp = array[i];
            array[i] = array[r];
            array[r] = temp;
        }
    }

    private void ShuffleList(List<Vector3Int> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int r = UnityEngine.Random.Range(i, list.Count);
            Vector3Int temp = list[i];
            list[i] = list[r];
            list[r] = temp;
        }
    }
}
