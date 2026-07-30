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

    [Header("Keyboard Icons")]
    [SerializeField] private UI_KeyboardImage[] keyboardImages;

    private RectTransform rectTransform;
    private Transform targetTransform;
    private Vector2 positionOffset;
    private int showCount = 0;
    private bool bHide = false;
    private bool bFollowTarget = true;

    public void Initialize(InputManager _inputManager)
    {
        rectTransform = GetComponent<RectTransform>();
        showCount = 0;
        bHide = true;

        if (null != motionPlayer)
            motionPlayer.Initialize();

        if (null != keyboardImages)
        {
            for (int i = 0; i < keyboardImages.Length; i++)
            {
                if (null != keyboardImages[i]) keyboardImages[i].Initialize(_inputManager);
            }
        }

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
    /// 상호작용 UI를 노출합니다. (카운트 증가)
    /// </summary>
    public void ShowInteraction(int _iconIndex = 0)
    {
        bHide = false;
        bFollowTarget = true;

        if (null == interactionIcons || 0 == interactionIcons.Length)
            return;
            
        if (0 > _iconIndex || _iconIndex >= interactionIcons.Length)
            return;
            
        if (null != iconImage)
            iconImage.sprite = interactionIcons[_iconIndex];

        // 동적 키보드 아이콘이 배정되어 있다면, interactionIcons 덮어쓰기 이후에 다시 한 번 올바른 키로 갱신해 줍니다.
        if (null != keyboardImages)
        {
            for (int i = 0; i < keyboardImages.Length; i++)
            {
                if (null != keyboardImages[i]) 
                    keyboardImages[i].RefreshIcon();
            }
        }

        if (null == motionPlayer)
            return;
            
        if (1 == ++showCount)
            motionPlayer.Play(motionTag, bReset: true);
    }

    /// <summary>
    /// 상호작용 UI를 숨깁니다. (카운트 감소, 0일 때만 실제 은닉)
    /// </summary>
    public void HideInteraction(bool _bSkip = false, bool _stopFollowing = false)
    {
        // 안전 장치: 카운트가 음수가 되지 않도록 함
        if (0 > --showCount)
            showCount = 0;

        if (0 < showCount)
            return;

        if (true == _stopFollowing)
            bFollowTarget = false;

        if (null == motionPlayer)
            return;
            
        motionPlayer.PlayBackward(motionTag, bReset: true, _skip: _bSkip, _onComplete: Hide);
    }

    private void Hide()
    {
        if (0 == showCount)
            bHide = true;
    }

    private void LateUpdate()
    {
        if (null == targetTransform || null == rectTransform)
            return;

        // 노출 카운트가 0이거나 따라가지 않는 상태면 위치 업데이트 생략 가능 (최적화)
        if (true == bHide || false == bFollowTarget)
            return;

        Vector2 targetPosition = targetTransform.position;
        Vector2 newPos = targetPosition + positionOffset;
        rectTransform.position = newPos;
    }
}
