using System.Collections.Generic;
using PresentationLayer.UISystem.CustomNumber;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;

/// <summary>
/// 인벤토리 슬롯에 마우스 오버 시 표시되는 상세 정보 팝업 UI입니다.
/// </summary>
public class UI_InventoryPopup : MonoBehaviour
{
    // //외부 의존성
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemDescriptionText;
    [SerializeField] private CurrencyCounterHUD uiCoin;
    [SerializeField] private ObjectMotionPlayer omp;

    [Header("Animation Settings")]
    [SerializeField] private string showTag = "Show";
    [SerializeField] private string hideTag = "Hide";

    // //내부 의존성
    private RectTransform rect;
    private MotionEntry enterMotion;
    private MotionEntry exitMotion;

    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(int _defaultCap)
    {
        rect = GetComponent<RectTransform>();
        
        if (null != uiCoin)
            uiCoin.SetNumber(0);
            
        if (null != omp)
            omp.Initialize();
    }

    public void SetupItem(ILogItemData _iLogItemData, Vector2 _position)
    {
        if (null == _iLogItemData)
            return;

        // TODO: 언어 시스템 연동 필요 (현재는 하드코딩)
        if (null != itemNameText)
            itemNameText.text = $"{_iLogItemData.treeType} Log ({_iLogItemData.logState})";

        if (null != itemDescriptionText)
            itemDescriptionText.text = $"이것은 {_iLogItemData.treeType} 타입 원목임.";

        if (null != rect && Vector2.zero != _position)
            rect.position = _position;
    }

    public void OnShow()
    {
        gameObject.SetActive(true);
        
        if (null != rect)
        {
            // 텍스트 변경 등으로 인해 사이즈가 변했을 수 있으므로 레이아웃 강제 갱신
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            rect.position = GlobalUI.KeepInsideScreenforUI(rect);
        }

        if (null == omp)
            return;

        omp.SettingEntryMotion(exitMotion, true, true);
        enterMotion = omp.Play(showTag, bReset: true);
    }

    public void OnHide()
    {
        if (null == omp)
            return;

        omp.SettingEntryMotion(enterMotion, true, true);
        exitMotion = omp.Play(hideTag, bReset: true, _onComplete: HandleCompletedAnimation);
    }

    private void HandleCompletedAnimation()
    {
        gameObject.SetActive(false);
    }
}
