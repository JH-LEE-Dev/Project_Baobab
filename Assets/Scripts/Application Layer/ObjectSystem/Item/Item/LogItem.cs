using System;
using System.Collections;
using UnityEngine;

public class LogItem : Item, IStaticCollidable
{
    // 이벤트
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

    public Material outlineMaterial;
    private Material originalMaterial;

    private MaterialPropertyBlock mpb;
    private static readonly int baseColorID = Shader.PropertyToID("_BaseColor");

    private CustomSortable customSortable;

    private bool bDisableCustomSortable = false;
    private float originalDurability;

    public void Initialize(LogItemTypeData _logItemTypeData, LogState _logState, Color _color, bool _bDisableCustomSortable = false)
    {
        base.Initialize(_logItemTypeData.itemType);

        bDisableCustomSortable = _bDisableCustomSortable;
        logState = _logState;
        treeType = _logItemTypeData.treeType;
        state = ItemMoveState.None;
        suckTarget = null;
        dynamicTarget = null;
        sprite = _logItemTypeData.sprite;
        color = _color;
        durability = _logItemTypeData.durability;
        originalDurability = durability;
        elapsed = 0;
        timberSprite = _logItemTypeData.timberSprite;
        landingDampTime = landingDampDuration;

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
            spriteRenderer.color = color;
        }

        transform.localScale = Vector3.one;
        originalMaterial = spriteRenderer.material;

        customSortable = GetComponent<CustomSortable>();

        if (customSortable != null)
        {
            // 정렬 기준(Anchor)을 상하 이동하는 visualTransform으로 설정
            customSortable.Initialize(visualTransform != null ? visualTransform : transform);
            customSortable.AddSpriteRenderer(spriteRenderer);
        }
    }

    public void SetInventoryChecker(IInventoryChecker _inventoryChecker)
    {
        inventoryChecker = _inventoryChecker;
    }

    public void IsDropItem(bool _boolean)
    {
        bDrop = _boolean;
    }

    public void Launch(Vector3 _start, Vector3 _end, float _height, float _duration, float _totalRotation = 0f)
    {
        startPos = _start;
        endPos = _end;
        height = _height;
        duration = _duration;
        trajectoryJitter = Vector3.zero;
        rotationSpeed = 0f;
        totalRotation = _totalRotation;
        elapsed = 0f;
        state = ItemMoveState.Launching;
        transform.localScale = Vector3.zero;

        spriteRenderer.material = outlineMaterial;

        if (mpb == null) mpb = new MaterialPropertyBlock();
        spriteRenderer.GetPropertyBlock(mpb);
        mpb.SetColor(baseColorID, spriteRenderer.color);
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

        if (spriteRenderer != null)
        {
            spriteRenderer.material = originalMaterial;
            spriteRenderer.SetPropertyBlock(null);
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
    }

    public void SetTimberSprite()
    {
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
    }

    private void UpdateLaunching(float _deltaTime)
    {
        elapsed += _deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // 1. 가로 이동에 EaseOutCubic 적용 (도착 지점에서 부드럽게 감속하여 쫀득한 느낌 부여)
        float easeT = 1f - (1f - t) * (1f - t) * (1f - t);
        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, easeT);

        // 2. 높이 계산 (포물선)
        float heightOffset = -4 * height * (t - 0.5f) * (t - 0.5f) + height;

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
            float verticalVelocity = -8 * height * (t - 0.5f) / duration;
            float pulse = Mathf.Abs(verticalVelocity) * 0.03f;
            pulse = Mathf.Min(pulse, 0.2f); // 최대 변형치 제한

            visualTransform.localScale = Vector3.one * (1f + pulse);
        }
        else
        {
            transform.position = currentGroundPos + new Vector3(0, heightOffset, 0);
        }

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
            }
            transform.localScale = Vector3.one;

            if (customSortable != null)
            {
                customSortable.SetHeight(0f);
            }

            landingDampTime = 0f;

            state = ItemMoveState.Dropped;
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

            if (customSortable != null)
            {
                customSortable.SetHeight(0f);
            }

            state = ItemMoveState.Dropped;
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

            if (customSortable != null)
            {
                customSortable.SetHeight(0f);
            }

            state = ItemMoveState.Dropped;
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

            if (customSortable != null)
            {
                customSortable.SetHeight(0f);
            }

            state = ItemMoveState.Dropped;
        }
    }

    private void UpdateSucking(float _deltaTime)
    {
        if (suckTarget == null)
        {
            state = ItemMoveState.Dropped;
            return;
        }

        elapsed += _deltaTime;

        Vector3 targetPos = suckTarget.position;
        float distance = Vector3.Distance(transform.position, targetPos);

        // 도착 조건: 거리가 가깝고 타겟을 향해 이동 중일 때
        if (distance < MinAcquireDist && suckSpeed > 0f)
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
        }

        // 타겟 방향으로 부드럽게 이동
        Vector3 dir = (targetPos - transform.position).normalized;
        transform.position += dir * suckSpeed * _deltaTime;

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, Vector3.zero, _deltaTime * 10f);

            // 스프링 댐퍼 연출 (진동 주기와 감쇠를 더 쫀득하게 튜닝)
            float freq = 18f;
            float decay = 4f;
            float springEffect = Mathf.Sin(elapsed * freq) * Mathf.Exp(-elapsed * decay) * 0.5f;

            if (suckSpeed > 0f)
            {
                // 빨려 들어가는 속도에 비례해 타겟 방향으로 길어지는 Stretch 효과 추가
                float speedStretch = Mathf.Min(suckSpeed * 0.04f, 0.25f);
                visualTransform.localScale = new Vector3(
                    (1f + springEffect) * (1f - speedStretch), // 가로축 축소
                    (1f - springEffect) * (1f + speedStretch), // 세로축 확대 (타겟 방향)
                    1f
                );
            }
            else
            {
                visualTransform.localScale = Vector3.one * (1f + springEffect);
            }

            if (customSortable != null)
            {
                customSortable.SetHeight(visualTransform.localPosition.y);
            }
        }

        // 타겟에 매우 가까워지면 전체 스케일 축소 (최소 0.25 유지)
        if (distance < 1.5f && suckSpeed > 0f)
        {
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
            // Y축 둥둥 떠있는 움직임 (Sine Wave)
            // 위치 기반 오프셋을 주어 아이템마다 타이밍이 다르게 함
            float posOffset = (transform.position.x + transform.position.y) * 10f;
            float floatOffset = Mathf.Sin(Time.time * 2.5f + posOffset) * 0.05f;
            visualTransform.localPosition = new Vector3(0, floatOffset, 0);

            if (customSortable != null)
            {
                customSortable.SetHeight(floatOffset);
            }

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
            }
            else
            {
                visualTransform.localScale = Vector3.one;
            }
        }

        if (!bDrop || suckTarget == null) return;

        CheckAcquireCondition();
    }

    private void CheckAcquireCondition()
    {
        if (suckTarget != null && inventoryChecker.CanAcquired(this))
        {
            StartSucking(suckTarget);

            return;
        }

        suckTarget = null;
    }

    public override void SetSuckTarget(Transform _target)
    {
        if (state == ItemMoveState.Sucking || !bDrop || bCanAcquired == false) return;

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
    }

    public void SetbCanAcquired(bool _boolean)
    {
        bCanAcquired = _boolean;
    }

    private void LateUpdate()
    {
        if (bDisableCustomSortable == false)
            customSortable.ManualLateUpdate();
    }
}
