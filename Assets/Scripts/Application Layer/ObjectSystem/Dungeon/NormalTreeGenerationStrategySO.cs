using UnityEngine;
using System.Collections.Generic;
using System.Collections;

[CreateAssetMenu(fileName = "NormalTreeGenerationStrategy", menuName = "Baobab/ObjectSystem/TreeGenerationStrategy/Normal")]
public class NormalTreeGenerationStrategySO : TreeGenerationStrategySO
{
    public override void SpawnInitialTrees(InDungeonObjectManager _manager, List<Vector3> _grassTilePositions)
    {
        _manager.ClearAvailablePositions();
        for (int i = 0; i < _grassTilePositions.Count; i++)
        {
            _manager.AddAvailablePosition(_grassTilePositions[i]);
        }
        _manager.ShuffleAvailablePositions();

        int startCount = _manager.EnvironmentProvider.densityProvider.GetTreeStartCnt(currentMapType);
        for (int i = 0; i < startCount; i++)
        {
            _manager.SpawnOneTreeFromAvailable(false);
        }
    }

    public override IEnumerator GrowthRoutine(InDungeonObjectManager _manager)
    {
        while (true)
        {
            float baseInterval = _manager.EnvironmentProvider.densityProvider.GetTreeRegenTime();
            // speedMul이 1 이상이 되어 시간이 0이 되면 무한루프에 빠질 수 있으므로, 최소 대기 시간(0.1초) 보장
            float interval = Mathf.Max(0.1f, baseInterval * (1f - _manager.GrowthSpeedMul));
            
            yield return new WaitForSeconds(interval);

            if (_manager.EnvironmentProvider.densityProvider.CanCreateTree(currentMapType) && _manager.AvailablePositionsCount > 0)
            {
                _manager.SpawnOneTreeFromAvailable(true);
            }
        }
    }

    public override void OnTreeDead(InDungeonObjectManager _manager, TreeObj _treeObj, Vector3 _deadPos)
    {
        _manager.AddAvailablePosition(_deadPos);

        if (_manager.AvailablePositionsCount > 1)
        {
            _manager.SwapRandomAvailablePositionWithLast();
        }
    }
}
