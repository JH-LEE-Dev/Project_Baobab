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
        [SerializeField, Range(0f, 1f), Tooltip("그림자 진하기(검정 알파). 리니어 색공간에서 섞이므로 0.4가 그림자를 그려 넣은 기둥 그림(PillarGrassShadowVer)의 그림자와 같은 진하기다.")]
        private float lootShadowAlpha = 0.4f;

        [Header("Outline Settings")]
        // 상호작용 범위에 들어오면 켜지는 Pillar 아웃라인. OffroadContainer/RepairBox와 같은 스텐실 방식이다 -
        // outlineStencilObj(OutlineStencilWriter)가 Pillar 스프라이트 모양대로 스텐실을 찍고, 그 자식인
        // Outline이 스텐실 바깥쪽만 그려 테두리가 된다. 둘이 부모-자식이라 켜고 끄는 건 루트 하나만 토글하면 된다.
        // (Loot·Shadow는 제외하고 Pillar만 감싼다)
        [SerializeField] private GameObject outlineStencilObj;

        // 아웃라인 렌더러들. Pillar의 SortingOrder는 런타임에 정해지므로(ApplySortingBasis) 같이 따라가야
        // 한다. 스텐실 라이터는 Pillar와 같은 Order, 아웃라인은 그 바로 위(+1)에 그린다.
        // (Loot은 +4라 아웃라인이 아이템을 가리지 않는다)
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

        [Header("First Reveal (처음 얻은 전리품의 기둥이 하늘에서 떨어져 뽀잉 착지)")]
        [SerializeField, Tooltip("땅에 드리우는 기둥 그림자(Shadow). 떨어지는 동안 작게 시작해 커지며 떨어질 자리를 먼저 알린다.")]
        private SpriteRenderer groundShadowRenderer;
        [SerializeField, Tooltip("착지 순간 기둥 밑동에서 퍼지는 먼지. 차량 시동 먼지(VFX_StartUp)를 그대로 쓴다.")]
        private ParticleSystem revealLandingDustPrefab;
        [SerializeField, Range(0.3f, 1.5f), Tooltip("착지 먼지 크기 배율. 차량용 먼지라 기둥(폭 1칸)에 맞춰 줄인다.")]
        private float revealLandingDustScale = 0.75f;
        [SerializeField, Tooltip("기둥보다 그림자가 먼저 나타나는 시간(초).")]
        private float revealShadowLead = 0.2f;
        [SerializeField, Tooltip("화면 위에서 땅까지 떨어지는 시간(초). 갈수록 빨라진다(중력).")]
        private float revealFallDuration = 0.4f;
        [SerializeField, Tooltip("떨어지기 시작하는 높이 = 화면 위 끝에서 이만큼 더 위(유닛). 카메라를 못 찾으면 revealFallbackHeight를 쓴다.")]
        private float revealDropMargin = 0.5f;
        [SerializeField] private float revealFallbackHeight = 8f;
        [SerializeField, Range(0f, 0.6f), Tooltip("떨어지는 동안 세로로 늘어나는 정도(땅에 닿기 직전 최대).")]
        private float revealFallStretch = 0.3f;
        [SerializeField, Tooltip("착지 순간 납작하게 눌린 채 멈춰 있는 시간(초). 쿵 하는 무게감.")]
        private float revealHitStop = 0.06f;
        [SerializeField, Range(0f, 0.6f), Tooltip("착지 때 세로로 눌리는 비율. 0.4면 60%까지 눌렸다가 튄다.")]
        private float revealSquashAmplitude = 0.4f;
        [SerializeField, Range(0f, 1f), Tooltip("세로 변화에 맞춰 가로가 반대로 변하는 비율(부피감).")]
        private float revealSquashWidthRatio = 0.6f;
        [SerializeField, Tooltip("뽀잉 출렁임 전체 시간(초).")]
        private float revealSquashDuration = 0.8f;
        [SerializeField, Tooltip("초당 출렁임 횟수.")]
        private float revealSquashFrequency = 3.2f;
        [SerializeField, Tooltip("출렁임이 잦아드는 빠르기. 클수록 빨리 멈춘다.")]
        private float revealSquashDamping = 5f;
        [SerializeField, Tooltip("착지 때 완전히 흰색으로 유지하는 시간(초).")]
        private float revealFlashHold = 0.1f;
        [SerializeField, Tooltip("흰색에서 원래 색으로 돌아오기까지의 시간(초).")]
        private float revealFlashDuration = 0.5f;
        [SerializeField, Tooltip("착지 반동으로 위의 전리품이 튀어 오르는 높이(유닛).")]
        private float revealLootHopHeight = 0.4f;
        [SerializeField, Tooltip("전리품이 튀었다가 다시 기둥에 앉기까지의 시간(초).")]
        private float revealLootHopDuration = 0.36f;
        [SerializeField, Range(0f, 0.5f), Tooltip("전리품이 다시 앉을 때 눌리는 비율.")]
        private float revealLootLandSquash = 0.22f;
        [SerializeField, Tooltip("전리품이 다시 앉은 뒤 출렁임 시간(초).")]
        private float revealLootLandDuration = 0.35f;
        [SerializeField, Tooltip("착지 카메라 흔들림(세기, 시간). 옵션의 카메라 흔들림 비율이 곱해진다.")]
        private Vector2 revealCameraShake = new Vector2(3.5f, 0.25f);

        [Header("Aura Orbit Settings")]
        [SerializeField] private bool useAuraOrbit = false;
        [SerializeField] private ItemAuraOrbitController auraOrbitController;
        [SerializeField] private List<LootAuraSetting> auraSettings;
        [SerializeField, Tooltip("전리품 뒤 기둥 빛(웅덩이·빛 줄기·원반·후광·입자·반짝임). 아우라와 같은 시점에 켜고 끄고, 같은 색을 쓴다. 기존 중앙 글로우는 프리팹에서 꺼 둔다.")]
        private VFX_LootPillarGlow pillarGlow;

        // 상호작용 후보 상태(범위 안 + 등장 연출 아님 = CanFocus)가 바뀔 수 있을 때 알린다.
        // 기둥끼리 상호작용 범위가 겹치면 아웃라인·상호작용 키가 여러 기둥에서 동시에 켜지므로, 기둥은 스스로
        // 켜지 않고 LootPillarManager가 후보 중 캐릭터에 가장 가까운 기둥 하나만 SetFocused(true)로 고른다.
        // (UIView_Unit 상호작용 아이콘 알림도 LootPillarManager가 선택 결과로 한 번만 보낸다)
        public event Action FocusCandidateChangedEvent;

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
        // LootPillarManager가 고른 "지금 상호작용 대상" 기둥인지. 아웃라인·상호작용 키는 이 값으로만 켠다.
        private bool bFocused;
        // 범위에 들어온 캐릭터(거리 비교용)와 이 기둥의 상호작용 범위 콜라이더.
        private Transform overlapCharacter;
        private Collider2D interactCollider;

        /// <summary>캐릭터가 범위 안에 있고 등장 연출 중이 아니라 상호작용 대상이 될 수 있는지.</summary>
        public bool CanFocus => bPhysicalOverlapped && false == IsRevealing && isActiveAndEnabled;

        /// <summary>이 기둥의 창(ScreenModal)이 열려 있는지.</summary>
        public bool IsInteracting => bInteracting;

        /// <summary>범위에 들어온 캐릭터와 상호작용 범위 중심 사이의 거리. 캐릭터를 모르면 무한대.</summary>
        public float GetFocusDistance()
        {
            if (null == overlapCharacter) return float.MaxValue;

            Vector2 _center = null != interactCollider ? (Vector2)interactCollider.bounds.center : (Vector2)transform.position;
            return Vector2.Distance(overlapCharacter.position, _center);
        }

        /// <summary>
        /// LootPillarManager가 상호작용 대상으로 고르거나 해제한다. 해제될 때 창이 열려 있었다면
        /// 범위를 벗어난 것과 같이 닫는다(LootPillarInteractEvent(false)).
        /// </summary>
        public void SetFocused(bool _bFocused)
        {
            if (bFocused == _bFocused) return;

            bFocused = _bFocused;
            SetOutlineActive(_bFocused);

            if (false == _bFocused && true == bInteracting)
            {
                bInteracting = false;
                LootPillarInteractEvent?.Invoke(false, CurrentLootType);
            }
        }

        private void Awake()
        {
            customSortable = GetComponent<CustomSortable>();
            cachedOnEvolveBurst = OnEvolveBurst;
            characterLayer = LayerMask.NameToLayer("Character");
            interactCollider = GetComponent<Collider2D>();

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
            // 범위가 겹친 여러 기둥이 한꺼번에 열리지 않도록, LootPillarManager가 고른 기둥만 받는다.
            if (false == bFocused) return;

            // 등장 연출이 끝나기 전(숨겨져 있거나 떨어지는 중)에는 상호작용하지 않는다.
            if (true == IsRevealing) return;

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
                overlapCharacter = _other.transform;

                // 아웃라인·상호작용 아이콘은 LootPillarManager가 겹친 기둥 중 하나만 골라 켠다.
                // 등장 연출 중이면 CanFocus가 false라 고르지 않고, 연출이 끝날 때 다시 알린다(FinishFirstReveal).
                FocusCandidateChangedEvent?.Invoke();
            }
        }

        private void OnTriggerExit2D(Collider2D _other)
        {
            if (_other.gameObject.layer == characterLayer)
            {
                bPhysicalOverlapped = false;
                overlapCharacter = null;

                // LootPillarManager가 선택을 해제(SetFocused(false))한다. 상호작용(모달이 열린) 상태로 범위를
                // 벗어나면 그때 키를 다시 눌러 취소한 것과 동일하게 LootPillarInteractEvent(false)를 발행한다.
                FocusCandidateChangedEvent?.Invoke();
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

            // 전리품은 +4. 기둥과 전리품 사이(+2)에 기둥 빛(VFX_LootPillarGlow)의 뒤 층 묶음을, 그 위(+3)에 진화 연출 후광을 둔다.
            // 기둥 빛의 반짝임은 전리품 앞(+5)이다. 기둥 빛은 기둥 렌더러의 Order를 읽어 스스로 맞춘다.
            if (null != targetRenderer)
            {
                targetRenderer.sortingOrder = pillarOrder + 4;
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

            if (null != pillarGlow)
            {
                if (true == useAuraOrbit) pillarGlow.Play();
                else pillarGlow.Stop();
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
            // 등장 연출과 진화 연출은 둘 다 전리품 자리를 움직이므로, 등장 연출이 남아 있으면 끝난 모습으로 맞춘 뒤 진화한다.
            if (true == IsRevealing) FinishFirstReveal(true);

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
            if (true == bRevealPlaying) UpdateFirstReveal(Time.deltaTime);

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

            // 연출 도중 꺼지면(마을을 떠남 등) 원래 모습으로 되돌린다. 꺼지는 중이라 상호작용 알림은 보내지 않는다.
            if (true == IsRevealing) FinishFirstReveal(false);

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

            if (null == auraOrbitController && null == pillarGlow) return;

            // 성장하는 전리품은 단계 색, 성장하지 않는 전리품(단계 0)은 종류별 색(auraSettings)을 같은 세기로 쓴다
            Color _auraColor = default;
            bool _bHasColor = null != growthVisualData
                && (1 <= currentGrowthTier
                    ? growthVisualData.TryGetAuraColor(currentGrowthTier, out _auraColor)
                    : TryGetLootAuraColor(CurrentLootType, out _auraColor));

            if (false == _bHasColor)
            {
                if (null != auraOrbitController) auraOrbitController.ClearCenterGlowShaderColors();
                if (null != pillarGlow) pillarGlow.ClearColor();
                return;
            }

            if (null != pillarGlow) pillarGlow.SetColor(_auraColor);

            if (true == useAuraOrbit && null != auraOrbitController)
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

        // ─── 첫 등장 연출 (처음 얻은 전리품의 기둥이 하늘에서 떨어짐) ──────────────────
        // 순서: 숨김(PrepareFirstReveal) → 땅에 그림자가 먼저 생김 → 화면 위에서 가속하며 늘어난 채 낙하(그림자가 커짐)
        //      → 쿵 착지: 납작하게 눌린 채 잠깐 멈춤 + 하얗게 번쩍 + 먼지 + 카메라 흔들림/진동/효과음
        //      → 뽀잉 출렁이며 제 모양으로, 위의 전리품은 반동으로 톡 튀었다 다시 앉고 아우라가 켜진다.
        // 기둥(Pillar)·전리품 부모(LootAnchor)·땅 그림자(Shadow)는 모두 루트의 직속 자식이라, 루트 기준 좌표에서 기둥 밑동을 기준점으로 늘이고 줄인다.
        // 숨김은 크기 0으로 한다 - 렌더러를 끄면 단계 갱신(SetLootShadow 등)이 다시 켜 버릴 수 있다.

        private bool bRevealHidden;
        private bool bRevealPlaying;
        private bool bRevealImpacted;
        private bool bRevealAuraPlayed;
        private float revealElapsed;
        private float revealDropHeight;
        private Transform revealPillar;
        private Transform revealLootAnchor;
        private Transform revealShadow;
        private Vector3 revealPillarBasePosition;
        private Vector3 revealPillarBaseScale;
        private Vector3 revealLootAnchorBasePosition;
        private Vector3 revealLootAnchorBaseScale;
        private Vector3 revealShadowBasePosition;
        private Vector3 revealShadowBaseScale;
        private Color revealShadowBaseColor;
        // 기둥 밑동 가운데(루트 기준). 기둥과 그림자가 이 점을 기준으로 늘고 줄어 땅에 붙어 있는다.
        private Vector3 revealAnchor;

        /// <summary>등장 연출을 기다리며 숨겨져 있거나 연출 중인지. 이동안 상호작용·아웃라인이 막힌다.</summary>
        public bool IsRevealing => bRevealHidden || bRevealPlaying;

        /// <summary>
        /// 처음 얻은 전리품의 기둥을 숨겨 둔다(PlayFirstReveal로 떨어뜨린다). LootPillarManager가 기둥을 세운 직후, 마을 카메라가 내려오기 전에 부른다.
        /// SetLootDisplay·SetGrowthTier 뒤에 불러야 한다(아우라를 멈춰 두므로).
        /// </summary>
        public void PrepareFirstReveal()
        {
            if (true == IsRevealing || null == pillarRenderer || null == targetRenderer) return;

            StopGrowthPop();

            revealPillar = pillarRenderer.transform;
            revealLootAnchor = null != targetRenderer.transform.parent && targetRenderer.transform.parent != transform ? targetRenderer.transform.parent : targetRenderer.transform;
            revealShadow = null != groundShadowRenderer ? groundShadowRenderer.transform : null;

            revealPillarBasePosition = revealPillar.localPosition;
            revealPillarBaseScale = revealPillar.localScale;
            revealLootAnchorBasePosition = revealLootAnchor.localPosition;
            revealLootAnchorBaseScale = revealLootAnchor.localScale;

            if (null != revealShadow)
            {
                revealShadowBasePosition = revealShadow.localPosition;
                revealShadowBaseScale = revealShadow.localScale;
                revealShadowBaseColor = groundShadowRenderer.color;
            }

            float _bottom = null != pillarRenderer.sprite ? pillarRenderer.sprite.bounds.min.y : 0f;
            revealAnchor = revealPillarBasePosition + new Vector3(0f, _bottom * revealPillarBaseScale.y, 0f);

            bRevealHidden = true;
            bRevealPlaying = false;
            bRevealImpacted = false;
            bRevealAuraPlayed = false;

            // 숨어 있는 동안 아우라 위성·꼬리가 남지 않게 멈춘다. 착지 순간 다시 켠다.
            if (null != auraOrbitController) auraOrbitController.Stop();
            if (null != pillarGlow) pillarGlow.Stop();

            SetOutlineActive(false);
            ApplyRevealPose(Vector3.zero, Vector3.zero, Vector3.zero, 0f, 0f, 0f);

            // 숨은 동안은 상호작용 대상이 아니다(CanFocus == false). 골라져 있었다면 LootPillarManager가 해제한다.
            FocusCandidateChangedEvent?.Invoke();
        }

        /// <summary>
        /// 숨겨 둔 기둥을 _delay초 뒤 하늘에서 떨어뜨린다. 마을에서 캐릭터가 움직일 수 있게 된 순간 LootPillarManager가 부른다.
        /// </summary>
        public void PlayFirstReveal(float _delay)
        {
            if (false == bRevealHidden) return;

            bRevealHidden = false;
            bRevealPlaying = true;
            bRevealImpacted = false;
            revealElapsed = -Mathf.Max(0f, _delay);
            revealDropHeight = ComputeRevealDropHeight();
        }

        // 기둥 밑동이 화면 위 끝보다 revealDropMargin만큼 위에서 출발하는 높이. 기둥 전체가 화면 밖에서 들어온다.
        private float ComputeRevealDropHeight()
        {
            Camera _camera = Camera.main;
            if (null == _camera || false == _camera.orthographic) return revealFallbackHeight;

            float _top = _camera.transform.position.y + _camera.orthographicSize;
            float _bottomWorld = transform.TransformPoint(revealAnchor).y;
            return Mathf.Clamp(_top - _bottomWorld + revealDropMargin, 3f, 16f);
        }

        private void UpdateFirstReveal(float _dt)
        {
            revealElapsed += _dt;
            float _t = revealElapsed;

            // 차례를 기다리는 중(여럿이 함께 떨어질 때)
            if (_t < 0f) return;

            float _lead = Mathf.Max(0.0001f, revealShadowLead);
            float _fall = Mathf.Max(0.0001f, revealFallDuration);

            // 1) 그림자가 먼저 작게 생긴다
            if (_t < _lead)
            {
                float _p = _t / _lead;
                ApplyRevealPose(Vector3.zero, Vector3.zero, Vector3.zero, 0f, 0.25f * (1f - (1f - _p) * (1f - _p)), 0.6f * _p);
                return;
            }

            // 2) 화면 위에서 가속하며 떨어진다(높이 = H(1-u²)). 땅에 가까워질수록 세로로 늘어나고 그림자는 커진다.
            float _tf = _t - _lead;
            if (_tf < _fall)
            {
                if (_tf - _dt < 0f) Sound.Play(SoundID.SFXDashWoosh, transform.position, 1f, true, 0.8f);

                float _u = _tf / _fall;
                float _height = revealDropHeight * (1f - _u * _u);
                Vector3 _stretch = new Vector3(1f - revealFallStretch * 0.5f * _u, 1f + revealFallStretch * _u, 1f);
                ApplyRevealPose(_stretch, _stretch, Vector3.zero, _height, Mathf.Lerp(0.25f, 1f, _u * _u), Mathf.Lerp(0.6f, 1f, _u));
                return;
            }

            // 3) 착지
            float _tc = _tf - _fall;
            if (false == bRevealImpacted)
            {
                bRevealImpacted = true;
                OnRevealImpact();
            }

            // 기둥: 눌린 채 잠깐 멈췄다가(히트스톱) 감쇠 진동하며 1로 돌아온다. 끝에서 정확히 1이 되도록 남은 시간 비율을 곱한다.
            float _a;
            float _ts = _tc - revealHitStop;
            if (_ts < 0f)
            {
                _a = -revealSquashAmplitude;
            }
            else if (_ts < revealSquashDuration)
            {
                float _remain = 1f - _ts / revealSquashDuration;
                _a = -revealSquashAmplitude * Mathf.Exp(-revealSquashDamping * _ts) * Mathf.Cos(2f * Mathf.PI * revealSquashFrequency * _ts) * _remain;
            }
            else
            {
                _a = 0f;
            }
            Vector3 _pillarScale = new Vector3(1f - _a * revealSquashWidthRatio, 1f + _a, 1f);

            // 전리품: 히트스톱 동안은 기둥과 함께 눌려 있다가, 기둥이 튀어 오르는 반동으로 톡 떴다가 다시 앉으며 작게 출렁인다.
            Vector3 _lootScale;
            float _hop = 0f;
            float _hopDuration = Mathf.Max(0.0001f, revealLootHopDuration);
            if (_ts < 0f)
            {
                _lootScale = _pillarScale;
            }
            else if (_ts < _hopDuration)
            {
                float _hu = _ts / _hopDuration;
                _hop = 4f * revealLootHopHeight * _hu * (1f - _hu);
                // 뜨고 내리는 동안 살짝 길쭉하다(꼭대기에서 가장 둥글다)
                float _air = 0.12f * Mathf.Abs(2f * _hu - 1f);
                _lootScale = new Vector3(1f - _air * 0.5f, 1f + _air, 1f);
            }
            else
            {
                float _tl = _ts - _hopDuration;
                float _la = 0f;
                if (_tl < revealLootLandDuration)
                {
                    float _remain = 1f - _tl / Mathf.Max(0.0001f, revealLootLandDuration);
                    _la = -revealLootLandSquash * Mathf.Exp(-7f * _tl) * Mathf.Cos(2f * Mathf.PI * 4f * _tl) * _remain;
                }
                _lootScale = new Vector3(1f - _la * 0.6f, 1f + _la, 1f);
            }

            // 번쩍임: 잠깐 완전히 흰색을 유지했다가 원래 색으로 풀린다.
            float _flash = _tc <= revealFlashHold ? 1f : 1f - Mathf.Clamp01((_tc - revealFlashHold) / Mathf.Max(0.0001f, revealFlashDuration - revealFlashHold));
            SetGrowthFlash(_flash * _flash);

            ApplyRevealPose(_pillarScale, _lootScale, new Vector3(0f, _hop, 0f), 0f, 1f, 1f);

            float _end = Mathf.Max(revealHitStop + revealSquashDuration, revealFlashDuration, revealHitStop + _hopDuration + revealLootLandDuration);
            if (_end <= _tc) FinishFirstReveal(true);
        }

        // 착지 순간 한 번: 먼지, 카메라 흔들림, 진동, 효과음, 아우라.
        private void OnRevealImpact()
        {
            Vector3 _ground = transform.TransformPoint(revealAnchor);

            PlayRevealLandingDust(_ground);

            CameraMoveController.Instance?.ShakeCamera(revealCameraShake.x, revealCameraShake.y);
            Rumble.Play(EHapticEvent.LootPillarLanding);

            Sound.Play(SoundID.BoxLanding, _ground);
            Sound.Play(SoundID.BoxLandingEx, _ground);
            Sound.Play(SoundID.NewItemApear, _ground);

            PlayRevealAura();
        }

        private void PlayRevealAura()
        {
            if (true == bRevealAuraPlayed) return;
            bRevealAuraPlayed = true;

            // 아우라 Play는 코루틴을 쓰므로 꺼져 있으면 부르지 않는다(다시 켜질 때 아우라가 스스로 재생한다).
            if (true == useAuraOrbit && null != auraOrbitController && true == auraOrbitController.isActiveAndEnabled)
            {
                auraOrbitController.Play();
            }

            // 기둥 빛은 코루틴을 쓰지 않아 꺼져 있어도 부를 수 있다(켜지면 이어서 재생한다)
            if (true == useAuraOrbit && null != pillarGlow) pillarGlow.Play();
        }

        private void PlayRevealLandingDust(Vector3 _ground)
        {
            if (null == revealLandingDustPrefab) return;

            ParticleSystem _dust = Instantiate(revealLandingDustPrefab, _ground, revealLandingDustPrefab.transform.rotation);

            // 차량보다 작은 기둥에 맞춰 줄인다. 이 인스턴스만 바꾸므로 차량 먼지(원본 프리팹)는 그대로다.
            _dust.transform.localScale = Vector3.Scale(_dust.transform.localScale, new Vector3(revealLandingDustScale, revealLandingDustScale, 1f));
            ParticleSystem[] _systems = _dust.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < _systems.Length; i++)
            {
                ParticleSystem.MainModule _m = _systems[i].main;
                _m.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }

            // 기둥 바로 뒤에 그린다. 먼지 고리의 뒤쪽 절반(화면에서 밑동보다 위)은 기둥 몸통 뒤로 가려지고, 앞쪽 절반은 밑동 앞 땅 위에 보인다.
            // 기둥보다 위에 그리면 착지 순간 피어오르는 먼지가 하얗게 번쩍이는 기둥을 통째로 덮는다.
            int _order = null != pillarRenderer ? pillarRenderer.sortingOrder - 1 : 0;
            string _layer = null != pillarRenderer ? pillarRenderer.sortingLayerName : "Objects";
            ParticleSystemRenderer[] _renderers = _dust.GetComponentsInChildren<ParticleSystemRenderer>(true);
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].sortingLayerName = _layer;
                _renderers[i].sortingOrder = _order;
            }

            _dust.Play(true);

            ParticleSystem.MainModule _main = _dust.main;
            Destroy(_dust.gameObject, _main.duration + _main.startLifetime.constantMax + 0.5f);
        }

        // 루트 기준 좌표로 기둥·전리품·그림자 자세를 정한다. 크기 0이면 숨김.
        // _pillarScale: 밑동 기준 늘이기, _lootScale: 전리품 밑둥(displayBaseAnchor) 기준 늘이기, _lootOffset: 전리품만 더 올리는 값(튀어 오름),
        // _height: 기둥 전체를 올리는 높이(낙하), _shadowScale/_shadowAlpha: 땅 그림자 크기·진하기 비율.
        private void ApplyRevealPose(Vector3 _pillarScale, Vector3 _lootScale, Vector3 _lootOffset, float _height, float _shadowScale, float _shadowAlpha)
        {
            Vector3 _up = new Vector3(0f, _height, 0f);

            if (null != revealPillar)
            {
                revealPillar.localScale = Vector3.Scale(revealPillarBaseScale, _pillarScale);
                revealPillar.localPosition = ScaleAround(revealAnchor, revealPillarBasePosition, _pillarScale) + _up;
            }

            if (null != revealLootAnchor)
            {
                // 전리품 밑둥(기둥 윗판 중앙)이 늘어난 기둥 윗판을 따라가고, 전리품은 그 밑둥을 기준으로 따로 늘어난다.
                Vector3 _baseOffset = Vector3.Scale(new Vector3(displayBaseAnchor.x, displayBaseAnchor.y, 0f), revealLootAnchorBaseScale);
                Vector3 _seat = ScaleAround(revealAnchor, revealLootAnchorBasePosition + _baseOffset, _pillarScale) + _up + _lootOffset;
                Vector3 _scale = Vector3.Scale(revealLootAnchorBaseScale, _lootScale);

                revealLootAnchor.localScale = _scale;
                Vector3 _position = _seat - Vector3.Scale(new Vector3(displayBaseAnchor.x, displayBaseAnchor.y, 0f), _scale);
                _position.z = revealLootAnchorBasePosition.z;
                revealLootAnchor.localPosition = _position;
            }

            if (null != revealShadow)
            {
                Vector3 _s = new Vector3(_shadowScale, _shadowScale, 1f);
                revealShadow.localScale = Vector3.Scale(revealShadowBaseScale, _s);
                revealShadow.localPosition = ScaleAround(revealAnchor, revealShadowBasePosition, _s);

                Color _color = revealShadowBaseColor;
                _color.a *= _shadowAlpha;
                groundShadowRenderer.color = _color;
            }
        }

        // _point(루트 기준)를 _anchor에서 _scale만큼 늘이고 줄인 위치. z는 그대로 둔다.
        private static Vector3 ScaleAround(Vector3 _anchor, Vector3 _point, Vector3 _scale)
        {
            Vector3 _result = _anchor + Vector3.Scale(_point - _anchor, _scale);
            _result.z = _point.z;
            return _result;
        }

        // 원래 모습으로 되돌리고 상호작용을 연다. _bNotify면 범위 안에 서 있던 캐릭터에게 아웃라인·상호작용 아이콘을 지금 켠다.
        private void FinishFirstReveal(bool _bNotify)
        {
            if (false == IsRevealing) return;

            bRevealHidden = false;
            bRevealPlaying = false;

            if (null != revealPillar)
            {
                revealPillar.localPosition = revealPillarBasePosition;
                revealPillar.localScale = revealPillarBaseScale;
            }

            if (null != revealLootAnchor)
            {
                revealLootAnchor.localPosition = revealLootAnchorBasePosition;
                revealLootAnchor.localScale = revealLootAnchorBaseScale;
            }

            if (null != revealShadow)
            {
                revealShadow.localPosition = revealShadowBasePosition;
                revealShadow.localScale = revealShadowBaseScale;
                groundShadowRenderer.color = revealShadowBaseColor;
            }

            SetGrowthFlash(0f);
            PlayRevealAura();

            // 범위 안에 서 있던 캐릭터가 있으면 LootPillarManager가 겹친 기둥과 비교해 상호작용 대상을 다시 고른다.
            if (true == _bNotify && true == bPhysicalOverlapped)
            {
                FocusCandidateChangedEvent?.Invoke();
            }
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

        // 전리품 크기에 맞춘 그림자 타원을 켠다(0 이하면 끈다). 세로는 4px로 고정한다 - 흙 윗판(PillarGrassVer)이 낮아서
        // 더 높으면 가장 넓은 그림자(18px)가 윗판 모서리의 돌 테두리로 삐져나온다.
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
            const int _h = 4;

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
