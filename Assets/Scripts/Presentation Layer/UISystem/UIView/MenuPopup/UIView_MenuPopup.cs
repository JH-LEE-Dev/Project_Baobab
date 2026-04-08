using UnityEngine;

public class UIView_MenuPopup : UIView
{
    [Header("Sub UI Prefabs")]
    [SerializeField] private GameObject zoneSelectorPrefab;
    [SerializeField] private GameObject zoneInfoPrefab;
    [SerializeField] private ZoneDatabase zoneDatabase;


    // 외부 의존성
    [Header("Sub UI Components")]
    private UI_ZoneSelector zoneSelector;
    private UI_ZoneInfo zoneInfo;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_ZoneInfo();
        Init_ZoneSelector();
    }


    private void Init_ZoneInfo()
    {
        if (null != zoneInfoPrefab)
            zoneInfo = Instantiate(zoneInfoPrefab, this.transform).GetComponent<UI_ZoneInfo>();

        if (null != zoneInfo)
        {
            zoneInfo.Initialize();
        }
    }

    private void Init_ZoneSelector()
    {
        if (null != zoneSelectorPrefab)
            zoneSelector = Instantiate(zoneSelectorPrefab, this.transform).GetComponent<UI_ZoneSelector>();    

        if (null != zoneSelector)
        {
            // 수정: 선택 상태 변경 콜백(HandleSelectionStatusChanged) 추가
            zoneSelector.Initialize(5, HandleZoneChanged, zoneDatabase, zoneInfo, HandleSelectionStatusChanged);
            zoneSelector.OpenRegion(0, 3);
            zoneSelector.UnlockZone(0, 0);
        }
    }

    private void HandleSelectionStatusChanged(bool _isSelected)
    {
        // 추후 확인 버튼 활성화/비활성화 로직 배치 위치
        Debug.Log($"[UIView_MenuPopup] Selection Status Changed: {_isSelected}");
        
        // 예: confirmButton.interactable = _isSelected;
    }





    private void HandleZoneChanged(int _regionId, int _zoneId)
    {
        // 3. 지역/구역 변경에 따른 정보창 동기화 타이밍
        // zoneInfo.UpdateDisplay(_regionId, _zoneId);
    }

    // --- 외부 진행 시스템(ProgressManager 등)에서 호출되는 타이밍 ---
    public void OnProgressUpdated(int _regionId, int _zoneId)
    {
        // 4. 특정 구역 해금 지시
        zoneSelector?.UnlockZone(_regionId, _zoneId);
        
        // 5. 특정 지역 클리어 시 다음 지역 묶음 통째로 개방 (예: 지역 1을 2개의 구역으로 개방)
        // zoneSelector?.OpenRegion(_regionId + 1, 2);
    }

    protected override void OnShow()
    {
        base.OnShow();

        zoneSelector?.OnShow();
        zoneInfo?.OnShow();
    }

    protected override void OnHide()
    {
        zoneSelector?.OnHide();
        zoneInfo?.OnHide();

        base.OnHide();
    }
}
