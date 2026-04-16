using System.Collections.Generic;
using UnityEngine;

public class SkillDispatcher : MonoBehaviour, ICommandHandleSystem
{
    private SignalHub signalHub;
    private IInventoryCH inventoryCH;
    private IContainerCH containerCH;
    private ICutterCH cutterCH;
    private ICharacterStatCH characterStatCH;


    [SerializeField] private List<SkillCommand> skillCommands;
    private Dictionary<SkillCommandType, SkillCommand> skillDic;

    IInventoryCH ICommandHandleSystem.inventoryCH => inventoryCH;

    IContainerCH ICommandHandleSystem.containerCH => containerCH;

    ICutterCH ICommandHandleSystem.cutterCH => cutterCH;

    ICharacterStatCH ICommandHandleSystem.characterStatCH => characterStatCH;

    public void Initialize(SignalHub _signalHub, IInventoryCH _inventoryCH, IContainerCH _containerCH, ICutterCH _cutterCH)
    {
        signalHub = _signalHub;
        inventoryCH = _inventoryCH;
        containerCH = _containerCH;
        cutterCH = _cutterCH;

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

        SubscribeSignals();
    }

    public void Release()
    {
        UnSubscribeSignals();
    }

    private void SubscribeSignals()
    {
        signalHub.Subscribe<CharacterSpawendSignal>(CharacterSpawned);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<CharacterSpawendSignal>(CharacterSpawned);
    }

    public void DispatchCommand(SkillDispatchInfo _skillDispatchInfo)
    {
        SkillCommandType commandType = _skillDispatchInfo.commandInfo.skillCommandType;

        if (skillDic.TryGetValue(commandType, out SkillCommand command))
        {
            command.level = _skillDispatchInfo.level;
            command.amount = _skillDispatchInfo.commandInfo.amount;
            command.Execute(this);
        }
        else
        {
            Debug.LogWarning($"[SkillDispatcher] SkillCommand not found for type: {commandType}");
        }
    }

    private void CharacterSpawned(CharacterSpawendSignal characterSpawendSignal)
    {
        characterStatCH = characterSpawendSignal.character.statComponent;
    }
}
