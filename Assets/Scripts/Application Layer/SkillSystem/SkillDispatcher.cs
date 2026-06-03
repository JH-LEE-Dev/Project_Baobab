using System.Collections.Generic;
using UnityEngine;
using System;

public class SkillDispatcher : MonoBehaviour, ICommandHandleSystem
{
    public event Action<SkillAccumulatedValueData> DeclareAccumulatedValueEvent;
    private SignalHub signalHub;
    private IInventoryCH inventoryCH;
    private IContainerCH containerCH;
    private ICutterCH cutterCH;
    private ICharacterStatCH characterStatCH;
    private ILogEvaluatorCH logEvaluatorCH;
    private IDensityCH densityCH;
    private ICarrotItemCH carrotItemCH;
    private ITownObjSystemCH townObjSystemCH;
    private ILogProcessingSystemCH logProcessingSystemCH;
    private ILogItemCH logItemCH;
    private IOffroadContainerCH offroadContainerCH;


    [SerializeField] private List<SkillCommand> skillCommands;
    private Dictionary<SkillCommandType, SkillCommand> skillDic;
    private Dictionary<SkillCommandType, float> accumulatedAmounts;

    IInventoryCH ICommandHandleSystem.inventoryCH => inventoryCH;

    IContainerCH ICommandHandleSystem.containerCH => containerCH;

    ICutterCH ICommandHandleSystem.cutterCH => cutterCH;

    ICharacterStatCH ICommandHandleSystem.characterStatCH => characterStatCH;

    ILogEvaluatorCH ICommandHandleSystem.logEvaluatorCH => logEvaluatorCH;

    IDensityCH ICommandHandleSystem.densityCH => densityCH;

    ICarrotItemCH ICommandHandleSystem.carrotItemCH => carrotItemCH;
    
    ITownObjSystemCH ICommandHandleSystem.townObjSystemCH => townObjSystemCH;

    ILogProcessingSystemCH ICommandHandleSystem.logProcessingSystemCH => logProcessingSystemCH;

    ILogItemCH ICommandHandleSystem.logItemCH => logItemCH;

    IOffroadContainerCH ICommandHandleSystem.offroadContainerCH => offroadContainerCH;

    public void Initialize(SignalHub _signalHub, IInventoryCH _inventoryCH, IContainerCH _containerCH, ICutterCH _cutterCH,
    ILogEvaluatorCH _logEvaluatorCH, IDensityCH _densityCH,ICarrotItemCH _carrotItemCH, ITownObjSystemCH _townObjSystemCH,
    ILogProcessingSystemCH _logProcessingSystemCH, ILogItemCH _logItemCH, IOffroadContainerCH _offroadContainerCH)
    {
        offroadContainerCH = _offroadContainerCH;
        signalHub = _signalHub;
        inventoryCH = _inventoryCH;
        containerCH = _containerCH;
        cutterCH = _cutterCH;
        logEvaluatorCH = _logEvaluatorCH;
        densityCH = _densityCH;
        carrotItemCH = _carrotItemCH;
        townObjSystemCH = _townObjSystemCH;
        logProcessingSystemCH = _logProcessingSystemCH;
        logItemCH = _logItemCH;
        
        if (skillCommands == null) return;

        skillDic = new Dictionary<SkillCommandType, SkillCommand>(skillCommands.Count);
        accumulatedAmounts = new Dictionary<SkillCommandType, float>(skillCommands.Count);

        for (int i = 0; i < skillCommands.Count; i++)
        {
            SkillCommand command = skillCommands[i];
            if (command == null) continue;

            if (!skillDic.ContainsKey(command.skillCommandType))
            {
                skillDic.Add(command.skillCommandType, command);
                accumulatedAmounts.Add(command.skillCommandType, 0f);
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
        signalHub.Subscribe<CharacterSpawnedSignal>(CharacterSpawned);
    }

    private void UnSubscribeSignals()
    {
        signalHub.UnSubscribe<CharacterSpawnedSignal>(CharacterSpawned);
    }

    public void DispatchCommand(SkillDispatchInfo _skillDispatchInfo)
    {
        SkillCommandType commandType = _skillDispatchInfo.commandInfo.skillCommandType;

        if (skillDic.TryGetValue(commandType, out SkillCommand command))
        {
            command.level = _skillDispatchInfo.level;
            // 커브 공식을 사용하여 레벨에 따른 최종 수치 계산
            float currentAmount = _skillDispatchInfo.commandInfo.amountCurve.Evaluate(_skillDispatchInfo.level);
            command.amount = currentAmount;

            if (accumulatedAmounts.ContainsKey(commandType))
            {
                accumulatedAmounts[commandType] += currentAmount;
            }
            else
            {
                accumulatedAmounts[commandType] = currentAmount;
            }

            command.Execute(this);
            DeclareAccumulatedValueEvent?.Invoke(new SkillAccumulatedValueData { type = commandType, amount = currentAmount });
        }
        else
        {
            Debug.LogWarning($"[SkillDispatcher] SkillCommand not found for type: {commandType}");
        }
    }

    public float GetAccumulatedAmount(SkillCommandType _commandType)
    {
        if (accumulatedAmounts != null && accumulatedAmounts.TryGetValue(_commandType, out float amount))
        {
            return amount;
        }
        return 0f;
    }

    private void CharacterSpawned(CharacterSpawnedSignal characterSpawendSignal)
    {
        characterStatCH = characterSpawendSignal.character.statComponent;
    }
}
