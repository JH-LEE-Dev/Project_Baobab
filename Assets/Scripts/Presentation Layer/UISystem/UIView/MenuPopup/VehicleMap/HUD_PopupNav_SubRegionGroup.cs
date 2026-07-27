using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class HUD_PopupNav_SubRegionGroup : MonoBehaviour
{
    [Header("Container & Global Animation")]
    [Tooltip("서브지역 버튼들이 자식으로 배치될 컨테이너 (이동/배치 기준점)")]
    [SerializeField] private RectTransform container;
    [Tooltip("선택된 대지역 위치로 컨테이너 동적 이동 여부 (기본값 false: 인스펙터 고정 위치 사용)")]
    [SerializeField] private bool useDynamicAnchoring = false;
    [Tooltip("서브지역 팝업 시 대상 대지역과의 X 오프셋 간격")]
    [SerializeField] private float anchorOffsetX = 200f;
    [Tooltip("서브지역 노출 시 요소간 딜레이(순차 등장)")]
    [SerializeField] private float appearSequenceDelay = 0.1f;


    [Header("Layout & Distribution")]
    [Tooltip("전체 버튼들의 기본 Y축 오프셋 (아랫쪽 배치를 위해 음수 권장)")]
    [SerializeField] private float baseOffsetY = -50f;
    [Tooltip("자연스러운 배치를 위한 중앙부 솟아오름(아치형) 기본 높이 편차")]
    [SerializeField] private float archHeightY = 30f;

    [Tooltip("각 버튼별 Y축 랜덤 분산 범위 최대값 (위아래 픽셀)")]
    [SerializeField] private float randomScatterRangeY = 40f;
    [Tooltip("절대 일렬로 배치되지 않도록 강제로 위/아래 지그재그 패턴 적용")]
    [SerializeField] private bool preventStraightLine = true;
    [Tooltip("각 버튼별 Y축 랜덤 분산 범위 최소값 (일렬 방지를 위해 최소한 이만큼은 어긋나게 함)")]
    [SerializeField] private float minRandomScatterY = 15f;
    [Tooltip("버튼이 상단 영역 밖으로 나가지 않게 하는 여백 (나무 정보 툴팁 공간 확보)")]
    [SerializeField] private float paddingTop = 80f;
    [Tooltip("버튼이 하단 영역 밖으로 나가지 않게 하는 여백")]
    [SerializeField] private float paddingBottom = 40f;
    [Tooltip("버튼이 좌측 영역 밖으로 나가지 않게 하는 여백 (가장자리 나무 정보 노출 보호)")]
    [SerializeField] private float paddingLeft = 80f;
    [Tooltip("버튼이 우측 영역 밖으로 나가지 않게 하는 여백")]
    [SerializeField] private float paddingRight = 80f;
    [Tooltip("버튼 간 최소 보장 거리 (겹침 방지용, 버튼 너비에 대한 비율. 예: 0.9 = 버튼 너비의 90% 이상 띄움)")]
    [SerializeField] private float minDistanceRatioX = 0.9f;
    [Tooltip("버튼 간 최소 보장 절대 거리 (프리팹의 Rect 넓이보다 실제 나무 이미지가 더 큰 경우를 대비한 절대 픽셀 값)")]
    [SerializeField] private float minDistanceAbsoluteX = 120f;

    [Header("SubRegion Button Setup & Animation")]
    [Tooltip("기본 프리팹 (초기화 후 비활성화됨)")]
    [SerializeField] private HUD_PopupNav_SubRegionBtn subRegionBtnPrefab;
    [Tooltip("각 서브지역 버튼 등장(스케일 업) 연출 시간")]
    [SerializeField] private float appearAnimDuration = 0.15f;
    [Tooltip("각 서브지역 버튼 등장(스케일 업) 연출 이즈(Ease)")]
    [SerializeField] private Ease appearAnimEase = Ease.OutBack;
    


    private HUD_PopupNav_Main mainController;
    private LocalizationManager localizationManager;
    private TreeVisualDataBase treeVisualDataBase;
    private IMapDataProvider mapDataProvider;
    
    private readonly List<HUD_PopupNav_SubRegionBtn> subRegionButtons = new List<HUD_PopupNav_SubRegionBtn>(16);
    private readonly List<HUD_PopupNav_SubRegionBtn> activeSubRegionButtons = new List<HUD_PopupNav_SubRegionBtn>(8);

    private WaitForSeconds cachedSequenceWait;
    private Coroutine sequenceCoroutine;
    private Sequence currentAppearSequence;
    
    private MapType pendingMapType = MapType.None;
    private Transform pendingRegionTransform;
    private Action pendingOnComplete;

    // Fixed GC allocs by making these class-level fields
    private readonly List<TreeVisualData> tempTreeVisualDatas = new List<TreeVisualData>(8);
    private float[] cachedSafeDistances = new float[32];
    private float[] cachedGaps = new float[32];
    
    private int completedDisappearCount;
    private Action cachedOnComplete;

    private Action cachedOnDisappearSequenceCompleteForToggle;
    private TweenCallback cachedOnAppearSequenceComplete;
    private Action cachedOnDisappearMotionComplete;
    private Action cachedOnSingleSubRegionUnlockComplete;

    public void Initialize(HUD_PopupNav_Main _mainController, LocalizationManager _localizationManager, TreeVisualDataBase _treeVisualDataBase)
    {
        mainController = _mainController;
        localizationManager = _localizationManager;
        treeVisualDataBase = _treeVisualDataBase;

        if (null != container)
        {
            HUD_PopupNav_SubRegionBtn[] _existBtns = container.GetComponentsInChildren<HUD_PopupNav_SubRegionBtn>(true);
            if (null != _existBtns)
            {
                for (int i = 0; i < _existBtns.Length; i++)
                {
                    if (false == subRegionButtons.Contains(_existBtns[i]))
                    {
                        _existBtns[i].gameObject.SetActive(false);
                        subRegionButtons.Add(_existBtns[i]);
                    }
                }
            }
        }
        
        cachedSequenceWait = new WaitForSeconds(appearSequenceDelay);

        if (null == cachedOnDisappearSequenceCompleteForToggle) cachedOnDisappearSequenceCompleteForToggle = OnDisappearSequenceCompleteForToggle;
        if (null == cachedOnAppearSequenceComplete) cachedOnAppearSequenceComplete = OnAppearSequenceComplete;
        if (null == cachedOnDisappearMotionComplete) cachedOnDisappearMotionComplete = OnDisappearMotionComplete;
        if (null == cachedOnSingleSubRegionUnlockComplete) cachedOnSingleSubRegionUnlockComplete = OnSingleSubRegionUnlockComplete;
    }

    public void ShowSubRegionsForMap(MapType _mapType, Transform _regionBtnTransform, IMapDataProvider _provider, Action _onComplete = null)
    {
        mapDataProvider = _provider;
        
        if (0 < activeSubRegionButtons.Count)
        {
            pendingMapType = _mapType;
            pendingRegionTransform = _regionBtnTransform;
            pendingOnComplete = _onComplete;
            
            if (null == sequenceCoroutine)
            {
                PlayDisappearSequence(cachedOnDisappearSequenceCompleteForToggle);
            }
        }
        else
        {
            SetupAndPlayAppearSequence(_mapType, _regionBtnTransform, _onComplete);
        }
    }

    private void OnDisappearSequenceCompleteForToggle()
    {
        SetupAndPlayAppearSequence(pendingMapType, pendingRegionTransform, pendingOnComplete);
        pendingOnComplete = null;
    }

    private void SetupAndPlayAppearSequence(MapType _mapType, Transform _regionBtnTransform, Action _onComplete)
    {
        if (null == mapDataProvider)
        {
            return;
        }

        MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();
        if (null == _db.mapDatas)
        {
            return;
        }

        activeSubRegionButtons.Clear();
        for (int i = 0; i < subRegionButtons.Count; i++)
        {
            subRegionButtons[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < _db.mapDatas.Count; i++)
        {
            if (_mapType == _db.mapDatas[i].mapType)
            {
                MapEnvironmentDataInfo _regionInfo = _db.mapDatas[i];
                if (null != _regionInfo.forestDatas)
                {
                    for (int j = 0; j < _regionInfo.forestDatas.Count; j++)
                    {
                        ForestEnvironmentInfo _subInfo = _regionInfo.forestDatas[j];
                        HUD_PopupNav_SubRegionBtn _btn = GetOrCreateSubRegionButton(j);
                        if (null != _btn)
                        {
                            _btn.transform.SetAsFirstSibling();
                            
                            _btn.gameObject.SetActive(false); 
                            tempTreeVisualDatas.Clear(); // Using pre-allocated list to fix GC
                            if (null != treeVisualDataBase && null != _subInfo.spawnTreeTypes)
                            {
                                int _treeCount = Mathf.Min(_subInfo.spawnTreeTypes.Count, 2);
                                for (int k = 0; k < _treeCount; k++)
                                {
                                    tempTreeVisualDatas.Add(treeVisualDataBase.Get(_subInfo.spawnTreeTypes[k].treeType));
                                }
                            }
                            
                            _btn.Initialize(mainController, _subInfo, localizationManager, _regionInfo.mapType, tempTreeVisualDatas);
                            activeSubRegionButtons.Add(_btn);
                        }
                    }
                }
                break;
            }
        }

        DistributeButtonsEvenly();

        if (true == useDynamicAnchoring && null != _regionBtnTransform && null != container)
        {
            container.position = _regionBtnTransform.position;
            container.anchoredPosition += new Vector2(anchorOffsetX, 0f);
        }

        if (null != currentAppearSequence && true == currentAppearSequence.IsActive())
        {
            currentAppearSequence.Kill();
            currentAppearSequence = null;
        }

        currentAppearSequence = PlayAppearSequence();
        if (null != currentAppearSequence)
        {
            cachedOnComplete = _onComplete;
            currentAppearSequence.OnComplete(cachedOnAppearSequenceComplete); // Removed lambda
        }
        else
        {
            _onComplete?.Invoke();
        }
    }

    private void OnAppearSequenceComplete()
    {
        cachedOnComplete?.Invoke();
        cachedOnComplete = null;
        currentAppearSequence = null;
    }

    private HUD_PopupNav_SubRegionBtn GetOrCreateSubRegionButton(int _index)
    {
        if (_index < subRegionButtons.Count)
        {
            return subRegionButtons[_index];
        }

        if (null == subRegionBtnPrefab)
        {
            Debug.LogWarning("[HUD_PopupNav_SubRegionGroup] subRegionBtnPrefab이 인스펙터에 할당되지 않았고 캐싱된 버튼 수량 부족!");
            return null;
        }

        HUD_PopupNav_SubRegionBtn _newBtn = Instantiate(subRegionBtnPrefab, container);
        subRegionButtons.Add(_newBtn);
        return _newBtn;
    }

    private void DistributeButtonsEvenly()
    {
        if (null == container || 0 == activeSubRegionButtons.Count)
        {
            return;
        }

        int _count = activeSubRegionButtons.Count;
        float _containerWidth = container.rect.width;
        
        float _usableWidth = _containerWidth - paddingLeft - paddingRight;

        if (cachedSafeDistances.Length < _count)
        {
            cachedSafeDistances = new float[_count * 2];
        }
        float _totalRequiredSpace = 0f;

        for (int i = 0; i < _count; i++)
        {
            if (null != activeSubRegionButtons[i])
            {
                float _btnWidth = activeSubRegionButtons[i].GetActualVisualWidth();
                cachedSafeDistances[i] = Mathf.Max(_btnWidth * minDistanceRatioX, minDistanceAbsoluteX);
            }
            else
            {
                cachedSafeDistances[i] = minDistanceAbsoluteX;
            }
            _totalRequiredSpace += cachedSafeDistances[i];
        }
        
        float _availableRandomSpace = _usableWidth - _totalRequiredSpace;

        if (0f > _availableRandomSpace)
        {
            _availableRandomSpace = 0f;
        }

        if (cachedGaps.Length < _count + 1)
        {
            cachedGaps = new float[(_count + 1) * 2];
        }
        float _sumGaps = 0f;
        for (int i = 0; i < _count + 1; i++)
        {
            cachedGaps[i] = UnityEngine.Random.Range(0.2f, 1.0f);
            _sumGaps += cachedGaps[i];
        }

        for (int i = 0; i < _count + 1; i++)
        {
            cachedGaps[i] = (cachedGaps[i] / _sumGaps) * _availableRandomSpace;
        }

        float _currentLocalX = -(_containerWidth * container.pivot.x) + paddingLeft;
        
        bool _isUpDownUp = 0.5f < UnityEngine.Random.value;

        for (int i = 0; i < _count; i++)
        {
            RectTransform _btnRect = activeSubRegionButtons[i].GetComponent<RectTransform>();
            if (null == _btnRect) continue;

            _currentLocalX += cachedGaps[i];

            float _mySafeDistance = cachedSafeDistances[i];
            float _targetX = _currentLocalX + (_mySafeDistance * 0.5f);

            _currentLocalX += _mySafeDistance;

            float t = 0f;
            if (1 < _count)
            {
                t = (i / (float)(_count - 1)) * 2f - 1f;
            }
            float _archOffset = archHeightY * (1f - (t * t));
            
            float _randomOffsetY = 0f;
            if (preventStraightLine)
            {
                bool _goesUp = _isUpDownUp ? (0 == i % 2) : (0 != i % 2);
                _randomOffsetY = UnityEngine.Random.Range(minRandomScatterY, randomScatterRangeY);
                _randomOffsetY = _goesUp ? _randomOffsetY : -_randomOffsetY;
            }
            else
            {
                _randomOffsetY = UnityEngine.Random.Range(-randomScatterRangeY, randomScatterRangeY);
            }
            
            float _targetY = baseOffsetY + _archOffset + _randomOffsetY;

            float _containerHeight = container.rect.height;
            float _maxY = (_containerHeight * (1f - container.pivot.y)) - paddingTop;
            float _minY = -(_containerHeight * container.pivot.y) + paddingBottom;
            
            float _btnHalfHeight = _btnRect.rect.height * 0.5f;
            _maxY -= _btnHalfHeight;
            _minY += _btnHalfHeight;

            _targetY = Mathf.Clamp(_targetY, _minY, _maxY);

            _btnRect.anchoredPosition = new Vector2(Mathf.Round(_targetX), Mathf.Round(_targetY));
        }
    }

    private void PlayDisappearSequence(Action _onComplete)
    {
        if (null != currentAppearSequence && true == currentAppearSequence.IsActive())
        {
            currentAppearSequence.Kill();
            currentAppearSequence = null;
        }

        if (null != sequenceCoroutine)
        {
            StopCoroutine(sequenceCoroutine);
        }
        sequenceCoroutine = StartCoroutine(CoPlayDisappearSequence(_onComplete));
    }

    private System.Collections.IEnumerator CoPlayDisappearSequence(Action _onComplete)
    {
        completedDisappearCount = 0;
        int _totalCount = activeSubRegionButtons.Count;

        if (0 == _totalCount)
        {
            _onComplete?.Invoke();
            yield break;
        }

        for (int i = _totalCount - 1; 0 <= i; i--)
        {
            activeSubRegionButtons[i].PlayDisappearMotion(cachedOnDisappearMotionComplete); // Removed lambda
        }

        float _timeout = 2f; 
        while (completedDisappearCount < _totalCount && 0f < _timeout)
        {
            _timeout -= Time.deltaTime;
            yield return null;
        }

        sequenceCoroutine = null;
        _onComplete?.Invoke();
    }

    private void OnDisappearMotionComplete()
    {
        completedDisappearCount++;
    }

    public Sequence PlayAppearSequence()
    {
        Sequence _seq = DOTween.Sequence();
        
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            HUD_PopupNav_SubRegionBtn _btn = activeSubRegionButtons[i];
            
            _btn.transform.localScale = new Vector3(1f, 0.01f, 1f);
            _btn.transform.localRotation = Quaternion.identity;

            float _startTime = i * appearSequenceDelay;
            
            Sequence _btnSeq = DOTween.Sequence();

            _btnSeq.AppendCallback(_btn.CachedActivate);
            
            _btnSeq.Append(_btn.transform.DOScaleY(1f, appearAnimDuration).SetEase(appearAnimEase));
            
            _seq.Insert(_startTime, _btnSeq);
        }
        
        return _seq;
    }

    private int pendingUnlockCount = 0;
    private Action onMultipleUnlockComplete;

    public void PlayUnlockProduction(List<ForestType> _forestTypes, Action _onComplete)
    {
        if (null == _forestTypes || 0 == _forestTypes.Count)
        {
            _onComplete?.Invoke();
            return;
        }

        pendingUnlockCount = 0;
        onMultipleUnlockComplete = _onComplete;

        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            if (true == _forestTypes.Contains(activeSubRegionButtons[i].GetForestType()))
            {
                pendingUnlockCount++;
                activeSubRegionButtons[i].PlayUnlockMotion(cachedOnSingleSubRegionUnlockComplete);
            }
        }
        
        if (0 == pendingUnlockCount)
        {
            _onComplete?.Invoke();
        }
    }

    private void OnSingleSubRegionUnlockComplete()
    {
        pendingUnlockCount--;
        if (0 >= pendingUnlockCount)
        {
            onMultipleUnlockComplete?.Invoke();
            onMultipleUnlockComplete = null;
        }
    }

    public void SetSelectSubRegion(ForestType _forestType)
    {
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            activeSubRegionButtons[i].SetSelectedState(_forestType == activeSubRegionButtons[i].GetForestType());
        }
    }

    public HUD_PopupNav_SubRegionBtn GetSubRegionButton(ForestType _forestType)
    {
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            if (_forestType == activeSubRegionButtons[i].GetForestType()) // Yoda notation
            {
                return activeSubRegionButtons[i];
            }
        }
        return null;
    }

    public void ClearAllNewIndicators()
    {
        for (int i = 0; i < subRegionButtons.Count; i++)
        {
            if (true == subRegionButtons[i].gameObject.activeSelf)
            {
                subRegionButtons[i].ClearNewIndicator();
            }
        }
    }

    public void StopAllHoverEffects()
    {
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            if (null != activeSubRegionButtons[i] && true == activeSubRegionButtons[i].gameObject.activeSelf)
            {
                activeSubRegionButtons[i].StopAllTreePropHoverEffects();
            }
        }
    }

    public void ResetState()
    {
        if (null != currentAppearSequence && true == currentAppearSequence.IsActive())
        {
            currentAppearSequence.Kill();
            currentAppearSequence = null;
        }

        if (null != sequenceCoroutine)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }

        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            if (null != activeSubRegionButtons[i])
            {
                activeSubRegionButtons[i].ResetState();
            }
        }
        activeSubRegionButtons.Clear();
        pendingMapType = MapType.None;
        pendingRegionTransform = null;
    }

    public void EvaluateAllHoverStates()
    {
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            if (null != activeSubRegionButtons[i])
            {
                activeSubRegionButtons[i].EvaluateHoverState();
            }
        }
    }

    private void OnDestroy()
    {
        if (null != currentAppearSequence && true == currentAppearSequence.IsActive())
        {
            currentAppearSequence.Kill();
            currentAppearSequence = null;
        }
        
        // Ensure tweens or sequence states are safely cleared
        if (null != sequenceCoroutine)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }
    }
}
