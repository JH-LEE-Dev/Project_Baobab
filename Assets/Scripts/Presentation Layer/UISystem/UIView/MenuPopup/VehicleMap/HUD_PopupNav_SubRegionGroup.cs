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
    [Tooltip("다른 지역으로 전환 시 모든 버튼이 사라진 후 다음 버튼들이 나타나기 전 대기 시간")]
    [SerializeField] private float transitionDelay = 0.15f;

    [Header("Layout & Distribution")]
    [Tooltip("전체 버튼들의 기본 Y축 오프셋 (아랫쪽 배치를 위해 음수 권장)")]
    [SerializeField] private float baseOffsetY = -50f;
    [Tooltip("자연스러운 배치를 위한 중앙부 솟아오름(아치형) 기본 높이 편차")]
    [SerializeField] private float archHeightY = 30f;
    [Tooltip("각 버튼별 X축 랜덤 분산 범위 (세그먼트 폭에 대한 비율, 0~0.4 추천. 순서가 안 바뀌게 제한)")]
    [SerializeField] private float randomScatterRatioX = 0.25f;
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

    [Header("SubRegion Button Setup & Animation")]
    [Tooltip("기본 프리팹 (초기화 후 비활성화됨)")]
    [SerializeField] private HUD_PopupNav_SubRegionBtn subRegionBtnPrefab;
    [Tooltip("각 서브지역 버튼 등장(스케일 업) 연출 시간")]
    [SerializeField] private float appearAnimDuration = 0.15f;
    [Tooltip("각 서브지역 버튼 등장(스케일 업) 연출 이즈(Ease)")]
    [SerializeField] private Ease appearAnimEase = Ease.OutBack;
    
    [Tooltip("스케일업 직후 꽂히는 흔들림(Shake) 연출 시간")]
    [SerializeField] private float shakeDuration = 0.2f;
    [Tooltip("흔들림 강도 (Z축 회전 각도)")]
    [SerializeField] private float shakeStrength = 10f;
    [Tooltip("흔들림 진동 수 (Vibrato)")]
    [SerializeField] private int shakeVibrato = 10;

    private HUD_PopupNav_Main mainController;
    private LocalizationManager localizationManager;
    private IMapDataProvider mapDataProvider;
    
    private readonly List<HUD_PopupNav_SubRegionBtn> subRegionButtons = new List<HUD_PopupNav_SubRegionBtn>(16);
    private readonly List<HUD_PopupNav_SubRegionBtn> activeSubRegionButtons = new List<HUD_PopupNav_SubRegionBtn>(8);

    private Coroutine sequenceCoroutine;
    private WaitForSeconds cachedSequenceWait;
    
    private MapType pendingMapType;
    private Transform pendingRegionTransform;

    public void Initialize(HUD_PopupNav_Main _mainController, LocalizationManager _localizationManager)
    {
        mainController = _mainController;
        localizationManager = _localizationManager;

        // 컨테이너 하위에 이미 배치되어 있는 버튼들이 있다면 사전 캐싱
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
    }

    public void ShowSubRegionsForMap(MapType _mapType, Transform _regionBtnTransform, IMapDataProvider _provider)
    {
        mapDataProvider = _provider;
        
        if (0 < activeSubRegionButtons.Count)
        {
            pendingMapType = _mapType;
            pendingRegionTransform = _regionBtnTransform;
            PlayDisappearSequence(OnDisappearSequenceCompleteForToggle);
        }
        else
        {
            SetupAndPlayAppearSequence(_mapType, _regionBtnTransform);
        }
    }

    private void OnDisappearSequenceCompleteForToggle()
    {
        SetupAndPlayAppearSequence(pendingMapType, pendingRegionTransform);
    }

    private void SetupAndPlayAppearSequence(MapType _mapType, Transform _regionBtnTransform)
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

        // 대상 대지역 정보 탐색
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
                            _btn.gameObject.SetActive(false); // 연출 시작 전까지 비활성화
                            _btn.Initialize(mainController, _subInfo, localizationManager, _regionInfo.mapType);
                            activeSubRegionButtons.Add(_btn);
                        }
                    }
                }
                break;
            }
        }

        // 버튼들을 컨테이너 가로폭에 맞춰 균등 배치
        DistributeButtonsEvenly();

        // 동적 앵커링 옵션이 켜져 있을 때만 대지역 위치로 컨테이너 이동
        if (true == useDynamicAnchoring && null != _regionBtnTransform && null != container)
        {
            container.position = _regionBtnTransform.position;
            container.anchoredPosition += new Vector2(anchorOffsetX, 0f);
        }

        // 세팅이 끝난 후 자체적으로 등장 시퀀스를 재생합니다.
        PlayAppearSequence().Play();
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
        float _segmentWidth = _containerWidth / _count;

        // Container의 Pivot X 값에 따라 좌측 끝(Local X) 좌표를 계산
        float _leftEdgeX = -(_containerWidth * container.pivot.x);
        
        // 각 버튼이 속한 분할 영역(Segment)의 중앙에 배치하기 위한 시작 X좌표
        float _startX = _leftEdgeX + (_segmentWidth * 0.5f);

        // 이번 배치에 적용할 지그재그 시작 방향 (true면 위->아래->위, false면 아래->위->아래)
        bool _isUpDownUp = 0.5f < UnityEngine.Random.value;

        for (int i = 0; i < _count; i++)
        {
            RectTransform _btnRect = activeSubRegionButtons[i].GetComponent<RectTransform>();
            if (null != _btnRect)
            {
                // 기본 X좌표 (분할된 구역의 중앙)
                float _baseTargetX = _startX + (i * _segmentWidth);
                
                // 순서(1->2->3)가 절대 뒤바뀌지 않도록 각자의 구역 내에서만 X축 랜덤 이동
                float _randomOffsetX = UnityEngine.Random.Range(-_segmentWidth * randomScatterRatioX, _segmentWidth * randomScatterRatioX);
                float _targetX = _baseTargetX + _randomOffsetX;
                
                // 아치형 기본 레이아웃 계산
                float t = 0f;
                if (1 < _count)
                {
                    t = (i / (float)(_count - 1)) * 2f - 1f; // -1 ~ 1 사이로 정규화 (중앙이 0)
                }
                float _archOffset = archHeightY * (1f - (t * t));
                
                // Y축 랜덤 분산 추가 (일렬 방지 로직 적용)
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

                // 컨테이너 영역 밖으로 나가지 않도록 Y축 클램핑 (안전 영역 보장)
                float _containerHeight = container.rect.height;
                float _maxY = (_containerHeight * (1f - container.pivot.y)) - paddingTop;
                float _minY = -(_containerHeight * container.pivot.y) + paddingBottom;
                
                // 버튼 자체의 크기(절반 높이)도 고려하여 클램핑 기준 강화
                float _btnHalfHeight = _btnRect.rect.height * 0.5f;
                _maxY -= _btnHalfHeight;
                _minY += _btnHalfHeight;

                _targetY = Mathf.Clamp(_targetY, _minY, _maxY);

                // X축 또한 좌우 영역 밖으로 나가지 않도록 클램핑
                float _maxX = (_containerWidth * (1f - container.pivot.x)) - paddingRight;
                float _minX = -(_containerWidth * container.pivot.x) + paddingLeft;
                
                float _btnHalfWidth = _btnRect.rect.width * 0.5f;
                _maxX -= _btnHalfWidth;
                _minX += _btnHalfWidth;

                _targetX = Mathf.Clamp(_targetX, _minX, _maxX);

                _btnRect.anchoredPosition = new Vector2(_targetX, _targetY);
            }
        }
    }

    private void PlayDisappearSequence(Action _onComplete)
    {
        if (null != sequenceCoroutine)
        {
            StopCoroutine(sequenceCoroutine);
        }
        sequenceCoroutine = StartCoroutine(CoPlayDisappearSequence(_onComplete));
    }

    private System.Collections.IEnumerator CoPlayDisappearSequence(Action _onComplete)
    {
        int _completedCount = 0;
        int _totalCount = activeSubRegionButtons.Count;

        if (0 == _totalCount)
        {
            _onComplete?.Invoke();
            yield break;
        }

        for (int i = _totalCount - 1; i >= 0; i--)
        {
            activeSubRegionButtons[i].PlayDisappearMotion(() => {
                _completedCount++;
            });
            yield return cachedSequenceWait;
        }

        float _timeout = 2f; // 무한 대기 방지
        while (_completedCount < _totalCount && 0f < _timeout)
        {
            _timeout -= Time.deltaTime;
            yield return null;
        }

        // 버튼이 사라졌다 나온 느낌(여운)을 주기 위한 짧은 딜레이
        if (0f < transitionDelay)
        {
            yield return new WaitForSeconds(transitionDelay);
        }

        _onComplete?.Invoke();
    }

    public Sequence PlayAppearSequence()
    {
        Sequence _seq = DOTween.Sequence();
        
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            HUD_PopupNav_SubRegionBtn _btn = activeSubRegionButtons[i];
            
            // 시퀀스가 생성될 때 DOTween이 현재 값을 캡처하므로, 미리 초기값을 세팅해둡니다. (X는 1, Y는 0.01f로 하여 0으로 인한 UI 레이아웃 무한대 팽창 버그 방지)
            _btn.transform.localScale = new Vector3(1f, 0.01f, 1f);
            _btn.transform.localRotation = Quaternion.identity;

            float _startTime = i * appearSequenceDelay;
            
            Sequence _btnSeq = DOTween.Sequence();

            // 애니메이션 시작 시간에 맞춰 버튼 활성화
            _btnSeq.AppendCallback(_btn.CachedActivate);
            
            // 1. 패널이 열리듯 Y스케일을 0에서 1로 쫙 펴주기 (X는 가만히 유지)
            _btnSeq.Append(_btn.transform.DOScaleY(1f, appearAnimDuration).SetEase(appearAnimEase));
            
            // 2. 펴지자마자 (스케일업 끝난 직후) 흔들흔들 거리는 펀치(Punch) 회전 연출로 '꽂힌 느낌' 부여
            _btnSeq.Append(_btn.transform.DOPunchRotation(new Vector3(0, 0, shakeStrength), shakeDuration, shakeVibrato, 1f));
            
            _seq.Insert(_startTime, _btnSeq);
        }
        
        return _seq;
    }

    public void PlayUnlockProduction(ForestType _forestType, Action _onComplete)
    {
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            if (_forestType == activeSubRegionButtons[i].GetForestType())
            {
                activeSubRegionButtons[i].PlayUnlockMotion(_onComplete);
                return;
            }
        }
        
        _onComplete?.Invoke();
    }

    public void SetSelectSubRegion(ForestType _forestType)
    {
        for (int i = 0; i < activeSubRegionButtons.Count; i++)
        {
            activeSubRegionButtons[i].SetSelectedState(_forestType == activeSubRegionButtons[i].GetForestType());
        }
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

    public void ResetState()
    {
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
}
