using System;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class UI_Homing : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action clickedEvent;

    [SerializeField] private UI_InvMotionPlayer invMotionPlayer;

    public MapType currentMapType { get; set; } = MapType.Town;

    // TODO :: DOTWEEN 할 이미지 받기.

    public void Initialize()
    {
        // TODO :: 컴포넌트 바인딩
        invMotionPlayer?.Initialize();
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

    public void OpenInventory()
    {
        invMotionPlayer?.OpenInventory();
    }

    public void CloseInventory()
    {
        invMotionPlayer?.CloseInventory();
    }

    public void SkipAnimation(bool _isTrigger)
    {
        invMotionPlayer?.SkipAnimation(_isTrigger);
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
