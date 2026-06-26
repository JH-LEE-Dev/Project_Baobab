using UnityEngine;

public class InDungeonStateManager : MonoBehaviour
{

    public void Initialize()
    {
        
    }

    public DungeonState CalcDungeonState(MapType _mapType)
    {
        switch (_mapType)
        {
            case MapType.WideGreenForest:
                return (DungeonState)UnityEngine.Random.Range((int)DungeonState.Stage1_Idle0, (int)DungeonState.Stage1_Idle3 + 1);
            case MapType.FluffySporeForest:
                return (DungeonState)UnityEngine.Random.Range((int)DungeonState.Stage2_Idle0, (int)DungeonState.Stage2_Idle2 + 1);
            case MapType.StarrootForest:
                return (DungeonState)UnityEngine.Random.Range((int)DungeonState.Stage3_Idle0, (int)DungeonState.Stage3_Idle2 + 1);
            case MapType.MagmaForest:
                return (DungeonState)UnityEngine.Random.Range((int)DungeonState.Stage4_Idle0, (int)DungeonState.Stage4_Idle2 + 1);
            default:
                return DungeonState.None;
        }
    } 
}
