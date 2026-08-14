using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using PresentationLayer.UISystem;

/// <summary>
/// 튜토리얼 단계별 퀘스트 목표와 가이드를 EscapeMenu 스타일의 분리선 DimBG 및 텍스트 연출로 표시하는 UI 컴포넌트입니다.
/// </summary>
public class UI_TutorialQuest : MonoBehaviour
{
    // 외부 의존성
    [Header("UI References")]
    [SerializeField] private RectTransform bgRoot;
    [SerializeField] private CanvasGroup bgCanvasGroup;
    [SerializeField] private CanvasGroup contentCanvasGroup;
    [SerializeField] private RectTransform textContainer;
    [SerializeField] private TextMeshProUGUI questTitleText;
    [SerializeField] private GameObject questDescRoot;
    [SerializeField] private TextMeshProUGUI questDescText;

    [Header("Background Dim Settings")]
    [SerializeField] private bool useBgFadeIn = true;
    [SerializeField] private bool useBgFadeOut = true;
    [SerializeField] private float bgTargetWidth = 900f;
    [SerializeField] private float bgOpenDuration = 0.35f;
    [SerializeField] private float bgCloseDuration = 0.25f;
    [SerializeField] private Ease bgOpenEase = Ease.OutQuart;
    [SerializeField] private Ease bgCloseEase = Ease.InQuart;
    [SerializeField] private float bgPieceStaggerDelay = 0.04f;

    [Header("Quest Colors")]
    [SerializeField] private Color inProgressColor = Color.white;
    [SerializeField] private Color completedColor = new Color(0.49f, 0.93f, 0.75f, 1f); // #7EEDBE
    [SerializeField] private float colorTransitionDuration = 0.3f;
    [SerializeField] private Ease colorTransitionEase = Ease.OutQuad;

    [Header("Text Scale Animation")]
    [SerializeField] private Vector3 startScale = new Vector3(1.25f, 1.25f, 1f);
    [SerializeField] private Vector3 targetScale = Vector3.one;
    [SerializeField] private float scaleDuration = 0.3f;
    [SerializeField] private Ease scaleEase = Ease.OutBack;

    [Header("Completed Transition Timing")]
    [SerializeField] private float completedHoldDuration = 1.0f;

    [Header("Completed Text Pop")]
    [SerializeField] private float completedTextPopDuration = 0.3f;
    [SerializeField] private Vector3 completedTextSquashScale = new Vector3(1.12f, 0.88f, 1.0f);
    [SerializeField] private Vector3 completedTextStretchScale = new Vector3(0.96f, 1.08f, 1.0f);



    // 내부 의존성
    private const float HiddenBGWidth = 0f;
    private const float DefaultBGTargetAlpha = 0.95f;

    private LocalizationManager localizationManager;
    private Action cachedRefreshLocalizedTexts;

    private TutorialQuestBGPiece[] bgPieces = Array.Empty<TutorialQuestBGPiece>();
    private int activeBGCount = 1;

    private Sequence showSequence;
    private Sequence hideSequence;
    private Sequence stepTransitionSequence;
    private Sequence completedSequence;

    private string pendingNextTitle;
    private string pendingNextDesc;

    private TweenCallback cachedOnShowComplete;
    private TweenCallback cachedOnHideComplete;
    private TweenCallback cachedOnTransitionComplete;
    private TweenCallback cachedOnCompletedHide;
    private TweenCallback cachedOnStepMidpoint;
    private TweenCallback cachedOnTransitionBGCollapse;
    private TweenCallback cachedPlayQuestTextAppearSounds;
    private TweenCallback cachedOnCompleteHideCallback;

    private TutorialStep currentStep;
    private bool bIsShowing = false;
    public event Action<TutorialStep> HideCompletedEvent;
    public event Action<TutorialStep> StepTransitionCompletedEvent;

    private sealed class TutorialQuestBGPiece
    {
        public RectTransform rectTransform;
        public Graphic graphic;
        public float targetWidth;
        public float targetAlpha;
        public float delay;
    }

