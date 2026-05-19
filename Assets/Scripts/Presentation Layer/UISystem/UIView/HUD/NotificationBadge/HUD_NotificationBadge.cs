using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;
using TMPro;

public class HUD_NotificationBadge : MonoBehaviour
{

    [SerializeField] private ObjectMotionPlayer omp;
    
    private TMP_Text temp;

    public void Initialize()
    {
        omp?.Initialize();
        temp = GetComponent<TMP_Text>();
    }

    
}
