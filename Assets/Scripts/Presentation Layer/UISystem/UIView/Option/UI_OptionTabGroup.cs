using UnityEngine;
using System;

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

        for (int i = 0; i < tabs.Length; i++)
        {
            if (null != tabs[i].tabButton)
            {
                tabs[i].tabButton.Initialize(this, i);
                
                if (null != _tabTexts && i < _tabTexts.Length)
                {
                    tabs[i].tabButton.SetText(_tabTexts[i]);
                }
            }
        }

        // 기본적으로 첫 번째 탭 활성화
        if (tabs.Length > 0)
        {
            SelectTab(0);
        }
    }

    public void RefreshTabTexts(string[] _tabTexts)
    {
        if (null == tabs || null == _tabTexts) return;

        for (int i = 0; i < tabs.Length; i++)
        {
            if (null != tabs[i].tabButton && i < _tabTexts.Length)
            {
                tabs[i].tabButton.SetText(_tabTexts[i]);
            }
        }
    }

    public void OnTabClicked(int _index)
    {
        SelectTab(_index);
    }

    private void SelectTab(int _selectedIndex)
    {
        if (null == tabs) return;

        for (int i = 0; i < tabs.Length; i++)
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
    }
}
