using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

/// <summary>
/// ESC 메뉴의 백그라운드 조각 연출, 버튼 순차 등장 애니메이션, 되감기 역모션 퇴장, 다국어 로컬라이징을 총괄하는 컴포넌트입니다.
/// </summary>
public class UI_EscapeMenu : MonoBehaviour
{
    public enum BGDelayMode
    {
        CenterOutward, // 중앙 기준 대칭 확장
        Random,        // 무작위 순서
        TopToBottom,   // 상단부터 하단 순차 확장
        BottomToTop    // 하단부터 상단 순차 확장
    }

    private const float HiddenBGWidth = 0f;
    private const float DefaultBGTargetAlpha = 0.95f;

    [Header("Background Production")]
    [SerializeField] private RectTransform bgRoot;
    [SerializeField] private BGDelayMode bgDelayMode = BGDelayMode.CenterOutward;
    [SerializeField] private float bgOpenDuration = 0.25f;
    [SerializeField] private float bgPieceOpenDelay = 0.04f;
    [SerializeField] private float bgTargetWidth = 500f;
    [SerializeField] private Ease bgEase = Ease.OutCubic;
    [SerializeField] private Ease bgCloseEase = Ease.InCubic;

    [Header("Button Container & Buttons")]
    [SerializeField] private RectTransform buttonContainer;
    [SerializeField] private UI_EscapeMenuButton resumeButton;
    [Tooltip("[로그라이크 전용] 도전과제 버튼. 로그라이크 런 도중(SetLobbyMode(true))에만 계속하기 바로 아래에 보인다.")]
    [SerializeField] private UI_EscapeMenuButton challengesButton;
    [SerializeField] private UI_EscapeMenuButton optionButton;
    [SerializeField] private UI_EscapeMenuButton mainMenuButton;
    [SerializeField] private UI_EscapeMenuButton exitButton;

    [Header("Button Production Settings")]
    [SerializeField] private float buttonOpenDuration = 0.2f;
    [SerializeField] private float buttonStaggerDelay = 0.06f;

    [Header("Localization Settings")]
    [SerializeField] private int localizationJsonId = 13;
    [SerializeField] private int resumeTextId = 1;
    [SerializeField] private int optionTextId = 2;
    [SerializeField] private int mainMenuTextId = 3;
    [SerializeField] private int exitTextId = 4;

    [Header("Popup UI")]
    [SerializeField] private UI_WarningPopup warningPopup;

    [Header("Warning Popup Localization Keys")]
    [SerializeField] private string exitWarningKey = "ExitGameWarning";
    [SerializeField] private string mainMenuWarningKey = "MainMenuWarning";

    [Header("Roguelike")]
    [Tooltip("로그라이크 런 도중 메인메뉴 버튼 자리에 쓰는 '로비' 문구와 확인 문구(기획서 3-7 '런 중도 나가기').")]
    [SerializeField] private int lobbyTextId = 7;
    [SerializeField] private string lobbyWarningKey = "LobbyWarning";
    [Tooltip("도전과제 버튼 문구 키. 메인메뉴 로그라이크 메뉴의 도전과제와 같은 문구를 쓴다.")]
    [SerializeField] private string challengesTextKey = "Roguelike_Menu_Challenges";

    // 버튼은 최대 5개(계속하기 · 도전과제 · 옵션 · 로비 · 게임 종료)다. 등장 효과음 콜백 캐시 크기.
    private const int MaxButtonCount = 5;

    // 로그라이크 런 도중에만 true. 본편에서는 아무도 켜지 않아 기존 문구 그대로다.
    private bool bLobbyMode = false;
    // 활성 버튼 구성(로비 모드)이 바뀌어 버튼 배열 · 내비게이션 · BG 띠를 다시 맞춰야 하면 true.
    private bool bButtonLayoutDirty = true;

    private LocalizationManager localizationManager;
    private Action cachedRefreshLocalizedTexts;
    private Action cachedConfirmExit;
    private Action cachedConfirmMainMenu;
    private Action cachedCancelWarningAndRestoreMenu;
    private Action<EInputDeviceType> cachedOnInputDeviceChanged;
    private InputManager inputManager;

    private Action onResumeCallback;
    private Action onChallengesCallback;
    private Action onOptionCallback;
    private Action onMainMenuCallback;
    private Action onExitCallback;

    private Sequence openSequence;
    private Sequence closeSequence;
    private bool isClosing = false;
    private bool isMenuOpen = false;
    private bool isButtonClicked = false;
    private UIView_ESC parentESCView;

    public bool IsMenuOpen => isMenuOpen;

    private CanvasGroup menuCanvasGroup;
    private CanvasGroup bgCanvasGroup;
    private CanvasGroup buttonContainerCanvasGroup;
    // 연출에 쓰는 활성 BG 띠(위 -> 아래). 버튼 수에 맞춰 bgPiecePool에서 필요한 만큼만 담는다.
    private EscapeMenuBGPiece[] bgPieces = Array.Empty<EscapeMenuBGPiece>();
    // 만들어 둔 모든 BG 띠(위 -> 아래). 모자라면 마지막 띠를 복제해 늘리고, 남는 띠는 꺼 둔다.
    private readonly List<EscapeMenuBGPiece> bgPiecePool = new List<EscapeMenuBGPiece>(8);
    private float bgStripHeight;

    private UI_EscapeMenuButton[] allButtons = Array.Empty<UI_EscapeMenuButton>();
    private readonly List<UI_EscapeMenuButton> activeButtonBuffer = new List<UI_EscapeMenuButton>(MaxButtonCount);
    private readonly TweenCallback[] buttonSoundCallbackCache = new TweenCallback[MaxButtonCount];
    private TweenCallback[] buttonAppearCallbacks = Array.Empty<TweenCallback>();
    private TweenCallback[] buttonAppearSoundCallbacks = Array.Empty<TweenCallback>();
    private TweenCallback[] buttonDisappearCallbacks = Array.Empty<TweenCallback>();
    private bool soundsEnabled;

