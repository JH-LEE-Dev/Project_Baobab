using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    // //외부 의존성
    [SerializeField] private int localizationId;
    
    private TMP_Text targetText;
    private LocalizationManager locMgr;

    public void Initialize(LocalizationManager _locMgr)
    {
        locMgr = _locMgr;
        targetText = GetComponent<TMP_Text>();

        if (locMgr != null)
        {
            locMgr.OnLanguageChanged -= Refresh;
            locMgr.OnLanguageChanged += Refresh;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (locMgr != null && targetText != null)
        {
            targetText.text = locMgr.GetText(localizationId);
        }
    }

    private void OnDestroy()
    {
        if (locMgr != null) locMgr.OnLanguageChanged -= Refresh;
    }
}
