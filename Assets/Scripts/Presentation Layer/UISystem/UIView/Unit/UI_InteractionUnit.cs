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
    [SerializeField] private string motionTag = "Absol";

    private RectTransform rectTransform;
    private Transform targetTransform;
    private Vector2 positionOffset;
    private bool bHide = false;
    private bool isShown = false;

    public void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
        isShown = false;
        bHide = true;

        if (null != motionPlayer)
            motionPlayer.Initialize();

        HideInteraction(true);
    }

    /// <summary>
    /// 추적할 대상과 오프셋을 설정합니다.
    /// </summary>
    public void SetTarget(Transform _target, Vector2 _offset)
    {
        targetTransform = _target;
        positionOffset = _offset;
    }

    /// <summary>
    /// 상호작용 UI를 노출합니다.
    /// </summary>
    public void ShowInteraction(int _iconIndex = 0)
    {
        bHide = false;

        if (null == interactionIcons || 0 == interactionIcons.Length)
            return;
            
        if (0 > _iconIndex || _iconIndex >= interactionIcons.Length)
            return;
            
        if (null != iconImage)
            iconImage.sprite = interactionIcons[_iconIndex];

        if (null == motionPlayer)
            return;

        if (false == isShown)
        {
            isShown = true;
            motionPlayer.Play(motionTag, bReset: true);
        }
    }

    /// <summary>
    /// 상호작용 UI를 숨깁니다.
    /// </summary>
    public void HideInteraction(bool _bSkip = false)
    {
        isShown = false;

        if (null == motionPlayer)
            return;
            
        motionPlayer.PlayBackward(motionTag, bReset: true, _skip: _bSkip, _onComplete: Hide);
    }

    private void Hide()
    {
        if (false == isShown)
            bHide = true;
    }

    private void LateUpdate()
    {
        if (null == targetTransform || null == rectTransform)
            return;

        // 노출 카운트가 0이면 위치 업데이트 생략 가능 (최적화)
        if (true == bHide)
            return;

        Vector2 targetPosition = targetTransform.position;
        Vector2 newPos = targetPosition + positionOffset;
        rectTransform.position = newPos;
    }
}
