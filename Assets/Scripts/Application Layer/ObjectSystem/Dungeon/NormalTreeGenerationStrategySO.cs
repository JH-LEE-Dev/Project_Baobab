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

        int startCount = _manager.EnvironmentProvider.densityProvider.GetTreeStartCnt();
        for (int i = 0; i < startCount; i++)
        {
            _manager.SpawnOneTreeFromAvailable(false);
        }
    }

    public override IEnumerator GrowthRoutine(InDungeonObjectManager _manager)
    {
        while (true)
        {
            float interval = _manager.EnvironmentProvider.densityProvider.GetTreeRegenTime();
            yield return new WaitForSeconds(interval);

            if (_manager.EnvironmentProvider.densityProvider.CanCreateTree() && _manager.AvailablePositionsCount > 0)
            {
                _manager.SpawnOneTreeFromAvailable(true);
            }
        }
    }

    public override void OnTreeDead(InDungeonObjectManager _manager, Vector3 _deadPos)
    {
        _manager.AddAvailablePosition(_deadPos);

        if (_manager.AvailablePositionsCount > 1)
        {
            _manager.SwapRandomAvailablePositionWithLast();
        }
    }
}
