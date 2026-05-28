using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PresentationLayer.DOTweenAnimationSystem;
using System;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    [Serializable]
    public struct MapTreeVisual
    {
        public Image leafImage;
        public Image trunkImage;
    }

    public struct MapTreeColor
    {
        public Color leafColor;
        public Color trunkColor;
    }

    /// <summary>
    /// 특정 지역(Region)의 시각적 요소(지형, 나무, 데코, 이름)를 관리하고 애니메이션을 재생하는 클래스입니다.
    /// 해당 지역의 MapType 정보를 보유하여 상위 매니저와 소통합니다.
    /// </summary>
    public class HUD_MapRegion : MonoBehaviour
    {
        // //외부 의존성
        [Header("Ground Visuals")]
        [SerializeField] private Image[] groundImages;      // 지형 이미지들 (에디터 사전 배치)

        [Header("Object Visuals")]
        [SerializeField] private MapTreeVisual[] treeVisuals; // 나무 비주얼 (잎, 기둥)
        [SerializeField] private Image[] decoImages;        // 동물 대신 들어간 데코 이미지들

        [Header("State Visuals")]
        [SerializeField] private GameObject lockObject;     // 잠금 시 활성화될 오브젝트
        [SerializeField] private GameObject unlockObject;   // 해제 시 활성화될 오브젝트

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI mapNameText; // 맵 이름 TMP

        [Header("Animation")]
        [SerializeField] private ObjectMotionPlayer motionPlayer;
        [SerializeField] private float groundShowDelay = 0.015f;

        [Header("Focus Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float dimFactor = 0.5f;     // 비포커스 시 명암 계수

        // //내부 의존성
        private Color[] groundOriginalColors;
        private MapTreeColor[] treeOriginalColors;
        private Color[] decoOriginalColors;

        private MapEnvironmentDataInfo mapEnvironmentInfo;
        private string currentMapName = string.Empty;
        private float uiAlpha = 1.0f;
        private int currentVisibleCount = 1; 
        private bool isLocked = false;
        private bool isFocused = false;
        private bool isInitialized = false;

        // GC Alloc 최적화를 위한 문자열 캐싱
        private static readonly string[] groundTags = 
        { 
            "Ground_1", "Ground_2", "Ground_3", "Ground_4", 
            "Ground_5", "Ground_6", "Ground_7", "Ground_8",
            "Ground_9", "Ground_10", "Ground_11", "Ground_12",
            "Ground_13", "Ground_14", "Ground_15", "Ground_16"
        };

        private static readonly string[] treeTags = { "Tree_1", "Tree_2", "Tree_3" };
        private static readonly string[] decoTags = { "Deco_1", "Deco_2", "Deco_3" };

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            if (true == isInitialized)
                return;

            if (null == motionPlayer)
                motionPlayer = GetComponent<ObjectMotionPlayer>();

            CaptureOriginalColors();

            currentVisibleCount = 1;
            isInitialized = true;
        }

        public void Setup(string _mapName, MapEnvironmentDataInfo _info, bool _shouldPlayAnimation = false, bool _isInstant = false)
        {
            if (false == isInitialized)
                Initialize();

            currentMapName = _mapName;
            mapEnvironmentInfo = _info;

            if (null != mapNameText)
                mapNameText.text = currentMapName;

            // 초기 리전 셋업 시 모든 나무는 100% 항상 활성화 상태 유지
            currentVisibleCount = 1;
            if (null != treeVisuals)
            {
                for (int _i = 0; _i < treeVisuals.Length; _i++)
                {
                    if (null != treeVisuals[_i].leafImage)
                        treeVisuals[_i].leafImage.gameObject.SetActive(true);

                    if (null != treeVisuals[_i].trunkImage)
                        treeVisuals[_i].trunkImage.gameObject.SetActive(true);
                }
            }

            ResetObjectsVisibility(); // 데코 등의 노출 개수 초기 리셋

            if (true == _shouldPlayAnimation)
                PlayStartGroundAnimation();
            else if (true == _isInstant)
                PlayStartAnimationInstant();
        }

        /// <summary>
        /// 서브 지역 등급에 따라 데코 노출을 제어합니다.
        /// </summary>
        public void UpdateObjectCount(int _count)
        {
            if (false == isInitialized)
                Initialize();

            if (_count <= 0 || _count > 3)
                return;

            if (currentVisibleCount == _count)
                return;

            // 데코 연출 조절
            if (null != decoImages)
            {
                for (int _i = 0; _i < decoImages.Length; _i++)
                {
                    bool _shouldBeVisible = (_i < _count);
                    bool _wasVisible = (_i < currentVisibleCount);

                    if (_shouldBeVisible && !_wasVisible)
                    {
                        if (null != decoImages[_i])
                            decoImages[_i].gameObject.SetActive(true);

                        if (_i < decoTags.Length)
                            motionPlayer.Play(decoTags[_i], bReset: true);
                    }
                    else if (!_shouldBeVisible && _wasVisible)
                    {
                        if (_i < decoTags.Length)
                            motionPlayer.PlayBackward(decoTags[_i], bReset: true);
                    }
                }
            }

            currentVisibleCount = _count;
        }

        public void SetLock(bool _isLock)
        {
            isLocked = _isLock;

            if (null != lockObject)
                lockObject.SetActive(isLocked);

            if (null != unlockObject)
                unlockObject.SetActive(false == isLocked);
        }

        public void SetUIAlpha(float _alpha)
        {
            uiAlpha = _alpha;
            ApplyVisualState();
        }

        public void SetFocus(bool _isFocus)
        {
            isFocused = _isFocus;
            ApplyVisualState();
        }

        public void PlayStartGroundAnimation()
        {
            if (null == groundImages || null == groundTags)
                return;

            int _loopCount = Mathf.Min(groundImages.Length, groundTags.Length);
            int _finalIdx = _loopCount - 1;

            for (int _i = 0; _i < _loopCount; _i++)
            {
                if (null == groundImages[_i])
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.Play(groundTags[_i], bReset: true, _onComplete: PlayStartTreeAnimation, _forceDelayForward: groundShowDelay * _i);
                else
                    motionPlayer.Play(groundTags[_i], bReset: true, _forceDelayForward: groundShowDelay * _i);
            }
        }

        public void PlayStartTreeAnimation()
        {
            if (null == treeVisuals || null == treeTags)
                return;

            int _targetCount = Mathf.Min(treeVisuals.Length, treeTags.Length);

            int _finalIdx = _targetCount - 1;
            const float _delay = 0.05f;

            if (_targetCount <= 0)
            {
                PlayStartDecoAnimation();
                return;
            }

            for (int _i = 0; _i < _targetCount; _i++)
            {
                if (null == treeVisuals[_i].leafImage && null == treeVisuals[_i].trunkImage)
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.Play(treeTags[_i], bReset: true, _onComplete: PlayStartDecoAnimation, _forceDelayForward: _delay * _i);
                else
                    motionPlayer.Play(treeTags[_i], bReset: true, _forceDelayForward: _delay * _i);
            }
        }

        public void PlayStartDecoAnimation()
        {
            if (null == decoImages || null == decoTags)
                return;

            int _targetCount = Mathf.Min(currentVisibleCount, decoImages.Length);
            _targetCount = Mathf.Min(_targetCount, decoTags.Length);
            
            const float _delay = 0.05f;

            for (int _i = 0; _i < _targetCount; _i++)
            {
                if (null == decoImages[_i])
                    continue;

                motionPlayer.Play(decoTags[_i], bReset: true, _forceDelayForward: _delay * _i);
            }
        }

        public void PlayEndAnimation(UnityEngine.Events.UnityAction _onComplete, bool _isSkip = false)
        {
            PlayEndDecoAnimation(_onComplete, _isSkip);
            PlayEndTreeAnimation(_onComplete, _isSkip);
            PlayEndGroundAnimation(_onComplete, _isSkip);
        }

        public MapEnvironmentDataInfo GetMapEnvironmentInfo() => mapEnvironmentInfo;
        public bool IsLocked() => isLocked;
        public string GetMapName() => currentMapName;
        public MapType GetMapType() => mapEnvironmentInfo.mapType;

        // //내부 로직

        private void CaptureOriginalColors()
        {
            if (null != groundImages)
            {
                groundOriginalColors = new Color[groundImages.Length];
                for (int _i = 0; _i < groundImages.Length; _i++)
                    if (null != groundImages[_i])
                        groundOriginalColors[_i] = groundImages[_i].color;
            }

            if (null != treeVisuals)
            {
                treeOriginalColors = new MapTreeColor[treeVisuals.Length];
                for (int _i = 0; _i < treeVisuals.Length; _i++)
                {
                    treeOriginalColors[_i] = new MapTreeColor();
                    if (null != treeVisuals[_i].leafImage)
                        treeOriginalColors[_i].leafColor = treeVisuals[_i].leafImage.color;

                    if (null != treeVisuals[_i].trunkImage)
                        treeOriginalColors[_i].trunkColor = treeVisuals[_i].trunkImage.color;
                }
            }

            if (null != decoImages)
            {
                decoOriginalColors = new Color[decoImages.Length];
                for (int _i = 0; _i < decoImages.Length; _i++)
                    if (null != decoImages[_i])
                        decoOriginalColors[_i] = decoImages[_i].color;
            }
        }

        private void ResetObjectsVisibility()
        {
            if (null != decoImages)
            {
                for (int _i = 0; _i < decoImages.Length; _i++)
                    if (null != decoImages[_i])
                        decoImages[_i].gameObject.SetActive(_i < currentVisibleCount);
            }
        }

        private void PlayStartAnimationInstant()
        {
            if (null == motionPlayer)
                return;

            if (null != groundImages && null != groundTags)
            {
                int _count = Mathf.Min(groundImages.Length, groundTags.Length);
                for (int _i = 0; _i < _count; _i++)
                    if (null != groundImages[_i])
                        motionPlayer.Play(groundTags[_i], bReset: true, _skip: true);
            }

            if (null != treeVisuals && null != treeTags)
            {
                int _count = Mathf.Min(treeVisuals.Length, treeTags.Length);
                for (int _i = 0; _i < _count; _i++)
                    if (null != treeVisuals[_i].leafImage || null != treeVisuals[_i].trunkImage)
                        motionPlayer.Play(treeTags[_i], bReset: true, _skip: true);
            }

            if (null != decoImages && null != decoTags)
            {
                int _count = Mathf.Min(decoImages.Length, decoTags.Length);
                for (int _i = 0; _i < _count; _i++)
                    if (null != decoImages[_i])
                        motionPlayer.Play(decoTags[_i], bReset: true, _skip: true);
            }
        }

        private void PlayEndTreeAnimation(UnityEngine.Events.UnityAction _onComplete, bool _isSkip = false)
        {
            if (null == treeVisuals || 0 == treeVisuals.Length)
            {
                PlayEndGroundAnimation(_onComplete, _isSkip);
                return;
            }

            const float _delay = 0.05f;
            int _loopCount = Mathf.Min(treeVisuals.Length, treeTags.Length);

            for (int _i = _loopCount - 1; _i >= 0; _i--)
            {
                if (null == treeVisuals[_i].leafImage && null == treeVisuals[_i].trunkImage)
                    continue;

                motionPlayer.PlayBackward(treeTags[_i], bReset: true, _skip: _isSkip, _forceDelayBackward: _delay * (_loopCount - 1 - _i));
            }
        }

        private void PlayEndDecoAnimation(UnityEngine.Events.UnityAction _onComplete, bool _isSkip = false)
        {
            if (null == decoImages || 0 == decoImages.Length)
            {
                PlayEndTreeAnimation(_onComplete, _isSkip);
                return;
            }

            const float _delay = 0.05f;
            int _loopCount = Mathf.Min(decoImages.Length, decoTags.Length);

            for (int _i = _loopCount - 1; _i >= 0; _i--)
            {
                if (null == decoImages[_i])
                    continue;

                motionPlayer.PlayBackward(decoTags[_i], bReset: true, _skip: _isSkip, _forceDelayBackward: _delay * (_loopCount - 1 - _i));
            }
        }

        private void PlayEndGroundAnimation(UnityEngine.Events.UnityAction _onComplete, bool _isSkip = false)
        {
            if (null == groundImages || 0 == groundImages.Length || null == groundTags)
            {
                _onComplete?.Invoke();
                return;
            }

            int _loopCount = Mathf.Min(groundImages.Length, groundTags.Length);
            int _finalIdx = 0;
            const float _delay = 0.05f;

            for (int _i = _loopCount - 1; _i >= 0; _i--)
            {
                if (null == groundImages[_i])
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.PlayBackward(groundTags[_i], bReset: true, _onComplete: _onComplete, _skip: _isSkip, _forceDelayBackward: _delay * (_loopCount - 1 - _i));
                else
                    motionPlayer.PlayBackward(groundTags[_i], bReset: true, _skip: _isSkip, _forceDelayBackward: _delay * (_loopCount - 1 - _i));
            }
        }

        private void ApplyVisualState()
        {
            float _focusFactor = (true == isFocused) ? 1.0f : dimFactor;

            if (null != groundImages && null != groundOriginalColors)
                for (int _i = 0; _i < groundImages.Length; _i++)
                    if (null != groundImages[_i])
                        groundImages[_i].color = new Color(groundOriginalColors[_i].r * _focusFactor, groundOriginalColors[_i].g * _focusFactor, groundOriginalColors[_i].b * _focusFactor, groundOriginalColors[_i].a * uiAlpha);

            if (null != treeVisuals && null != treeOriginalColors)
                for (int _i = 0; _i < treeVisuals.Length; _i++)
                {
                    if (null != treeVisuals[_i].leafImage)
                    {
                        Color _origLeaf = treeOriginalColors[_i].leafColor;
                        if (0f == _origLeaf.r && 0f == _origLeaf.g && 0f == _origLeaf.b)
                            _origLeaf = Color.white;

                        treeVisuals[_i].leafImage.color = new Color(_origLeaf.r * _focusFactor, _origLeaf.g * _focusFactor, _origLeaf.b * _focusFactor, _origLeaf.a * uiAlpha);
                    }

                    if (null != treeVisuals[_i].trunkImage)
                    {
                        Color _origTrunk = treeOriginalColors[_i].trunkColor;
                        if (0f == _origTrunk.r && 0f == _origTrunk.g && 0f == _origTrunk.b)
                            _origTrunk = Color.white;

                        treeVisuals[_i].trunkImage.color = new Color(_origTrunk.r * _focusFactor, _origTrunk.g * _focusFactor, _origTrunk.b * _focusFactor, _origTrunk.a * uiAlpha);
                    }
                }

            if (null != decoImages && null != decoOriginalColors)
                for (int _i = 0; _i < decoImages.Length; _i++)
                    if (null != decoImages[_i])
                        decoImages[_i].color = new Color(decoOriginalColors[_i].r * _focusFactor, decoOriginalColors[_i].g * _focusFactor, decoOriginalColors[_i].b * _focusFactor, decoOriginalColors[_i].a * uiAlpha);

            if (null != mapNameText)
            {
                Color _textColor = mapNameText.color;
                _textColor.a = uiAlpha;
                mapNameText.color = _textColor;
            }
        }
    }
}
