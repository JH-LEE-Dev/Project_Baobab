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

    public void Initialize(LogItemTypeData _logItemTypeData, LogState _logState)
    {
        base.Initialize(_logItemTypeData.itemType);

        logState = _logState;
        treeType = _logItemTypeData.treeType;
        isSucked = false;
        isLaunching = false;
        suckTarget = null;
        sprite = _logItemTypeData.sprite;
        

        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = sprite;
            spriteRenderer.color = _logItemTypeData.color;
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
        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.zero;
        }

        isLaunching = false;
        moveCoroutine = null;

        // 도착했을 때 이미 타겟이 범위 내에 있었다면 흡입 시작
        if (suckTarget != null)
        {
            StartSucking(suckTarget);
        }
    }

    private void OnTriggerEnter2D(Collider2D _other)
    {
        if (isSucked) return;

        if (_other.CompareTag("ItemSensor"))
        {
            suckTarget = _other.transform;

            // 발사 중이 아닐 때만 즉시 흡입 시작
            if (!isLaunching)
            {
                StartSucking(suckTarget);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D _other)
    {
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
