using System;
using System.Collections.Generic;
using UnityEngine;

public class LogItem : Item, IStaticCollidable
{
    // 이벤트
    public event Action<LogItem> LogItemActivatedEvent;
    public event Action<LogItem> LogItemDeActivatedEvent;
    public event Action<LogItem> LogItemAcquired;
    // 던전/마을 이동 시 인벤토리를 강제로 버릴 때(DropAllItem) 처럼, 착지하지 않고 공중에서
    // 페이드아웃되어 사라지는 연출이 끝났을 때 발생한다.
    public event Action<LogItem> LogItemVanishedEvent;

    // IStaticCollidable 구현
    public Vector2 Position => transform.position;
    public Vector2 Offset => Vector2.zero;
    public float Radius => 0.1f;
    public int Layer => gameObject.layer;

    // 내부 의존성
    public LogState logState { get; private set; }
    public TreeType treeType { get; private set; }



    public SpriteRenderer spriteRenderer;
    private Transform visualTransform;

    // 상태 변수
    private ItemMoveState state = ItemMoveState.None;
    public ItemMoveState MoveState => state;
    public bool IsMoving => state != ItemMoveState.Dropped && state != ItemMoveState.None;
    private Transform suckTarget;
    private Transform dynamicTarget;
    private bool bDrop = true;
    public float durability = 0f;

    private IInventoryChecker inventoryChecker;
    // 특정 대상(NPC 등)이 흡입을 시도할 때, 전역 inventoryChecker 대신 이 값을 우선 사용한다
    private IInventoryChecker suckerChecker;
    private IItemAcquirer customAcquirer;
    public IItemAcquirer CustomAcquirer => customAcquirer;

    // 이동 관련 변수 (캐싱)
    private Vector3 startPos;
    private Vector3 endPos;
    private Vector3 trajectoryJitter;
    private Vector3 sideDir; // 곡선 방향 (기울기에 수직)
    private float height;
    private float duration;
    private float elapsed;
    private float rotationSpeed;
    private float totalRotation;
    private float suckSpeed;
    private const float SuckAccel = 16f;
    private const float MinAcquireDist = 0.2f;

    private Sprite timberSprite;

    // 커터를 통과해 제재목으로 가공된 상태인지. 세이브/로드로 외형을 복원하는 데 쓴다.
    // (기존에는 스프라이트만 바꾸고 상태를 남기지 않아, 로드하면 원목 모습으로 되돌아갔다)
    private bool bIsTimber = false;
    public bool BIsTimber => bIsTimber;

    // 관리용 인덱스
    public int PoolIndex { get; set; } = -1;
    public int UpdateIndex { get; set; } = -1;

    // 쫀득한 착지 연출용 변수
    private float landingDampTime = 0.5f;
    private const float landingDampDuration = 0.5f;

    private bool bSuckAccelerating = false; // 튕김이 끝나고 흡입이 시작됨을 감지하는 플래그

    bool bCanAcquired = true;

    private Material originalMaterial;

    private MaterialPropertyBlock mpb;

    private CustomSortable customSortable;

    private bool bDisableCustomSortable = false;
    private float originalDurability;
    private float inventoryCheckTimer = 0f;
    private static readonly int UseFloatingPropertyID = Shader.PropertyToID("_UseFloating");
    private static readonly int FloatingOffsetPropertyID = Shader.PropertyToID("_FloatingOffset");
    private static readonly int ShinyEnabledPropertyID = Shader.PropertyToID("_ShinyEnabled");
    private static readonly int ShadowFrameRect0PropertyID = Shader.PropertyToID("_ShadowFrameRect0");
    private static readonly int ShadowFrameRect1PropertyID = Shader.PropertyToID("_ShadowFrameRect1");
    private static readonly int ShadowFrameRect2PropertyID = Shader.PropertyToID("_ShadowFrameRect2");
    private static readonly int ShadowFrameRect3PropertyID = Shader.PropertyToID("_ShadowFrameRect3");
    private static readonly int ShadowHeightPixelsPropertyID = Shader.PropertyToID("_ShadowHeightPixels");
    private static readonly int ShadowLocalOffsetPropertyID = Shader.PropertyToID("_ShadowLocalOffset");
    // 그림자 프레임 Rect/오프셋은 같은 머티리얼을 쓰는 모든 LogItem에 동일하므로, 머티리얼당 1회만 구워두면 된다.
    private static readonly HashSet<Material> initializedShadowMaterials = new HashSet<Material>();

    [SerializeField] private GameObject shadow;
    private Transform shadowTransform;
    private SpriteRenderer shadowRenderer;

    [Header("Shadow Sprites By Height Position")]
    [Tooltip("원목이 -1 위치에 있을 때(바닥 아래로 1픽셀 내려갔을 때)")]
    [SerializeField] private Sprite shadowSprite_Minus1;
    [Tooltip("원목이 0 위치에 있을 때(기본 착지 위치)")]
    [SerializeField] private Sprite shadowSprite_0;
    [Tooltip("원목이 1 위치에 있을 때(바닥 위로 1픽셀 올라갔을 때)")]
    [SerializeField] private Sprite shadowSprite_1;
    [Tooltip("원목이 2 위치 이상에 있을 때(포물선 비행 중)")]
    [SerializeField] private Sprite shadowSprite_2Plus;
    [Tooltip("포물선 비행 중 그림자 프레임 전환 폭(값이 클수록 정점 부근에서 3번 프레임을 더 오래 유지)")]
    [SerializeField] private float shadowFlightPixelScale = 3f;