    // 퍼블릭 초기화 및 제어 메서드
    public void Initialize(LocalizationManager _localizationManager)
    {
        InitCachedCallbacks();
        localizationManager = _localizationManager;

        CacheCanvasGroups();
        CacheBGPieces();

        if (null != localizationManager)
        {
            if (null == cachedRefreshLocalizedTexts)
                cachedRefreshLocalizedTexts = RefreshLocalizedTexts;

            localizationManager.OnLanguageChanged -= cachedRefreshLocalizedTexts;
            localizationManager.OnLanguageChanged += cachedRefreshLocalizedTexts;
        }

        ResetQuest();
    }

    public void OnTutorialStepStarted(TutorialStep _step)
    {
        if (_step == currentStep && true == bIsShowing)
            return;

        // 이전 퀘스트의 "완료 색상 전환 → 유지 → 숨김" 연출(PlayCompleteAndHide)이 아직 끝나지 않은 상태로
        // 다음 퀘스트가 시작될 수 있다(예: 피로도가 바닥값에 빠르게 도달해 GoHomeBeforeExhausted가
        // FillOffroadContainer의 숨김 연출보다 먼저 시작하는 경우). 이때 아래 PlayStepTransition()이
        // KillSequences()로 진행 중이던 연출을 죽여버리면 HideCompletedEvent가 영영 발행되지 않아,
        // 이 이벤트에 걸려있는 게임 로직(예: 차량 상호작용 잠금 해제)이 씹힌다.
        // 따라서 다음 퀘스트로 넘어가기 직전에 미완료 숨김 연출을 강제로 즉시 완료 처리해 이벤트가
        // 반드시 발행되도록 보장한다.
        ForceCompletePendingHide();

        currentStep = _step;

        currentStep = _step;

        GetQuestTitleAndDesc(_step, out string _title, out string _desc);

        if (true == bIsShowing)
        {
            PlayStepTransition(_title, _desc);
        }
        else
        {
            SetQuestContent(_title, _desc);
            PlayShowQuest();
        }
    }

    public void OnTutorialStepCompleted(TutorialStep _step)
    {
        switch (_step)
        {
            case TutorialStep.CutTree:
            case TutorialStep.FillOffroadContainer:
            case TutorialStep.GoHomeBeforeExhausted:
            case TutorialStep.PutItemsInLogContainer:
            case TutorialStep.ReceiveMoney:
            case TutorialStep.UpgradeAxe:
            case TutorialStep.StartNewLogging:
                if (true == bIsShowing)
                {
                    PlayCompleteAndHide();
                }
                break;
        }
    }

    public void ResetQuest()
    {
        KillSequences();

        bIsShowing = false;
        PrepareHiddenState();
    }

    public void RefreshLocalizedTexts()
    {
        if (false == bIsShowing)
            return;

        GetQuestTitleAndDesc(currentStep, out string _title, out string _desc);
        SetQuestContent(_title, _desc);
    }

    private void PlayShowQuest()
    {
        KillSequences();
        bIsShowing = true;

        AssignBGDelays();
        PrepareShowState();

        showSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // 1. DimBG 확장 연출
        if (0 < bgPieces.Length)
        {
            SetCanvasGroupAlpha(bgCanvasGroup, 1f);
            InsertBGOpenTweens(showSequence);
        }
        else if (null != bgRoot)
        {
            showSequence.Append(DOTween.To(GetBGWidth, SetBGWidth, bgTargetWidth, bgOpenDuration).SetEase(bgOpenEase));
            if (null != bgCanvasGroup && true == useBgFadeIn)
            {
                showSequence.Join(bgCanvasGroup.DOFade(1f, bgOpenDuration).SetEase(bgOpenEase));
            }
        }

        // 2. 텍스트 컨텐츠 스케일 & 페이드 등장 연출
        float _contentStartTime = 0.05f;

        if (null != contentCanvasGroup)
        {
            showSequence.Insert(_contentStartTime, contentCanvasGroup.DOFade(1f, scaleDuration).SetEase(Ease.OutQuad));
        }

        if (null != textContainer)
        {
            showSequence.Insert(_contentStartTime, textContainer.DOScale(targetScale, scaleDuration).SetEase(scaleEase));
        }

        showSequence.InsertCallback(_contentStartTime, cachedPlayQuestTextAppearSounds);

        showSequence.OnComplete(cachedOnShowComplete);
    }

