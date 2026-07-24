using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public abstract class TreeGenerationStrategySO : ScriptableObject
{
    [HideInInspector] public MapType currentMapType;
    // 나무 대량 Instantiate를 한 프레임에 몰아서 실행하지 않고 프레임에 걸쳐 분산시키기 위해 코루틴으로 실행한다.
    public abstract IEnumerator SpawnInitialTrees(InDungeonObjectManager _manager, List<Vector3> _grassTilePositions);
    public abstract IEnumerator GrowthRoutine(InDungeonObjectManager _manager);
    public abstract void OnTreeDead(InDungeonObjectManager _manager, TreeObj _treeObj, Vector3 _deadPos);
    public virtual void OnTreeGetHit(InDungeonObjectManager _manager, TreeObj _treeObj) { }
}
