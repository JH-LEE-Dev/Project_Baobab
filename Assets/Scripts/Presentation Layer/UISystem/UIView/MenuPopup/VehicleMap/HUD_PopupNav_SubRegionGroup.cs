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
    [Tooltip("버튼과 버튼 사이의 최소 물리적 여백 (최소 픽셀 간격)")]
    [SerializeField] private float minButtonGapX = 20f;

    [Header("SubRegion Button Setup & Animation")]
    [Tooltip("기본 프리팹 (초기화 후 비활성화됨)")]
    [SerializeField] private HUD_PopupNav_SubRegionBtn subRegionBtnPrefab;
    [Tooltip("사전 생성할 서브지역 버튼 개수")]
    [SerializeField] private int maxPrewarmCount = 8;
    [Tooltip("각 서브지역 버튼 등장(스케일 업) 연출 시간")]
    [SerializeField] private float appearAnimDuration = 0.15f;
    [Tooltip("각 서브지역 버튼 등장(스케일 업) 연출 이즈(Ease)")]
    [SerializeField] private Ease appearAnimEase = Ease.OutBack;
    


    private HUD_PopupNav_Main mainController;
    private LocalizationManager localizationManager;
    private TreeVisualDataBase treeVisualDataBase;
    private IMapDataProvider mapDataProvider;
    private ICursorBoxUI cursorBoxUI;
    
    private readonly List<HUD_PopupNav_SubRegionBtn> subRegionButtons = new List<HUD_PopupNav_SubRegionBtn>(16);
    private readonly List<HUD_PopupNav_SubRegionBtn> activeSubRegionButtons = new List<HUD_PopupNav_SubRegionBtn>(8);

    private Sequence currentAppearSequence;
    
    private MapType currentDisplayedMapType = MapType.None;
    private MapType pendingMapType = MapType.None;
    private Transform pendingRegionTransform;
    private Action pendingOnComplete;

    // Fixed GC allocs by making these class-level fields
    private readonly List<TreeVisualData> tempTreeVisualDatas = new List<TreeVisualData>(8);
    private readonly float[] cachedSafeDistances = new float[32];
    private readonly float[] cachedGaps = new float[32];
    private readonly Vector2[] cachedCalculatedPositions = new Vector2[32];
    
    private int completedDisappearCount;
    private Action cachedOnComplete;
    private Action pendingDisappearComplete;

    private Action cachedOnDisappearSequenceCompleteForToggle;
    private TweenCallback cachedOnAppearSequenceComplete;
    private Action cachedOnDisappearMotionComplete;
    private Action cachedOnSingleSubRegionUnlockComplete;

    public void Initialize(HUD_PopupNav_Main _mainController, LocalizationManager _localizationManager, ICursorBoxUI _cursorBoxUI, TreeVisualDataBase _treeVisualDataBase)
    {
        mainController = _mainController;
        localizationManager = _localizationManager;
        cursorBoxUI = _cursorBoxUI;
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

            int _needCount = maxPrewarmCount - subRegionButtons.Count;
            for (int i = 0; _needCount > i; i++)
            {
                HUD_PopupNav_SubRegionBtn _newBtn = Instantiate(subRegionBtnPrefab, container);
                _newBtn.gameObject.SetActive(false);
                subRegionButtons.Add(_newBtn);
            }
        }

        if (null == cachedOnDisappearSequenceCompleteForToggle) cachedOnDisappearSequenceCompleteForToggle = OnDisappearSequenceCompleteForToggle;
        if (null == cachedOnAppearSequenceComplete) cachedOnAppearSequenceComplete = OnAppearSequenceComplete;
        if (null == cachedOnDisappearMotionComplete) cachedOnDisappearMotionComplete = OnDisappearMotionComplete;
        if (null == cachedOnSingleSubRegionUnlockComplete) cachedOnSingleSubRegionUnlockComplete = OnSingleSubRegionUnlockComplete;
    }

    public void ShowSubRegionsForMap(MapType _mapType, Transform _regionBtnTransform, IMapDataProvider _provider, Action _onComplete = null)
    {
        mapDataProvider = _provider;
        
        if (_mapType == currentDisplayedMapType && 0 < activeSubRegionButtons.Count)
        {
            _onComplete?.Invoke();
            return;
        }

        if (0 < activeSubRegionButtons.Count)
        {
            pendingMapType = _mapType;
            pendingRegionTransform = _regionBtnTransform;
            pendingOnComplete = _onComplete;
            
            if (null == pendingDisappearComplete)
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

        currentDisplayedMapType = _mapType;
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
                            tempTreeVisualDatas.Clear(); // GC 할당을 줄이기 위해 미리 할당된 리스트 사용
                            if (null != treeVisualDataBase && null != _subInfo.spawnTreeTypes)
                            {
                                int _treeCount = Mathf.Min(_subInfo.spawnTreeTypes.Count, 2);
                                for (int k = 0; k < _treeCount; k++)
                                {
                                    tempTreeVisualDatas.Add(treeVisualDataBase.Get(_subInfo.spawnTreeTypes[k].treeType));
                                }
                            }
                            
                            _btn.Initialize(mainController, _subInfo, localizationManager, cursorBoxUI, _regionInfo.mapType, tempTreeVisualDatas, j);
                            activeSubRegionButtons.Add(_btn);
                        }
                    }
                }
                break;
            }
        }

        DistributeButtonsEvenly(_mapType);

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
            currentAppearSequence.OnComplete(cachedOnAppearSequenceComplete); // 람다식 제거됨
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

        Debug.LogWarning($"[HUD_PopupNav_SubRegionGroup] maxPrewarmCount({maxPrewarmCount}) 부족으로 런타임 동적 생성됨.");
        HUD_PopupNav_SubRegionBtn _newBtn = Instantiate(subRegionBtnPrefab, container);
        _newBtn.gameObject.SetActive(false);
        subRegionButtons.Add(_newBtn);
        return _newBtn;
    }

    private void DistributeButtonsEvenly(MapType _mapType)
    {
        if (null == container || 0 == activeSubRegionButtons.Count)
        {
            return;
        }

        int _count = activeSubRegionButtons.Count;
        float _containerWidth = container.rect.width;
        float _containerHeight = container.rect.height;
        float _usableWidth = _containerWidth - paddingLeft - paddingRight;

        // 대지역별 고유 Seed 기반의 결정적(Deterministic) 레이아웃 산출기 초기화
        int _seed = ((int)_mapType * 10007) + (_count * 389) + 48271;
        System.Random _rng = new System.Random(_seed);

        // Y축 상/하한선 계산 및 역전 방지 (패딩이 컨테이너보다 크더라도 최소 50px의 안전 높이 마진 확보)
        float _topLimit = (_containerHeight * (1f - container.pivot.y)) - paddingTop;
        float _bottomLimit = -(_containerHeight * container.pivot.y) + paddingBottom;
        if (_bottomLimit >= _topLimit)
        {
            float _mid = (_bottomLimit + _topLimit) * 0.5f;
            _bottomLimit = _mid - 25f;
            _topLimit = _mid + 25f;
        }

        float _verticalMid = ((_bottomLimit + _topLimit) * 0.5f) + baseOffsetY;
        _verticalMid = Mathf.Clamp(_verticalMid, _bottomLimit + 10f, _topLimit - 10f);

        if (1 == _count)
        {
            RectTransform _singleBtnRect = activeSubRegionButtons[0].CachedRectTransform;
            if (null != _singleBtnRect)
            {
                float _centerX = -(_containerWidth * container.pivot.x) + paddingLeft + (_usableWidth * 0.5f);
                float _targetY = Mathf.Clamp(_verticalMid, _bottomLimit, _topLimit);
                _singleBtnRect.anchoredPosition = new Vector2(Mathf.Round(_centerX), Mathf.Round(_targetY));
            }
            return;
        }

        // 1. 각 버튼의 시각적 너비 및 최소 요구 중심 간격 계산
        float _minCenterSpan = 0f;
        for (int i = 0; i < _count; i++)
        {
            if (null != activeSubRegionButtons[i])
            {
                float _visualWidth = activeSubRegionButtons[i].GetActualVisualWidth();
                cachedSafeDistances[i] = Mathf.Max(_visualWidth * minDistanceRatioX, _visualWidth, minDistanceAbsoluteX * 0.5f);
            }
            else
            {
                cachedSafeDistances[i] = minDistanceAbsoluteX * 0.5f;
            }
        }

        for (int i = 0; i < _count - 1; i++)
        {
            float _requiredCenterDist = (cachedSafeDistances[i] * 0.5f) + (cachedSafeDistances[i + 1] * 0.5f) + minButtonGapX;
            _requiredCenterDist = Mathf.Max(_requiredCenterDist, minDistanceAbsoluteX);
            _minCenterSpan += _requiredCenterDist;
        }

        float _btn0HalfWidth = cachedSafeDistances[0] * 0.5f;
        float _btnLastHalfWidth = cachedSafeDistances[_count - 1] * 0.5f;
        float _totalRequiredWidth = _btn0HalfWidth + _minCenterSpan + _btnLastHalfWidth;
        float _availableExtraSpace = _usableWidth - _totalRequiredWidth;

        // 2. X 좌표 슬롯 및 가변 여백 산출
        float _baseLeftX = -(_containerWidth * container.pivot.x) + paddingLeft;

        if (0f < _availableExtraSpace)
        {
            // 여유 공간이 있을 때: 좌우 마진 및 버튼 간 추가 간격에 결정적 난수 가중치 분배
            float _sumWeights = 0f;
            for (int i = 0; i < _count + 1; i++)
            {
                cachedGaps[i] = (float)(_rng.NextDouble() * 0.7 + 0.3);
                _sumWeights += cachedGaps[i];
            }

            for (int i = 0; i < _count + 1; i++)
            {
                cachedGaps[i] = (cachedGaps[i] / _sumWeights) * _availableExtraSpace;
            }

            float _currentCenterX = _baseLeftX + cachedGaps[0] + _btn0HalfWidth;
            cachedCalculatedPositions[0].x = _currentCenterX;

            for (int i = 0; i < _count - 1; i++)
            {
                float _baseCenterDist = (cachedSafeDistances[i] * 0.5f) + (cachedSafeDistances[i + 1] * 0.5f) + minButtonGapX;
                _baseCenterDist = Mathf.Max(_baseCenterDist, minDistanceAbsoluteX);
                _currentCenterX += _baseCenterDist + cachedGaps[i + 1];
                cachedCalculatedPositions[i + 1].x = _currentCenterX;
            }
        }
        else
        {
            // 여유 공간이 좁을 때: usableWidth 내에서 버튼들을 균등 분할 배치하여 클리핑 방지
            float _firstCenterX = _baseLeftX + _btn0HalfWidth;
            float _lastCenterX = _baseLeftX + _usableWidth - _btnLastHalfWidth;

            if (_firstCenterX > _lastCenterX)
            {
                _lastCenterX = _firstCenterX;
            }

            for (int i = 0; i < _count; i++)
            {
                float _t = (float)i / (_count - 1);
                cachedCalculatedPositions[i].x = Mathf.Lerp(_firstCenterX, _lastCenterX, _t);
            }
        }

        // 3. Y 좌표 산출 (산 모양 Mountain / Arch 단일 형태로 고정)
        float _maxHalfSpan = (_topLimit - _bottomLimit) * 0.5f;
        float _baseDelta = true == preventStraightLine ? Mathf.Max(archHeightY, randomScatterRangeY, 25f) : 0f;
        float _heightDelta = Mathf.Clamp(_baseDelta, 0f, Mathf.Max(0f, _maxHalfSpan * 0.85f));

        if (2 == _count)
        {
            // 2개 버튼: 자연스러운 능선형 (좌측 하단 -> 우측 상단)
            cachedCalculatedPositions[0].y = _verticalMid - (_heightDelta * 0.4f);
            cachedCalculatedPositions[1].y = _verticalMid + (_heightDelta * 0.4f);
        }
        else if (3 == _count)
        {
            // 3개 버튼: 완벽한 삼각 산 모양 (양쪽 아래, 중앙 정상)
            cachedCalculatedPositions[0].y = _verticalMid - _heightDelta;
            cachedCalculatedPositions[1].y = _verticalMid + _heightDelta;
            cachedCalculatedPositions[2].y = _verticalMid - _heightDelta;
        }
        else
        {
            // 4개 이상 버튼: 산 모양 매크로 아치 (중앙 솟아오름)
            for (int i = 0; i < _count; i++)
            {
                float _t = (i / (float)(_count - 1)) * 2f - 1f;
                float _macroOffset = _heightDelta * (1f - (_t * _t));
                float _stagger = (0 == i % 2 ? 0.2f : -0.2f) * minRandomScatterY;

                cachedCalculatedPositions[i].y = (_verticalMid - (_heightDelta * 0.5f)) + _macroOffset + _stagger;
            }
        }

        // 1차 상하한 클램핑
        for (int i = 0; i < _count; i++)
        {
            cachedCalculatedPositions[i].y = Mathf.Clamp(cachedCalculatedPositions[i].y, _bottomLimit, _topLimit);
        }

        // 4. 2D 거리 검증 및 이완(Relaxation) 패스
        float _minSqDistance = minDistanceAbsoluteX * minDistanceAbsoluteX;
        for (int i = 0; i < _count - 1; i++)
        {
            for (int j = i + 1; j < _count; j++)
            {
                Vector2 _delta = cachedCalculatedPositions[j] - cachedCalculatedPositions[i];
                float _sqDist = _delta.sqrMagnitude;
                if (_sqDist < _minSqDistance && 0.001f < _sqDist)
                {
                    float _dist = Mathf.Sqrt(_sqDist);
                    float _overlap = (minDistanceAbsoluteX - _dist) * 0.5f;
                    Vector2 _pushDir = _delta / _dist;

                    cachedCalculatedPositions[i] -= _pushDir * _overlap;
                    cachedCalculatedPositions[j] += _pushDir * _overlap;
                }
            }
        }

        // 5. 최종 좌표 클램핑 및 적용
        for (int i = 0; i < _count; i++)
        {
            RectTransform _btnRect = activeSubRegionButtons[i].CachedRectTransform;
            if (null == _btnRect) continue;

            float _finalX = cachedCalculatedPositions[i].x;
            float _finalY = Mathf.Clamp(cachedCalculatedPositions[i].y, _bottomLimit, _topLimit);

            _btnRect.anchoredPosition = new Vector2(Mathf.Round(_finalX), Mathf.Round(_finalY));
        }
    }

    private void PlayDisappearSequence(Action _onComplete)
    {
        if (null != currentAppearSequence && true == currentAppearSequence.IsActive())
        {
            currentAppearSequence.Kill();
            currentAppearSequence = null;
        }

        completedDisappearCount = 0;
        int _totalCount = activeSubRegionButtons.Count;

        if (0 == _totalCount)
        {
            _onComplete?.Invoke();
            return;
        }

        pendingDisappearComplete = _onComplete;

        for (int i = _totalCount - 1; 0 <= i; i--)
        {
            activeSubRegionButtons[i].PlayDisappearMotion(cachedOnDisappearMotionComplete);
        }
    }

    private void OnDisappearMotionComplete()
    {
        completedDisappearCount++;
        if (completedDisappearCount >= activeSubRegionButtons.Count)
        {
            if (null != pendingDisappearComplete)
            {
                pendingDisappearComplete.Invoke();
                pendingDisappearComplete = null;
            }
        }
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
            if (_forestType == activeSubRegionButtons[i].GetForestType()) // Yoda 표기법 사용
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

    public IReadOnlyList<HUD_PopupNav_SubRegionBtn> GetActiveSubRegionButtons() => activeSubRegionButtons;

    public HUD_PopupNav_SubRegionBtn GetHoveredSubRegionButton()
    {
        for (int i = 0; activeSubRegionButtons.Count > i; i++)
        {
            if (null != activeSubRegionButtons[i] && true == activeSubRegionButtons[i].gameObject.activeSelf && true == activeSubRegionButtons[i].IsMouseOver())
            {
                return activeSubRegionButtons[i];
            }
        }
        return null;
    }

    public int GetSubRegionIndex(ForestType _forestType)
    {
        for (int i = 0; activeSubRegionButtons.Count > i; i++)
        {
            if (null != activeSubRegionButtons[i] && true == activeSubRegionButtons[i].gameObject.activeSelf && _forestType == activeSubRegionButtons[i].GetForestType())
            {
                return i;
            }
        }
        return -1;
    }

    public int GetFirstUnlockedSubRegionIndex()
    {
        for (int i = 0; activeSubRegionButtons.Count > i; i++)
        {
            if (null != activeSubRegionButtons[i] && true == activeSubRegionButtons[i].gameObject.activeSelf && true == activeSubRegionButtons[i].IsUnlocked)
            {
                return i;
            }
        }
        return 0;
    }

    public void FocusSubRegionButton(int _index)
    {
        for (int i = 0; activeSubRegionButtons.Count > i; i++)
        {
            if (null == activeSubRegionButtons[i] || false == activeSubRegionButtons[i].gameObject.activeSelf) continue;

            if (i == _index)
            {
                activeSubRegionButtons[i].TriggerHover();
            }
            else
            {
                activeSubRegionButtons[i].ForceStopHoverEffect();
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

        pendingDisappearComplete = null;

        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            if (null != activeSubRegionButtons[i])
            {
                activeSubRegionButtons[i].ResetState();
            }
        }
        activeSubRegionButtons.Clear();
        currentDisplayedMapType = MapType.None;
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
        
        pendingDisappearComplete = null;
    }
}
