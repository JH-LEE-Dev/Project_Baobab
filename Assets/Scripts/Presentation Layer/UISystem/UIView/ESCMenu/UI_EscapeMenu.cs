using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

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

    private LocalizationManager localizationManager;
    private Action cachedRefreshLocalizedTexts;

    private Action onResumeCallback;
    private Action onOptionCallback;
    private Action onMainMenuCallback;
    private Action onExitCallback;

    private Sequence openSequence;
    private Sequence closeSequence;
    private bool isClosing = false;

    private CanvasGroup menuCanvasGroup;
    private CanvasGroup bgCanvasGroup;
    private CanvasGroup buttonContainerCanvasGroup;
    private EscapeMenuBGPiece[] bgPieces = Array.Empty<EscapeMenuBGPiece>();

    private UI_EscapeMenuButton[] allButtons = Array.Empty<UI_EscapeMenuButton>();
    private TweenCallback[] buttonAppearCallbacks = Array.Empty<TweenCallback>();
    private TweenCallback[] buttonDisappearCallbacks = Array.Empty<TweenCallback>();

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
        CacheCanvasGroups();
        CacheBGPieces();
        CacheButtons();
    }

    private void OnDestroy()
    {
        KillProductionSequences();

        if (null != localizationManager && null != cachedRefreshLocalizedTexts)
        {
            localizationManager.OnLanguageChanged -= cachedRefreshLocalizedTexts;
        }

        onResumeCallback = null;
        onOptionCallback = null;
        onMainMenuCallback = null;
        onExitCallback = null;
    }

    public void Initialize(LocalizationManager _localizationManager, Action _onResume, Action _onOption, Action _onMainMenu, Action _onExit)
    {
        localizationManager = _localizationManager;
        onResumeCallback = _onResume;
        onOptionCallback = _onOption;
        onMainMenuCallback = _onMainMenu;
        onExitCallback = _onExit;

        if (null != resumeButton) resumeButton.Initialize(OnResumeButtonClicked);
        if (null != optionButton) optionButton.Initialize(OnOptionButtonClicked);
        if (null != mainMenuButton) mainMenuButton.Initialize(OnMainMenuButtonClicked);
        if (null != exitButton) exitButton.Initialize(OnExitButtonClicked);

        if (null != localizationManager)
        {
            if (null == cachedRefreshLocalizedTexts)
                cachedRefreshLocalizedTexts = RefreshLocalizedTexts;

            localizationManager.OnLanguageChanged -= cachedRefreshLocalizedTexts;
            localizationManager.OnLanguageChanged += cachedRefreshLocalizedTexts;

            RefreshLocalizedTexts();
        }
    }

    public void RefreshLocalizedTexts()
    {
        if (null == localizationManager) return;

        if (null != resumeButton)
        {
            string _text = localizationManager.GetText(localizationJsonId, resumeTextId);
            if (false == string.IsNullOrEmpty(_text)) resumeButton.SetText(_text);
        }

        if (null != optionButton)
        {
            string _text = localizationManager.GetText(localizationJsonId, optionTextId);
            if (false == string.IsNullOrEmpty(_text)) optionButton.SetText(_text);
        }

        if (null != mainMenuButton)
        {
            string _text = localizationManager.GetText(localizationJsonId, mainMenuTextId);
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
        isClosing = false;

        AssignBGDelays();
        float _bgDuration = GetBGProductionDuration();

        PrepareOpenState();

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

            if (i < buttonAppearCallbacks.Length && null != buttonAppearCallbacks[i])
            {
                openSequence.InsertCallback(_btnDelay, buttonAppearCallbacks[i]);
            }
        }

        if (_maxButtonEndTime > 0f)
        {
            openSequence.Insert(_maxButtonEndTime, DOTween.To(() => 0f, _ => { }, 0f, 0f));
        }

        openSequence.OnComplete(() =>
        {
            openSequence = null;
            if (null != _onComplete)
            {
                _onComplete.Invoke();
            }
        });
    }

    /// <summary>
    /// 버튼과 백그라운드를 되감기(Rewind)하듯이 역순으로 축소/퇴장시키는 역모션 연출을 재생합니다.
    /// </summary>
    public void PlayCloseProduction(Action _onComplete)
    {
        KillProductionSequences();
        isClosing = true;
        SetButtonsInteractable(false);

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

        closeSequence.OnComplete(() =>
        {
            closeSequence = null;
            isClosing = false;
            if (null != _onComplete)
            {
                _onComplete.Invoke();
            }
        });
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
        if (null == bgRoot) return;

        RectTransform[] _rects = bgRoot.GetComponentsInChildren<RectTransform>(true);
        List<EscapeMenuBGPiece> _list = new List<EscapeMenuBGPiece>();

        for (int i = 0; i < _rects.Length; i++)
        {
            RectTransform _rect = _rects[i];
            if (_rect == bgRoot || false == _rect.name.StartsWith("BG_", StringComparison.Ordinal))
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

        if (bgPieces.Length > 0 && bgTargetWidth <= 0f)
        {
            bgTargetWidth = bgPieces[0].targetWidth;
        }
        else if (null != bgRoot && bgTargetWidth <= 0f)
        {
            bgTargetWidth = bgRoot.rect.width;
        }
    }

    private void CacheButtons()
    {
        allButtons = new[] { resumeButton, optionButton, mainMenuButton, exitButton };
        buttonAppearCallbacks = new TweenCallback[allButtons.Length];
        buttonDisappearCallbacks = new TweenCallback[allButtons.Length];

        for (int i = 0; i < allButtons.Length; i++)
        {
            if (null != allButtons[i])
            {
                buttonAppearCallbacks[i] = allButtons[i].PlayAppearAnimation;
                buttonDisappearCallbacks[i] = allButtons[i].PlayDisappearAnimation;
            }
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
        for (int i = 0; i < allButtons.Length; i++)
        {
            if (null != allButtons[i])
            {
                allButtons[i].SetInteractable(_interactable);
            }
        }
    }

    private void KillProductionSequences()
    {
        if (null != openSequence && true == openSequence.IsActive())
        {
            openSequence.Kill();
            openSequence = null;
        }

        if (null != closeSequence && true == closeSequence.IsActive())
        {
            closeSequence.Kill();
            closeSequence = null;
        }
    }

    private void OnResumeButtonClicked()
    {
        if (true == isClosing) return;
        if (null != onResumeCallback) onResumeCallback.Invoke();
    }

    private void OnOptionButtonClicked()
    {
        if (true == isClosing) return;
        if (null != onOptionCallback) onOptionCallback.Invoke();
    }

    private void OnMainMenuButtonClicked()
    {
        if (true == isClosing) return;
        if (null != onMainMenuCallback) onMainMenuCallback.Invoke();
    }

    private void OnExitButtonClicked()
    {
        if (true == isClosing) return;
        if (null != onExitCallback) onExitCallback.Invoke();
    }
}
