using System.Collections.Generic;
using PresentationLayer.UISystem.CustomNumber;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;

public class UI_InventoryPopup : MonoBehaviour
{
    //외부 의존성
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private TMP_Text itemDescriptionText;
    [SerializeField] private CurrencyCounterHUD uiCoin;
    [SerializeField] private ObjectMotionPlayer omp;

    [SerializeField] private string showTag = "Show";
    [SerializeField] private string hideTag = "Hide";

    //내부 의존성
    private RectTransform rect;

    private MotionEntry enter;
    private MotionEntry exit;

    public void Initialize(int _defaultCap)
    {
        rect = GetComponent<RectTransform>();
        uiCoin?.SetNumber(0);
        omp?.Initialize();
    }

    public void SetupItem(ILogItemData _iLogItemData, Vector2 _position)
    {
        if (null == _iLogItemData)
            return;

        // 임시 이름 및 설명 설정
        if (null != itemNameText)
            itemNameText.text = $"{_iLogItemData.treeType} Log ({_iLogItemData.logState})";

        if (null != itemDescriptionText)
            itemDescriptionText.text = $"이것은 {_iLogItemData.treeType} 타입 원목임.";

        if (null != rect && Vector2.zero != _position)
            rect.position = _position;

        Debug.Log(rect.position);
    }

    public void OnShow()
    {
        gameObject.SetActive(true);
        rect.position = GlobalUI.KeepInsideScreenforUI(rect);

        if (null == omp)
            return;

        omp.SettingEntryMotion(exit, true, true);
        enter = omp.Play(showTag, bReset: true);
    }

    public void OnHide()
    {
        if (null == omp)
            return;

        omp.SettingEntryMotion(enter, true, true);
        exit = omp.Play(hideTag, bReset: true, _onComplete: OnCompletedAnimation);
    }

    private void OnCompletedAnimation() => gameObject.SetActive(false);
}
