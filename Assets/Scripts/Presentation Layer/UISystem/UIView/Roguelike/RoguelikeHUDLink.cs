/// <summary>
/// 로그라이크 전투 HUD의 스태미너 바 · 대쉬 칸을 본편 HUD(UIView_HUD)가 찾는 정적 연결점.
/// UIView_RoguelikeHUD가 Initialize에서 넣고 OnDestroy에서(자기 것일 때만) 비운다.
/// UIView_HUD는 로그라이크 런 상태(SetEquipmentHiddenForRun)일 때 이 대상에도 같은 값을 전달한다.
/// 정식 연결(코디네이터가 직접 넘기는 방식)은 기획서 v3.2 C-2 "HUD 정식 연결 메서드"로 남아 있다(지금은 이 정적 연결점이 임시 연결).
/// OwnedTray는 카드 선택 화면(UIView_RoguelikeCardSelect)이 보관 연출이 끝나는 순간 트레이 도착 반응을 걸 때 찾는다. 없으면(null) 트레이 반응은 건너뛴다.
/// </summary>
public static class RoguelikeHUDLink
{
    public static UI_RoguelikeStaminaBar StaminaBar;
    public static UI_RoguelikeDashCells DashCells;
    public static UI_RoguelikeOwnedCardTray OwnedTray;
}
