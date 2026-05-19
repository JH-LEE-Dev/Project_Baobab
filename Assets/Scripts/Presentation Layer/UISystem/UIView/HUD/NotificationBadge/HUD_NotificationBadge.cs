using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;
using TMPro;
using PresentationLayer.UISystem.CustomNumber;

public class HUD_NotificationBadge : MonoBehaviour
{

    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private string showTag = "Show";
    [SerializeField] private string hideTag = "Hide";
    
    private CurrencyFontHUD fontHUD;

    private MotionEntry showMotion;
    private MotionEntry hideMotion;

    public void Initialize()
    {
        omp?.Initialize();

        if (null != fontHUD)
        {
            fontHUD.Initialize();
            //fontHUD.SetMod;
        }
    }

    public void UpdateAndInteraction(int _newCnt)
    {
        SetCount(_newCnt);
        
        if (0 < _newCnt)
            OnShow_Animated();
        else
            OnHide_Animated();
    }

    public void OnShow_Animated()
    {
        OnShow();

        if (null == omp)
            return;

        omp.SettingEntryMotion(hideMotion, true, true);
        showMotion = omp.Play(showTag, bReset: true);
    }

    public void OnHide_Animated()
    {
        if (null == omp)
            return;

        omp.SettingEntryMotion(showMotion, true, true);
        hideMotion = omp.Play(hideTag, bReset: true, _onComplete: OnHide);
    }

    public void SetCount(int _newCnt)
    {
        if (null == fontHUD)
            return;

        fontHUD.SetNumber(_newCnt);
    }

    public void OnShow() => gameObject.SetActive(true);
    public void OnHide() => gameObject.SetActive(false);
}
