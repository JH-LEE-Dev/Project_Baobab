using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;
using PresentationLayer.UISystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum OpeningMotionType
{
    None = 0,       // 위치/스케일 변형 없음
    Scale = 1       // 스케일 변화 (startScale -> targetScale)
}

public enum OpeningFadeType
{
    None = 0,       // 알파 불변 (1 유지)
    FadeIn = 1,     // 0 -> 1 페이드인
    FadeOut = 2,    // 1 -> 0 페이드아웃 (유지 후 종료 직전 페이드아웃)
    FadeInOut = 3,  // 시작 시 페이드인 -> 유지 -> 종료 직전 페이드아웃
    Custom = 4      // startAlpha -> targetAlpha 직접 지정
}

[Serializable]
public struct OpeningElementInfo
{
    public RectTransform targetRect;
    public CanvasGroup canvasGroup;

    // Order & Total Timing
    public int orderIndex;
    public float duration;
    public float delay;

    // Motion Settings
    public OpeningMotionType motionType;
    public float motionDuration;
    public Vector3 startScale;
    public Vector3 targetScale;
    public Ease scaleEase;

    // Fade Settings
    public OpeningFadeType fadeType;
    public float fadeInDuration;
    public float fadeOutDuration;
    public float startAlpha;
    public float targetAlpha;
    public Ease fadeEase;

    // Localization (Optional for Text)
    public TextMeshProUGUI targetText;
    public int localizationEntryId;
    public TMPInlineStyleAnimator targetAnimator;
    public bool playTMPRevealBounce;

    // 켜면 시간이 지나도 사라지지 않고, 글자 등장이 끝난 뒤 공격 키를 눌러야 다음으로 넘어간다.
    public bool waitForInput;
}

public enum OpeningPhase
{
    Timed = 0,          // 정해진 시간 동안 진행하고 자동으로 다음 단계로 넘어감
    GatedEnter = 1,     // 입력 대기 대사가 나타나는 중 (끝나면 키 입력을 기다린다)
    GatedExit = 2,      // 키 입력을 받고 대사가 사라지는 중
    Finished = 3,       // 진행할 단계가 없어 곧바로 끝남
}

public class UI_OpeningProduction : MonoBehaviour
{
    private const int OpeningMonologue2EntryId = 3;
    private const float FontPopCooldown = 0.03f;

    [Header("Localization Settings")]
    [SerializeField] private int localizationJsonId = 16;

    // 외부 의존성
    private LocalizationManager localizationManager;
    private InputManager inputManager;
    private Action onIntroCompleteCallback;
    private Action cachedApplyLocalization;
    private Action cachedRevealCharacterAppeared;
    private Action cachedOnAdvancePressed;
    private TweenCallback cachedOnPhaseCompleted;
    private float nextFontPopAllowedTime;

    // 내부 의존성
    [SerializeField] private List<OpeningElementInfo> introSceneElements = new List<OpeningElementInfo>();

    [Header("Advance Input (waitForInput 대사)")]
    [Tooltip("대사가 나타난 뒤 공격 키 입력을 받기 시작하기까지의 시간(초). 글자 등장 연출(TMPInlineStyleAnimator의 revealTotalDuration + revealCharacterDuration, 현재 0.4초)이 끝난 뒤 여유를 더한 값이어야 한다. 등장 연출을 늘리면 이 값도 함께 올린다.")]
    [SerializeField] private float inputWaitDelay = 0.6f;
    [Tooltip("키 입력을 받을 수 있게 되면 보여줄 안내 UI의 루트. 공격 키 아이콘(UI_KeyboardImage)이 들어 있다. 피벗은 (0, 0.5)여야 하며, 크기는 아이콘의 원본 픽셀 크기로 코드가 맞춘다. 위치는 대기 중인 대사의 마지막 줄 끝 오른쪽에 코드가 맞춘다.")]
    [SerializeField] private RectTransform advancePromptRoot;
    [SerializeField] private UI_KeyboardImage advancePromptKey;
    [Tooltip("대사 마지막 줄 끝과 아이콘 사이의 간격(캔버스 픽셀)")]
    [SerializeField] private float advancePromptGap = 4f;
    [Tooltip("안내 아이콘이 깜빡일 때 가장 어두운 밝기(0~1). 알파가 아니라 색(RGB)만 바꾼다. UIEffect 외곽선은 아이콘 모양의 복사본 여러 장을 겹쳐 그려서, 알파를 낮추면 복사본이 안쪽에 쌓여 아이콘이 허옇게 번진다.")]
    [SerializeField] private float advancePromptPulseBrightness = 0.55f;
    [SerializeField] private float advancePromptPulseDuration = 0.8f;

    private Sequence activeSequence;
    private Tween advancePromptTween;

    private TweenCallback[] cachedActivateCallbacks;
    private TweenCallback[] cachedDeactivateCallbacks;

    // 상태 변수
    private readonly List<int> stepOrders = new List<int>(8);
    private int stepCursor;
    private int gatedElementIndex = -1;
    private bool isAdvanceInputArmed;
    private Image advancePromptIcon;
    private Sprite appliedPromptSprite;
    private OpeningPhase currentPhase = OpeningPhase.Timed;

