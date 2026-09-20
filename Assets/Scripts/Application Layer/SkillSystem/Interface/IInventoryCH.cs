
public interface IInventoryCH
{
   public void ExpandInventorySlotCnt(float _amount);
   public void LogCapacityIncrease(float _amount);

   /// <summary>
   /// 원석 주머니 한도를 늘린다. 아직 이걸 부르는 특성(SkillCommand)은 없고, 나중에 붙일 자리다.
   /// 음수를 주면 줄어든다(특성 Undo 경로).
   /// </summary>
   public void IncreaseGemOrePouchCapacity(float _amount);
}
