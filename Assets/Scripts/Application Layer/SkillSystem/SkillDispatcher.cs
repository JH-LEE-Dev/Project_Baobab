using System.Collections.Generic;
using UnityEngine;

public class SkillDispatcher : MonoBehaviour,ICommandHandleSystem
{
    private IInventoryCH inventoryCH;

    [SerializeField] private List<SkillCommand> skillCommands;
    private Dictionary<SkillCommandType, SkillCommand> skillDic;

    IInventoryCH ICommandHandleSystem.inventoryCH => inventoryCH;

    public void Initialize(IInventoryCH _inventoryCH)
    {
        inventoryCH = _inventoryCH;

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

    public void DispatchCommand(SkillDispatchInfo _skillDispatchInfo)
    {
        SkillCommandType commandType = _skillDispatchInfo.commandInfo.info.skillCommandType;

        if (skillDic.TryGetValue(commandType, out SkillCommand command))
        {
            command.level = _skillDispatchInfo.level;
            command.amount = _skillDispatchInfo.commandInfo.info.amount;
            command.Execute(this);
        }
        else
        {
            Debug.LogWarning($"[SkillDispatcher] SkillCommand not found for type: {commandType}");
        }
    }
}
