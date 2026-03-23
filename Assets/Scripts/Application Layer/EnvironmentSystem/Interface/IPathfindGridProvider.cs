using UnityEngine;

public interface IPathfindGridProvider
{
    /// <summary>
    /// 해당 셀이 다른 유닛에 의해 점유되어 있는지 확인합니다.
    /// </summary>
    bool IsOccupied(Vector3Int _cellPos);

    /// <summary>
    /// 해당 셀을 점유 상태로 설정합니다. 성공 시 true를 반환합니다.
    /// </summary>
    bool Occupy(Vector3Int _cellPos);

    /// <summary>
    /// 해당 셀의 점유를 해제합니다.
    /// </summary>
    void Release(Vector3Int _cellPos);
}
