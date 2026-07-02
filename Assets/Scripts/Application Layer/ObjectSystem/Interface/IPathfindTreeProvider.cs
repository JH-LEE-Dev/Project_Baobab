using UnityEngine;

public interface IPathfindTreeProvider
{
    ITreeObj GetTreeAt(int _index);
    ITreeObj GetTreeAt(Vector3Int _cellPos);
}