    private void PlayHideQuest()
    {
        if (false == bIsShowing)
            return;

        KillSequences();
        bIsShowing = false;

        hideSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // 1. 텍스트 컨텐츠 스케일 & 페이드아웃 선행 연출
        float _textHideDuration = scaleDuration;

        if (null != contentCanvasGroup)
        {
            hideSequence.Insert(0f, contentCanvasGroup.DOFade(0f, _textHideDuration).SetEase(Ease.InQuad));
        }

        if (null != textContainer)
        {
            hideSequence.Insert(0f, textContainer.DOScale(startScale, _textHideDuration).SetEase(Ease.InBack));
        }

        // 2. DimBG 축소 및 페이드 역재생 연출 (글자가 완전히 사라진 후 시작)
        float _bgHideStartTime = _textHideDuration;

        if (0 < bgPieces.Length)
        {
            AssignBGCloseDelays();
            InsertBGCloseTweens(hideSequence, _bgHideStartTime);
        }
        else if (null != bgRoot)
        {
            hideSequence.Insert(_bgHideStartTime, DOTween.To(GetBGWidth, SetBGWidth, HiddenBGWidth, bgCloseDuration).SetEase(bgCloseEase));
            if (null != bgCanvasGroup && true == useBgFadeOut)
            {
                hideSequence.Insert(_bgHideStartTime, bgCanvasGroup.DOFade(0f, bgCloseDuration).SetEase(bgCloseEase));
            }
        }

        hideSequence.OnComplete(cachedOnHideComplete);
    }

    private void PlayStepTransition(string _nextTitle, string _nextDesc)
    {
        pendingNextTitle = _nextTitle;
        pendingNextDesc = _nextDesc;

        KillSequences();
        bIsShowing = true;

        PlayQuestCompletedSound();

        stepTransitionSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        AppendCompletionEffect(stepTransitionSequence);
        AppendFadeOutEffect(stepTransitionSequence);
        AppendNextQuestAppearEffect(stepTransitionSequence);

        stepTransitionSequence.OnComplete(cachedOnTransitionComplete);
    }

    private void AppendCompletionEffect(Sequence _seq)
    {
        if (null != questTitleText)
        {
            _seq.Append(questTitleText.DOColor(completedColor, colorTransitionDuration).SetEase(colorTransitionEase));
        }
        else
        {
            _seq.AppendInterval(colorTransitionDuration);
        }

        Tween _completedTextPopTween = BuildCompletedTextPopTween();
        if (null != _completedTextPopTween)
            _seq.Join(_completedTextPopTween);

        float _holdTime = Mathf.Max(0.1f, completedHoldDuration);
        _seq.AppendInterval(_holdTime);
    }

    private void AppendFadeOutEffect(Sequence _seq)
    {
        if (null != contentCanvasGroup)
        {
            _seq.Append(contentCanvasGroup.DOFade(0f, 0.12f).SetEase(Ease.InQuad));
        }
    }

    private void AppendNextQuestAppearEffect(Sequence _seq)
    {
        _seq.AppendCallback(cachedOnStepMidpoint);

        if (null != contentCanvasGroup)
        {
            _seq.Append(contentCanvasGroup.DOFade(1f, scaleDuration).SetEase(Ease.OutQuad));
        }

        if (null != textContainer)
        {
            if (null != contentCanvasGroup)
            {
                _seq.Join(textContainer.DOScale(targetScale, scaleDuration).SetEase(scaleEase));
            }
            else
            {
                _seq.Append(textContainer.DOScale(targetScale, scaleDuration).SetEase(scaleEase));
            }
        }
    }

