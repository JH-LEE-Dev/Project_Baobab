using PresentationLayer.DOTweenAnimationSystem;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 메인 맵 지역(MapRegion)들과 서브 셀렉터(MapSubSelector)를 총괄하며 최종 MapType을 결정하는 클래스입니다.
    /// 마우스 슬라이드를 통해 지역을 이동시키고 중앙에 위치한 지역을 자동으로 포커싱합니다.
    /// </summary>
    public class HUD_MapSelector : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        // //외부 의존성
        [Header("References")]
        [SerializeField] private HUD_MapSubSelector subSelector; // 서브 지역 셀렉터
        [SerializeField] private HUD_MapSunMoon sunMoon;         // 밤낮 연출 관리자
        [SerializeField] private HUD_MapSelectorButton selectButton; // 선택 확인 버튼
        [SerializeField] private HUD_MapSelectorButton exitButton;     // 종료 버튼
        [SerializeField] private CanvasGroup canvasGroup;          // 알파 통합 관리를 위한 캔버스 그룹
        [SerializeField] private Image backgroundImage;          // 백그라운드 이미지
        [SerializeField] private Transform regionContainer;     // 지역 항목 부모 컨테이너
        [SerializeField] private GameObject regionPrefab;       // 지역 항목 프리팹

        [Header("Settings")]
        [SerializeField] private float snapSpeed = 10.0f;        // 스냅 이동 속도
        [SerializeField] private float itemSpacing = 600.0f;     // 항목 간 가로 간격
        [SerializeField] private float dragSensitivity = 1.0f;   // 드래그 민감도 (추가)
        [SerializeField] private float fadeDuration = 0.5f;      // 페이드 애니메이션 시간
        [SerializeField] private float maxBackgroundAlpha = 0.9f; // 백그라운드 최대 알파

        // //내부 의존성
        private IMapDataProvider mapDataProvider;
        private ITimeDataProvider timeDataProvider;
        private RectTransform containerRect;

        private List<HUD_MapRegion> spawnedRegions = new List<HUD_MapRegion>(8);
        private HUD_MapRegion currentFocusedRegion;
        private Action<MapType, ForestType> onConfirmCallback;
        private Action onExitCallback;
        
        private bool isInitialized = false;
        private bool isDayTime = true;
        private bool isDragging = false;
        private bool isClosing = false;
        private float targetPosX = 0.0f;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 셀렉터를 초기화하고 콜백을 등록합니다.
        /// </summary>
        public void Initialize(IMapDataProvider _mapDataProvider, IWeatherProvider _weatherProvider, ITimeDataProvider _timeDataProvider, Action<MapType, ForestType> _onConfirm, Action _onExit)
        {
            if (true == isInitialized)
                return;

            mapDataProvider = _mapDataProvider;
            timeDataProvider = _timeDataProvider;
            onConfirmCallback = _onConfirm;
            onExitCallback = _onExit;

            if (null == canvasGroup)
                canvasGroup = GetComponent<CanvasGroup>();

            if (null != backgroundImage)
            {
                Color _color = backgroundImage.color;
                _color.a = maxBackgroundAlpha;
                backgroundImage.color = _color;
            }

            if (null != regionContainer)
                containerRect = regionContainer.GetComponent<RectTransform>();

            if (null != subSelector)
                subSelector.Initialize(RefreshSelectButtonState);

            if (null != sunMoon)
                sunMoon.Initialize();

            if (null != selectButton)
                selectButton.Initialize(HandleConfirm, "OkHover", "OkHoverOff", "OkClickTwist");

            if (null != exitButton)
                exitButton.Initialize(HandleExit, "ExitHover", "ExitHoverOff", "ExitClickTwist");

            SetupRegionsFromData();

            isInitialized = true;

            if (null != timeDataProvider)
                SetTimeState(timeDataProvider.isDay);
            else
                UpdateSunMoonState();

            SetUIAlpha(0.0f);
        }

        public void SetTimeState(bool _isDay)
        {
            isDayTime = _isDay;
            UpdateSunMoonState();
        }

        /// <summary>
        /// 새로운 지역 항목을 생성하고 등록합니다.
        /// </summary>
        public void AddRegion(MapEnvironmentDataInfo _info)
        {
            if (null == regionPrefab || null == regionContainer)
                return;

            GameObject _obj = Instantiate(regionPrefab, regionContainer);
            HUD_MapRegion _region = _obj.GetComponent<HUD_MapRegion>();

            if (null != _region)
            {
                _region.Initialize();
                _region.Setup(_info.mapType.ToString(), _info);
                
                // 가로 배치를 위한 위치 설정
                RectTransform _rect = _region.GetComponent<RectTransform>();
                if (null != _rect)
                    _rect.anchoredPosition = new Vector2(spawnedRegions.Count * itemSpacing, 0.0f);

                spawnedRegions.Add(_region);
            }
        }

        private void SetupRegionsFromData()
        {
            if (null == mapDataProvider)
                return;

            MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();
            if (null == _db.mapDatas)
                return;

            // 기존 생성된 지역 제거
            for (int _i = 0; _i < spawnedRegions.Count; _i++)
                if (null != spawnedRegions[_i])
                    Destroy(spawnedRegions[_i].gameObject);
            
            spawnedRegions.Clear();

            for (int _i = 0; _i < _db.mapDatas.Count; _i++)
            {
                MapEnvironmentDataInfo _info = _db.mapDatas[_i];
                
                if (MapType.Town == _info.mapType)
                    continue;

                AddRegion(_info);
            }

            if (0 < spawnedRegions.Count)
                FocusRegion(0);
        }

        private void HandleConfirm()
        {
            if (null == currentFocusedRegion || null == subSelector)
                return;

            MapType _mapType = currentFocusedRegion.GetMapType();
            ForestType _forestType = subSelector.GetSelectedForestType();

            if (MapType.None != _mapType && ForestType.None != _forestType)
                onConfirmCallback?.Invoke(_mapType, _forestType);
        }

        private void HandleExit()
        {
            onExitCallback?.Invoke();
        }

        private void UpdateSunMoonState()
        {
            if (null == sunMoon)
                return;

            if (true == isDayTime)
                sunMoon.SetRotation(55f, 0.25f);
            else
                sunMoon.SetRotation(235f, 0.25f);
        }

        private void FocusRegion(int _index, bool _shouldPlayAnimation = false)
        {
            if (0 > _index || _index >= spawnedRegions.Count)
            {
                if (null != selectButton)
                    selectButton.SetDimmed(true);
                return;
            }

            currentFocusedRegion = spawnedRegions[_index];
            targetPosX = -(_index * itemSpacing);

            // 모든 지역의 포커스 상태 및 셋업 업데이트
            for (int _i = 0; _i < spawnedRegions.Count; _i++)
            {
                if (null != spawnedRegions[_i])
                {
                    bool _isFocus = (_i == _index);
                    // 포커스된 지역이고 애니메이션 재생 요청이 있을 때만 true 전달
                    // 오픈 시에 포커스가 아닌 지역들은 등장 애니메이션을 스킵(즉시 로드 상태)
                    bool _play = _isFocus && _shouldPlayAnimation;
                    bool _instant = !_isFocus && _shouldPlayAnimation;

                    spawnedRegions[_i].Setup(spawnedRegions[_i].GetMapName(), spawnedRegions[_i].GetMapEnvironmentInfo(), _play, _instant);
                    spawnedRegions[_i].SetFocus(_isFocus);
                }
            }

            if (null != subSelector && null != currentFocusedRegion.GetMapEnvironmentInfo().forestDatas)
                subSelector.SetSubRegions(currentFocusedRegion.GetMapEnvironmentInfo().forestDatas);

            RefreshSelectButtonState();
        }

        private void RefreshSelectButtonState()
        {
            if (null != timeDataProvider)
                SetTimeState(timeDataProvider.isDay);

            if (null != currentFocusedRegion && null != subSelector)
                currentFocusedRegion.UpdateObjectCount(subSelector.GetSelectedRegionNumber());

            if (null == selectButton)
                return;

            bool _isDimmed = (null == currentFocusedRegion) || (ForestType.None == subSelector.GetSelectedForestType());
            selectButton.SetDimmed(_isDimmed);
        }

        public void MapSelectorOpen()
        {
            gameObject.SetActive(true);
            isClosing = false;

            if (null == subSelector)
                return;

            // UI가 열릴 때 현재 위치한 지역 애니메이션 재생
            int _closestIndex = 0;
            if (null != containerRect)
            {
                _closestIndex = Mathf.RoundToInt(-containerRect.localPosition.x / itemSpacing);
                _closestIndex = Mathf.Clamp(_closestIndex, 0, spawnedRegions.Count - 1);
            }
            
            FocusRegion(_closestIndex, true); // 오픈 시에는 애니메이션 재생 요청

            MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();
            if (null == _db.mapDatas)
                return;

            for (int _i = 0; _i < _db.mapDatas.Count; _i++)
            {
                MapEnvironmentDataInfo _info = _db.mapDatas[_i];

                if (MapType.Town == _info.mapType)
                    continue;

                subSelector.UpdateHiddenGauges(_info.forestDatas);
            }

            PlayFadeAnimation(1.0f);
        }

        public void MapSelectorClose()
        {
            if (true == isClosing)
                return;

            isClosing = true;

            if (null != subSelector)
                subSelector.Close();

            PlayFadeAnimation(0.0f, DeactivateSelector);

            if (null != currentFocusedRegion)
            {
                currentFocusedRegion.PlayEndAnimation(null, true);
            }
        }

        private void PlayFadeAnimation(float _targetAlpha, Action _onComplete = null)
        {
            if (null == canvasGroup)
            {
                _onComplete?.Invoke();
                return;
            }

            canvasGroup.DOFade(_targetAlpha, fadeDuration)
                .SetEase(Ease.InOutSine)
                .OnComplete(() => _onComplete?.Invoke());
        }

        private void SetUIAlpha(float _alpha)
        {
            if (null != canvasGroup)
                canvasGroup.alpha = _alpha;
        }

        private void DeactivateSelector()
        {
            gameObject.SetActive(false);

            if (null != subSelector)
                subSelector.ClearSelection();
        }

        // //Event System 구현부

        public void OnBeginDrag(PointerEventData _eventData)
        {
            isDragging = true;

            if (null != subSelector)
                subSelector.SetVisibility(false);

            if (null != selectButton)
                selectButton.SetDimmed(true);
        }

        public void OnDrag(PointerEventData _eventData)
        {
            if (null == containerRect)
                return;

            Vector3 _pos = containerRect.localPosition;
            _pos.x += _eventData.delta.x * dragSensitivity;
            containerRect.localPosition = _pos;
        }

        public void OnEndDrag(PointerEventData _eventData)
        {
            isDragging = false;
            
            if (0 == spawnedRegions.Count)
                return;

            // 드래그 종료 시 가장 가까운 인덱스 계산하여 스냅
            int _closestIndex = Mathf.RoundToInt(-containerRect.localPosition.x / itemSpacing);
            _closestIndex = Mathf.Clamp(_closestIndex, 0, spawnedRegions.Count - 1);
            
            FocusRegion(_closestIndex, false); // 슬라이드 이동 시에는 애니메이션 재생 안 함
        }

        // //유니티 이벤트 함수

        private void Update()
        {
            if (false == isInitialized || true == isDragging || true == isClosing)
                return;

            if (null == containerRect)
                return;

            // 목표 위치로 부드럽게 스냅 이동 (Lerp)
            Vector3 _currentPos = containerRect.localPosition;
            if (0.1f < Mathf.Abs(_currentPos.x - targetPosX))
            {
                _currentPos.x = Mathf.Lerp(_currentPos.x, targetPosX, Time.deltaTime * snapSpeed);
                containerRect.localPosition = _currentPos;

                // 이동 중에는 숨김/흐리게
                if (null != subSelector)
                    subSelector.SetVisibility(false);

                if (null != selectButton)
                    selectButton.SetDimmed(true);
            }
            else
            {
                // 목표 위치에 도달하여 멈췄을 때만 표시/밝게
                if (null != subSelector)
                    subSelector.SetVisibility(true);

                RefreshSelectButtonState();
            }
        }
    }
}
