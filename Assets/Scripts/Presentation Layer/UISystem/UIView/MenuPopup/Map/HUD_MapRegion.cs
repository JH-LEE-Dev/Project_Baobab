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

    [Serializable]
    public struct MapThemeData
    {
        public MapType mapType;
        public Sprite[] groundSprites;
        public Sprite leafSprite;
        public Sprite trunkSprite;
        public Sprite[] animalSprites;
    }

    /// <summary>
    /// 특정 지역(Region)의 시각적 요소(지형, 나무, 동물, 이름)를 관리하고 애니메이션을 재생하는 클래스입니다.
    /// 해당 지역의 MapType 정보를 보유하여 상위 매니저와 소통합니다.
    /// </summary>
    public class HUD_MapRegion : MonoBehaviour
    {
        // //외부 의존성
        [Header("Ground Visuals")]
        [SerializeField] private Image[] groundImages;      // 지형 이미지 4개

        [Header("Object Visuals")]
        [SerializeField] private MapTreeVisual[] treeVisuals; // 나무 비주얼 (잎, 기둥)
        [SerializeField] private Image[] animalImages;      // 동물 이미지 3개

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

        [Header("Theme Configuration")]
        [SerializeField] private MapThemeData[] mapThemes;

        // //내부 의존성
        private Color[] groundOriginalColors;
        private MapTreeColor[] treeOriginalColors;
        private Color[] animalOriginalColors;

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
        private static readonly string[] animalTags = { "Animal_1", "Animal_2", "Animal_3" };

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

            ApplyTheme(_info.mapType);

            // 새로운 지역 셋업 시 오브젝트 노출 상태 리셋 (초기 노출 개수 1)
            currentVisibleCount = 1;
            ResetObjectsVisibility();

            if (true == _shouldPlayAnimation)
            {
                PlayStartGroundAnimation();
            }
            else if (true == _isInstant)
            {
                PlayStartAnimationInstant();
            }
        }

        /// <summary>
        /// 서브 지역 번호에 따라 나무와 동물의 노출 개수를 애니메이션과 함께 조절합니다.
        /// </summary>
        public void UpdateObjectCount(int _count)
        {
            if (false == isInitialized)
                Initialize();

            if (_count <= 0 || _count > 3)
                return;

            if (currentVisibleCount == _count)
                return;

            // 나무 연출 조절
            if (null != treeVisuals)
            {
                for (int _i = 0; _i < treeVisuals.Length; _i++)
                {
                    bool _shouldBeVisible = (_i < _count);
                    bool _wasVisible = (_i < currentVisibleCount);

                    if (_shouldBeVisible && !_wasVisible)
                    {
                        if (null != treeVisuals[_i].leafImage) treeVisuals[_i].leafImage.gameObject.SetActive(true);
                        if (null != treeVisuals[_i].trunkImage) treeVisuals[_i].trunkImage.gameObject.SetActive(true);
                        
                        if (_i < treeTags.Length)
                            motionPlayer.Play(treeTags[_i], bReset: true);
                    }
                    else if (!_shouldBeVisible && _wasVisible)
                    {
                        if (_i < treeTags.Length)
                            motionPlayer.PlayBackward(treeTags[_i], bReset: true);
                    }
                }
            }

            // 동물 연출 조절
            if (null != animalImages)
            {
                for (int _i = 0; _i < animalImages.Length; _i++)
                {
                    bool _shouldBeVisible = (_i < _count);
                    bool _wasVisible = (_i < currentVisibleCount);

                    if (_shouldBeVisible && !_wasVisible)
                    {
                        if (null != animalImages[_i]) animalImages[_i].gameObject.SetActive(true);
                        
                        if (_i < animalTags.Length)
                            motionPlayer.Play(animalTags[_i], bReset: true);
                    }
                    else if (!_shouldBeVisible && _wasVisible)
                    {
                        if (_i < animalTags.Length)
                            motionPlayer.PlayBackward(animalTags[_i], bReset: true);
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

            int _targetCount = Mathf.Min(currentVisibleCount, treeVisuals.Length);
            _targetCount = Mathf.Min(_targetCount, treeTags.Length);

            int _finalIdx = _targetCount - 1;
            const float _delay = 0.05f;

            if (_targetCount <= 0)
            {
                PlayStartAnimalAnimation();
                return;
            }

            for (int _i = 0; _i < _targetCount; _i++)
            {
                if (null == treeVisuals[_i].leafImage && null == treeVisuals[_i].trunkImage)
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.Play(treeTags[_i], bReset: true, _onComplete: PlayStartAnimalAnimation, _forceDelayForward: _delay * _i);
                else
                    motionPlayer.Play(treeTags[_i], bReset: true, _forceDelayForward: _delay * _i);
            }
        }

        public void PlayStartAnimalAnimation()
        {
            if (null == animalImages || null == animalTags)
                return;

            int _targetCount = Mathf.Min(currentVisibleCount, animalImages.Length);
            _targetCount = Mathf.Min(_targetCount, animalTags.Length);
            
            const float _delay = 0.05f;

            for (int _i = 0; _i < _targetCount; _i++)
            {
                if (null == animalImages[_i])
                    continue;

                motionPlayer.Play(animalTags[_i], bReset: true, _forceDelayForward: _delay * _i);
            }
        }

        public void PlayEndAnimation(UnityEngine.Events.UnityAction _onComplete, bool _isSkip = false)
        {
            PlayEndAnimalAnimation(_onComplete, _isSkip);
            PlayEndTreeAnimation(_onComplete, _isSkip);
            PlayEndGroundAnimation(_onComplete, _isSkip);
        }

        public MapEnvironmentDataInfo GetMapEnvironmentInfo() => mapEnvironmentInfo;
        public bool IsLocked() => isLocked;
        public string GetMapName() => currentMapName;
        public MapType GetMapType() => mapEnvironmentInfo.mapType;

        // //내부 로직

        /// <summary>
        /// 맵 타입에 맞게 미리 세팅된 테마 스프라이트 에셋들을 일괄 교체합니다.
        /// </summary>
        private void ApplyTheme(MapType _mapType)
        {
            if (null == mapThemes)
                return;

            MapThemeData _targetTheme = default;
            bool _found = false;

            for (int _i = 0; _i < mapThemes.Length; _i++)
            {
                if (mapThemes[_i].mapType == _mapType)
                {
                    _targetTheme = mapThemes[_i];
                    _found = true;
                    break;
                }
            }

            if (false == _found)
                return;

            // 지형 스프라이트 교체 적용
            if (null != groundImages && null != _targetTheme.groundSprites)
            {
                int _len = Mathf.Min(groundImages.Length, _targetTheme.groundSprites.Length);
                for (int _i = 0; _i < _len; _i++)
                    if (null != groundImages[_i] && null != _targetTheme.groundSprites[_i])
                        groundImages[_i].sprite = _targetTheme.groundSprites[_i];
            }

            // 나무 스프라이트 교체 적용
            if (null != treeVisuals)
            {
                for (int _i = 0; _i < treeVisuals.Length; _i++)
                {
                    if (null != treeVisuals[_i].leafImage && null != _targetTheme.leafSprite)
                        treeVisuals[_i].leafImage.sprite = _targetTheme.leafSprite;

                    if (null != treeVisuals[_i].trunkImage && null != _targetTheme.trunkSprite)
                        treeVisuals[_i].trunkImage.sprite = _targetTheme.trunkSprite;
                }
            }

            // 동물 스프라이트 교체 적용
            if (null != animalImages && null != _targetTheme.animalSprites)
            {
                int _len = Mathf.Min(animalImages.Length, _targetTheme.animalSprites.Length);
                for (int _i = 0; _i < _len; _i++)
                    if (null != animalImages[_i] && null != _targetTheme.animalSprites[_i])
                        animalImages[_i].sprite = _targetTheme.animalSprites[_i];
            }
        }

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

            if (null != animalImages)
            {
                animalOriginalColors = new Color[animalImages.Length];
                for (int _i = 0; _i < animalImages.Length; _i++)
                    if (null != animalImages[_i])
                        animalOriginalColors[_i] = animalImages[_i].color;
            }
        }

        private void ResetObjectsVisibility()
        {
            if (null != treeVisuals)
            {
                for (int _i = 0; _i < treeVisuals.Length; _i++)
                {
                    bool _isVisible = (_i < currentVisibleCount);
                    if (null != treeVisuals[_i].leafImage) treeVisuals[_i].leafImage.gameObject.SetActive(_isVisible);
                    if (null != treeVisuals[_i].trunkImage) treeVisuals[_i].trunkImage.gameObject.SetActive(_isVisible);
                }
            }

            if (null != animalImages)
            {
                for (int _i = 0; _i < animalImages.Length; _i++)
                    if (null != animalImages[_i])
                        animalImages[_i].gameObject.SetActive(_i < currentVisibleCount);
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

            if (null != animalImages && null != animalTags)
            {
                int _count = Mathf.Min(animalImages.Length, animalTags.Length);
                for (int _i = 0; _i < _count; _i++)
                    if (null != animalImages[_i])
                        motionPlayer.Play(animalTags[_i], bReset: true, _skip: true);
            }
        }

        private void PlayEndAnimalAnimation(UnityEngine.Events.UnityAction _onComplete, bool _isSkip = false)
        {
            if (null == animalImages || 0 == animalImages.Length)
            {
                PlayEndTreeAnimation(_onComplete, _isSkip);
                return;
            }

            const float _delay = 0.05f;
            int _loopCount = Mathf.Min(animalImages.Length, animalTags.Length);

            for (int _i = _loopCount - 1; _i >= 0; _i--)
            {
                if (null == animalImages[_i])
                    continue;

                motionPlayer.PlayBackward(animalTags[_i], bReset: true, _skip: _isSkip, _forceDelayBackward: _delay * (_loopCount - 1 - _i));
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
                        treeVisuals[_i].leafImage.color = new Color(treeOriginalColors[_i].leafColor.r * _focusFactor, treeOriginalColors[_i].leafColor.g * _focusFactor, treeOriginalColors[_i].leafColor.b * _focusFactor, treeOriginalColors[_i].leafColor.a * uiAlpha);

                    if (null != treeVisuals[_i].trunkImage)
                        treeVisuals[_i].trunkImage.color = new Color(treeOriginalColors[_i].trunkColor.r * _focusFactor, treeOriginalColors[_i].trunkColor.g * _focusFactor, treeOriginalColors[_i].trunkColor.b * _focusFactor, treeOriginalColors[_i].trunkColor.a * uiAlpha);
                }

            if (null != animalImages && null != animalOriginalColors)
                for (int _i = 0; _i < animalImages.Length; _i++)
                    if (null != animalImages[_i])
                        animalImages[_i].color = new Color(animalOriginalColors[_i].r * _focusFactor, animalOriginalColors[_i].g * _focusFactor, animalOriginalColors[_i].b * _focusFactor, animalOriginalColors[_i].a * uiAlpha);

            if (null != mapNameText)
            {
                Color _textColor = mapNameText.color;
                _textColor.a = uiAlpha;
                mapNameText.color = _textColor;
            }
        }
    }
}
