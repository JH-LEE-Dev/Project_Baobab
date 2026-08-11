/// <summary>
/// ESC로 닫을 수 있는 UI 뎁스 스택(UIDepthController)에 참여하기 위한 인터페이스입니다.
/// UIView뿐 아니라 UI_Option, UI_WarningPopup처럼 UIView를 상속하지 않는 컴포넌트도
/// 이 인터페이스만 구현하면 같은 뎁스 스택에서 ESC로 순서대로 닫힐 수 있습니다.
/// </summary>
public interface IUIDepthCloseable
{
    bool IsActive { get; }
    void Hide();
}
