using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SkillDataBase", menuName = "Game/Skill Database")]
public class SkillDataBase : ScriptableObject
{
    public List<Skill> skills;
}
