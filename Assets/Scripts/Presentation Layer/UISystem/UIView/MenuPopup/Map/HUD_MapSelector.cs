using System.Collections.Generic;
using UnityEngine;
using PresentationLayer.DOTweenAnimationSystem;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    /// <summary>
    /// 메인 맵 지역(MapRegion)들과 서브 셀렉터(MapSubSelector)를 총괄하며 최종 MapType을 결정하는 클래스입니다.
    /// </summary>
    public class HUD_MapSelector : MonoBehaviour
    {
        // //외부 의존성
        [Header("References")]
        [SerializeField] private HUD_MapSubSelector subSelector; // 서브 지역 셀렉터
        [SerializeField] private Transform regionContainer;     // 지역 항목 부모 컨테이너
        [SerializeField] private GameObject regionPrefab;       // 지역 항목 프리팹

        // //내부 의존성
        private List<HUD_MapRegion> spawnedRegions = new List<HUD_MapRegion>(8);
        private HUD_MapRegion currentSelectedRegion;
        private bool isInitialized = false;

        // //퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 셀렉터를 초기화합니다.
        /// </summary>
        public void Initialize()
        {
            if (true == isInitialized)
                return;

            if (null != subSelector)
                subSelector.Initialize();

            isInitialized = true;
        }

        /// <summary>
        /// 새로운 지역 항목을 생성하고 등록합니다.
        /// </summary>
        /// <param name="_name">지역 이름</param>
        /// <param name="_baseType">해당 지역의 첫 번째 MapType</param>
        /// <param name="_subCount">해당 지역이 보유한 서브 지역 개수</param>
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
                
                // TODO: _subCount 정보를 저장하거나 연동하는 로직 필요
                spawnedRegions.Add(_region);
            }
        }

        /// <summary>
        /// 특정 지역을 선택하고 서브 셀렉터의 개수를 갱신합니다.
        /// </summary>
        public void SelectRegion(HUD_MapRegion _region, int _subCount)
        {
            if (null == _region)
                return;

            currentSelectedRegion = _region;

            if (null != subSelector)
                subSelector.SetSubRegionCount(_subCount);
        }

        /// <summary>
        /// 현재 선택된 메인 지역의 타입과 서브 지역 번호를 조합하여 최종 MapType을 반환합니다.
        /// </summary>
        public MapType GetFinalMapType()
        {
            if (null == currentSelectedRegion || null == subSelector)
                return MapType.None;

            MapType _baseType = currentSelectedRegion.GetMapType();
            int _subNumber = subSelector.GetSelectedRegionNumber();

            // 선택된 번호가 없으면(초기 상태 등) 기본 타입 반환
            if (-1 == _subNumber)
                return _baseType;

            // Enum 구조상 Forest1_1, Forest1_2, Forest1_3 순서대로 되어 있으므로 번호를 더해 조합
            // _subNumber가 1부터 시작하므로 (_subNumber - 1)을 더함
            return (MapType)((int)_baseType + (_subNumber - 1));
        }
    }
}
