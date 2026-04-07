using UnityEngine;

public class UIView_MenuPopup : UIView
{
    [Header("Sub UI Prefabs")]
    [SerializeField] private GameObject zoneSelectorPrefab;
    [SerializeField] private GameObject zoneInfoPrefab;

    // 외부 의존성
    [Header("Sub UI Components")]
    private UI_ZoneSelector zoneSelector;
    private UI_ZoneInfo zoneInfo;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_ZoneSelector();
        Init_ZoneInfo();
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

        // 1. 하위 시스템 초기화 및 클릭 콜백 연결 타이밍
        if (null != zoneSelector)
        {
            zoneSelector.Initialize(1, HandleZoneChanged);
            // 2. 초기 지역(묶음 0) 개방 타이밍: 구역 개수(3개)를 함께 전달하여 오류 수정
            zoneSelector.OpenRegion(0, 3);
            // 초기 첫 구역은 해금된 상태로 시작하도록 지시 가능
            zoneSelector.UnlockZone(0, 0);
        }
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