    // 착지하지 않고 공중에서 서서히 사라지는 연출(버려진 아이템 등) 관련 상태
    private bool bFadeAndVanish = false;
    private const float VanishFadeStartT = 0.35f; // 이 시점(t) 이후부터 착지 시점(t=1)까지 알파를 1->0으로 서서히 감소

    private Color originalColor;
    private Color originalOutlineColor;
    private Color originalShadowColor;

    private string flyingItemSortingLayerName = "FlyingItem";
    private string objectsSortingLayerName = "Objects";
    private static int objectsSortingLayerID = -1;
    private static int flyingItemSortingLayerID = -1;

    [SerializeField] private GameObject outlineObj;
    [SerializeField] private SpriteRenderer outlineStencilSR;
    [SerializeField] private SpriteRenderer outlineSR;

    [Header("Outline Color")]
    [SerializeField] private Color normalOutlineColor = Color.white;
    private static readonly int OutlineColorPropertyID = Shader.PropertyToID("_OutlineColor");

    private ICharacter character;

    private ParticleSystem particleEffect;
    private VFXComponent vfxComponent;

    // 보석 등급 원목에 붙는 아우라. 소유자(LogItemController)가 풀로 관리하며 여기서는 빌려 쓴다.
    private ILogItemAuraProvider auraProvider;
    private ItemAuraEffectController gemAura;

    //private string objectSortingLayerName = "Objects";

