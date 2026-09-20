using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

/// <summary>
/// 보석 나무가 최종적으로 쓰러질 때 떨어지는 원석.
///
/// 원목(LogItem)과 달리 인벤토리 칸을 쓰지 않는다. 주우면 곧바로 재화(황금/다이아/프리즘)로
/// 환산되어 돈처럼 쌓이므로, 인벤토리가 가득 찼는지 확인할 필요도 없고 벨트/재단기로 흘러가지도 않는다.
///
/// 떨어지는 모습만큼은 원목과 완전히 같아야 하므로, 포물선 공식·착지 스프링 연출·흡입 가속·
/// 그림자 4프레임 처리(Shadow_LogItem 셰이더)를 LogItem에서 그대로 가져왔다. 이 계열은
/// 아이템 종류마다 각자 이동 코드를 들고 있는 것이 이 프로젝트의 기존 방식이다
/// (LogItem / LootItem / CarrotItem 모두 같은 포물선을 각자 구현하고 있다).
/// </summary>
public class GemOreItem : Item, IStaticCollidable
{
    // 이벤트
    public event Action<GemOreItem> GemOreItemAcquired;

    // 움직이기 시작/멈춤을 소유자(GemOreItemController)에게 알려 업데이트 목록 출입을 관리하게 한다.
    // 바닥에 완전히 안착한 원석까지 매 프레임 돌릴 이유가 없다(원목과 동일한 구조).
    public event Action<GemOreItem> GemOreItemActivatedEvent;
    public event Action<GemOreItem> GemOreItemDeActivatedEvent;

    // IStaticCollidable 구현 - 캐릭터의 아이템 감지(Character.OnItemDetected)가 이 등록을 보고 흡입을 건다
    public Vector2 Position => transform.position;
    public Vector2 Offset => Vector2.zero;
    public float Radius => 0.1f;
    public int Layer => gameObject.layer;

    // 이 원석이 어떤 보석 종류인지. 획득 시 어느 재화로 들어갈지를 결정한다.
    public GemOreType gemOreType { get; private set; } = GemOreType.None;

    // 이 알갱이 하나가 지급할 재화량. 나무 한 그루의 총 재화량을 크기 비율로 나눠 받은 몫이라
    // 같은 종류·같은 크기라도 그루마다 값이 다를 수 있다.
    public long currencyAmount { get; private set; } = 1;

    // 이 알갱이의 크기. 외형(스프라이트)과 재화 분배 비중을 함께 결정한다.
    public GemOreSize gemOreSize { get; private set; } = GemOreSize.Small;

    public ItemMoveState MoveState => state;
    public bool IsMoving => state != ItemMoveState.Dropped && state != ItemMoveState.None;

    // 관리용 인덱스
    public int PoolIndex { get; set; } = -1;
    public int UpdateIndex { get; set; } = -1;

    // 이 오브젝트가 현재 풀 안에 들어가 있는지. 이중 반납을 O(1)로 차단하기 위한 플래그로,
    // 풀의 actionOnGet/actionOnRelease에서만 갱신한다.
    public bool IsPooled { get; set; } = false;

    // 내부 의존성
    [SerializeField] private SpriteRenderer spriteRenderer;
    private Transform visualTransform;
    private CustomSortable customSortable;
    private ICharacter character;

    // 상태
    private ItemMoveState state = ItemMoveState.None;
    private Transform suckTarget;
    private bool bCanAcquired = true;

    // 주머니 여유를 물어볼 곳. 가득 차 있으면 흡입 자체가 시작되지 않는다.
    private IInventoryChecker inventoryChecker;

    // 이동 관련
    private Vector3 startPos;
    private Vector3 endPos;
    private float height;
    private float duration;
    private float elapsed;
    private float totalRotation;
    private float suckSpeed;
    private bool bSuckAccelerating;
    private const float SuckAccel = 16f;
    private const float MinAcquireDist = 0.2f;

    // 착지 스프링 연출
    private float landingDampTime = landingDampDuration;
    private const float landingDampDuration = 0.5f;

    // ── 그림자 (LogItem과 동일한 스프라이트시트/셰이더를 그대로 쓴다) ──
    private static readonly int ShadowFrameRect0PropertyID = Shader.PropertyToID("_ShadowFrameRect0");
    private static readonly int ShadowFrameRect1PropertyID = Shader.PropertyToID("_ShadowFrameRect1");
    private static readonly int ShadowFrameRect2PropertyID = Shader.PropertyToID("_ShadowFrameRect2");
    private static readonly int ShadowFrameRect3PropertyID = Shader.PropertyToID("_ShadowFrameRect3");
    private static readonly int ShadowHeightPixelsPropertyID = Shader.PropertyToID("_ShadowHeightPixels");
    private static readonly int ShadowLocalOffsetPropertyID = Shader.PropertyToID("_ShadowLocalOffset");

