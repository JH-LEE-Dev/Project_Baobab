using System;
using System.Collections.Generic;
using UnityEngine;
using PresentationLayer.VFX;

namespace PresentationLayer.Environment
{
    [Serializable]
    public struct LootAuraSetting
    {
        public LootType lootType;
        [ColorUsage(true, true)] public Color auraCenterColor;
    }

    // 성장 뽀잉(LateUpdate)이 진화 연출(VFX_LootEvolve.LateUpdate, 기본값 0)이 전리품 위치를 쓴 뒤에 그 위에 보정을 더하도록 늦게 실행한다.
    [DefaultExecutionOrder(100)]
    public class LootDisplayObject : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private SpriteRenderer targetRenderer;
        [SerializeField] private LootItemTypeDataBase lootDataBase;
        [SerializeField, Tooltip("기둥 윗판 중앙 - 전리품 부모(LootAnchor) 기준, 유닛. 전시 그림의 밑둥(LootGrowthVisualData.displayBase)을 여기에 맞춘다.")]
        private Vector2 displayBaseAnchor = new Vector2(0f, -0.27f);

        [Header("Sorting Settings")]
        [SerializeField] private SpriteRenderer pillarRenderer;
        private CustomSortable customSortable;

        [Header("Loot Shadow (기둥 윗판에 드리우는 전리품 그림자)")]
        [Tooltip("기둥 윗판 중앙에 놓인 그림자 렌더러(Pillar 자식). 전리품마다 크기(LootGrowthVisualData.displayShadowWidth)에 맞춘 타원을 그린다.")]
        [SerializeField] private SpriteRenderer lootShadowRenderer;
        [SerializeField, Range(0f, 1f), Tooltip("그림자 진하기(검정 알파). 리니어 색공간에서 섞이므로 0.4가 기존 기둥 그림에 그려져 있던 그림자(윗판 192 → 153)와 같은 진하기다.")]
        private float lootShadowAlpha = 0.4f;

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

        [Header("Growth Visual (단계별 아우라 색 / 스프라이트)")]
        [Tooltip("단계(씨앗/꽃/나무)별 아우라 색과 스프라이트. 비워 두면 단계와 상관없이 아래 auraSettings(종류별 색)를 쓴다.")]
        [SerializeField] private LootGrowthVisualData growthVisualData;
        [SerializeField, Range(0.1f, 1.5f), Tooltip("단계 색 후광 외곽의 세기 배율. 후광 머티리얼의 Intensity가 커서 1을 넘기면 색이 포화된다.")]
        private float growthGlowStrength = 0.9f;
        [SerializeField, Range(0.1f, 1.5f), Tooltip("단계 색 후광 중심의 세기 배율. 중심이 하얗게 날아가면 낮춘다.")]
        private float growthGlowCoreStrength = 0.8f;

        [Header("Evolve VFX (단계가 오를 때 재생되는 진화 연출)")]
        [Tooltip("비워 두면 단계가 오를 때 연출 없이 바로 바뀐다.")]
        [SerializeField] private VFX_LootEvolve evolveEffect;

        [Header("Growth Pop (단계가 오를 때 기둥·전리품이 하얗게 번쩍이며 뽀잉)")]
        [SerializeField, Tooltip("완전히 흰색으로 유지하는 시간(초). 이후 growthFlashDuration까지 원래 색으로 풀린다.")]
        private float growthFlashHold = 0.05f;
        [SerializeField, Tooltip("흰색으로 번쩍였다가 원래 색으로 돌아오기까지의 시간(초).")]
        private float growthFlashDuration = 0.3f;
        [SerializeField, Tooltip("뽀잉(눌렸다 튀며 출렁임) 전체 시간(초).")]
        private float growthPopDuration = 0.6f;
        [SerializeField, Range(0f, 0.5f), Tooltip("처음 눌리는 세로 비율. 0.2면 세로가 80%까지 눌렸다가 튄다.")]
        private float growthPopAmplitude = 0.2f;
        [SerializeField, Range(0f, 1f), Tooltip("세로 변화에 맞춰 가로가 반대로 변하는 비율(부피감).")]
        private float growthPopWidthRatio = 0.6f;
        [SerializeField, Tooltip("초당 출렁임 횟수.")]
        private float growthPopFrequency = 3.5f;
        [SerializeField, Tooltip("출렁임이 잦아드는 빠르기. 클수록 빨리 멈춘다.")]
        private float growthPopDamping = 6f;

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

