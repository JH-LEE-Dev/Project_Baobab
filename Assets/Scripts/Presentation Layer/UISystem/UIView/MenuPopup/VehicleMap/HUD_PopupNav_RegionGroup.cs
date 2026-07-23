using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using PresentationLayer.DOTweenAnimationSystem;

public class HUD_PopupNav_RegionGroup : MonoBehaviour
{
    [Serializable]
    public struct RegionBackgroundSetup
    {
        public MapType mapType;
        public Sprite backgroundImage;
    }

    [Header("Container & Global Animation")]
    [Tooltip("대지역 버튼 부모(컨테이너)")]
    [SerializeField] private RectTransform container;
    [Tooltip("대지역 버튼 순차 등장 시 버튼 간 딜레이")]
    [SerializeField] private float appearSequenceDelay = 0.05f;

    [Header("Region Button Setup & Animation")]
    [Tooltip("기본 프리팹 (초기화 후 비활성화됨)")]
    [SerializeField] private HUD_PopupNav_RegionBtn regionBtnPrefab;

    [Header("Region Background Settings")]
    [Tooltip("맵 타입별 배경 이미지 설정")]
    [SerializeField] private List<RegionBackgroundSetup> regionBackgrounds = new List<RegionBackgroundSetup>();

    [Tooltip("버튼 등장 시 초기 X축 스케일 (짜부된 상태)")]
    [SerializeField] private float startScaleX = 0.15f;
    [Tooltip("버튼 등장 시 초기 Y축 스케일")]
    [SerializeField] private float startScaleY = 1.0f;
    [Tooltip("버튼 등장 시 초기 Z축 회전(기울기) 각도")]
    [SerializeField] private float startRotationZ = -15f;
    
    [Header("Region Button Phase 1 (Rotation & Small Scale)")]
    [Tooltip("페이즈 1: 회전 복구와 동시에 살짝 커질 목표 X 스케일")]
    [SerializeField] private float phase1ScaleX = 0.3f;
    [Tooltip("페이즈 1: 회전 복구와 동시에 살짝 커질 목표 Y 스케일")]
    [SerializeField] private float phase1ScaleY = 1.0f;
    [Tooltip("페이즈 1: 연출 시간")]
    [SerializeField] private float phase1Duration = 0.15f;
    [Tooltip("페이즈 1: 연출 이즈(Ease)")]
    [SerializeField] private Ease phase1Ease = Ease.OutQuad;

    [Header("Region Button Phase 2 (Full Scale Bounce)")]
    [Tooltip("페이즈 2: 원래 크기로 쫙 펴지는 연출 시간")]
    [SerializeField] private float phase2Duration = 0.25f;
    [Tooltip("페이즈 2: 연출 이즈(Ease)")]
    [SerializeField] private Ease phase2Ease = Ease.OutBack;

    private HUD_PopupNav_Main mainController;
    private LocalizationManager localizationManager;
    private readonly List<HUD_PopupNav_RegionBtn> regionButtons = new List<HUD_PopupNav_RegionBtn>(8);

    public void Initialize(HUD_PopupNav_Main _mainController, LocalizationManager _localizationManager)
    {
        mainController = _mainController;
        localizationManager = _localizationManager;

        // 컨테이너 하위에 이미 배치되어 있는 버튼들이 있다면 사전 캐싱
        if (null != container)
        {
            HUD_PopupNav_RegionBtn[] _existBtns = container.GetComponentsInChildren<HUD_PopupNav_RegionBtn>(true);
            if (null != _existBtns)
            {
                for (int i = 0; i < _existBtns.Length; i++)
                {
                    if (false == regionButtons.Contains(_existBtns[i]))
                    {
                        _existBtns[i].gameObject.SetActive(false);
                        regionButtons.Add(_existBtns[i]);
                    }
                }
            }
        }
    }

