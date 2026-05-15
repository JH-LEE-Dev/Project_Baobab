using PresentationLayer.DOTweenAnimationSystem;
using PresentationLayer.UISystem.CustomNumber;
using UnityEngine;

public class UI_TraderCoin : MonoBehaviour
{
    [SerializeField] private CurrencyCounterHUD currencyCounter;
    [SerializeField] private ObjectMotionPlayer omp;
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
}
