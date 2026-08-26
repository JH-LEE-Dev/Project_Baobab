using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 옵션 창 상단의 탭 목록과 각 패널의 활성/비활성을 관리하는 그룹 컨트롤러입니다.
/// </summary>
public class UI_OptionTabGroup : MonoBehaviour
{
    [Serializable]
    public struct OptionTabPair
    {
        public UI_OptionTabButton tabButton;
        public GameObject tabPanel;
    }

    // 외부 컴포넌트 참조
    [Header("Tabs Configuration")]
    [SerializeField] private OptionTabPair[] tabs;

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(string[] _tabTexts = null)
    {
        if (null == tabs) return;

        for (int i = 0; tabs.Length > i; i++)
        {
            if (null != tabs[i].tabButton)
            {
                tabs[i].tabButton.Initialize(this, i);
                
                if (null != _tabTexts && _tabTexts.Length > i)
                {
                    tabs[i].tabButton.SetText(_tabTexts[i]);
                }

                Navigation _nav = new Navigation();
                _nav.mode = Navigation.Mode.Explicit;
                _nav.selectOnLeft = tabs[(i - 1 + tabs.Length) % tabs.Length].tabButton;
                _nav.selectOnRight = tabs[(i + 1) % tabs.Length].tabButton;
                tabs[i].tabButton.navigation = _nav;
            }
        }

        // 기본적으로 첫 번째 탭 활성화
        if (0 < tabs.Length)
        {
            SelectTab(0, false);
        }
    }

    public void RefreshTabTexts(string[] _tabTexts)
    {
        if (null == tabs || null == _tabTexts) return;

        for (int i = 0; tabs.Length > i; i++)
        {
            if (null != tabs[i].tabButton && _tabTexts.Length > i)
            {
                tabs[i].tabButton.SetText(_tabTexts[i]);
            }
        }
    }

    public int CurrentTabIndex { get; private set; } = 0;
    public int TabCount => null != tabs ? tabs.Length : 0;

    public void SetCursorBoxUI(ICursorBoxUI _cursorBoxUI, InputManager _inputManager = null)
    {
        if (null == tabs) return;
        for (int i = 0; tabs.Length > i; i++)
        {
            if (null != tabs[i].tabButton)
            {
                tabs[i].tabButton.SetCursorBoxUI(_cursorBoxUI, _inputManager);
            }
        }
    }

    public UI_OptionTabButton GetTabButton(int _index)
    {
        if (null != tabs && 0 <= _index && tabs.Length > _index) return tabs[_index].tabButton;
        return null;
    }

    public GameObject GetTabPanel(int _index)
    {
        if (null != tabs && 0 <= _index && tabs.Length > _index) return tabs[_index].tabPanel;
        return null;
    }

    public void OnTabClicked(int _index)
    {
        SelectTab(_index);
    }

    public void ShiftTab(int _delta)
    {
        if (null == tabs || 0 == tabs.Length) return;
        int _newIndex = (CurrentTabIndex + _delta) % tabs.Length;
        if (0 > _newIndex) _newIndex += tabs.Length;
        SelectTab(_newIndex);
        if (null != tabs[_newIndex].tabButton && true == tabs[_newIndex].tabButton.gameObject.activeInHierarchy)
        {
            EventSystem.current?.SetSelectedGameObject(tabs[_newIndex].tabButton.gameObject);
        }
    }

    public event Action<int> OnTabChanged;

    public void SelectTab(int _selectedIndex, bool _playSound = true)
    {
        if (null == tabs) return;

        bool _isChanged = (CurrentTabIndex != _selectedIndex);
        CurrentTabIndex = _selectedIndex;

        if (true == _isChanged && true == _playSound)
        {
            Sound.PlayUI(SoundID.OptionClick);
        }

        for (int i = 0; tabs.Length > i; i++)
        {
            bool _isSelected = (i == _selectedIndex);

            if (null != tabs[i].tabPanel)
            {
                tabs[i].tabPanel.SetActive(_isSelected);
            }
            if (null != tabs[i].tabButton)
            {
                tabs[i].tabButton.SetSelected(_isSelected);
            }
        }

        OnTabChanged?.Invoke(_selectedIndex);
    }
}
