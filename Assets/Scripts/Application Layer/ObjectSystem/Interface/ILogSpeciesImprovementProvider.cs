/// <summary>
/// "수종 개량" 특성이 원목의 수종을 지역 최고 수종으로 바꿔주는 통로.
///
/// LogItem은 자기가 어느 지역에 떨어졌는지도, 특성이 켜져 있는지도 모른다. 판단과 데이터는 전부
/// 소유자(LogItemController)에게 맡기고, LogItem은 "지금 습득을 시도한다"는 시점만 알려준다.
/// 공급자가 없는 경로(예: 마을 LogItemPoolingManager)에서는 아무 일도 일어나지 않는다.
/// </summary>
public interface ILogSpeciesImprovementProvider
{
    /// <summary>
    /// 흡입(습득)을 시도하기 직전에 호출된다. 특성이 켜져 있고 바꿀 수종이 있으면 그 자리에서 바꾼다.
    /// <b>반드시 인벤토리 공간 검사(IInventoryChecker.CanAcquired)보다 먼저 불려야 한다.</b>
    /// 검사와 자리 예약이 수종을 보고 이뤄지므로, 순서가 뒤집히면 검사한 수종과 실제로 담기는
    /// 수종이 어긋나 예약이 샌다.
    ///
    /// 바꿔놓고 결국 담지 못했을 때의 되돌리기는 LogItem이 알아서 한다 - 원래 수종 데이터는
    /// Initialize가 이미 인자로 받아 들고 있으므로 여기로 되물을 필요가 없다.
    /// </summary>
    void ApplySpeciesImprovement(LogItem _logItem);

    /// <summary>
    /// 수종 개량이 원목을 끌어올리는 "바닥 수종". 특성이 꺼져 있거나 올릴 곳이 없으면 TreeType.None.
    /// 아무것도 바꾸지 않고 물어보기만 한다.
    ///
    /// 지역마다 하나로 정해지는 값이라 어느 원목에게 물어도 답이 같다. 흡입 순서를 정하는 쪽
    /// (ItemDetector.SortByPickupPriority)이 "바닥에 보이는 수종"이 아니라 "실제로 담길 수종"으로
    /// 줄을 세우기 위해 쓴다 - ApplySpeciesImprovement와 같은 규칙을 봐야 하므로 판정은 이 메서드
    /// 하나에 모아두고 양쪽이 함께 쓴다.
    /// </summary>
    TreeType GetImprovementFloor();
}
