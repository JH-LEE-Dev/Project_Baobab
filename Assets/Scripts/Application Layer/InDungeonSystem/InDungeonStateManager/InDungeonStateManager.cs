using UnityEngine;

public class InDungeonStateManager : MonoBehaviour
{

    public void Initialize()
    {
        
    }

    public DungeonState CalcDungeonState(MapType _mapType)
    {
        if (_mapType == MapType.VegetatedForest)
        {
            return (DungeonState)UnityEngine.Random.Range((int)DungeonState.Stage1_Idle0, (int)DungeonState.Stage1_Idle3 + 1);
        }

        return DungeonState.None;
    } 
}
