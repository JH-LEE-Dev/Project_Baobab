using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine;

/// <summary>
/// 인벤토리의 전반적인 등장/퇴장 애니메이션을 제어하는 클래스입니다.
/// </summary>
public class UI_InvMotionPlayer : MonoBehaviour
{
    // //외부 의존성
    [SerializeField] private UIMotion_AbsoluteMove absoluteMove;

    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize()
    {
        if (null != absoluteMove)
            absoluteMove.Initialize();
    }

    public void OpenInventory()
    {
        if (null != absoluteMove)
            absoluteMove.Play();
    }

    public void CloseInventory()
    {
        if (null != absoluteMove)
            absoluteMove.PlayBackwards();
    }

    public void SkipAnimation(bool _isTrigger)
    {
        if (null != absoluteMove)
            absoluteMove.Skip(_isTrigger);
    }
}
