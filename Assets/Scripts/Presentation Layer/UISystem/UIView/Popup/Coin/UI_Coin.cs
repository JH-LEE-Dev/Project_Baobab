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

    // [제거됨] UpdateMoneyText(int)
    //
    // 재화가 전부 long으로 넓어지면서(원목 가격이 int 범위를 넘겨 소지금이 음수가 되던 문제)
    // 호출부가 0이 된 죽은 오버로드였습니다. 인스펙터(UnityEvent)에 물린 곳도 없었습니다.
    //
    // 남겨두면 새 재화 UI를 붙이는 사람이 무심코 골라 쓰고, 21억을 넘는 순간 표시가 음수로
    // 깨집니다. 값을 직접 넣어야 하면 UI_TraderCoin.UpdateMoneyText(long)처럼 long으로
    // 받는 것을 쓰거나, 인자 없는 UpdateMoneyText()로 moneyData에서 직접 읽으십시오.

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
