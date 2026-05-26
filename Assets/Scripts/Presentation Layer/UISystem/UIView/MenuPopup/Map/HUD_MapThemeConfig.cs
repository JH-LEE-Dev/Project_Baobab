using UnityEngine;
using System;

namespace PresentationLayer.UISystem.UIView.MenuPopup.Map
{
    [Serializable]
    public struct MapTreeVisualData
    {
        public Sprite leafSprite;
        public Sprite trunkSprite;
    }

    [Serializable]
    public struct SubRegionTreeConfig
    {
        [Header("SubRegion Index (0: SubRegion 1, 1: SubRegion 2, 2: SubRegion 3)")]
        public int subRegionIndex;

        [Header("Available Trees for this SubRegion")]
        public MapTreeVisualData[] treeSets;     // 이 서브리전에서 사용할 식생 세트들
    }

    [Serializable]
    public struct MapThemeData
    {
        public MapType mapType;

        [Header("Ground (Plain)")]
        public Sprite[] plainGroundSprites;      // 일반 땅 스프라이트 N개 (랜덤 선택)

        [Header("Water Tiles")]
        public Sprite waterSprite;               // 단독 물 스프라이트 1개

        [Header("SubRegion Tree Configurations")]
        public SubRegionTreeConfig[] subRegionTreePools; // 서브리전 등급별 나무 풀 세팅

        [Header("Deco Configuration")]
        public Sprite[] decoSprites;             // 데코 소품 스프라이트 N개

        [Header("Tile Map Layout (Size: 4)")]
        public TileType[] tileLayout;            // 0: Ground, 1: Water 레이아웃
    }

    /// <summary>
    /// 모든 맵 타입별 지형/물/나무 테마 스프라이트와 레이아웃을 중앙 집중 관리하는 스크립터블 오브젝트 클래스입니다.
    /// </summary>
    [CreateAssetMenu(fileName = "HUD_MapThemeConfig", menuName = "Project Baobab/UI/Map Theme Config", order = 1)]
    public class HUD_MapThemeConfig : ScriptableObject
    {
        [SerializeField] private MapThemeData[] mapThemes;

        public MapThemeData[] MapThemes
        {
            get
            {
                return mapThemes;
            }
        }
    }
}