        // 지금 표시 중인 성장 단계(0 = 모름/성장하지 않음). SetGrowthTier로 정해진다.
        private int currentGrowthTier = 0;

        // 진화 연출 중에 올라갈 단계(0 = 연출 중이 아님). 연출의 폭발 시각에 이 단계로 후광 색/스프라이트를 바꾼다.
        private int pendingGrowthTier = 0;
        private Action cachedOnEvolveBurst;
        // PlayGrowthEvolve가 진행 중인 연출을 끊고 다시 시작하는 동안 true. 그 사이 불리는 폭발 콜백에서 폭발음을 막는다.
        private bool bEvolveRestarting;

        // 성장 뽀잉/번쩍임 상태. 기둥과 전리품을 기둥 바닥(growthPopAnchorWorld) 기준으로 함께 늘이고 줄인다.
        // 진화 연출(VFX_LootEvolve)은 폭발 직후까지 전리품의 부모(LootAnchor)를 흔든다. 전리품 자신의 위치(밑둥 맞춤, SetDisplayBase)를
        // 다른 곳이 바꿔 써도 따라가도록, 마지막으로 읽은 위치(lootRawLocalPosition)를 원본으로 삼아 그 위에 뽀잉 보정만 더한다(LateUpdate).
        // 끝나면 기둥은 원래대로, 전리품은 원본 위치로 돌린다.
        private static readonly int FlashAmountID = Shader.PropertyToID("_FlashAmount");
        private MaterialPropertyBlock growthFlashMPB;
        private bool bGrowthPopPlaying;
        private float growthPopElapsed;
        private Vector3 growthPopAnchorWorld;
        private Vector3 pillarBaseLocalPosition;
        private Vector3 pillarBaseLocalScale;
        private Vector3 lootBaseLocalScale;
        private Vector3 lootRawLocalPosition;
        private Vector3 lootWrittenLocalPosition;
        private bool bLootPositionWritten;

        // 임시 스프라이트 경고를 (전리품, 단계)당 한 번만 낸다
        private static readonly HashSet<int> placeholderWarned = new HashSet<int>();

        private int characterLayer;
        private InputManager inputManager;
        private bool bPhysicalOverlapped;
        private bool bInteracting;

