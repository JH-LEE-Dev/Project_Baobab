using System;
using UnityEngine;

public class UIView_MenuPopup : UIView
{
    public event Action<MapType> DungeonSelectedEvent;

    [Header("Sub UI Prefabs")]
    [SerializeField] private GameObject zoneSelectorPrefab;
    [SerializeField] private GameObject zoneInfoPrefab;
    [SerializeField] private ZoneDatabase zoneDatabase;


    // 외부 의존성
    [Header("Sub UI Components")]
    private UI_ZoneSelector zoneSelector;
    private UI_ZoneInfo zoneInfo;
    private UI_ZoneButton zoneSelectButton;
    private UI_ZoneButton zoneCancelButton;

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);

        Init_ZoneInfo();
        Init_ZoneSelector();
        Init_ZoneButtons();

        // 버튼이 모두 초기화된 후 첫 지역을 엽니다.
        if (null != zoneSelector)
        {
            zoneSelector.OpenRegion(0, 3);
            zoneSelector.UnlockZone(0, 0);
            zoneSelector.UnlockZone(0, 1);
        }

        CloseTeleportUI();
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
            zoneSelector.Initialize(5, HandleZoneChanged, zoneDatabase, zoneInfo, HandleSelectionStatusChanged);
        }
    }

    private void Init_ZoneButtons()
    {
        // 자식 컴포넌트에서 버튼 참조 구성
        UI_ZoneButton[] buttons = GetComponentsInChildren<UI_ZoneButton>(true);
        foreach (var button in buttons)
        {
            if (button.name.Contains("SelectZone"))
                zoneSelectButton = button;

            else if (button.name.Contains("CancelZone"))
                zoneCancelButton = button;
        }

        // 런타임에 던전 진입 처리를 위한 이벤트 바인딩
        zoneSelectButton?.Initialize(HandleEnterDungeon);
        zoneCancelButton?.Initialize(CloseTeleportUI);
    }

    private void HandleEnterDungeon(MapType _type)
    {
        Debug.Log($"[UIView_MenuPopup] Entering Dungeon: {_type}");
        // 통신 및 던전 진입 로직 배치
        DungeonSelectedEvent?.Invoke(_type);
        CloseTeleportUI();
    }

    private void HandleSelectionStatusChanged(bool _isSelected)
    {
        // 추후 확인 버튼 활성화/비활성화 로직 배치 위치
        if (zoneSelectButton != null)
        {
            zoneSelectButton.SetInteractable(_isSelected);
        }
    }

    private void HandleZoneChanged(MapType _dungeonType)
    {
        // 지역/구역 변경 시 선택 버튼의 던전 타입 정보 업데이트
        zoneSelectButton?.ChangeDungeonType(_dungeonType);
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
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    public void TeleportUIOpen()
    {
        zoneSelector?.OnShow();
        zoneInfo?.OnShow();
    }

    public void CloseTeleportUI()
    {
        zoneSelector?.OnHide();
        zoneInfo?.OnHide();
    }
}