    // 그림자 프레임 Rect/오프셋은 같은 머티리얼을 쓰는 모든 인스턴스에 동일하므로 머티리얼당 1회만 구우면 된다.
    private static readonly HashSet<Material> initializedShadowMaterials = new HashSet<Material>();

    [SerializeField] private GameObject shadow;
    private Transform shadowTransform;
    private SpriteRenderer shadowRenderer;

    [Header("Shadow Sprites By Height Position")]
    [Tooltip("원석이 -1 위치에 있을 때(바닥 아래로 1픽셀 내려갔을 때)")]
    [SerializeField] private Sprite shadowSprite_Minus1;
    [Tooltip("원석이 0 위치에 있을 때(기본 착지 위치)")]
    [SerializeField] private Sprite shadowSprite_0;
    [Tooltip("원석이 1 위치에 있을 때(바닥 위로 1픽셀 올라갔을 때)")]
    [SerializeField] private Sprite shadowSprite_1;
    [Tooltip("원석이 2 위치 이상에 있을 때(포물선 비행 중)")]
    [SerializeField] private Sprite shadowSprite_2Plus;
    [Tooltip("포물선 비행 중 그림자 프레임 전환 폭(값이 클수록 정점 부근에서 3번 프레임을 더 오래 유지)")]
    [SerializeField] private float shadowFlightPixelScale = 3f;

    // ── 아웃라인 (LogItem과 동일한 스텐실 2-패스 구조) ──
    [SerializeField] private GameObject outlineObj;
    [SerializeField] private SpriteRenderer outlineStencilSR;
    [SerializeField] private SpriteRenderer outlineSR;
    [Header("Outline Color")]
    [SerializeField] private Color normalOutlineColor = Color.white;
    private static readonly int OutlineColorPropertyID = Shader.PropertyToID("_OutlineColor");

    private MaterialPropertyBlock mpb;

    // ── 보석 연출 (LogItem의 보석 등급 원목과 동일한 3종) ──
    // 1) Shiny 파티클, 2) 종류별 아우라, 3) 셰이더 샤이니(_ShinyEnabled)
    private static readonly int ShinyEnabledPropertyID = Shader.PropertyToID("_ShinyEnabled");

    private VFXComponent vfxComponent;
    private ParticleSystem particleEffect;

    // 원석에 붙는 아우라. 소유자(GemOreItemController)가 풀로 관리하며 여기서는 빌려 쓴다.
    private IGemOreAuraProvider auraProvider;
    private ItemAuraEffectController gemAura;

    private Color originalColor;
    private Color originalOutlineColor;
    private Color originalShadowColor;
    private bool bRenderersCached = false;

    private const string objectsSortingLayerName = "Objects";
    private static int objectsSortingLayerID = -1;

    private static void EnsureSortingLayerIDs()
    {
        if (objectsSortingLayerID == -1)
        {
            objectsSortingLayerID = SortingLayer.NameToID(objectsSortingLayerName);
        }
    }

    private void Awake()
    {
        CacheRenderers();
    }

    public void Initialize(GemOreTypeData _typeData, GemOreSize _size, long _currencyAmount, ICharacter _character,
                           IInventoryChecker _inventoryChecker)
    {
        base.Initialize(ItemType.GemOre);

        character = _character;
        inventoryChecker = _inventoryChecker;
        gemOreType = _typeData.gemOreType;
        gemOreSize = _size;
        // 분배 결과가 0 이하로 떨어져도 주웠을 때 아무것도 안 들어오는 일은 없어야 한다.
        currencyAmount = _currencyAmount > 0 ? _currencyAmount : 1;

        sprite = _typeData.GetSprite(_size);
        color = _typeData.color;

        state = ItemMoveState.None;
        suckTarget = null;
        elapsed = 0f;
        landingDampTime = landingDampDuration;

        CacheRenderers();

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
            if (outlineStencilSR != null) outlineStencilSR.sprite = sprite;
            if (outlineSR != null) outlineSR.sprite = sprite;
        }

