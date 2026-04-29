using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;

public class UI_InvMotionPlayer : MonoBehaviour
{
    [SerializeField] private UIMotion_AbsoluteMove absoluteMove;

    public void Initialize()
    {
        absoluteMove?.Initialize();
    }

    public void OpenInventory()
    {
        absoluteMove?.Play();
    }

    public void CloseInventory()
    {
        absoluteMove?.PlayBackwards();
    }
}