    public void SetupRegions(IMapDataProvider _mapDataProvider)
    {
        if (null == _mapDataProvider)
        {
            return;
        }

        MapEnvironmentDatabase _db = _mapDataProvider.GetMapEnvironmentDatabase();
        if (null == _db.mapDatas)
        {
            return;
        }

        // 기존 생성된 버튼 초기화 및 비활성화
        for (int i = 0; i < regionButtons.Count; i++)
        {
            regionButtons[i].gameObject.SetActive(false);
        }

        // 1. 유효한 대지역 목록 추출 (Town 제외)
        List<MapEnvironmentDataInfo> _validRegions = new List<MapEnvironmentDataInfo>(4);
        for (int i = 0; i < _db.mapDatas.Count; i++)
        {
            if (MapType.Town == _db.mapDatas[i].mapType)
            {
                continue;
            }
            _validRegions.Add(_db.mapDatas[i]);
        }

        // 2. 두 번째 대지역(1번 인덱스) 해금 가능 상태 체크
        bool _isSecondStageAvailable = false;
        if (1 < _validRegions.Count)
        {
            _isSecondStageAvailable = _validRegions[1].bCanAccess || _validRegions[1].isUnlocked;
        }

        // 3. 버튼 생성 및 노출
        int _btnIndex = 0;
        for (int i = 0; i < _validRegions.Count; i++)
        {
            // 두 번째 대지역이 해금 가능 상태가 아닐 때, 첫 번째 지역 이후의 지역들은 노출하지 않음
            if (false == _isSecondStageAvailable && 0 < i)
            {
                break;
            }

            HUD_PopupNav_RegionBtn _btn = GetOrCreateRegionButton(_btnIndex);
            if (null != _btn)
            {
                Sprite _bgSprite = null;
                for (int j = 0; j < regionBackgrounds.Count; j++)
                {
                    if (regionBackgrounds[j].mapType == _validRegions[i].mapType)
                    {
                        _bgSprite = regionBackgrounds[j].backgroundImage;
                        break;
                    }
                }

                _btn.Initialize(mainController, _validRegions[i], localizationManager, _bgSprite);
                
                // 연출을 위해 임시로 꺼두거나 초기 스케일/로테이션 세팅 (세부 연출 전 자리잡기)
                _btn.gameObject.SetActive(false);
                _btn.transform.localScale = new Vector3(startScaleX, startScaleY, 1f);
                _btn.transform.localRotation = Quaternion.Euler(0, 0, startRotationZ);
                
                _btnIndex++;
            }
        }
    }

    public Sequence PlayAppearSequence()
    {
        Sequence _seq = DOTween.Sequence();
        
        for (int i = 0; i < regionButtons.Count; i++)
        {
            HUD_PopupNav_RegionBtn _btn = regionButtons[i];
            
            // 데이터가 초기화되어 사용할 버튼인지 확인 (초기화시 activeSelf가 false로 설정되어 있음)
            if (false == _btn.gameObject.activeSelf && MapType.None != _btn.GetMapType())
            {
                float _startTime = i * appearSequenceDelay;

                // 버튼 개별 시퀀스 생성
                Sequence _btnSeq = DOTween.Sequence();
                
                // 애니메이션 시작 시점에 버튼 켜기 (초기 상태는 이미 SetupRegions에서 세팅됨)
                _btnSeq.AppendCallback(_btn.CachedActivate);

                // Phase 1: 빠르게 회전을 0,0,0으로 복구하며 동시에 스케일이 살짝 커짐
                _btnSeq.Append(_btn.transform.DOLocalRotate(Vector3.zero, phase1Duration).SetEase(phase1Ease));
                _btnSeq.Join(_btn.transform.DOScale(new Vector3(phase1ScaleX, phase1ScaleY, 1f), phase1Duration).SetEase(phase1Ease));

                // Phase 2: 회전이 끝난 직후, 원래 스케일(1)로 쫙 펴짐
                _btnSeq.Append(_btn.transform.DOScale(1f, phase2Duration).SetEase(phase2Ease));

                // 전체 시퀀스의 본인 타이밍에 개별 시퀀스 합체
                _seq.Insert(_startTime, _btnSeq);
            }
        }

        return _seq;
    }

    private HUD_PopupNav_RegionBtn GetOrCreateRegionButton(int _index)
    {
        if (_index < regionButtons.Count)
        {
            return regionButtons[_index];
        }

        if (null == regionBtnPrefab)
        {
            Debug.LogWarning("[HUD_PopupNav_RegionGroup] regionBtnPrefab이 인스펙터에 할당되지 않았고 캐싱된 버튼 수량 부족!");
            return null;
        }

        HUD_PopupNav_RegionBtn _newBtn = Instantiate(regionBtnPrefab, container);
        regionButtons.Add(_newBtn);
        return _newBtn;
    }

    public void PlayUnlockProduction(MapType _mapType, float _speedRate, Action _onComplete)
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (_mapType == regionButtons[i].GetMapType())
            {
                regionButtons[i].PlayUnlockMotion(_onComplete);
                return;
            }
        }
        
        _onComplete?.Invoke();
    }

    public void SetSelectRegion(MapType _mapType, bool _playClickAnim = true)
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            regionButtons[i].SetSelectedState(_mapType == regionButtons[i].GetMapType(), _playClickAnim);
        }
    }

    public Transform GetRegionTransform(MapType _mapType)
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (_mapType == regionButtons[i].GetMapType())
            {
                return regionButtons[i].transform;
            }
        }
        return container;
    }

    public void ClearAllNewIndicators()
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (true == regionButtons[i].gameObject.activeSelf)
            {
                regionButtons[i].ClearNewIndicator();
            }
        }
    }

    public void ClearNewIndicator(MapType _mapType)
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (_mapType == regionButtons[i].GetMapType())
            {
                regionButtons[i].ClearNewIndicator();
                break;
            }
        }
    }
}
