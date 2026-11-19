using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 기록 창 최근 기록 탭의 카드 보관함: 8칸 x 4줄 = 32칸(한 판에 가질 수 있는 최대 장수). 받침(Panel_Info_Tile) 위에 빈 홈(Panel_Info_Empty)이 늘 깔려 있고
/// 카드가 있는 칸만 UI_RoguelikeCardIconSlot(선택 대상)이 위에 놓인다. 칸 36 + 간격 4(칸 자리는 빌더가 정한다), 정수 픽셀.
///   등장: 받침이 0.16초 페이드, 칸은 0.02초 간격으로 4px 위에서 OutBack으로 떨어지며(위치만, 크기 변화 없음) 나타난다.
///   패드: 카드가 있는 칸끼리 상하좌우 Explicit. 고르면 SlotSelectedEvent를 올린다(커서는 화면이 그린다).
/// </summary>
public class UI_RoguelikeRecordCardGrid : MonoBehaviour
{
    public const int Columns = 8;
    public const int RowCount = 4;
    public const int Capacity = Columns * RowCount;
    private const float TrayDelay = 0.36f;
    private const float TraySeconds = 0.16f;
    private const float SlotDelay = 0.45f;
    private const float SlotStagger = 0.02f;
    private const float SlotSeconds = 0.14f;
    private const float SlotDrop = 4f;

    public event Action<UI_RoguelikeCardIconSlot> SlotSelectedEvent;

    //외부 의존성 (프리팹에서 연결)
    [SerializeField, Tooltip("카드 칸 32개(선택 대상, 각자 CanvasGroup). 칸 원래 위치는 빌더가 정수 픽셀로 놓는다.")] private UI_RoguelikeCardIconSlot[] slots = new UI_RoguelikeCardIconSlot[Capacity];
    [SerializeField, Tooltip("받침 + 빈 홈 묶음.")] private CanvasGroup trayGroup;

    //내부 의존성
    private readonly CanvasGroup[] slotGroups = new CanvasGroup[Capacity];
    private readonly Vector2[] slotBases = new Vector2[Capacity];
    private Action<UI_RoguelikeCardIconSlot> slotSelectedHandler;

    //상태 변수
    private int count;
    private int poppedCount;
    private bool bInitialized;

    public int Count => count;

    public void Initialize()
    {
        if (true == bInitialized) return;
        bInitialized = true;

        slotSelectedHandler = OnSlotSelected;
        for (int i = 0; Capacity > i; i++)
        {
            UI_RoguelikeCardIconSlot slot = slots[i];
            if (null == slot) continue;

            slotGroups[i] = slot.GetComponent<CanvasGroup>();
            slotBases[i] = slot.Rect.anchoredPosition;
            slot.SelectedEvent -= slotSelectedHandler;
            slot.SelectedEvent += slotSelectedHandler;
        }
    }

    /// <summary>종류 순으로 정렬된 카드를 앞에서부터 채우고 채운 장수를 돌려준다(32장을 넘는 몫은 버린다 - 규칙상 없다).</summary>
    public int SetCards(List<RoguelikeOwnedCardViewData> _sortedCards)
    {
        Initialize();
        count = null != _sortedCards ? Mathf.Min(_sortedCards.Count, Capacity) : 0;
        poppedCount = 0;

        for (int i = 0; Capacity > i; i++)
        {
            UI_RoguelikeCardIconSlot slot = slots[i];
            if (null == slot) continue;

            bool bUsed = i < count;
            if (slot.gameObject.activeSelf != bUsed) slot.gameObject.SetActive(bUsed);
            if (false == bUsed) continue;

            RoguelikeOwnedCardViewData data = _sortedCards[i];
            slot.SetCard(in data);
            slot.Rect.anchoredPosition = slotBases[i];
        }

        for (int i = 0; count > i; i++) LinkNavigation(i);
        return count;
    }

    public UI_RoguelikeCardIconSlot GetSlot(int _index)
    {
        return 0 <= _index && count > _index ? slots[_index] : null;
    }

    public int IndexOf(UI_RoguelikeCardIconSlot _slot)
    {
        for (int i = 0; count > i; i++)
        {
            if (slots[i] == _slot) return i;
        }
        return -1;
    }

    /// <summary>등장 연출 한 프레임(_localTime = 스포트라이트 등장 시작부터 지난 시간, unscaled). 4칸마다 한 번 팝 소리를 낸다.</summary>
    public void RenderIntro(float _localTime)
    {
        if (null != trayGroup) trayGroup.alpha = Mathf.Clamp01((_localTime - TrayDelay) / TraySeconds);

        for (int i = 0; count > i; i++)
        {
            float p = Mathf.Clamp01((_localTime - SlotDelay - i * SlotStagger) / SlotSeconds);
            if (null != slotGroups[i]) slotGroups[i].alpha = Mathf.Clamp01(p * 2f);
            slots[i].Rect.anchoredPosition = new Vector2(slotBases[i].x, slotBases[i].y + Mathf.Round((1f - OutBack(p)) * SlotDrop));

            if (0f < p && i >= poppedCount)
            {
                poppedCount = i + 1;
                if (0 == i % 4) Sound.PlayUI(SoundID.FontPop);
            }
        }
    }

    /// <summary>등장 연출 없이 끝 상태로 둔다.</summary>
    public void SnapIntro()
    {
        if (null != trayGroup) trayGroup.alpha = 1f;
        for (int i = 0; count > i; i++)
        {
            if (null != slotGroups[i]) slotGroups[i].alpha = 1f;
            slots[i].Rect.anchoredPosition = slotBases[i];
        }
        poppedCount = count;
    }

    private void LinkNavigation(int _index)
    {
        Selectable target = slots[_index].SelectTarget;
        if (null == target) return;

        int column = _index % Columns;
        Navigation navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = 0 < column ? SelectableAt(_index - 1) : null,
            selectOnRight = Columns - 1 > column ? SelectableAt(_index + 1) : null,
            selectOnUp = SelectableAt(_index - Columns),
            selectOnDown = SelectableAt(_index + Columns),
        };
        target.navigation = navigation;
    }

    private Selectable SelectableAt(int _index)
    {
        if (0 > _index || count <= _index || null == slots[_index]) return null;
        return slots[_index].SelectTarget;
    }

    private void OnSlotSelected(UI_RoguelikeCardIconSlot _slot)
    {
        SlotSelectedEvent?.Invoke(_slot);
    }

    private static float OutBack(float _t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float t = _t - 1f;
        return 1f + c3 * t * t * t + c1 * t * t;
    }

    private void OnDestroy()
    {
        if (null != slotSelectedHandler)
        {
            for (int i = 0; Capacity > i; i++)
            {
                if (null != slots[i]) slots[i].SelectedEvent -= slotSelectedHandler;
            }
        }
        SlotSelectedEvent = null;
    }
}
