using UnityEngine;

/// <summary>
/// 전투 HUD 우하단의 보관함 트레이. 기획서 10-2 "보유 카드 보관함 - 기본 상태(접힘)": 등급색 미니 카드 아이콘 + 숫자가 든 숫자판 둘(레어 · 일반 장수).
/// 표시 전용이다 - 마우스를 받지 않고(레이캐스트 그래픽 없음) 누르거나 올려도 반응하지 않는다. 보유 카드 창은 키(인벤토리 키)로만 연다.
///
/// [숫자] 레어 · 일반 장수는 SetCounts로 받는다. 값을 받기 전(HUD가 켜질 때, ResetTray)에는 0 · 0이다. 카드가 0장이어도 0으로 표기한다.
///   숫자는 버프 HUD와 같은 작은 숫자 글리프(UI_RoguelikeIconNumber)다.
/// </summary>
public class UI_RoguelikeOwnedCardTray : MonoBehaviour
{
    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private UI_RoguelikeIconNumber rareNumber;
    [SerializeField] private UI_RoguelikeIconNumber normalNumber;

    /// <summary>레어(케이스) · 일반(슬리브) 장수. 값이 바뀔 때만 부르면 된다.</summary>
    public void SetCounts(int _rareCount, int _normalCount)
    {
        if (null != rareNumber) rareNumber.SetSingle(_rareCount);
        if (null != normalNumber) normalNumber.SetSingle(_normalCount);
    }

    /// <summary>런 시작·종료 초기화. 0 · 0으로 되돌린다.</summary>
    public void ResetTray()
    {
        SetCounts(0, 0);
    }

    public void SetVisible(bool _bVisible)
    {
        gameObject.SetActive(_bVisible);
    }
}
