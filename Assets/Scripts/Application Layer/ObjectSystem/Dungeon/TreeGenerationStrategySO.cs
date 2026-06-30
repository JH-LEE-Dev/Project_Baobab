using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public abstract class TreeGenerationStrategySO : ScriptableObject
{
    [HideInInspector] public MapType currentMapType;
    public abstract void SpawnInitialTrees(InDungeonObjectManager _manager, List<Vector3> _grassTilePositions);
    public abstract IEnumerator GrowthRoutine(InDungeonObjectManager _manager);
    public abstract void OnTreeDead(InDungeonObjectManager _manager, Vector3 _deadPos);
    public virtual void OnTreeGetHit(InDungeonObjectManager _manager, TreeObj _treeObj) { }
}
