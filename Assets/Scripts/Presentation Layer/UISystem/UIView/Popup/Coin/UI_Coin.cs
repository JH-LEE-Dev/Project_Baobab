using TMPro;
using UnityEngine;

public class UI_Coin : MonoBehaviour
{
    //외부 의존성
    private TMP_Text moneyText;

    //내부 의존성
    private IInventory inventory;

    public void Initialize()
    {
        // 초기화 로직이 필요할 경우 작성
        moneyText = GetComponentInChildren<TMP_Text>();
    }

    public void BindInventory(IInventory _inventory)
    {
        inventory = _inventory;
        UpdateMoneyText();
    }

    public void UpdateMoneyText()
    {
        if (null == inventory || null == moneyText)
        {
            return;
        }

        moneyText.text = inventory.money.ToString();
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
