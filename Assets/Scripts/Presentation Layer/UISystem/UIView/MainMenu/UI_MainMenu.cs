using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// 메인 메뉴의 실질적인 UI 요소들을 관리하는 스크립트입니다.
/// UIView_MainMenu에 의해 생성되어 소통 창구 역할을 합니다.
///
/// [로그라이크 하위 메뉴 - 로비 폐지, 시안 v3] 로그라이크 버튼을 누르면 메인 버튼이 퇴장 연출로 빠지고, 같은 자리에 Title_Plate 명판 + 게임 시작 / 기록 / 뒤로가
/// 같은 버튼 스타일(UI_MainMenuButton)로 들어온다. 오른쪽에는 최고 기록판(UI_RoguelikeMenuRecordPanel)이 미끄러져 들어온다.
///   게임 시작 = 기존 로그라이크 진입(UIView_MainMenu.OnRoguelikeButtonClicked). 기록 = 기록 창(UIView_MainMenu.OpenRoguelikeRecords).
///   뒤로 · Esc · 패드 B = 메인 버튼으로 돌아가고 패드 모드면 로그라이크 버튼을 다시 선택한다(가이드 3-8 "직전 선택 복원").
///   하위 메뉴가 열리면 패드 모드에서만 게임 시작을 선택한다(마우스 모드는 선택하지 않는다).
/// </summary>
public class UI_MainMenu : MonoBehaviour
{
    // 외부 의존성
    [Header("Main Buttons")]
    [SerializeField] private UI_MainMenuButton newGameButton;
    [SerializeField] private UI_MainMenuButton roguelikeButton;
    [SerializeField] private UI_MainMenuButton loadGameButton;
    [SerializeField] private UI_MainMenuButton exitButton;
    
    [Header("Sub Views")]
    [SerializeField] private UI_MainMenuButton optionButton;
    [SerializeField] private UI_MainMenuButton creditButton;

    [Header("External Links")]
    [SerializeField] private UI_ExternalLinkButton discordButton;

    [Header("Popup UI")]
    [SerializeField] private UI_WarningPopup warningPopup;

    [Header("Roguelike Menu (LumberBoy/UI/Build Roguelike Main Menu)")]
    [SerializeField] private RectTransform roguelikeMenuRoot;
    [SerializeField] private RectTransform roguelikePlate;
    [SerializeField] private TMP_Text roguelikePlateText;
    [SerializeField] private UI_MainMenuButton roguelikeStartButton;
    [SerializeField] private UI_MainMenuButton roguelikeRecordsButton;
    [SerializeField] private UI_MainMenuButton roguelikeBackButton;
    [SerializeField] private UI_RoguelikeMenuRecordPanel roguelikeRecordPanel;
    [SerializeField, Tooltip("하위 메뉴 첫 버튼의 Y(BTNs 기준). 명판 아래로 버튼 간격만큼 이어진다.")]
    private float roguelikeFirstButtonY = -32f;
    [SerializeField, Tooltip("메인 버튼 퇴장 연출 뒤 하위 메뉴가 들어오기까지의 시간(초, unscaled).")]
    private float roguelikeSwapDelay = 0.3f;
    [SerializeField] private float roguelikePlateSlideDistance = 40f;
    [SerializeField] private float roguelikePlateSlideSeconds = 0.22f;

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
    private UI_MainMenuButton[] buttonsInOrder;
    private readonly List<UI_MainMenuButton> activeButtonsCache = new List<UI_MainMenuButton>(6);
    
    // 캐싱 델리게이트
    private System.Action cachedExecuteNewGame;
    private System.Action cachedCancelNewGame;
    private System.Action cachedOnNewGameDisappearComplete;
    private System.Action<EInputDeviceType> cachedOnDeviceChanged;
    private System.Action<UI_MainMenuButton> cachedHandleButtonSelected;

    private bool isNewGameConfirmationOpen;
    private bool isInputBlocked;

    // 로그라이크 하위 메뉴 상태
    private const string RoguelikeStartKey = "Roguelike_Menu_Start";
    private const string RoguelikeRecordsKey = "Roguelike_Menu_Records";
    private const string RoguelikeBackKey = "Roguelike_Menu_Back";
    private const float RoguelikeDisappearStagger = 0.03f;
    private UI_MainMenuButton[] roguelikeButtons;
    private bool bRoguelikeMenuOpen;
    private bool bRoguelikeRecordsOpen;
    private bool bRoguelikeCancelBound;
    private Vector2 roguelikePlateBasePosition;
    private bool bRoguelikePlateBaseCaptured;
    private Action cachedOnRoguelikeCancel;
    private Action cachedOnRoguelikeRecordsClosed;
    private TweenCallback cachedShowRoguelikeButtons;
    private TweenCallback cachedShowMainButtonsAfterRoguelike;

