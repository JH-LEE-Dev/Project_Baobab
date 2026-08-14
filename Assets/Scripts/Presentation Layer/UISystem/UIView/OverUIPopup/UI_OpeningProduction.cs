using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
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
}

public class UI_OpeningProduction : MonoBehaviour
{
    [Header("Localization Settings")]
    [SerializeField] private int localizationJsonId = 16;

    // 외부 의존성
    private LocalizationManager localizationManager;
    private Action onIntroCompleteCallback;
    private Action cachedApplyLocalization;

    // 내부 의존성
    [SerializeField] private List<OpeningElementInfo> introSceneElements = new List<OpeningElementInfo>();

    private Sequence activeSequence;
    
    private TweenCallback[] cachedActivateCallbacks;
    private TweenCallback[] cachedDeactivateCallbacks;

    public void Initialize(LocalizationManager _localizationManager = null)
    {
        localizationManager = _localizationManager;
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

        onIntroCompleteCallback = _onComplete;
        activeSequence = DOTween.Sequence();

        int maxOrder = GetMaxOrderIndex();

        for (int order = 0; order <= maxOrder; order++)
        {
            if (false == HasElementsInOrder(order))
            {
                continue;
            }

            float stepDuration = GetStepDuration(order);
            if (0f >= stepDuration)
            {
                continue;
            }

            Sequence stepSequence = DOTween.Sequence();

            for (int i = 0; i < introSceneElements.Count; i++)
            {
                OpeningElementInfo elem = introSceneElements[i];
                if (order != elem.orderIndex || null == elem.targetRect)
                {
                    continue;
                }

                AppendElementToSequence(stepSequence, elem, i);
            }

            float currentSeqDuration = stepSequence.Duration(false);
            if (currentSeqDuration < stepDuration)
            {
                stepSequence.AppendInterval(stepDuration - currentSeqDuration);
            }

            activeSequence.Append(stepSequence);
        }

        activeSequence.OnComplete(HandleSequenceComplete);
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
            elem.targetAnimator.PlayRevealBounce();
        }
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

    private void OnEnable()
    {
        localizationJsonIdProp = serializedObject.FindProperty("localizationJsonId");
        introSceneElementsProp = serializedObject.FindProperty("introSceneElements");
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

            EditorGUILayout.Space(2);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }
}
#endif
