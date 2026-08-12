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
    private const string LevelUpSideEffectResourcePath = "AbilityHUD/AbilityHUD_SideEffect";
    private const string LevelUpAboveEffectResourcePath = "AbilityHUD/AbilityHUD_AboveEffect";

    [Header("UI References")]
    [SerializeField] private Image fillImage;
    [SerializeField] private FontMaker fontMakerForBar;
    [SerializeField] private FontMaker fontMakerForFlowerStack;
    [SerializeField] private ObjectMotionPlayer barFontMotionPlayer;

    [Header("Level Up Sprite Effect")]
    [SerializeField] private GameObject levelUpSpriteEffectRoot;
    [SerializeField] private Image[] levelUpSideEffectImages = new Image[2];
    [SerializeField] private Image levelUpAboveEffectImage;
    [SerializeField, Min(1.0f)] private float levelUpSpriteEffectFrameRate = 24.0f;

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
    private Tween levelUpSpriteEffectTween;
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
    private float lastDownTickSoundElapsed = float.NegativeInfinity;
    private bool playExperienceMotionAfterReset;
    private bool playFlowerDangleAfterReset;
    private Sprite[] levelUpSideEffectFrames;
    private Sprite[] levelUpAboveEffectFrames;

    private void Awake()
    {
        BindReferencesIfNeeded();

        if (Application.isPlaying)
            LoadLevelUpSpriteEffectFramesIfNeeded();

        HideLevelUpSpriteEffect();
        RefreshOrSchedule();
    }

    private void OnEnable()
    {
        BindReferencesIfNeeded();

        if (Application.isPlaying && (null == levelUpSpriteEffectTween || false == levelUpSpriteEffectTween.IsActive()))
            HideLevelUpSpriteEffect();

        RefreshOrSchedule();
    }

    private void OnValidate()
    {
        maxExperience = Mathf.Max(1, maxExperience);
        currentExperience = Mathf.Clamp(currentExperience, 0, maxExperience);
        flowerStack = Mathf.Max(0, flowerStack);

        BindReferencesIfNeeded();
        RefreshOrSchedule();
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
        int _startExperience = maxExperience;
        currentExperience = _startExperience;
        RefreshAbilityBar();
        lastDownTickSoundElapsed = float.NegativeInfinity;

        fillInitialColor = null == fillImage ? Color.white : fillImage.color;
        ApplyResetFlashColors(0.0f, 0.0f);

        resetEffectSequence = DOTween.Sequence();
        resetEffectSequence.AppendCallback(() =>
        {
            Sound.PlayUI(SoundID.AbilityHUDLevelUp);
            PlayLevelUpSpriteEffect();

            if (null != barFontRectTransform)
                barFontRectTransform.DOShakeAnchorPos(
                    Mathf.Max(0.0f, resetColorDuration + resetDrainDuration),
                    resetShakeStrength,
                    Mathf.Max(1, resetShakeVibrato),
                    90.0f,
                    false,
                    true);
        });
        resetEffectSequence.Join(BuildResetSquashTween());
        resetEffectSequence.Join(BuildResetColorFlashTween());
        resetEffectSequence.AppendCallback(PlayFlowerStackGrowEffect);
        resetEffectSequence.AppendCallback(PlayDownStartSound);
        resetEffectSequence.Append(DOVirtual.Float(0.0f, 1.0f, Mathf.Max(0.0f, resetDrainDuration), _progress =>
        {
            int _targetExperience = ClampExperience(resetDrainTargetExperience);
            int _previousExperience = currentExperience;
            currentExperience = Mathf.RoundToInt(Mathf.Lerp(_startExperience, _targetExperience, _progress));
            TryPlayDownTickSound(
                _previousExperience,
                currentExperience,
                resetDrainDuration * _progress);
            ApplyResetFlashColors(resetColorDuration + (resetDrainDuration * _progress), _progress);
            RefreshAbilityBar();
        }).SetEase(Ease.Linear));
        resetEffectSequence.OnKill(RestoreResetExperienceEffectState);
        resetEffectSequence.OnComplete(() =>
        {
            currentExperience = ClampExperience(resetDrainTargetExperience);
            RefreshAbilityBar();
            RestoreResetExperienceEffectState();
            resetEffectSequence = null;
            ScheduleDeferredExperienceMotion();
        });
    }

    private Tween BuildResetSquashTween()
    {
        if (null == barFontRectTransform)
            return DOVirtual.DelayedCall(Mathf.Max(0.0f, resetSquashDuration), () => { });

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
        FlowerVisual _newFlowerVisual = AddFlowerStackFromReset();
        if (null != _newFlowerVisual)
            Sound.PlayUI(SoundID.AbilityHUDFlowerGrow);

        if (null == flowerStackFontRectTransform)
        {
            _newFlowerVisual?.PlayGrow();
            return;
        }

        flowerStackFontRectTransform.DOKill(false);
        flowerStackFontRectTransform.localScale = flowerStackFontInitialScale;
        flowerStackFontRectTransform.anchoredPosition = flowerStackFontInitialAnchoredPosition;

        Sequence _sequence = DOTween.Sequence();
        _sequence.Append(flowerStackFontRectTransform.DOScale(flowerStackFontInitialScale * flowerStackPopScale, flowerStackPopDuration * 0.22f).SetEase(Ease.OutExpo));
        _sequence.Append(flowerStackFontRectTransform.DOScale(flowerStackFontInitialScale * 0.86f, flowerStackPopDuration * 0.22f).SetEase(Ease.InOutSine));
        _sequence.Append(flowerStackFontRectTransform.DOScale(flowerStackFontInitialScale * 1.12f, flowerStackPopDuration * 0.20f).SetEase(Ease.InOutSine));
        _sequence.Append(flowerStackFontRectTransform.DOScale(flowerStackFontInitialScale, flowerStackPopDuration * 0.36f).SetEase(Ease.OutBack));

        _newFlowerVisual?.PlayGrow();
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
        float _duration = Mathf.Max(0.0f, resetColorDuration);

        return DOVirtual.Float(0.0f, _duration, _duration, _elapsedTime =>
        {
            ApplyResetFlashColors(_elapsedTime, 0.0f);
        }).SetEase(Ease.Linear).OnComplete(() =>
            {
                ApplyResetFlashColors(_duration, 0.0f);
            });
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
            flowerStackFontRectTransform.DOKill(false);
            flowerStackFontRectTransform.localScale = flowerStackFontInitialScale;
            flowerStackFontRectTransform.anchoredPosition = flowerStackFontInitialAnchoredPosition;
        }

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

        EnsureFlowerVisualPrefab();
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

    private void EnsureFlowerVisualPrefab()
    {
#if UNITY_EDITOR
        if (null == flowerVisualPrefab)
            flowerVisualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/TentUI/FlowerVisual.prefab");
#endif
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

        EnsureFlowerVisualPrefab();

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

        if (null == levelUpSideEffectImages || levelUpSideEffectImages.Length != 2)
            levelUpSideEffectImages = new Image[2];

        if (null == levelUpSideEffectImages[0])
            levelUpSideEffectImages[0] = FindChildComponent<Image>("Left_SideEffect");

        if (null == levelUpSideEffectImages[1])
            levelUpSideEffectImages[1] = FindChildComponent<Image>("Right_SideEffect");

        SetLevelUpSpriteEffectRaycastTargets(false);
    }

    private void OnDestroy()
    {
        StopResetExperienceEffect(false);
        StopLevelUpSpriteEffect();
        StopDownStartSound();
    }

    private void PlayLevelUpSpriteEffect()
    {
        StopLevelUpSpriteEffect();
        LoadLevelUpSpriteEffectFramesIfNeeded();

        int _sideFrameCount = null == levelUpSideEffectFrames ? 0 : levelUpSideEffectFrames.Length;
        int _aboveFrameCount = null == levelUpAboveEffectFrames ? 0 : levelUpAboveEffectFrames.Length;
        int _longestFrameCount = Mathf.Max(_sideFrameCount, _aboveFrameCount);
        if (null == levelUpSpriteEffectRoot || _longestFrameCount <= 0)
            return;

        levelUpSpriteEffectRoot.SetActive(true);
        SetSpriteEffectFrame(levelUpSideEffectImages, levelUpSideEffectFrames, 0);
        SetSpriteEffectFrame(levelUpAboveEffectImage, levelUpAboveEffectFrames, 0);

        float _frameRate = Mathf.Max(1.0f, levelUpSpriteEffectFrameRate);
        float _duration = _longestFrameCount / _frameRate;
        int _lastFrameIndex = 0;
        levelUpSpriteEffectTween = DOVirtual.Float(0.0f, _duration, _duration, _elapsedTime =>
        {
            int _frameIndex = Mathf.FloorToInt(_elapsedTime * _frameRate);
            if (_frameIndex == _lastFrameIndex)
                return;

            _lastFrameIndex = _frameIndex;
            SetSpriteEffectFrame(levelUpSideEffectImages, levelUpSideEffectFrames, _frameIndex);
            SetSpriteEffectFrame(levelUpAboveEffectImage, levelUpAboveEffectFrames, _frameIndex);
        })
        .SetEase(Ease.Linear)
        .SetUpdate(true)
        .OnComplete(() =>
        {
            levelUpSpriteEffectTween = null;
            HideLevelUpSpriteEffect();
        });
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
        if (null != levelUpSpriteEffectRoot)
            levelUpSpriteEffectRoot.SetActive(false);
    }

    private void LoadLevelUpSpriteEffectFramesIfNeeded()
    {
        if (null == levelUpSideEffectFrames || levelUpSideEffectFrames.Length == 0)
        {
            levelUpSideEffectFrames = Resources.LoadAll<Sprite>(LevelUpSideEffectResourcePath);
            SortSpriteFrames(levelUpSideEffectFrames);
        }

        if (null == levelUpAboveEffectFrames || levelUpAboveEffectFrames.Length == 0)
        {
            levelUpAboveEffectFrames = Resources.LoadAll<Sprite>(LevelUpAboveEffectResourcePath);
            SortSpriteFrames(levelUpAboveEffectFrames);
        }
    }

    private static void SetSpriteEffectFrame(Image[] _images, Sprite[] _frames, int _frameIndex)
    {
        if (null == _images)
            return;

        for (int i = 0; i < _images.Length; i++)
            SetSpriteEffectFrame(_images[i], _frames, _frameIndex);
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

    private void SetLevelUpSpriteEffectRaycastTargets(bool _raycastTarget)
    {
        if (null != levelUpAboveEffectImage)
            levelUpAboveEffectImage.raycastTarget = _raycastTarget;

        if (null == levelUpSideEffectImages)
            return;

        for (int i = 0; i < levelUpSideEffectImages.Length; i++)
        {
            if (null != levelUpSideEffectImages[i])
                levelUpSideEffectImages[i].raycastTarget = _raycastTarget;
        }
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
