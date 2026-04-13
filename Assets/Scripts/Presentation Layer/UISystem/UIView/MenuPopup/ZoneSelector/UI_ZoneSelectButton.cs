using System;
using Microsoft.Unity.VisualStudio.Editor;
using NaughtyAttributes;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_ZoneButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Action<DungeonType> enterDungeonEvent;
    private DungeonType dungeonType;
    private Action enterHideEvent;

    [SerializeField] private RectTransform visualRect; 

    public void Initialize(Action<DungeonType> _bindEvent) => enterDungeonEvent = _bindEvent;
    public void Initialize(Action _bindEvent) => enterHideEvent = _bindEvent;


    public void ChangeDungeonType(DungeonType _type)
    {
        dungeonType = _type;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dungeonType == DungeonType.None)
        {
            enterHideEvent?.Invoke();
            return;
        }

        enterDungeonEvent?.Invoke(dungeonType);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        //visualRect
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        //visualRect   
    }
}
