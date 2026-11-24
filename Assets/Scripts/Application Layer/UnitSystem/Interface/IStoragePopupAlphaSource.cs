/// <summary>
/// 보관함 월드 팝업(UI_Storage)의 투명도를 위치에 따라 정해 주는 보관함. 운반 상자만 구현한다 -
/// 휴식 범위가 열린 던전에서 캐릭터가 휴식 범위 바깥쪽으로 갈수록 슬롯 UI를 흐리게 해 뒤쪽 나무를 가리지 않게 한다.
/// </summary>
public interface IStoragePopupAlphaSource
{
    /// <summary>지금 팝업에 곱할 투명도(0~1). 흐리게 할 상황이 아니면 1.</summary>
    float PopupAlpha { get; }
}