    public void Initialize(LogItemTypeData _logItemTypeData, Color _color, LogState _logState, ICharacter _character, bool _bDisableCustomSortable = false)
    {
        base.Initialize(_logItemTypeData.itemType);

        character = _character;
        bDisableCustomSortable = _bDisableCustomSortable;
        logState = _logState;
        ApplyOutlineColor();
        treeType = _logItemTypeData.treeType;
        state = ItemMoveState.None;
        suckTarget = null;
        suckerChecker = null;
        customAcquirer = null;
        dynamicTarget = null;
        // 원목 상태(황금/다이아/무지개)에 따라 다른 스프라이트로 드랍된다.
        sprite = _logItemTypeData.GetSprite(_logState);
        durability = _logItemTypeData.durability;
        originalDurability = durability;
        elapsed = 0;
        // 제재목도 원목 상태(황금/다이아/무지개)에 따라 다른 스프라이트를 쓴다.
        timberSprite = _logItemTypeData.GetTimberSprite(_logState);
        landingDampTime = landingDampDuration;
        color = _color;

        // 최적화: GetComponentInChildren 캐싱
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                visualTransform = spriteRenderer.transform;
            }
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
            if (outlineStencilSR != null)
                outlineStencilSR.sprite = sprite;
            if (outlineSR != null)
                outlineSR.sprite = sprite;
        }

        if (shadow != null && shadowTransform == null)
        {
            shadowTransform = shadow.transform;
            shadowRenderer = shadow.GetComponentInChildren<SpriteRenderer>();
        }

        InitializeShadowFrames();

        if (objectsSortingLayerID == -1)
        {
            objectsSortingLayerID = SortingLayer.NameToID(objectsSortingLayerName);
        }

        if (flyingItemSortingLayerID == -1)
        {
            flyingItemSortingLayerID = SortingLayer.NameToID(flyingItemSortingLayerName);
        }

        transform.localScale = Vector3.one;
        originalMaterial = spriteRenderer.sharedMaterial;

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

        originalColor = spriteRenderer.color;
        originalOutlineColor = outlineSR != null ? outlineSR.color : Color.white;
        originalShadowColor = shadowRenderer != null ? shadowRenderer.color : Color.white;
    }

    public void SetVfxComponent(VFXComponent _vfxComponent)
    {
        vfxComponent = _vfxComponent;
    }

    public void SetAuraProvider(ILogItemAuraProvider _auraProvider)
    {
        auraProvider = _auraProvider;
    }

    /// <summary>
    /// Fascinating 등급 이상 원목의 반짝임 파티클을 재생한다.
    /// 던전 착지뿐 아니라 마을 벨트에 안착했을 때(제재목 포함)도 같은 이펙트를 쓴다.
    /// </summary>
    /// <summary>
    /// 외부(벨트 등)에서 원목을 직접 배치했을 때 반짝임을 붙인다.
    /// 벨트는 Launch/Transfer를 쓰지 않고 위치만 옮기므로, 자동 재생 지점을 타지 않는다.
    /// </summary>
    public void PlayGemShiny()
    {
        PlayShinyEffect();
    }

    private void PlayShinyEffect()
    {
        if (vfxComponent == null || logState <= LogState.Normal) return;

        // 이전 재생분이 남아 있으면 정리하고 새로 붙인다(전송 -> 안착 경로에서 중복 부착 방지).
        if (particleEffect != null)
        {
            vfxComponent.Stop(particleEffect, false);
            particleEffect = null;
        }

        particleEffect = vfxComponent.Play("Shiny", transform.position, transform.rotation, transform);
        if (particleEffect == null) return;

        particleEffect.transform.localScale = Vector3.one;
        SyncParticleSorting();
    }

    /// <summary>
    /// 반짝임 파티클을 원목 본체 바로 앞에 그린다.
    /// 레이어를 "Objects"로 고정하지 않고 본체를 따라가게 해야, 벨트/컨테이너로 옮겨지며
    /// FlyingItem 레이어로 전환됐을 때도 파티클이 함께 따라간다.
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
    /// 보석 등급(Normal 초과) 원목이 땅에 놓였을 때 등급별 아우라를 붙인다.
    /// Shiny 파티클과 함께 재생되며, 흡입이 시작되거나 풀로 반환될 때 회수된다.
    /// </summary>
    private void PlayGemAura()
    {
        // 이미 붙어 있으면 중복 부착하지 않는다(착지 -> 흡입취소 -> 재착지 경로에서 새는 것을 막는다).
        if (auraProvider == null || gemAura != null) return;
        if (logState <= LogState.Normal) return;

        gemAura = auraProvider.GetAura(logState);
        if (gemAura == null) return;

        Transform auraTransform = gemAura.transform;
        auraTransform.SetParent(transform, false);
        auraTransform.localPosition = Vector3.zero;

        SyncGemAura();
        gemAura.Play();
    }

    /// <summary>
    /// 아우라를 원목 그림에 맞춰 따라가게 한다. 매 프레임 호출된다.
    ///
    /// 위치: 본체 transform은 지면 좌표를 선형 보간할 뿐이고(그림자가 그 자리에 있다),
    /// 포물선 높이는 visualTransform.localPosition이 들고 있다. 아우라가 원목과 함께
    /// 날아가려면 그 높이를 따라가야 한다. outlineObj도 같은 방식으로 맞춘다.
    /// visualTransform에 직접 붙이지 않는 이유는 착지 스쿼시(비균등 스케일)까지 상속받아
    /// 방사형 아우라가 타원으로 찌그러지기 때문이다.
    ///
    /// 정렬: 프리셋 프리팹은 Default 레이어(맨 뒤)라 맞추지 않으면 가려지고,
    /// sortingOrder는 CustomSortable이 위치에 따라 매 프레임 다시 계산하므로 한 번만 설정하면 어긋난다.
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
        // 원목 뒤에 그린다. 원목의 렌더러들이 order와 order-1(아웃라인 스텐실)을 이미 쓰고 있어,
        // -1로 두면 스텐실과 순서가 같아져 그리기 순서가 불안정해지므로 한 칸 더 뒤로 뺀다.
        gemAura.SetSortingOrder(spriteRenderer.sortingOrder - 2);
    }

    /// <summary>
    /// 풀로 반환될 때 빌려온 아우라만 즉시 돌려준다.
    /// ResetItem은 다음 획득 시점에 호출되므로, 그대로 두면 원목이 풀에서 쉬는 동안에도
    /// 아우라가 대여 상태로 묶여 실제 동시 사용량보다 풀이 크게 잡힌다.
    /// 아우라 외의 상태는 일절 건드리지 않는다.
    /// </summary>
    public void ReleaseGemAura()
    {
        StopGemAura();
    }

    private void StopGemAura()
    {
        if (gemAura == null) return;

        gemAura.Stop();
        // logState는 다음 Initialize 전까지 바뀌지 않으므로, 꺼낸 풀로 정확히 되돌아간다.
        auraProvider?.ReleaseAura(logState, gemAura);
        gemAura = null;
    }

    public void SetParticleEffect(ParticleSystem _particleEffect)
    {
        particleEffect = _particleEffect;
    }

    public void SetInventoryChecker(IInventoryChecker _inventoryChecker)
    {
        inventoryChecker = _inventoryChecker;
    }

    public void IsDropItem(bool _boolean)
    {
        bDrop = _boolean;
    }

    // 활성화하면 다음 Launch()의 포물선 비행 도중 알파가 서서히 0으로 줄어들어, 착지 전에
    // 사라진 채로 비행을 마친다(= 땅에 떨어져 보이지 않고 공중에서 소멸).
    public void SetFadeAndVanish(bool _boolean)
    {
        bFadeAndVanish = _boolean;
    }

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

        trajectoryJitter = Vector3.zero;
        rotationSpeed = 0f;
        totalRotation = _totalRotation;
        elapsed = 0f;
        state = ItemMoveState.Launching;
        LogItemActivatedEvent?.Invoke(this);
        transform.localScale = Vector3.zero;

        // 공중에서 페이드아웃되어 사라지는 연출(버려진 아이템)에서는 아웃라인을 켜지 않는다
        if (outlineObj != null && !bFadeAndVanish)
        {
            outlineObj.SetActive(true);
        }

        if (outlineObj != null && visualTransform != null)
        {
            outlineObj.transform.localPosition = visualTransform.localPosition;
            outlineObj.transform.localRotation = visualTransform.localRotation;
            outlineObj.transform.localScale = visualTransform.localScale;
        }

        if (mpb == null) mpb = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(mpb);
        spriteRenderer.SetPropertyBlock(mpb);

        // 활성화 상태라면 등록 (OnEnable에서도 처리됨)
        if (gameObject.activeInHierarchy)
        {
            CollisionSystem.Instance?.Register(this, false);
        }

        // 보석 등급 아우라는 착지가 아니라 생성(발사) 시점부터 붙어, 포물선 비행 내내 보인다.
        PlayGemAura();
    }

    public void TransferLaunch(Vector3 _start, Vector3 _end, float _height, float _duration, Vector3 _jitter, float _rotationSpeed = 0f)
    {
        startPos = _start;
        endPos = _end;
        height = _height;
        duration = _duration;
        trajectoryJitter = _jitter;
        rotationSpeed = _rotationSpeed;
        elapsed = 0f;
        state = ItemMoveState.Transferring;
        LogItemActivatedEvent?.Invoke(this);
        transform.localScale = Vector3.zero;

        if (gameObject.activeInHierarchy)
        {
            CollisionSystem.Instance?.Register(this, false);
        }
    }

    public void ContainerTransferLaunch(Vector3 _start, Vector3 _end, float _height, float _duration, Vector3 _jitter, float _rotationSpeed = 0f)
    {
        startPos = _start;
        endPos = _end;
        height = _height;
        duration = _duration;
        trajectoryJitter = _jitter;
        rotationSpeed = _rotationSpeed;
        elapsed = 0f;
        state = ItemMoveState.ContainerTransferring;
        LogItemActivatedEvent?.Invoke(this);
        transform.localScale = Vector3.zero;

        if (gameObject.activeInHierarchy)
        {
            CollisionSystem.Instance?.Register(this, false);
        }
    }

    public void DynamicTransferLaunch(Vector3 _start, Transform _target, float _height, float _duration, Vector3 _jitter, float _rotationSpeed = 0f)
    {
        startPos = _start;
        dynamicTarget = _target;
        endPos = _target != null ? _target.position : _start;
        height = _height;
        duration = _duration;
        trajectoryJitter = _jitter;
        rotationSpeed = _rotationSpeed;
        elapsed = 0f;
        state = ItemMoveState.DynamicTransferring;
        LogItemActivatedEvent?.Invoke(this);
        transform.localScale = Vector3.zero;

        if (gameObject.activeInHierarchy)
        {
            CollisionSystem.Instance?.Register(this, false);
        }
    }

    public void CurveTransferLaunch(Vector3 _start, Vector3 _end, float _height, float _duration, float _rotationSpeed = 0f)
    {
        startPos = _start;
        endPos = _end;
        height = _height;
        duration = _duration;
        rotationSpeed = _rotationSpeed;
        elapsed = 0f;
        state = ItemMoveState.CurveTransferring;
        LogItemActivatedEvent?.Invoke(this);
        transform.localScale = Vector3.zero;

        // 시점과 종점을 잇는 방향에 수직인 벡터 계산 (2D 법선)
        Vector3 dir = (endPos - startPos).normalized;
        sideDir = new Vector3(-dir.y, dir.x, 0f);

        if (gameObject.activeInHierarchy)
        {
            CollisionSystem.Instance?.Register(this, false);
        }
    }
    private void OnEnable()
    {
        // Launch나 TransferLaunch가 이미 호출된 상태에서 활성화될 때만 등록
        if (state != ItemMoveState.None)
        {
            CollisionSystem.Instance?.Register(this, false);
        }
    }

    private void OnDisable()
    {
        CollisionSystem.Instance?.Unregister(this, false);
    }

    public override void ResetItem()
    {
        base.ResetItem();

        if(particleEffect != null)
        {
            if(vfxComponent != null && particleEffect.transform.IsChildOf(transform))
                vfxComponent.Stop(particleEffect, true);
            particleEffect = null;    
        }

        StopGemAura();

        state = ItemMoveState.None;
        suckTarget = null;
        suckerChecker = null;
        customAcquirer = null;
        dynamicTarget = null;
        elapsed = 0;
        trajectoryJitter = Vector3.zero;
        sideDir = Vector3.zero;
        rotationSpeed = 0f;
        totalRotation = 0f;
        bCanAcquired = true;
        bFadeAndVanish = false;
        // 풀에서 재사용될 때 이전 소유자(예: DropAllItem)가 남긴 구독이 새 사용처로 잘못 넘어가지 않도록 초기화
        LogItemVanishedEvent = null;
        transform.localScale = Vector3.one;
        landingDampTime = landingDampDuration;
        durability = originalDurability;
        inventoryCheckTimer = 0.15f; // 스폰 시 즉시 검사하도록 설정

        if (outlineObj != null)
            outlineObj.SetActive(false);

        if (outlineSR != null)
        {
            outlineSR.SetPropertyBlock(null);
            outlineSR.color = originalOutlineColor; // FadeAndVanish 연출로 줄어든 알파 복구
        }

        if (shadowRenderer != null)
        {
            shadowRenderer.color = originalShadowColor; // FadeAndVanish 연출로 줄어든 알파 복구
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
            spriteRenderer.SetPropertyBlock(null);
            spriteRenderer.sortingLayerID = objectsSortingLayerID;
        }

        // 풀 재사용 시 원목 스프라이트로 되돌아가므로 가공 상태도 함께 해제한다.
        bIsTimber = false;

        if (sprite != null && spriteRenderer != null)
            spriteRenderer.sprite = sprite;

        if (visualTransform != null)
        {
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = Vector3.one;
        }

        if (customSortable != null)
        {
            customSortable.SetHeight(0f);
        }

        if (shadowTransform != null)
        {
            shadowTransform.localScale = Vector3.one;
        }

        SetShaderFloating(false);
    }

    // 제재목 스프라이트는 이미 색이 입혀진 그림이라 색을 따로 입히지 않는다.
    // (렌더러 색은 ResetItem이 복원해 둔 originalColor = 프리팹의 흰색을 그대로 쓴다)
    public void SetTimberSprite()
    {
        bIsTimber = true;

        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = timberSprite;
        }
    }

    public void ManualUpdate(float _deltaTime)
    {
        switch (state)
        {
            case ItemMoveState.Launching:
                UpdateLaunching(_deltaTime);
                break;
            case ItemMoveState.Transferring:
                UpdateTransferring(_deltaTime);
                break;
            case ItemMoveState.ContainerTransferring:
                UpdateContainerTransferring(_deltaTime);
                break;
            case ItemMoveState.DynamicTransferring:
                UpdateDynamicTransferring(_deltaTime);
                break;
            case ItemMoveState.Sucking:
                UpdateSucking(_deltaTime);
                break;
            case ItemMoveState.Dropped:
                UpdateDropped(_deltaTime);
                break;
        }

        if (bDisableCustomSortable == false)
        {
            customSortable.ManualLateUpdate();
            if (outlineStencilSR != null && outlineSR != null)
                outlineStencilSR.sortingOrder = outlineSR.sortingOrder - 1;
        }

        SyncGemAura();

        SyncParticleSorting();
    }

    public void UpdateSortingOrder()
    {
        if (bDisableCustomSortable == false)
        {
            customSortable.ManualLateUpdate();
            if (outlineStencilSR != null && outlineSR != null)
                outlineStencilSR.sortingOrder = outlineSR.sortingOrder - 1;
        }

        SyncGemAura();
        SyncParticleSorting();
    }

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
            // t=0.5(정점)에서 추가 스케일이 0이 되고, 시작과 끝에서 최대가 됨
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

        // 4. 전체 Scale 팝업 (0.4까지 BackEaseOut 효과로 탄력 있게 커짐)
        float targetScale = 1f;
        if (t < 0.8f)
        {
            float nt = t / 0.8f;
            const float s = 2.5f; // 약간 더 과장된 탄성 계수
            float t1 = nt - 1f;
            targetScale = Mathf.Max(0, (t1 * t1 * ((s + 1f) * t1 + s) + 1f));
        }
        transform.localScale = Vector3.one * targetScale;

        // 착지 전에 서서히 사라지는 연출: VanishFadeStartT 시점부터 착지(t=1)까지 알파를 1->0으로 선형 감소
        if (bFadeAndVanish)
        {
            float fadeT = t <= VanishFadeStartT ? 0f : (t - VanishFadeStartT) / (1f - VanishFadeStartT);
            ApplyLaunchFadeAlpha(1f - fadeT);
        }

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

            // 착지 시점(t=1)에는 이미 완전히 투명해진 상태이므로, 일반 착지 처리(줍기 판정/샤이니 VFX 등) 없이
            // 바로 소멸을 알리고 종료한다.
            if (bFadeAndVanish)
            {
                ApplyLaunchFadeAlpha(0f);
                state = ItemMoveState.None;
                LogItemVanishedEvent?.Invoke(this);
                return;
            }

            landingDampTime = 0f;

            state = ItemMoveState.Dropped;
            SetShaderFloating(true);
            if (vfxComponent != null && logState > LogState.Normal)
            {
                particleEffect = vfxComponent.Play("Shiny", transform.position, transform.rotation, transform);
                if (particleEffect != null) particleEffect.transform.localScale = Vector3.one;
            }
            CheckAcquireCondition();
        }
    }

    private void ApplyLaunchFadeAlpha(float _alpha)
    {
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            c.a = _alpha;
            spriteRenderer.color = c;
        }

        if (outlineSR != null)
        {
            Color c = outlineSR.color;
            c.a = _alpha;
            outlineSR.color = c;
        }

        if (shadowRenderer != null)
        {
            Color c = shadowRenderer.color;
            c.a = _alpha;
            shadowRenderer.color = c;
        }
    }

    private void UpdateTransferring(float _deltaTime)
    {
        float currentT = duration > 0 ? (elapsed / duration) : 1f;
        float speedMultiplier = 1f;

        if (currentT > 0.7f)
        {
            // 0.7f부터 가속도 적용 (도착할수록 속도 배율 증가)
            speedMultiplier = 1f + (currentT - 0.7f) * 15f;
        }

        elapsed += _deltaTime * speedMultiplier;
        float t = Mathf.Clamp01(elapsed / duration);

        // 시점과 종점은 jitter가 0이고 중간에서 최대가 되도록 (Parabolic factor: 4 * t * (1-t))
        float jitterFactor = 4f * t * (1f - t);
        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, t) + (trajectoryJitter * jitterFactor);

        float heightOffset = -4 * height * (t - 0.5f) * (t - 0.5f) + height;

        if (visualTransform != null)
        {
            transform.position = currentGroundPos;
            visualTransform.localPosition = new Vector3(0, heightOffset, 0);
            visualTransform.Rotate(Vector3.forward, rotationSpeed * _deltaTime);

            if (customSortable != null)
            {
                customSortable.SetHeight(heightOffset);
            }
        }
        else
        {
            transform.position = currentGroundPos + new Vector3(0, heightOffset, 0);
        }

        UpdateShadowScale(heightOffset);

        // Scale 연출 (0.4까지 스프링 댐퍼(Overshoot) 효과로 커지고, 0.7부터 작아짐)
        float targetScale = 1f;
        if (t < 0.4f)
        {
            float nt = t / 0.4f;
            const float s = 1.70158f; // BackEaseOut 탄성 계수
            float t1 = nt - 1f;
            // (t-1)^2 * ((s+1)(t-1) + s) + 1 공식 적용
            targetScale = Mathf.Max(0, (t1 * t1 * ((s + 1f) * t1 + s) + 1f));
        }
        else if (t > 0.7f)
        {
            float nt = (t - 0.7f) / 0.3f;
            targetScale = 1f - nt;
        }

        transform.localScale = Vector3.one * targetScale;

        CollisionSystem.Instance?.UpdatePosition(this, transform.position);

        if (t >= 1.0f)
        {
            transform.position = GlobalPixelSnapper.Snap(endPos);
            if (visualTransform != null) visualTransform.localPosition = Vector3.zero;

            visualTransform.rotation = Quaternion.identity;

            UpdateShadowScale(0f);

            state = ItemMoveState.Dropped;
            SetShaderFloating(true);

            // 벨트/컨테이너에 안착했을 때도 보석 등급이면 반짝임을 붙인다(제재목 포함).
            PlayShinyEffect();
        }
    }

    private void UpdateContainerTransferring(float _deltaTime)
    {
        float currentT = duration > 0 ? (elapsed / duration) : 1f;
        float speedMultiplier = 1f;

        if (currentT > 0.7f)
        {
            // 오프로드 컨테이너 전용: 가속도를 대폭 완화 (15f -> 3f)하여 끊김 현상 방지
            speedMultiplier = 1f + (currentT - 0.7f) * 3f;
        }

        elapsed += _deltaTime * speedMultiplier;
        float t = Mathf.Clamp01(elapsed / duration);

        float jitterFactor = 4f * t * (1f - t);
        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, t) + (trajectoryJitter * jitterFactor);

        float heightOffset = -4 * height * (t - 0.5f) * (t - 0.5f) + height;

        if (visualTransform != null)
        {
            transform.position = currentGroundPos;
            visualTransform.localPosition = new Vector3(0, heightOffset, 0);
            visualTransform.Rotate(Vector3.forward, rotationSpeed * _deltaTime);

            if (customSortable != null)
            {
                customSortable.SetHeight(heightOffset);
            }
        }
        else
        {
            transform.position = currentGroundPos + new Vector3(0, heightOffset, 0);
        }

        UpdateShadowScale(heightOffset);

        // Scale 연출 동일하게 적용
        float targetScale = 1f;
        if (t < 0.4f)
        {
            float nt = t / 0.4f;
            const float s = 1.70158f;
            float t1 = nt - 1f;
            targetScale = Mathf.Max(0, (t1 * t1 * ((s + 1f) * t1 + s) + 1f));
        }
        else if (t > 0.7f)
        {
            float nt = (t - 0.7f) / 0.3f;
            targetScale = 1f - nt;
        }

        transform.localScale = Vector3.one * targetScale;

        CollisionSystem.Instance?.UpdatePosition(this, transform.position);

        if (t >= 1.0f)
        {
            transform.position = GlobalPixelSnapper.Snap(endPos);
            if (visualTransform != null) visualTransform.localPosition = Vector3.zero;
            visualTransform.rotation = Quaternion.identity;

            UpdateShadowScale(0f);

            state = ItemMoveState.Dropped;
            SetShaderFloating(true);

            // 벨트/컨테이너에 안착했을 때도 보석 등급이면 반짝임을 붙인다(제재목 포함).
            PlayShinyEffect();
        }
    }

    private void UpdateDynamicTransferring(float _deltaTime)
    {
        if (dynamicTarget == null)
        {
            state = ItemMoveState.Dropped;
            
            if (vfxComponent != null && particleEffect != null)
            {
                vfxComponent.Stop(particleEffect, false);
            }
            return;
        }

        float currentT = duration > 0 ? (elapsed / duration) : 1f;
        float speedMultiplier = 1f;

        if (currentT > 0.7f)
        {
            speedMultiplier = 1f + (currentT - 0.7f) * 3f;
        }

        elapsed += _deltaTime * speedMultiplier;
        float t = Mathf.Clamp01(elapsed / duration);

        // 타겟 위치로 계속 업데이트
        endPos = dynamicTarget.position;

        float jitterFactor = 4f * t * (1f - t);
        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, t) + (trajectoryJitter * jitterFactor);

        float heightOffset = -4 * height * (t - 0.5f) * (t - 0.5f) + height;

        if (visualTransform != null)
        {
            transform.position = currentGroundPos;
            visualTransform.localPosition = new Vector3(0, heightOffset, 0);
            visualTransform.Rotate(Vector3.forward, rotationSpeed * _deltaTime);

            if (customSortable != null)
            {
                customSortable.SetHeight(heightOffset);
            }
        }
        else
        {
            transform.position = currentGroundPos + new Vector3(0, heightOffset, 0);
        }

        UpdateShadowScale(heightOffset);

        // Scale 연출 동일하게 적용
        float targetScale = 1f;
        if (t < 0.4f)
        {
            float nt = t / 0.4f;
            const float s = 1.70158f;
            float t1 = nt - 1f;
            targetScale = Mathf.Max(0, (t1 * t1 * ((s + 1f) * t1 + s) + 1f));
        }
        else if (t > 0.7f)
        {
            float nt = (t - 0.7f) / 0.3f;
            targetScale = 1f - nt;
        }

        transform.localScale = Vector3.one * targetScale;

        CollisionSystem.Instance?.UpdatePosition(this, transform.position);

        if (t >= 1.0f)
        {
            transform.position = GlobalPixelSnapper.Snap(endPos);
            if (visualTransform != null) visualTransform.localPosition = Vector3.zero;
            visualTransform.rotation = Quaternion.identity;

            UpdateShadowScale(0f);

            state = ItemMoveState.Dropped;
            SetShaderFloating(true);

            // 벨트/컨테이너에 안착했을 때도 보석 등급이면 반짝임을 붙인다(제재목 포함).
            PlayShinyEffect();
        }
    }

    private void UpdateSucking(float _deltaTime)
    {
        if (suckTarget == null || (character != null && character.bDead))
        {
            suckTarget = null;
            transform.localScale = Vector3.one;
            if (visualTransform != null) visualTransform.localScale = Vector3.one;
            state = ItemMoveState.Dropped;

            if (vfxComponent != null && logState > LogState.Normal)
            {
                particleEffect = vfxComponent.Play("Shiny", transform.position, transform.rotation, transform);
                if (particleEffect != null) particleEffect.transform.localScale = Vector3.one;
            }

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
                LogItemAcquired?.Invoke(this);

                return;
            }
        }

        // 도착 조건: 거리가 가깝고 타겟을 향해 이동 중일 때
        if (suckSpeed > 0f && sqrDistance < (MinAcquireDist * MinAcquireDist))
        {
            LogItemAcquired?.Invoke(this);

            return;
        }

        // 가속도 계산: 튕김 구간과 흡수 구간 분리 및 탄력적 가속 적용
        if (suckSpeed < 0f)
        {
            // 뒤로 튕기는 구간 (기존 가속 30f에서 24f로 완화하여 튕김 모션 확보)
            suckSpeed += (SuckAccel * 2.0f) * _deltaTime;
        }
        else
        {
            // 튕김이 끝나고 본격적으로 전진하기 시작하는 첫 프레임 감지
            if (!bSuckAccelerating)
            {
                bSuckAccelerating = true;
                elapsed = 0f; // 스프링 댐핑 애니메이션 리셋
            }

            // 끌려갈 때 속도가 빨라질수록 가속도도 기하급수적으로 증가하는 탄성 가속
            float dynamicAccel = SuckAccel * 2.5f * (1f + suckSpeed * 0.15f);
            suckSpeed += dynamicAccel * _deltaTime;

            // 가속도 폭발 방지 (최대 속도 제한)
            suckSpeed = Mathf.Min(suckSpeed, 35f);
        }

        // 타겟 방향으로 부드럽게 이동
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

        // 타겟에 매우 가까워지면 전체 스케일 축소 (최소 0.25 유지)
        if (suckSpeed > 0f && sqrDistance < (1.5f * 1.5f))
        {
            float distance = Mathf.Sqrt(sqrDistance);
            float scaleT = distance / 1.5f;
            scaleT = Mathf.Max(0.35f, scaleT);
            transform.localScale = Vector3.one * scaleT;
        }

        CollisionSystem.Instance?.UpdatePosition(this, transform.position);
    }

    private void UpdateDropped(float _deltaTime)
    {
        if (visualTransform != null)
        {
            // 셰이더 연동용 기본값 설정 (CPU 연산 없음)
            visualTransform.localPosition = Vector3.zero;
            if (customSortable != null)
            {
                customSortable.SetHeight(0f);
            }
            UpdateShadowScale(0f);

            // 착지 후 스프링 댐퍼 쫀득한 Scale 연출
            if (landingDampTime < landingDampDuration)
            {
                landingDampTime += _deltaTime;

                // 스프링 댐퍼 감쇠 코사인파 (착지 시 찌그러진 상태에서 시작하여 진동 감쇠)
                float freq = 25f; // 진동 속도
                float decay = 7f; // 감쇠율
                float amp = 0.4f; // 최초 충격 변형량

                float springEffect = Mathf.Cos(landingDampTime * freq) * Mathf.Exp(-landingDampTime * decay) * amp;

                // Squash & Stretch: 수평(X)은 늘어나고 수직(Y)은 찌그러짐
                visualTransform.localScale = new Vector3(1f + springEffect, 1f - springEffect, 1f);

                if (landingDampTime >= landingDampDuration)
                {
                    visualTransform.localScale = Vector3.one;
                    LogItemDeActivatedEvent?.Invoke(this);
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

        if (!bDrop || suckTarget == null) return;

        inventoryCheckTimer += _deltaTime;
        if (inventoryCheckTimer >= 0.15f)
        {
            inventoryCheckTimer = 0f;
            CheckAcquireCondition();
        }
    }

    private void CheckAcquireCondition()
    {
        IInventoryChecker checker = suckerChecker ?? inventoryChecker;
        if (suckTarget != null && checker != null && checker.CanAcquired(this) && bCanAcquired == true)
        {
            StartSucking(suckTarget);

            return;
        }

        suckTarget = null;
    }

    public override void SetSuckTarget(Transform _target)
    {
        if (state != ItemMoveState.Dropped || !bDrop || bCanAcquired == false) return;

        suckTarget = _target;
        // 특정 소비자 지정 없이 호출된 경우(플레이어 경로) 이전에 남아있을 수 있는
        // NPC용 checker/acquirer를 지워 전역 inventoryChecker/이벤트 체인으로 되돌린다
        suckerChecker = null;
        customAcquirer = null;

        if (state == ItemMoveState.Dropped)
        {
            CheckAcquireCondition();
        }
    }

    /// <summary>
    /// 특정 소비자(NPC 등)를 지정해 흡입을 시도합니다. 전역 inventoryChecker/이벤트 체인을 타지 않고
    /// 지정된 checker/acquirer로만 습득 여부를 판단하고 귀속시킵니다.
    /// </summary>
    public void SetSuckTarget(Transform _target, IInventoryChecker _checker, IItemAcquirer _acquirer)
    {
        if (state != ItemMoveState.Dropped || !bDrop || bCanAcquired == false) return;

        suckTarget = _target;
        suckerChecker = _checker;
        customAcquirer = _acquirer;

        if (state == ItemMoveState.Dropped)
        {
            CheckAcquireCondition();
        }
    }

    private void StartSucking(Transform _target)
    {
        if (vfxComponent != null && particleEffect != null)
        {
            vfxComponent.Stop(particleEffect, false);
        }

        StopGemAura();

        suckTarget = _target;
        suckSpeed = -5.0f; // 뒤로 튕기는 동작을 더 크게 하기 위해 초기 음수 속도 상향
        elapsed = 0f;
        bSuckAccelerating = false; // 플래그 초기화
        state = ItemMoveState.Sucking;
        SetShaderFloating(false);
        LogItemActivatedEvent?.Invoke(this);
    }

    public void SetbCanAcquired(bool _boolean)
    {
        bCanAcquired = _boolean;
    }

    private void UpdateShadowScale(float _heightOffset)
    {
        if (shadowTransform == null) return;

        float shadowScale = Mathf.Max(0.3f, 1f - (_heightOffset * 0.25f));
        shadowTransform.localScale = new Vector3(shadowScale, shadowScale, 1f);

        // Dropped 상태에서는 그림자의 높이-프레임 판정(둥둥 뜨는 움직임 포함)이 Shadow_LogItem 셰이더 내부에서
        // 전부 처리되므로(CPU 연산 없음), 여기서 프로퍼티 블록을 갱신하지 않는다.
        if (state == ItemMoveState.Dropped || shadowRenderer == null) return;

        // 실제 월드 높이(height)는 던지기마다 제각각 크기 때문에 그대로 픽셀로 환산하면 3번 프레임에
        // 순식간에 도달해 버린다(대부분의 비행 구간에서 3번 프레임에 "무작정" 고정). 대신 이번 비행의
        // 정점(height) 대비 진행률로 정규화해서, 솟아오를 때 1->2->3, 떨어질 때 3->2->1로 자연스럽게
        // 전환되고 착지 순간(heightOffset=0)에는 항상 1번 프레임으로 정확히 이어지도록 한다.
        float normalizedHeight = height > 0.0001f ? Mathf.Clamp01(_heightOffset / height) : 0f;
        float shadowHeightPixels = normalizedHeight * shadowFlightPixelScale;

        if (mpb == null) mpb = new MaterialPropertyBlock();
        shadowRenderer.GetPropertyBlock(mpb);
        mpb.SetFloat(ShadowHeightPixelsPropertyID, shadowHeightPixels);
        shadowRenderer.SetPropertyBlock(mpb);
    }

    // 그림자용 4프레임 스프라이트의 UV 사각형을 계산해 셰이더에 1회 전달한다(스폰/재사용 시 1회만 실행).
    // Dropped 상태의 둥둥 뜨는 애니메이션에 따른 프레임 선택은 Shadow_LogItem 셰이더가 LogItem 본체와 동일한
    // 사인파 공식으로 직접 계산하므로, 매 프레임 CPU 개입이 전혀 없다.
    private void InitializeShadowFrames()
    {
        if (shadowRenderer == null) return;

        shadowRenderer.sprite = shadowSprite_0 != null ? shadowSprite_0 : shadowRenderer.sprite;

        // 모든 LogItem이 같은 그림자 스프라이트시트를 공유하므로, 프레임 Rect는 인스턴스별이 아니라
        // 공유 머티리얼에 직접 굽는다(인스턴싱 버퍼로 개별 전달했을 때의 신뢰성 문제를 피하기 위함).
        Material sharedShadowMaterial = shadowRenderer.sharedMaterial;
        if (sharedShadowMaterial != null && initializedShadowMaterials.Add(sharedShadowMaterial))
        {
            sharedShadowMaterial.SetVector(ShadowFrameRect0PropertyID, GetSpriteUVRect(shadowSprite_Minus1));
            sharedShadowMaterial.SetVector(ShadowFrameRect1PropertyID, GetSpriteUVRect(shadowSprite_0));
            sharedShadowMaterial.SetVector(ShadowFrameRect2PropertyID, GetSpriteUVRect(shadowSprite_1));
            sharedShadowMaterial.SetVector(ShadowFrameRect3PropertyID, GetSpriteUVRect(shadowSprite_2Plus));

            // Shadow 오브젝트가 본체(Animator)와 다른 로컬 오프셋을 가질 수 있으므로, 셰이더가 본체와 동일한
            // 월드 기준점으로 둥둥 뜨는 위상을 계산할 수 있도록 그 오프셋을 전달한다(회전/스케일 없는 형제 관계 전제).
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

    public void SetHeight(float _height)
    {
        customSortable.SetHeight(_height);
    }

    public void SetFlyingItemSortingLayer()
    {
        spriteRenderer.sortingLayerID = flyingItemSortingLayerID;
    }

    private void SetShaderFloating(bool _enable)
    {
        // GPU 인스턴싱(SRP Batcher) 유지를 위해 MaterialPropertyBlock 사용을 제거하고 셰이더 내부 연산으로 대체함
        // outlineSR은 _OutlineColor를 인스턴스 프로퍼티 블록에 유지해야 하므로 여기서 초기화하지 않는다
        // 샤이니 효과는 Dropped 상태이면서 logState가 Normal보다 높을 때만 노출되어야 하므로 인스턴스 프로퍼티 블록으로 제어한다
        bool shinyEnabled = _enable && logState > LogState.Normal;

        if (spriteRenderer != null)
        {
            if (mpb == null) mpb = new MaterialPropertyBlock();
            spriteRenderer.GetPropertyBlock(mpb);
            mpb.SetFloat(ShinyEnabledPropertyID, shinyEnabled ? 1f : 0f);
            spriteRenderer.SetPropertyBlock(mpb);
        }
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
