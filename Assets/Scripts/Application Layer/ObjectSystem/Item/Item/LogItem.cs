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
    private bool isSucked = false;
    private bool isLaunching = false;
    private Transform suckTarget;
    private Coroutine moveCoroutine;
    private bool bDrop = true;
    public float durability = 0f;

    private IInventoryChecker inventoryChecker;

    public void Initialize(LogItemTypeData _logItemTypeData, LogState _logState, Color _color)
    {
        base.Initialize(_logItemTypeData.itemType);

        logState = _logState;
        treeType = _logItemTypeData.treeType;
        isSucked = false;
        isLaunching = false;
        suckTarget = null;
        sprite = _logItemTypeData.sprite;
        color = _color;
        durability = _logItemTypeData.durability;

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = color;
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                visualTransform = spriteRenderer.transform;
            }
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
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        moveCoroutine = StartCoroutine(ParabolicMoveRoutine(_start, _end, _height, _duration));
    }

    public override void ResetItem()
    {
        base.ResetItem();

        isSucked = false;
        isLaunching = false;
        suckTarget = null;
    }

    private IEnumerator ParabolicMoveRoutine(Vector3 _start, Vector3 _end, float _height, float _duration)
    {
        isLaunching = true;
        float elapsed = 0f;

        while (elapsed < _duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _duration;

            // 선형 보간 (바닥 위치)
            Vector3 currentGroundPos = Vector3.Lerp(_start, _end, t);

            // 포물선 높이 계산 (y = -4h(t-0.5)^2 + h)
            float heightOffset = -4 * _height * (t - 0.5f) * (t - 0.5f) + _height;

            if (visualTransform != null)
            {
                // 시각적 트랜스폼이 있는 경우 (높이만 따로 조절)
                transform.position = currentGroundPos;
                visualTransform.localPosition = new Vector3(0, heightOffset, 0);
            }
            else
            {
                // 없는 경우 직접 position 수정
                transform.position = currentGroundPos + new Vector3(0, heightOffset, 0);
            }

            yield return null;
        }

        transform.position = _end;

        // 전역 픽셀 스냅 유틸리티 사용
        transform.position = GlobalPixelSnapper.Snap(transform.position);

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.zero;
        }

        isLaunching = false;
        moveCoroutine = null;

        // 도착했을 때 이미 타겟이 범위 내에 있었다면 흡입 시작
        if (suckTarget != null && inventoryChecker.CanAcquired(this))
        {
            StartSucking(suckTarget);
        }
    }

    private void Update()
    {
        if (isSucked || isLaunching || bDrop == false || suckTarget == null) return;

        // 인벤토리 공간이 생겼을 때만 흡입 시작
        if (inventoryChecker.CanAcquired(this))
        {
            StartSucking(suckTarget);
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (isSucked || bDrop == false) return;

        // 아이템 센서 범위에 들어왔을 때 타겟 설정
        if (_other.CompareTag("ItemSensor"))
        {
            suckTarget = _other.transform;

            // 발사 중이 아니고 인벤토리에 여유가 있을 때 즉시 흡입 시작
            if (!isLaunching)
            {
                if (inventoryChecker.CanAcquired(this))
                {
                    StartSucking(suckTarget);
                }
            }
        }
        
        // 캐릭터(Player)와 직접 충돌했을 때는 아무것도 하지 않음 (흡입을 통해서만 습득)
        if (_other.CompareTag("Player"))
        {
            // Do nothing
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
        if (bDrop == false)
            return;
            
        if (_other.CompareTag("ItemSensor"))
        {
            if (suckTarget == _other.transform)
            {
                suckTarget = null;
            }
        }
    }

    private void StartSucking(Transform _target)
    {
        if (moveCoroutine != null) StopCoroutine(moveCoroutine);
        isSucked = true;
        moveCoroutine = StartCoroutine(SuckingRoutine(_target));
    }

    private IEnumerator SuckingRoutine(Transform _target)
    {
        float speed = 0f; // 0에서 시작하여 서서히 가속
        float accel = 12f;

        while (true)
        {
            if (_target == null) break;

            Vector3 targetPos = _target.position;
            float distance = Vector3.Distance(transform.position, targetPos);

            if (distance < 0.2f)
            {
                LogItemAcquired?.Invoke(this);

                yield break;
            }

            speed += accel * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);

            if (visualTransform != null)
            {
                visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, Vector3.zero, Time.deltaTime * 5f);
            }

            yield return null;
        }
    }
}
