/// <summary>
/// 세이브 파일 확인 화면이 시스템과 주고받는 통로입니다.
///
/// UIView가 SaveManager를 통째로 알면 UI 작업 중에 세이브 시스템을 건드릴 수 있게 되므로,
/// IMainMenuSaveSystem과 같은 방식으로 필요한 것만 노출합니다.
/// 실제 연결은 SaveCheckCoordinator가 하며, UIView는 이 인터페이스조차 직접 보지 않습니다.
/// </summary>
public interface ISaveCheckSystem
{
    /// <summary>확인 진행 상태입니다.</summary>
    ESaveCheckState SaveCheckState { get; }

    /// <summary>확인을 시작한 뒤 흐른 시간입니다. "잠깐이면 화면을 띄우지 않는다" 판단에 씁니다.</summary>
    float SaveCheckElapsedSeconds { get; }

    /// <summary>다시 한 번 읽어봅니다. 그새 잠금이 풀렸으면 그대로 복구됩니다.</summary>
    void RetrySaveAvailabilityCheck();

    /// <summary>
    /// 기존 세이브를 포기하고 새로 시작합니다. 되돌릴 수 없으므로 반드시 확인 팝업을 거친 뒤에만 부릅니다.
    /// </summary>
    void AbandonUnreadableSaveAndStartFresh();
}