    public bool IsRoguelikeMenuOpen => bRoguelikeMenuOpen;

    public bool IsInputBlocked => isInputBlocked;
    
    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(UIView_MainMenu _parentView, UIViewContext _uIViewContext)
    {
        parentView = _parentView;
        viewCtx = _uIViewContext;
        
        cachedExecuteNewGame = ExecuteNewGame;
        cachedCancelNewGame = CancelNewGame;
        cachedOnNewGameDisappearComplete = OnNewGameDisappearComplete;
        cachedOnDeviceChanged = OnDeviceChanged;
        cachedHandleButtonSelected = HandleButtonSelected;
        
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
        
        if (null != roguelikeButton)
        {
            roguelikeButton.Initialize(OnRoguelikeClicked, null, _inputMgr);
        }

        if (null != loadGameButton)
        {
            loadGameButton.Initialize(OnLoadGameClicked, null, _inputMgr);
            // saveSystem 의존성 주입 후 UpdateLoadGameButtonState()를 통해 상태가 갱신됩니다.
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

        InitializeRoguelikeMenu(_inputMgr);

        InitButtonsInOrder();

        if (null != buttonsInOrder)
        {
            for (int i = 0; buttonsInOrder.Length > i; i++)
            {
                if (null != buttonsInOrder[i])
                {
                    buttonsInOrder[i].OnButtonSelectedEvent -= cachedHandleButtonSelected;
                    buttonsInOrder[i].OnButtonSelectedEvent += cachedHandleButtonSelected;
                }
            }
        }

        SetLocalization();
        isInputBlocked = false;
    }

    public void SetDiscordButton(UI_ExternalLinkButton _discordButton)
    {
        discordButton = _discordButton;
        if (null != discordButton)
        {
            discordButton.SetParentMainMenu(this);
        }
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

    private void InitButtonsInOrder()
    {
        if (null == buttonsInOrder)
        {
            buttonsInOrder = new UI_MainMenuButton[]
            {
                loadGameButton,
                newGameButton,
                roguelikeButton,
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
            UpdateButtonsActiveState(_hasSaveData);
            UpdateButtonLayout();
        }
    }

    private void UpdateButtonsActiveState(bool _hasSaveData)
    {
        if (null != loadGameButton)
        {
            loadGameButton.gameObject.SetActive(_hasSaveData);
        }

        if (null != newGameButton) newGameButton.gameObject.SetActive(true);
        // 로그라이크 모드 진입 버튼. 해금 조건(본편 모든 특성 해금 + 엔딩)은 본편 엔딩이 아직 없어 개발 중에는 상시 활성이다(기획서 9장).
        if (null != roguelikeButton) roguelikeButton.gameObject.SetActive(true);
        if (null != optionButton) optionButton.gameObject.SetActive(true);
        if (null != creditButton) creditButton.gameObject.SetActive(true);
        if (null != exitButton) exitButton.gameObject.SetActive(true);
    }

    private void UpdateButtonLayout()
    {
        if (null == startPoint) return;
        if (null == buttonsInOrder) return;

        float _startY = startPoint.anchoredPosition.y;
        int _activeIndex = 0;

        // 버튼들이 기존에 하이어라키에서 가지고 있던 최소 Sibling Index를 찾습니다 (다른 배경 이미지 뒤로 숨지 않도록 방지)
        int _minSiblingIndex = int.MaxValue;
        for (int i = 0; buttonsInOrder.Length > i; i++)
        {
            if (null != buttonsInOrder[i])
            {
                int _idx = buttonsInOrder[i].transform.GetSiblingIndex();
                if (_idx < _minSiblingIndex) _minSiblingIndex = _idx;
            }
        }

        activeButtonsCache.Clear();

        for (int i = 0; buttonsInOrder.Length > i; i++)
        {
            UI_MainMenuButton _btn = buttonsInOrder[i];
            
            if (null != _btn && true == _btn.gameObject.activeSelf)
            {
                activeButtonsCache.Add(_btn);
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

        if (0 < activeButtonsCache.Count)
        {
            for (int i = 0; activeButtonsCache.Count > i; i++)
            {
                Navigation _nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = activeButtonsCache[(i - 1 + activeButtonsCache.Count) % activeButtonsCache.Count],
                    selectOnDown = activeButtonsCache[(i + 1) % activeButtonsCache.Count],
                    selectOnRight = (null != discordButton && true == discordButton.gameObject.activeInHierarchy) ? discordButton : null
                };
                activeButtonsCache[i].navigation = _nav;
            }
        }

        UpdateDiscordButtonNavigation();
    }

    /// <summary>
    /// 메인 메뉴 화면으로 복귀하거나 창이 닫혔을 때, 버튼들을 활성화하고 등장 연출을 1회 재생합니다.
    /// 전역 입력 락을 해제하고 모든 버튼의 내부 클릭 락을 안전하게 초기화합니다.
    /// </summary>
    public void ResetAndShowButtons()
    {
        CloseRoguelikeMenuImmediate();
        isInputBlocked = false;
        InitButtonsInOrder();

        bool _hasSaveData = false;
        if (null != parentView)
        {
            _hasSaveData = parentView.HasSaveData();
        }

        // 1. 모든 버튼의 활성화 상태를 먼저 확정합니다.
        //    (새 게임 진입 시 퇴장 연출로 비활성화되었던 버튼들을 켜주어야 UpdateButtonLayout이 전체 슬롯을 정확히 계산함)
        UpdateButtonsActiveState(_hasSaveData);

        // 2. 활성화된 버튼들을 기준으로 위치(anchoredPosition), SiblingIndex, Navigation을 재배치합니다.
        UpdateButtonLayout();

        // 3. 활성화된 버튼들의 상태를 리셋하고 순차 등장 연출 및 사운드를 재생합니다.
        int _appearSoundIndex = 0;
        for (int i = 0; buttonsInOrder.Length > i; i++)
        {
            UI_MainMenuButton _btn = buttonsInOrder[i];
            if (null != _btn && true == _btn.gameObject.activeSelf)
            {
                _btn.ResetButtonState();
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
        if (true == bRoguelikeMenuOpen)
        {
            HandleRoguelikeDeviceChanged(_device);
            return;
        }

        if (EInputDeviceType.Gamepad == _device)
        {
            HandleGamepadDeviceTransition();
        }
        else if (EInputDeviceType.KeyboardMouse == _device)
        {
            HandleKeyboardMouseDeviceTransition();
        }
    }

    private void HandleGamepadDeviceTransition()
    {
        if (null != discordButton && true == discordButton.gameObject.activeInHierarchy && true == discordButton.IsMouseOver())
        {
            MoveDirection _dir = GetTriggeringMoveDirection();
            if (MoveDirection.Left == _dir)
            {
                UI_MainMenuButton _target = (null != lastFocusedMainMenuButton && true == lastFocusedMainMenuButton.gameObject.activeInHierarchy)
                    ? lastFocusedMainMenuButton
                    : GetFirstActiveButton();

                UnhoverAllButtonsExcept(_target);
                if (null != _target && null != EventSystem.current)
                {
                    EventSystem.current.SetSelectedGameObject(_target.gameObject);
                }
                return;
            }

            UnhoverAllButtons();
            if (null != EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(discordButton.gameObject);
            }
            return;
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
                UnhoverAllButtons();
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

        UnhoverAllButtonsExcept(_targetBtn);

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

    private void HandleKeyboardMouseDeviceTransition()
    {
        if (null != EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }

        UnhoverAllButtons();
    }

    private void UnhoverAllButtonsExcept(UI_MainMenuButton _excludeButton)
    {
        if (null != buttonsInOrder)
        {
            for (int i = 0; buttonsInOrder.Length > i; i++)
            {
                UI_MainMenuButton _btn = buttonsInOrder[i];
                if (null != _btn && _btn != _excludeButton)
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
        
        if (null != roguelikeButton)
        {
            roguelikeButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 8));
            roguelikeButton.SetDotText(_dotText);
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

        SetRoguelikeMenuLocalization(_dotText);
    }
    
    private void UnhoverAllButtons()
    {
        if (null != buttonsInOrder)
        {
            for (int i = 0; buttonsInOrder.Length > i; i++)
            {
                if (null != buttonsInOrder[i]) buttonsInOrder[i].ForceUnhover();
            }
        }
        if (null != discordButton)
        {
            discordButton.ForceUnhover();
        }
    }
    
    private void OnNewGameClicked()
    {
        if (true == isInputBlocked) return;
        isInputBlocked = true;

        if (null != parentView)
        {
            if (ShouldConfirmNewGame())
            {
                isNewGameConfirmationOpen = true;

                UnhoverAllButtons();

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

        isInputBlocked = false;

        if (null != newGameButton)
        {
            newGameButton.ResetButtonState();
        }

        if (null != viewCtx && null != viewCtx.inputManager && true == viewCtx.inputManager.IsGamepadMode)
        {
            if (null != newGameButton && null != EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(newGameButton.gameObject);
                newGameButton.ForceHover();
            }
        }
    }
    
    private void OnLoadGameClicked()
    {
        if (true == isInputBlocked) return;
        isInputBlocked = true;
        UnhoverAllButtons();

        if (null != parentView)
        {
            parentView.OnLoadGameButtonClicked();
        }
    }
    
    /// <summary>
    /// 로그라이크 버튼: 로그라이크 하위 메뉴(게임 시작 / 기록 / 뒤로 + 최고 기록판)를 연다(로비 폐지).
    /// 실제 진입(디스토션 → 암전 → 씬 전환)은 하위 메뉴의 게임 시작(OnRoguelikeStartClicked)이 맡는다.
    /// </summary>
    private void OnRoguelikeClicked()
    {
        if (true == isInputBlocked) return;
        OpenRoguelikeMenu(true);
    }

    private void OnExitClicked()
    {
        if (true == isInputBlocked) return;
        isInputBlocked = true;
        UnhoverAllButtons();

        if (null != parentView)
        {
            parentView.OnExitButtonClicked();
        }
    }
    
    private void OnOptionClicked()
    {
        if (true == isInputBlocked) return;
        isInputBlocked = true;
        UnhoverAllButtons();

        if (null != parentView)
        {
            parentView.OnOptionButtonClicked();
        }
    }
    
    /// <summary>
    /// 옵션 또는 크레딧 등 서브 뷰가 닫혔을 때 메인 메뉴 버튼들을 다시 활성화하고 상태를 복원합니다.
    /// </summary>
    public void OnSubViewClosed()
    {
        isInputBlocked = false;

        if (null != optionButton)
        {
            optionButton.ReleaseMaintainState();
        }
        
        ResetAndShowButtons();

        if (null != viewCtx && null != viewCtx.inputManager && true == viewCtx.inputManager.IsGamepadMode && null != optionButton)
        {
            if (null != EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(optionButton.gameObject);
            }
        }
    }

    /// <summary>
    /// 하위 호환성을 위해 유지되는 메서드입니다. OnSubViewClosed()를 호출합니다.
    /// </summary>
    public void ReleaseOptionButtonState()
    {
        OnSubViewClosed();
    }
    
    private void OnCreditClicked()
    {
        if (true == isInputBlocked) return;
        isInputBlocked = true;
        UnhoverAllButtons();

        if (null != parentView)
        {
            parentView.OnCreditButtonClicked();
        }
    }
    
    // // 로그라이크 하위 메뉴

    private void InitializeRoguelikeMenu(InputManager _inputMgr)
    {
        cachedOnRoguelikeCancel = OnRoguelikeCancel;
        cachedOnRoguelikeRecordsClosed = OnRoguelikeRecordsClosed;
        cachedShowRoguelikeButtons = ShowRoguelikeButtons;
        cachedShowMainButtonsAfterRoguelike = ShowMainButtonsAfterRoguelike;

        roguelikeButtons = new UI_MainMenuButton[] { roguelikeStartButton, roguelikeRecordsButton, roguelikeBackButton };

        if (null != roguelikeStartButton) roguelikeStartButton.Initialize(OnRoguelikeStartClicked, null, _inputMgr);
        if (null != roguelikeRecordsButton) roguelikeRecordsButton.Initialize(OnRoguelikeRecordsClicked, null, _inputMgr);
        if (null != roguelikeBackButton) roguelikeBackButton.Initialize(OnRoguelikeBackClicked, null, _inputMgr);

        // 위아래로만 돈다(Explicit - 가이드 3-8).
        for (int i = 0; roguelikeButtons.Length > i; i++)
        {
            if (null == roguelikeButtons[i]) continue;

            Navigation nav = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = roguelikeButtons[(i - 1 + roguelikeButtons.Length) % roguelikeButtons.Length],
                selectOnDown = roguelikeButtons[(i + 1) % roguelikeButtons.Length],
            };
            roguelikeButtons[i].navigation = nav;
        }

        if (null != roguelikeRecordPanel)
        {
            roguelikeRecordPanel.Initialize(null != viewCtx ? viewCtx.localizationManager : null);
            roguelikeRecordPanel.HideImmediate();
        }

        if (null != roguelikeMenuRoot) roguelikeMenuRoot.gameObject.SetActive(false);
    }

    /// <summary>
    /// 로그라이크 하위 메뉴를 연다. _bAnimated면 메인 버튼 퇴장 연출 뒤에 들어오고, 아니면 바로 열린 상태가 된다
    /// (로그라이크 결과 화면에서 메인메뉴로 돌아왔을 때 - UIView_MainMenu.OpenRoguelikeMenuOnShow).
    /// </summary>
    public void OpenRoguelikeMenu(bool _bAnimated)
    {
        if (true == bRoguelikeMenuOpen) return;

        InitButtonsInOrder();
        bRoguelikeMenuOpen = true;
        isInputBlocked = true;
        UnhoverAllButtons();
        DOTween.Kill(this);

        if (true == _bAnimated)
        {
            int index = 0;
            for (int i = 0; buttonsInOrder.Length > i; i++)
            {
                UI_MainMenuButton btn = buttonsInOrder[i];
                if (null == btn || false == btn.gameObject.activeSelf) continue;

                btn.PlayDisappearMotion(index * RoguelikeDisappearStagger);
                index++;
            }

            DOVirtual.DelayedCall(roguelikeSwapDelay, cachedShowRoguelikeButtons, true).SetId(this);
        }
        else
        {
            ShowRoguelikeButtons();
        }

        BindRoguelikeCancel(true);
    }

    private void ShowRoguelikeButtons()
    {
        for (int i = 0; buttonsInOrder.Length > i; i++)
        {
            if (null != buttonsInOrder[i]) buttonsInOrder[i].gameObject.SetActive(false);
        }

        if (null != roguelikeMenuRoot) roguelikeMenuRoot.gameObject.SetActive(true);

        // 명판: 왼쪽에서 정수 픽셀로 미끄러져 들어온다.
        if (null != roguelikePlate)
        {
            if (false == bRoguelikePlateBaseCaptured)
            {
                roguelikePlateBasePosition = roguelikePlate.anchoredPosition;
                bRoguelikePlateBaseCaptured = true;
            }

            roguelikePlate.DOKill();
            roguelikePlate.anchoredPosition = roguelikePlateBasePosition - new Vector2(roguelikePlateSlideDistance, 0f);
            roguelikePlate.DOAnchorPos(roguelikePlateBasePosition, roguelikePlateSlideSeconds, true).SetEase(Ease.OutCubic).SetUpdate(true);
        }

        for (int i = 0; roguelikeButtons.Length > i; i++)
        {
            UI_MainMenuButton btn = roguelikeButtons[i];
            if (null == btn) continue;

            RectTransform rect = btn.GetComponent<RectTransform>();
            if (null != rect) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, roguelikeFirstButtonY - i * buttonSpacingY);
            btn.transform.SetSiblingIndex(i);
            btn.ResetButtonState();
            btn.ResetAndPlayAppear(i);
        }

        if (null != roguelikeRecordPanel)
        {
            roguelikeRecordPanel.Refresh();
            roguelikeRecordPanel.PlayShow();
        }

        isInputBlocked = false;

        if (null != viewCtx && null != viewCtx.inputManager && true == viewCtx.inputManager.IsGamepadMode)
        {
            SelectRoguelikeButton(roguelikeStartButton);
        }
    }

    /// <summary>하위 메뉴를 닫고 메인 버튼으로 돌아간다(뒤로 · Esc · 패드 B).</summary>
    public void CloseRoguelikeMenu()
    {
        if (false == bRoguelikeMenuOpen) return;

        bRoguelikeMenuOpen = false;
        isInputBlocked = true;
        BindRoguelikeCancel(false);
        DOTween.Kill(this);

        for (int i = 0; roguelikeButtons.Length > i; i++)
        {
            if (null != roguelikeButtons[i] && true == roguelikeButtons[i].gameObject.activeInHierarchy)
            {
                roguelikeButtons[i].PlayDisappearMotion(i * RoguelikeDisappearStagger);
            }
        }

        if (null != roguelikePlate)
        {
            roguelikePlate.DOKill();
            roguelikePlate.DOAnchorPos(roguelikePlateBasePosition - new Vector2(roguelikePlateSlideDistance, 0f), roguelikePlateSlideSeconds, true).SetEase(Ease.InCubic).SetUpdate(true);
        }

        if (null != roguelikeRecordPanel) roguelikeRecordPanel.PlayHide();

        DOVirtual.DelayedCall(roguelikeSwapDelay, cachedShowMainButtonsAfterRoguelike, true).SetId(this);
    }

    private void ShowMainButtonsAfterRoguelike()
    {
        if (null != roguelikeMenuRoot) roguelikeMenuRoot.gameObject.SetActive(false);
        ResetAndShowButtons();

        // 직전 선택(로그라이크 버튼)을 복원한다(가이드 3-8). ResetAndShowButtons가 첫 버튼을 고른 뒤 덮어쓴다.
        if (null != viewCtx && null != viewCtx.inputManager && true == viewCtx.inputManager.IsGamepadMode && null != roguelikeButton && null != EventSystem.current)
        {
            UnhoverAllButtonsExcept(roguelikeButton);
            EventSystem.current.SetSelectedGameObject(roguelikeButton.gameObject);
        }
    }

    /// <summary>메인메뉴로 돌아오거나 화면이 다시 열릴 때 하위 메뉴를 연출 없이 닫힌 상태로 되돌린다.</summary>
    private void CloseRoguelikeMenuImmediate()
    {
        DOTween.Kill(this);
        BindRoguelikeCancel(false);
        bRoguelikeMenuOpen = false;
        bRoguelikeRecordsOpen = false;

        if (null != roguelikePlate)
        {
            roguelikePlate.DOKill();
            if (true == bRoguelikePlateBaseCaptured) roguelikePlate.anchoredPosition = roguelikePlateBasePosition;
        }

        if (null != roguelikeButtons)
        {
            for (int i = 0; roguelikeButtons.Length > i; i++)
            {
                if (null != roguelikeButtons[i]) roguelikeButtons[i].ResetButtonState();
            }
        }

        if (null != roguelikeRecordPanel) roguelikeRecordPanel.HideImmediate();
        if (null != roguelikeMenuRoot) roguelikeMenuRoot.gameObject.SetActive(false);
    }

    private void OnRoguelikeStartClicked()
    {
        if (true == isInputBlocked || false == bRoguelikeMenuOpen) return;
        isInputBlocked = true;
        BindRoguelikeCancel(false);

        if (null != parentView) parentView.OnRoguelikeButtonClicked();
    }

    private void OnRoguelikeRecordsClicked()
    {
        if (true == isInputBlocked || false == bRoguelikeMenuOpen || null == parentView) return;

        bRoguelikeRecordsOpen = parentView.OpenRoguelikeRecords(cachedOnRoguelikeRecordsClosed);
        if (false == bRoguelikeRecordsOpen)
        {
            if (null != roguelikeRecordsButton) roguelikeRecordsButton.ResetButtonState();
            return;
        }

        // 기록 창이 떠 있는 동안 뒤쪽 버튼이 패드 선택으로 움직이지 않게 선택을 비운다. 닫히면 기록 버튼을 다시 고른다(OnRoguelikeRecordsClosed).
        if (null != EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        for (int i = 0; roguelikeButtons.Length > i; i++)
        {
            if (null != roguelikeButtons[i]) roguelikeButtons[i].ForceUnhover();
        }
    }

    private void OnRoguelikeRecordsClosed()
    {
        bRoguelikeRecordsOpen = false;
        if (false == bRoguelikeMenuOpen) return;

        if (null != roguelikeRecordsButton) roguelikeRecordsButton.ResetButtonState();
        if (null != viewCtx && null != viewCtx.inputManager && true == viewCtx.inputManager.IsGamepadMode)
        {
            SelectRoguelikeButton(roguelikeRecordsButton);
        }
    }

    private void OnRoguelikeBackClicked()
    {
        if (true == isInputBlocked) return;
        CloseRoguelikeMenu();
    }

    private void OnRoguelikeCancel()
    {
        // 기록 창이 열려 있으면 그 창이 먼저 닫힌다(같은 키로 두 단계가 한 번에 닫히지 않게).
        if (true == bRoguelikeRecordsOpen || true == isInputBlocked || false == bRoguelikeMenuOpen) return;
        Sound.PlayUI(SoundID.MainClick);
        CloseRoguelikeMenu();
    }

    private void BindRoguelikeCancel(bool _bBind)
    {
        if (null == viewCtx || null == viewCtx.inputManager || null == viewCtx.inputManager.inputReader) return;
        if (bRoguelikeCancelBound == _bBind) return;

        bRoguelikeCancelBound = _bBind;
        InputReader reader = viewCtx.inputManager.inputReader;
        reader.UICancelEvent -= cachedOnRoguelikeCancel;
        reader.ESCButtonPressedEvent -= cachedOnRoguelikeCancel;
        if (false == _bBind) return;

        reader.UICancelEvent += cachedOnRoguelikeCancel;
        reader.ESCButtonPressedEvent += cachedOnRoguelikeCancel;
    }

    private void SelectRoguelikeButton(UI_MainMenuButton _button)
    {
        if (null == _button || null == EventSystem.current) return;

        for (int i = 0; roguelikeButtons.Length > i; i++)
        {
            if (null != roguelikeButtons[i] && roguelikeButtons[i] != _button) roguelikeButtons[i].ForceUnhover();
        }

        if (EventSystem.current.currentSelectedGameObject == _button.gameObject) _button.ForceHover();
        else EventSystem.current.SetSelectedGameObject(_button.gameObject);
    }

    private void HandleRoguelikeDeviceChanged(EInputDeviceType _device)
    {
        if (EInputDeviceType.Gamepad == _device)
        {
            if (true == bRoguelikeRecordsOpen || true == isInputBlocked) return;

            UI_MainMenuButton target = roguelikeStartButton;
            for (int i = 0; roguelikeButtons.Length > i; i++)
            {
                if (null != roguelikeButtons[i] && true == roguelikeButtons[i].IsMouseOver())
                {
                    target = roguelikeButtons[i];
                    break;
                }
            }

            SelectRoguelikeButton(target);
            return;
        }

        if (null != EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
        for (int i = 0; roguelikeButtons.Length > i; i++)
        {
            if (null != roguelikeButtons[i]) roguelikeButtons[i].ForceUnhover();
        }
    }

    private void SetRoguelikeMenuLocalization(string _dotText)
    {
        if (null == viewCtx || null == viewCtx.localizationManager) return;

        LocalizationManager loc = viewCtx.localizationManager;
        if (null != roguelikePlateText) roguelikePlateText.text = loc.GetText(mainMenuUIJsonId, 8);
        SetRoguelikeButtonText(roguelikeStartButton, loc.GetText(RoguelikeStartKey), _dotText);
        SetRoguelikeButtonText(roguelikeRecordsButton, loc.GetText(RoguelikeRecordsKey), _dotText);
        SetRoguelikeButtonText(roguelikeBackButton, loc.GetText(RoguelikeBackKey), _dotText);
        if (true == bRoguelikeMenuOpen && null != roguelikeRecordPanel) roguelikeRecordPanel.Refresh();
    }

    private static void SetRoguelikeButtonText(UI_MainMenuButton _button, string _text, string _dotText)
    {
        if (null == _button) return;
        _button.SetText(_text);
        _button.SetDotText(_dotText);
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

    // 유니티 이벤트 함수
    private void OnDestroy()
    {
        if (null != viewCtx && null != viewCtx.inputManager && null != viewCtx.inputManager.inputReader && null != cachedOnDeviceChanged)
        {
            viewCtx.inputManager.inputReader.InputDeviceChangedEvent -= cachedOnDeviceChanged;
        }

        if (null != buttonsInOrder && null != cachedHandleButtonSelected)
        {
            for (int i = 0; buttonsInOrder.Length > i; i++)
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

        if (null != warningPopup)
        {
            warningPopup.Release();
        }

        DOTween.Kill(this);
        BindRoguelikeCancel(false);
        if (null != roguelikePlate) roguelikePlate.DOKill();
        if (null != roguelikeStartButton) roguelikeStartButton.Release();
        if (null != roguelikeRecordsButton) roguelikeRecordsButton.Release();
        if (null != roguelikeBackButton) roguelikeBackButton.Release();
    }
}
