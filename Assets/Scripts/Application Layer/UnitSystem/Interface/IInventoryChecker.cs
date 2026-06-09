
public interface IInventoryChecker 
{
    public bool bInventoryIsEmpty { get; }
    public bool CanAcquired(LogItem _item);
}
