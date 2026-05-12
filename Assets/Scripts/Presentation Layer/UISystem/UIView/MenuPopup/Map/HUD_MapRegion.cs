using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine.EventSystems;
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

        [Header("Focus Settings")]
        [Range(0f, 1f)]
        [SerializeField] private float dimFactor = 0.5f;     // 비포커스 시 명암 계수

        // //내부 의존성
        private Color[] groundOriginalColors;
        private MapTreeColor[] treeOriginalColors;
        private Color[] animalOriginalColors;

        private MapEnvironmentDataInfo mapEnvironmentInfo;
        private string currentMapName = string.Empty;
        private float uiAlpha = 1.0f;
        private bool isLocked = false;
        private bool isFocused = false;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        public void Initialize()
        {
            if (true == isInitialized)
                return;

            if (null == motionPlayer)
                motionPlayer = GetComponent<ObjectMotionPlayer>();

            CaptureOriginalColors();

            isInitialized = true;
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

        public void Setup(string _mapName, MapEnvironmentDataInfo _info, bool _shouldPlayAnimation = false, bool _isInstant = false)
        {
            if (false == isInitialized)
                Initialize();

            currentMapName = _mapName;
            mapEnvironmentInfo = _info;

            if (null != mapNameText)
                mapNameText.text = currentMapName;

            if (true == _shouldPlayAnimation)
            {
                PlayStartGroundAnimation();
            }
            else if (true == _isInstant)
            {
                PlayStartAnimationInstant();
            }
        }

        private void PlayStartAnimationInstant()
        {
            if (null == motionPlayer)
                return;

            // 모든 지형 애니메이션 즉시 적용
            if (null != groundImages)
            {
                for (int _i = 0; _i < groundImages.Length; _i++)
                    if (null != groundImages[_i])
                        motionPlayer.Play("Ground_" + (_i + 1), bReset: true, _skip: true);
            }

            // 모든 나무 애니메이션 즉시 적용
            if (null != treeVisuals)
            {
                for (int _i = 0; _i < treeVisuals.Length; _i++)
                    if (null != treeVisuals[_i].leafImage || null != treeVisuals[_i].trunkImage)
                        motionPlayer.Play("Tree_" + (_i + 1), bReset: true, _skip: true);
            }

            // 모든 동물 애니메이션 즉시 적용
            if (null != animalImages)
            {
                for (int _i = 0; _i < animalImages.Length; _i++)
                    if (null != animalImages[_i])
                        motionPlayer.Play("Animal_" + (_i + 1), bReset: true, _skip: true);
            }
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

        public void PlayStartGroundAnimation()
        {
            if (null == groundImages)
                return;

            int _finalIdx = groundImages.Length - 1;
            const string _groundAnimationTag = "Ground_"; 
            const float delay = 0.05f;

            for (int _i = 0; _i < groundImages.Length; _i++)
            {
                if (null == groundImages[_i])
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.Play(_groundAnimationTag + (_i + 1).ToString(), bReset: true, _onComplete: PlayStartTreeAnimation, _forceDelayForward: delay * _i);
                else
                    motionPlayer.Play(_groundAnimationTag + (_i + 1).ToString(), bReset: true, _forceDelayForward: delay * _i);
            }
        }

        public void PlayStartTreeAnimation()
        {
            if (null == treeVisuals)
                return;

            int _finalIdx = treeVisuals.Length - 1;
            const string _treeAnimationTag = "Tree_"; 
            const float delay = 0.05f;

            for (int _i = 0; _i < treeVisuals.Length; _i++)
            {
                // 구조체는 null이 될 수 없으므로 실제 이미지 컴포넌트 유무를 확인합니다.
                if (null == treeVisuals[_i].leafImage && null == treeVisuals[_i].trunkImage)
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.Play(_treeAnimationTag + (_i + 1).ToString(), 
                        bReset: true, _onComplete: PlayStartAnimalAnimation, _forceDelayForward: delay * _i);
                else
                    motionPlayer.Play(_treeAnimationTag + (_i + 1).ToString(), bReset: true, _forceDelayForward: delay * _i);
            }
        }

        public void PlayStartAnimalAnimation()
        {
            if (null == animalImages)
                return;

            const string _animalAnimationTag = "Animal_";
            const float _delay = 0.05f;

            for (int _i = 0; _i < animalImages.Length; _i++)
            {
                if (null == animalImages[_i])
                    continue;

                motionPlayer.Play(_animalAnimationTag + (_i + 1).ToString(), bReset: true, _forceDelayForward: _delay * _i);
            }
        }

        public void PlayEndAnimation(UnityEngine.Events.UnityAction _onComplete)
        {
            PlayEndAnimalAnimation(_onComplete);
            PlayEndTreeAnimation(_onComplete);
            PlayEndGroundAnimation(_onComplete);
        }

        private void PlayEndAnimalAnimation(UnityEngine.Events.UnityAction _onComplete)
        {
            if (null == animalImages || 0 == animalImages.Length)
            {
                PlayEndTreeAnimation(_onComplete);
                return;
            }

            int _finalIdx = 0;
            const string _animalAnimationTag = "Animal_";
            const float _delay = 0.05f;

            for (int _i = animalImages.Length - 1; _i >= 0; _i--)
            {
                if (null == animalImages[_i])
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.PlayBackward(_animalAnimationTag + (_i + 1).ToString(), bReset: true, _forceDelayBackward: _delay * (animalImages.Length - 1 - _i));
                else
                    motionPlayer.PlayBackward(_animalAnimationTag + (_i + 1).ToString(), bReset: true, _forceDelayBackward: _delay * (animalImages.Length - 1 - _i));
            }
        }

        private void PlayEndTreeAnimation(UnityEngine.Events.UnityAction _onComplete)
        {
            if (null == treeVisuals || 0 == treeVisuals.Length)
            {
                PlayEndGroundAnimation(_onComplete);
                return;
            }

            int _finalIdx = 0;
            const string _treeAnimationTag = "Tree_";
            const float _delay = 0.05f;

            for (int _i = treeVisuals.Length - 1; _i >= 0; _i--)
            {
                if (null == treeVisuals[_i].leafImage && null == treeVisuals[_i].trunkImage)
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.PlayBackward(_treeAnimationTag + (_i + 1).ToString(), bReset: true, _forceDelayBackward: _delay * (treeVisuals.Length - 1 - _i));
                else
                    motionPlayer.PlayBackward(_treeAnimationTag + (_i + 1).ToString(), bReset: true, _forceDelayBackward: _delay * (treeVisuals.Length - 1 - _i));
            }
        }

        private void PlayEndGroundAnimation(UnityEngine.Events.UnityAction _onComplete)
        {
            if (null == groundImages || 0 == groundImages.Length)
            {
                _onComplete?.Invoke();
                return;
            }

            int _finalIdx = 0;
            const string _groundAnimationTag = "Ground_";
            const float _delay = 0.05f;

            for (int _i = groundImages.Length - 1; _i >= 0; _i--)
            {
                if (null == groundImages[_i])
                    continue;

                if (_i == _finalIdx)
                    motionPlayer.PlayBackward(_groundAnimationTag + (_i + 1).ToString(), bReset: true, _onComplete: _onComplete, _forceDelayBackward: _delay * (groundImages.Length - 1 - _i));
                else
                    motionPlayer.PlayBackward(_groundAnimationTag + (_i + 1).ToString(), bReset: true, _forceDelayBackward: _delay * (groundImages.Length - 1 - _i));
            }
        }

        public MapEnvironmentDataInfo GetMapEnvironmentInfo()
        {
            return mapEnvironmentInfo;
        }

        public bool IsLocked()
        {
            return isLocked;
        }

        public string GetMapName()
        {
            return currentMapName;
        }

        public MapType GetMapType()
        {
            return mapEnvironmentInfo.mapType;
        }
    }
}
