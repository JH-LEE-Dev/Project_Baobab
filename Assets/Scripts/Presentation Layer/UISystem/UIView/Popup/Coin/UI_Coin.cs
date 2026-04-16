using TMPro;
using UnityEngine;

public class UI_Coin : MonoBehaviour
{
    //외부 의존성
    private TMP_Text moneyText;

    //내부 의존성
    private IInventory inventory;

    private MoneyType moneyType;


    public void Initialize()
    {
        // 초기화 로직이 필요할 경우 작성
        moneyText = GetComponentInChildren<TMP_Text>();
    }

    public void BindInventory(IInventory _inventory, MoneyType _moneyType)
    {
        inventory = _inventory;
        moneyType = _moneyType;

        UpdateMoneyText();
    }

    public void UpdateMoneyText()
    {
        if (null == inventory || null == moneyText)
            return;

        if (MoneyType.Coin == moneyType)
            moneyText.text = inventory.money.ToString();
        else
            moneyText.text = inventory.carrot.ToString();
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
