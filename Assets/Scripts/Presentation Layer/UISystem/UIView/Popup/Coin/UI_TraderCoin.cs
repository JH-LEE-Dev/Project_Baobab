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
        UpddateMoneyText(0);

        omp?.Initialize();
    }

    public void UpddateMoneyText(int _newMoney)
    {
        if (null == currencyCounter)
            return;

        currencyCounter.SetNumber(_newMoney);
        PlayTwistMotion();
    }

    public void UpddateMoneyText_Anim(int _newMoney)
    {
        if (null == currencyCounter)
            return;

        currencyCounter.SetNumberAnimated(_newMoney);
    }

    private void PlayTwistMotion()
    {
        if (null == omp)
            return;

        omp.Play(twistTag, bReset: true);
    }
}
