using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;

public class UI_InteractionUnit : MonoBehaviour
{
    // //외부 의존성
    [SerializeField] private ObjectMotionPlayer motionPlayer;
    
    // //내부 의존성
    [SerializeField] private Image iconImage;
    [SerializeField] private Sprite[] interactionIcons;
    [SerializeField] private string motionTag = "Default";

    public void Initialize()
    {
        if (null != motionPlayer)
            motionPlayer.Initialize();
    }

    /// <summary>
    /// 상호작용 UI를 노출합니다.
    /// 추후 단축키 설정에 따라 _iconIndex를 넘겨받아 아이콘을 변경할 수 있습니다.
    /// </summary>
    public void ShowInteraction(int _iconIndex = 0)
    {
        if (null == motionPlayer)
            return;
            
        if (null == interactionIcons || 0 == interactionIcons.Length)
            return;
            
        // 인덱스 범위 체크
        if (0 > _iconIndex || _iconIndex >= interactionIcons.Length)
            return;
            
        if (null != iconImage)
            iconImage.sprite = interactionIcons[_iconIndex];
            
        motionPlayer.Play(motionTag, bReset: true);
    }

    /// <summary>
    /// 상호작용 UI를 숨깁니다.
    /// </summary>
    public void HideInteraction()
    {
        if (null == motionPlayer)
            return;
            
        motionPlayer.PlayBackward(motionTag, bReset: true);
    }
}
