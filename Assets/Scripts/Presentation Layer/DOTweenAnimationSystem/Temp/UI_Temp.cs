using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_Temp : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private ObjectMotionPlayer omp;
    
    public void Awake()
    {
        omp = gameObject.GetComponent<ObjectMotionPlayer>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        omp?.Play("Click");
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        omp?.Play("Enter");
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        omp?.Play("Exit");
    }
}
