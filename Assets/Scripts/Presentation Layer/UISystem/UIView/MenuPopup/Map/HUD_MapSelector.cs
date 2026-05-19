using PresentationLayer.DOTweenAnimationSystem;
using System;
using System.Collections.Generic;
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
        [SerializeField] private UISelectionCursor selectorCursor;
        [SerializeField] private HUD_MapSubSelector subSelector;
        [SerializeField] private HUD_MapSelectorButton selectButton;
        [SerializeField] private HUD_MapSelectorButton exitButton;
        [SerializeField] private ObjectMotionPlayer motionPlayer;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Transform regionContainer;
        [SerializeField] private GameObject regionPrefab;

        [Header("Settings")]
        [SerializeField] private float snapSpeed = 10.0f;
        [SerializeField] private float itemSpacing = 600.0f;
        [SerializeField] private float dragSensitivity = 1.0f;
        [SerializeField] private float fadeDuration = 0.5f;
        [SerializeField] private float maxBackgroundAlpha = 0.9f;

        // //내부 의존성
        private IMapDataProvider mapDataProvider;
        private ITimeDataProvider timeDataProvider;
        private RectTransform containerRect;

        private readonly List<HUD_MapRegion> spawnedRegions = new List<HUD_MapRegion>(8);
        private HUD_MapRegion currentFocusedRegion;
        private Action<MapType, ForestType> onConfirmCallback;
        private Action onExitCallback;
        
        private bool isInitialized = false;
        private bool isDayTime = true;
        private bool isDragging = false;
        private bool isClosing = false;
        private float targetPosX = 0.0f;

        // //퍼블릭 초기화 및 제어 메서드

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

            if (null != selectorCursor)
                selectorCursor.Initialize(selectorCursor.CursorSize);

            if (null != subSelector)
                subSelector.Initialize(RefreshSelectButtonState, OnHoverEnterItem, OnHoverExitItem);

            if (null != selectButton)
                selectButton.Initialize(HandleConfirm, "OkHover", "OkHoverOff", "OkClickTwist", OnHoverEnterItem, OnHoverExitItem);

            if (null != exitButton)
                exitButton.Initialize(HandleExit, "ExitHover", "ExitHoverOff", "ExitClickTwist", OnHoverEnterItem, OnHoverExitItem);

            SetupRegionsFromData();

            isInitialized = true;

            SetUIAlpha(0.0f);

            
            motionPlayer?.Play("Cloud1Floating"); 
            motionPlayer?.Play("Cloud2Floating");
        }

        public void MapSelectorOpen()
        {
            gameObject.SetActive(true);
            isClosing = false;

            selectorCursor?.HideImmediately();

            if (null == subSelector)
                return;

            // UI가 열릴 때 현재 위치를 계산하여 즉시 스냅 (Update 루프에서 숨겨지는 것 방지)
            int _closestIndex = 0;
            if (null != containerRect)
            {
                _closestIndex = Mathf.RoundToInt(-containerRect.localPosition.x / itemSpacing);
                _closestIndex = Mathf.Clamp(_closestIndex, 0, spawnedRegions.Count - 1);
                
                Vector3 _snapPos = containerRect.localPosition;
                _snapPos.x = -(_closestIndex * itemSpacing);
                containerRect.localPosition = _snapPos;
                targetPosX = _snapPos.x;
            }
            
            FocusRegion(_closestIndex, true);

            // 하위 지역이 즉시 보이도록 설정
            subSelector.SetVisibility(true);

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
                currentFocusedRegion.PlayEndAnimation(null, true);
        }

        // //내부 로직

        private void AddRegion(MapEnvironmentDataInfo _info)
        {
            if (null == regionPrefab || null == regionContainer)
                return;

            GameObject _obj = Instantiate(regionPrefab, regionContainer);
            HUD_MapRegion _region = _obj.GetComponent<HUD_MapRegion>();

            if (null != _region)
            {
                _region.Initialize();
                _region.Setup(_info.mapType.ToString(), _info);
                
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

            for (int _i = 0; _i < spawnedRegions.Count; _i++)
                if (null != spawnedRegions[_i])
                    Destroy(spawnedRegions[_i].gameObject);
            
            spawnedRegions.Clear();

            for (int _i = 0; _i < _db.mapDatas.Count; _i++)
            {
                MapEnvironmentDataInfo _info = _db.mapDatas[_i];
                if (MapType.Town == _info.mapType) continue;
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

        private void HandleExit() => onExitCallback?.Invoke();

        private void FocusRegion(int _index, bool _shouldPlayAnimation = false)
        {
            if (0 > _index || _index >= spawnedRegions.Count)
            {
                if (null != selectButton)
                    selectButton.SetDimmed(true);
                return;
            }

            MapEnvironmentDatabase _db = mapDataProvider.GetMapEnvironmentDatabase();

            currentFocusedRegion = spawnedRegions[_index];
            targetPosX = -(_index * itemSpacing);

            for (int _i = 0; _i < spawnedRegions.Count; _i++)
            {
                if (null != spawnedRegions[_i])
                {
                    bool _isFocus = (_i == _index);
                    bool _play = _isFocus && _shouldPlayAnimation;
                    bool _instant = !_isFocus && _shouldPlayAnimation;

                    spawnedRegions[_i].Setup(spawnedRegions[_i].GetMapName(), _db.mapDatas[_index], _play, _instant);
                    spawnedRegions[_i].SetFocus(_isFocus);
                }
            }

            if (null != subSelector && null != currentFocusedRegion && null != currentFocusedRegion.GetMapEnvironmentInfo().forestDatas)
                subSelector.SetSubRegions(currentFocusedRegion.GetMapEnvironmentInfo().forestDatas);

            selectorCursor?.HideImmediately();
            RefreshSelectButtonState();
        }

        private void RefreshSelectButtonState()
        {
            if (null != currentFocusedRegion && null != subSelector)
                currentFocusedRegion.UpdateObjectCount(subSelector.GetSelectedRegionNumber());

            if (null == selectButton)
                return;

            bool _isDimmed = (null == currentFocusedRegion) || (ForestType.None == subSelector.GetSelectedForestType());
            selectButton.SetDimmed(_isDimmed);
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
                
            selectorCursor?.HideImmediately();
        }

        private void OnHoverEnterItem(RectTransform _targetRect)
        {
            if (null != selectorCursor && null != _targetRect)
                selectorCursor.Show(_targetRect);
        }

        private void OnHoverExitItem() => selectorCursor?.Hide();

        // //Event System 구현부

        public void OnBeginDrag(PointerEventData _eventData)
        {
            isDragging = true;
            if (null != subSelector) subSelector.SetVisibility(false);
            if (null != selectButton) selectButton.SetDimmed(true);
            selectorCursor?.HideImmediately();
        }

        public void OnDrag(PointerEventData _eventData)
        {
            if (null == containerRect) return;
            Vector3 _pos = containerRect.localPosition;
            _pos.x += _eventData.delta.x * dragSensitivity;
            containerRect.localPosition = _pos;
        }

        public void OnEndDrag(PointerEventData _eventData)
        {
            isDragging = false;
            if (0 == spawnedRegions.Count) return;

            int _closestIndex = Mathf.RoundToInt(-containerRect.localPosition.x / itemSpacing);
            _closestIndex = Mathf.Clamp(_closestIndex, 0, spawnedRegions.Count - 1);
            FocusRegion(_closestIndex, false);
        }

        // //유니티 이벤트 함수

        private void Update()
        {
            if (false == isInitialized || true == isDragging || true == isClosing)
                return;

            if (null == containerRect)
                return;

            Vector3 _currentPos = containerRect.localPosition;
            if (0.1f < Mathf.Abs(_currentPos.x - targetPosX))
            {
                _currentPos.x = Mathf.Lerp(_currentPos.x, targetPosX, Time.deltaTime * snapSpeed);
                containerRect.localPosition = _currentPos;

                if (null != subSelector) subSelector.SetVisibility(false);
                if (null != selectButton) selectButton.SetDimmed(true);
                selectorCursor?.HideImmediately();
            }
            else
            {
                if (null != subSelector) subSelector.SetVisibility(true);
                RefreshSelectButtonState();
            }
        }
    }
}