        private void Awake()
        {
            customSortable = GetComponent<CustomSortable>();
            cachedOnEvolveBurst = OnEvolveBurst;
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

            // 그림자는 기둥 바로 위, 전리품 아래. 아웃라인(+1)과 같은 Order지만 아웃라인은 기둥 바깥에만 그려져 겹치지 않는다.
            if (null != lootShadowRenderer)
            {
                lootShadowRenderer.sortingOrder = pillarOrder + 1;
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

            ApplyGrowthVisual();
        }

        /// <summary>
        /// 성장 단계(1~3)를 알려 주면 그 단계의 아우라 색과 스프라이트로 바꾼다. 같은 단계면 전리품 종류와 상관없이 같은 색이다.
        /// 0 이하(성장하지 않는 전리품 / 단계를 모를 때)면 종류별 기본 색(auraSettings)을 그대로 쓴다.
        /// SetLootDisplay 뒤에 불러야 한다.
        /// </summary>
        public void SetGrowthTier(int _tier)
        {
            // 연출 없이 바로 바꾼다(마을 기둥 생성 때). 진화 연출 중이었다면 보류해 둔 단계는 버리고 지정한 단계로 맞춘다.
            if (0 < pendingGrowthTier) pendingGrowthTier = 0;

            currentGrowthTier = _tier;
            ApplyGrowthVisual();
        }

        /// <summary>
        /// 단계 상승 연출(VFX_LootEvolve). 연출을 재생하고 후광/스프라이트 교체는 폭발 시각(약 0.8초)까지 미룬다.
        /// 연출 컴포넌트가 없거나 오브젝트가 꺼져 있거나 이미 성장한 상태(1단계 이상)에서 오르는 변화가 아니면 바로 바꾼다.
        /// 특성 창(TentUI)에서 프레스티지를 올리고 창을 닫았을 때 LootPillarManager.PlayPendingEvolutions가 부른다.
        /// </summary>
        public void PlayGrowthEvolve(int _tier)
        {
            if (null == evolveEffect || false == isActiveAndEnabled || false == (1 <= currentGrowthTier && currentGrowthTier < _tier))
            {
                SetGrowthTier(_tier);
                return;
            }

            int fromTier = 0 < pendingGrowthTier ? pendingGrowthTier : currentGrowthTier;

            // 진행 중이던 연출이 있으면 Play가 먼저 정리하면서 이전 목표 단계를 적용한 뒤 새 연출을 시작한다.
            // 그때 불리는 폭발 콜백은 연출이 끊긴 것이라 폭발음을 내지 않는다.
            // 이전 뽀잉이 남아 있으면 먼저 원래 자리/크기로 돌린다(연출이 시작 위치를 기준으로 기억하므로).
            StopGrowthPop();

            bEvolveRestarting = true;
            evolveEffect.Play(fromTier, _tier, cachedOnEvolveBurst);
            bEvolveRestarting = false;
            pendingGrowthTier = _tier;

            // 레벨업 시작음
            Sound.Play(SoundID.SFXLevelUpAura01, transform.position);
        }

        // 진화 연출의 폭발 시각: 이때 새 단계의 후광 색과 스프라이트로 바꾼다.
        private void OnEvolveBurst()
        {
            if (pendingGrowthTier <= 0) return;

            currentGrowthTier = pendingGrowthTier;
            pendingGrowthTier = 0;
            ApplyGrowthVisual();

            // 폭발음은 연출이 끝까지 진행돼 폭발 시각에 닿았을 때만 낸다(다시 시작/기둥이 꺼지며 정리될 때는 제외).
            if (true == bEvolveRestarting || false == isActiveAndEnabled) return;

            Sound.Play(SoundID.SFXLevelUpAura, transform.position);
            Sound.Play(SoundID.SFXLevelUpAura01, transform.position);
            Sound.Play(SoundID.SFXLevelUpChime, transform.position);

            StartGrowthPop();
        }

        // 기둥과 전리품을 하얗게 번쩍이며 눌렸다 튀게 한다. 진행은 LateUpdate에서 한다.
        private void StartGrowthPop()
        {
            if (null == pillarRenderer || null == targetRenderer) return;

            StopGrowthPop();

            Transform _pillar = pillarRenderer.transform;
            Transform _loot = targetRenderer.transform;

            // 기준점은 기둥 그림의 바닥 가운데다. 둘 다 이 점을 기준으로 늘고 줄어 한 덩어리처럼 출렁인다.
            float _bottom = null != pillarRenderer.sprite ? pillarRenderer.sprite.bounds.min.y : 0f;
            growthPopAnchorWorld = _pillar.TransformPoint(new Vector3(0f, _bottom, 0f));

            pillarBaseLocalPosition = _pillar.localPosition;
            pillarBaseLocalScale = _pillar.localScale;
            lootBaseLocalScale = _loot.localScale;
            lootRawLocalPosition = _loot.localPosition;
            bLootPositionWritten = false;

            growthPopElapsed = 0f;
            bGrowthPopPlaying = true;
        }

        private void StopGrowthPop()
        {
            if (false == bGrowthPopPlaying) return;

            bGrowthPopPlaying = false;

            Transform _pillar = pillarRenderer.transform;
            Transform _loot = targetRenderer.transform;

            _pillar.localPosition = pillarBaseLocalPosition;
            _pillar.localScale = pillarBaseLocalScale;
            _loot.localScale = lootBaseLocalScale;
            // 그사이 진화 연출이 위치를 다시 썼다면 그 값을 그대로 둔다.
            if (true == bLootPositionWritten && _loot.localPosition == lootWrittenLocalPosition) _loot.localPosition = lootRawLocalPosition;

            SetGrowthFlash(0f);
        }

        private void LateUpdate()
        {
            if (false == bGrowthPopPlaying) return;

            float _t = growthPopElapsed;

            if (Mathf.Max(growthPopDuration, growthFlashDuration) <= _t)
            {
                StopGrowthPop();
                return;
            }

            // 번쩍임: 잠깐 완전히 흰색을 유지했다가 원래 색으로 풀린다.
            float _flash = _t <= growthFlashHold ? 1f : 1f - Mathf.Clamp01((_t - growthFlashHold) / Mathf.Max(0.0001f, growthFlashDuration - growthFlashHold));
            SetGrowthFlash(_flash * _flash);

            // 뽀잉: 폭발 순간 세로로 눌린 채 시작해 감쇠 진동하며 1로 돌아온다. 끝에서 정확히 1이 되도록 남은 시간 비율을 곱한다.
            float _a = 0f;
            if (_t < growthPopDuration)
            {
                float _remain = 1f - _t / growthPopDuration;
                _a = -growthPopAmplitude * Mathf.Exp(-growthPopDamping * _t) * Mathf.Cos(2f * Mathf.PI * growthPopFrequency * _t) * _remain;
            }
            Vector3 _scale = new Vector3(1f - _a * growthPopWidthRatio, 1f + _a, 1f);

            Transform _pillar = pillarRenderer.transform;
            _pillar.localScale = Vector3.Scale(pillarBaseLocalScale, _scale);
            _pillar.localPosition = ScaleAroundAnchor(_pillar, pillarBaseLocalPosition, _scale);

            // 진화 연출이 이번 프레임에 전리품 위치를 새로 썼으면 그 값을 원본으로 삼는다.
            Transform _loot = targetRenderer.transform;
            if (false == bLootPositionWritten || _loot.localPosition != lootWrittenLocalPosition) lootRawLocalPosition = _loot.localPosition;

            _loot.localScale = Vector3.Scale(lootBaseLocalScale, _scale);
            lootWrittenLocalPosition = ScaleAroundAnchor(_loot, lootRawLocalPosition, _scale);
            _loot.localPosition = lootWrittenLocalPosition;
            bLootPositionWritten = true;

            growthPopElapsed += Time.deltaTime;
        }

        // _basePosition(부모 기준)을 기준점(growthPopAnchorWorld)에서 _scale만큼 늘이고 줄인 위치.
        private Vector3 ScaleAroundAnchor(Transform _target, Vector3 _basePosition, Vector3 _scale)
        {
            Vector3 _anchor = null != _target.parent ? _target.parent.InverseTransformPoint(growthPopAnchorWorld) : growthPopAnchorWorld;
            _anchor.z = _basePosition.z;
            return _anchor + Vector3.Scale(_basePosition - _anchor, _scale);
        }

        private void SetGrowthFlash(float _amount)
        {
            if (null == growthFlashMPB) growthFlashMPB = new MaterialPropertyBlock();

            ApplyGrowthFlash(pillarRenderer, _amount);
            ApplyGrowthFlash(targetRenderer, _amount);

            // 하얗게 번쩍이는 동안 그림자는 그만큼 옅어진다
            if (null != lootShadowRenderer)
            {
                lootShadowRenderer.color = new Color(0f, 0f, 0f, lootShadowAlpha * (1f - _amount));
            }
        }

        private void ApplyGrowthFlash(SpriteRenderer _renderer, float _amount)
        {
            if (null == _renderer) return;

            _renderer.GetPropertyBlock(growthFlashMPB);
            growthFlashMPB.SetFloat(FlashAmountID, _amount);
            _renderer.SetPropertyBlock(growthFlashMPB);
        }

        private void OnDisable()
        {
            StopGrowthPop();

            // 연출 중에 꺼지면(마을을 떠남 등) 연출이 정리되며 폭발 콜백을 불러 주지만, 못 불렸다면 여기서 단계를 맞춘다.
            if (0 < pendingGrowthTier)
            {
                currentGrowthTier = pendingGrowthTier;
                pendingGrowthTier = 0;
                ApplyGrowthVisual();
            }
        }

        private void ApplyGrowthVisual()
        {
            ApplyDisplayAnimation();

            if (null == auraOrbitController) return;

            // 성장하는 전리품은 단계 색, 성장하지 않는 전리품(단계 0)은 종류별 색(auraSettings)을 같은 세기로 쓴다
            Color _auraColor = default;
            bool _bHasColor = null != growthVisualData
                && (1 <= currentGrowthTier
                    ? growthVisualData.TryGetAuraColor(currentGrowthTier, out _auraColor)
                    : TryGetLootAuraColor(CurrentLootType, out _auraColor));

            if (false == _bHasColor)
            {
                auraOrbitController.ClearCenterGlowShaderColors();
                return;
            }

            if (true == useAuraOrbit)
            {
                // 글로우 머티리얼 기본색(금색)이 곱해져 렌더러 색만으로는 단계 색이 나오지 않으므로 셰이더 색을 직접 덮어쓴다.
                Color _glowCore = Color.Lerp(_auraColor, new Color(2f, 2f, 2f, 1f), 0.2f) * growthGlowCoreStrength;
                _glowCore.a = 1f;
                Color _glowOuter = _auraColor * growthGlowStrength;
                _glowOuter.a = 1f;
                auraOrbitController.SetCenterGlowShaderColors(_glowCore, _glowOuter);
            }
        }

        private bool TryGetLootAuraColor(LootType _lootType, out Color _color)
        {
            _color = default;

            if (null == auraSettings) return false;

            int _index = auraSettings.FindIndex(x => x.lootType == _lootType);

            if (_index < 0) return false;

            _color = auraSettings[_index].auraCenterColor;
            return true;
        }

        // ─── 전시 애니메이션 (마을 기둥 위 씨앗/꽃/나무) ─────────────────────────
        // idle 순서를 계속 반복하다가, action이 있으면 actionInterval(랜덤)마다 한 번 재생하고 idle로 돌아온다.
        // 성장하지 않는 전리품(단계 0)은 그 전리품의 가장 낮은 단계 프레임을 쓴다(LootGrowthVisualData.TryGetDisplay).

        private const float DisplayPixelsPerUnit = 32f;

        private Sprite[] animFrames;
        private int[] animIdle;
        private int[] animAction;
        private float animFrameTime;
        private Vector2 animInterval;
        private bool bAnimAction;
        private int animStep;
        private float animFrameTimer;
        private float animActionWait;

        private void ApplyDisplayAnimation()
        {
            animFrames = null;

            // 뽀잉은 전리품 위치를 원본으로 되돌리며 끝나므로, 밑둥 위치를 바꾸기 전에 먼저 정리한다.
            StopGrowthPop();

            if (null == growthVisualData || null == targetRenderer
                || false == growthVisualData.TryGetDisplay(CurrentLootType, currentGrowthTier, out LootGrowthVisualData.TierSprite _entry))
            {
                SetDisplayBase(Vector2.zero);
                SetLootShadow(0);
                SetGlowOffset(Vector2.zero);
                return;
            }

            // 그림마다 밑둥이 프레임 안 다른 곳에 있다 - 밑둥이 기둥 윗판 중앙에 오도록 전리품을 옮긴다(후광은 전리품 자식이라 같이 움직인다).
            SetDisplayBase(displayBaseAnchor - _entry.displayBase);
            SetLootShadow(_entry.displayShadowWidth);

            // 전시 그림은 32x32 프레임 아래쪽(기둥 윗면)에 서 있어 프레임 중심과 그림 중심이 다르다 - 후광을 그림 중심에 맞춘다.
            SetGlowOffset(_entry.displayArtCenter);

            animFrames = _entry.displayFrames;
            animIdle = _entry.idleFrames;
            animAction = _entry.actionFrames;
            animFrameTime = 1f / Mathf.Max(0.1f, _entry.fps);
            animInterval = _entry.actionInterval;
            bAnimAction = false;
            animStep = 0;
            animFrameTimer = 0f;
            // 기둥마다 동작 시점이 겹치지 않도록 첫 대기도 랜덤
            animActionWait = RollActionInterval();
            ShowAnimFrame(animIdle, 0);

            if (true == _entry.isPlaceholder)
            {
                int _key = ((int)CurrentLootType << 8) | currentGrowthTier;

                if (true == placeholderWarned.Add(_key))
                {
                    Debug.LogWarning("[LootDisplayObject] 임시 스프라이트 사용 중: " + CurrentLootType + " 단계 " + currentGrowthTier
                        + " (LootGrowthVisualData에서 정식 스프라이트로 교체하고 isPlaceholder를 끄세요)");
                }
            }
        }

        private void Update()
        {
            if (null == animFrames || 0 == animFrames.Length) return;

            float _dt = Time.deltaTime;

            if (false == bAnimAction && 0 < AnimLength(animAction, true))
            {
                animActionWait -= _dt;

                if (animActionWait <= 0f)
                {
                    bAnimAction = true;
                    animStep = 0;
                    animFrameTimer = 0f;
                    ShowAnimFrame(animAction, 0);
                    return;
                }
            }

            int[] _sequence = true == bAnimAction ? animAction : animIdle;
            int _length = AnimLength(_sequence, bAnimAction);

            // idle이 한 장이면 넘길 프레임이 없다
            if (false == bAnimAction && _length <= 1) return;

            animFrameTimer += _dt;

            while (animFrameTime <= animFrameTimer)
            {
                animFrameTimer -= animFrameTime;
                animStep++;

                if (_length <= animStep)
                {
                    animStep = 0;

                    if (true == bAnimAction)
                    {
                        bAnimAction = false;
                        animFrameTimer = 0f;
                        animActionWait = RollActionInterval();
                        ShowAnimFrame(animIdle, 0);
                        return;
                    }
                }
            }

            ShowAnimFrame(_sequence, animStep);
        }

        // 전리품을 부모(LootAnchor) 기준 _offset으로 옮긴다. 기둥과 픽셀 격자가 어긋나지 않도록 픽셀 단위로 맞춘다.
        // 진화 연출은 부모를 흔들고 성장 뽀잉은 이 위치를 원본으로 삼으므로 둘 다 이 값과 겹치지 않는다.
        private void SetDisplayBase(Vector2 _offset)
        {
            if (null == targetRenderer) return;

            Transform _loot = targetRenderer.transform;
            _loot.localPosition = new Vector3(
                Mathf.Floor(_offset.x * DisplayPixelsPerUnit + 0.5f) / DisplayPixelsPerUnit,
                Mathf.Floor(_offset.y * DisplayPixelsPerUnit + 0.5f) / DisplayPixelsPerUnit,
                _loot.localPosition.z);
        }

        // 전리품 크기에 맞춘 그림자 타원을 켠다(0 이하면 끈다). 세로는 가로의 30%를 짝수로 맞춘다 -
        // 가로·세로가 짝수라 타원 가장자리가 기둥 픽셀 격자에 맞는다(그림자 중심이 기둥 픽셀 경계에 있다).
        private void SetLootShadow(int _width)
        {
            if (null == lootShadowRenderer) return;

            if (_width <= 0)
            {
                lootShadowRenderer.enabled = false;
                return;
            }

            int _w = Mathf.Max(2, _width / 2 * 2);
            int _h = Mathf.Max(4, Mathf.RoundToInt(_w * 0.15f) * 2);

            lootShadowRenderer.sprite = GetShadowSprite(_w, _h);
            lootShadowRenderer.color = new Color(0f, 0f, 0f, lootShadowAlpha);
            lootShadowRenderer.enabled = true;
        }

        // 크기별 그림자 타원 스프라이트. 기둥마다 같은 크기를 쓰므로 한 번 만들어 계속 쓴다.
        private static readonly Dictionary<int, Sprite> shadowSprites = new Dictionary<int, Sprite>();

        private static Sprite GetShadowSprite(int _w, int _h)
        {
            int _key = (_w << 8) | _h;

            if (true == shadowSprites.TryGetValue(_key, out Sprite _cached) && null != _cached)
            {
                return _cached;
            }

            Texture2D _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false);
            _tex.filterMode = FilterMode.Point;
            _tex.wrapMode = TextureWrapMode.Clamp;
            _tex.hideFlags = HideFlags.DontSave;

            Color32[] _pixels = new Color32[_w * _h];
            float _rx = _w * 0.5f;
            float _ry = _h * 0.5f;

            for (int y = 0; y < _h; y++)
            {
                for (int x = 0; x < _w; x++)
                {
                    float _dx = (x + 0.5f - _rx) / _rx;
                    float _dy = (y + 0.5f - _ry) / _ry;
                    _pixels[y * _w + x] = _dx * _dx + _dy * _dy <= 1f ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }

            _tex.SetPixels32(_pixels);
            _tex.Apply(false, true);

            Sprite _sprite = Sprite.Create(_tex, new Rect(0f, 0f, _w, _h), new Vector2(0.5f, 0.5f), DisplayPixelsPerUnit, 0, SpriteMeshType.FullRect);
            _sprite.hideFlags = HideFlags.DontSave;
            shadowSprites[_key] = _sprite;
            return _sprite;
        }

        private void SetGlowOffset(Vector2 _offset)
        {
            if (null == auraOrbitController) return;

            auraOrbitController.SetCenterGlowOffset(_offset);
        }

        private float RollActionInterval()
        {
            return UnityEngine.Random.Range(animInterval.x, Mathf.Max(animInterval.x, animInterval.y));
        }

        // idle 순서가 비어 있으면 프레임 전부를 순서대로 쓴다. action 순서가 비어 있으면 action이 없다.
        private int AnimLength(int[] _sequence, bool _bAction)
        {
            if (null == _sequence || 0 == _sequence.Length)
            {
                return true == _bAction ? 0 : animFrames.Length;
            }

            return _sequence.Length;
        }

        private void ShowAnimFrame(int[] _sequence, int _step)
        {
            int _frame = (null == _sequence || 0 == _sequence.Length) ? _step : _sequence[_step];

            // 시트를 교체해 프레임 수가 줄었으면 범위 안으로 접는다
            if (_frame < 0 || animFrames.Length <= _frame)
            {
                _frame = Mathf.Clamp(_frame, 0, animFrames.Length - 1);
            }

            Sprite _sprite = animFrames[_frame];

            if (null != _sprite && targetRenderer.sprite != _sprite)
            {
                targetRenderer.sprite = _sprite;
            }
        }
    }
}
