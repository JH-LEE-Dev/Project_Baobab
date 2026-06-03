using System;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public class HUD_NavigationRegion : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // //외부 의존성
    [SerializeField] private ObjectMotionPlayer omp;
    [SerializeField] private string hoverTag = "Hover";
    [SerializeField] private string clickTag = "Click";
    [SerializeField] private string unHoverTag = "unHover";

    // //내부 의존성
    private MapType mapType = MapType.None;
    private Action<MapType> onSelectEvent;

    private MotionEntry hoverEntry;
    private MotionEntry clickEntry;
    private MotionEntry unHoverEntry;
    private bool isClicked = false;


    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize(MapType _mapType, Action<MapType> _onSelect)
    {
        mapType = _mapType;
        onSelectEvent = _onSelect;
        isClicked = false;

        if (null != omp)
            omp.Initialize();
    }


    // //Event System 구현부

    public void OnPointerClick(PointerEventData _eventData)
    {
        isClicked = true;
        onSelectEvent?.Invoke(mapType);

        if (null != omp)
        {
            if (null != hoverEntry)
                omp.SettingEntryMotion(hoverEntry, true, true);
            if (null != unHoverEntry)
                omp.SettingEntryMotion(unHoverEntry, true, true);
            clickEntry = omp.Play(clickTag, bReset: true, _onComplete: OnClickAnimationComplete);
        }
    }

    public void OnPointerEnter(PointerEventData _eventData)
    {
        if (true == isClicked)
            return;

        if (null != omp)
        {
            if (null != unHoverEntry)
                omp.SettingEntryMotion(unHoverEntry, true, true);
            if (null != clickEntry)
                omp.SettingEntryMotion(clickEntry, true, true);
            hoverEntry = omp.Play(hoverTag, bReset: true);
        }
    }

    public void OnPointerExit(PointerEventData _eventData)
    {
        if (true == isClicked)
            return;

        if (null != omp)
        {
            if (null != hoverEntry)
                omp.SettingEntryMotion(hoverEntry, true, true);
            if (null != clickEntry)
                omp.SettingEntryMotion(clickEntry, true, true);
            unHoverEntry = omp.Play(unHoverTag, bReset: true);
        }
    }

    private void OnClickAnimationComplete()
    {
        isClicked = false;
    }
}