    public void Initialize(LocalizationManager _localizationManager = null, InputManager _inputManager = null)
    {
        localizationManager = _localizationManager;
        inputManager = _inputManager;

        advancePromptIcon = null != advancePromptKey ? advancePromptKey.GetComponent<Image>() : null;

        if (null != advancePromptKey && null != inputManager)
        {
            advancePromptKey.Initialize(inputManager);
        }

        HideAdvancePrompt();

        if (null != localizationManager)
        {
            if (null == cachedApplyLocalization)
            {
                cachedApplyLocalization = ApplyLocalization;
            }
            localizationManager.OnLanguageChanged -= cachedApplyLocalization;
            localizationManager.OnLanguageChanged += cachedApplyLocalization;
        }

        CacheComponents();
        CacheCallbacks();
        cachedRevealCharacterAppeared = HandleRevealCharacterAppeared;
        ApplyLocalization();
        SetAllActive(false);
    }

    public void PlayIntroScene(Action _onComplete = null)
    {
        KillActiveSequence();
        CacheComponents();
        CacheCallbacks();
        SetAllActive(false);
        ApplyLocalization();
        nextFontPopAllowedTime = 0f;

        onIntroCompleteCallback = _onComplete;

        stepOrders.Clear();
        int maxOrder = GetMaxOrderIndex();
        for (int order = 0; order <= maxOrder; order++)
        {
            if (false == HasElementsInOrder(order))
            {
                continue;
            }

            if (0f >= GetStepDuration(order))
            {
                continue;
            }

            stepOrders.Add(order);
        }

        stepCursor = 0;
        gatedElementIndex = -1;

        if (0 == stepOrders.Count)
        {
            // 진행할 단계가 없어도 호출한 쪽에는 한 프레임 뒤에 알린다. (연출이 있을 때와 같은 비동기 순서를 지킨다)
            currentPhase = OpeningPhase.Finished;
            activeSequence = DOTween.Sequence().AppendInterval(0.01f).OnComplete(cachedOnPhaseCompleted);
            return;
        }

        PlayCurrentStep();
    }

    // 단계(order) 하나를 재생한다. 한 단계가 끝나면 HandlePhaseCompleted가 다음 단계로 이어 붙인다.
    // 입력 대기 대사가 있는 단계는 "나타나기"와 "사라지기"가 서로 다른 시퀀스이고, 그 사이는 시퀀스를 멈추지 않고
    // 키 입력을 기다린다. (한 시퀀스 안에서 Pause하면 같은 프레임의 남은 시간만큼 사라지는 연출이 먼저 진행된다)
    private void PlayCurrentStep()
    {
        if (stepCursor >= stepOrders.Count)
        {
            HandleSequenceComplete();
            return;
        }

        int order = stepOrders[stepCursor];
        int gatedIndex = FindGatedElementIndex(order);

        Sequence stepSequence = DOTween.Sequence();

        for (int i = 0; i < introSceneElements.Count; i++)
        {
            OpeningElementInfo elem = introSceneElements[i];
            if (order != elem.orderIndex || null == elem.targetRect)
            {
                continue;
            }

            if (i == gatedIndex)
            {
                AppendGatedEnterToSequence(stepSequence, elem, i);
            }
            else
            {
                AppendElementToSequence(stepSequence, elem, i);
            }
        }

        float stepDuration = GetStepDuration(order);
        if (0 <= gatedIndex)
        {
            // 입력 대기 대사는 나타난 뒤 inputWaitDelay가 지나야 입력을 받기 시작한다.
            stepDuration = Mathf.Max(0f, introSceneElements[gatedIndex].delay) + Mathf.Max(0f, inputWaitDelay);
            gatedElementIndex = gatedIndex;
            currentPhase = OpeningPhase.GatedEnter;
        }
        else
        {
            currentPhase = OpeningPhase.Timed;
        }

        float currentSeqDuration = stepSequence.Duration(false);
        if (currentSeqDuration < stepDuration)
        {
            stepSequence.AppendInterval(stepDuration - currentSeqDuration);
        }

        stepSequence.OnComplete(cachedOnPhaseCompleted);
        activeSequence = stepSequence;
    }

    private void HandlePhaseCompleted()
    {
        switch (currentPhase)
        {
            case OpeningPhase.Timed:
            case OpeningPhase.GatedExit:
                gatedElementIndex = -1;
                stepCursor++;
                PlayCurrentStep();
                break;

            case OpeningPhase.GatedEnter:
                ArmAdvanceInput();
                break;

            case OpeningPhase.Finished:
                HandleSequenceComplete();
                break;
        }
    }

    private int FindGatedElementIndex(int _order)
    {
        if (null == introSceneElements)
        {
            return -1;
        }

        for (int i = 0; i < introSceneElements.Count; i++)
        {
            OpeningElementInfo elem = introSceneElements[i];
            if (_order == elem.orderIndex && true == elem.waitForInput && null != elem.targetRect)
            {
                return i;
            }
        }

        return -1;
    }

