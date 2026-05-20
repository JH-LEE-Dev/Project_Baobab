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

    private RectTransform rectTransform;
    private Transform targetTransform;
    private Vector2 positionOffset;
    private int showCount = 0;
    private bool bHide = false;

    public void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
        showCount = 0;

        if (null != motionPlayer)
            motionPlayer.Initialize();
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
    /// 상호작용 UI를 노출합니다. (카운트 증가)
    /// </summary>
    public void ShowInteraction(int _iconIndex = 0)
    {
        showCount++;
        bHide = false;

        if (null == motionPlayer)
            return;
            
        if (null == interactionIcons || 0 == interactionIcons.Length)
            return;
            
        if (0 > _iconIndex || _iconIndex >= interactionIcons.Length)
            return;
            
        if (null != iconImage)
            iconImage.sprite = interactionIcons[_iconIndex];
            
        motionPlayer.Play(motionTag, bReset: true);
    }

    /// <summary>
    /// 상호작용 UI를 숨깁니다. (카운트 감소, 0일 때만 실제 은닉)
    /// </summary>
    public void HideInteraction(bool _bSkip = false)
    {
        showCount--;

        // 안전 장치: 카운트가 음수가 되지 않도록 함
        if (0 > showCount)
            showCount = 0;

        if (0 < showCount)
            return;

        if (null == motionPlayer)
            return;
            
        motionPlayer.PlayBackward(motionTag, bReset: true, _skip: _bSkip, _onComplete: Hide);
    }

    private void Hide() => bHide = true;

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
