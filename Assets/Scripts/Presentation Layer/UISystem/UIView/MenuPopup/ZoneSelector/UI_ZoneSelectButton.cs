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
    private bool isInteractable = true;

    [SerializeField] private RectTransform visualRect; 

    public void Initialize(Action<DungeonType> _bindEvent) => enterDungeonEvent = _bindEvent;
    public void Initialize(Action _bindEvent) => enterHideEvent = _bindEvent;


    public void ChangeDungeonType(DungeonType _type)
    {
        dungeonType = _type;
    }

    public void SetInteractable(bool _interactable)
    {
        isInteractable = _interactable;
        
        // 시각적 피드백 (투명도 조절 등)
        if (visualRect != null)
        {
            CanvasGroup cg = visualRect.GetComponent<CanvasGroup>();
            if (cg == null) cg = visualRect.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = isInteractable ? 1.0f : 0.5f;
            cg.blocksRaycasts = isInteractable;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log(dungeonType);

        if (!isInteractable) 
            return;

        if (dungeonType == DungeonType.None)
        {
            enterHideEvent?.Invoke();
            return;
        }

        enterDungeonEvent?.Invoke(dungeonType);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isInteractable) return;
        //visualRect
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!isInteractable) return;
        //visualRect   
    }
}
