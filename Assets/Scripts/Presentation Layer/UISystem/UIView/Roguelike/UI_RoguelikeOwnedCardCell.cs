using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보유 카드 목록의 카드 한 칸. 카드 선택 화면과 같은 카드(UI_RoguelikeCard, 50x81)를 크기 그대로 보여 주고, 마우스·패드 판정(HitBox)을 묶는다.
/// 이름·레벨·효과는 이 칸에 없다 - 오른쪽 설명 패널이 포커스된 칸의 카드를 보여 준다(UIView_RoguelikeOwnedCards).
///
/// [픽셀 규칙 - UI_WorkList 0장] 스케일은 항상 1, 카드는 원본 픽셀 크기 그대로, 위치는 정수 픽셀이다.
///   호버(마우스)와 패드 선택은 같은 포커스(SetFocused)다: 카드가 스스로 위로 올라가고(UI_RoguelikeCard.HoverLiftPixels) 레어 카드는 빛 입자가 인다.
///   마우스를 받는 판정(HitBox)은 올라간 범위까지 덮고 움직이지 않는다 - 카드가 올라가며 판정이 풀렸다 걸렸다 하는 떨림이 없다.
///
/// [시간] 보유 카드 창이 떠 있는 동안 게임은 일시정지(Time.timeScale = 0)다. 카드의 움직임은 unscaled 시간으로 돈다(UI_RoguelikeCard).
/// </summary>
public class UI_RoguelikeOwnedCardCell : MonoBehaviour
{
    public const int CardWidth = 50;
    public const int CardHeight = 81;
    public const int HoverLiftPixels = UI_RoguelikeCard.HoverLiftPixels;

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private UI_RoguelikeCard card;
    [SerializeField] private Button button;
    [SerializeField] private UI_RoguelikeSelectRelay relay;

    //상태 변수
    private RoguelikeOwnedCardViewData data;

    public RoguelikeOwnedCardViewData Data => data;
    public UI_RoguelikeSelectRelay Relay => relay;
    public Button SelectButton => button;

    /// <summary>카드를 담고 켠다. 앞면 · 원래 위치 · 호버 없음 상태로 시작한다.</summary>
    public void Bind(in RoguelikeOwnedCardViewData _data)
    {
        data = _data;

        gameObject.SetActive(true);
        card.gameObject.SetActive(true);

        card.SetFaceDown(false);
        card.SetFlipWidth(card.FrameWidth);
        card.SetShakeOffset(0);
        card.ResetMotion();
        card.SetCard(_data.icon, _data.kind, _data.displayGrade, _data.level, _data.maxLevel, _data.bRuby);
    }

    /// <summary>비운다(칸이 줄었을 때). 오브젝트는 꺼 두고 다음에 다시 쓴다.</summary>
    public void Unbind()
    {
        card.ResetMotion();
        gameObject.SetActive(false);
    }

    /// <summary>왼쪽 위 기준 위치(콘텐츠 좌표, 정수 픽셀).</summary>
    public void SetPosition(Vector2 _position)
    {
        RectTransform rect = (RectTransform)transform;
        rect.anchoredPosition = new Vector2(Mathf.Round(_position.x), Mathf.Round(_position.y));
    }

    /// <summary>열기 · 닫기 연출용 공개 폭(픽셀, 짝수). 0이면 카드를 아예 꺼서(뒤집힘 옆면 선 · 그림자까지) 안 보이게 하고, 카드 폭이면 정상이다.</summary>
    public void SetRevealWidth(int _width)
    {
        bool bVisible = 0 < _width;
        if (card.gameObject.activeSelf != bVisible) card.gameObject.SetActive(bVisible);
        if (true == bVisible) card.SetFlipWidth(_width);
    }

    /// <summary>공개 폭을 카드 폭으로 되돌린다(연출이 끝났을 때).</summary>
    public void RevealFull()
    {
        card.gameObject.SetActive(true);
        card.SetFlipWidth(card.FrameWidth);
    }

    /// <summary>호버·패드 선택 공통. 카드가 올라가고 레어 카드는 빛 입자가 인다.</summary>
    public void SetFocused(bool _bFocused)
    {
        card.SetHovered(_bFocused);
    }
}
