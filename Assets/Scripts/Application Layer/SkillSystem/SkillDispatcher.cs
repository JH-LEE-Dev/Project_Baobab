using System.Collections.Generic;
using UnityEngine;

public class SkillDispatcher : MonoBehaviour
{
    private IInventoryCH inventoryCH;

    [SerializeField] private List<SkillCommand> skillCommands;
    private Dictionary<SkillCommandType, SkillCommand> skillDic;

    public void Initialize()
    {
        if (skillCommands == null) return;

        skillDic = new Dictionary<SkillCommandType, SkillCommand>(skillCommands.Count);

        for (int i = 0; i < skillCommands.Count; i++)
        {
            SkillCommand command = skillCommands[i];
            if (command == null) continue;

            if (!skillDic.ContainsKey(command.skillCommandType))
            {
                skillDic.Add(command.skillCommandType, command);
            }
            else
            {
                Debug.LogWarning($"[SkillDispatcher] Duplicate SkillCommandType found: {command.skillCommandType}");
            }
        }
    }
}
