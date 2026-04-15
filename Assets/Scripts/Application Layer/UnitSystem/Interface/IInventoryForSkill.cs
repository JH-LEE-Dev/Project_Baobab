
public interface IInventoryForSkill 
{
    public int GetCurrentCarrot();
    public int GetCurrentMoney();
    public void DecreaseCarrot(int _amount);
    public void DecreaseMoney(int _amount);
}
