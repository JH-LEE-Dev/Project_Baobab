
public interface IInventoryForSkill 
{
    public long GetCurrentCarrot();
    public long GetCurrentMoney();
    public void DecreaseCarrot(long _amount);
    public void DecreaseMoney(long _amount);
}
