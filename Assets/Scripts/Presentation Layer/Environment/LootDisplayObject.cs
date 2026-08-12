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

        // 캐릭터가 콜라이더 범위 안에 들어와 있는지(=상호작용 키 입력을 받을 수 있는지)를 UIView_Unit
        // 아이콘에 알리는 이벤트. Tent.TentInteractStateChangedEvent와 동일한 역할.
        public event Action<bool, LootType> InteractStateChangedEvent;

        // 범위 안에서 상호작용 키를 눌렀을 때만 발생하는 토글 이벤트(true=열기, false=닫기).
        // Tent.TentInteractEvent와 동일한 역할 - UIView_ScreenModal을 실제로 여닫는 쪽은 이 이벤트다.
        public event Action<bool, LootType> LootPillarInteractEvent;

        public LootType CurrentLootType { get; private set; } = LootType.None;

        private int characterLayer;
        private InputManager inputManager;
        private bool bPhysicalOverlapped;
        private bool bInteracting;

        private void Awake()
        {
            customSortable = GetComponent<CustomSortable>();
            characterLayer = LayerMask.NameToLayer("Character");

            // 기본값: 생성 위치(자기 자신의 Y) 기준. 스포너가 별도 Pivot 기준점을 알고 있다면
            // ApplySortingBasis()로 덮어써서 그 기준으로 다시 계산한다.
            ApplySortingBasis(transform.position.y);
        }

        public void Initialize(InputManager _inputManager)
        {
            inputManager = _inputManager;

            inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
            inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;
        }

        private void OnDestroy()
        {
            if (inputManager != null)
            {
                inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
            }
        }

        private void InteractionKeyPressed()
        {
            if (!bPhysicalOverlapped) return;

            bInteracting = !bInteracting;
            LootPillarInteractEvent?.Invoke(bInteracting, CurrentLootType);
        }

        private void OnTriggerEnter2D(Collider2D _other)
        {
            if (_other.gameObject.layer == characterLayer)
            {
                bPhysicalOverlapped = true;
                InteractStateChangedEvent?.Invoke(true, CurrentLootType);
            }
        }

        private void OnTriggerExit2D(Collider2D _other)
        {
            if (_other.gameObject.layer == characterLayer)
            {
                bPhysicalOverlapped = false;
                InteractStateChangedEvent?.Invoke(false, CurrentLootType);

                // 상호작용(모달이 열린) 상태로 범위를 벗어나면, 키를 다시 눌러 취소한 것과 동일하게
                // 취급해 LootPillarInteractEvent(false)를 발행하고 상태를 초기화한다.
                if (bInteracting)
                {
                    bInteracting = false;
                    LootPillarInteractEvent?.Invoke(false, CurrentLootType);
                }
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
