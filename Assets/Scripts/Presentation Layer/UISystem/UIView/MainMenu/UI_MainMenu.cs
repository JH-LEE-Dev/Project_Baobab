using UnityEngine;

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
    
    // 캐싱 델리게이트
    private System.Action cachedExecuteNewGame;
    private System.Action cachedCancelNewGame;
    private System.Action cachedOnNewGameDisappearComplete;
    private System.Action cachedSetLocalization;

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
        
        if (null != viewCtx && null != viewCtx.localizationManager)
        {
            viewCtx.localizationManager.OnLanguageChanged -= cachedSetLocalization;
            viewCtx.localizationManager.OnLanguageChanged += cachedSetLocalization;
        }
        
        if (null != newGameButton)
        {
            newGameButton.Initialize(OnNewGameClicked);
        }
        
        if (null != loadGameButton)
        {
            loadGameButton.Initialize(OnLoadGameClicked);
            // 이곳에서의 초기 판단은 saveSystem 주입 전일 수 있으므로 제거하거나 둡니다. (안전하게 의존성 주입 후 다시 업데이트함)
        }
        
        if (null != exitButton)
        {
            exitButton.Initialize(OnExitClicked);
        }
        
        if (null != optionButton)
        {
            optionButton.Initialize(OnOptionClicked);
        }
        
        if (null != creditButton)
        {
            creditButton.Initialize(OnCreditClicked);
        }

        SetLocalization();
    }

    private UI_MainMenuButton[] buttonsInOrder;

    public void UpdateLoadGameButtonState()
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

        if (null != parentView)
        {
            bool _hasSaveData = parentView.HasSaveData();
            
            if (null != loadGameButton)
            {
                // 세이브 데이터가 없으면 버튼 자체를 비활성화(숨김) 처리하고, 있으면 상호작용 가능 상태로 만듦
                loadGameButton.gameObject.SetActive(_hasSaveData);
                loadGameButton.SetInteractable(_hasSaveData);
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

        for (int i = 0; i < buttonsInOrder.Length; i++)
        {
            UI_MainMenuButton _btn = buttonsInOrder[i];
            
            if (null != _btn && _btn.gameObject.activeSelf)
            {
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
    }

    /// <summary>
    /// 게임 씬에서 다시 돌아왔을 때, 이전에 꺼진 버튼들을 다시 활성화하고 등장 연출을 재생합니다.
    /// </summary>
    public void ResetAndShowButtons()
    {
        if (null == buttonsInOrder) return;

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
                if (_btn == loadGameButton && false == _hasSaveData)
                {
                    _btn.gameObject.SetActive(false);
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
    }

    public void SetLocalization()
    {
        if (null == viewCtx || null == viewCtx.localizationManager)
            return;

        if (null != newGameButton)
        {
            newGameButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 1));
        }
        
        if (null != loadGameButton)
        {
            loadGameButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 2));
        }
        
        if (null != optionButton)
        {
            optionButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 3));
        }
        
        if (null != creditButton)
        {
            creditButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 4));
        }

        if (null != exitButton)
        {
            exitButton.SetText(viewCtx.localizationManager.GetText(mainMenuUIJsonId, 5));
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
}
