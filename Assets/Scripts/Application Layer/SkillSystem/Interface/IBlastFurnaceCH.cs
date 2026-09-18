/// <summary>
/// 용광로 관련 특성이 건드리는 창구.
///   - IncreaseFurnaceCount : "용광로" 특성. 용광로를 한 대씩 추가한다(황금 -> 다이아 -> 프리즘 순).
///   - IncreaseProcessingSpeed : "용광로 가속" 특성. 가공 속도를 N% 올린다.
/// </summary>
public interface IBlastFurnaceCH
{
    public void IncreaseFurnaceCount(float _amount);
    public void IncreaseProcessingSpeed(float _percent);
}