        ApplyOutlineColor();
        InitializeShadowFrames();
        EnsureSortingLayerIDs();

        transform.localScale = Vector3.one;

        if (customSortable == null)
        {
            customSortable = GetComponent<CustomSortable>();
        }

        if (customSortable != null)
        {
            // 정렬 기준(Anchor)을 상하 이동하는 visualTransform으로 설정
            customSortable.Initialize(visualTransform != null ? visualTransform : transform);
            customSortable.AddSpriteRenderer(spriteRenderer);
            customSortable.AddSpriteRenderer(outlineSR);
        }
    }

    /// <summary>
    /// 렌더러/트랜스폼과 색 원본값을 한 번만 캐싱한다.
    /// 풀에서 갓 생성된 인스턴스는 Get 시점의 ResetItem()이 Initialize()보다 먼저 불리므로
    /// (LogItem과 같은 이유) Awake와 ResetItem 양쪽에서 확보한다.
    /// </summary>
    private void CacheRenderers()
    {
        if (bRenderersCached) return;

        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // 아직 준비되지 않았으면 캐싱 완료로 표시하지 않고 다음 호출에서 다시 시도한다
        if (spriteRenderer == null) return;

        visualTransform = spriteRenderer.transform;

        if (shadow != null && shadowTransform == null)
        {
            shadowTransform = shadow.transform;
            shadowRenderer = shadow.GetComponentInChildren<SpriteRenderer>();
        }

        originalColor = spriteRenderer.color;
        originalOutlineColor = outlineSR != null ? outlineSR.color : Color.white;
        originalShadowColor = shadowRenderer != null ? shadowRenderer.color : Color.white;

        bRenderersCached = true;
    }

    public void SetCharacter(ICharacter _character)
    {
        character = _character;
    }

    public void SetVfxComponent(VFXComponent _vfxComponent)
    {
        vfxComponent = _vfxComponent;
    }

    public void SetAuraProvider(IGemOreAuraProvider _auraProvider)
    {
        auraProvider = _auraProvider;
    }

    public void SetbCanAcquired(bool _boolean)
    {
        bCanAcquired = _boolean;
    }

    /// <summary>
    /// 원목과 동일한 포물선으로 던진다. 비행시간은 높이에서만 유도하므로(중력상수 15f)
    /// 같은 높이를 넘기면 원목과 완전히 같은 궤적·같은 속도로 떨어진다.
    /// </summary>
    public void Launch(Vector3 _start, Vector3 _end, float _height, float _totalRotation = 0f)
    {
        startPos = _start;
        endPos = _end;
        height = _height;

        // 포물선의 높이에 따라서만 비행시간(duration) 동적 계산
        float gravityConstant = 15f;
        duration = 2f * Mathf.Sqrt(2f * _height / gravityConstant);
        if (duration < 0.1f)
        {
            duration = 0.1f; // 최소 비행시간 제한
        }

        totalRotation = _totalRotation;
        elapsed = 0f;
        state = ItemMoveState.Launching;
        GemOreItemActivatedEvent?.Invoke(this);
        transform.localScale = Vector3.zero;

        if (outlineObj != null)
        {
            outlineObj.SetActive(true);

            if (visualTransform != null)
            {
                outlineObj.transform.localPosition = visualTransform.localPosition;
                outlineObj.transform.localRotation = visualTransform.localRotation;
                outlineObj.transform.localScale = visualTransform.localScale;
            }
        }

        if (gameObject.activeInHierarchy)
        {
            CollisionSystem.Instance?.Register(this, false);
        }

        // 아우라는 착지가 아니라 생성(발사) 시점부터 붙어, 포물선 비행 내내 보인다.
        PlayGemAura();
    }

    private void OnEnable()
    {
        // Launch가 이미 호출된 상태에서 활성화될 때만 등록
        if (state != ItemMoveState.None)
        {
            CollisionSystem.Instance?.Register(this, false);
        }
    }

    private void OnDisable()
    {
        CollisionSystem.Instance?.Unregister(this, false);
    }

    private void OnDestroy()
    {
        transform.DOKill();

        if (null != visualTransform)
        {
            visualTransform.DOKill();
        }
    }

    public override void ResetItem()
    {
        base.ResetItem();

        CacheRenderers();

        StopGemShiny();
        StopGemAura();

        state = ItemMoveState.None;
        suckTarget = null;
        elapsed = 0f;
        totalRotation = 0f;
        suckSpeed = 0f;
        bSuckAccelerating = false;
        bCanAcquired = true;
        landingDampTime = landingDampDuration;

        transform.DOKill();
        transform.localScale = Vector3.one;

        if (null != outlineObj)
            outlineObj.SetActive(false);

        if (null != outlineSR)
        {
            outlineSR.SetPropertyBlock(null);
            outlineSR.color = originalOutlineColor;
        }

        if (null != shadowRenderer)
        {
            shadowRenderer.color = originalShadowColor;
        }

        if (null != spriteRenderer)
        {
            EnsureSortingLayerIDs();
            spriteRenderer.color = originalColor;
            spriteRenderer.SetPropertyBlock(null);
            spriteRenderer.sortingLayerID = objectsSortingLayerID;
        }

        if (null != sprite && null != spriteRenderer)
            spriteRenderer.sprite = sprite;

        if (null != visualTransform)
        {
            visualTransform.DOKill();
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = Vector3.one;
        }

        if (null != customSortable)
        {
            customSortable.SetHeight(0f);
        }

        if (null != shadowTransform)
        {
            shadowTransform.localScale = Vector3.one;
        }

        SetShaderShiny(false);
    }

    public void ManualUpdate(float _deltaTime)
    {
        switch (state)
        {
            case ItemMoveState.Launching:
                UpdateLaunching(_deltaTime);
                break;
            case ItemMoveState.Sucking:
                UpdateSucking(_deltaTime);
                break;
            case ItemMoveState.Dropped:
                UpdateDropped(_deltaTime);
                break;
        }

        UpdateSortingOrder();

        SyncGemAura();
        SyncParticleSorting();
    }

    public void UpdateSortingOrder()
    {
        if (customSortable == null) return;

        customSortable.ManualLateUpdate();
        if (outlineStencilSR != null && outlineSR != null)
            outlineStencilSR.sortingOrder = outlineSR.sortingOrder - 1;
    }

    // LogItem.UpdateLaunching과 같은 공식을 쓴다(가로 등속 + 포물선 높이 + 수직속도 비례 부피감
    // + BackEaseOut 팝업). 값을 바꾸면 원목과 다르게 떨어지므로 함께 고쳐야 한다.
    private void UpdateLaunching(float _deltaTime)
    {
        elapsed += _deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // 1. 가로 이동: 등속도(고정된 속도)로 목적지까지 이동
        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, t);

        // 2. 높이 계산 (포물선)
        float heightOffset = -4f * height * (t - 0.5f) * (t - 0.5f) + height;

        if (visualTransform != null)
        {
            transform.position = currentGroundPos;
            visualTransform.localPosition = new Vector3(0, heightOffset, 0);
            visualTransform.localRotation = Quaternion.Euler(0, 0, totalRotation * t);

            if (customSortable != null)
            {
                customSortable.SetHeight(heightOffset);
            }

            // 3. Uniform Scale (수직 속도에 비례하여 부피감이 변함)
            float verticalVelocity = -8f * height * (t - 0.5f) / duration;
            float pulse = Mathf.Abs(verticalVelocity) * 0.03f;
            pulse = Mathf.Min(pulse, 0.2f); // 최대 변형치 제한

            visualTransform.localScale = Vector3.one * (1f + pulse);

            if (outlineObj != null)
            {
                outlineObj.transform.localPosition = visualTransform.localPosition;
                outlineObj.transform.localRotation = visualTransform.localRotation;
                outlineObj.transform.localScale = visualTransform.localScale;
            }
        }
        else
        {
            transform.position = currentGroundPos + new Vector3(0, heightOffset, 0);
        }

        UpdateShadowScale(heightOffset);

        // 4. 전체 Scale 팝업 (0.8까지 BackEaseOut 효과로 탄력 있게 커짐)
        float targetScale = 1f;
        if (t < 0.8f)
        {
            float nt = t / 0.8f;
            const float s = 2.5f;
            float t1 = nt - 1f;
            targetScale = Mathf.Max(0, (t1 * t1 * ((s + 1f) * t1 + s) + 1f));
        }
        transform.localScale = Vector3.one * targetScale;

        CollisionSystem.Instance?.UpdatePosition(this, transform.position);

        if (t >= 1.0f)
        {
            transform.position = GlobalPixelSnapper.Snap(endPos);

            if (visualTransform != null)
            {
                visualTransform.localPosition = Vector3.zero;
                visualTransform.localRotation = Quaternion.identity;

                if (outlineObj != null)
                {
                    outlineObj.transform.localPosition = Vector3.zero;
                    outlineObj.transform.localRotation = Quaternion.identity;
                    outlineObj.transform.localScale = Vector3.one;
                }
            }

            transform.localScale = Vector3.one;

            if (customSortable != null)
            {
                customSortable.SetHeight(0f);
            }

            UpdateShadowScale(0f);

            landingDampTime = 0f;
            state = ItemMoveState.Dropped;

            SetShaderShiny(true);
            PlayShinyEffect();

            // 착지 시점에 이미 흡입 예약이 걸려 있었다면 바로 이어서 빨려간다
            if (suckTarget != null)
            {
                StartSucking(suckTarget);
            }
        }
    }

    private void UpdateDropped(float _deltaTime)
    {
        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.zero;

            if (customSortable != null)
            {
                customSortable.SetHeight(0f);
            }

            UpdateShadowScale(0f);

            // 착지 후 스프링 댐퍼 쫀득한 Scale 연출 (LogItem과 동일한 계수)
            if (landingDampTime < landingDampDuration)
            {
                landingDampTime += _deltaTime;

                float freq = 25f;  // 진동 속도
                float decay = 7f;  // 감쇠율
                float amp = 0.4f;  // 최초 충격 변형량

                float springEffect = Mathf.Cos(landingDampTime * freq) * Mathf.Exp(-landingDampTime * decay) * amp;

                // Squash & Stretch: 수평(X)은 늘어나고 수직(Y)은 찌그러짐
                visualTransform.localScale = new Vector3(1f + springEffect, 1f - springEffect, 1f);

                if (landingDampTime >= landingDampDuration)
                {
                    visualTransform.localScale = Vector3.one;
                    GemOreItemDeActivatedEvent?.Invoke(this);
                }
            }
            else
            {
                visualTransform.localScale = Vector3.one;
            }

            if (outlineObj != null)
            {
                outlineObj.transform.localPosition = visualTransform.localPosition;
                outlineObj.transform.localScale = visualTransform.localScale;
                outlineObj.transform.localRotation = visualTransform.localRotation;
            }
        }

        // 원석은 인벤토리 칸을 쓰지 않으므로 원목처럼 주기적으로 여유 칸을 검사할 필요가 없다.
        // 흡입 대상이 잡히는 즉시(SetSuckTarget) 빨려간다.
        if (suckTarget != null)
        {
            StartSucking(suckTarget);
        }
    }

    // LogItem.UpdateSucking과 동일한 "뒤로 살짝 튕겼다가 가속해서 빨려드는" 흡입 연출.
    private void UpdateSucking(float _deltaTime)
    {
        if (suckTarget == null || (character != null && character.bDead))
        {
            suckTarget = null;
            transform.localScale = Vector3.one;
            if (visualTransform != null) visualTransform.localScale = Vector3.one;
            state = ItemMoveState.Dropped;

            // 다른 착지 경로와 맞춰 반짝임과 아우라를 되살린다
            SetShaderShiny(true);
            PlayShinyEffect();
            PlayGemAura();
            return;
        }

        elapsed += _deltaTime;

        Vector3 targetPos = suckTarget.position;
        Vector3 diff = targetPos - transform.position;
        float sqrDistance = diff.sqrMagnitude;

        // 프레임 드랍 방어 가드: 다음 이동 거리가 남은 거리보다 크거나 같다면 오버슈트 방지를 위해 바로 획득 처리
        if (suckSpeed > 0f)
        {
            float nextMoveStep = suckSpeed * _deltaTime;
            if (nextMoveStep * nextMoveStep >= sqrDistance)
            {
                transform.position = targetPos;
                GemOreItemAcquired?.Invoke(this);
                return;
            }
        }

        // 도착 조건: 거리가 가깝고 타겟을 향해 이동 중일 때
        if (suckSpeed > 0f && sqrDistance < (MinAcquireDist * MinAcquireDist))
        {
            GemOreItemAcquired?.Invoke(this);
            return;
        }

        if (suckSpeed < 0f)
        {
            // 뒤로 튕기는 구간
            suckSpeed += (SuckAccel * 2.0f) * _deltaTime;
        }
        else
        {
            // 튕김이 끝나고 본격적으로 전진하기 시작하는 첫 프레임 감지
            if (!bSuckAccelerating)
            {
                bSuckAccelerating = true;
                elapsed = 0f;
            }

            float dynamicAccel = SuckAccel * 2.5f * (1f + suckSpeed * 0.15f);
            suckSpeed += dynamicAccel * _deltaTime;
            suckSpeed = Mathf.Min(suckSpeed, 35f); // 가속도 폭발 방지
        }

        Vector3 dir = diff.normalized;
        transform.position += dir * suckSpeed * _deltaTime;

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, Vector3.zero, _deltaTime * 10f);
            visualTransform.localScale = Vector3.one;

            if (customSortable != null)
            {
                customSortable.SetHeight(visualTransform.localPosition.y);
            }

            UpdateShadowScale(visualTransform.localPosition.y);

            if (outlineObj != null)
            {
                outlineObj.transform.localPosition = visualTransform.localPosition;
                outlineObj.transform.localScale = visualTransform.localScale;
                outlineObj.transform.localRotation = visualTransform.localRotation;
            }
        }

        // 타겟에 매우 가까워지면 전체 스케일 축소 (최소 0.35 유지)
        if (suckSpeed > 0f && sqrDistance < (1.5f * 1.5f))
        {
            float distance = Mathf.Sqrt(sqrDistance);
            float scaleT = Mathf.Max(0.35f, distance / 1.5f);
            transform.localScale = Vector3.one * scaleT;
        }

        CollisionSystem.Instance?.UpdatePosition(this, transform.position);
    }

    /// <summary>
    /// 캐릭터의 아이템 감지(Character.OnItemDetected)가 Item 기반으로 호출한다.
    /// 원목과 동일하게 착지(Dropped)한 뒤에만 흡입을 받는다. 비행 중에 미리 예약되지 않으므로
    /// 착지 후 다음 감지 주기(Character.itemDetectionInterval)에 걸려 빨려간다.
    ///
    /// 원목이 인벤토리 여유를 보는 것처럼(CheckAcquireCondition) 원석은 주머니 여유를 본다.
    /// 자리가 아예 없으면 빨려가지 않고 바닥에 그대로 남는다 - 빨아들인 뒤 한 톨도 못 담고
    /// 사라지면 플레이어 눈에는 원석이 그냥 증발한 것으로 보이기 때문이다.
    /// 자리가 조금이라도 있으면 빨려가고, 담을 수 있는 만큼만 담긴다(GemOreEarned).
    /// </summary>
    public override void SetSuckTarget(Transform _target)
    {
        if (state != ItemMoveState.Dropped || bCanAcquired == false) return;

        // checker가 없는 경로(주입 전)는 예전처럼 그냥 받는다. 주머니 판정만 못 할 뿐 동작은 유지된다.
        if (inventoryChecker != null && false == inventoryChecker.CanAcquireGemOre()) return;

        suckTarget = _target;
        StartSucking(suckTarget);
    }

    private void StartSucking(Transform _target)
    {
        if (state == ItemMoveState.Sucking) return;

        if (vfxComponent != null && particleEffect != null)
        {
            vfxComponent.Stop(particleEffect, false);
            particleEffect = null;
        }

        StopGemAura();
        SetShaderShiny(false);

        suckTarget = _target;
        suckSpeed = -5.0f; // 뒤로 튕기는 동작
        elapsed = 0f;
        bSuckAccelerating = false;
        state = ItemMoveState.Sucking;
        GemOreItemActivatedEvent?.Invoke(this);
    }

    // ── 그림자 (LogItem과 동일) ──

    private void UpdateShadowScale(float _heightOffset)
    {
        if (shadowTransform == null) return;

        float shadowScale = Mathf.Max(0.3f, 1f - (_heightOffset * 0.25f));
        shadowTransform.localScale = new Vector3(shadowScale, shadowScale, 1f);

        // Dropped 상태에서는 그림자의 높이-프레임 판정(둥둥 뜨는 움직임 포함)이 Shadow_LogItem 셰이더
        // 내부에서 전부 처리되므로(CPU 연산 없음), 여기서 프로퍼티 블록을 갱신하지 않는다.
        if (state == ItemMoveState.Dropped || shadowRenderer == null) return;

        // 이번 비행의 정점(height) 대비 진행률로 정규화해서, 솟아오를 때 1->2->3,
        // 떨어질 때 3->2->1로 전환되고 착지 순간에는 항상 1번 프레임으로 이어지도록 한다.
        float normalizedHeight = height > 0.0001f ? Mathf.Clamp01(_heightOffset / height) : 0f;
        float shadowHeightPixels = normalizedHeight * shadowFlightPixelScale;

        if (mpb == null) mpb = new MaterialPropertyBlock();
        shadowRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ShadowHeightPixelsPropertyID, shadowHeightPixels);
        shadowRenderer.SetPropertyBlock(mpb);
    }

    // 그림자용 4프레임 스프라이트의 UV 사각형을 계산해 셰이더에 1회 전달한다.
    // LogItem과 같은 그림자 스프라이트시트/머티리얼을 쓰면 이미 구워진 머티리얼을 그대로 재사용한다.
    private void InitializeShadowFrames()
    {
        if (shadowRenderer == null) return;

        shadowRenderer.sprite = shadowSprite_0 != null ? shadowSprite_0 : shadowRenderer.sprite;

        Material sharedShadowMaterial = shadowRenderer.sharedMaterial;
        if (sharedShadowMaterial != null && initializedShadowMaterials.Add(sharedShadowMaterial))
        {
            sharedShadowMaterial.SetVector(ShadowFrameRect0PropertyID, GetSpriteUVRect(shadowSprite_Minus1));
            sharedShadowMaterial.SetVector(ShadowFrameRect1PropertyID, GetSpriteUVRect(shadowSprite_0));
            sharedShadowMaterial.SetVector(ShadowFrameRect2PropertyID, GetSpriteUVRect(shadowSprite_1));
            sharedShadowMaterial.SetVector(ShadowFrameRect3PropertyID, GetSpriteUVRect(shadowSprite_2Plus));

            Vector3 localOffset = shadowTransform != null ? shadowTransform.localPosition : Vector3.zero;
            sharedShadowMaterial.SetVector(ShadowLocalOffsetPropertyID, new Vector4(localOffset.x, localOffset.y, 0f, 0f));
        }

        if (mpb == null) mpb = new MaterialPropertyBlock();
        shadowRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ShadowHeightPixelsPropertyID, 0f);
        shadowRenderer.SetPropertyBlock(mpb);
    }

    private static Vector4 GetSpriteUVRect(Sprite _sprite)
    {
        if (_sprite == null || _sprite.texture == null) return new Vector4(0f, 0f, 1f, 1f);

        Rect r = _sprite.textureRect;
        float texWidth = _sprite.texture.width;
        float texHeight = _sprite.texture.height;

        return new Vector4(r.xMin / texWidth, r.yMin / texHeight, r.xMax / texWidth, r.yMax / texHeight);
    }

    // ── 보석 연출 (LogItem의 같은 이름 메서드들과 동작이 동일하다) ──

    /// <summary>
    /// 외부에서 원석을 직접 배치했을 때 반짝임을 붙인다.
    /// 포물선 발사(Launch)를 타지 않는 경로를 위한 공개 진입점이다.
    /// </summary>
    public void PlayGemShiny()
    {
        PlayShinyEffect();
    }

    private void PlayShinyEffect()
    {
        if (null == vfxComponent || gemOreType == GemOreType.None) return;

        // 이전 재생분이 남아 있으면 정리하고 새로 붙인다(중복 부착 방지).
        if (null != particleEffect)
        {
            vfxComponent.Stop(particleEffect, false);
            particleEffect = null;
        }

        particleEffect = vfxComponent.Play("Shiny", transform.position, transform.rotation, transform);
        if (null == particleEffect) return;

        particleEffect.transform.localScale = Vector3.one;
        SyncParticleSorting();
    }

    /// <summary>
    /// 붙어 있는 반짝임 파티클을 즉시 풀로 회수한다.
    /// 오브젝트를 비활성화하기 전에 반드시 호출해야 한다. 자식으로 매달린 채 부모가 꺼지면
    /// 파티클의 activeSelf는 true로 남아 풀이 "사용 중"으로 오인하고, 그 인스턴스는 영영
    /// 재사용되지 못한 채 누수된다.
    /// </summary>
    public void StopGemShiny()
    {
        if (null == particleEffect) return;

        if (null != vfxComponent && particleEffect.transform.IsChildOf(transform))
            vfxComponent.Stop(particleEffect, true);

        particleEffect = null;
    }

    /// <summary>
    /// 반짝임 파티클을 원석 본체 바로 앞에 그린다.
    /// sortingOrder는 CustomSortable이 위치에 따라 매 프레임 다시 계산하므로 계속 따라가야 한다.
    /// </summary>
    private void SyncParticleSorting()
    {
        if (vfxComponent == null || particleEffect == null || spriteRenderer == null) return;

        if (!particleEffect.transform.IsChildOf(transform))
        {
            particleEffect = null;
            return;
        }

        vfxComponent.SetSortingSettings(particleEffect, spriteRenderer.sortingLayerName, spriteRenderer.sortingOrder + 1);
    }

    /// <summary>
    /// 원석 종류별 아우라를 붙인다. 발사 시점부터 붙어 비행 내내 보이고,
    /// 흡입이 시작되거나 풀로 반환될 때 회수된다.
    /// </summary>
    private void PlayGemAura()
    {
        // 이미 붙어 있으면 중복 부착하지 않는다(착지 -> 흡입취소 -> 재착지 경로에서 새는 것을 막는다).
        if (auraProvider == null || gemAura != null) return;
        if (gemOreType == GemOreType.None) return;

        gemAura = auraProvider.GetAura(gemOreType);
        if (gemAura == null) return;

        Transform auraTransform = gemAura.transform;
        auraTransform.SetParent(transform, false);
        auraTransform.localPosition = Vector3.zero;

        SyncGemAura();
        gemAura.Play();
    }

    /// <summary>
    /// 아우라를 원석 그림에 맞춰 따라가게 한다. 매 프레임 호출된다.
    ///
    /// 위치: 본체 transform은 지면 좌표를 선형 보간할 뿐이고(그림자가 그 자리에 있다),
    /// 포물선 높이는 visualTransform.localPosition이 들고 있다. visualTransform에 직접 붙이지 않는
    /// 이유는 착지 스쿼시(비균등 스케일)까지 상속받아 방사형 아우라가 타원으로 찌그러지기 때문이다.
    ///
    /// 정렬: 프리셋 프리팹은 Default 레이어(맨 뒤)라 맞추지 않으면 가려지고,
    /// sortingOrder는 CustomSortable이 매 프레임 다시 계산하므로 한 번만 설정하면 어긋난다.
    /// </summary>
    private void SyncGemAura()
    {
        if (gemAura == null) return;

        if (visualTransform != null)
        {
            gemAura.transform.localPosition = visualTransform.localPosition;
        }

        if (spriteRenderer == null) return;

        gemAura.SetSortingLayer(spriteRenderer.sortingLayerID);
        // 원석 뒤에 그린다. 본체 렌더러들이 order와 order-1(아웃라인 스텐실)을 이미 쓰고 있어,
        // -1로 두면 스텐실과 순서가 같아져 그리기 순서가 불안정해지므로 한 칸 더 뒤로 뺀다.
        gemAura.SetSortingOrder(spriteRenderer.sortingOrder - 2);
    }

    /// <summary>
    /// 풀로 반환될 때 빌려온 아우라만 즉시 돌려준다.
    /// ResetItem은 다음 획득 시점에 호출되므로, 그대로 두면 원석이 풀에서 쉬는 동안에도
    /// 아우라가 대여 상태로 묶여 실제 동시 사용량보다 풀이 크게 잡힌다.
    /// </summary>
    public void ReleaseGemAura()
    {
        StopGemAura();
    }

    private void StopGemAura()
    {
        if (gemAura == null) return;

        gemAura.Stop();
        // gemOreType은 다음 Initialize 전까지 바뀌지 않으므로, 꺼낸 풀로 정확히 되돌아간다.
        auraProvider?.ReleaseAura(gemOreType, gemAura);
        gemAura = null;
    }

    /// <summary>
    /// 스프라이트 셰이더의 반짝임(_ShinyEnabled)을 켜고 끈다.
    /// 원석은 전부 보석이므로 원목과 달리 등급 조건이 따로 없다 - 바닥에 놓여 있는 동안 항상 켜진다.
    /// </summary>
    private void SetShaderShiny(bool _enable)
    {
        if (spriteRenderer == null) return;

        bool shinyEnabled = _enable && gemOreType != GemOreType.None;

        if (mpb == null) mpb = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ShinyEnabledPropertyID, shinyEnabled ? 1f : 0f);
        spriteRenderer.SetPropertyBlock(mpb);

        if (outlineStencilSR != null) outlineStencilSR.SetPropertyBlock(null);
    }

    private void ApplyOutlineColor()
    {
        if (outlineSR == null) return;

        if (mpb == null) mpb = new MaterialPropertyBlock();
        outlineSR.GetPropertyBlock(mpb);
        mpb.SetColor(OutlineColorPropertyID, normalOutlineColor);
        outlineSR.SetPropertyBlock(mpb);
    }
}
