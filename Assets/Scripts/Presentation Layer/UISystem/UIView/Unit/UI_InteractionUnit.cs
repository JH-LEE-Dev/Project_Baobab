using UnityEngine;
using UnityEngine.UI;
using PresentationLayer.DOTweenAnimationSystem;
using System;
using UnityEngine.Events;

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
    public GameObject rootObject;          // 켜고 끌 전체 루트 오브젝트 (예: Tutorial_Move)
    public GameObject keyboardRootObject;  // 키보드 모드 시 활성화할 오브젝트 (예: KeyboardImages)
    public GameObject gamepadRootObject;   // 게임패드 모드 시 활성화할 오브젝트 (예: PadMoveImg)
    public string motionTag;               // motionPlayer에 재생할 태그
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
    private bool bTutorialKeyDisabled = false;
    private bool isInteractionShowing => 0 < showCount;

    private InputManager inputManager;

    // 델리게이트 캐싱
    private UnityAction cachedOnHideComplete;
    private UnityAction cachedOnHideVisibleTutorialKeyComplete;
    private Action<EInputDeviceType> cachedOnInputDeviceChanged;

    public void Initialize(InputManager _inputManager)
    {
        UnsubscribeEvents();

        rectTransform = GetComponent<RectTransform>();
        inputManager = _inputManager;
        showCount = 0;
        bHide = true;
        pendingTutorialKey = TutorialKeyType.None;
        currentlyVisibleTutorialKey = TutorialKeyType.None;
        bTutorialKeyDisabled = false;

        if (null == cachedOnHideComplete)
            cachedOnHideComplete = OnHideSequenceComplete;

        if (null == cachedOnHideVisibleTutorialKeyComplete)
            cachedOnHideVisibleTutorialKeyComplete = OnHideVisibleTutorialKeyComplete;

        if (null == cachedOnInputDeviceChanged)
            cachedOnInputDeviceChanged = OnInputDeviceChanged;

        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.InputDeviceChangedEvent += cachedOnInputDeviceChanged;
        }

        if (null != motionPlayer)
            motionPlayer.Initialize();

        if (null != keyboardImages)
        {
            for (int i = 0; keyboardImages.Length > i; i++)
            {
                if (null != keyboardImages[i]) keyboardImages[i].Initialize(_inputManager);
            }
        }

        // 초기화 시 모든 튜토리얼 키 숨기기
        if (null != tutorialKeyConfigs)
        {
            for (int i = 0; tutorialKeyConfigs.Length > i; i++)
            {
                if (null != tutorialKeyConfigs[i].rootObject)
                    tutorialKeyConfigs[i].rootObject.SetActive(false);
            }
        }

        HideInteraction(true);
    }

    private void OnDestroy()
    {
        UnsubscribeEvents();
    }

    private void UnsubscribeEvents()
    {
        if (null != inputManager && null != inputManager.inputReader && null != cachedOnInputDeviceChanged)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
        }
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
            
        if (0 > _iconIndex || interactionIcons.Length <= _iconIndex)
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
            
        motionPlayer.PlayBackward(motionTag, bReset: true, _skip: _bSkip, _onComplete: cachedOnHideComplete);
    }

    private void OnHideSequenceComplete()
    {
        Hide();
        // 상호작용 UI 연출 종료 후 대기 중인 튜토리얼 키가 있다면 화면에 복구합니다.
        ShowPendingTutorialKey();
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

        if (true == bHide && TutorialKeyType.None == currentlyVisibleTutorialKey)
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
        if (true == bTutorialKeyDisabled) return;
        if (_type == pendingTutorialKey) return;

        TutorialKeyType _prevKey = pendingTutorialKey;
        pendingTutorialKey = _type;

        // 다른 키로 직접 전환되는 경우(예: Move -> Attack), 이전 키를 즉시 비활성화하여 비동기 콜백 간섭을 방지
        if (TutorialKeyType.None != _prevKey && _type != _prevKey)
        {
            DeactivateKeyConfig(_prevKey);
        }

        // 현재 상호작용 UI가 떠있지 않다면 즉시 튜토리얼 UI를 띄웁니다.
        if (false == isInteractionShowing)
        {
            ShowPendingTutorialKey();
        }
    }

    public void HideTutorialKey(TutorialKeyType _type)
    {
        if (_type == pendingTutorialKey)
        {
            pendingTutorialKey = TutorialKeyType.None;
            HideVisibleTutorialKey();
        }
    }

    public void HideAllTutorialKeys()
    {
        bTutorialKeyDisabled = true;
        pendingTutorialKey = TutorialKeyType.None;
        HideVisibleTutorialKey();
    }

    // ==========================================================
    // 튜토리얼 내부 헬퍼 로직
    // ==========================================================
    
    private void OnInputDeviceChanged(EInputDeviceType _device)
    {
        RefreshTutorialKeyDeviceVisuals();
    }

    private void RefreshTutorialKeyDeviceVisuals()
    {
        if (null == tutorialKeyConfigs) return;

        bool _isGamepad = (null != inputManager) && inputManager.IsGamepadMode;

        for (int i = 0; tutorialKeyConfigs.Length > i; i++)
        {
            var config = tutorialKeyConfigs[i];
            if (config.keyType == currentlyVisibleTutorialKey || (null != config.rootObject && true == config.rootObject.activeSelf))
            {
                ApplyDeviceVisuals(config, _isGamepad);
            }
        }

        if (null != keyboardImages)
        {
            for (int i = 0; keyboardImages.Length > i; i++)
            {
                if (null != keyboardImages[i])
                {
                    keyboardImages[i].RefreshIcon();
                }
            }
        }
    }

    private void ApplyDeviceVisuals(TutorialKeyConfig _config, bool _isGamepad)
    {
        if (null != _config.keyboardRootObject)
        {
            _config.keyboardRootObject.SetActive(false == _isGamepad);
        }

        if (null != _config.gamepadRootObject)
        {
            _config.gamepadRootObject.SetActive(true == _isGamepad);
        }
    }

    private void ShowPendingTutorialKey()
    {
        if (true == bTutorialKeyDisabled) return;
        if (TutorialKeyType.None == pendingTutorialKey) return;
        if (pendingTutorialKey == currentlyVisibleTutorialKey) return; // 이미 재생 중

        if (null == tutorialKeyConfigs) return;

        bool _isGamepad = (null != inputManager) && inputManager.IsGamepadMode;

        for (int i = 0; tutorialKeyConfigs.Length > i; i++)
        {
            if (pendingTutorialKey == tutorialKeyConfigs[i].keyType)
            {
                var config = tutorialKeyConfigs[i];
                if (null != config.rootObject)
                {
                    config.rootObject.SetActive(true);
                }

                ApplyDeviceVisuals(config, _isGamepad);

                if (null != motionPlayer && false == string.IsNullOrEmpty(config.motionTag))
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
        TutorialKeyType _targetHideKey = currentlyVisibleTutorialKey;
        currentlyVisibleTutorialKey = TutorialKeyType.None;

        if (null == tutorialKeyConfigs) return;

        bool _hasTargetHideKey = (TutorialKeyType.None != _targetHideKey);

        for (int i = 0; tutorialKeyConfigs.Length > i; i++)
        {
            var config = tutorialKeyConfigs[i];
            bool _isTarget = (_hasTargetHideKey && _targetHideKey == config.keyType);
            bool _isActive = (null != config.rootObject && true == config.rootObject.activeSelf);

            if (true == _isTarget || true == _isActive)
            {
                if (null != motionPlayer && false == string.IsNullOrEmpty(config.motionTag))
                {
                    motionPlayer.PlayBackward(config.motionTag, bReset: false, _onComplete: cachedOnHideVisibleTutorialKeyComplete);
                }
                else
                {
                    DeactivateKeyConfig(config.keyType);
                }
            }
        }
    }

    private void DeactivateKeyConfig(TutorialKeyType _keyType)
    {
        if (TutorialKeyType.None == _keyType || null == tutorialKeyConfigs) return;

        for (int i = 0; tutorialKeyConfigs.Length > i; i++)
        {
            if (_keyType == tutorialKeyConfigs[i].keyType)
            {
                if (null != tutorialKeyConfigs[i].rootObject)
                {
                    tutorialKeyConfigs[i].rootObject.SetActive(false);
                }
                break;
            }
        }

        if (_keyType == currentlyVisibleTutorialKey)
        {
            currentlyVisibleTutorialKey = TutorialKeyType.None;
        }
    }

    private void OnHideVisibleTutorialKeyComplete()
    {
        // 상호작용 UI가 노출 중이거나, 대기 중인 튜토리얼 키가 없을 때만 안전하게 비활성화
        if (false == isInteractionShowing && TutorialKeyType.None != pendingTutorialKey) return;

        if (null == tutorialKeyConfigs) return;

        for (int i = 0; tutorialKeyConfigs.Length > i; i++)
        {
            if (null != tutorialKeyConfigs[i].rootObject)
            {
                tutorialKeyConfigs[i].rootObject.SetActive(false);
            }
        }
    }
}
