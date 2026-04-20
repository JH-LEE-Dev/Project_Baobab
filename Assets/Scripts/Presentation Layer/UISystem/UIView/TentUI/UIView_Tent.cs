using System;
using UnityEngine;
using UnityEngine.UI;

public class UIView_Tent : UIView
{
    public event Action SleepEvent;
    private ISkillSystemProvider skillSystemProvider;
    private IMoneyData moneyData;

    [Header("UI References")]
    [SerializeField] private Transform uiRoot;
    [SerializeField] private RectTransform buttonPivot;
    [SerializeField] private GameObject buttonAbility;
    [SerializeField] private GameObject buttonCollection;
    [SerializeField] private GameObject buttonRest;
    [SerializeField] private UI_TentAbilityComponent abilityUIComponent;

    [Header("Tent Menu Unlock")]
    [SerializeField] private bool showAbilityOption = true;
    [SerializeField] private bool showCollectionOption = true;
    [SerializeField] private bool showRestOption = true;

    /// <summary>
    /// Tent UI 초기 설정을 진행한다.
    /// </summary>
    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        BindButtonEvents();
        InitializeComponents();
        ApplyButtonVisibility();

        if (buttonPivot != null)
            buttonPivot.gameObject.SetActive(false);
    }

    // Tent UI가 사용하는 하위 컴포넌트들을 초기화한다.
    private void InitializeComponents()
    {
        abilityUIComponent?.Initialize(skillSystemProvider);
    }


    // 외부에서 전달한 스킬 시스템을 보관한다.
    public void DependencyInjection(ISkillSystemProvider _skillSystemProvider, IMoneyData _moneyData)
    {
        skillSystemProvider = _skillSystemProvider;
        moneyData = _moneyData;
        abilityUIComponent?.Initialize(skillSystemProvider);
    }

    /// TentInteract 시 사용할 버튼 노출 루트를 찾는다.
    public override void SetupUI()
    {
        base.SetupUI();

        if (uiRoot == null)
            uiRoot = transform;
    }

    // 마우스 입력 처리
    public override void Update()
    {
        abilityUIComponent?.Tick();
    }


    // 능력 버튼 노출 여부를 갱신한다.
    public void SetAbilityOptionVisible(bool _visible)
    {
        showAbilityOption = _visible;
        ApplyButtonVisibility();
    }

    // 전리품 상자 버튼 노출 여부를 갱신한다.
    public void SetCollectionOptionVisible(bool _visible)
    {
        showCollectionOption = _visible;
        ApplyButtonVisibility();
    }

    // 휴식 버튼 노출 여부를 갱신한다.
    public void SetRestOptionVisible(bool _visible)
    {
        showRestOption = _visible;
        ApplyButtonVisibility();
    }

    // 해금 상태에 따라 버튼 활성 여부를 적용하고 레이아웃을 갱신한다.
    private void ApplyButtonVisibility()
    {
        if (buttonAbility != null)
            buttonAbility.SetActive(showAbilityOption);

        if (buttonCollection != null)
            buttonCollection.SetActive(showCollectionOption);

        if (buttonRest != null)
            buttonRest.SetActive(showRestOption);
    }


    public void TentInteract(bool _bInteract)
    {
        abilityUIComponent?.Close();


        if (_bInteract)
        {
            ApplyButtonVisibility();

            if (buttonPivot != null)
                buttonPivot.gameObject.SetActive(true);
        }
        else
        {
            if (buttonPivot != null)
                buttonPivot.gameObject.SetActive(false);
        }
    }


    // Tent 버튼 3개의 클릭 이벤트를 각 전용 함수에 연결
    private void BindButtonEvents()
    {
        BindButtonEvent(buttonAbility, OnAbilityButtonClicked);
        BindButtonEvent(buttonCollection, OnCollectionButtonClicked);
        BindButtonEvent(buttonRest, OnRestButtonClicked);
    }

    // Tent UI가 정리될 때 버튼 3개의 클릭 이벤트를 해제한다.
    private void ReleaseButtonEvents()
    {
        ReleaseButtonEvent(buttonAbility, OnAbilityButtonClicked);
        ReleaseButtonEvent(buttonCollection, OnCollectionButtonClicked);
        ReleaseButtonEvent(buttonRest, OnRestButtonClicked);
    }




    // 능력 버튼 클릭 시 호출되는 함수
    private void OnAbilityButtonClicked()
    {
        if (buttonPivot != null)
            buttonPivot.gameObject.SetActive(false);

        abilityUIComponent?.Open();
    }

    // 전리품 상자 버튼 클릭 시 호출되는 함수
    private void OnCollectionButtonClicked()
    {
        Debug.Log("Tent Collection button clicked.");
    }

    // 휴식 버튼 클릭 시 호출되는 함수
    private void OnRestButtonClicked()
    {
        Debug.Log("Tent Rest button clicked.");
        SleepEvent.Invoke();
    }






    // 지정한 버튼 오브젝트에 클릭 콜백을 연결
    private void BindButtonEvent(GameObject _buttonObject, UnityEngine.Events.UnityAction _callback)
    {
        Button button = GetButton(_buttonObject);
        if (button == null)
            return;

        button.onClick.RemoveListener(_callback);
        button.onClick.AddListener(_callback);
    }

    // 지정한 버튼 오브젝트에서 클릭 콜백을 해제
    private void ReleaseButtonEvent(GameObject _buttonObject, UnityEngine.Events.UnityAction _callback)
    {
        Button button = GetButton(_buttonObject);
        if (button == null)
            return;

        button.onClick.RemoveListener(_callback);
    }

    // 버튼 오브젝트에서 Button 컴포넌트를 가져온다.
    private Button GetButton(GameObject _buttonObject)
    {
        if (_buttonObject == null)
            return null;

        return _buttonObject.GetComponent<Button>();
    }

    public void CharacterEarnMoney(MoneyType _moneyType) //캐릭터가 돈을 얻었을 때,
    {
        
    }
    public void CharactersMoneyChanged() //캐릭터 돈에 변화가 있었을 때,
    {

    }

    // Tent UI 파괴 시 버튼 이벤트를 정리하기 위한 프레임워크 훅이다.
    public override void OnDestroy()
    {
        ReleaseButtonEvents();
    }

}