    /// <summary>
    /// 현재 퀘스트를 완료 색상으로 전환하고 유지시간 대기 후 DimBG를 퇴장시킵니다.
    /// </summary>
    private void PlayCompleteAndHide()
    {
        KillSequences();

        PlayQuestCompletedSound();

        completedSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        AppendCompletionEffect(completedSequence);

        completedSequence.AppendCallback(cachedOnCompleteHideCallback);
        completedSequence.OnComplete(cachedOnCompletedHide);
    }

    private Tween BuildCompletedTextPopTween()
    {
        if (null == textContainer)
            return null;

        float _duration = Mathf.Max(0.0f, completedTextPopDuration);
        textContainer.localScale = targetScale;

        if (0.0f >= _duration)
            return null;

        Vector3 _squashScale = Vector3.Scale(targetScale, completedTextSquashScale);
        Vector3 _stretchScale = Vector3.Scale(targetScale, completedTextStretchScale);

        return DOTween.Sequence()
            .Append(textContainer.DOScale(_squashScale, _duration * 0.28f).SetEase(Ease.OutQuad))
            .Append(textContainer.DOScale(_stretchScale, _duration * 0.30f).SetEase(Ease.InOutQuad))
            .Append(textContainer.DOScale(targetScale, _duration * 0.42f).SetEase(Ease.OutBack));
    }

    private void PlayQuestTextAppearSounds()
    {
        Sound.PlayUI(SoundID.MainMenuButtonAppearStart00);
        Sound.PlayUI(SoundID.MainMenuDot02);
    }

    private void PlayQuestCompletedSound()
    {
        Sound.PlayUI(SoundID.AbilityUpgradeFailed);
    }

    private void SetQuestContent(string _title, string _desc)
    {
        if (null != questTitleText)
        {
            questTitleText.text = _title;
            questTitleText.color = inProgressColor;
        }

        bool _hasDesc = false == string.IsNullOrEmpty(_desc);
        if (null != questDescRoot)
        {
            questDescRoot.SetActive(_hasDesc);
        }

        if (null != questDescText && _hasDesc)
        {
            questDescText.text = _desc;
        }

        activeBGCount = _hasDesc ? Mathf.Min(2, bgPieces.Length) : Mathf.Min(1, bgPieces.Length);
    }

    private void PrepareShowState()
    {
        if (0 < bgPieces.Length)
        {
            for (int i = 0; i < bgPieces.Length; i++)
            {
                SetBGPieceWidth(bgPieces[i], HiddenBGWidth);
                if (i < activeBGCount)
                {
                    SetGraphicAlpha(bgPieces[i].graphic, useBgFadeIn ? 0f : bgPieces[i].targetAlpha);
                }
                else
                {
                    SetGraphicAlpha(bgPieces[i].graphic, 0f);
                }
            }
        }
        else if (null != bgRoot)
        {
            SetBGWidth(HiddenBGWidth);
        }

        SetCanvasGroupAlpha(bgCanvasGroup, useBgFadeIn ? 0f : 1f);
        SetCanvasGroupAlpha(contentCanvasGroup, 0f);

        if (null != textContainer)
        {
            textContainer.localScale = startScale;
        }

        if (null != questTitleText)
        {
            questTitleText.color = inProgressColor;
        }
    }

    private void PrepareHiddenState()
    {
        if (0 < bgPieces.Length)
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
        SetCanvasGroupAlpha(contentCanvasGroup, 0f);

        if (null != questDescRoot)
        {
            questDescRoot.SetActive(false);
        }
    }