    private Action onOpenCompleteAction;
    private Action onCloseCompleteAction;
    private TweenCallback cachedOnOpenComplete;
    private TweenCallback cachedOnCloseComplete;

    private sealed class EscapeMenuBGPiece
    {
        public RectTransform rectTransform;
        public Graphic graphic;
        public float targetWidth;
        public float targetAlpha;
        public float delay;
    }

    private void Awake()
    {
        cachedOnOpenComplete = HandleOpenComplete;
        cachedOnCloseComplete = HandleCloseComplete;

        if (null == parentESCView)
        {
            parentESCView = GetComponentInParent<UIView_ESC>();
        }

        CacheCanvasGroups();
        CacheBGPieces();
        RebuildButtonLayout();
    }

    private void OnDisable()
    {
        isMenuOpen = false;
        isClosing = false;
    }

    private void OnDestroy()
    {
        KillProductionSequences();

        if (null != localizationManager && null != cachedRefreshLocalizedTexts)
        {
            localizationManager.OnLanguageChanged -= cachedRefreshLocalizedTexts;
        }

        if (null != inputManager && null != inputManager.inputReader && null != cachedOnInputDeviceChanged)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
        }

        onResumeCallback = null;
        onChallengesCallback = null;
        onOptionCallback = null;
        onMainMenuCallback = null;
        onExitCallback = null;

        cachedConfirmExit = null;
        cachedConfirmMainMenu = null;
        cachedCancelWarningAndRestoreMenu = null;
        cachedOnInputDeviceChanged = null;
        cachedOnOpenComplete = null;
        cachedOnCloseComplete = null;
        onOpenCompleteAction = null;
        onCloseCompleteAction = null;

        if (null != warningPopup)
        {
            warningPopup.Release();
        }

