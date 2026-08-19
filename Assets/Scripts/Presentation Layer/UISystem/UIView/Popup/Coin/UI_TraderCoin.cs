using PresentationLayer.DOTweenAnimationSystem;
using PresentationLayer.UISystem.CustomNumber;
using UnityEngine;

public class UI_TraderCoin : MonoBehaviour
{
    [SerializeField] private CurrencyCounterHUD currencyCounter;
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private string twistTag = "Twist";

    public void Initialize()
    {
        currencyCounter?.Initialize();
        UpdateMoneyText(0);

        omp?.Initialize();
    }

    public void UpdateMoneyText(int _newMoney)
    {
        if (null == currencyCounter)
            return;

        currencyCounter.SetNumber(_newMoney);
        PlayTwistMotion();
    }

    // UpdateMoneyText()와 달리 트위스트 연출 없이 값만 스냅으로 맞춘다. 세이브 로드/주기적 Refresh처럼
    // "실제 변화가 아니라 현재값을 다시 확인시키는" 상황에서, 값이 그대로여도 매번 재생되는
    // PlayTwistMotion()의 불필요한 연출을 피하기 위한 용도.
    public void SyncMoneyTextSilent(int _newMoney)
    {
        currencyCounter?.SetNumber(_newMoney);
    }

    public void UpdateMoneyText_Anim(int _newMoney)
    {
        if (null == currencyCounter)
            return;

        currencyCounter.SetNumberAnimated(_newMoney, true);
    }

    private void PlayTwistMotion()
    {
        if (null == omp)
            return;

        omp.Play(twistTag, bReset: true);
    }

    public void OnHide()
    {
        if(null == canvasGroup)
            return;

        canvasGroup.alpha = 0f;
    }

    public void OnShow()
    {
        if(null == canvasGroup)
            return;
            
        canvasGroup.alpha = 1f;
    }
}
