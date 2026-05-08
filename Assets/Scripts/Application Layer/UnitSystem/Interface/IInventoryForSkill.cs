
public interface IInventoryForSkill 
{
    public int GetCurrentCarrot();
    public long GetCurrentMoney();
    public void DecreaseCarrot(int _amount);
    public void DecreaseMoney(int _amount);
}
