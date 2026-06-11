using System.Collections;
using TMPro;
using UnityEngine;

public class HUD_Message : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;

    private LocalizationManager localizationManager;
    private ForestType forestType = ForestType.None;
    private DungeonState dungeonState = DungeonState.None;
    private Coroutine hideCoroutine;

    public void Initialize(LocalizationManager _localizationManager)
    {
        localizationManager = _localizationManager;
        CacheTextRefs();
        RefreshTexts();

        if (null != localizationManager)
        {
            localizationManager.OnLanguageChanged -= RefreshTexts;
            localizationManager.OnLanguageChanged += RefreshTexts;
        }

        Hide();
    }

    public void Release()
    {
        if (null != localizationManager)
            localizationManager.OnLanguageChanged -= RefreshTexts;
    }

    public void SetMessage(ForestType _forestType, DungeonState _dungeonState)
    {
        forestType = _forestType;
        dungeonState = _dungeonState;

        RefreshTexts();
    }

    public void ShowForSeconds(float _duration)
    {
        RefreshTexts();
        gameObject.SetActive(true);

        if (null != hideCoroutine)
            StopCoroutine(hideCoroutine);

        hideCoroutine = StartCoroutine(HideAfterDelay(_duration));
    }

    public void Hide()
    {
        if (null != hideCoroutine)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }

        gameObject.SetActive(false);
    }

    private IEnumerator HideAfterDelay(float _duration)
    {
        yield return new WaitForSeconds(_duration);

        hideCoroutine = null;
        gameObject.SetActive(false);
    }

    private void CacheTextRefs()
    {
        if (null != titleText && null != subText)
            return;

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (null == titleText && texts[i].name == "TitleText")
                titleText = texts[i];
            else if (null == subText && texts[i].name == "SubText")
                subText = texts[i];
        }
    }

    private void RefreshTexts()
    {
        if (null != titleText)
            titleText.text = ResolveText(forestType);

        if (null != subText)
            subText.text = ResolveText(dungeonState);
    }

    private string ResolveText<T>(T _enumValue) where T : struct, System.Enum
    {
        if (null == localizationManager)
            return _enumValue.ToString();

        string localizedText = localizationManager.GetText(_enumValue);
        return string.IsNullOrEmpty(localizedText) ? _enumValue.ToString() : localizedText;
    }
}
