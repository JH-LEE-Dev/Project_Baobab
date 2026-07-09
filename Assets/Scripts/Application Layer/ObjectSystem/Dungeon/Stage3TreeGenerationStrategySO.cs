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

    public override void SpawnInitialTrees(InDungeonObjectManager _manager, List<Vector3> _grassTilePositions)
    {
        _manager.ClearAvailablePositions();
        treeTriggerGroupDict.Clear();
        groupStarPositions.Clear();
        groupRemainingStarCount.Clear();
        nextGroupId = 0;

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
        for (int i = 0; i < clusterGroups.Count; i++)
        {
            List<Vector3Int> group = clusterGroups[i];
            if (group.Count == 0) continue;

            int groupId = nextGroupId++;
            int triggerCount = UnityEngine.Random.Range(group.Count / 3, group.Count / 2 + 1);
            triggerCount = Mathf.Clamp(triggerCount, 2, 5);
            triggerCount = Mathf.Min(triggerCount, group.Count);

            ShuffleList(group);

            int actualTriggerAssigned = 0;

            for (int j = 0; j < group.Count; j++)
            {
                Vector3 worldPos = _manager.EnvironmentProvider.tilemapDataProvider.CellToWorld(group[j]);
                TreeObj spawnedTree = _manager.SpawnTreeAt(worldPos, false);

                if (spawnedTree != null)
                {
                    if (actualTriggerAssigned < triggerCount)
                    {
                        treeTriggerGroupDict[spawnedTree] = groupId;
                        spawnedTree.SetStarMarked(true);

                        if (!groupStarPositions.TryGetValue(groupId, out List<Vector3> positions))
                        {
                            positions = new List<Vector3>();
                            groupStarPositions[groupId] = positions;
                        }
                        positions.Add(worldPos);

                        groupRemainingStarCount.TryGetValue(groupId, out int currentCount);
                        groupRemainingStarCount[groupId] = currentCount + 1;

                        actualTriggerAssigned++;
                    }
                }
            }
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
            _manager.TriggerConstellationManifestation(positions);
        }
    }

    public override void OnTreeGetHit(InDungeonObjectManager _manager, TreeObj _treeObj)
    {
        if (treeTriggerGroupDict.TryGetValue(_treeObj, out int groupId))
        {
            Debug.Log($"[Stage3] Trigger Group {groupId} hit! (Tree: {_treeObj.name}) - Prepare for chain reaction.");
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