    private void InsertBGOpenTweens(Sequence _seq)
    {
        if (useBgFadeIn)
        {
            _seq.Insert(0f, DOTween.To(
                GetBGPiecesAlpha,
                SetBGPiecesAlpha,
                1f,
                bgOpenDuration).SetEase(Ease.Linear));
        }

        int _count = Mathf.Clamp(activeBGCount, 1, bgPieces.Length);
        for (int i = 0; i < _count; i++)
        {
            TutorialQuestBGPiece _piece = bgPieces[i];
            float _width = 0f < bgTargetWidth ? bgTargetWidth : _piece.targetWidth;

            _seq.Insert(_piece.delay, DOTween.To(
                () => GetBGPieceWidth(_piece),
                w => SetBGPieceWidth(_piece, w),
                _width,
                bgOpenDuration).SetEase(bgOpenEase));
        }

        for (int i = _count; i < bgPieces.Length; i++)
        {
            SetBGPieceWidth(bgPieces[i], HiddenBGWidth);
            SetGraphicAlpha(bgPieces[i].graphic, 0f);
        }
    }

    private void InsertBGCloseTweens(Sequence _seq, float _startOffset)
    {
        if (useBgFadeOut)
        {
            _seq.Insert(_startOffset, DOTween.To(
                GetBGPiecesAlpha,
                SetBGPiecesAlpha,
                0f,
                bgCloseDuration).SetEase(Ease.Linear));
        }

        int _count = Mathf.Clamp(activeBGCount, 1, bgPieces.Length);
        for (int i = 0; i < _count; i++)
        {
            TutorialQuestBGPiece _piece = bgPieces[i];
            float _pieceTime = _startOffset + _piece.delay;

            _seq.Insert(_pieceTime, DOTween.To(
                () => GetBGPieceWidth(_piece),
                w => SetBGPieceWidth(_piece, w),
                HiddenBGWidth,
                bgCloseDuration).SetEase(bgCloseEase));
        }
    }

    private void AssignBGDelays()
    {
        if (0 >= bgPieces.Length) return;

        for (int i = 0; i < bgPieces.Length; i++)
        {
            bgPieces[i].delay = i * bgPieceStaggerDelay;
        }
    }

    private void AssignBGCloseDelays()
    {
        if (0 >= bgPieces.Length) return;

        int _count = Mathf.Clamp(activeBGCount, 1, bgPieces.Length);
        for (int i = 0; i < _count; i++)
        {
            bgPieces[i].delay = (_count - 1 - i) * bgPieceStaggerDelay;
        }
    }

