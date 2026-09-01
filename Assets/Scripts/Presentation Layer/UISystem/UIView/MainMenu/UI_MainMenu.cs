using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 메인 메뉴의 실질적인 UI 요소들을 관리하는 스크립트입니다.
/// UIView_MainMenu에 의해 생성되어 소통 창구 역할을 합니다.
/// </summary>
public class UI_MainMenu : MonoBehaviour
{
    // 외부 의존성
    [Header("Main Buttons")]
    [SerializeField] private UI_MainMenuButton newGameButton;
    [SerializeField] private UI_MainMenuButton loadGameButton;
    [SerializeField] private UI_MainMenuButton exitButton;
    
    [Header("Additional Features (Upcoming)")]
    [SerializeField] private UI_MainMenuButton optionButton;
    [SerializeField] private UI_MainMenuButton creditButton;

    [Header("External Links")]
    [SerializeField] private UI_ExternalLinkButton discordButton;

    [Header("Popup UI")]
    [SerializeField] private UI_WarningPopup warningPopup;

    [Header("Localization Settings")]
    [SerializeField] private int mainMenuUIJsonId = 8;
    
    [Header("Layout Settings")]
    [SerializeField, Tooltip("버튼들이 순차적으로 배치될 기준점 (빈 오브젝트)")]
    private RectTransform startPoint;
    [SerializeField, Tooltip("버튼 간 간격 (Y축 거리)")]
    private float buttonSpacingY = 100f;
    
    // 내부 상태
    private UIView_MainMenu parentView;
    private UIViewContext viewCtx;
    private UI_MainMenuButton lastFocusedMainMenuButton;
    
    // 캐싱 델리게이트
    private System.Action cachedExecuteNewGame;
    private System.Action cachedCancelNewGame;
    private System.Action cachedOnNewGameDisappearComplete;
    private System.Action cachedSetLocalization;
    private System.Action<EInputDeviceType> cachedOnDeviceChanged;
    private System.Action<UI_MainMenuButton> cachedHandleButtonSelected;

    private bool isNewGameConfirmationOpen;
    
    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(UIView_MainMenu _parentView, UIViewContext _uIViewContext)
    {
        parentView = _parentView;
        viewCtx = _uIViewContext;
        
        cachedExecuteNewGame = ExecuteNewGame;
        cachedCancelNewGame = CancelNewGame;
        cachedOnNewGameDisappearComplete = OnNewGameDisappearComplete;
        cachedSetLocalization = SetLocalization;
        cachedOnDeviceChanged = OnDeviceChanged;
        cachedHandleButtonSelected = HandleButtonSelected;
        
        if (null != viewCtx && null != viewCtx.localizationManager)
        {
            viewCtx.localizationManager.OnLanguageChanged -= cachedSetLocalization;
            viewCtx.localizationManager.OnLanguageChanged += cachedSetLocalization;
        }

        if (null != viewCtx && null != viewCtx.inputManager && null != viewCtx.inputManager.inputReader)
        {
            viewCtx.inputManager.inputReader.InputDeviceChangedEvent -= cachedOnDeviceChanged;
            viewCtx.inputManager.inputReader.InputDeviceChangedEvent += cachedOnDeviceChanged;
        }
        
        InputManager _inputMgr = _uIViewContext?.inputManager;

        if (null != newGameButton)
        {
            newGameButton.Initialize(OnNewGameClicked, null, _inputMgr);
        }
        
        if (null != loadGameButton)
        {
            loadGameButton.Initialize(OnLoadGameClicked, null, _inputMgr);
            // 이곳에서의 초기 판단은 saveSystem 주입 전일 수 있으므로 제거하거나 둡니다. (안전하게 의존성 주입 후 다시 업데이트함)
        }
        
        if (null != exitButton)
        {
            exitButton.Initialize(OnExitClicked, null, _inputMgr);
        }
        
        if (null != optionButton)
        {
            optionButton.Initialize(OnOptionClicked, null, _inputMgr);
        }
        
        if (null != creditButton)
        {
            creditButton.Initialize(OnCreditClicked, null, _inputMgr);
        }

        if (null != warningPopup)
        {
            warningPopup.Initialize(_uIViewContext);
        }

        InitButtonsInOrder();

        if (null != buttonsInOrder)
        {
            for (int i = 0; i < buttonsInOrder.Length; i++)
            {
                if (null != buttonsInOrder[i])
                {
                    buttonsInOrder[i].OnButtonSelectedEvent -= cachedHandleButtonSelected;
                    buttonsInOrder[i].OnButtonSelectedEvent += cachedHandleButtonSelected;
                }
            }
        }

        SetLocalization();
    }