    // 입력 대기 대사의 "나타나기": 활성화, 확대, 페이드인까지만 넣는다. 사라지기는 키 입력 뒤(BuildGatedExitSequence)에 한다.
    private void AppendGatedEnterToSequence(Sequence _stepSequence, in OpeningElementInfo _elem, int _elemIndex)
    {
        float _startTime = Mathf.Max(0f, _elem.delay);
        float _enterDuration = 0f < inputWaitDelay ? inputWaitDelay : 0.1f;

        if (null != cachedActivateCallbacks && _elemIndex < cachedActivateCallbacks.Length)
        {
            _stepSequence.InsertCallback(_startTime, cachedActivateCallbacks[_elemIndex]);
        }

        if (OpeningMotionType.Scale == _elem.motionType)
        {
            float _mDuration = 0f < _elem.motionDuration ? _elem.motionDuration : _enterDuration;
            Vector3 _targetSc = GetSafeScale(_elem.targetScale);
            Ease _ease = Ease.Unset == _elem.scaleEase ? Ease.OutQuad : _elem.scaleEase;

            _stepSequence.Insert(_startTime, _elem.targetRect.DOScale(_targetSc, _mDuration).SetEase(_ease));
        }

        if (null == _elem.canvasGroup)
        {
            return;
        }

        Ease _fadeEase = Ease.Unset == _elem.fadeEase ? Ease.Linear : _elem.fadeEase;

        switch (_elem.fadeType)
        {
            case OpeningFadeType.FadeIn:
            {
                float _inTime = 0f < _elem.fadeInDuration ? _elem.fadeInDuration : _enterDuration;
                _stepSequence.Insert(_startTime, _elem.canvasGroup.DOFade(1f, _inTime).SetEase(_fadeEase));
                break;
            }

            case OpeningFadeType.FadeInOut:
            {
                float _inTime = 0f < _elem.fadeInDuration ? _elem.fadeInDuration : 0.5f;
                _stepSequence.Insert(_startTime, _elem.canvasGroup.DOFade(1f, _inTime).SetEase(_fadeEase));
                break;
            }

            case OpeningFadeType.Custom:
            {
                _stepSequence.Insert(_startTime, _elem.canvasGroup.DOFade(_elem.targetAlpha, _enterDuration).SetEase(_fadeEase));
                break;
            }
        }
    }

    // 입력 대기 대사의 "사라지기": 페이드아웃(해당하는 타입만) 뒤 비활성화.
    private Sequence BuildGatedExitSequence(int _elemIndex)
    {
        Sequence _exitSequence = DOTween.Sequence();
        OpeningElementInfo _elem = introSceneElements[_elemIndex];

        float _outTime = 0f;
        if (OpeningFadeType.FadeOut == _elem.fadeType)
        {
            _outTime = 0f < _elem.fadeOutDuration ? _elem.fadeOutDuration : 0.25f;
        }
        else if (OpeningFadeType.FadeInOut == _elem.fadeType)
        {
            _outTime = 0f < _elem.fadeOutDuration ? _elem.fadeOutDuration : 0.5f;
        }

        if (0f < _outTime && null != _elem.canvasGroup)
        {
            Ease _ease = Ease.Unset == _elem.fadeEase ? Ease.Linear : _elem.fadeEase;
            _exitSequence.Append(_elem.canvasGroup.DOFade(0f, _outTime).SetEase(_ease));
        }

        if (null != cachedDeactivateCallbacks && _elemIndex < cachedDeactivateCallbacks.Length)
        {
            _exitSequence.AppendCallback(cachedDeactivateCallbacks[_elemIndex]);
        }

        return _exitSequence;
    }

    // 대사가 다 나타났으니 공격 키 입력을 받기 시작하고 안내 UI를 보여준다.
    private void ArmAdvanceInput()
    {
        if (null == inputManager || null == inputManager.inputReader)
        {
            // 입력 수단을 알 수 없으면 영영 멈춰 서지 않도록 그대로 사라지게 한다.
            BeginGatedExit();
            return;
        }

        isAdvanceInputArmed = true;
        inputManager.inputReader.MouseClickEvent -= cachedOnAdvancePressed;
        inputManager.inputReader.MouseClickEvent += cachedOnAdvancePressed;

        ShowAdvancePrompt();
    }

    private void DisarmAdvanceInput()
    {
        isAdvanceInputArmed = false;

        if (null != inputManager && null != inputManager.inputReader && null != cachedOnAdvancePressed)
        {
            inputManager.inputReader.MouseClickEvent -= cachedOnAdvancePressed;
        }
    }

    private void HandleAdvancePressed()
    {
        if (false == isAdvanceInputArmed)
        {
            return;
        }

        DisarmAdvanceInput();
        HideAdvancePrompt();
        Sound.PlayUI(SoundID.MainClick);
        BeginGatedExit();
    }

    private void BeginGatedExit()
    {
        currentPhase = OpeningPhase.GatedExit;

        if (0 > gatedElementIndex || introSceneElements.Count <= gatedElementIndex)
        {
            // 대기 중이던 대사를 찾을 수 없으면 바로 다음 단계로 넘어간다.
            HandlePhaseCompleted();
            return;
        }

        Sequence _exitSequence = BuildGatedExitSequence(gatedElementIndex);
        _exitSequence.OnComplete(cachedOnPhaseCompleted);
        activeSequence = _exitSequence;
    }

