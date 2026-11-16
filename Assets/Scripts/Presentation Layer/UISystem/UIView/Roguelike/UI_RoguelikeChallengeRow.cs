using System;
using TMPro;
using PresentationLayer.VFX;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 도전과제 창의 한 칸: Panel_Info 9-슬라이스 바탕 위에 제목 1줄 + 아래 팁 최대 2줄.
/// 달성한 칸은 금색 테두리(goldSprite, Panel_Info_Gold) + 제목 금색 + 오른쪽 금색 깃발, 미달성 칸은 기본 바탕이다.
///
/// 칸 전체가 패드 선택용 대상(Button, 탐색은 Explicit - 화면이 위아래로 잇는다)이다. 눌러도 하는 일은 없다.
/// 마우스 호버와 패드 선택은 FocusedEvent(칸, 마우스인지)로 알리고, 마우스가 나가면 ExitedEvent로 알린다(UI_RoguelikeRecordRow와 같은 방식).
/// </summary>
public class UI_RoguelikeChallengeRow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler
{
    public event Action<UI_RoguelikeChallengeRow, bool> FocusedEvent;
    public event Action<UI_RoguelikeChallengeRow> ExitedEvent;

    //외부 의존성 (프리팹에서 연결 - LumberBoy/UI/Build Roguelike Challenges)
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private Button button;
    [SerializeField, Tooltip("칸 바탕 Image(Panel_Info 9-슬라이스).")] private Image background;
    [SerializeField, Tooltip("달성한 칸의 바탕 그림(Panel_Info_Gold).")] private Sprite goldSprite;
    [SerializeField, Tooltip("달성 깃발. 달성한 칸에서만 켠다.")] private GameObject clearedMark;
    [SerializeField, Tooltip("달성한 칸의 제목 색.")] private Color clearedTitleColor = Color.white;

    [SerializeField, Tooltip("달성 칸 파티클 연출(칸 안쪽 + 깃발 통통). 달성한 칸에서만 돈다.")] private UI_ChallengeClearGlow clearGlow;

    //상태 변수
    private Sprite normalSprite;
    private Color normalTitleColor = Color.white;
    private bool bNormalTitleColorCached;
    private bool bCleared;

    public RectTransform Rect => (RectTransform)transform;
    public Button SelectButton => button;
    /// <summary>마지막 SetChallenge의 달성 여부.</summary>
    public bool IsCleared => bCleared;

    /// <summary>기본 바탕 그림을 기억한다. 칸을 만든 직후 한 번 부른다.</summary>
    public void Initialize()
    {
        CacheNormalState();
    }

    public void SetChallenge(in RoguelikeChallengeViewData _challenge)
    {
        CacheNormalState();

        if (null != titleText)
        {
            titleText.text = null != _challenge.title ? _challenge.title : string.Empty;
            titleText.color = true == _challenge.bCleared ? clearedTitleColor : normalTitleColor;
        }
        if (null != tipText) tipText.text = null != _challenge.tip ? _challenge.tip : string.Empty;

        if (null != clearedMark) clearedMark.SetActive(true == _challenge.bCleared);

        if (null != background)
        {
            Sprite target = true == _challenge.bCleared && null != goldSprite ? goldSprite : normalSprite;
            if (background.sprite != target) background.sprite = target;
        }

        bCleared = true == _challenge.bCleared;
        if (false == bCleared) StopClearGlow();
    }

    public void PlayClearGlow(float _delay, int _order, bool _bEntrance)
    {
        if (null != clearGlow) clearGlow.Play(_delay, _order, _bEntrance);
    }

    public void StopClearGlow()
    {
        if (null != clearGlow) clearGlow.Stop();
    }

    public void TickClearGlow(float _deltaTime)
    {
        if (null != clearGlow) clearGlow.Tick(_deltaTime);
    }

    // 기본 바탕 그림과 미달성 제목 색을 아직 못 잡았으면 잡는다.
    private void CacheNormalState()
    {
        if (null != background && null == normalSprite) normalSprite = background.sprite;
        if (false == bNormalTitleColorCached && null != titleText)
        {
            normalTitleColor = titleText.color;
            bNormalTitleColorCached = true;
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        FocusedEvent?.Invoke(this, true);
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        ExitedEvent?.Invoke(this);
    }

    public void OnSelect(BaseEventData _eventData)
    {
        FocusedEvent?.Invoke(this, false);
    }

    private void OnDestroy()
    {
        FocusedEvent = null;
        ExitedEvent = null;
    }
}