    public void SetDiscordButton(UI_ExternalLinkButton _discordButton)
    {
        discordButton = _discordButton;
        UpdateButtonLayout();
    }

    private void HandleButtonSelected(UI_MainMenuButton _button)
    {
        lastFocusedMainMenuButton = _button;
        UpdateDiscordButtonNavigation();
    }

    private void UpdateDiscordButtonNavigation()
    {
        if (null == discordButton) return;

        UI_MainMenuButton _target = lastFocusedMainMenuButton ?? GetFirstActiveButton();
        if (null != _target)
        {
            Navigation _discordNav = discordButton.navigation;
            _discordNav.mode = Navigation.Mode.Explicit;
            _discordNav.selectOnLeft = _target;
            _discordNav.selectOnUp = _target;
            _discordNav.selectOnDown = _target;
            discordButton.navigation = _discordNav;
        }
    }

    private UI_MainMenuButton[] buttonsInOrder;

    private void InitButtonsInOrder()
    {
        if (null == buttonsInOrder)
        {
            buttonsInOrder = new UI_MainMenuButton[]
            {
                loadGameButton,
                newGameButton,
                optionButton,
                creditButton,
                exitButton
            };
        }
    }

    public void UpdateLoadGameButtonState()
    {
        InitButtonsInOrder();

        if (null != parentView)
        {
            bool _hasSaveData = parentView.HasSaveData();
            
            if (null != loadGameButton)
            {
                // 세이브 데이터가 없으면 버튼 자체를 비활성화(숨김) 처리
                loadGameButton.gameObject.SetActive(_hasSaveData);
            }

            UpdateButtonLayout();
        }
    }

    private void UpdateButtonLayout()
    {
        if (null == startPoint) return;
        if (null == buttonsInOrder) return;

        float _startY = startPoint.anchoredPosition.y;
        int _activeIndex = 0;

        // 버튼들이 기존에 하이어라키에서 가지고 있던 최소 Sibling Index를 찾습니다 (다른 배경 이미지 뒤로 숨지 않도록 방지)
        int _minSiblingIndex = int.MaxValue;
        for (int i = 0; i < buttonsInOrder.Length; i++)
        {
            if (null != buttonsInOrder[i])
            {
                int _idx = buttonsInOrder[i].transform.GetSiblingIndex();
                if (_idx < _minSiblingIndex) _minSiblingIndex = _idx;
            }
        }

        List<UI_MainMenuButton> _activeButtons = new List<UI_MainMenuButton>();

        for (int i = 0; i < buttonsInOrder.Length; i++)
        {
            UI_MainMenuButton _btn = buttonsInOrder[i];
            
            if (null != _btn && _btn.gameObject.activeSelf)
            {
                _activeButtons.Add(_btn);
                RectTransform _rect = _btn.GetComponent<RectTransform>();
                if (null != _rect)
                {
                    // 시작점 Y에서 간격만큼 빼면서 아래로 배치
                    _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, _startY - (_activeIndex * buttonSpacingY));
                    
                    // Sibling Index를 기존 최소값부터 1씩 더해가며 갱신하여 순차 애니메이션은 살리고 뎁스(Depth) 꼬임은 방지
                    _btn.transform.SetSiblingIndex(_minSiblingIndex + _activeIndex);
                    
                    _activeIndex++;
                }
            }
        }

        if (0 < _activeButtons.Count)
        {
            for (int i = 0; _activeButtons.Count > i; i++)
            {
                Navigation _nav = new Navigation();
                _nav.mode = Navigation.Mode.Explicit;
                _nav.selectOnUp = _activeButtons[(i - 1 + _activeButtons.Count) % _activeButtons.Count];
                _nav.selectOnDown = _activeButtons[(i + 1) % _activeButtons.Count];
                if (null != discordButton && true == discordButton.gameObject.activeInHierarchy)
                {
                    _nav.selectOnRight = discordButton;
                }
                _activeButtons[i].navigation = _nav;
            }
        }

