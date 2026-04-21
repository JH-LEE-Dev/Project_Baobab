using System;
using System.Collections;
using UnityEngine;

public class LogItem : Item
{
    // 이벤트
    public event Action<LogItem> LogItemAcquired;

    // 내부 의존성
    public LogState logState { get; private set; }
    public TreeType treeType { get; private set; }
    private SpriteRenderer spriteRenderer;
    private Transform visualTransform;

    // 상태 변수
    private ItemMoveState state = ItemMoveState.None;
    private Transform suckTarget;
    private bool bDrop = true;
    public float durability = 0f;

    private IInventoryChecker inventoryChecker;

    // 이동 관련 변수 (캐싱)
    private Vector3 startPos;
    private Vector3 endPos;
    private float height;
    private float duration;
    private float elapsed;
    private float suckSpeed;
    private const float SuckAccel = 12f;
    private const float MinAcquireDist = 0.2f;

    public void Initialize(LogItemTypeData _logItemTypeData, LogState _logState, Color _color)
    {
        base.Initialize(_logItemTypeData.itemType);

        logState = _logState;
        treeType = _logItemTypeData.treeType;
        state = ItemMoveState.None;
        suckTarget = null;
        sprite = _logItemTypeData.sprite;
        color = _color;
        durability = _logItemTypeData.durability;
        elapsed = 0;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
            visualTransform = spriteRenderer.transform;
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

    public void Launch(Vector3 _start, Vector3 _end, float _height, float _duration)
    {
        startPos = _start;
        endPos = _end;
        height = _height;
        duration = _duration;
        elapsed = 0f;
        state = ItemMoveState.Launching;
    }

    public override void ResetItem()
    {
        base.ResetItem();
        state = ItemMoveState.None;
        suckTarget = null;
        elapsed = 0;
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
                UpdateDropped();
                break;
        }
    }

    private void UpdateLaunching(float _deltaTime)
    {
        elapsed += _deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, t);
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

        if (t >= 1.0f)
        {
            transform.position = GlobalPixelSnapper.Snap(endPos);
            if (visualTransform != null) visualTransform.localPosition = Vector3.zero;
            
            state = ItemMoveState.Dropped;
            CheckAcquireCondition();
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
            LogItemAcquired?.Invoke(this);
            return;
        }

        suckSpeed += SuckAccel * _deltaTime;
        transform.position = Vector3.MoveTowards(transform.position, targetPos, suckSpeed * _deltaTime);

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, Vector3.zero, _deltaTime * 5f);
        }
    }

    private void UpdateDropped()
    {
        if (!bDrop || suckTarget == null) return;
        
        CheckAcquireCondition();
    }

    private void CheckAcquireCondition()
    {
        if (suckTarget != null && inventoryChecker.CanAcquired(this))
        {
            StartSucking(suckTarget);
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (state == ItemMoveState.Sucking || !bDrop) return;

        if (_other.CompareTag("ItemSensor"))
        {
            suckTarget = _other.transform;
            if (state == ItemMoveState.Dropped)
            {
                CheckAcquireCondition();
            }
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (!bDrop) return;
            
        if (_other.CompareTag("ItemSensor") && suckTarget == _other.transform)
        {
            suckTarget = null;
        }
    }

    private void StartSucking(Transform _target)
    {
        suckTarget = _target;
        suckSpeed = 0f;
        state = ItemMoveState.Sucking;
    }
}
