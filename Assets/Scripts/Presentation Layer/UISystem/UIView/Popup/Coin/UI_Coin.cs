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

        // 재화 종류가 늘어도 여기에 if 사슬이 쌓이지 않도록 통합 진입점으로 읽는다.
        // (예전에는 Coin이 아니면 무조건 carrot을 보여줘서, 새 재화를 붙이는 순간 엉뚱한 값이 나왔다)
        moneyText.text = moneyData.GetMoney(moneyType).ToString();
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