        UpdateDiscordButtonNavigation();
    }

    /// <summary>
    /// 게임 씬에서 다시 돌아왔을 때, 이전에 꺼진 버튼들을 다시 활성화하고 등장 연출을 재생합니다.
    /// </summary>
    public void ResetAndShowButtons()
    {
        InitButtonsInOrder();

        bool _hasSaveData = false;
        if (null != parentView)
        {
            _hasSaveData = parentView.HasSaveData();
        }

        for (int i = 0; i < buttonsInOrder.Length; i++)
        {
            UI_MainMenuButton _btn = buttonsInOrder[i];
            if (null != _btn)
            {
                // LoadGame 버튼은 세이브 데이터가 없으면 활성화하지 않음
                if (_btn == loadGameButton)
                {
                    _btn.gameObject.SetActive(_hasSaveData);
                    continue;
                }

                if (false == _btn.gameObject.activeSelf)
                {
                    _btn.gameObject.SetActive(true);
                }
            }
        }

        UpdateButtonLayout();

        int _appearSoundIndex = 0;
        for (int i = 0; i < buttonsInOrder.Length; i++)
        {
            UI_MainMenuButton _btn = buttonsInOrder[i];
            if (null != _btn && true == _btn.gameObject.activeSelf)
            {
                _btn.ResetAndPlayAppear(_appearSoundIndex);
                _appearSoundIndex++;
            }
        }

        SelectFirstActiveButton();
    }

    public UI_MainMenuButton GetFirstActiveButton()
    {
        if (null == buttonsInOrder) return null;

        for (int i = 0; buttonsInOrder.Length > i; i++)
        {
            UI_MainMenuButton _btn = buttonsInOrder[i];
            if (null != _btn && true == _btn.gameObject.activeInHierarchy)
            {
                return _btn;
            }
        }
        return null;
    }

    public void SelectFirstActiveButton()
    {
        if (null == viewCtx || null == viewCtx.inputManager || false == viewCtx.inputManager.IsGamepadMode)
            return;

        UI_MainMenuButton _first = GetFirstActiveButton();
        if (null != _first && null != EventSystem.current)
        {
            if (EventSystem.current.currentSelectedGameObject == _first.gameObject)
            {
                _first.ForceHover();
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(_first.gameObject);
            }
        }
    }

    private bool IsMainMenuButton(GameObject _obj)
    {
        if (null == _obj || null == buttonsInOrder) return false;
        for (int i = 0; buttonsInOrder.Length > i; i++)
        {
            if (null != buttonsInOrder[i] && buttonsInOrder[i].gameObject == _obj)
            {
                return true;
            }
        }
        return false;
    }

    private void OnDeviceChanged(EInputDeviceType _device)
    {
        if (EInputDeviceType.Gamepad == _device)
        {
            if (null != discordButton && true == discordButton.gameObject.activeInHierarchy && true == discordButton.IsMouseOver())
            {
                MoveDirection _dir = GetTriggeringMoveDirection();
                if (MoveDirection.Left == _dir)
                {
                    UI_MainMenuButton _target = (null != lastFocusedMainMenuButton && true == lastFocusedMainMenuButton.gameObject.activeInHierarchy)
                        ? lastFocusedMainMenuButton
                        : GetFirstActiveButton();

                    if (null != buttonsInOrder)
                    {
                        for (int i = 0; buttonsInOrder.Length > i; i++)
                        {
                            if (null != buttonsInOrder[i] && buttonsInOrder[i] != _target) buttonsInOrder[i].ForceUnhover();
                        }
                    }
                    if (null != _target && null != EventSystem.current)
                    {
                        EventSystem.current.SetSelectedGameObject(_target.gameObject);
                    }
                    return;
                }
                else
                {
                    if (null != buttonsInOrder)
                    {
                        for (int i = 0; buttonsInOrder.Length > i; i++)
                        {
                            if (null != buttonsInOrder[i]) buttonsInOrder[i].ForceUnhover();
                        }
                    }
                    if (null != EventSystem.current)
                    {
                        EventSystem.current.SetSelectedGameObject(discordButton.gameObject);
                    }
                    return;
                }
            }

            UI_MainMenuButton _hoveredBtn = null;
            if (null != buttonsInOrder)
            {
                for (int i = 0; buttonsInOrder.Length > i; i++)
                {
                    UI_MainMenuButton _btn = buttonsInOrder[i];
                    if (null != _btn && true == _btn.gameObject.activeInHierarchy && true == _btn.IsMouseOver())
                    {
                        _hoveredBtn = _btn;
                        break;
                    }
                }
            }

            UI_MainMenuButton _targetBtn = _hoveredBtn;
            if (null != _hoveredBtn)
            {
                MoveDirection _dir = GetTriggeringMoveDirection();
                if (MoveDirection.Down == _dir && null != _hoveredBtn.navigation.selectOnDown && _hoveredBtn.navigation.selectOnDown is UI_MainMenuButton _downBtn && true == _downBtn.gameObject.activeInHierarchy)
                {
                    _targetBtn = _downBtn;
                }
                else if (MoveDirection.Up == _dir && null != _hoveredBtn.navigation.selectOnUp && _hoveredBtn.navigation.selectOnUp is UI_MainMenuButton _upBtn && true == _upBtn.gameObject.activeInHierarchy)
                {
                    _targetBtn = _upBtn;
                }
                else if (MoveDirection.Right == _dir && null != discordButton && true == discordButton.gameObject.activeInHierarchy)
                {
                    lastFocusedMainMenuButton = _hoveredBtn;
                    if (null != buttonsInOrder)
                    {
                        for (int i = 0; buttonsInOrder.Length > i; i++)
                        {
                            if (null != buttonsInOrder[i]) buttonsInOrder[i].ForceUnhover();
                        }
                    }
                    if (null != EventSystem.current)
                    {
                        EventSystem.current.SetSelectedGameObject(discordButton.gameObject);
                    }
                    return;
                }
            }
            else
            {
                _targetBtn = GetFirstActiveButton();
            }

            if (null != buttonsInOrder)
            {
                for (int i = 0; buttonsInOrder.Length > i; i++)
                {
                    UI_MainMenuButton _btn = buttonsInOrder[i];
                    if (null != _btn && _btn != _targetBtn)
                    {
                        _btn.ForceUnhover();
                    }
                }
            }

            if (null != _targetBtn && null != EventSystem.current)
            {
                if (EventSystem.current.currentSelectedGameObject == _targetBtn.gameObject)
                {
                    _targetBtn.ForceHover();
                }
                else
                {
                    EventSystem.current.SetSelectedGameObject(_targetBtn.gameObject);
                }
            }
        }
        else if (EInputDeviceType.KeyboardMouse == _device)
        {
            if (null != EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            EvaluateMouseHoverStates(true);
        }
    }

    private void Update()
    {
        if (null != viewCtx && null != viewCtx.inputManager && false == viewCtx.inputManager.IsGamepadMode)
        {
            EvaluateMouseHoverStates(false);
        }
    }

    private void EvaluateMouseHoverStates(bool _forceCheckSound)
    {
        if (null == buttonsInOrder) return;
        for (int i = 0; buttonsInOrder.Length > i; i++)
        {
            UI_MainMenuButton _btn = buttonsInOrder[i];
            if (null == _btn || false == _btn.gameObject.activeInHierarchy) continue;

            if (true == _btn.IsMouseOver())
            {
                if (false == _btn.IsHovered)
                {
                    _btn.ForceHover(true);
                }
            }
            else
            {
                if (true == _btn.IsHovered)
                {
                    _btn.ForceUnhover();
                }
            }
        }
    }

    public void SetLocalization()
    {
        if (null == viewCtx || null == viewCtx.localizationManager)
            return;

        string _dotText = viewCtx.localizationManager.GetText(mainMenuUIJsonId, 7);
        if (true == string.IsNullOrEmpty(_dotText)) _dotText = "◆";

        if (null != newGameButton)
        {
            newGameButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 1));
            newGameButton.SetDotText(_dotText);
        }
        
        if (null != loadGameButton)
        {
            loadGameButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 2));
            loadGameButton.SetDotText(_dotText);
        }
        
        if (null != optionButton)
        {
            optionButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 3));
            optionButton.SetDotText(_dotText);
        }
        
        if (null != creditButton)
        {
            creditButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 4));
            creditButton.SetDotText(_dotText);
        }

        if (null != exitButton)
        {
            exitButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 5));
            exitButton.SetDotText(_dotText);
        }
    }
    
    private void OnNewGameClicked()
    {
        if (null != parentView)
        {
            if (ShouldConfirmNewGame())
            {
                isNewGameConfirmationOpen = true;
                string _warnMsg = viewCtx.localizationManager.GetText("NewGameWarning");
                warningPopup.ShowWarning(
                    _warnMsg,
                    cachedExecuteNewGame,
                    cachedCancelNewGame,
                    SoundID.ResultUIOpen,
                    SoundID.ResultUIClose,
                    SoundID.ResultUIHover);
            }
            else
            {
                ExecuteNewGame();
            }
        }
    }

    private bool ShouldConfirmNewGame()
    {
        return null != parentView
            && parentView.HasSaveData()
            && null != warningPopup
            && null != viewCtx
            && null != viewCtx.localizationManager;
    }

    private void ExecuteNewGame()
    {
        if (true == isNewGameConfirmationOpen)
        {
            isNewGameConfirmationOpen = false;
            Sound.PlayUI(SoundID.MainClick);
        }

        if (null != newGameButton)
        {
            newGameButton.PlayDisappearSequenceManually(cachedOnNewGameDisappearComplete);
        }
        else
        {
            OnNewGameDisappearComplete();
        }
    }

    private void OnNewGameDisappearComplete()
    {
        if (null != parentView)
        {
            parentView.OnNewGameStartButton();
        }
    }

    private void CancelNewGame()
    {
        if (true == isNewGameConfirmationOpen)
        {
            isNewGameConfirmationOpen = false;
            Sound.PlayUI(SoundID.MainClick);
        }

        // 팝업 닫힘 (특별한 동작 없음)
    }
    
    private void OnLoadGameClicked()
    {
        if (null != parentView)
        {
            parentView.OnLoadGameButtonClicked();
        }
    }
    
    private void OnExitClicked()
    {
        if (null != parentView)
        {
            parentView.OnExitButtonClicked();
        }
    }
    
    private void OnOptionClicked()
    {
        if (null != parentView)
        {
            parentView.OnOptionButtonClicked();
        }
    }
    
    public void ReleaseOptionButtonState()
    {
        if (null != optionButton)
        {
            optionButton.ReleaseMaintainState();
        }
        
        // 옵션 창이 닫히면 모든 메인 메뉴 버튼을 다시 활성화하고 등장 연출을 재생
        ResetAndShowButtons();

        if (null != viewCtx && null != viewCtx.inputManager && true == viewCtx.inputManager.IsGamepadMode && null != optionButton)
        {
            EventSystem.current?.SetSelectedGameObject(optionButton.gameObject);
        }
    }
    
    private void OnCreditClicked()
    {
        if (null != parentView)
        {
            parentView.OnCreditButtonClicked();
        }
    }
    
    // 유니티 이벤트 함수
    private void OnDestroy()
    {
        if (null != viewCtx && null != viewCtx.localizationManager && null != cachedSetLocalization)
        {
            viewCtx.localizationManager.OnLanguageChanged -= cachedSetLocalization;
        }

        if (null != viewCtx && null != viewCtx.inputManager && null != viewCtx.inputManager.inputReader && null != cachedOnDeviceChanged)
        {
            viewCtx.inputManager.inputReader.InputDeviceChangedEvent -= cachedOnDeviceChanged;
        }

        if (null != buttonsInOrder && null != cachedHandleButtonSelected)
        {
            for (int i = 0; i < buttonsInOrder.Length; i++)
            {
                if (null != buttonsInOrder[i])
                {
                    buttonsInOrder[i].OnButtonSelectedEvent -= cachedHandleButtonSelected;
                }
            }
        }

        if (null != newGameButton)
        {
            newGameButton.Release();
        }
        
        if (null != loadGameButton)
        {
            loadGameButton.Release();
        }
        
        if (null != exitButton)
        {
            exitButton.Release();
        }
        
        if (null != optionButton)
        {
            optionButton.Release();
        }
        
        if (null != creditButton)
        {
            creditButton.Release();
        }
    }

    private MoveDirection GetTriggeringMoveDirection()
    {
        Gamepad _pad = Gamepad.current;
        if (null == _pad) return MoveDirection.None;

        if (true == _pad.dpad.down.isPressed || _pad.leftStick.y.ReadValue() < -0.5f) return MoveDirection.Down;
        if (true == _pad.dpad.up.isPressed || _pad.leftStick.y.ReadValue() > 0.5f) return MoveDirection.Up;
        if (true == _pad.dpad.left.isPressed || _pad.leftStick.x.ReadValue() < -0.5f) return MoveDirection.Left;
        if (true == _pad.dpad.right.isPressed || _pad.leftStick.x.ReadValue() > 0.5f) return MoveDirection.Right;

        return MoveDirection.None;
    }
}
