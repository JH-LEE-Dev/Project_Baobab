using System;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class UI_Homing : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action clickedEvent;

    public MapType currentMapType { get; set; } = MapType.Town;

    // TODO :: DOTWEEN 할 이미지 받기.

    public void Initialize()
    {

    }

    public void OnShow()
    {
        gameObject.SetActive(true);
    }

    public void OnHide()
    {
         gameObject.SetActive(false);
    }

    private void Homing()
    {
        clickedEvent.Invoke();
    }

    // TODO :: DOTWEEN 

    public void OnPointerClick(PointerEventData eventData)
    {
        if (MapType.Town == currentMapType)
            return;

        Homing();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        
    }
}
