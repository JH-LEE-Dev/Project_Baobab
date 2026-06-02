using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;
using PresentationLayer.UISystem.CustomNumber;
using TMPro;

public class HUD_NotificationBadge : MonoBehaviour
{

    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private TMP_Text font;
    [SerializeField] private CurrencyFontHUD currencyFont;
    [SerializeField] private string onOffTag = "NotificationAbsol";
    [SerializeField] private string popTag = "NotificationPop";
    

    private MotionEntry showMotion;
    private MotionEntry popMotion;
    private MotionEntry hideMotion;

    public void Initialize()
    {
        omp?.Initialize();
        currencyFont?.Initialize();
        currencyFont?.SetMode(CurrencyFontAlignmentMode.Center);
    }

    public void UpdateAndInteraction(int _newCnt)
    {
        if (0 >= _newCnt)
            OnHide_Animated();
        else if (1 >= _newCnt)
            OnShow_Animated(_newCnt);
        else
            PopAnimated(_newCnt);
    }

    public void OnShow_Animated(int _newCnt)
    {
        OnShow();
        SetCount(_newCnt);

        if (null == omp)
            return;
        
        omp.SettingEntryMotion(hideMotion, true, true);
        omp.SettingEntryMotion(popMotion, true, true);
        showMotion = omp.Play(onOffTag, bReset: true);
    }

    
    public void PopAnimated(int _newCnt)
    {
        if (null == omp)
            return;

        SetCount(_newCnt);

        omp.SettingEntryMotion(hideMotion, true, true);
        omp.SettingEntryMotion(showMotion, true, true);
        popMotion = omp.Play(popTag, bReset: true);
    }


    public void OnHide_Animated()
    {
        if (null == omp)
            return;

        omp.SettingEntryMotion(showMotion, true, true);
        omp.SettingEntryMotion(popMotion, true, true);
        hideMotion = omp.PlayBackward(onOffTag, bReset: true, _onComplete: OnHide);
    }

    public void SetCount(int _newCnt)
    {
        if (null != font)
            font.text = _newCnt.ToString();

        if (null != currencyFont)
            currencyFont.SetNumber(_newCnt);
    }

    public void OnShow() => gameObject.SetActive(true);
    public void OnHide()
    {
        SetCount(0);
        gameObject.SetActive(false);
    }
}
