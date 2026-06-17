using System;
using UnityEngine;

public class LogItem : Item, IStaticCollidable
{
    // 이벤트
    public event Action<LogItem> LogItemActivatedEvent;
    public event Action<LogItem> LogItemDeActivatedEvent;
    public event Action<LogItem> LogItemAcquired;

    // IStaticCollidable 구현
    public Vector2 Position => transform.position;
    public Vector2 Offset => Vector2.zero;
    public float Radius => 0.1f;
    public int Layer => gameObject.layer;
    public void TakeDamage(float _damage) { }

    // 내부 의존성
    public LogState logState { get; private set; }
    public TreeType treeType { get; private set; }

    public bool bCanApplyDamage => false;

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

    [SerializeField] private GameObject shadow;
    private Transform shadowTransform;

    private Color originalColor;

    private string flyingItemSortingLayerName = "FlyingItem";
    private string objectsSortingLayerName = "Objects";
    private static int objectsSortingLayerID = -1;
    private static int flyingItemSortingLayerID = -1;

    [SerializeField] private GameObject outlineObj;
    [SerializeField] private SpriteRenderer outlineStencilSR;
    [SerializeField] private SpriteRenderer outlineSR;

    private ICharacter character;

    private ParticleSystem particleEffect;
    private VFXComponent vfxComponent;

    private string objectSortingLayerName = "Objects";

    public void Initialize(LogItemTypeData _logItemTypeData, Color _color, LogState _logState, ICharacter _character, bool _bDisableCustomSortable = false)
    {
        base.Initialize(_logItemTypeData.itemType);

        character = _character;
        bDisableCustomSortable = _bDisableCustomSortable;
        logState = _logState;
        treeType = _logItemTypeData.treeType;
        state = ItemMoveState.None;
        suckTarget = null;
        dynamicTarget = null;
        sprite = _logItemTypeData.sprite;
        durability = _logItemTypeData.durability;
        originalDurability = durability;
        elapsed = 0;
        timberSprite = _logItemTypeData.timberSprite;
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
        }

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
    }

    public void SetVfxComponent(VFXComponent _vfxComponent)
    {
        vfxComponent = _vfxComponent;
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

        outlineObj.SetActive(true);

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
        state = ItemMoveState.None;
        suckTarget = null;
        dynamicTarget = null;
        elapsed = 0;
        trajectoryJitter = Vector3.zero;
        sideDir = Vector3.zero;
        rotationSpeed = 0f;
        totalRotation = 0f;
        bCanAcquired = true;
        transform.localScale = Vector3.one;
        landingDampTime = landingDampDuration;
        durability = originalDurability;
        inventoryCheckTimer = 0.15f; // 스폰 시 즉시 검사하도록 설정

        if (outlineObj != null)
            outlineObj.SetActive(false);

        if (spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
            spriteRenderer.SetPropertyBlock(null);
            spriteRenderer.sortingLayerID = objectsSortingLayerID;
        }

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

    public void SetTimberSprite()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = timberSprite;
            spriteRenderer.color = color;
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

        if (particleEffect != null)
        {
            vfxComponent.SetSortingSettings(particleEffect, objectSortingLayerName, spriteRenderer.sortingOrder + 1);
        }
    }

    public void UpdateSortingOrder()
    {
        if (bDisableCustomSortable == false)
        {
            customSortable.ManualLateUpdate();
            if (outlineStencilSR != null && outlineSR != null)
                outlineStencilSR.sortingOrder = outlineSR.sortingOrder - 1;
        }
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
            SetShaderFloating(true);
            CheckAcquireCondition();
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
        }
    }

    private void UpdateDynamicTransferring(float _deltaTime)
    {
        if (dynamicTarget == null)
        {
            state = ItemMoveState.Dropped;
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
            // Y축 둥둥 떠있는 움직임 (Sine Wave) - 셰이더로 이관됨 (기존 코드 보존)
            /*
            float posOffset = (transform.position.x + transform.position.y) * 10f;
            float floatOffset = Mathf.Sin(Time.time * 2.5f + posOffset) * 0.05f;
            visualTransform.localPosition = new Vector3(0, floatOffset, 0);

            if (customSortable != null)
            {
                customSortable.SetHeight(floatOffset);
            }

            UpdateShadowScale(floatOffset);
            */

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
        if (suckTarget != null && inventoryChecker.CanAcquired(this) && bCanAcquired == true)
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
        if (state == ItemMoveState.Dropped)
        {
            CheckAcquireCondition();
        }
    }

    private void StartSucking(Transform _target)
    {
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
        if (spriteRenderer != null) spriteRenderer.SetPropertyBlock(null);
        if (outlineStencilSR != null) outlineStencilSR.SetPropertyBlock(null);
        if (outlineSR != null) outlineSR.SetPropertyBlock(null);
    }
}