    private void ShowAdvancePrompt()
    {
        if (null == advancePromptRoot)
        {
            return;
        }

        KillAdvancePromptTween();
        advancePromptRoot.gameObject.SetActive(true);
        LayoutAdvancePrompt(false);

        if (null == advancePromptIcon)
        {
            return;
        }

        // 알파는 항상 1로 둔다. 아이콘의 UIEffect 외곽선은 모양 복사본을 겹쳐 그리므로 알파를 낮추면(페이드, 알파 깜빡임)
        // 복사본이 안쪽에 쌓여 아이콘이 허옇게 번진다. 그래서 나타남/사라짐은 즉시, 깜빡임은 밝기(RGB)로만 한다.
        advancePromptIcon.color = Color.white;

        float _brightness = Mathf.Clamp01(advancePromptPulseBrightness);
        advancePromptTween = advancePromptIcon.DOColor(new Color(_brightness, _brightness, _brightness, 1f), advancePromptPulseDuration * 0.5f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    // 아이콘은 스프라이트의 원본 픽셀 크기 그대로 쓰고(캔버스 1유닛 = 1픽셀, 늘리지 않는다),
    // 대기 중인 대사의 마지막 줄 끝 오른쪽에 그 줄의 세로 중앙에 맞춰 붙인다.
    // 대사는 우측 정렬이라 마지막 줄의 끝은 텍스트 영역의 오른쪽 가장자리이고, 줄 수는 언어와 문장에 따라 달라진다.
    private void LayoutAdvancePrompt(bool _forceTextMeshUpdate)
    {
        if (null == advancePromptRoot)
        {
            return;
        }

        if (null != advancePromptIcon && null != advancePromptIcon.sprite)
        {
            appliedPromptSprite = advancePromptIcon.sprite;
            advancePromptRoot.sizeDelta = advancePromptIcon.sprite.rect.size;
        }

        if (0 > gatedElementIndex || introSceneElements.Count <= gatedElementIndex)
        {
            return;
        }

        TextMeshProUGUI _text = introSceneElements[gatedElementIndex].targetText;
        if (null == _text)
        {
            return;
        }

        if (true == _forceTextMeshUpdate)
        {
            _text.ForceMeshUpdate(false, true);
        }

        TMP_TextInfo _info = _text.textInfo;
        if (null == _info || 0 >= _info.lineCount)
        {
            return;
        }

        TMP_LineInfo _lastLine = _info.lineInfo[_info.lineCount - 1];
        float _x = _lastLine.lineExtents.max.x + advancePromptGap;
        float _y = (_lastLine.ascender + _lastLine.descender) * 0.5f;

        advancePromptRoot.position = _text.rectTransform.TransformPoint(new Vector3(_x, _y, 0f));
    }

    private void HideAdvancePrompt()
    {
        KillAdvancePromptTween();

        if (null != advancePromptIcon)
        {
            advancePromptIcon.color = Color.white;
        }

        if (null != advancePromptRoot)
        {
            advancePromptRoot.gameObject.SetActive(false);
        }
    }

    private void KillAdvancePromptTween()
    {
        if (null != advancePromptTween)
        {
            advancePromptTween.Kill();
            advancePromptTween = null;
        }
    }

    private void AppendElementToSequence(Sequence _stepSequence, in OpeningElementInfo _elem, int _elemIndex)
    {
        float _startTime = Mathf.Max(0f, _elem.delay);
        float _activeDuration = GetElementActiveDuration(_elem);
        float _endTime = _startTime + _activeDuration;

        if (null != cachedActivateCallbacks && _elemIndex < cachedActivateCallbacks.Length)
        {
            _stepSequence.InsertCallback(_startTime, cachedActivateCallbacks[_elemIndex]);
        }

        if (OpeningMotionType.Scale == _elem.motionType)
        {
            float _mDuration = 0f < _elem.motionDuration ? _elem.motionDuration : _activeDuration;
            Vector3 _targetSc = GetSafeScale(_elem.targetScale);
            Ease _ease = Ease.Unset == _elem.scaleEase ? Ease.OutQuad : _elem.scaleEase;

            Tween _scaleTween = _elem.targetRect.DOScale(_targetSc, _mDuration).SetEase(_ease);
            _stepSequence.Insert(_startTime, _scaleTween);
        }

        if (OpeningFadeType.None != _elem.fadeType && null != _elem.canvasGroup)
        {
            AppendFadeTween(_stepSequence, _elem, _startTime, _activeDuration, _endTime);
        }

        if (null != cachedDeactivateCallbacks && _elemIndex < cachedDeactivateCallbacks.Length)
        {
            _stepSequence.InsertCallback(_endTime, cachedDeactivateCallbacks[_elemIndex]);
        }
    }

    private void AppendFadeTween(Sequence _stepSequence, in OpeningElementInfo _elem, float _startTime, float _activeDuration, float _endTime)
    {
        Ease _ease = Ease.Unset == _elem.fadeEase ? Ease.Linear : _elem.fadeEase;

        switch (_elem.fadeType)
        {
            case OpeningFadeType.FadeIn:
            {
                float _inTime = 0f < _elem.fadeInDuration ? _elem.fadeInDuration : _activeDuration;
                Tween _fadeTween = _elem.canvasGroup.DOFade(1f, _inTime).SetEase(_ease);
                _stepSequence.Insert(_startTime, _fadeTween);
                break;
            }

            case OpeningFadeType.FadeOut:
            {
                float _outTime = 0f < _elem.fadeOutDuration ? _elem.fadeOutDuration : _activeDuration;
                float _fadeOutStartTime = Mathf.Max(_startTime, _endTime - _outTime);
                Tween _fadeTween = _elem.canvasGroup.DOFade(0f, _outTime).SetEase(_ease);
                _stepSequence.Insert(_fadeOutStartTime, _fadeTween);
                break;
            }

            case OpeningFadeType.FadeInOut:
            {
                float _inTime = 0f < _elem.fadeInDuration ? _elem.fadeInDuration : 0.5f;
                float _outTime = 0f < _elem.fadeOutDuration ? _elem.fadeOutDuration : 0.5f;
                float _holdTime = Mathf.Max(0f, _activeDuration - _inTime - _outTime);

                Tween _fadeInTween = _elem.canvasGroup.DOFade(1f, _inTime).SetEase(_ease);
                _stepSequence.Insert(_startTime, _fadeInTween);

                float _fadeOutStartTime = _startTime + _inTime + _holdTime;
                Tween _fadeOutTween = _elem.canvasGroup.DOFade(0f, _outTime).SetEase(_ease);
                _stepSequence.Insert(_fadeOutStartTime, _fadeOutTween);
                break;
            }

            case OpeningFadeType.Custom:
            {
                Tween _customFadeTween = _elem.canvasGroup.DOFade(_elem.targetAlpha, _activeDuration).SetEase(_ease);
                _stepSequence.Insert(_startTime, _customFadeTween);
                break;
            }
        }
    }

    public void StopOpeningProduction()
    {
        KillActiveSequence();
        SetAllActive(false);
    }

    public void ResetOpeningUI()
    {
        KillActiveSequence();
        SetAllActive(false);
    }

    public float CalculateIntroSceneDuration()
    {
        if (null == introSceneElements || 0 == introSceneElements.Count)
        {
            return 0f;
        }

        int maxOrder = GetMaxOrderIndex();
        float totalDuration = 0f;

        for (int order = 0; order <= maxOrder; order++)
        {
            // 입력 대기 대사는 플레이어가 키를 누를 때까지 길어지므로, 입력을 받기 시작하기까지의 최소 시간만 센다.
            int gatedIndex = FindGatedElementIndex(order);
            if (0 <= gatedIndex)
            {
                totalDuration += Mathf.Max(0f, introSceneElements[gatedIndex].delay) + Mathf.Max(0f, inputWaitDelay);
                continue;
            }

            totalDuration += GetStepDuration(order);
        }

        return totalDuration;
    }

    private float GetElementActiveDuration(in OpeningElementInfo _elem)
    {
        float activeDuration = _elem.duration;

        if (OpeningMotionType.Scale == _elem.motionType)
        {
            float mDuration = 0f < _elem.motionDuration ? _elem.motionDuration : _elem.duration;
            if (activeDuration < mDuration)
            {
                activeDuration = mDuration;
            }
        }

        if (OpeningFadeType.FadeInOut == _elem.fadeType)
        {
            float inTime = 0f < _elem.fadeInDuration ? _elem.fadeInDuration : 0.5f;
            float outTime = 0f < _elem.fadeOutDuration ? _elem.fadeOutDuration : 0.5f;
            if (activeDuration < inTime + outTime)
            {
                activeDuration = inTime + outTime;
            }
        }
        else if (OpeningFadeType.FadeIn == _elem.fadeType)
        {
            float inTime = 0f < _elem.fadeInDuration ? _elem.fadeInDuration : _elem.duration;
            if (activeDuration < inTime)
            {
                activeDuration = inTime;
            }
        }
        else if (OpeningFadeType.FadeOut == _elem.fadeType)
        {
            float outTime = 0f < _elem.fadeOutDuration ? _elem.fadeOutDuration : _elem.duration;
            if (activeDuration < outTime)
            {
                activeDuration = outTime;
            }
        }

        return 0f < activeDuration ? activeDuration : 0.1f;
    }

    private float GetStepDuration(int _orderIndex)
    {
        if (null == introSceneElements)
        {
            return 0f;
        }

        float maxStepDuration = 0f;
        for (int i = 0; i < introSceneElements.Count; i++)
        {
            OpeningElementInfo elem = introSceneElements[i];
            if (_orderIndex == elem.orderIndex && null != elem.targetRect)
            {
                float total = Mathf.Max(0f, elem.delay) + GetElementActiveDuration(elem);
                if (maxStepDuration < total)
                {
                    maxStepDuration = total;
                }
            }
        }
        return maxStepDuration;
    }

    private static Vector3 GetSafeScale(Vector3 _scale)
    {
        if (Mathf.Approximately(0f, _scale.z))
        {
            _scale.z = 1f;
        }
        return _scale;
    }

    // "...네? 말도 안 되는 소리 말고 나무나 베라고요?" 대사(Opening_Monologue_2, localizationEntryId 3)가
    // 나오는 순간에만 카메라를 살짝 흔들어 캐릭터의 놀람/황당함을 연출한다.
    private const int ShakeOnMonologueEntryId = 3;

    private void ActivateElement(int _index)
    {
        if (0 > _index || introSceneElements.Count <= _index)
        {
            return;
        }

        OpeningElementInfo elem = introSceneElements[_index];
        if (null == elem.targetRect)
        {
            return;
        }

        elem.targetRect.gameObject.SetActive(true);

        if (OpeningMotionType.Scale == elem.motionType)
        {
            elem.targetRect.localScale = GetSafeScale(elem.startScale);
        }

        if (null != elem.canvasGroup)
        {
            elem.canvasGroup.alpha = GetInitialAlpha(elem);
        }

        if (true == elem.playTMPRevealBounce && null != elem.targetAnimator)
        {
            elem.targetAnimator.PlayRevealBounce(cachedRevealCharacterAppeared);
        }

        if (ShakeOnMonologueEntryId == elem.localizationEntryId)
        {
            CameraMoveController.Instance?.ShakeCamera(2.5f, 0.2f);
        }

        if (OpeningMonologue2EntryId == elem.localizationEntryId)
        {
            Sound.PlayUI(SoundID.FontBomb);
        }
    }

    private void HandleRevealCharacterAppeared()
    {
        if (Time.time < nextFontPopAllowedTime)
        {
            return;
        }

        nextFontPopAllowedTime = Time.time + FontPopCooldown;
        Sound.PlayUI(SoundID.FontPop);
    }

    private void DeactivateElement(int _index)
    {
        if (0 > _index || introSceneElements.Count <= _index)
        {
            return;
        }

        OpeningElementInfo elem = introSceneElements[_index];
        if (null == elem.targetRect)
        {
            return;
        }

        elem.targetRect.gameObject.SetActive(false);
    }

    public void ApplyLocalization()
    {
        if (null == localizationManager || null == introSceneElements)
        {
            return;
        }

        for (int i = 0; i < introSceneElements.Count; i++)
        {
            OpeningElementInfo element = introSceneElements[i];
            if (0 >= element.localizationEntryId || null == element.targetText)
            {
                continue;
            }

            string localizedText = localizationManager.GetText(localizationJsonId, element.localizationEntryId);
            if (false == string.IsNullOrEmpty(localizedText))
            {
                element.targetText.text = localizedText;
            }
        }

        // 입력 대기 중에 언어가 바뀌면 줄 수가 달라질 수 있으므로 안내 아이콘의 위치를 다시 맞춘다.
        if (true == isAdvanceInputArmed)
        {
            LayoutAdvancePrompt(true);
        }
    }

    private void CacheComponents()
    {
        if (null == introSceneElements)
        {
            return;
        }

        for (int i = 0; i < introSceneElements.Count; i++)
        {
            OpeningElementInfo element = introSceneElements[i];
            if (null == element.targetRect)
            {
                continue;
            }

            if (null == element.canvasGroup)
            {
                element.canvasGroup = element.targetRect.GetComponent<CanvasGroup>();
                if (null == element.canvasGroup && OpeningFadeType.None != element.fadeType)
                {
                    element.canvasGroup = element.targetRect.gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (null == element.targetText)
            {
                element.targetText = element.targetRect.GetComponent<TextMeshProUGUI>();
            }

            if (null == element.targetAnimator)
            {
                element.targetAnimator = element.targetRect.GetComponent<TMPInlineStyleAnimator>();
            }

            introSceneElements[i] = element;
        }
    }

    private void CacheCallbacks()
    {
        if (null == cachedOnPhaseCompleted) cachedOnPhaseCompleted = HandlePhaseCompleted;
        if (null == cachedOnAdvancePressed) cachedOnAdvancePressed = HandleAdvancePressed;

        if (null == introSceneElements) return;

        if (null == cachedActivateCallbacks || cachedActivateCallbacks.Length != introSceneElements.Count)
        {
            cachedActivateCallbacks = new TweenCallback[introSceneElements.Count];
            cachedDeactivateCallbacks = new TweenCallback[introSceneElements.Count];

            for (int i = 0; i < introSceneElements.Count; i++)
            {
                int index = i;
                cachedActivateCallbacks[i] = () => ActivateElement(index);
                cachedDeactivateCallbacks[i] = () => DeactivateElement(index);
            }
        }
    }

    private void SetAllActive(bool _isActive)
    {
        if (null == introSceneElements)
        {
            return;
        }

        for (int i = 0; i < introSceneElements.Count; i++)
        {
            OpeningElementInfo element = introSceneElements[i];
            if (null != element.targetRect)
            {
                element.targetRect.gameObject.SetActive(_isActive);
                if (null != element.canvasGroup)
                {
                    element.canvasGroup.alpha = GetInitialAlpha(element);
                }
                if (OpeningMotionType.Scale == element.motionType)
                {
                    element.targetRect.localScale = GetSafeScale(element.startScale);
                }
            }
        }
    }

    private static float GetInitialAlpha(in OpeningElementInfo _elem)
    {
        return _elem.fadeType switch
        {
            OpeningFadeType.FadeIn => 0f,
            OpeningFadeType.FadeInOut => 0f,
            OpeningFadeType.FadeOut => 1f,
            OpeningFadeType.Custom => _elem.startAlpha,
            _ => 1f
        };
    }

    private int GetMaxOrderIndex()
    {
        if (null == introSceneElements || 0 == introSceneElements.Count)
        {
            return 0;
        }

        int maxOrder = 0;
        for (int i = 0; i < introSceneElements.Count; i++)
        {
            if (maxOrder < introSceneElements[i].orderIndex)
            {
                maxOrder = introSceneElements[i].orderIndex;
            }
        }
        return maxOrder;
    }

    private bool HasElementsInOrder(int _orderIndex)
    {
        if (null == introSceneElements)
        {
            return false;
        }

        for (int i = 0; i < introSceneElements.Count; i++)
        {
            if (_orderIndex == introSceneElements[i].orderIndex)
            {
                return true;
            }
        }
        return false;
    }

    private void HandleSequenceComplete()
    {
        SetAllActive(false);
        if (null != onIntroCompleteCallback)
        {
            Action callback = onIntroCompleteCallback;
            onIntroCompleteCallback = null;
            callback.Invoke();
        }
    }

    private void KillActiveSequence()
    {
        if (null != activeSequence && activeSequence.IsActive())
        {
            activeSequence.Kill();
            activeSequence = null;
        }

        // 입력 대기 중에 연출이 중단돼도(메인 메뉴 이동, 씬 리셋 등) 공격 키 구독과 안내 UI가 남지 않게 한다.
        DisarmAdvanceInput();
        HideAdvancePrompt();
        gatedElementIndex = -1;

        if (null != introSceneElements)
        {
            for (int i = 0; i < introSceneElements.Count; i++)
            {
                OpeningElementInfo elem = introSceneElements[i];
                if (null != elem.targetRect)
                {
                    elem.targetRect.DOKill();
                }
                if (null != elem.canvasGroup)
                {
                    elem.canvasGroup.DOKill();
                }
            }
        }
    }

    private void Update()
    {
        // 입력 대기 중에 장치가 바뀌어(패드/마우스, 패드 종류) 아이콘이 바뀌면 크기와 위치를 다시 맞춘다.
        // UI_KeyboardImage에는 아이콘이 바뀌었다는 알림이 없어 스프라이트를 직접 비교한다. 대기 중에만 확인한다.
        if (false == isAdvanceInputArmed || null == advancePromptIcon)
        {
            return;
        }

        if (false == ReferenceEquals(appliedPromptSprite, advancePromptIcon.sprite))
        {
            LayoutAdvancePrompt(false);
        }
    }

    private void OnDestroy()
    {
        KillActiveSequence();
        if (null != localizationManager && null != cachedApplyLocalization)
        {
            localizationManager.OnLanguageChanged -= cachedApplyLocalization;
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(UI_OpeningProduction))]
public class UI_OpeningProductionEditor : Editor
{
    private SerializedProperty localizationJsonIdProp;
    private SerializedProperty introSceneElementsProp;
    private SerializedProperty inputWaitDelayProp;
    private SerializedProperty advancePromptRootProp;
    private SerializedProperty advancePromptKeyProp;
    private SerializedProperty advancePromptGapProp;
    private SerializedProperty advancePromptPulseBrightnessProp;
    private SerializedProperty advancePromptPulseDurationProp;

    private void OnEnable()
    {
        localizationJsonIdProp = serializedObject.FindProperty("localizationJsonId");
        introSceneElementsProp = serializedObject.FindProperty("introSceneElements");
        inputWaitDelayProp = serializedObject.FindProperty("inputWaitDelay");
        advancePromptRootProp = serializedObject.FindProperty("advancePromptRoot");
        advancePromptKeyProp = serializedObject.FindProperty("advancePromptKey");
        advancePromptGapProp = serializedObject.FindProperty("advancePromptGap");
        advancePromptPulseBrightnessProp = serializedObject.FindProperty("advancePromptPulseBrightness");
        advancePromptPulseDurationProp = serializedObject.FindProperty("advancePromptPulseDuration");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        UI_OpeningProduction opening = (UI_OpeningProduction)target;

        // 1. Localization Settings
        if (null != localizationJsonIdProp)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.PropertyField(localizationJsonIdProp, new GUIContent("🌐 Localization Json ID"));
        }

        // 2. Duration Summary Box
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🎬 [Intro Scene Duration (Read-Only)]", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        float introDuration = opening.CalculateIntroSceneDuration();
        EditorGUILayout.LabelField(" • 🎬 Intro Scene", $"{introDuration:F2}s ({introDuration}초)");
        EditorGUILayout.LabelField("Wait For Input 대사는 입력을 받기 시작하기까지의 최소 시간만 포함합니다.", EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // 2-1. Advance Input Settings
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("⌨️ [Advance Input (Wait For Input 대사)]", EditorStyles.boldLabel);
        DrawPropertyIfExists(inputWaitDelayProp, "Input Wait Delay");
        DrawPropertyIfExists(advancePromptRootProp, "Prompt Root");
        DrawPropertyIfExists(advancePromptKeyProp, "Prompt Key Icon");
        DrawPropertyIfExists(advancePromptGapProp, "Prompt Gap From Text");
        DrawPropertyIfExists(advancePromptPulseBrightnessProp, "Prompt Pulse Min Brightness");
        DrawPropertyIfExists(advancePromptPulseDurationProp, "Prompt Pulse Duration");
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // 3. Elements List
        if (null != introSceneElementsProp)
        {
            EditorGUILayout.BeginVertical(GUI.skin.box);
            EditorGUILayout.BeginHorizontal();
            introSceneElementsProp.isExpanded = EditorGUILayout.Foldout(
                introSceneElementsProp.isExpanded,
                $"🎬 Intro Scene Elements ({introSceneElementsProp.arraySize})",
                true,
                EditorStyles.foldoutHeader
            );

            if (GUILayout.Button("+ Add Element", GUILayout.Width(110)))
            {
                int newIndex = introSceneElementsProp.arraySize;
                introSceneElementsProp.InsertArrayElementAtIndex(newIndex);
                SerializedProperty newElem = introSceneElementsProp.GetArrayElementAtIndex(newIndex);
                newElem.FindPropertyRelative("startScale").vector3Value = Vector3.one;
                newElem.FindPropertyRelative("targetScale").vector3Value = Vector3.one;
                newElem.FindPropertyRelative("duration").floatValue = 3.0f;
                newElem.FindPropertyRelative("targetAlpha").floatValue = 1.0f;
            }
            EditorGUILayout.EndHorizontal();

            if (introSceneElementsProp.isExpanded)
            {
                EditorGUILayout.Space(4);
                for (int i = 0; i < introSceneElementsProp.arraySize; i++)
                {
                    SerializedProperty elementProp = introSceneElementsProp.GetArrayElementAtIndex(i);
                    DrawElementCard(elementProp, i);
                }
            }
            EditorGUILayout.EndVertical();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private static void DrawPropertyIfExists(SerializedProperty _prop, string _label)
    {
        if (null != _prop)
        {
            EditorGUILayout.PropertyField(_prop, new GUIContent(_label));
        }
    }

    private void DrawElementCard(SerializedProperty _elementProp, int _index)
    {
        SerializedProperty targetRect = _elementProp.FindPropertyRelative("targetRect");
        string elemName = (null != targetRect && null != targetRect.objectReferenceValue)
            ? targetRect.objectReferenceValue.name
            : "Empty Element";

        SerializedProperty orderProp = _elementProp.FindPropertyRelative("orderIndex");
        int orderVal = null != orderProp ? orderProp.intValue : 0;

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.BeginHorizontal();
        _elementProp.isExpanded = EditorGUILayout.Foldout(
            _elementProp.isExpanded,
            $"[Order {orderVal}] Element {_index}: [{elemName}]",
            true,
            EditorStyles.foldout
        );

        GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
        if (GUILayout.Button("X", GUILayout.Width(25), GUILayout.Height(18)))
        {
            introSceneElementsProp.DeleteArrayElementAtIndex(_index);
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            return;
        }
        GUI.backgroundColor = Color.white;
        EditorGUILayout.EndHorizontal();

        if (_elementProp.isExpanded)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.Space(2);

            // 1. Target References
            EditorGUILayout.LabelField("🎯 Target References", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(targetRect, new GUIContent("Target Rect"));
            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("canvasGroup"), new GUIContent("Canvas Group"));

            EditorGUILayout.Space(4);
            // 2. Order & Total Timing
            EditorGUILayout.LabelField("⏱️ Order & Total Timing", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(orderProp, new GUIContent("Order Index"));
            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("duration"), new GUIContent("Total Duration"));
            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("delay"), new GUIContent("Start Delay"));

            EditorGUILayout.Space(4);
            // 3. Motion Settings
            EditorGUILayout.LabelField("✨ Motion Settings", EditorStyles.boldLabel);
            SerializedProperty motionType = _elementProp.FindPropertyRelative("motionType");
            EditorGUILayout.PropertyField(motionType, new GUIContent("Motion Type"));

            if (null != motionType && (int)OpeningMotionType.Scale == motionType.enumValueIndex)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("motionDuration"), new GUIContent("Motion Duration"));
                EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("startScale"), new GUIContent("Start Scale"));
                EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("targetScale"), new GUIContent("Target Scale"));
                EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("scaleEase"), new GUIContent("Scale Ease"));
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(4);
            // 4. Fade Settings
            EditorGUILayout.LabelField("🌓 Fade Settings", EditorStyles.boldLabel);
            SerializedProperty fadeType = _elementProp.FindPropertyRelative("fadeType");
            EditorGUILayout.PropertyField(fadeType, new GUIContent("Fade Type"));

            if (null != fadeType)
            {
                OpeningFadeType fType = (OpeningFadeType)fadeType.enumValueIndex;
                if (OpeningFadeType.None != fType)
                {
                    EditorGUI.indentLevel++;
                    switch (fType)
                    {
                        case OpeningFadeType.FadeIn:
                            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("fadeInDuration"), new GUIContent("Fade In Duration"));
                            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("fadeEase"), new GUIContent("Fade Ease"));
                            break;
                        case OpeningFadeType.FadeOut:
                            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("fadeOutDuration"), new GUIContent("Fade Out Duration"));
                            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("fadeEase"), new GUIContent("Fade Ease"));
                            break;
                        case OpeningFadeType.FadeInOut:
                            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("fadeInDuration"), new GUIContent("Fade In Duration"));
                            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("fadeOutDuration"), new GUIContent("Fade Out Duration"));
                            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("fadeEase"), new GUIContent("Fade Ease"));
                            break;
                        case OpeningFadeType.Custom:
                            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("startAlpha"), new GUIContent("Start Alpha"));
                            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("targetAlpha"), new GUIContent("Target Alpha"));
                            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("fadeEase"), new GUIContent("Fade Ease"));
                            break;
                    }
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUILayout.Space(4);
            // 5. Localization & TMP (Optional)
            EditorGUILayout.LabelField("🌐 Localization & TMP (Optional)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("targetText"), new GUIContent("Target Text"));
            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("localizationEntryId"), new GUIContent("Localization Entry Id"));
            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("targetAnimator"), new GUIContent("Target Animator"));
            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("playTMPRevealBounce"), new GUIContent("Play TMP Reveal Bounce"));

            EditorGUILayout.Space(4);
            // 6. Advance Input
            EditorGUILayout.LabelField("⌨️ Advance Input", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_elementProp.FindPropertyRelative("waitForInput"), new GUIContent("Wait For Input", "켜면 Total Duration 후에 자동으로 사라지지 않고, 글자 등장이 끝난 뒤 공격 키를 눌러야 다음으로 넘어간다."));

            EditorGUILayout.Space(2);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }
}
#endif
