using System.Collections.Generic;
using UnityEngine;
using System;

public class SkillDispatcher : MonoBehaviour, ICommandHandleSystem
{
    public event Action<SkillAccumulatedValueData> DeclareAccumulatedValueEvent;
    public event Action<SkillAccumulatedValueChangeData> ProvideAccumulatedValueChangeEvent;
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
    private ILogItemControllerCH logItemControllerCH;
    private IOffroadContainerCH offroadContainerCH;
    private IInDungeonObjManagerCH inDungeonObjManagerCH;
    private IInDungeonUnitSpawnerCH inDungeonUnitSpawnerCH;


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

    ILogItemControllerCH ICommandHandleSystem.logItemControllerCH => logItemControllerCH;

    IOffroadContainerCH ICommandHandleSystem.offroadContainerCH => offroadContainerCH;

    IInDungeonObjManagerCH ICommandHandleSystem.inDungeonObjManagerCH => inDungeonObjManagerCH;

    IInDungeonUnitSpawnerCH ICommandHandleSystem.inDungeonUnitSpawnerCH => inDungeonUnitSpawnerCH;

    public void Initialize(SignalHub _signalHub, IInventoryCH _inventoryCH, IContainerCH _containerCH, ICutterCH _cutterCH,
    ILogEvaluatorCH _logEvaluatorCH, IDensityCH _densityCH,ICarrotItemCH _carrotItemCH, ITownObjSystemCH _townObjSystemCH,
    ILogProcessingSystemCH _logProcessingSystemCH, ILogItemControllerCH _logItemCH, IOffroadContainerCH _offroadContainerCH,
    IInDungeonObjManagerCH _inDungeonObjManagerCH, IInDungeonUnitSpawnerCH _inDungeonUnitSpawnerCH)
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
        logItemControllerCH = _logItemCH;
        inDungeonObjManagerCH = _inDungeonObjManagerCH;
        inDungeonUnitSpawnerCH = _inDungeonUnitSpawnerCH;
        
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
            DeclareAccumulatedValueEvent?.Invoke(new SkillAccumulatedValueData { type = commandType, amount = accumulatedAmounts[commandType] });
        }
        else
        {
            Debug.LogWarning($"[SkillDispatcher] SkillCommand not found for type: {commandType}");
        }
    }

    public void DispatchCommandWithChange(SkillDispatchInfo _skillDispatchInfo)
    {
        SkillCommandType commandType = _skillDispatchInfo.commandInfo.skillCommandType;

        if (skillDic.TryGetValue(commandType, out SkillCommand command))
        {
            // 커브 공식을 사용하여 레벨에 따른 최종 수치 계산 (Y)
            float currentAmount = _skillDispatchInfo.commandInfo.amountCurve.Evaluate(_skillDispatchInfo.level);

            // 기존 스킬 누적값 (X)
            float previousAccumulated = 0f;
            if (accumulatedAmounts.TryGetValue(commandType, out float val))
            {
                previousAccumulated = val;
            }

            // 총 누적값 (Z)
            float totalAccumulated = previousAccumulated + currentAmount;

            SkillAccumulatedValueChangeData changeData = new SkillAccumulatedValueChangeData
            {
                type = commandType,
                currentValueX = previousAccumulated,
                addedValueY = currentAmount,
                totalValueZ = totalAccumulated
            };

            ProvideAccumulatedValueChangeEvent?.Invoke(changeData);
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
