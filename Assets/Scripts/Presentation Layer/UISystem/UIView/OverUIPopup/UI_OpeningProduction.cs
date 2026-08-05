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
    private const int OpeningLocalizationJsonId = 15;

    // 외부 의존성
    private LocalizationManager localizationManager;
    private Action onIntroCompleteCallback;

    // 내부 의존성
    [SerializeField] private List<OpeningElementInfo> introSceneElements = new List<OpeningElementInfo>();

    private Sequence activeSequence;

    public void Initialize(LocalizationManager _localizationManager = null)
    {
        localizationManager = _localizationManager;
        CacheComponents();
        ApplyLocalization();
        SetAllActive(false);
    }

    public void PlayIntroScene(Action _onComplete = null)
    {
        KillActiveSequence();
        CacheComponents();
        SetAllActive(false);
        ApplyLocalization();

        onIntroCompleteCallback = _onComplete;
        activeSequence = DOTween.Sequence();

        int maxOrder = GetMaxOrderIndex();

        for (int order = 0; order <= maxOrder; order++)
        {
            if (!HasElementsInOrder(order))
            {
                continue;
            }

            float stepDuration = GetStepDuration(order);
            if (stepDuration <= 0f)
            {
                continue;
            }

            Sequence stepSequence = DOTween.Sequence();

            for (int i = 0; i < introSceneElements.Count; i++)
            {
                OpeningElementInfo elem = introSceneElements[i];
                if (elem.orderIndex != order || null == elem.targetRect)
                {
                    continue;
                }

                int elemIndex = i;
                float startTime = Mathf.Max(0f, elem.delay);
                float activeDuration = GetElementActiveDuration(elem);
                float endTime = startTime + activeDuration;

                // 1. 딜레이가 끝나는 startTime 시점에 오브젝트 활성화 및 초기값 적용
                stepSequence.InsertCallback(startTime, () => ActivateElement(elemIndex));

                // 2-1. 스케일 모션 트윈 등록
                if (elem.motionType == OpeningMotionType.Scale)
                {
                    float mDuration = elem.motionDuration > 0f ? elem.motionDuration : activeDuration;
                    Vector3 targetSc = GetSafeScale(elem.targetScale);
                    Ease ease = elem.scaleEase == Ease.Unset ? Ease.OutQuad : elem.scaleEase;

                    Tween scaleTween = elem.targetRect.DOScale(targetSc, mDuration).SetEase(ease);
                    stepSequence.Insert(startTime, scaleTween);
                }

                // 2-2. 알파 페이드 연출 등록
                if (elem.fadeType != OpeningFadeType.None && null != elem.canvasGroup)
                {
                    Ease ease = elem.fadeEase == Ease.Unset ? Ease.Linear : elem.fadeEase;

                    switch (elem.fadeType)
                    {
                        case OpeningFadeType.FadeIn:
                        {
                            float inTime = elem.fadeInDuration > 0f ? elem.fadeInDuration : activeDuration;
                            Tween fadeTween = elem.canvasGroup.DOFade(1f, inTime).SetEase(ease);
                            stepSequence.Insert(startTime, fadeTween);
                            break;
                        }

                        case OpeningFadeType.FadeOut:
                        {
                            float outTime = elem.fadeOutDuration > 0f ? elem.fadeOutDuration : activeDuration;
                            float fadeOutStartTime = Mathf.Max(startTime, endTime - outTime);
                            Tween fadeTween = elem.canvasGroup.DOFade(0f, outTime).SetEase(ease);
                            stepSequence.Insert(fadeOutStartTime, fadeTween);
                            break;
                        }

                        case OpeningFadeType.FadeInOut:
                        {
                            float inTime = elem.fadeInDuration > 0f ? elem.fadeInDuration : 0.5f;
                            float outTime = elem.fadeOutDuration > 0f ? elem.fadeOutDuration : 0.5f;
                            float holdTime = Mathf.Max(0f, activeDuration - inTime - outTime);

                            Tween fadeInTween = elem.canvasGroup.DOFade(1f, inTime).SetEase(ease);
                            stepSequence.Insert(startTime, fadeInTween);

                            float fadeOutStartTime = startTime + inTime + holdTime;
                            Tween fadeOutTween = elem.canvasGroup.DOFade(0f, outTime).SetEase(ease);
                            stepSequence.Insert(fadeOutStartTime, fadeOutTween);
                            break;
                        }

                        case OpeningFadeType.Custom:
                        {
                            Tween customFadeTween = elem.canvasGroup.DOFade(elem.targetAlpha, activeDuration).SetEase(ease);
                            stepSequence.Insert(startTime, customFadeTween);
                            break;
                        }
                    }
                }

                // 3. 해당 요소의 연출 및 유지 시간이 모두 끝나는 endTime 시점에 비활성화
                stepSequence.InsertCallback(endTime, () => DeactivateElement(elemIndex));
            }

            // 4. stepSequence의 실제 길이가 stepDuration에 미달할 때만 차이만큼 보정하여 순차 실행 확보
            float currentSeqDuration = stepSequence.Duration(false);
            if (stepDuration > currentSeqDuration)
            {
                stepSequence.AppendInterval(stepDuration - currentSeqDuration);
            }

            // 5. 메인 시퀀스에 순차적으로 Append
            activeSequence.Append(stepSequence);
        }

        activeSequence.OnComplete(HandleSequenceComplete);
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
        if (null == introSceneElements || introSceneElements.Count == 0)
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

        if (_elem.motionType == OpeningMotionType.Scale)
        {
            float mDuration = _elem.motionDuration > 0f ? _elem.motionDuration : _elem.duration;
            if (mDuration > activeDuration)
            {
                activeDuration = mDuration;
            }
        }

        if (_elem.fadeType == OpeningFadeType.FadeInOut)
        {
            float inTime = _elem.fadeInDuration > 0f ? _elem.fadeInDuration : 0.5f;
            float outTime = _elem.fadeOutDuration > 0f ? _elem.fadeOutDuration : 0.5f;
            if (inTime + outTime > activeDuration)
            {
                activeDuration = inTime + outTime;
            }
        }
        else if (_elem.fadeType == OpeningFadeType.FadeIn)
        {
            float inTime = _elem.fadeInDuration > 0f ? _elem.fadeInDuration : _elem.duration;
            if (inTime > activeDuration)
            {
                activeDuration = inTime;
            }
        }
        else if (_elem.fadeType == OpeningFadeType.FadeOut)
        {
            float outTime = _elem.fadeOutDuration > 0f ? _elem.fadeOutDuration : _elem.duration;
            if (outTime > activeDuration)
            {
                activeDuration = outTime;
            }
        }

        return activeDuration > 0f ? activeDuration : 0.1f;
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
            if (elem.orderIndex == _orderIndex && null != elem.targetRect)
            {
                float total = Mathf.Max(0f, elem.delay) + GetElementActiveDuration(elem);
                if (total > maxStepDuration)
                {
                    maxStepDuration = total;
                }
            }
        }
        return maxStepDuration;
    }

    private static Vector3 GetSafeScale(Vector3 _scale)
    {
        if (Mathf.Approximately(_scale.z, 0f))
        {
            _scale.z = 1f;
        }
        return _scale;
    }

    private void ActivateElement(int _index)
    {
        if (_index < 0 || _index >= introSceneElements.Count)
        {
            return;
        }

        OpeningElementInfo elem = introSceneElements[_index];
        if (null == elem.targetRect)
        {
            return;
        }

        elem.targetRect.gameObject.SetActive(true);

        if (elem.motionType == OpeningMotionType.Scale)
        {
            elem.targetRect.localScale = GetSafeScale(elem.startScale);
        }

        if (null != elem.canvasGroup)
        {
            elem.canvasGroup.alpha = GetInitialAlpha(elem);
        }

        if (elem.playTMPRevealBounce && null != elem.targetAnimator)
        {
            elem.targetAnimator.PlayRevealBounce();
        }
    }

    private void DeactivateElement(int _index)
    {
        if (_index < 0 || _index >= introSceneElements.Count)
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
        if (null == localizationManager || introSceneElements == null)
        {
            return;
        }

        for (int i = 0; i < introSceneElements.Count; i++)
        {
            OpeningElementInfo element = introSceneElements[i];
            if (element.localizationEntryId <= 0 || null == element.targetText)
            {
                continue;
            }

            string localizedText = localizationManager.GetText(OpeningLocalizationJsonId, element.localizationEntryId);
            if (!string.IsNullOrEmpty(localizedText))
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
                if (null == element.canvasGroup && element.fadeType != OpeningFadeType.None)
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
                if (element.motionType == OpeningMotionType.Scale)
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
        if (null == introSceneElements || introSceneElements.Count == 0)
        {
            return 0;
        }

        int maxOrder = 0;
        for (int i = 0; i < introSceneElements.Count; i++)
        {
            if (introSceneElements[i].orderIndex > maxOrder)
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
            if (introSceneElements[i].orderIndex == _orderIndex)
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
        if (activeSequence != null && activeSequence.IsActive())
        {
            activeSequence.Kill();
            activeSequence = null;
        }

        if (introSceneElements != null)
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
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(UI_OpeningProduction))]
public class UI_OpeningProductionEditor : Editor
{
    private SerializedProperty introSceneElementsProp;

    private void OnEnable()
    {
        introSceneElementsProp = serializedObject.FindProperty("introSceneElements");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        UI_OpeningProduction opening = (UI_OpeningProduction)target;

        // 1. Duration Summary Box
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("🎬 [Intro Scene Duration (Read-Only)]", EditorStyles.boldLabel);
        EditorGUILayout.Space(2);
        float introDuration = opening.CalculateIntroSceneDuration();
        EditorGUILayout.LabelField(" • 🎬 Intro Scene", $"{introDuration:F2}s ({introDuration}초)");
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(8);

        // 2. Elements List
        if (introSceneElementsProp != null)
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
        string elemName = (targetRect != null && targetRect.objectReferenceValue != null)
            ? targetRect.objectReferenceValue.name
            : "Empty Element";

        SerializedProperty orderProp = _elementProp.FindPropertyRelative("orderIndex");
        int orderVal = orderProp != null ? orderProp.intValue : 0;

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

            if (motionType != null && motionType.enumValueIndex == (int)OpeningMotionType.Scale)
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

            if (fadeType != null)
            {
                OpeningFadeType fType = (OpeningFadeType)fadeType.enumValueIndex;
                if (fType != OpeningFadeType.None)
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
