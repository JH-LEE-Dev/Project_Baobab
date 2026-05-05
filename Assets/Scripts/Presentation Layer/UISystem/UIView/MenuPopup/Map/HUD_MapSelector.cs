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

        // //내부 의존성
        private List<HUD_MapRegion> spawnedRegions = new List<HUD_MapRegion>(8);
        private HUD_MapRegion currentFocusedRegion;
        private Action<MapType> onConfirmCallback;
        private bool isInitialized = false;
        private bool isDayTime = true;
        private bool isDragging = false;
        private float targetPosX = 0.0f;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 셀렉터를 초기화하고 콜백을 등록합니다.
        /// </summary>
        public void Initialize(Action<MapType> _onConfirm)
        {
            if (true == isInitialized)
                return;

            onConfirmCallback = _onConfirm;

            if (null != subSelector)
                subSelector.Initialize();

            if (null != sunMoon)
                sunMoon.Initialize();

            if (null != selectButton)
                selectButton.Initialize(HandleConfirm);

            isInitialized = true;
            UpdateSunMoonState();
        }

        private void HandleConfirm()
        {
            // TODO: 추후 GetFinalMapType()을 사용하여 실제 선택된 타입을 가져와야 함
            MapType _finalType = MapType.Forest1_1;

            if (MapType.None != _finalType)
                onConfirmCallback?.Invoke(_finalType);
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
        public void AddRegion(string _name, MapType _baseType, int _subCount)
        {
            if (null == regionPrefab || null == regionContainer)
                return;

            GameObject _obj = Instantiate(regionPrefab, regionContainer);
            HUD_MapRegion _region = _obj.GetComponent<HUD_MapRegion>();

            if (null != _region)
            {
                _region.Initialize();
                _region.Setup(_name, _baseType);
                
                // 가로 배치를 위한 위치 설정
                RectTransform _rect = _region.GetComponent<RectTransform>();
                if (null != _rect)
                    _rect.anchoredPosition = new Vector2(spawnedRegions.Count * itemSpacing, 0.0f);

                spawnedRegions.Add(_region);
            }

            // 최초 생성 시 첫 번째 지역으로 타겟 설정
            if (1 == spawnedRegions.Count)
                FocusRegion(0);
        }

        /// <summary>
        /// 현재 선택된 메인 지역의 타입과 서브 지역 번호를 조합하여 최종 MapType을 반환합니다.
        /// </summary>
        public MapType GetFinalMapType()
        {
            if (null == currentFocusedRegion || null == subSelector)
                return MapType.None;

            MapType _baseType = currentFocusedRegion.GetMapType();
            int _subNumber = subSelector.GetSelectedRegionNumber();

            if (-1 == _subNumber)
                return _baseType;

            return (MapType)((int)_baseType + (_subNumber - 1));
        }

        // //내부 로직

        private void UpdateSunMoonState()
        {
            if (null == sunMoon)
                return;

            if (true == isDayTime)
                sunMoon.PlayMotion("Day");
            else
                sunMoon.PlayMotion("Night");
        }

        private void FocusRegion(int _index)
        {
            if (0 > _index || _index >= spawnedRegions.Count)
                return;

            currentFocusedRegion = spawnedRegions[_index];
            targetPosX = -(_index * itemSpacing);

            // TODO: 실제 데이터 시트에서 서브 지역 개수를 가져와야 함
            if (null != subSelector)
                subSelector.SetSubRegionCount(3);
        }

        // //Event System 구현부

        public void OnBeginDrag(PointerEventData _eventData)
        {
            isDragging = true;
        }

        public void OnDrag(PointerEventData _eventData)
        {
            if (null == regionContainer)
                return;

            Vector3 _pos = regionContainer.localPosition;
            _pos.x += _eventData.delta.x;
            regionContainer.localPosition = _pos;
        }

        public void OnEndDrag(PointerEventData _eventData)
        {
            isDragging = false;
            
            if (0 == spawnedRegions.Count)
                return;

            // 드래그 종료 시 가장 가까운 인덱스 계산하여 스냅
            int _closestIndex = Mathf.RoundToInt(-regionContainer.localPosition.x / itemSpacing);
            _closestIndex = Mathf.Clamp(_closestIndex, 0, spawnedRegions.Count - 1);
            
            FocusRegion(_closestIndex);
        }

        private void Update()
        {
            if (false == isInitialized || true == isDragging)
                return;

            if (null == regionContainer)
                return;

            // 목표 위치로 부드럽게 스냅 이동 (Lerp)
            Vector3 _currentPos = regionContainer.localPosition;
            if (0.1f < Mathf.Abs(_currentPos.x - targetPosX))
            {
                _currentPos.x = Mathf.Lerp(_currentPos.x, targetPosX, Time.deltaTime * snapSpeed);
                regionContainer.localPosition = _currentPos;
            }
        }
    }
}