        inputManager = null;
        isMenuOpen = false;
        isButtonClicked = false;
        parentESCView = null;
    }

    public void Initialize(
        LocalizationManager _localizationManager,
        Action _onResume,
        Action _onOption,
        Action _onMainMenu,
        Action _onExit,
        UI_WarningPopup _warningPopup = null,
        InputManager _inputManager = null,
        Action _onChallenges = null)
    {
        localizationManager = _localizationManager;
        onResumeCallback = _onResume;
        onChallengesCallback = _onChallenges;
        onOptionCallback = _onOption;
        onMainMenuCallback = _onMainMenu;
        onExitCallback = _onExit;
        inputManager = _inputManager;

        if (null == cachedOnInputDeviceChanged)
        {
            cachedOnInputDeviceChanged = OnDeviceChanged;
        }

        if (null != inputManager && null != inputManager.inputReader && null != cachedOnInputDeviceChanged)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
            inputManager.inputReader.InputDeviceChangedEvent += cachedOnInputDeviceChanged;
        }

        if (null != _warningPopup)
        {
            warningPopup = _warningPopup;
        }

        if (null == parentESCView)
        {
            parentESCView = GetComponentInParent<UIView_ESC>();
        }

        if (null == warningPopup)
        {
            warningPopup = parentESCView?.GetComponentInChildren<UI_WarningPopup>(true)
                ?? GetComponentInChildren<UI_WarningPopup>(true);
        }

        cachedConfirmExit = ConfirmExit;
        cachedConfirmMainMenu = ConfirmMainMenu;
        cachedCancelWarningAndRestoreMenu = CancelWarningAndRestoreMenu;

        if (null != resumeButton) resumeButton.Initialize(OnResumeButtonClicked, inputManager);
        if (null != challengesButton) challengesButton.Initialize(OnChallengesButtonClicked, inputManager);
        if (null != optionButton) optionButton.Initialize(OnOptionButtonClicked, inputManager);
        if (null != mainMenuButton) mainMenuButton.Initialize(OnMainMenuButtonClicked, inputManager);
        if (null != exitButton) exitButton.Initialize(OnExitButtonClicked, inputManager);

        // 활성 버튼 순서대로 배열 · 위아래 순환 내비게이션 · BG 띠를 맞춘다.
        RebuildButtonLayout();

        if (null != localizationManager)
        {
            if (null == cachedRefreshLocalizedTexts)
                cachedRefreshLocalizedTexts = RefreshLocalizedTexts;

            localizationManager.OnLanguageChanged -= cachedRefreshLocalizedTexts;
            localizationManager.OnLanguageChanged += cachedRefreshLocalizedTexts;

            RefreshLocalizedTexts();
        }
    }

    /// <summary>[로그라이크 전용] 메인메뉴 버튼을 "로비(포기)" 문구로 바꾸거나 되돌린다. 버튼 동작은 그대로다.</summary>
    public void SetLobbyMode(bool _bLobbyMode)
    {
        if (bLobbyMode == _bLobbyMode) return;

        bLobbyMode = _bLobbyMode;
        bButtonLayoutDirty = true;
        RefreshLocalizedTexts();

        // 메뉴가 열려 있지도 연출 중이지도 않으면 바로 맞춘다. 그렇지 않으면 다음 PlayOpenProduction이 맞춘다.
        if (false == isMenuOpen && null == openSequence && null == closeSequence)
        {
            RebuildButtonLayout();
        }
    }

    /// <summary>
    /// 지금 모드에서 보여 줄 버튼(계속하기 · [도전과제] · 옵션 · 메인메뉴/로비 · 종료)만 표시 순서대로 모아
    /// 버튼 배열 · 등장/퇴장/효과음 콜백 · 위아래 순환 내비게이션 · 버튼 영역 위치 · BG 띠를 다시 맞춘다.
    /// 효과음은 활성 순서 번호로 묶는다(본편 4개 = Dot00~03, 로그라이크 5개 = Dot00~04). 모드가 바뀔 때만 불린다.
    /// </summary>
    private void RebuildButtonLayout()
    {
        if (null != challengesButton) challengesButton.gameObject.SetActive(bLobbyMode);

        activeButtonBuffer.Clear();
        if (null != resumeButton) activeButtonBuffer.Add(resumeButton);
        if (null != challengesButton && true == bLobbyMode) activeButtonBuffer.Add(challengesButton);
        if (null != optionButton) activeButtonBuffer.Add(optionButton);
        if (null != mainMenuButton) activeButtonBuffer.Add(mainMenuButton);
        if (null != exitButton) activeButtonBuffer.Add(exitButton);

        int _count = activeButtonBuffer.Count;
        allButtons = new UI_EscapeMenuButton[_count];
        buttonAppearCallbacks = new TweenCallback[_count];
        buttonAppearSoundCallbacks = new TweenCallback[_count];
        buttonDisappearCallbacks = new TweenCallback[_count];

        float _contentHeight = 0f;
        for (int i = 0; _count > i; i++)
        {
            UI_EscapeMenuButton _btn = activeButtonBuffer[i];
            allButtons[i] = _btn;
            buttonAppearCallbacks[i] = _btn.PlayAppearAnimation;
            buttonDisappearCallbacks[i] = _btn.PlayDisappearAnimation;
            buttonAppearSoundCallbacks[i] = GetButtonSoundCallback(i);
            _contentHeight += _btn.RectTransform.rect.height;

            Navigation _nav = new Navigation();
            _nav.mode = Navigation.Mode.Explicit;
            _nav.selectOnUp = activeButtonBuffer[(i - 1 + _count) % _count];
            _nav.selectOnDown = activeButtonBuffer[(i + 1) % _count];
            _btn.navigation = _nav;
        }

        if (null != buttonContainer)
        {
            // 버튼이 25px라 개수가 홀수면 위아래 끝이 .5px에 걸린다. 홀수일 때 0.5px 올려 모든 경계를 정수 픽셀에 맞춘다.
            float _offsetY = 1 == (_count & 1) ? 0.5f : 0f;
            buttonContainer.anchoredPosition = new Vector2(buttonContainer.anchoredPosition.x, _offsetY);

            // 꺼진 계층에서는 레이아웃이 계산되지 않는다. 그 경우는 켜질 때(PlayOpenProduction) 다시 맞춘다.
            if (true == gameObject.activeInHierarchy)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(buttonContainer);
            }
        }

        LayoutBGStrips(_contentHeight);

        bButtonLayoutDirty = false == gameObject.activeInHierarchy;
    }

    /// <summary>등장 효과음 콜백. 활성 순서 번호별로 한 번만 만들어 재사용한다.</summary>
    private TweenCallback GetButtonSoundCallback(int _buttonIndex)
    {
        if (0 > _buttonIndex || buttonSoundCallbackCache.Length <= _buttonIndex) return null;

        if (null == buttonSoundCallbackCache[_buttonIndex])
        {
            int _index = _buttonIndex;
            buttonSoundCallbackCache[_buttonIndex] = () => PlayButtonAppearSound(_index);
        }

        return buttonSoundCallbackCache[_buttonIndex];
    }

    /// <summary>
    /// 버튼 영역 높이에 맞춰 BG 띠 수와 위치를 정한다. 띠 높이는 BG_0 기준이다.
    /// 본편 100px = 5줄(y 40 ~ -40, 기존과 같음), 로그라이크 125px = 7줄(y 60 ~ -60). 모자라면 마지막 띠를 복제해 늘리고 남는 띠는 끈다.
    /// </summary>
    private void LayoutBGStrips(float _contentHeight)
    {
        if (0 >= bgPiecePool.Count || 0f >= bgStripHeight) return;

        int _count = Mathf.Max(1, Mathf.CeilToInt(_contentHeight / bgStripHeight));
        while (bgPiecePool.Count < _count)
        {
            AppendBGStrip();
        }

        if (bgPieces.Length != _count)
        {
            bgPieces = new EscapeMenuBGPiece[_count];
        }

        float _topY = (_count - 1) * 0.5f * bgStripHeight;
        for (int i = 0; bgPiecePool.Count > i; i++)
        {
            EscapeMenuBGPiece _piece = bgPiecePool[i];
            bool _bActive = _count > i;
            if (_piece.rectTransform.gameObject.activeSelf != _bActive)
            {
                _piece.rectTransform.gameObject.SetActive(_bActive);
            }

            if (false == _bActive) continue;

            bgPieces[i] = _piece;
            Vector2 _pos = _piece.rectTransform.anchoredPosition;
            _piece.rectTransform.anchoredPosition = new Vector2(_pos.x, _topY - i * bgStripHeight);
        }
    }

    /// <summary>마지막 BG 띠를 복제해 풀 끝에 붙인다(이름 BG_n). 한 번 만들면 계속 재사용한다.</summary>
    private void AppendBGStrip()
    {
        EscapeMenuBGPiece _source = bgPiecePool[bgPiecePool.Count - 1];
        RectTransform _rect = Instantiate(_source.rectTransform, bgRoot);
        _rect.name = "BG_" + bgPiecePool.Count;

        bgPiecePool.Add(new EscapeMenuBGPiece
        {
            rectTransform = _rect,
            graphic = _rect.GetComponent<Graphic>(),
            targetWidth = _source.targetWidth,
            targetAlpha = _source.targetAlpha
        });
    }

    public void RefreshLocalizedTexts()
    {
        if (null == localizationManager) return;

        if (null != resumeButton)
        {
            string _text = localizationManager.GetText(localizationJsonId, resumeTextId);
            if (false == string.IsNullOrEmpty(_text)) resumeButton.SetText(_text);
        }

        if (null != challengesButton)
        {
            string _text = localizationManager.GetText(challengesTextKey);
            if (false == string.IsNullOrEmpty(_text)) challengesButton.SetText(_text);
        }

        if (null != optionButton)
        {
            string _text = localizationManager.GetText(localizationJsonId, optionTextId);
            if (false == string.IsNullOrEmpty(_text)) optionButton.SetText(_text);
        }

        if (null != mainMenuButton)
        {
            string _text = localizationManager.GetText(localizationJsonId, bLobbyMode ? lobbyTextId : mainMenuTextId);
            if (false == string.IsNullOrEmpty(_text)) mainMenuButton.SetText(_text);
        }

        if (null != exitButton)
        {
            string _text = localizationManager.GetText(localizationJsonId, exitTextId);
            if (false == string.IsNullOrEmpty(_text)) exitButton.SetText(_text);
        }
    }

    public void PlayOpenProduction(Action _onComplete = null)
    {
        KillProductionSequences();

        // 로비 모드가 바뀐 뒤 처음 여는 것이면 버튼 구성 · BG 띠를 먼저 맞춘다.
        if (true == bButtonLayoutDirty)
        {
            RebuildButtonLayout();
        }

        isClosing = false;
        isButtonClicked = false;
        LockAllButtonsClick();

        if (soundsEnabled)
            Sound.PlayUI(SoundID.ResultUIOpen);

        AssignBGDelays();
        float _bgDuration = GetBGProductionDuration();

        PrepareOpenState();

        onOpenCompleteAction = _onComplete;

        openSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // BG Open Animation
        if (bgPieces.Length > 0)
        {
            SetCanvasGroupAlpha(bgCanvasGroup, 1f);
            InsertBGOpenTweens(openSequence);
        }
        else if (null != bgRoot)
        {
            openSequence.Join(DOTween.To(GetBGWidth, SetBGWidth, bgTargetWidth, bgOpenDuration).SetEase(bgEase));
            if (null != bgCanvasGroup)
                openSequence.Join(bgCanvasGroup.DOFade(1f, bgOpenDuration).SetEase(bgEase));
        }

        // Synchronize Button Appear Animations to follow BG
        float _buttonsStartTime = _bgDuration;

        if (null != buttonContainerCanvasGroup)
        {
            openSequence.Insert(_buttonsStartTime, buttonContainerCanvasGroup.DOFade(1f, buttonOpenDuration).SetEase(Ease.OutQuad));
        }

        float _maxButtonEndTime = _buttonsStartTime;

        for (int i = 0; i < allButtons.Length; i++)
        {
            UI_EscapeMenuButton _btn = allButtons[i];
            if (null == _btn) continue;

            float _btnDelay = _buttonsStartTime + (i * buttonStaggerDelay);
            float _btnEndTime = _btnDelay + _btn.AppearDuration;
            if (_btnEndTime > _maxButtonEndTime)
            {
                _maxButtonEndTime = _btnEndTime;
            }

            if (i < buttonAppearSoundCallbacks.Length && null != buttonAppearSoundCallbacks[i])
            {
                openSequence.InsertCallback(_btnDelay, buttonAppearSoundCallbacks[i]);
            }

            if (i < buttonAppearCallbacks.Length && null != buttonAppearCallbacks[i])
            {
                openSequence.InsertCallback(_btnDelay, buttonAppearCallbacks[i]);
            }
        }

        if (_maxButtonEndTime > openSequence.Duration())
        {
            openSequence.AppendInterval(_maxButtonEndTime - openSequence.Duration());
        }

        if (null != cachedOnOpenComplete)
        {
            openSequence.OnComplete(cachedOnOpenComplete);
        }
    }

    private void HandleOpenComplete()
    {
        openSequence = null;
        isMenuOpen = true;
        isButtonClicked = false;
        UnlockAllButtonsClick();

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            SelectFirstButton();
        }
        else
        {
            if (null != allButtons)
            {
                for (int i = 0; allButtons.Length > i; i++)
                {
                    UI_EscapeMenuButton _btn = allButtons[i];
                    if (null != _btn && true == _btn.gameObject.activeInHierarchy && true == _btn.IsInteractable)
                    {
                        _btn.CheckCursorHover();
                    }
                }
            }
        }

        if (null != onOpenCompleteAction)
        {
            Action _cb = onOpenCompleteAction;
            onOpenCompleteAction = null;
            _cb.Invoke();
        }
    }

    public UI_EscapeMenuButton GetFirstActiveButton()
    {
        if (null == allButtons) return null;
        for (int i = 0; allButtons.Length > i; i++)
        {
            UI_EscapeMenuButton _btn = allButtons[i];
            if (null != _btn && true == _btn.gameObject.activeInHierarchy && true == _btn.IsInteractable)
            {
                return _btn;
            }
        }
        return null;
    }

    private bool IsEscapeMenuButton(GameObject _obj)
    {
        if (null == _obj || null == allButtons) return false;
        for (int i = 0; allButtons.Length > i; i++)
        {
            if (null != allButtons[i] && allButtons[i].gameObject == _obj)
            {
                return true;
            }
        }
        return false;
    }

    public void SelectFirstButton()
    {
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        UI_EscapeMenuButton _hoveredBtn = null;
        if (null != allButtons)
        {
            for (int i = 0; allButtons.Length > i; i++)
            {
                UI_EscapeMenuButton _btn = allButtons[i];
                if (null != _btn && true == _btn.gameObject.activeInHierarchy && true == _btn.IsMouseOver())
                {
                    _hoveredBtn = _btn;
                    break;
                }
            }
        }

        UI_EscapeMenuButton _targetBtn = _hoveredBtn ?? GetFirstActiveButton();

        if (null != allButtons)
        {
            for (int i = 0; allButtons.Length > i; i++)
            {
                UI_EscapeMenuButton _btn = allButtons[i];
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

    public void SelectChallengesButton()
    {
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        if (null != allButtons)
        {
            for (int i = 0; allButtons.Length > i; i++)
            {
                UI_EscapeMenuButton _btn = allButtons[i];
                if (null != _btn && _btn != challengesButton)
                {
                    _btn.ForceUnhover();
                }
            }
        }

        if (null != challengesButton && true == challengesButton.gameObject.activeInHierarchy)
        {
            if (null != EventSystem.current)
            {
                if (EventSystem.current.currentSelectedGameObject == challengesButton.gameObject)
                {
                    challengesButton.ForceHover();
                }
                else
                {
                    EventSystem.current.SetSelectedGameObject(challengesButton.gameObject);
                }
            }
        }
    }

    public void SelectOptionButton()
    {
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        if (null != allButtons)
        {
            for (int i = 0; allButtons.Length > i; i++)
            {
                UI_EscapeMenuButton _btn = allButtons[i];
                if (null != _btn && _btn != optionButton)
                {
                    _btn.ForceUnhover();
                }
            }
        }

        if (null != optionButton && true == optionButton.gameObject.activeInHierarchy)
        {
            if (null != EventSystem.current)
            {
                if (EventSystem.current.currentSelectedGameObject == optionButton.gameObject)
                {
                    optionButton.ForceHover();
                }
                else
                {
                    EventSystem.current.SetSelectedGameObject(optionButton.gameObject);
                }
            }
        }
    }

    private void OnDeviceChanged(EInputDeviceType _device)
    {
        if (false == gameObject.activeInHierarchy || false == isMenuOpen || true == isClosing) return;
        if (null != parentESCView && true == parentESCView.IsOptionOpen) return;
        if (null != warningPopup && true == warningPopup.IsActive) return;

        if (EInputDeviceType.Gamepad == _device)
        {
            UI_EscapeMenuButton _hoveredBtn = null;
            if (null != allButtons)
            {
                for (int i = 0; allButtons.Length > i; i++)
                {
                    UI_EscapeMenuButton _btn = allButtons[i];
                    if (null != _btn && true == _btn.gameObject.activeInHierarchy && true == _btn.IsMouseOver())
                    {
                        _hoveredBtn = _btn;
                        break;
                    }
                }
            }

            UI_EscapeMenuButton _targetBtn = _hoveredBtn;
            if (null != _hoveredBtn)
            {
                MoveDirection _dir = GetTriggeringMoveDirection();
                if (MoveDirection.Down == _dir && null != _hoveredBtn.navigation.selectOnDown && _hoveredBtn.navigation.selectOnDown is UI_EscapeMenuButton _downBtn && true == _downBtn.gameObject.activeInHierarchy)
                {
                    _targetBtn = _downBtn;
                }
                else if (MoveDirection.Up == _dir && null != _hoveredBtn.navigation.selectOnUp && _hoveredBtn.navigation.selectOnUp is UI_EscapeMenuButton _upBtn && true == _upBtn.gameObject.activeInHierarchy)
                {
                    _targetBtn = _upBtn;
                }
            }
            else
            {
                _targetBtn = GetFirstActiveButton();
            }

            if (null != allButtons)
            {
                for (int i = 0; allButtons.Length > i; i++)
                {
                    UI_EscapeMenuButton _btn = allButtons[i];
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

            if (null != allButtons)
            {
                for (int i = 0; allButtons.Length > i; i++)
                {
                    UI_EscapeMenuButton _btn = allButtons[i];
                    if (null != _btn && true == _btn.gameObject.activeInHierarchy)
                    {
                        if (true == _btn.IsMouseOver())
                        {
                            _btn.ForceHover();
                        }
                        else
                        {
                            _btn.ForceUnhover();
                        }
                    }
                }
            }
        }
    }

    private void Update()
    {
        if (false == gameObject.activeInHierarchy || false == isMenuOpen || true == isClosing) return;
        if (null != parentESCView && true == parentESCView.IsOptionOpen) return;
        if (null == inputManager || false == inputManager.IsGamepadMode) return;
        if (null != warningPopup && true == warningPopup.IsActive) return;

        if (null != EventSystem.current)
        {
            GameObject _selected = EventSystem.current.currentSelectedGameObject;
            if (null == _selected || false == _selected.activeInHierarchy)
            {
                UI_EscapeMenuButton _firstBtn = GetFirstActiveButton();
                if (null != _firstBtn)
                {
                    EventSystem.current.SetSelectedGameObject(_firstBtn.gameObject);
                    _firstBtn.ForceHover();
                }
            }
        }
    }

    /// <summary>
    /// 버튼과 백그라운드를 되감기(Rewind)하듯이 역순으로 축소/퇴장시키는 역모션 연출을 재생합니다.
    /// </summary>
    public void PlayCloseProduction(Action _onComplete)
    {
        KillProductionSequences();
        isMenuOpen = false;
        isClosing = true;
        SetButtonsInteractable(false);

        if (null != warningPopup && true == warningPopup.IsActive)
        {
            warningPopup.Hide();
        }

        if (soundsEnabled)
            Sound.PlayUI(SoundID.ResultUIClose);

        onCloseCompleteAction = _onComplete;

        closeSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // 1단계: 버튼 역순 퇴장 (하단 -> 상단)
        float _buttonCloseTime = 0.15f;
        for (int i = 0; i < allButtons.Length; i++)
        {
            int _btnIndex = (allButtons.Length - 1) - i;
            UI_EscapeMenuButton _btn = allButtons[_btnIndex];
            if (null == _btn) continue;

            float _btnDelay = i * buttonStaggerDelay;
            _buttonCloseTime = _btn.DisappearDuration;

            if (_btnIndex < buttonDisappearCallbacks.Length && null != buttonDisappearCallbacks[_btnIndex])
            {
                closeSequence.InsertCallback(_btnDelay, buttonDisappearCallbacks[_btnIndex]);
            }
        }

        float _bgStartTime = allButtons.Length > 0
            ? ((allButtons.Length - 1) * buttonStaggerDelay + _buttonCloseTime)
            : 0f;

        // 2단계: 백그라운드 역순 축소 (외곽 -> 중앙)
        if (bgPieces.Length > 0)
        {
            AssignBGCloseDelays();
            InsertBGCloseTweens(closeSequence, _bgStartTime);
        }
        else if (null != bgRoot)
        {
            closeSequence.Insert(_bgStartTime, DOTween.To(GetBGWidth, SetBGWidth, HiddenBGWidth, bgOpenDuration).SetEase(bgCloseEase));
            if (null != bgCanvasGroup)
            {
                closeSequence.Insert(_bgStartTime, bgCanvasGroup.DOFade(0f, bgOpenDuration).SetEase(bgCloseEase));
            }
        }

        if (null != menuCanvasGroup)
        {
            float _totalBGCloseDuration = GetBGProductionDuration();
            closeSequence.Insert(_bgStartTime + _totalBGCloseDuration * 0.8f, menuCanvasGroup.DOFade(0f, _totalBGCloseDuration * 0.2f));
        }

        if (null != cachedOnCloseComplete)
        {
            closeSequence.OnComplete(cachedOnCloseComplete);
        }
    }

    private void HandleCloseComplete()
    {
        closeSequence = null;
        isClosing = false;
        isMenuOpen = false;
        if (null != onCloseCompleteAction)
        {
            Action _cb = onCloseCompleteAction;
            onCloseCompleteAction = null;
            _cb.Invoke();
        }
    }

    private void CacheCanvasGroups()
    {
        menuCanvasGroup = GetComponent<CanvasGroup>();
        if (null == menuCanvasGroup)
            menuCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (null != bgRoot)
        {
            bgCanvasGroup = bgRoot.GetComponent<CanvasGroup>();
            if (null == bgCanvasGroup)
                bgCanvasGroup = bgRoot.gameObject.AddComponent<CanvasGroup>();
        }

        if (null != buttonContainer)
        {
            buttonContainerCanvasGroup = buttonContainer.GetComponent<CanvasGroup>();
            if (null == buttonContainerCanvasGroup)
                buttonContainerCanvasGroup = buttonContainer.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void CacheBGPieces()
    {
        bgPieces = Array.Empty<EscapeMenuBGPiece>();
        bgPiecePool.Clear();
        if (null == bgRoot) return;

        RectTransform[] _rects = bgRoot.GetComponentsInChildren<RectTransform>(true);
        List<EscapeMenuBGPiece> _list = bgPiecePool;

        for (int i = 0; i < _rects.Length; i++)
        {
            RectTransform _rect = _rects[i];
            if (bgRoot == _rect || false == _rect.name.StartsWith("BG_", StringComparison.Ordinal))
                continue;

            Graphic _graphic = _rect.GetComponent<Graphic>();
            if (null == _graphic) continue;

            _list.Add(new EscapeMenuBGPiece
            {
                rectTransform = _rect,
                graphic = _graphic,
                targetWidth = _rect.rect.width > 0f ? _rect.rect.width : bgTargetWidth,
                targetAlpha = DefaultBGTargetAlpha
            });
        }

        // Y 좌표 내림차순 (상단 -> 하단) 정렬, 동일 Y일 경우 이름순 정렬
        _list.Sort((left, right) =>
        {
            if (null == left?.rectTransform || null == right?.rectTransform) return 0;
            int _yComp = right.rectTransform.anchoredPosition.y.CompareTo(left.rectTransform.anchoredPosition.y);
            if (0 != _yComp) return _yComp;
            return string.CompareOrdinal(left.rectTransform.name, right.rectTransform.name);
        });

        bgPieces = _list.ToArray();

        // 띠 높이는 BG_0 기준이다(LayoutBGStrips가 버튼 영역 높이에 맞는 띠 수를 정할 때 쓴다).
        if (bgPieces.Length > 0)
        {
            RectTransform _firstRect = bgPieces[0].rectTransform;
            bgStripHeight = _firstRect.rect.height > 0f ? _firstRect.rect.height : _firstRect.sizeDelta.y;
        }

        if (bgPieces.Length > 0 && bgTargetWidth <= 0f)
        {
            bgTargetWidth = bgPieces[0].targetWidth;
        }
        else if (null != bgRoot && bgTargetWidth <= 0f)
        {
            bgTargetWidth = bgRoot.rect.width;
        }
    }

    public void SetSoundsEnabled(bool _enabled)
    {
        soundsEnabled = _enabled;
    }

    private void PlayButtonAppearSound(int _buttonIndex)
    {
        if (false == soundsEnabled) return;

        switch (_buttonIndex)
        {
            case 0: Sound.PlayUI(SoundID.MainMenuDot00); break;
            case 1: Sound.PlayUI(SoundID.MainMenuDot01); break;
            case 2: Sound.PlayUI(SoundID.MainMenuDot02); break;
            case 3: Sound.PlayUI(SoundID.MainMenuDot03); break;
            case 4: Sound.PlayUI(SoundID.MainMenuDot04); break;
        }
    }

    private void PrepareOpenState()
    {
        SetCanvasGroupAlpha(menuCanvasGroup, 1f);

        if (bgPieces.Length > 0)
        {
            for (int i = 0; i < bgPieces.Length; i++)
            {
                SetBGPieceWidth(bgPieces[i], HiddenBGWidth);
                SetGraphicAlpha(bgPieces[i].graphic, 0f);
            }
        }
        else if (null != bgRoot)
        {
            SetBGWidth(HiddenBGWidth);
        }

        SetCanvasGroupAlpha(bgCanvasGroup, 0f);
        SetCanvasGroupAlpha(buttonContainerCanvasGroup, 1f);

        for (int i = 0; i < allButtons.Length; i++)
        {
            if (null == allButtons[i]) continue;

            allButtons[i].PrepareAppearState();
        }
    }

    private void InsertBGOpenTweens(Sequence _seq)
    {
        if (bgPieces.Length > 0)
        {
            _seq.Insert(0f, DOTween.To(
                GetBGPiecesAlpha,
                SetBGPiecesAlpha,
                1f,
                bgOpenDuration).SetEase(Ease.Linear));
        }

        for (int i = 0; i < bgPieces.Length; i++)
        {
            EscapeMenuBGPiece _piece = bgPieces[i];
            float _width = bgTargetWidth > 0f ? bgTargetWidth : _piece.targetWidth;

            _seq.Insert(_piece.delay, DOTween.To(
                () => GetBGPieceWidth(_piece),
                w => SetBGPieceWidth(_piece, w),
                _width,
                bgOpenDuration).SetEase(bgEase));
        }
    }

    private void InsertBGCloseTweens(Sequence _seq, float _startOffset)
    {
        if (bgPieces.Length > 0)
        {
            _seq.Insert(_startOffset, DOTween.To(
                GetBGPiecesAlpha,
                SetBGPiecesAlpha,
                0f,
                bgOpenDuration).SetEase(Ease.Linear));
        }

        for (int i = 0; i < bgPieces.Length; i++)
        {
            EscapeMenuBGPiece _piece = bgPieces[i];
            float _pieceTime = _startOffset + _piece.delay;

            _seq.Insert(_pieceTime, DOTween.To(
                () => GetBGPieceWidth(_piece),
                w => SetBGPieceWidth(_piece, w),
                HiddenBGWidth,
                bgOpenDuration).SetEase(bgCloseEase));
        }
    }

    private void AssignBGDelays()
    {
        if (bgPieces.Length <= 0) return;

        switch (bgDelayMode)
        {
            case BGDelayMode.CenterOutward:
                float _mid = (bgPieces.Length - 1) * 0.5f;
                for (int i = 0; i < bgPieces.Length; i++)
                {
                    int _distFromCenter = Mathf.FloorToInt(Mathf.Abs(i - _mid));
                    bgPieces[i].delay = _distFromCenter * bgPieceOpenDelay;
                }
                break;

            case BGDelayMode.Random:
                AssignRandomBGDelays();
                break;

            case BGDelayMode.TopToBottom:
                for (int i = 0; i < bgPieces.Length; i++)
                {
                    bgPieces[i].delay = i * bgPieceOpenDelay;
                }
                break;

            case BGDelayMode.BottomToTop:
                for (int i = 0; i < bgPieces.Length; i++)
                {
                    bgPieces[i].delay = (bgPieces.Length - 1 - i) * bgPieceOpenDelay;
                }
                break;
        }
    }

    private void AssignBGCloseDelays()
    {
        if (bgPieces.Length <= 0) return;

        switch (bgDelayMode)
        {
            case BGDelayMode.CenterOutward:
                float _mid = (bgPieces.Length - 1) * 0.5f;
                int _maxDist = 0;
                for (int i = 0; i < bgPieces.Length; i++)
                {
                    int _dist = Mathf.FloorToInt(Mathf.Abs(i - _mid));
                    _maxDist = Mathf.Max(_maxDist, _dist);
                }
                for (int i = 0; i < bgPieces.Length; i++)
                {
                    int _distFromCenter = Mathf.FloorToInt(Mathf.Abs(i - _mid));
                    bgPieces[i].delay = (_maxDist - _distFromCenter) * bgPieceOpenDelay;
                }
                break;

            case BGDelayMode.Random:
                AssignRandomBGDelays();
                break;

            case BGDelayMode.TopToBottom:
                for (int i = 0; i < bgPieces.Length; i++)
                {
                    bgPieces[i].delay = (bgPieces.Length - 1 - i) * bgPieceOpenDelay;
                }
                break;

            case BGDelayMode.BottomToTop:
                for (int i = 0; i < bgPieces.Length; i++)
                {
                    bgPieces[i].delay = i * bgPieceOpenDelay;
                }
                break;
        }
    }

    private void AssignRandomBGDelays()
    {
        if (bgPieces.Length <= 0) return;

        float[] _delays = new float[bgPieces.Length];
        for (int i = 0; i < _delays.Length; i++)
            _delays[i] = i * bgPieceOpenDelay;

        for (int i = _delays.Length - 1; i > 0; i--)
        {
            int _rand = UnityEngine.Random.Range(0, i + 1);
            float _tmp = _delays[i];
            _delays[i] = _delays[_rand];
            _delays[_rand] = _tmp;
        }

        for (int i = 0; i < bgPieces.Length; i++)
            bgPieces[i].delay = _delays[i];
    }

    private float GetBGProductionDuration()
    {
        if (bgPieces.Length <= 0) return bgOpenDuration;

        float _maxDelay = 0f;
        for (int i = 0; i < bgPieces.Length; i++)
            _maxDelay = Mathf.Max(_maxDelay, bgPieces[i].delay);

        return bgOpenDuration + _maxDelay;
    }

    private float GetBGPieceWidth(EscapeMenuBGPiece _piece)
    {
        return null != _piece?.rectTransform ? _piece.rectTransform.rect.width : 0f;
    }

    private void SetBGPieceWidth(EscapeMenuBGPiece _piece, float _width)
    {
        if (null != _piece?.rectTransform)
        {
            _piece.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _width);
        }
    }

    private float GetBGPiecesAlpha()
    {
        if (bgPieces.Length <= 0 || null == bgPieces[0].graphic) return 0f;
        float _targetAlpha = Mathf.Max(bgPieces[0].targetAlpha, 0.0001f);
        return Mathf.Clamp01(bgPieces[0].graphic.color.a / _targetAlpha);
    }

    private void SetBGPiecesAlpha(float _ratio)
    {
        for (int i = 0; i < bgPieces.Length; i++)
        {
            SetGraphicAlpha(bgPieces[i].graphic, bgPieces[i].targetAlpha * _ratio);
        }
    }

    private void SetGraphicAlpha(Graphic _graphic, float _alpha)
    {
        if (null == _graphic) return;
        Color _c = _graphic.color;
        _c.a = _alpha;
        _graphic.color = _c;
    }

    private float GetBGWidth()
    {
        return null != bgRoot ? bgRoot.rect.width : 0f;
    }

    private void SetBGWidth(float _width)
    {
        if (null != bgRoot)
        {
            bgRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _width);
        }
    }

    private void SetCanvasGroupAlpha(CanvasGroup _cg, float _alpha)
    {
        if (null != _cg) _cg.alpha = _alpha;
    }

    private void SetButtonsInteractable(bool _interactable)
    {
        for (int i = 0; allButtons.Length > i; i++)
        {
            if (null != allButtons[i])
            {
                allButtons[i].SetInteractable(_interactable);
            }
        }
    }

    private void LockAllButtonsClick()
    {
        for (int i = 0; allButtons.Length > i; i++)
        {
            if (null != allButtons[i])
            {
                allButtons[i].SetClickLocked(true);
            }
        }
    }

    private void UnlockAllButtonsClick()
    {
        for (int i = 0; allButtons.Length > i; i++)
        {
            if (null != allButtons[i])
            {
                allButtons[i].SetClickLocked(false);
            }
        }
    }

    // 진행 중인 연출을 교체하기 위해 죽인다.
    //
    // 주의: DOTween의 Kill()은 OnComplete를 발동시키지 않으므로, 여기서 죽는 시퀀스의 완료 콜백
    // (HandleOpenComplete/HandleCloseComplete를 거쳐 호출될 onOpenCompleteAction/onCloseCompleteAction)은
    // "영영 호출되지 않는다". 연출이 실제로 끝나지 않았으므로 여기서 대신 호출해 주는 것도 옳지 않다
    // - 예컨대 닫기 완료 콜백(OnCloseProductionFinished)은 GameObject를 비활성화하는데, 다시 여는
    // 도중에 그것을 실행하면 방금 연 창이 꺼져 버린다.
    //
    // 따라서 이 콜백들에는 "반드시 실행되어야 하는 뒷정리"를 담으면 안 된다. 입력 잠금 해제가 바로
    // 그런 뒷정리였고, 그래서 UIView_ESC가 완료 콜백이 아니라 소유 플래그(bInputLockHeld)로 직접
    // 관리하도록 옮겼다. 아래에서 참조를 비우는 것은 죽은 콜백이 나중에 엉뚱한 순서로 살아나지
    // 못하게 하기 위해서다.
    private void KillProductionSequences()
    {
        if (null != openSequence && true == openSequence.IsActive())
        {
            openSequence.Kill();
            openSequence = null;
            onOpenCompleteAction = null;
        }

        if (null != closeSequence && true == closeSequence.IsActive())
        {
            closeSequence.Kill();
            closeSequence = null;
            onCloseCompleteAction = null;
        }
    }

    private void OnResumeButtonClicked()
    {
        if (false == isMenuOpen || true == isClosing || true == isButtonClicked) return;
        isButtonClicked = true;
        LockAllButtonsClick();
        if (null != onResumeCallback) onResumeCallback.Invoke();
    }

    private void OnChallengesButtonClicked()
    {
        if (false == isMenuOpen || true == isClosing || true == isButtonClicked) return;
        isButtonClicked = true;
        LockAllButtonsClick();
        if (null != onChallengesCallback) onChallengesCallback.Invoke();
    }

    private void OnOptionButtonClicked()
    {
        if (false == isMenuOpen || true == isClosing || true == isButtonClicked) return;
        isButtonClicked = true;
        LockAllButtonsClick();
        if (null != onOptionCallback) onOptionCallback.Invoke();
    }

    private void OnMainMenuButtonClicked()
    {
        if (false == isMenuOpen || true == isClosing || true == isButtonClicked) return;
        isButtonClicked = true;
        LockAllButtonsClick();

        if (null != warningPopup && null != localizationManager)
        {
            PlayCloseProduction(null);
            string _warningMsg = localizationManager.GetText(bLobbyMode ? lobbyWarningKey : mainMenuWarningKey);
            warningPopup.ShowWarning(
                _warningMsg,
                cachedConfirmMainMenu,
                cachedCancelWarningAndRestoreMenu,
                SoundID.ResultUIOpen,
                SoundID.ResultUIClose,
                SoundID.ResultUIHover);
        }
        else
        {
            ConfirmMainMenu();
        }
    }

    private void OnExitButtonClicked()
    {
        if (false == isMenuOpen || true == isClosing || true == isButtonClicked) return;
        isButtonClicked = true;
        LockAllButtonsClick();

        if (null != warningPopup && null != localizationManager)
        {
            PlayCloseProduction(null);
            string _warningMsg = localizationManager.GetText(exitWarningKey);
            warningPopup.ShowWarning(
                _warningMsg,
                cachedConfirmExit,
                cachedCancelWarningAndRestoreMenu,
                SoundID.ResultUIOpen,
                SoundID.ResultUIClose,
                SoundID.ResultUIHover);
        }
        else
        {
            ConfirmExit();
        }
    }

    private void CancelWarningAndRestoreMenu()
    {
        isButtonClicked = false;
        UnlockAllButtonsClick();
        PlayOpenProduction();
    }

    private void ConfirmMainMenu()
    {
        isMenuOpen = false;
        isClosing = true;
        if (null != onMainMenuCallback)
        {
            onMainMenuCallback.Invoke();
        }
    }

    private void ConfirmExit()
    {
        isMenuOpen = false;
        isClosing = true;
        if (null != onExitCallback)
        {
            onExitCallback.Invoke();
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
