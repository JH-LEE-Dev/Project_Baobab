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

    [Header("Region Background Settings")]
    [Tooltip("맵 타입별 배경 이미지 설정")]
    [SerializeField] private List<RegionBackgroundSetup> regionBackgrounds = new List<RegionBackgroundSetup>();

    [Tooltip("버튼 등장 시 초기 X축 스케일")]
    [SerializeField] private float startScaleX = 0.1f;
    [Tooltip("버튼 등장 시 초기 Y축 스케일")]
    [SerializeField] private float startScaleY = 1.0f;
    
    [Header("Region Button Appear Animation")]
    [Tooltip("쫙 펼쳐지는 연출 시간")]
    [SerializeField] private float appearDuration = 0.35f;
    [Tooltip("쫙 펼쳐지는 연출 이즈(Ease)")]
    [SerializeField] private Ease appearEase = Ease.OutBack;

    private HUD_PopupNav_Main mainController;
    private LocalizationManager localizationManager;
    private readonly List<HUD_PopupNav_RegionBtn> regionButtons = new List<HUD_PopupNav_RegionBtn>(8);

    private Sequence appearSeq;

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
                    if (null == _existBtns[i]) continue;
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
            if (null == regionButtons[i]) continue;
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

        // 3. 버튼 생성 및 노출
        int _btnIndex = 0;
        for (int i = 0; i < _validRegions.Count; i++)
        {

            if (regionButtons.Count > _btnIndex)
            {
                HUD_PopupNav_RegionBtn _btn = regionButtons[_btnIndex];
                if (null == _btn) continue;

                Sprite _bgSprite = null;
                for (int j = 0; j < regionBackgrounds.Count; j++)
                {
                    if (_validRegions[i].mapType == regionBackgrounds[j].mapType)
                    {
                        _bgSprite = regionBackgrounds[j].backgroundImage;
                        break;
                    }
                }

                _btn.Initialize(mainController, _validRegions[i], localizationManager, _bgSprite, _btnIndex);
                
                // 연출을 위해 임시로 꺼두거나 초기 스케일 세팅
                _btn.gameObject.SetActive(false);
                _btn.transform.localScale = new Vector3(startScaleX, startScaleY, 1f);
                _btn.transform.localRotation = Quaternion.identity;
                
                _btnIndex++;
            }
            else
            {
                Debug.LogWarning($"[HUD_PopupNav_RegionGroup] 캐싱된 대지역 버튼 갯수가 부족합니다! (필요 수: {_btnIndex + 1}, 캐싱 수: {regionButtons.Count})");
            }
        }
    }

    public Sequence PlayAppearSequence()
    {
        if (null != appearSeq && appearSeq.IsActive())
        {
            appearSeq.Kill();
            appearSeq = null;
        }
        
        appearSeq = DOTween.Sequence();
        
        for (int i = 0; i < regionButtons.Count; i++)
        {
            HUD_PopupNav_RegionBtn _btn = regionButtons[i];
            if (null == _btn) continue;
            
            // 데이터가 초기화되어 사용할 버튼인지 확인 (초기화시 activeSelf가 false로 설정되어 있음)
            if (false == _btn.gameObject.activeSelf && MapType.None != _btn.GetMapType())
            {
                float _startTime = i * appearSequenceDelay;

                // 버튼 개별 시퀀스 생성
                Sequence _btnSeq = DOTween.Sequence();
                
                // 애니메이션 시작 시점에 버튼 켜기
                _btnSeq.AppendCallback(_btn.CachedActivate);

                // 쫙 펴지는 스케일 연출
                _btnSeq.Append(_btn.transform.DOScale(1f, appearDuration).SetEase(appearEase));

                // 전체 시퀀스의 본인 타이밍에 개별 시퀀스 합체
                appearSeq.Insert(_startTime, _btnSeq);
            }
        }

        return appearSeq;
    }

    public void PlayUnlockProduction(MapType _mapType, float _speedRate, Action _onComplete)
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (null == regionButtons[i]) continue;
            if (_mapType == regionButtons[i].GetMapType())
            {
                regionButtons[i].PlayUnlockMotion(_onComplete, _speedRate);
                return;
            }
        }
        
        _onComplete?.Invoke();
    }

    public void SetSelectRegion(MapType _mapType, bool _playClickAnim = true)
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (null == regionButtons[i]) continue;
            regionButtons[i].SetSelectedState(_mapType == regionButtons[i].GetMapType(), _playClickAnim);
        }
    }

    public Transform GetRegionTransform(MapType _mapType)
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (null == regionButtons[i]) continue;
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
            if (null == regionButtons[i]) continue;
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
            if (null == regionButtons[i]) continue;
            if (_mapType == regionButtons[i].GetMapType())
            {
                regionButtons[i].ClearNewIndicator();
                break;
            }
        }
    }

    public void EvaluateAllHoverStates()
    {
        for (int i = 0; i < regionButtons.Count; i++)
        {
            if (null == regionButtons[i]) continue;
            if (true == regionButtons[i].gameObject.activeSelf)
            {
                regionButtons[i].EvaluateHoverState();
            }
        }
    }

    private void OnDestroy()
    {
        if (null != appearSeq && appearSeq.IsActive())
        {
            appearSeq.Kill();
            appearSeq = null;
        }
    }
}
