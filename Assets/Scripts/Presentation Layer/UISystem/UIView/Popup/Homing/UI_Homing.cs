using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UIElements;

public class UI_Homing : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
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

    // TODO :: DOTWEEN 

    public void OnPointerClick(PointerEventData eventData)
    {
        
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        
    }
}
