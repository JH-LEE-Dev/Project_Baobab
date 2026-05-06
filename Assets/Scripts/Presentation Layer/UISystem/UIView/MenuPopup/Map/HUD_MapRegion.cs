using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PresentationLayer.DOTweenAnimationSystem;
using UnityEngine.EventSystems;
using System;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
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
        [SerializeField] private Image[] treeImages;        // 나무 이미지 2개
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
        private Color[] treeOriginalColors;
        private Color[] animalOriginalColors;

        private MapEnvironmentDataInfo mapEnvironmentInfo;
        private string currentMapName = string.Empty;
        private bool isLocked = false;
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

            if (null != treeImages)
            {
                treeOriginalColors = new Color[treeImages.Length];
                for (int _i = 0; _i < treeImages.Length; _i++)
                    if (null != treeImages[_i])
                        treeOriginalColors[_i] = treeImages[_i].color;
            }

            if (null != animalImages)
            {
                animalOriginalColors = new Color[animalImages.Length];
                for (int _i = 0; _i < animalImages.Length; _i++)
                    if (null != animalImages[_i])
                        animalOriginalColors[_i] = animalImages[_i].color;
            }
        }

        public void Setup(string _mapName, MapEnvironmentDataInfo _info)
        {
            if (false == isInitialized)
                Initialize();

            currentMapName = _mapName;
            mapEnvironmentInfo = _info;

            if (null != mapNameText)
                mapNameText.text = currentMapName;
        }

        public void SetLock(bool _isLock)
        {
            isLocked = _isLock;

            if (null != lockObject)
                lockObject.SetActive(isLocked);

            if (null != unlockObject)
                unlockObject.SetActive(false == isLocked);
        }

        public void SetFocus(bool _isFocus)
        {
            float _factor = (true == _isFocus) ? 1.0f : dimFactor;

            if (null != groundImages && null != groundOriginalColors)
            {
                for (int _i = 0; _i < groundImages.Length; _i++)
                {
                    if (null != groundImages[_i])
                    {
                        Color _c = groundOriginalColors[_i];
                        groundImages[_i].color = new Color(_c.r * _factor, _c.g * _factor, _c.b * _factor, _c.a);
                    }
                }
            }

            if (null != treeImages && null != treeOriginalColors)
            {
                for (int _i = 0; _i < treeImages.Length; _i++)
                {
                    if (null != treeImages[_i])
                    {
                        Color _c = treeOriginalColors[_i];
                        treeImages[_i].color = new Color(_c.r * _factor, _c.g * _factor, _c.b * _factor, _c.a);
                    }
                }
            }

            if (null != animalImages && null != animalOriginalColors)
            {
                for (int _i = 0; _i < animalImages.Length; _i++)
                {
                    if (null != animalImages[_i])
                    {
                        Color _c = animalOriginalColors[_i];
                        animalImages[_i].color = new Color(_c.r * _factor, _c.g * _factor, _c.b * _factor, _c.a);
                    }
                }
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
