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
    [SerializeField] private TMPInlineStyleAnimator questTitleAnimator;
    [SerializeField] private GameObject questDescRoot;
    [SerializeField] private TextMeshProUGUI questDescText;
    [SerializeField] private TMPInlineStyleAnimator questDescAnimator;

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

    [Header("Localization")]
    [SerializeField] private int localizationJsonId = 15;
    [SerializeField] private int cutTreeTitleId = 1;
    [SerializeField] private int fillContainerTitleId = 2;
    [SerializeField] private int goHomeTitleId = 3;
    [SerializeField] private int goHomeDescId = 4;

    // 내부 의존성
    private const float HiddenBGWidth = 0f;
    private const float DefaultBGTargetAlpha = 0.95f;

    private LocalizationManager localizationManager;
    private Action cachedRefreshLocalizedTexts;

    private TutorialQuestBGPiece[] bgPieces = Array.Empty<TutorialQuestBGPiece>();

    private Sequence showSequence;
    private Sequence hideSequence;
    private Sequence stepTransitionSequence;
    private Sequence completedSequence;

    private TutorialStep currentStep;
    private bool bIsShowing = false;

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
        currentStep = _step;

        switch (_step)
        {
            case TutorialStep.CutTree:
                if (bIsShowing)
                {
                    PlayStepTransition(GetCutTreeTitle(), string.Empty);
                }
                else
                {
                    SetQuestContent(GetCutTreeTitle(), string.Empty);
                    PlayShowQuest();
                }
                break;

            case TutorialStep.FillOffroadContainer:
                if (bIsShowing)
                {
                    PlayStepTransition(GetFillContainerTitle(), string.Empty);
                }
                else
                {
                    SetQuestContent(GetFillContainerTitle(), string.Empty);
                    PlayShowQuest();
                }
                break;

            case TutorialStep.GoHomeBeforeExhausted:
                if (bIsShowing)
                {
                    PlayStepTransition(GetGoHomeTitle(), GetGoHomeDesc());
                }
                else
                {
                    SetQuestContent(GetGoHomeTitle(), GetGoHomeDesc());
                    PlayShowQuest();
                }
                break;
        }
    }

    public void OnTutorialStepCompleted(TutorialStep _step)
    {
        switch (_step)
        {
            case TutorialStep.FillOffroadContainer:
            case TutorialStep.GoHomeBeforeExhausted:
                if (bIsShowing)
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

        switch (currentStep)
        {
            case TutorialStep.CutTree:
                SetQuestContent(GetCutTreeTitle(), string.Empty);
                break;
            case TutorialStep.FillOffroadContainer:
                SetQuestContent(GetFillContainerTitle(), string.Empty);
                break;
            case TutorialStep.GoHomeBeforeExhausted:
                SetQuestContent(GetGoHomeTitle(), GetGoHomeDesc());
                break;
        }
    }

    private void PlayShowQuest()
    {
        KillSequences();
        bIsShowing = true;

        AssignBGDelays();
        PrepareShowState();

        showSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // 1. DimBG 확장 연출
        if (bgPieces.Length > 0)
        {
            SetCanvasGroupAlpha(bgCanvasGroup, 1f);
            InsertBGOpenTweens(showSequence);
        }
        else if (null != bgRoot)
        {
            showSequence.Append(DOTween.To(GetBGWidth, SetBGWidth, bgTargetWidth, bgOpenDuration).SetEase(bgOpenEase));
            if (null != bgCanvasGroup && useBgFadeIn)
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

        showSequence.InsertCallback(_contentStartTime, OnPlayTextRevealAnimations);

        showSequence.OnComplete(() =>
        {
            showSequence = null;
        });
    }

    private void PlayHideQuest()
    {
        if (false == bIsShowing)
            return;

        KillSequences();
        bIsShowing = false;

        hideSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // 1. 텍스트 컨텐츠 페이드아웃
        if (null != contentCanvasGroup)
        {
            hideSequence.Join(contentCanvasGroup.DOFade(0f, bgCloseDuration * 0.5f).SetEase(Ease.InQuad));
        }

        // 2. DimBG 축소 및 페이드 연출
        if (bgPieces.Length > 0)
        {
            AssignBGCloseDelays();
            InsertBGCloseTweens(hideSequence, 0f);
        }
        else if (null != bgRoot)
        {
            hideSequence.Join(DOTween.To(GetBGWidth, SetBGWidth, HiddenBGWidth, bgCloseDuration).SetEase(bgCloseEase));
            if (null != bgCanvasGroup && useBgFadeOut)
            {
                hideSequence.Join(bgCanvasGroup.DOFade(0f, bgCloseDuration).SetEase(bgCloseEase));
            }
        }

        hideSequence.OnComplete(() =>
        {
            hideSequence = null;
            PrepareHiddenState();
        });
    }

    /// <summary>
    /// 이전 퀘스트를 완료 색상으로 전환하고 유지시간 대기 후, 다음 퀘스트 텍스트로 스케일 바운스 교체합니다.
    /// </summary>
    private void PlayStepTransition(string _nextTitle, string _nextDesc)
    {
        KillSequences();
        bIsShowing = true;

        stepTransitionSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // 1. 현재 완료된 퀘스트 텍스트 색상 전환 (Append로 시퀀스 타임라인에 명시적 등록)
        if (null != questTitleText)
        {
            stepTransitionSequence.Append(questTitleText.DOColor(completedColor, colorTransitionDuration).SetEase(colorTransitionEase));
        }
        else
        {
            stepTransitionSequence.AppendInterval(colorTransitionDuration);
        }

        // 2. 완료 색상 유지 시간(Hold Duration) 대기 (지정한 시간 동안 완료 상태 유지)
        float _holdTime = Mathf.Max(0.1f, completedHoldDuration);
        stepTransitionSequence.AppendInterval(_holdTime);

        // 3. 완료된 텍스트 부드럽게 페이드아웃 (0.12초)
        if (null != contentCanvasGroup)
        {
            stepTransitionSequence.Append(contentCanvasGroup.DOFade(0f, 0.12f).SetEase(Ease.InQuad));
        }

        // 4. 다음 퀘스트 텍스트 교체 및 시작 스케일 세팅
        stepTransitionSequence.AppendCallback(() =>
        {
            SetQuestContent(_nextTitle, _nextDesc);
            if (null != textContainer)
            {
                textContainer.localScale = startScale;
            }
        });

        // 5. 다음 퀘스트 텍스트 스케일 바운스 & 페이드인 등장 연출
        if (null != contentCanvasGroup)
        {
            stepTransitionSequence.Append(contentCanvasGroup.DOFade(1f, scaleDuration).SetEase(Ease.OutQuad));
        }

        if (null != textContainer)
        {
            if (null != contentCanvasGroup)
            {
                stepTransitionSequence.Join(textContainer.DOScale(targetScale, scaleDuration).SetEase(scaleEase));
            }
            else
            {
                stepTransitionSequence.Append(textContainer.DOScale(targetScale, scaleDuration).SetEase(scaleEase));
            }
        }

        stepTransitionSequence.AppendCallback(OnPlayTextRevealAnimations);

        stepTransitionSequence.OnComplete(() =>
        {
            stepTransitionSequence = null;
        });
    }

    /// <summary>
    /// 현재 퀘스트를 완료 색상으로 전환하고 유지시간 대기 후 DimBG를 퇴장시킵니다.
    /// </summary>
    private void PlayCompleteAndHide()
    {
        KillSequences();

        completedSequence = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);

        // 1. 현재 퀘스트 텍스트 완료 색상 전환 (Append로 타임라인 명시적 등록)
        if (null != questTitleText)
        {
            completedSequence.Append(questTitleText.DOColor(completedColor, colorTransitionDuration).SetEase(colorTransitionEase));
        }
        else
        {
            completedSequence.AppendInterval(colorTransitionDuration);
        }

        // 2. 완료 색상 유지
        float _holdTime = Mathf.Max(0.1f, completedHoldDuration);
        completedSequence.AppendInterval(_holdTime);

        // 3. DimBG 및 퀘스트 UI 퇴장
        completedSequence.AppendCallback(OnCompleteHideCallback);

        completedSequence.OnComplete(() =>
        {
            completedSequence = null;
        });
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
    }

    private void PrepareShowState()
    {
        if (bgPieces.Length > 0)
        {
            for (int i = 0; i < bgPieces.Length; i++)
            {
                SetBGPieceWidth(bgPieces[i], HiddenBGWidth);
                SetGraphicAlpha(bgPieces[i].graphic, useBgFadeIn ? 0f : bgPieces[i].targetAlpha);
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

        for (int i = 0; i < bgPieces.Length; i++)
        {
            TutorialQuestBGPiece _piece = bgPieces[i];
            float _width = bgTargetWidth > 0f ? bgTargetWidth : _piece.targetWidth;

            _seq.Insert(_piece.delay, DOTween.To(
                () => GetBGPieceWidth(_piece),
                w => SetBGPieceWidth(_piece, w),
                _width,
                bgOpenDuration).SetEase(bgOpenEase));
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

        for (int i = 0; i < bgPieces.Length; i++)
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
        if (bgPieces.Length <= 0) return;

        for (int i = 0; i < bgPieces.Length; i++)
        {
            bgPieces[i].delay = i * bgPieceStaggerDelay;
        }
    }

    private void AssignBGCloseDelays()
    {
        if (bgPieces.Length <= 0) return;

        for (int i = 0; i < bgPieces.Length; i++)
        {
            bgPieces[i].delay = (bgPieces.Length - 1 - i) * bgPieceStaggerDelay;
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
                targetWidth = _rect.rect.width > 0f ? _rect.rect.width : bgTargetWidth,
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

        if (bgPieces.Length > 0 && bgTargetWidth <= 0f)
        {
            bgTargetWidth = bgPieces[0].targetWidth;
        }
        else if (null != bgRoot && bgTargetWidth <= 0f)
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

    private void OnPlayTextRevealAnimations()
    {
        if (null != questTitleAnimator)
        {
            questTitleAnimator.PlayRevealBounce();
        }

        if (null != questDescAnimator && null != questDescRoot && questDescRoot.activeSelf)
        {
            questDescAnimator.PlayRevealBounce();
        }
    }

    private void OnCompleteHideCallback()
    {
        PlayHideQuest();
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
            string _text = localizationManager.GetText(localizationJsonId, cutTreeTitleId);
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "나무를 벌목하세요";
    }

    private string GetFillContainerTitle()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText(localizationJsonId, fillContainerTitleId);
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "마을로 가져갈 원목을 운반 상자에 넣으세요!";
    }

    private string GetGoHomeTitle()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText(localizationJsonId, goHomeTitleId);
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "탈진하기 전에 마을로 돌아가세요!";
    }

    private string GetGoHomeDesc()
    {
        if (null != localizationManager)
        {
            string _text = localizationManager.GetText(localizationJsonId, goHomeDescId);
            if (false == string.IsNullOrEmpty(_text)) return _text;
        }
        return "피로도가 20% 아래로 내려가면 탈진할 수 있습니다.";
    }

    // 유니티 이벤트 함수
    private void Awake()
    {
        CacheCanvasGroups();
        CacheBGPieces();
    }

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
