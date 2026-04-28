
public interface ICommandHandleSystem
{
    public IInventoryCH inventoryCH { get; }
    public IContainerCH containerCH { get; }
    public ICutterCH cutterCH { get; }
    public ICharacterStatCH characterStatCH { get; }
    public ILogEvaluatorCH logEvaluatorCH { get; }
    public IDensityCH densityCH { get; }
    public ICarrotItemCH carrotItemCH { get; }
    public ITownObjSystemCH townObjSystemCH { get; }
    public ILogProcessingSystemCH logProcessingSystemCH { get; }
}