    private void CacheCanvasGroups()
    {
        if (null != bgRoot && null == bgCanvasGroup)
        {
            bgCanvasGroup = bgRoot.GetComponent<CanvasGroup>();
            if (null == bgCanvasGroup)
                bgCanvasGroup = bgRoot.gameObject.AddComponent<CanvasGroup>();
        }

        if (null != textContainer && null == contentCanvasGroup)
        {
            contentCanvasGroup = textContainer.GetComponent<CanvasGroup>();
            if (null == contentCanvasGroup)
                contentCanvasGroup = textContainer.gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void CacheBGPieces()
    {
        bgPieces = Array.Empty<TutorialQuestBGPiece>();
        if (null == bgRoot) return;

        RectTransform[] _rects = bgRoot.GetComponentsInChildren<RectTransform>(true);
        List<TutorialQuestBGPiece> _list = new List<TutorialQuestBGPiece>(8);

        for (int i = 0; i < _rects.Length; i++)
        {
            RectTransform _rect = _rects[i];
            if (_rect == bgRoot || false == _rect.name.StartsWith("BG_", StringComparison.Ordinal))
                continue;

            Graphic _graphic = _rect.GetComponent<Graphic>();
            if (null == _graphic) continue;

            _list.Add(new TutorialQuestBGPiece
            {
                rectTransform = _rect,
                graphic = _graphic,
                targetWidth = 0f < _rect.rect.width ? _rect.rect.width : bgTargetWidth,
                targetAlpha = DefaultBGTargetAlpha
            });
        }

        // Y 좌표 내림차순 (상단 -> 하단) 정렬
        _list.Sort((left, right) =>
        {
            if (null == left?.rectTransform || null == right?.rectTransform) return 0;
            int _yComp = right.rectTransform.anchoredPosition.y.CompareTo(left.rectTransform.anchoredPosition.y);
            if (0 != _yComp) return _yComp;
            return string.CompareOrdinal(left.rectTransform.name, right.rectTransform.name);
        });

        bgPieces = _list.ToArray();

        if (0 < bgPieces.Length && 0f >= bgTargetWidth)
        {
            bgTargetWidth = bgPieces[0].targetWidth;
        }
        else if (null != bgRoot && 0f >= bgTargetWidth)
        {
            bgTargetWidth = bgRoot.rect.width;
        }
    }

    private float GetBGPieceWidth(TutorialQuestBGPiece _piece)
    {
        return null != _piece?.rectTransform ? _piece.rectTransform.rect.width : 0f;
    }

    private void SetBGPieceWidth(TutorialQuestBGPiece _piece, float _width)
    {
        if (null != _piece?.rectTransform)
        {
            _piece.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, _width);
        }
    }

    private float GetBGPiecesAlpha()
    {
        if (0 >= bgPieces.Length || null == bgPieces[0].graphic) return 0f;
        float _targetAlpha = Mathf.Max(bgPieces[0].targetAlpha, 0.0001f);
        return Mathf.Clamp01(bgPieces[0].graphic.color.a / _targetAlpha);
    }

    private void SetBGPiecesAlpha(float _ratio)
    {
        int _count = Mathf.Clamp(activeBGCount, 1, bgPieces.Length);
        for (int i = 0; i < _count; i++)
        {
            SetGraphicAlpha(bgPieces[i].graphic, bgPieces[i].targetAlpha * _ratio);
        }
        for (int i = _count; i < bgPieces.Length; i++)
        {
            SetGraphicAlpha(bgPieces[i].graphic, 0f);
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

    private void OnCompleteHideCallback()
    {
        PlayHideQuest();
    }

    /// <summary>
    /// PlayCompleteAndHide()로 시작된 "완료 후 숨김" 연출이 completedSequence(완료 색상/유지) 또는
    /// hideSequence(퇴장 애니메이션) 단계에서 아직 끝나지 않았다면, 남은 연출을 건너뛰고 즉시 숨김
    /// 상태로 확정한 뒤 HideCompletedEvent를 발행한다. 이 이벤트를 구독해 게임 로직을 진행시키는
    /// 쪽(예: 상호작용 잠금 해제)이 다음 퀘스트 시작 타이밍과 무관하게 항상 신호를 받도록 보장하기 위함.
    /// </summary>
    private void ForceCompletePendingHide()
    {
        bool _hasPendingHide = (null != completedSequence && completedSequence.IsActive())
                             || (null != hideSequence && hideSequence.IsActive());

        if (false == _hasPendingHide)
            return;

        TutorialStep _hidingStep = currentStep;

        KillSequences();
        bIsShowing = false;
        PrepareHiddenState();

        HideCompletedEvent?.Invoke(_hidingStep);
    }

    private void KillSequences()
    {
        if (null != stepTransitionSequence && stepTransitionSequence.IsActive())
        {
            stepTransitionSequence.Kill();
            stepTransitionSequence = null;
        }

        if (null != showSequence && showSequence.IsActive())
        {
            showSequence.Kill();
            showSequence = null;
        }

        if (null != hideSequence && hideSequence.IsActive())
        {
            hideSequence.Kill();
            hideSequence = null;
        }

        if (null != completedSequence && completedSequence.IsActive())
        {
            completedSequence.Kill();
            completedSequence = null;
        }
    }

    private string GetCutTreeTitle()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText("CutTree_Title");
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "나무를 벌목하세요";
    }

    private string GetFillContainerTitle()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText("FillOffroadContainer_Title");
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "마을로 가져갈 원목을 운반 상자에 넣으세요!";
    }

    private string GetFillContainerDesc()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText("FillOffroadContainer_Desc");
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "넣지 않은 원목은 숲을 떠날 때 사라집니다.";
    }

