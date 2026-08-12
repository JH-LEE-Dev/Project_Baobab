using System;
using System.Collections.Generic;
using UnityEngine;

namespace PresentationLayer.Environment
{
    [Serializable]
    public struct LootAuraSetting
    {
        public LootType lootType;
        [ColorUsage(true, true)] public Color auraCenterColor;
    }

    public class LootDisplayObject : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private LootItemTypeDataBase lootDataBase;

        [Header("Aura Orbit Settings")]
        [SerializeField] private bool useAuraOrbit = false;
        [SerializeField] private ItemAuraOrbitController auraOrbitController;
        [SerializeField] private List<LootAuraSetting> auraSettings;

        public void SetLootDisplay(LootType _lootType)
        {
            if (null != lootDataBase && null != targetRenderer)
            {
                LootItemTypeData itemData = lootDataBase.Get(_lootType);
                if (null != itemData)
                {
                    targetRenderer.sprite = itemData.sprite;
                }
            }

            if (true == useAuraOrbit && null != auraOrbitController)
            {
                if (null != auraSettings)
                {
                    LootAuraSetting foundSetting = auraSettings.Find(x => x.lootType == _lootType);
                    // Color 구조체는 레퍼런스 타입이 아니므로 null 비교가 불가능하지만
                    // 기본값이 투명이 되는 것을 막기 위해 명시적으로 검색 성공 여부를 확인하기보다 Find로 매치되는게 없으면 default 리턴(전부 0)
                    // 조금 더 안전하게 인덱스로 확인
                    int settingIndex = auraSettings.FindIndex(x => x.lootType == _lootType);
                    if (settingIndex >= 0)
                    {
                        auraOrbitController.SetCenterGlowColor(auraSettings[settingIndex].auraCenterColor);
                    }
                }
                auraOrbitController.Play();
            }
            else if (false == useAuraOrbit && null != auraOrbitController)
            {
                auraOrbitController.Stop();
            }
        }
    }
}
