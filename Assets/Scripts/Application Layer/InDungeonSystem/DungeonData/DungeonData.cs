using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "DungeonData", menuName = "Dungeon/DungeonData")]
public class DungeonData : ScriptableObject
{
    public DungeonType dungeonType;
    public List<TreeType> treeTypes;
    public List<TreeGradeProb> treeGradeProbs;
    public float staminaDecAmount;
    public float staminaIncAmount;
}
