using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;
using TMPro;

public class HUD_NotificationBadge : MonoBehaviour
{

    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private string showTag = "Show";
    [SerializeField] private string hideTag = "Hide";
    
    private TMP_Text temp;

    private MotionEntry showMotion;
    private MotionEntry hideMotion;

    public void Initialize()
    {
        omp?.Initialize();
        temp = GetComponent<TMP_Text>();
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
        if (null == temp)
            return;

        temp.text = _newCnt.ToString();
    }

    public void OnShow() => gameObject.SetActive(true);
    public void OnHide() => gameObject.SetActive(false);
}
