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

        [Header("Sorting Settings")]
        [SerializeField] private SpriteRenderer pillarRenderer;
        private CustomSortable customSortable;

        [Header("Aura Orbit Settings")]
        [SerializeField] private bool useAuraOrbit = false;
        [SerializeField] private ItemAuraOrbitController auraOrbitController;
        [SerializeField] private List<LootAuraSetting> auraSettings;

        public event Action<bool, LootType> InteractStateChangedEvent;

        public LootType CurrentLootType { get; private set; } = LootType.None;

        private int characterLayer;

        private void Awake()
        {
            customSortable = GetComponent<CustomSortable>();
            characterLayer = LayerMask.NameToLayer("Character");

            // 기본값: 생성 위치(자기 자신의 Y) 기준. 스포너가 별도 Pivot 기준점을 알고 있다면
            // ApplySortingBasis()로 덮어써서 그 기준으로 다시 계산한다.
            ApplySortingBasis(transform.position.y);
        }

        private void OnTriggerEnter2D(Collider2D _other)
        {
            if (_other.gameObject.layer == characterLayer)
            {
                InteractStateChangedEvent?.Invoke(true, CurrentLootType);
            }
        }

        private void OnTriggerExit2D(Collider2D _other)
        {
            if (_other.gameObject.layer == characterLayer)
            {
                InteractStateChangedEvent?.Invoke(false, CurrentLootType);
            }
        }

        /// <summary>
        /// customSortable의 정렬 기준 Y좌표를 지정해 Pillar/Loot 렌더러의 Sorting Order를 다시 계산한다.
        /// LootPoint의 실제 지면 접점(Pivot)이 오브젝트 자신의 Transform과 다를 때 스포너가 호출한다.
        /// </summary>
        public void ApplySortingBasis(float _sortingBasisY)
        {
            if (null == customSortable) return;

            int pillarOrder = customSortable.ComputeSortingOrder(_sortingBasisY);

            if (null != pillarRenderer)
            {
                pillarRenderer.sortingOrder = pillarOrder;
            }

            if (null != targetRenderer)
            {
                targetRenderer.sortingOrder = pillarOrder + 2;
            }
        }

        public void SetLootDisplay(LootType _lootType)
        {
            CurrentLootType = _lootType;

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
