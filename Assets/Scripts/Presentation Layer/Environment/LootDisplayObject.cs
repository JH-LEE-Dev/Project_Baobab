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

        [Header("Outline Settings")]
        // 상호작용 범위에 들어오면 켜지는 Pillar 아웃라인. OffroadContainer/RepairBox와 같은 스텐실 방식이다 -
        // outlineStencilObj(OutlineStencilWriter)가 Pillar 스프라이트 모양대로 스텐실을 찍고, 그 자식인
        // Outline이 스텐실 바깥쪽만 그려 테두리가 된다. 둘이 부모-자식이라 켜고 끄는 건 루트 하나만 토글하면 된다.
        // (Loot·Shadow는 제외하고 Pillar만 감싼다)
        [SerializeField] private GameObject outlineStencilObj;

        // 아웃라인 렌더러들. Pillar의 SortingOrder는 런타임에 정해지므로(ApplySortingBasis) 같이 따라가야
        // 한다. 스텐실 라이터는 Pillar와 같은 Order, 아웃라인은 그 바로 위(+1)에 그린다.
        // (Loot은 +2라 아웃라인이 아이템을 가리지 않는다)
        [SerializeField] private SpriteRenderer outlineStencilRenderer;
        [SerializeField] private SpriteRenderer outlineRenderer;

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

            // 프리팹에서도 꺼진 상태로 저장해 두지만, 범위 밖에서 시작한다는 것을 코드로도 못 박는다.
            SetOutlineActive(false);
        }

        // 상호작용 범위 진입/이탈에 맞춰 아웃라인을 켜고 끈다. (outlineStencilObj 주석 참고)
        private void SetOutlineActive(bool _bActive)
        {
            if (null == outlineStencilObj) return;

            outlineStencilObj.SetActive(_bActive);
        }

        public void Initialize(InputManager _inputManager)
        {
            inputManager = _inputManager;

            inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
            inputManager.inputReader.InteractionKeyPressedEvent += InteractionKeyPressed;

            inputManager.inputReader.InteractionKeyPressedWhileUIModeEvent -= InteractionKeyPressedWhileUIMode;
            inputManager.inputReader.InteractionKeyPressedWhileUIModeEvent += InteractionKeyPressedWhileUIMode;
        }

        private void OnDestroy()
        {
            if (inputManager != null)
            {
                inputManager.inputReader.InteractionKeyPressedEvent -= InteractionKeyPressed;
                inputManager.inputReader.InteractionKeyPressedWhileUIModeEvent -= InteractionKeyPressedWhileUIMode;
            }
        }

        private void InteractionKeyPressed()
        {
            if (!bPhysicalOverlapped) return;

            bInteracting = !bInteracting;
            LootPillarInteractEvent?.Invoke(bInteracting, CurrentLootType);
        }

        // UIView_ScreenModal이 스스로 SetInputMode(UI)를 걸어둔 동안에는 InteractionKeyPressedEvent가
        // 막혀 있어 위 InteractionKeyPressed()가 호출되지 않는다. 그동안에도 키보드로는 닫을 수 있도록
        // 하는 전용 통로 - 이미 열려 있을 때(bInteracting == true)만 의미가 있으므로 닫기만 처리한다.
        // (패드는 InputReader가 걸러서 여기로 오지 않는다 - 패드는 Cancel(B/○)로 닫는다)
        // Tent.InteractionKeyPressedWhileUIMode()와 동일한 역할.
        private void InteractionKeyPressedWhileUIMode()
        {
            if (false == bInteracting) return;

            bInteracting = false;
            LootPillarInteractEvent?.Invoke(false, CurrentLootType);
        }

        /// <summary>
        /// UIView_ScreenModal이 상호작용 키 토글 경로를 거치지 않고 닫혔을 때(ESC·패드 Cancel 등
        /// UIDepthController가 Hide()를 직접 호출하는 경로) bInteracting이 true로 남아, 다음 상호작용
        /// 입력이 "닫기"로 오인되어 무시되는 문제를 막는다. TownSystem이 LootPillarUIClosedSignal을
        /// 받아 호출한다. (Tent.SyncInteractStateOnExternalClose()와 동일한 역할)
        ///
        /// bPhysicalOverlapped(상호작용 가능 범위)는 건드리지 않는다 - 플레이어가 여전히 필러 앞에
        /// 서 있을 수 있다.
        /// </summary>
        public void SyncInteractStateOnExternalClose()
        {
            bInteracting = false;
        }

        private void OnTriggerEnter2D(Collider2D _other)
        {
            if (_other.gameObject.layer == characterLayer)
            {
                bPhysicalOverlapped = true;
                SetOutlineActive(true);
                InteractStateChangedEvent?.Invoke(true, CurrentLootType);
            }
        }

        private void OnTriggerExit2D(Collider2D _other)
        {
            if (_other.gameObject.layer == characterLayer)
            {
                bPhysicalOverlapped = false;
                SetOutlineActive(false);
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

            // 아웃라인도 Pillar와 같은 기준으로 따라간다. (outlineStencilRenderer 주석 참고)
            if (null != outlineStencilRenderer)
            {
                outlineStencilRenderer.sortingOrder = pillarOrder;
            }

            if (null != outlineRenderer)
            {
                outlineRenderer.sortingOrder = pillarOrder + 1;
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
