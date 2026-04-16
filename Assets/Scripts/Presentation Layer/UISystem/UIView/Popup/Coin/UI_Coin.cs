using TMPro;
using UnityEngine;

public class UI_Coin : MonoBehaviour
{
    //외부 의존성
    private TMP_Text moneyText;

    //내부 의존성
    private IMoneyData moneyData;

    private MoneyType moneyType;


    public void Initialize()
    {
        // 초기화 로직이 필요할 경우 작성
        moneyText = GetComponentInChildren<TMP_Text>();
    }

    public void BindMoneyData(IMoneyData _moneyData, MoneyType _moneyType)
    {
        moneyData = _moneyData;
        moneyType = _moneyType;

        UpdateMoneyText();
    }

    public void UpdateMoneyText()
    {
        if (null == moneyData || null == moneyText)
            return;

        if (MoneyType.Coin == moneyType)
            moneyText.text = moneyData.money.ToString();
        else
            moneyText.text = moneyData.carrot.ToString();
    }

    public void UpdateMoneyText(int _money)
    {
        if (null == moneyText)
            return;

        moneyText.text = _money.ToString();
    }

    public void OnShow()
    {
        gameObject.SetActive(true);
        UpdateMoneyText();
    }

    public void OnHide()
    {
        gameObject.SetActive(false);
    }

    private void Awake()
    {
        // Awake 로직
    }

    private void Start()
    {
        // Start 로직
    }

    private void OnDestroy()
    {
        // OnDestroy 로직
    }
}
