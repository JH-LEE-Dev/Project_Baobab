
public interface IInventoryCH
{
   public void ExpandInventorySlotCnt(float _amount);
   public void LogCapacityIncrease(float _amount);

   /// <summary>
   /// 원석 주머니 한도를 늘린다("원석 주머니 확장" 특성 - SC_GemOrePouchExpansion).
   /// 음수를 주면 줄어든다(특성 Undo 경로).
   /// </summary>
   public void IncreaseGemOrePouchCapacity(float _amount);
}
