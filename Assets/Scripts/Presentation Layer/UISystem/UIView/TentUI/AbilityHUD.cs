using System.Collections.Generic;
using DG.Tweening;
using NaughtyAttributes;
using PresentationLayer.DOTweenAnimationSystem;
using PresentationLayer.UISystem.CustomNumber;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class AbilityHUD : MonoBehaviour
{
    private const float DownTickMinimumInterval = 0.04f;
    private const float LevelUpParticleReferencePixelsPerUnit = 32.0f;
    private enum EventSpriteEffectType
    {
        LevelUpSpark,
        FontSpark,
        FlowerAppear,
        Count
    }

    [Header("UI References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private FontMaker fontMakerForBar;
    [SerializeField] private FontMaker fontMakerForFlowerStack;
    [SerializeField] private ObjectMotionPlayer barFontMotionPlayer;

    [Header("Level Up Sprite Effect")]
    [SerializeField] private GameObject levelUpSpriteEffectRoot;
    [SerializeField] private Image levelUpAboveEffectImage;
    [SerializeField] private Sprite[] levelUpAboveEffectFrames;
    [SerializeField, Min(1.0f)] private float levelUpSpriteEffectFrameRate = 24.0f;

    [Header("Level Up Particle Effect")]
    [SerializeField] private Canvas abilityHUDCanvas;
    [SerializeField] private Canvas levelUpParticleCanvas;
    [SerializeField] private GameObject levelUpParticleLeftRoot;
    [SerializeField] private GameObject levelUpParticleRightRoot;
    [SerializeField] private string levelUpParticleSortingLayer = "HUD";
    [SerializeField] private int abilityHUDSortingOrder = 2;
    [SerializeField] private int levelUpParticleSortingOrder = 1;
    [SerializeField, Min(0.0f)] private float levelUpParticleEmissionDuration = 1.5f;

    [Header("Event Sprite Effects")]
    [SerializeField] private Image levelUpSparkEffectImage;
    [SerializeField] private Sprite[] levelUpSparkEffectFrames;
    [SerializeField] private Image fontSparkEffectImage;
    [SerializeField] private Sprite[] fontSparkEffectFrames;
    [SerializeField] private Image flowerAppearEffectImage;
    [SerializeField] private Sprite[] flowerAppearEffectFrames;
    [SerializeField] private Vector2 flowerAppearEffectOffset = new Vector2(0.0f, 6.0f);
    [SerializeField, Min(1.0f)] private float eventSpriteEffectFrameRate = 30.0f;

    [Header("Ability Bar")]
    [SerializeField] private int maxExperience = 50;
    [SerializeField] private int currentExperience;
    [SerializeField] private string barFontExperienceMotionTag = "Experience";
    [SerializeField] private bool resetBarFontMotionBeforePlay = true;

    [Header("Reset Effect")]
    [SerializeField] private float resetSquashDuration = 0.25f;
    [SerializeField] private Vector3 resetSquashScale = new Vector3(1.25f, 0.8f, 1.0f);
    [SerializeField] private float resetColorDuration = 1.5f;
    [SerializeField] private float resetColorFlashInterval = 0.12f;
    [SerializeField] private Color resetHighlightYellow = new Color(1.0f, 0.95f, 0.2f, 1.0f);
    [SerializeField] private Color resetHighlightRed = new Color(1.0f, 0.45f, 0.35f, 1.0f);
    [SerializeField] private Color resetFillHighlightYellow = new Color(1.0f, 0.95f, 0.2f, 1.0f);
    [SerializeField] private float resetDrainDuration = 1.5f;
    [SerializeField] private float resetShakeStrength = 1.0f;
    [SerializeField] private int resetShakeVibrato = 40;

    [Header("Flower Stack")]
    [SerializeField] private int flowerStack;
    [SerializeField] private RectTransform flowerObjectPivot;
    [SerializeField] private GameObject flowerVisualPrefab;
    [SerializeField] private float flowerObjectMinX = -55.0f;
    [SerializeField] private float flowerObjectMaxX = 55.0f;
    [SerializeField] private int flowerObjectMinY = 0;
    [SerializeField] private int flowerObjectMaxY = 2;
    [SerializeField] private float flowerObjectRandomOffsetX = 2.0f;
    [SerializeField] private float flowerStackPopScale = 1.8f;
    [SerializeField] private float flowerStackPopDuration = 0.45f;
    [SerializeField, HideInInspector] private List<Vector2> flowerObjectPositions = new List<Vector2>();

    [Header("Debug Effect Buttons")]
    [SerializeField] private int debugEffectExperience = 10;
    [SerializeField] private int debugEffectMaxExperience = 50;
    [SerializeField] private int debugEffectFlowerStack = 3;

    public int CurrentExperience => currentExperience;
    public int MaxExperience => maxExperience;
    public int FlowerStack => flowerStack;

    private Sequence resetEffectSequence;
    private Sequence flowerStackPopSequence;
    private Tween levelUpSpriteEffectTween;
    private Tween levelUpParticleStopTween;
    private readonly Tween[] eventSpriteEffectTweens = new Tween[(int)EventSpriteEffectType.Count];
    private readonly Image[] activeEventSpriteEffectImages = new Image[(int)EventSpriteEffectType.Count];
    private readonly Sprite[][] activeEventSpriteEffectFrames = new Sprite[(int)EventSpriteEffectType.Count][];
    private readonly float[] activeEventSpriteEffectFrameRates = new float[(int)EventSpriteEffectType.Count];
    private readonly int[] activeEventSpriteEffectLastFrameIndices = new int[(int)EventSpriteEffectType.Count];
    private readonly List<RectTransform> spawnedFlowerObjects = new List<RectTransform>();
    private readonly List<RectTransform> pooledFlowerObjects = new List<RectTransform>();
    private RectTransform barFontRectTransform;
    private RectTransform flowerStackFontRectTransform;
    private Vector3 barFontInitialScale = Vector3.one;
    private Vector2 barFontInitialAnchoredPosition;
    private Vector3 flowerStackFontInitialScale = Vector3.one;
    private Vector2 flowerStackFontInitialAnchoredPosition;
    private Color fillInitialColor = Color.white;
    private AudioHandle downStartSoundHandle = AudioHandle.Invalid;
    private int resetDrainTargetExperience;
    private int resetTargetFlowerStack;
    private int resetStartExperience;
    private float resetFlashTweenDuration;
    private float levelUpSpriteEffectActiveFrameRate;
    private int levelUpSpriteEffectLastFrameIndex;
    private float lastDownTickSoundElapsed = float.NegativeInfinity;
    private bool playExperienceMotionAfterReset;
    private bool playFlowerDangleAfterReset;
    private ParticleSystem[] levelUpParticleSystems;
    private bool deferCanvasSortingSettings;

    private void Awake()
    {
        // Canvas 설정은 Awake 중에 적용하면 SendMessage 경고가 발생하므로 OnEnable로 미룬다.
        deferCanvasSortingSettings = true;
        try
        {
            BindReferencesIfNeeded();
        }
        finally
        {
            deferCanvasSortingSettings = false;
        }

        SortSerializedSpriteEffectFrames();

        HideLevelUpSpriteEffect();
        StopLevelUpParticleEffect();
        HideIdleEventSpriteEffects();
        RefreshOrSchedule();
    }

    private void OnEnable()
    {
        BindReferencesIfNeeded();

        if (Application.isPlaying && (null == levelUpSpriteEffectTween || false == levelUpSpriteEffectTween.IsActive()))
            HideLevelUpSpriteEffect();

        if (Application.isPlaying && (null == levelUpParticleStopTween || false == levelUpParticleStopTween.IsActive()))
            StopLevelUpParticleEffect();

        HideIdleEventSpriteEffects();

        RefreshOrSchedule();
    }

    private void OnTransformParentChanged()
    {
        BindReferencesIfNeeded();
    }

    private void OnValidate()
    {
        SortSerializedSpriteEffectFrames();
        maxExperience = Mathf.Max(1, maxExperience);
        currentExperience = Mathf.Clamp(currentExperience, 0, maxExperience);
        flowerStack = Mathf.Max(0, flowerStack);

        // OnValidate 중 Canvas 설정을 건드리면 SendMessage 경고가 발생하므로 지연 적용한다.
        deferCanvasSortingSettings = true;
        try
        {
            BindReferencesIfNeeded();
            RefreshOrSchedule();
        }
        finally
        {
            deferCanvasSortingSettings = false;
        }
    }

    public void SetExperience(int _currentExperience)
    {
        SetExperience(_currentExperience, maxExperience);
    }

    public void SetExperience(int _currentExperience, int _maxExperience)
    {
        StopResetExperienceEffect(true);
        ClearDeferredExperienceMotion();
        maxExperience = Mathf.Max(1, _maxExperience);
        currentExperience = ClampExperience(_currentExperience);
        RefreshAbilityBar();
    }

    public void SetState(int _currentExperience, int _maxExperience, int _flowerStack)
    {
        StopResetExperienceEffect(true);
        ClearDeferredExperienceMotion();
        maxExperience = Mathf.Max(1, _maxExperience);
        currentExperience = ClampExperience(_currentExperience);
        flowerStack = Mathf.Max(0, _flowerStack);
        Refresh();
    }

    public void CancelPresentation()
    {
        StopResetExperienceEffect(true);
        ClearDeferredExperienceMotion();
        StopLevelUpSpriteEffect();
        StopLevelUpParticleEffect();
        StopAllEventSpriteEffects();
        StopDownStartSound();
        StopBarFontExperienceMotion();
        StopFlowerStackPopEffect();
        StopFlowerVisualPresentationEffects();
    }

    public void SetExperience_Effect(int _currentExperience)
    {
        SetExperience_Effect(_currentExperience, maxExperience);
    }

    public void SetExperience_Effect(int _currentExperience, int _maxExperience)
    {
        maxExperience = Mathf.Max(1, _maxExperience);
        int _clampedExperience = ClampExperience(_currentExperience);

        if (IsResetExperienceEffectPlaying())
        {
            resetDrainTargetExperience = _clampedExperience;
            playExperienceMotionAfterReset = true;
            playFlowerDangleAfterReset = true;
            return;
        }

        int _previousExperience = currentExperience;
        currentExperience = _clampedExperience;

        // TODO: Add experience number/bar animation timing here.
        RefreshAbilityBar();
        PlayBarFontExperienceMotion();

        if (currentExperience > _previousExperience)
            PlayFlowerDangleEffect();
    }

    public void ResetExperience_Effect()
    {
        ResetExperience_Effect(0, maxExperience, flowerStack + 1);
    }

    public void ResetExperience_Effect(int _targetExperience, int _maxExperience, int _targetFlowerStack)
    {
        maxExperience = Mathf.Max(1, _maxExperience);
        resetDrainTargetExperience = ClampExperience(_targetExperience);
        resetTargetFlowerStack = Mathf.Max(0, _targetFlowerStack);
        PlayResetExperienceEffect();
    }

    public void AddExperience(int _amount)
    {
        SetExperience(currentExperience + _amount);
    }

    public void SetFlowerStack(int _flowerStack)
    {
        int _targetFlowerStack = Mathf.Max(0, _flowerStack);
        if (IsResetExperienceEffectPlaying())
        {
            resetTargetFlowerStack = _targetFlowerStack;
            return;
        }

        SetFlowerStackImmediate(_targetFlowerStack);
    }

    public void SetFlowerStack_Effect(int _flowerStack)
    {
        int _targetFlowerStack = Mathf.Max(0, _flowerStack);
        if (IsResetExperienceEffectPlaying())
        {
            resetTargetFlowerStack = _targetFlowerStack;
            return;
        }

        // TODO: Add flower stack change animation timing here.
        SetFlowerStackImmediate(_targetFlowerStack);
    }

    [Button("SetExperience_Effect")]
    private void DebugSetExperienceEffect()
    {
        SetExperience_Effect(debugEffectExperience, debugEffectMaxExperience);
    }

    [Button("ResetExperience_Effect")]
    private void DebugResetExperienceEffect()
    {
        ResetExperience_Effect();
    }

    [Button("SetFlowerStack_Effect")]
    private void DebugSetFlowerStackEffect()
    {
        SetFlowerStack_Effect(debugEffectFlowerStack);
    }

    public void Refresh()
    {
        RefreshAbilityBar();
        RefreshFlowerStack();
    }

    private void RefreshOrSchedule()
    {
#if UNITY_EDITOR
        if (false == Application.isPlaying)
        {
            ScheduleEditorRefresh();
            return;
        }
#endif

        Refresh();
    }

#if UNITY_EDITOR
    private void ScheduleEditorRefresh()
    {
        EditorApplication.delayCall -= DelayedEditorRefresh;
        EditorApplication.delayCall += DelayedEditorRefresh;
    }

    private void DelayedEditorRefresh()
    {
        EditorApplication.delayCall -= DelayedEditorRefresh;

        if (null == this || Application.isPlaying)
            return;

        BindReferencesIfNeeded();
        Refresh();
    }
#endif

    private void RefreshAbilityBar()
    {
        if (null != fillImage)
        {
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = maxExperience <= 0 ? 0.0f : Mathf.Clamp01((float)currentExperience / maxExperience);
        }

        if (null != fontMakerForBar)
            fontMakerForBar.SetFraction(currentExperience, maxExperience);
    }

    private int ClampExperience(int _experience)
    {
        return Mathf.Clamp(_experience, 0, maxExperience);
    }

    private void PlayResetExperienceEffect()
    {
        BindReferencesIfNeeded();
        StopResetExperienceEffect(true);
        StopBarFontExperienceMotion();
        playExperienceMotionAfterReset = false;
        playFlowerDangleAfterReset = false;

        if (null == fontMakerForBar)
        {
            currentExperience = ClampExperience(resetDrainTargetExperience);
            SetFlowerStack(resetTargetFlowerStack);
            Refresh();
            return;
        }

        barFontRectTransform = fontMakerForBar.GetComponent<RectTransform>();
        if (null != barFontRectTransform)
        {
            barFontInitialScale = barFontRectTransform.localScale;
            barFontInitialAnchoredPosition = barFontRectTransform.anchoredPosition;
        }

        flowerStackFontRectTransform = null == fontMakerForFlowerStack ? null : fontMakerForFlowerStack.GetComponent<RectTransform>();
        if (null != flowerStackFontRectTransform)
        {
            flowerStackFontInitialScale = flowerStackFontRectTransform.localScale;
            flowerStackFontInitialAnchoredPosition = flowerStackFontRectTransform.anchoredPosition;
        }

        // The reset begins from a visually full bar using the newly applied limit.
        // The previous limit can differ, so using the old absolute experience here
        // would make the fill jump before the drain starts.
        resetStartExperience = maxExperience;
        currentExperience = resetStartExperience;
        RefreshAbilityBar();
        lastDownTickSoundElapsed = float.NegativeInfinity;

        fillInitialColor = null == fillImage ? Color.white : fillImage.color;
        ApplyResetFlashColors(0.0f, 0.0f);

        resetEffectSequence = DOTween.Sequence();
        resetEffectSequence.AppendCallback(PlayResetStartEffects);
        resetEffectSequence.Join(BuildResetSquashTween());
        resetEffectSequence.Join(BuildResetColorFlashTween());
        resetEffectSequence.AppendCallback(PlayFlowerStackGrowEffect);
        resetEffectSequence.AppendCallback(PlayDownStartSound);
        resetEffectSequence.Append(DOVirtual.Float(0.0f, 1.0f, Mathf.Max(0.0f, resetDrainDuration), UpdateResetDrain).SetEase(Ease.Linear));
        resetEffectSequence.OnKill(RestoreResetExperienceEffectState);
        resetEffectSequence.OnComplete(CompleteResetEffect);
    }

    private void PlayResetStartEffects()
    {
        Sound.PlayUI(SoundID.AbilityHUDLevelUp);
        PlayLevelUpSpriteEffect();
        PlayLevelUpParticleEffect();
        PlayEventSpriteEffect(EventSpriteEffectType.LevelUpSpark);

        if (null != barFontRectTransform)
            barFontRectTransform.DOShakeAnchorPos(
                Mathf.Max(0.0f, resetColorDuration + resetDrainDuration),
                resetShakeStrength,
                Mathf.Max(1, resetShakeVibrato),
                90.0f,
                false,
                true);
    }

    private void UpdateResetDrain(float _progress)
    {
        int _targetExperience = ClampExperience(resetDrainTargetExperience);
        int _previousExperience = currentExperience;
        currentExperience = Mathf.RoundToInt(Mathf.Lerp(resetStartExperience, _targetExperience, _progress));
        TryPlayDownTickSound(
            _previousExperience,
            currentExperience,
            resetDrainDuration * _progress);
        ApplyResetFlashColors(resetColorDuration + (resetDrainDuration * _progress), _progress);
        RefreshAbilityBar();
    }

    private void CompleteResetEffect()
    {
        currentExperience = ClampExperience(resetDrainTargetExperience);
        RefreshAbilityBar();
        RestoreResetExperienceEffectState();
        resetEffectSequence = null;
        ScheduleDeferredExperienceMotion();
    }

    private Tween BuildResetSquashTween()
    {
        if (null == barFontRectTransform)
            return DOVirtual.DelayedCall(Mathf.Max(0.0f, resetSquashDuration), DoNothing);

        float _halfDuration = Mathf.Max(0.0f, resetSquashDuration) * 0.5f;
        Vector3 _targetScale = new Vector3(
            barFontInitialScale.x * resetSquashScale.x,
            barFontInitialScale.y * resetSquashScale.y,
            barFontInitialScale.z * resetSquashScale.z);

        return DOTween.Sequence()
            .Append(barFontRectTransform.DOScale(_targetScale, _halfDuration).SetEase(Ease.OutBack))
            .Append(barFontRectTransform.DOScale(barFontInitialScale, _halfDuration).SetEase(Ease.OutBack));
    }

    private void PlayFlowerStackGrowEffect()
    {
        int _previousFlowerStack = flowerStack;
        FlowerVisual _newFlowerVisual = AddFlowerStackFromReset();
        if (flowerStack != _previousFlowerStack)
            PlayEventSpriteEffect(EventSpriteEffectType.FontSpark);

        if (null != _newFlowerVisual)
        {
            Sound.PlayUI(SoundID.AbilityHUDFlowerGrow);
            PlayEventSpriteEffect(
                EventSpriteEffectType.FlowerAppear,
                _newFlowerVisual.transform as RectTransform,
                flowerAppearEffectOffset);
        }

        if (null == flowerStackFontRectTransform)
        {
            _newFlowerVisual?.PlayGrow();
            return;
        }

        StopFlowerStackPopEffect();
        flowerStackFontRectTransform.DOKill(false);
        flowerStackFontRectTransform.localScale = flowerStackFontInitialScale;
        flowerStackFontRectTransform.anchoredPosition = flowerStackFontInitialAnchoredPosition;

        flowerStackPopSequence = DOTween.Sequence();
        flowerStackPopSequence.Append(flowerStackFontRectTransform.DOScale(flowerStackFontInitialScale * flowerStackPopScale, flowerStackPopDuration * 0.22f).SetEase(Ease.OutExpo));
        flowerStackPopSequence.Append(flowerStackFontRectTransform.DOScale(flowerStackFontInitialScale * 0.86f, flowerStackPopDuration * 0.22f).SetEase(Ease.InOutSine));
        flowerStackPopSequence.Append(flowerStackFontRectTransform.DOScale(flowerStackFontInitialScale * 1.12f, flowerStackPopDuration * 0.20f).SetEase(Ease.InOutSine));
        flowerStackPopSequence.Append(flowerStackFontRectTransform.DOScale(flowerStackFontInitialScale, flowerStackPopDuration * 0.36f).SetEase(Ease.OutBack));
        flowerStackPopSequence.OnComplete(CompleteFlowerStackPopEffect);

        _newFlowerVisual?.PlayGrow();
    }

    private void CompleteFlowerStackPopEffect()
    {
        flowerStackPopSequence = null;
    }

    private void StopFlowerStackPopEffect()
    {
        bool _wasPlaying = null != flowerStackPopSequence && flowerStackPopSequence.IsActive();
        if (_wasPlaying)
            flowerStackPopSequence.Kill(false);

        flowerStackPopSequence = null;

        if (_wasPlaying && null != flowerStackFontRectTransform)
        {
            flowerStackFontRectTransform.localScale = flowerStackFontInitialScale;
            flowerStackFontRectTransform.anchoredPosition = flowerStackFontInitialAnchoredPosition;
        }
    }

    private void StopFlowerVisualPresentationEffects()
    {
        CollectFlowerObjectLists();
        StopFlowerVisualPresentationEffects(spawnedFlowerObjects);
        StopFlowerVisualPresentationEffects(pooledFlowerObjects);
    }

    private static void StopFlowerVisualPresentationEffects(List<RectTransform> _flowerObjects)
    {
        for (int i = 0; i < _flowerObjects.Count; i++)
        {
            RectTransform _flowerObject = _flowerObjects[i];
            if (null == _flowerObject)
                continue;

            _flowerObject.GetComponent<FlowerVisual>()?.StopPresentationEffects();
        }
    }

    private void TryPlayDownTickSound(int _previousExperience, int _currentExperience, float _elapsed)
    {
        if (_currentExperience >= _previousExperience ||
            _elapsed - lastDownTickSoundElapsed < DownTickMinimumInterval)
        {
            return;
        }

        lastDownTickSoundElapsed = _elapsed;
        Sound.PlayUI(SoundID.AbilityHUDDownTick);
    }

    private void PlayDownStartSound()
    {
        StopDownStartSound();
        downStartSoundHandle = Sound.PlayTracked(
            SoundID.AbilityHUDDownStart,
            Vector3.zero,
            1.0f,
            false);
    }

    private void StopDownStartSound()
    {
        if (false == downStartSoundHandle.IsValid)
            return;

        Sound.StopTracked(downStartSoundHandle);
        downStartSoundHandle = AudioHandle.Invalid;
    }

    private Tween BuildResetColorFlashTween()
    {
        resetFlashTweenDuration = Mathf.Max(0.0f, resetColorDuration);
        return DOVirtual.Float(0.0f, resetFlashTweenDuration, resetFlashTweenDuration, UpdateResetFlashColors)
            .SetEase(Ease.Linear)
            .OnComplete(CompleteResetFlashColors);
    }

    private void UpdateResetFlashColors(float _elapsedTime)
    {
        ApplyResetFlashColors(_elapsedTime, 0.0f);
    }

    private void CompleteResetFlashColors()
    {
        ApplyResetFlashColors(resetFlashTweenDuration, 0.0f);
    }

    private static void DoNothing()
    {
    }

    private void ApplyResetFlashColors(float _elapsedTime, float _whiteBlend)
    {
        fontMakerForBar?.SetGlyphColor(GetResetFontFlashColor(_elapsedTime, _whiteBlend));

        if (null != fillImage)
            fillImage.color = GetResetFillFlashColor(_elapsedTime, _whiteBlend);
    }

    private Color GetResetFontFlashColor(float _elapsedTime, float _whiteBlend)
    {
        float _flashProgress = GetResetFlashProgress(_elapsedTime);
        Color _flashColor = Color.Lerp(resetHighlightYellow, resetHighlightRed, _flashProgress);
        return Color.Lerp(_flashColor, Color.white, Mathf.Clamp01(_whiteBlend));
    }

    private Color GetResetFillFlashColor(float _elapsedTime, float _whiteBlend)
    {
        float _flashProgress = GetResetFlashProgress(_elapsedTime);
        Color _highlightColor = MultiplyColor(fillInitialColor, resetFillHighlightYellow);
        Color _flashColor = Color.Lerp(fillInitialColor, _highlightColor, _flashProgress);
        return Color.Lerp(_flashColor, fillInitialColor, Mathf.Clamp01(_whiteBlend));
    }

    private float GetResetFlashProgress(float _elapsedTime)
    {
        float _flashInterval = Mathf.Max(0.01f, resetColorFlashInterval);
        return Mathf.PingPong(_elapsedTime / _flashInterval, 1.0f);
    }

    private Color MultiplyColor(Color _left, Color _right)
    {
        return new Color(
            _left.r * _right.r,
            _left.g * _right.g,
            _left.b * _right.b,
            _left.a * _right.a);
    }

    private bool IsResetExperienceEffectPlaying()
    {
        return null != resetEffectSequence && resetEffectSequence.IsActive();
    }

    private void StopResetExperienceEffect(bool _restoreState)
    {
        bool _wasPlaying = null != resetEffectSequence && resetEffectSequence.IsActive();

        if (_wasPlaying)
            resetEffectSequence.Kill(false);

        resetEffectSequence = null;

        if (_restoreState && _wasPlaying)
            RestoreResetExperienceEffectState();

        if (_wasPlaying == false)
        {
            playExperienceMotionAfterReset = false;
            playFlowerDangleAfterReset = false;
        }
    }

    private void RestoreResetExperienceEffectState()
    {
        StopLevelUpSpriteEffect();
        StopLevelUpParticleEffect();
        StopAllEventSpriteEffects();
        StopDownStartSound();
        StopBarFontExperienceMotion();

        if (null != barFontRectTransform)
        {
            barFontRectTransform.DOKill(false);
            barFontRectTransform.localScale = barFontInitialScale;
            barFontRectTransform.anchoredPosition = barFontInitialAnchoredPosition;
        }

        if (null != flowerStackFontRectTransform)
        {
            StopFlowerStackPopEffect();
            flowerStackFontRectTransform.DOKill(false);
            flowerStackFontRectTransform.localScale = flowerStackFontInitialScale;
            flowerStackFontRectTransform.anchoredPosition = flowerStackFontInitialAnchoredPosition;
        }

        StopFlowerVisualPresentationEffects();

        fontMakerForBar?.ResetGlyphColor();
        if (null != fillImage)
            fillImage.color = fillInitialColor;
    }

    private void RefreshFlowerStack()
    {
        if (null != fontMakerForFlowerStack)
            fontMakerForFlowerStack.SetNumber(flowerStack);

        if (Application.isPlaying)
            SyncFlowerObjects();
#if UNITY_EDITOR
        else
            ScheduleEditorFlowerObjectSync();
#endif
    }

    private void SetFlowerStackImmediate(int _flowerStack)
    {
        flowerStack = Mathf.Max(0, _flowerStack);
        RefreshFlowerStack();
    }

#if UNITY_EDITOR
    private void ScheduleEditorFlowerObjectSync()
    {
        EditorApplication.delayCall -= DelayedEditorFlowerObjectSync;
        EditorApplication.delayCall += DelayedEditorFlowerObjectSync;
    }

    private void DelayedEditorFlowerObjectSync()
    {
        EditorApplication.delayCall -= DelayedEditorFlowerObjectSync;

        if (null == this || Application.isPlaying)
            return;

        BindReferencesIfNeeded();
        SyncFlowerObjects();
    }
#endif

    private void SyncFlowerObjects()
    {
        if (null == flowerObjectPivot)
            return;

        if (null == flowerVisualPrefab)
            return;

        flowerStack = Mathf.Max(0, flowerStack);
        EnsureFlowerObjectPositionCount(flowerStack);
        CollectFlowerObjectLists();

        for (int i = spawnedFlowerObjects.Count - 1; i >= flowerStack; i--)
            ReleaseFlowerObject(spawnedFlowerObjects[i]);

        CollectFlowerObjectLists();

        while (spawnedFlowerObjects.Count < flowerStack)
        {
            RectTransform _flowerObject = CreateFlowerObject(spawnedFlowerObjects.Count);
            if (null == _flowerObject)
                break;

            spawnedFlowerObjects.Add(_flowerObject);
        }

        int _visibleCount = Mathf.Min(flowerStack, spawnedFlowerObjects.Count);
        for (int i = 0; i < _visibleCount; i++)
        {
            RectTransform _flowerObject = spawnedFlowerObjects[i];
            if (null == _flowerObject)
                continue;

            _flowerObject.name = GetFlowerObjectName(i);
            _flowerObject.SetSiblingIndex(i);
            _flowerObject.anchorMin = new Vector2(0.5f, 0.5f);
            _flowerObject.anchorMax = new Vector2(0.5f, 0.5f);
            _flowerObject.pivot = new Vector2(0.5f, 0.5f);
            _flowerObject.anchoredPosition = flowerObjectPositions[i];
            ConfigureFlowerObject(_flowerObject, i);
        }

        SortFlowerObjectsByY();
    }

    private void EnsureFlowerObjectPositionCount(int _targetCount)
    {
        while (flowerObjectPositions.Count > _targetCount)
            flowerObjectPositions.RemoveAt(flowerObjectPositions.Count - 1);

        while (flowerObjectPositions.Count < _targetCount)
            flowerObjectPositions.Add(CreateNextFlowerObjectPosition());
    }

    private Vector2 CreateNextFlowerObjectPosition()
    {
        float _centerX = GetWidestFlowerGapCenterX();
        float _randomX = Random.Range(-flowerObjectRandomOffsetX, flowerObjectRandomOffsetX);
        float _x = Mathf.Round(Mathf.Clamp(_centerX + _randomX, flowerObjectMinX, flowerObjectMaxX));
        float _y = Mathf.Round(Random.Range(flowerObjectMinY, flowerObjectMaxY + 1));
        return new Vector2(_x, Mathf.Clamp(_y, flowerObjectMinY, flowerObjectMaxY));
    }

    private float GetWidestFlowerGapCenterX()
    {
        if (0 == flowerObjectPositions.Count)
            return Mathf.Round((flowerObjectMinX + flowerObjectMaxX) * 0.5f);

        List<float> _sortedXPositions = new List<float>(flowerObjectPositions.Count);
        for (int i = 0; i < flowerObjectPositions.Count; i++)
            _sortedXPositions.Add(Mathf.Clamp(flowerObjectPositions[i].x, flowerObjectMinX, flowerObjectMaxX));

        _sortedXPositions.Sort();

        float _bestLeft = flowerObjectMinX;
        float _bestRight = _sortedXPositions[0];
        float _bestWidth = _bestRight - _bestLeft;

        for (int i = 0; i < _sortedXPositions.Count - 1; i++)
        {
            float _left = _sortedXPositions[i];
            float _right = _sortedXPositions[i + 1];
            float _width = _right - _left;
            if (_width <= _bestWidth)
                continue;

            _bestLeft = _left;
            _bestRight = _right;
            _bestWidth = _width;
        }

        float _lastWidth = flowerObjectMaxX - _sortedXPositions[_sortedXPositions.Count - 1];
        if (_lastWidth > _bestWidth)
        {
            _bestLeft = _sortedXPositions[_sortedXPositions.Count - 1];
            _bestRight = flowerObjectMaxX;
        }

        return Mathf.Round((_bestLeft + _bestRight) * 0.5f);
    }

    private void CollectFlowerObjectLists()
    {
        spawnedFlowerObjects.Clear();
        pooledFlowerObjects.Clear();

        if (null == flowerObjectPivot)
            return;

        for (int i = 0; i < flowerObjectPivot.childCount; i++)
        {
            Transform _child = flowerObjectPivot.GetChild(i);
            bool _isFlowerObject = _child.name.StartsWith("FlowerObject_");
            bool _isPooledFlowerObject = _child.name.StartsWith("PooledFlowerObject_");
            if (false == _isFlowerObject && false == _isPooledFlowerObject)
                continue;

            if (_child is RectTransform _rectTransform && _isFlowerObject && _child.gameObject.activeSelf)
                spawnedFlowerObjects.Add(_rectTransform);

            if (_child is RectTransform _pooledRectTransform && false == _child.gameObject.activeSelf)
                pooledFlowerObjects.Add(_pooledRectTransform);
        }

        spawnedFlowerObjects.Sort(CompareFlowerObjectIndex);
    }

    private void CollectSpawnedFlowerObjects()
    {
        CollectFlowerObjectLists();
    }

    private RectTransform CreateFlowerObject(int _index)
    {
        RectTransform _pooledFlowerObject = GetPooledFlowerObject();
        if (null != _pooledFlowerObject)
        {
            _pooledFlowerObject.gameObject.SetActive(true);
            _pooledFlowerObject.SetParent(flowerObjectPivot, false);
            _pooledFlowerObject.name = GetFlowerObjectName(_index);
            ConfigureFlowerObject(_pooledFlowerObject, _index);
            return _pooledFlowerObject;
        }

        GameObject _flowerObject = null;

#if UNITY_EDITOR
        if (false == Application.isPlaying)
            _flowerObject = PrefabUtility.InstantiatePrefab(flowerVisualPrefab, flowerObjectPivot) as GameObject;
#endif

        if (null == _flowerObject)
            _flowerObject = Instantiate(flowerVisualPrefab, flowerObjectPivot, false);

        if (null == _flowerObject)
            return null;

        _flowerObject.name = GetFlowerObjectName(_index);
        RectTransform _flowerRectTransform = _flowerObject.transform as RectTransform;
        ConfigureFlowerObject(_flowerRectTransform, _index);
        return _flowerRectTransform;
    }

    private RectTransform GetPooledFlowerObject()
    {
        for (int i = pooledFlowerObjects.Count - 1; i >= 0; i--)
        {
            RectTransform _flowerObject = pooledFlowerObjects[i];
            pooledFlowerObjects.RemoveAt(i);

            if (null != _flowerObject)
                return _flowerObject;
        }

        return null;
    }

    private void ConfigureFlowerObject(RectTransform _flowerObject, int _index)
    {
        if (null == _flowerObject)
            return;

        FlowerVisual _flowerVisual = _flowerObject.GetComponent<FlowerVisual>();
        if (null != _flowerVisual)
            _flowerVisual.SetBottomVariant(_index);
    }

    private void SortFlowerObjectsByY()
    {
        spawnedFlowerObjects.Sort(CompareFlowerObjectSorting);

        for (int i = 0; i < spawnedFlowerObjects.Count; i++)
        {
            if (null != spawnedFlowerObjects[i])
                spawnedFlowerObjects[i].SetSiblingIndex(i);
        }

        spawnedFlowerObjects.Sort(CompareFlowerObjectIndex);
    }

    private int CompareFlowerObjectSorting(RectTransform _left, RectTransform _right)
    {
        if (null == _left && null == _right)
            return 0;

        if (null == _left)
            return -1;

        if (null == _right)
            return 1;

        int _yCompare = _right.anchoredPosition.y.CompareTo(_left.anchoredPosition.y);
        return 0 != _yCompare ? _yCompare : GetFlowerObjectIndex(_left).CompareTo(GetFlowerObjectIndex(_right));
    }

    private void PlayFlowerDangleEffect()
    {
        CollectFlowerObjectLists();

        for (int i = 0; i < spawnedFlowerObjects.Count; i++)
        {
            if (null == spawnedFlowerObjects[i])
                continue;

            FlowerVisual _flowerVisual = spawnedFlowerObjects[i].GetComponent<FlowerVisual>();
            if (null != _flowerVisual)
                _flowerVisual.PlayDangle();
        }
    }

    private FlowerVisual AddFlowerStackFromReset()
    {
        int _previousFlowerStack = flowerStack;
        int _targetFlowerStack = Mathf.Max(0, resetTargetFlowerStack);
        SetFlowerStackImmediate(_targetFlowerStack);

        if (_targetFlowerStack <= _previousFlowerStack)
            return null;

        int _newFlowerIndex = _targetFlowerStack - 1;
        CollectFlowerObjectLists();

        for (int i = 0; i < spawnedFlowerObjects.Count; i++)
        {
            if (GetFlowerObjectIndex(spawnedFlowerObjects[i]) != _newFlowerIndex)
                continue;

            return spawnedFlowerObjects[i].GetComponent<FlowerVisual>();
        }

        return null;
    }

    private void ReleaseFlowerObject(RectTransform _flowerObject)
    {
        if (null == _flowerObject)
            return;

        if (Application.isPlaying)
        {
            _flowerObject.name = $"PooledFlowerObject_{pooledFlowerObjects.Count:00}";
            _flowerObject.gameObject.SetActive(false);
            pooledFlowerObjects.Add(_flowerObject);
            return;
        }

        DestroyImmediate(_flowerObject.gameObject);
    }

    private int CompareFlowerObjectIndex(RectTransform _left, RectTransform _right)
    {
        return GetFlowerObjectIndex(_left).CompareTo(GetFlowerObjectIndex(_right));
    }

    private int GetFlowerObjectIndex(RectTransform _rectTransform)
    {
        if (null == _rectTransform)
            return int.MaxValue;

        const string _prefix = "FlowerObject_";
        string _name = _rectTransform.name;
        if (false == _name.StartsWith(_prefix))
            return int.MaxValue;

        string _indexText = _name.Substring(_prefix.Length);
        return int.TryParse(_indexText, out int _index) ? _index : int.MaxValue;
    }

    private string GetFlowerObjectName(int _index)
    {
        return $"FlowerObject_{_index:00}";
    }

    private void BindReferencesIfNeeded()
    {
        if (null == fillImage)
            fillImage = FindChildComponent<Image>("Fill");

        if (null == fontMakerForBar)
            fontMakerForBar = FindChildComponent<FontMaker>("FontMaker_For_Bar");

        if (null == fontMakerForFlowerStack)
            fontMakerForFlowerStack = FindChildComponent<FontMaker>("FontMaker_For_FlowerStack");

        if (null == flowerObjectPivot)
            flowerObjectPivot = FindChildComponent<RectTransform>("FlowerObjectPivot");

        if (null == barFontMotionPlayer)
            barFontMotionPlayer = GetComponent<ObjectMotionPlayer>();

        if (null == barFontMotionPlayer)
            barFontMotionPlayer = FindChildComponent<ObjectMotionPlayer>("Motions");

        if (null == levelUpSpriteEffectRoot)
        {
            Transform _effectRoot = FindChild(transform, "VFX_Sprite");
            levelUpSpriteEffectRoot = null == _effectRoot ? null : _effectRoot.gameObject;
        }

        if (null == levelUpAboveEffectImage)
            levelUpAboveEffectImage = FindChildComponent<Image>("Above_AboveEffect");

        if (null == levelUpParticleCanvas)
            levelUpParticleCanvas = FindChildComponent<Canvas>("LevelUpParticleCanvas");

        if (null == abilityHUDCanvas)
            abilityHUDCanvas = GetComponent<Canvas>();

        if (null == levelUpParticleLeftRoot)
        {
            Transform _leftParticleRoot = FindChild(transform, "VFX_LevelUpPop_Left");
            levelUpParticleLeftRoot = null == _leftParticleRoot ? null : _leftParticleRoot.gameObject;
        }

        if (null == levelUpParticleRightRoot)
        {
            Transform _rightParticleRoot = FindChild(transform, "VFX_LevelUpPop_Right");
            levelUpParticleRightRoot = null == _rightParticleRoot ? null : _rightParticleRoot.gameObject;
        }

        CacheAndConfigureLevelUpParticleEffect();

        if (null == levelUpSparkEffectImage)
            levelUpSparkEffectImage = FindChildComponent<Image>("LevelUp_Spark");

        if (null == fontSparkEffectImage)
            fontSparkEffectImage = FindChildComponent<Image>("Font_Spark");

        if (null == flowerAppearEffectImage)
            flowerAppearEffectImage = FindChildComponent<Image>("Appear_Flower");

        if (null != levelUpAboveEffectImage)
            levelUpAboveEffectImage.raycastTarget = false;
        SetEventSpriteEffectRaycastTargets(false);

        if (null != levelUpSpriteEffectRoot)
            levelUpSpriteEffectRoot.SetActive(true);
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            CancelPresentation();
    }

    private void OnDestroy()
    {
        CancelPresentation();
    }

    private void PlayLevelUpSpriteEffect()
    {
        StopLevelUpSpriteEffect();

        int _aboveFrameCount = null == levelUpAboveEffectFrames ? 0 : levelUpAboveEffectFrames.Length;
        if (null == levelUpSpriteEffectRoot || _aboveFrameCount <= 0)
            return;

        levelUpSpriteEffectRoot.SetActive(true);
        SetSpriteEffectFrame(levelUpAboveEffectImage, levelUpAboveEffectFrames, 0);

        levelUpSpriteEffectActiveFrameRate = Mathf.Max(1.0f, levelUpSpriteEffectFrameRate);
        float _duration = _aboveFrameCount / levelUpSpriteEffectActiveFrameRate;
        levelUpSpriteEffectLastFrameIndex = 0;
        levelUpSpriteEffectTween = DOVirtual.Float(0.0f, _duration, _duration, UpdateLevelUpSpriteEffectFrame)
        .SetEase(Ease.Linear)
        .SetUpdate(true)
        .OnComplete(CompleteLevelUpSpriteEffect);
    }

    private void UpdateLevelUpSpriteEffectFrame(float _elapsedTime)
    {
        int _frameIndex = Mathf.FloorToInt(_elapsedTime * levelUpSpriteEffectActiveFrameRate);
        if (_frameIndex == levelUpSpriteEffectLastFrameIndex)
            return;

        levelUpSpriteEffectLastFrameIndex = _frameIndex;
        SetSpriteEffectFrame(levelUpAboveEffectImage, levelUpAboveEffectFrames, _frameIndex);
    }

    private void CompleteLevelUpSpriteEffect()
    {
        levelUpSpriteEffectTween = null;
        HideLevelUpSpriteEffect();
    }

    private void StopLevelUpSpriteEffect()
    {
        if (null != levelUpSpriteEffectTween && levelUpSpriteEffectTween.IsActive())
            levelUpSpriteEffectTween.Kill(false);

        levelUpSpriteEffectTween = null;
        HideLevelUpSpriteEffect();
    }

    private void HideLevelUpSpriteEffect()
    {
        SetSpriteEffectFrame(levelUpAboveEffectImage, null, -1);
    }

    private void PlayLevelUpParticleEffect()
    {
        StopLevelUpParticleEffect();
        CacheAndConfigureLevelUpParticleEffect();
        if (null == levelUpParticleSystems || 0 == levelUpParticleSystems.Length)
            return;

        SetLevelUpParticleRootsActive(true);
        float _maxParticleLifetime = 0.0f;
        for (int i = 0; i < levelUpParticleSystems.Length; i++)
        {
            ParticleSystem _particleSystem = levelUpParticleSystems[i];
            if (null == _particleSystem)
                continue;

            ParticleSystem.MainModule _main = _particleSystem.main;
            _particleSystem.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            _particleSystem.Play(false);
            _maxParticleLifetime = Mathf.Max(_maxParticleLifetime, _main.startLifetime.constantMax);
        }

        float _emissionDuration = Mathf.Max(0.0f, levelUpParticleEmissionDuration);
        levelUpParticleStopTween = DOTween.Sequence()
            .AppendInterval(_emissionDuration)
            .AppendCallback(StopLevelUpParticleEmission)
            .AppendInterval(Mathf.Max(0.0f, _maxParticleLifetime))
            .OnComplete(CompleteLevelUpParticleEffect)
            .SetUpdate(true);
    }

    private void CompleteLevelUpParticleEffect()
    {
        levelUpParticleStopTween = null;
        SetLevelUpParticleRootsActive(false);
    }

    private void StopLevelUpParticleEffect()
    {
        if (null != levelUpParticleStopTween && levelUpParticleStopTween.IsActive())
            levelUpParticleStopTween.Kill(false);

        levelUpParticleStopTween = null;
        if (null != levelUpParticleSystems)
        {
            for (int i = 0; i < levelUpParticleSystems.Length; i++)
            {
                if (null != levelUpParticleSystems[i])
                    levelUpParticleSystems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        SetLevelUpParticleRootsActive(false);
    }

    private void ApplyCanvasSortingSettings()
    {
        if (deferCanvasSortingSettings)
            return;

        ApplyCanvasSortingSettings(abilityHUDCanvas, abilityHUDSortingOrder, false);
        ApplyCanvasSortingSettings(levelUpParticleCanvas, levelUpParticleSortingOrder, true);
    }

    private void ApplyCanvasSortingSettings(Canvas _canvas, int _sortingOrder, bool _applyReferencePixelsPerUnit)
    {
        if (null == _canvas)
            return;

        // 같은 값을 다시 대입해도 Canvas가 갱신 메시지를 보내므로 달라진 값만 적용한다.
        if (false == _canvas.overrideSorting)
            _canvas.overrideSorting = true;

        if (_canvas.sortingLayerName != levelUpParticleSortingLayer)
            _canvas.sortingLayerName = levelUpParticleSortingLayer;

        if (_canvas.sortingOrder != _sortingOrder)
            _canvas.sortingOrder = _sortingOrder;

        if (_applyReferencePixelsPerUnit &&
            false == Mathf.Approximately(_canvas.referencePixelsPerUnit, LevelUpParticleReferencePixelsPerUnit))
            _canvas.referencePixelsPerUnit = LevelUpParticleReferencePixelsPerUnit;
    }

    private void CacheAndConfigureLevelUpParticleEffect()
    {
        ApplyCanvasSortingSettings();

        List<ParticleSystem> _particleSystems = new List<ParticleSystem>(4);
        AddParticleSystems(levelUpParticleLeftRoot, _particleSystems);
        AddParticleSystems(levelUpParticleRightRoot, _particleSystems);
        levelUpParticleSystems = _particleSystems.ToArray();
        for (int i = 0; i < levelUpParticleSystems.Length; i++)
        {
            ParticleSystem _particleSystem = levelUpParticleSystems[i];
            ParticleSystem.MainModule _main = _particleSystem.main;
            _main.loop = true;
            _main.playOnAwake = false;
            _main.useUnscaledTime = true;

            ParticleSystemRenderer _renderer = _particleSystem.GetComponent<ParticleSystemRenderer>();
            if (null == _renderer)
                continue;

            _renderer.sortingLayerName = levelUpParticleSortingLayer;
            _renderer.sortingOrder = levelUpParticleSortingOrder;
        }
    }

    private static void AddParticleSystems(GameObject _root, List<ParticleSystem> _results)
    {
        if (null == _root || null == _results)
            return;

        _results.AddRange(_root.GetComponentsInChildren<ParticleSystem>(true));
    }

    private void StopLevelUpParticleEmission()
    {
        if (null == levelUpParticleSystems)
            return;

        for (int i = 0; i < levelUpParticleSystems.Length; i++)
        {
            if (null != levelUpParticleSystems[i])
                levelUpParticleSystems[i].Stop(false, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void SetLevelUpParticleRootsActive(bool _active)
    {
        if (null != levelUpParticleLeftRoot)
            levelUpParticleLeftRoot.SetActive(_active);

        if (null != levelUpParticleRightRoot)
            levelUpParticleRightRoot.SetActive(_active);
    }

    private void PlayEventSpriteEffect(
        EventSpriteEffectType _effectType,
        RectTransform _target = null,
        Vector2 _targetOffset = default)
    {
        int _effectIndex = (int)_effectType;
        StopEventSpriteEffect(_effectType);

        Image _effectImage = GetEventSpriteEffectImage(_effectType);
        Sprite[] _frames = GetEventSpriteEffectFrames(_effectType);
        if (null == _effectImage || null == _frames || 0 == _frames.Length)
            return;

        if (null != _target)
            SnapEffectToTarget(_effectImage.rectTransform, _target, _targetOffset);

        SetSpriteEffectFrame(_effectImage, _frames, 0);

        float _frameRate = Mathf.Max(1.0f, eventSpriteEffectFrameRate);
        float _duration = _frames.Length / _frameRate;
        activeEventSpriteEffectImages[_effectIndex] = _effectImage;
        activeEventSpriteEffectFrames[_effectIndex] = _frames;
        activeEventSpriteEffectFrameRates[_effectIndex] = _frameRate;
        activeEventSpriteEffectLastFrameIndices[_effectIndex] = 0;

        switch (_effectType)
        {
            case EventSpriteEffectType.LevelUpSpark:
                eventSpriteEffectTweens[_effectIndex] = CreateEventSpriteEffectTween(
                    _duration,
                    UpdateLevelUpSparkEffect,
                    CompleteLevelUpSparkEffect);
                break;
            case EventSpriteEffectType.FontSpark:
                eventSpriteEffectTweens[_effectIndex] = CreateEventSpriteEffectTween(
                    _duration,
                    UpdateFontSparkEffect,
                    CompleteFontSparkEffect);
                break;
            case EventSpriteEffectType.FlowerAppear:
                eventSpriteEffectTweens[_effectIndex] = CreateEventSpriteEffectTween(
                    _duration,
                    UpdateFlowerAppearEffect,
                    CompleteFlowerAppearEffect);
                break;
        }
    }

    private static Tween CreateEventSpriteEffectTween(
        float _duration,
        TweenCallback<float> _updateCallback,
        TweenCallback _completeCallback)
    {
        return DOVirtual.Float(0.0f, _duration, _duration, _updateCallback)
            .SetEase(Ease.Linear)
            .SetUpdate(true)
            .OnComplete(_completeCallback);
    }

    private void UpdateLevelUpSparkEffect(float _elapsedTime)
    {
        UpdateEventSpriteEffect(EventSpriteEffectType.LevelUpSpark, _elapsedTime);
    }

    private void UpdateFontSparkEffect(float _elapsedTime)
    {
        UpdateEventSpriteEffect(EventSpriteEffectType.FontSpark, _elapsedTime);
    }

    private void UpdateFlowerAppearEffect(float _elapsedTime)
    {
        UpdateEventSpriteEffect(EventSpriteEffectType.FlowerAppear, _elapsedTime);
    }

    private void UpdateEventSpriteEffect(EventSpriteEffectType _effectType, float _elapsedTime)
    {
        int _effectIndex = (int)_effectType;
        Sprite[] _frames = activeEventSpriteEffectFrames[_effectIndex];
        if (null == _frames || 0 == _frames.Length)
            return;

        int _frameIndex = Mathf.Min(
            Mathf.FloorToInt(_elapsedTime * activeEventSpriteEffectFrameRates[_effectIndex]),
            _frames.Length - 1);
        if (_frameIndex == activeEventSpriteEffectLastFrameIndices[_effectIndex])
            return;

        activeEventSpriteEffectLastFrameIndices[_effectIndex] = _frameIndex;
        SetSpriteEffectFrame(activeEventSpriteEffectImages[_effectIndex], _frames, _frameIndex);
    }

    private void CompleteLevelUpSparkEffect()
    {
        CompleteEventSpriteEffect(EventSpriteEffectType.LevelUpSpark);
    }

    private void CompleteFontSparkEffect()
    {
        CompleteEventSpriteEffect(EventSpriteEffectType.FontSpark);
    }

    private void CompleteFlowerAppearEffect()
    {
        CompleteEventSpriteEffect(EventSpriteEffectType.FlowerAppear);
    }

    private void CompleteEventSpriteEffect(EventSpriteEffectType _effectType)
    {
        int _effectIndex = (int)_effectType;
        eventSpriteEffectTweens[_effectIndex] = null;
        SetSpriteEffectFrame(activeEventSpriteEffectImages[_effectIndex], null, -1);
        activeEventSpriteEffectImages[_effectIndex] = null;
        activeEventSpriteEffectFrames[_effectIndex] = null;
    }

    private void StopEventSpriteEffect(EventSpriteEffectType _effectType)
    {
        int _effectIndex = (int)_effectType;
        Tween _tween = eventSpriteEffectTweens[_effectIndex];
        if (null != _tween && _tween.IsActive())
            _tween.Kill(false);

        eventSpriteEffectTweens[_effectIndex] = null;
        SetSpriteEffectFrame(GetEventSpriteEffectImage(_effectType), null, -1);
        activeEventSpriteEffectImages[_effectIndex] = null;
        activeEventSpriteEffectFrames[_effectIndex] = null;
    }

    private void StopAllEventSpriteEffects()
    {
        for (int i = 0; i < (int)EventSpriteEffectType.Count; i++)
            StopEventSpriteEffect((EventSpriteEffectType)i);
    }

    private void HideIdleEventSpriteEffects()
    {
        for (int i = 0; i < (int)EventSpriteEffectType.Count; i++)
        {
            Tween _tween = eventSpriteEffectTweens[i];
            if (null == _tween || false == _tween.IsActive())
                SetSpriteEffectFrame(GetEventSpriteEffectImage((EventSpriteEffectType)i), null, -1);
        }
    }

    private Sprite[] GetEventSpriteEffectFrames(EventSpriteEffectType _effectType)
    {
        switch (_effectType)
        {
            case EventSpriteEffectType.LevelUpSpark:
                return levelUpSparkEffectFrames;
            case EventSpriteEffectType.FontSpark:
                return fontSparkEffectFrames;
            case EventSpriteEffectType.FlowerAppear:
                return flowerAppearEffectFrames;
            default:
                return null;
        }
    }

    private Image GetEventSpriteEffectImage(EventSpriteEffectType _effectType)
    {
        switch (_effectType)
        {
            case EventSpriteEffectType.LevelUpSpark:
                return levelUpSparkEffectImage;
            case EventSpriteEffectType.FontSpark:
                return fontSparkEffectImage;
            case EventSpriteEffectType.FlowerAppear:
                return flowerAppearEffectImage;
            default:
                return null;
        }
    }

    private static void SnapEffectToTarget(
        RectTransform _effectRectTransform,
        RectTransform _target,
        Vector2 _targetOffset)
    {
        if (null == _effectRectTransform || null == _target || null == _effectRectTransform.parent)
            return;

        Vector3 _localTargetPosition = _effectRectTransform.parent.InverseTransformPoint(_target.position);
        _effectRectTransform.anchoredPosition = new Vector2(
            Mathf.Round(_localTargetPosition.x + _targetOffset.x),
            Mathf.Round(_localTargetPosition.y + _targetOffset.y));
    }

    private void SortSerializedSpriteEffectFrames()
    {
        SortSpriteFrames(levelUpAboveEffectFrames);
        SortSpriteFrames(levelUpSparkEffectFrames);
        SortSpriteFrames(fontSparkEffectFrames);
        SortSpriteFrames(flowerAppearEffectFrames);
    }

    private static void SetSpriteEffectFrame(Image _image, Sprite[] _frames, int _frameIndex)
    {
        if (null == _image)
            return;

        bool _hasFrame = null != _frames && _frameIndex >= 0 && _frameIndex < _frames.Length;
        if (_image.enabled != _hasFrame)
            _image.enabled = _hasFrame;

        if (_hasFrame && _image.sprite != _frames[_frameIndex])
            _image.sprite = _frames[_frameIndex];
    }

    private static void SortSpriteFrames(Sprite[] _frames)
    {
        if (null == _frames || _frames.Length <= 1)
            return;

        System.Array.Sort(_frames, CompareSpriteFrameNames);
    }

    private static int CompareSpriteFrameNames(Sprite _left, Sprite _right)
    {
        int _leftIndex = GetSpriteFrameIndex(_left);
        int _rightIndex = GetSpriteFrameIndex(_right);
        int _indexComparison = _leftIndex.CompareTo(_rightIndex);
        if (_indexComparison != 0)
            return _indexComparison;

        string _leftName = null == _left ? string.Empty : _left.name;
        string _rightName = null == _right ? string.Empty : _right.name;
        return string.CompareOrdinal(_leftName, _rightName);
    }

    private static int GetSpriteFrameIndex(Sprite _sprite)
    {
        if (null == _sprite || string.IsNullOrEmpty(_sprite.name))
            return int.MaxValue;

        string _name = _sprite.name;
        int _digitStart = _name.Length;
        while (_digitStart > 0 && char.IsDigit(_name[_digitStart - 1]))
            _digitStart--;

        if (_digitStart >= _name.Length)
            return int.MaxValue;

        return int.TryParse(_name.Substring(_digitStart), out int _index)
            ? _index
            : int.MaxValue;
    }

    private void SetEventSpriteEffectRaycastTargets(bool _raycastTarget)
    {
        if (null != levelUpSparkEffectImage)
            levelUpSparkEffectImage.raycastTarget = _raycastTarget;

        if (null != fontSparkEffectImage)
            fontSparkEffectImage.raycastTarget = _raycastTarget;

        if (null != flowerAppearEffectImage)
            flowerAppearEffectImage.raycastTarget = _raycastTarget;
    }

    private void PlayBarFontExperienceMotion()
    {
        if (null == barFontMotionPlayer || string.IsNullOrEmpty(barFontExperienceMotionTag))
            return;

        if (IsResetExperienceEffectPlaying())
        {
            playExperienceMotionAfterReset = true;
            return;
        }

        barFontMotionPlayer.Play(barFontExperienceMotionTag, bReset: resetBarFontMotionBeforePlay);
    }

    private void StopBarFontExperienceMotion()
    {
        if (null == barFontMotionPlayer || string.IsNullOrEmpty(barFontExperienceMotionTag))
            return;

        barFontMotionPlayer.Stop(barFontExperienceMotionTag);
    }

    private void PlayDeferredExperienceMotion()
    {
        if (playExperienceMotionAfterReset)
        {
            playExperienceMotionAfterReset = false;
            PlayBarFontExperienceMotion();
        }

        if (playFlowerDangleAfterReset)
        {
            playFlowerDangleAfterReset = false;
            PlayFlowerDangleEffect();
        }
    }

    private void ScheduleDeferredExperienceMotion()
    {
        if (playExperienceMotionAfterReset == false && playFlowerDangleAfterReset == false)
            return;

        DOVirtual.DelayedCall(0.0f, PlayDeferredExperienceMotion).SetUpdate(true);
    }

    private void ClearDeferredExperienceMotion()
    {
        playExperienceMotionAfterReset = false;
        playFlowerDangleAfterReset = false;
    }

    private T FindChildComponent<T>(string _name) where T : Component
    {
        Transform _child = FindChild(transform, _name);
        return null == _child ? null : _child.GetComponent<T>();
    }

    private Transform FindChild(Transform _root, string _name)
    {
        if (null == _root)
            return null;

        for (int i = 0; i < _root.childCount; i++)
        {
            Transform _child = _root.GetChild(i);
            if (_child.name == _name)
                return _child;

            Transform _result = FindChild(_child, _name);
            if (null != _result)
                return _result;
        }

        return null;
    }
}