    private string GetGoHomeTitle()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText("GoHomeBeforeExhausted_Title");
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "탈진하기 전에 마을로 돌아가세요!";
    }

    private string GetPutItemsTitle()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText("PutItemsInLogContainer_Title");
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "가져온 원목을 제재소 원목 보관함에 넣으세요";
    }

    private string GetReceiveMoneyTitle()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText("ReceiveMoney_Title");
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "정산된 금액을 받아가세요!";
    }

    private string GetUpgradeAxeTitle()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText("UpgradeAxe_Title");
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "도끼를 강화하세요.";
    }

    private string GetStartNewLoggingTitle()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText("StartNewLogging_Title");
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "새로운 벌목을 시작하세요!";
    }

    private void GetQuestTitleAndDesc(TutorialStep _step, out string _title, out string _desc)
    {
        switch (_step)
        {
            case TutorialStep.CutTree:
                _title = GetCutTreeTitle();
                _desc = string.Empty;
                break;
            case TutorialStep.FillOffroadContainer:
                _title = GetFillContainerTitle();
                _desc = GetFillContainerDesc();
                break;
            case TutorialStep.GoHomeBeforeExhausted:
                _title = GetGoHomeTitle();
                _desc = string.Empty;
                break;
            case TutorialStep.PutItemsInLogContainer:
                _title = GetPutItemsTitle();
                _desc = string.Empty;
                break;
            case TutorialStep.ReceiveMoney:
                _title = GetReceiveMoneyTitle();
                _desc = string.Empty;
                break;
            case TutorialStep.UpgradeAxe:
                _title = GetUpgradeAxeTitle();
                _desc = string.Empty;
                break;
            case TutorialStep.StartNewLogging:
                _title = GetStartNewLoggingTitle();
                _desc = string.Empty;
                break;
            default:
                _title = string.Empty;
                _desc = string.Empty;
                break;
        }
    }

    // 유니티 이벤트 함수
    private void Awake()
    {
        InitCachedCallbacks();
        CacheCanvasGroups();
        CacheBGPieces();
    }

    private void InitCachedCallbacks()
    {
        if (null != cachedOnShowComplete) return; // 중복 초기화 방지

        cachedOnShowComplete = OnShowComplete;
        cachedOnHideComplete = OnHideComplete;
        cachedOnTransitionComplete = OnTransitionComplete;
        cachedOnCompletedHide = OnCompletedHide;
        cachedOnStepMidpoint = OnStepMidpoint;
        cachedOnTransitionBGCollapse = OnTransitionBGCollapse;
        cachedPlayQuestTextAppearSounds = PlayQuestTextAppearSounds;
        cachedOnCompleteHideCallback = OnCompleteHideCallback;
    }

    private void OnShowComplete() { }
    
    private void OnHideComplete() 
    { 
        HideCompletedEvent?.Invoke(currentStep); 
    }
    
    private void OnTransitionComplete() 
    { 
        StepTransitionCompletedEvent?.Invoke(currentStep); 
    }
    
    private void OnCompletedHide() 
    { 
        HideCompletedEvent?.Invoke(currentStep); 
    }

    private void OnStepMidpoint()
    {
        SetQuestContent(pendingNextTitle, pendingNextDesc);
        if (null != textContainer)
        {
            textContainer.localScale = startScale;
        }
    }

    private void OnTransitionBGCollapse() { }

    private void OnDestroy()
    {
        KillSequences();

        if (null != localizationManager && null != cachedRefreshLocalizedTexts)
        {
            localizationManager.OnLanguageChanged -= cachedRefreshLocalizedTexts;
        }

        cachedRefreshLocalizedTexts = null;
        localizationManager = null;
    }
}
