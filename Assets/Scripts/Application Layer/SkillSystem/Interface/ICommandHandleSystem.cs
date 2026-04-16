
public interface ICommandHandleSystem
{
    public IInventoryCH inventoryCH { get; }
    public IContainerCH containerCH { get; }
    public ICutterCH cutterCH { get; }
    public ICharacterStatCH characterStatCH { get; }
}
