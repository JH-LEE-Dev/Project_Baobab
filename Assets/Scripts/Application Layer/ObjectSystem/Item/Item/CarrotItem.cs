using System;
using UnityEngine;

public enum ItemMoveState
{
    None,
    Launching, // 포물선 비행 중
    Dropped,   // 바닥에 떨어짐 (습득 대기)
    Sucking    // 캐릭터에게 흡수 중
}

public class CarrotItem : Item, IStaticCollidable
{
    // 이벤트
    public event Action<CarrotItem> CarrotItemAcquired;

    // IStaticCollidable 구현
    public Vector2 Position => transform.position;
    public Vector2 Offset => Vector2.zero;
    public float Radius => 0.1f;
    public int Layer => gameObject.layer;
    public void TakeDamage(float _damage) { }

    // 내부 의존성
    private SpriteRenderer spriteRenderer;
    private Transform visualTransform;

    // 상태 변수
    private ItemMoveState state = ItemMoveState.None;
    private Transform suckTarget;
    private bool bDrop = true;
    public float amount { get; private set; } = 0;

    public bool bCanApplyDamage => false;

    // 이동 관련 변수 (캐싱)
    private Vector3 startPos;
    private Vector3 endPos;
    private float height;
    private float duration;
    private float elapsed;
    private float suckSpeed;
    private const float SuckAccel = 12f;
    private const float MinAcquireDist = 0.2f;

    public void Initialize()
    {
        base.Initialize(ItemType.Carrot);

        state = ItemMoveState.None;
        suckTarget = null;
        elapsed = 0;

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                visualTransform = spriteRenderer.transform;
            }
        }
    }

    public void IsDropItem(bool _boolean)
    {
        bDrop = _boolean;
    }

    public void Launch(Vector3 _start, Vector3 _end, float _height, float _duration)
    {
        startPos = _start;
        endPos = _end;
        height = _height;
        duration = _duration;
        elapsed = 0f;
        state = ItemMoveState.Launching;

        // 활성화 상태라면 등록
        if (gameObject.activeInHierarchy)
        {
            CollisionSystem.Instance?.Register(this, false);
        }
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

    public override void ResetItem()
    {
        base.ResetItem();
        state = ItemMoveState.None;
        suckTarget = null;
        elapsed = 0;
    }

    public void SetAmount(float _amount)
    {
        amount = _amount;
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
                // 바닥 상태에서 타겟이 있으면 흡입 시작
                if (suckTarget != null)
                {
                    StartSucking(suckTarget);
                }
                break;
        }
    }

    private void UpdateLaunching(float _deltaTime)
    {
        elapsed += _deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        // 선형 보간 (바닥 위치)
        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, t);

        // 포물선 높이 계산 (y = -4h(t-0.5)^2 + h)
        float heightOffset = -4 * height * (t - 0.5f) * (t - 0.5f) + height;

        if (visualTransform != null)
        {
            transform.position = currentGroundPos;
            visualTransform.localPosition = new Vector3(0, heightOffset, 0);
        }
        else
        {
            transform.position = currentGroundPos + new Vector3(0, heightOffset, 0);
        }

        CollisionSystem.Instance?.UpdatePosition(this, transform.position);

        if (t >= 1.0f)
        {
            transform.position = GlobalPixelSnapper.Snap(endPos);
            if (visualTransform != null) visualTransform.localPosition = Vector3.zero;
            
            state = ItemMoveState.Dropped;
            if (suckTarget != null) StartSucking(suckTarget);
        }
    }

    private void UpdateSucking(float _deltaTime)
    {
        if (suckTarget == null)
        {
            state = ItemMoveState.Dropped;
            return;
        }

        Vector3 targetPos = suckTarget.position;
        float distance = Vector3.Distance(transform.position, targetPos);

        if (distance < MinAcquireDist)
        {
            CarrotItemAcquired?.Invoke(this);
            return;
        }

        suckSpeed += SuckAccel * _deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, suckSpeed * _deltaTime);

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, Vector3.zero, _deltaTime * 5f);
        }

        CollisionSystem.Instance?.UpdatePosition(this, transform.position);
    }

    public override void SetSuckTarget(Transform _target)
    {
        if (state == ItemMoveState.Sucking || !bDrop) return;

        suckTarget = _target;
        if (state == ItemMoveState.Dropped)
        {
            StartSucking(suckTarget);
        }
    }

    private void StartSucking(Transform _target)
    {
        suckTarget = _target;
        suckSpeed = 0f;
        state = ItemMoveState.Sucking;
    }
}
