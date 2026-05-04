using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PresentationLayer.DOTweenAnimationSystem;

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
        [SerializeField] private Image animalImage;         // 동물 이미지 1개

        [Header("Text")]
        [SerializeField] private TextMeshProUGUI mapNameText; // 맵 이름 TMP

        // //내부 의존성
        [Header("Animation")]
        [SerializeField] private ObjectMotionPlayer motionPlayer;

        private MapType currentMapType;
        private string currentMapName = string.Empty;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 지역 항목을 초기화합니다.
        /// </summary>
        public void Initialize()
        {
            if (true == isInitialized)
                return;

            if (null == motionPlayer)
                motionPlayer = GetComponent<ObjectMotionPlayer>();

            isInitialized = true;
        }

        /// <summary>
        /// 지역의 이름과 타입을 설정합니다.
        /// </summary>
        public void Setup(string _mapName, MapType _mapType)
        {
            if (false == isInitialized)
                Initialize();

            currentMapName = _mapName;
            currentMapType = _mapType;

            if (null != mapNameText)
                mapNameText.text = currentMapName;
        }

        /// <summary>
        /// 현재 설정된 지역 이름을 반환합니다.
        /// </summary>
        public string GetMapName()
        {
            return currentMapName;
        }

        /// <summary>
        /// 현재 설정된 지역의 MapType을 반환합니다.
        /// </summary>
        public MapType GetMapType()
        {
            return currentMapType;
        }

        /// <summary>
        /// 특정 모션 태그를 통해 애니메이션을 재생합니다.
        /// </summary>
        public void PlayMotion(string _motionTag)
        {
            if (null == motionPlayer)
                return;

            motionPlayer.Play(_motionTag);
        }
    }
}
