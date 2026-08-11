using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;
using System;

public enum TutorialKeyType
{
    None,
    Move,
    Attack
}

[Serializable]
public struct TutorialKeyConfig
{
    public TutorialKeyType keyType;
    public GameObject rootObject; // 켜고 끌 오브젝트
    public string motionTag;      // motionPlayer에 재생할 태그
}

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

    [Header("Tutorial Keys")]
    [SerializeField] private TutorialKeyConfig[] tutorialKeyConfigs;

    private RectTransform rectTransform;
    private Transform targetTransform;
    private Vector2 positionOffset;
    private int showCount = 0;
    private bool bHide = false;
    private bool bFollowTarget = true;

    // 튜토리얼 상태 관리 변수
    private TutorialKeyType pendingTutorialKey = TutorialKeyType.None;
    private TutorialKeyType currentlyVisibleTutorialKey = TutorialKeyType.None;
    private bool isInteractionShowing => showCount > 0;

    public void Initialize(InputManager _inputManager)
    {
        rectTransform = GetComponent<RectTransform>();
        showCount = 0;
        bHide = true;
        pendingTutorialKey = TutorialKeyType.None;
        currentlyVisibleTutorialKey = TutorialKeyType.None;

        if (null != motionPlayer)
            motionPlayer.Initialize();

        if (null != keyboardImages)
        {
            for (int i = 0; i < keyboardImages.Length; i++)
            {
                if (null != keyboardImages[i]) keyboardImages[i].Initialize(_inputManager);
            }
        }

        // 초기화 시 모든 튜토리얼 키 숨기기
        if (tutorialKeyConfigs != null)
        {
            foreach (var config in tutorialKeyConfigs)
            {
                if (config.rootObject != null)
                    config.rootObject.SetActive(false);
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

        // 동적 키보드 아이콘이 배정되어 있다면 갱신
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
        {
            // 상호작용 UI가 우선이므로 화면에 켜져 있는 튜토리얼이 있다면 숨깁니다.
            HideVisibleTutorialKey();

            motionPlayer.Play(motionTag, bReset: true);
        }
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
            
        motionPlayer.PlayBackward(motionTag, bReset: true, _skip: _bSkip, _onComplete: () => 
        {
            Hide();
            // 상호작용 UI 연출 종료 후 대기 중인 튜토리얼 키가 있다면 화면에 복구합니다.
            ShowPendingTutorialKey();
        });
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
        // 단, 튜토리얼 UI가 띄워져 있다면 계속 위치를 업데이트 해야 함.
        if (false == bFollowTarget)
            return;

        if (true == bHide && currentlyVisibleTutorialKey == TutorialKeyType.None)
            return;

        Vector2 targetPosition = targetTransform.position;
        Vector2 newPos = targetPosition + positionOffset;
        rectTransform.position = newPos;
    }

    // ==========================================================
    // 튜토리얼 UI 제어부 (타 프로그래머 연동용 퍼블릭 메서드)
    // ==========================================================
    
    public void ShowTutorialKey(TutorialKeyType _type)
    {
        if (pendingTutorialKey == _type) return;

        // 다른 튜토리얼 키가 켜져 있거나 대기 중이라면 끕니다.
        if (pendingTutorialKey != TutorialKeyType.None && pendingTutorialKey != _type)
        {
            HideVisibleTutorialKey();
        }

        pendingTutorialKey = _type;

        // 현재 상호작용 UI가 떠있지 않다면 즉시 튜토리얼 UI를 띄웁니다.
        if (!isInteractionShowing)
        {
            ShowPendingTutorialKey();
        }
    }

    public void HideTutorialKey(TutorialKeyType _type)
    {
        if (pendingTutorialKey == _type)
        {
            pendingTutorialKey = TutorialKeyType.None;
            HideVisibleTutorialKey();
        }
    }

    public void HideAllTutorialKeys()
    {
        pendingTutorialKey = TutorialKeyType.None;
        HideVisibleTutorialKey();
    }

    // ==========================================================
    // 튜토리얼 내부 헬퍼 로직
    // ==========================================================
    
    private void ShowPendingTutorialKey()
    {
        if (pendingTutorialKey == TutorialKeyType.None) return;
        if (currentlyVisibleTutorialKey == pendingTutorialKey) return; // 이미 재생 중

        if (tutorialKeyConfigs == null) return;

        for (int i = 0; i < tutorialKeyConfigs.Length; i++)
        {
            if (tutorialKeyConfigs[i].keyType == pendingTutorialKey)
            {
                var config = tutorialKeyConfigs[i];
                if (config.rootObject != null)
                {
                    config.rootObject.SetActive(true);
                }

                if (motionPlayer != null && !string.IsNullOrEmpty(config.motionTag))
                {
                    motionPlayer.Play(config.motionTag, bReset: true);
                }

                currentlyVisibleTutorialKey = pendingTutorialKey;
                
                // 튜토리얼 UI가 나타나면 반드시 타겟을 따라가도록 설정합니다.
                bFollowTarget = true;
                break;
            }
        }
    }

    private void HideVisibleTutorialKey()
    {
        if (currentlyVisibleTutorialKey == TutorialKeyType.None) return;

        if (tutorialKeyConfigs == null) return;

        for (int i = 0; i < tutorialKeyConfigs.Length; i++)
        {
            if (tutorialKeyConfigs[i].keyType == currentlyVisibleTutorialKey)
            {
                var config = tutorialKeyConfigs[i];
                
                if (motionPlayer != null && !string.IsNullOrEmpty(config.motionTag))
                {
                    motionPlayer.PlayBackward(config.motionTag, bReset: true, _onComplete: () => 
                    {
                        if (config.rootObject != null) 
                            config.rootObject.SetActive(false);
                    });
                }
                else
                {
                    if (config.rootObject != null) 
                        config.rootObject.SetActive(false);
                }

                break;
            }
        }
        
        currentlyVisibleTutorialKey = TutorialKeyType.None;
    }
}
