using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using PresentationLayer.DOTweenAnimationSystem;

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
        [SerializeField] private HUD_MapSelectButton selectButton; // 선택 확인 버튼
        [SerializeField] private Transform regionContainer;     // 지역 항목 부모 컨테이너
        [SerializeField] private GameObject regionPrefab;       // 지역 항목 프리팹

        [Header("Settings")]
        [SerializeField] private float snapSpeed = 10.0f;        // 스냅 이동 속도
        [SerializeField] private float itemSpacing = 600.0f;     // 항목 간 가로 간격
        [SerializeField] private float dragSensitivity = 1.0f;   // 드래그 민감도 (추가)

        // //내부 의존성
        private IMapDataProvider mapDataProvider;
        private ITimeDataProvider timeDataProvider;
        private RectTransform containerRect;

        private List<HUD_MapRegion> spawnedRegions = new List<HUD_MapRegion>(8);
        private HUD_MapRegion currentFocusedRegion;
        private Action<MapType, ForestType> onConfirmCallback;
        
        private bool isInitialized = false;
        private bool isDayTime = true;
        private bool isDragging = false;
        private float targetPosX = 0.0f;

        private static readonly string dayMotionKey = "Day";
        private static readonly string nightMotionKey = "Night";

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 셀렉터를 초기화하고 콜백을 등록합니다.
        /// </summary>
        public void Initialize(IMapDataProvider _mapDataProvider, IWeatherProvider _weatherProvider, ITimeDataProvider _timeDataProvider, Action<MapType, ForestType> _onConfirm)
        {
            if (true == isInitialized)
                return;

            mapDataProvider = _mapDataProvider;
            timeDataProvider = _timeDataProvider;
            onConfirmCallback = _onConfirm;

            if (null != regionContainer)
                containerRect = regionContainer.GetComponent<RectTransform>();

            if (null != subSelector)
                subSelector.Initialize();

            if (null != sunMoon)
                sunMoon.Initialize();

            if (null != selectButton)
                selectButton.Initialize(HandleConfirm);

            SetupRegionsFromData();

            isInitialized = true;

            if (null != timeDataProvider)
                SetTimeState(timeDataProvider.isDay);
            else
                UpdateSunMoonState();
        }

        /// <summary>
        /// 밤낮 상태를 설정하고 관련 애니메이션을 재생합니다.
        /// </summary>
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

        private void UpdateSunMoonState()
        {
            if (null == sunMoon)
                return;

            if (true == isDayTime)
                sunMoon.PlayMotion(dayMotionKey);
            else
                sunMoon.PlayMotion(nightMotionKey);
        }

        private void FocusRegion(int _index)
        {
            if (0 > _index || _index >= spawnedRegions.Count)
                return;

            currentFocusedRegion = spawnedRegions[_index];
            targetPosX = -(_index * itemSpacing);

            // 모든 지역의 포커스 상태 업데이트
            for (int _i = 0; _i < spawnedRegions.Count; _i++)
            {
                if (null != spawnedRegions[_i])
                    spawnedRegions[_i].SetFocus(_i == _index);
            }

            if (null != subSelector && null != currentFocusedRegion.GetMapEnvironmentInfo().forestDatas)
                subSelector.SetSubRegions(currentFocusedRegion.GetMapEnvironmentInfo().forestDatas);
        }

        // //Event System 구현부

        public void OnBeginDrag(PointerEventData _eventData)
        {
            isDragging = true;

            if (null != subSelector)
                subSelector.SetVisibility(false);

            if (null != selectButton)
                selectButton.gameObject.SetActive(false);
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
            
            FocusRegion(_closestIndex);
        }

        // //유니티 이벤트 함수

        private void Update()
        {
            if (false == isInitialized || true == isDragging)
                return;

            if (null == containerRect)
                return;

            // 목표 위치로 부드럽게 스냅 이동 (Lerp)
            Vector3 _currentPos = containerRect.localPosition;
            if (0.1f < Mathf.Abs(_currentPos.x - targetPosX))
            {
                _currentPos.x = Mathf.Lerp(_currentPos.x, targetPosX, Time.deltaTime * snapSpeed);
                containerRect.localPosition = _currentPos;

                // 이동 중에는 숨김
                if (null != subSelector)
                    subSelector.SetVisibility(false);

                if (null != selectButton)
                    selectButton.gameObject.SetActive(false);
            }
            else
            {
                // 목표 위치에 도달하여 멈췄을 때만 표시
                if (null != subSelector)
                    subSelector.SetVisibility(true);

                if (null != selectButton && false == selectButton.gameObject.activeSelf)
                    selectButton.gameObject.SetActive(true);
            }
        }
    }
}
