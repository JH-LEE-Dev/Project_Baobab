using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

public class UI_Homing : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public Action clickedEvent;

    // TODO :: DOTWEEN 할 이미지 받기.

    public void Initialize()
    {
        // TODO :: 컴포넌트 바인딩
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
        Scene currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

        if ("TownScene" == currentScene.name)
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
