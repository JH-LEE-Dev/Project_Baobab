// ─────────────────────────────────────────────────────────────────────────────
// [죽은 코드] 원석 / 용광로 계열.
//
// SYSTEM_VAR.GEM_ORE_SYSTEM_ENABLED 가 false 라서 이 파일의 코드는 한 줄도 실행되지
// 않는다. 원석이 게임에 들어오는 입구(InDungeonObjectManager.OnTreeDead)가 막혀 있어
// 원석 아이템이 생성되지 않고, 용광로도 열리지 않는다.
//
// 버그 / 회귀 검토 대상에서 제외한다. 동작하지 않는 코드라 여기서 나오는 지적은 의미가 없다.
// 되살릴 때는 Assets/Scripts/Global/System.cs 의 그 스위치를 true 로 바꾸면 되고,
// 그 시점에 이 헤더들도 같이 지워야 한다.
// ─────────────────────────────────────────────────────────────────────────────

using System;
using UnityEngine;

/// <summary>
/// 포물선을 그리며 날아가는 단순 스프라이트. 용광로가 두 방향으로 쓴다.
///   - 캐릭터 -> 용광로 : 원석(각 보석의 M 사이즈 그림)
///   - 용광로 -> 상점NPC : 완성된 주괴
///
/// 움직임은 LogItem.UpdateTransferring을 그대로 옮겨온 것이다(가속 구간 0.7, 포물선 지터,
/// BackEaseOut 스케일, 도착 0.3 구간 축소). 원목을 납품할 때와 눈에 보이는 궤적이 같아야 해서
/// 수식을 새로 만들지 않고 복제했다.
///
/// LogItem을 그대로 쓰지 않은 이유: LogItem은 원목 데이터(TreeType/LogState)와 충돌 시스템 등록,
/// 보석 아우라까지 달고 다니는 무거운 아이템이라 "그림 하나 날리는" 용도로는 과하다.
/// 주괴는 아이템 타입 자체가 없기도 하다.
/// </summary>
public class FlyingSpriteItem : MonoBehaviour
{
    /// <summary>도착했을 때. 도착 처리(재화 반영 등)는 구독자가 한다.</summary>
    public event Action<FlyingSpriteItem> ArrivedEvent;

    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform visualTransform;

    private Vector3 startPos;
    private Vector3 endPos;
    private Transform dynamicTarget;
    private Vector3 trajectoryJitter;
    private float height;
    private float duration;
    private float rotationSpeed;
    private float elapsed;
    private bool bFlying;

    public bool Flying => bFlying;

    /// <summary>도착 처리에 쓸 꼬리표. 어느 용광로/어느 원석인지 호출한 쪽이 기억하게 한다.</summary>
    public object Payload { get; set; }

    public void Setup(Sprite _sprite, string _sortingLayer, int _sortingOrder)
    {
        if (spriteRenderer == null) return;

        spriteRenderer.sprite = _sprite;
        spriteRenderer.sortingLayerName = _sortingLayer;
        spriteRenderer.sortingOrder = _sortingOrder;
    }

    /// <summary>
    /// 고정 목표로 발사. 상대가 움직이지 않을 때(용광로) 쓴다.
    /// </summary>
    public void Launch(Vector3 _start, Vector3 _end, float _height, float _duration, Vector3 _jitter, float _rotationSpeed)
    {
        dynamicTarget = null;
        LaunchInternal(_start, _end, _height, _duration, _jitter, _rotationSpeed);
    }

    /// <summary>
    /// 움직이는 목표로 발사. 상점NPC처럼 서 있다가도 밀릴 수 있는 대상에 쓴다.
    /// </summary>
    public void LaunchToTarget(Vector3 _start, Transform _target, float _height, float _duration, Vector3 _jitter, float _rotationSpeed)
    {
        dynamicTarget = _target;
        LaunchInternal(_start, _target != null ? _target.position : _start, _height, _duration, _jitter, _rotationSpeed);
    }

    private void LaunchInternal(Vector3 _start, Vector3 _end, float _height, float _duration, Vector3 _jitter, float _rotationSpeed)
    {
        startPos = _start;
        endPos = _end;
        height = _height;
        duration = Mathf.Max(0.01f, _duration);
        trajectoryJitter = _jitter;
        rotationSpeed = _rotationSpeed;
        elapsed = 0f;
        bFlying = true;

        transform.position = _start;
        transform.localScale = Vector3.zero;

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
        }
    }

    public void ResetItem()
    {
        bFlying = false;
        dynamicTarget = null;
        Payload = null;
        elapsed = 0f;
        transform.localScale = Vector3.one;

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
        }
    }

    /// <summary>소유자(BlastFurnaceManager)가 매 프레임 직접 돌린다.</summary>
    public void ManualUpdate(float _deltaTime)
    {
        if (false == bFlying) return;

        if (dynamicTarget != null)
        {
            endPos = dynamicTarget.position;
        }

        // 도착 직전 가속 (LogItem.UpdateTransferring과 동일)
        float currentT = elapsed / duration;
        float speedMultiplier = 1f;
        if (currentT > 0.7f)
        {
            speedMultiplier = 1f + (currentT - 0.7f) * 15f;
        }

        elapsed += _deltaTime * speedMultiplier;
        float t = Mathf.Clamp01(elapsed / duration);

        // 지터는 시점/종점에서 0, 중간에서 최대
        float jitterFactor = 4f * t * (1f - t);
        Vector3 groundPos = Vector3.Lerp(startPos, endPos, t) + (trajectoryJitter * jitterFactor);

        float heightOffset = -4f * height * (t - 0.5f) * (t - 0.5f) + height;

        if (visualTransform != null)
        {
            transform.position = groundPos;
            visualTransform.localPosition = new Vector3(0f, heightOffset, 0f);
            visualTransform.Rotate(Vector3.forward, rotationSpeed * _deltaTime);
        }
        else
        {
            transform.position = groundPos + new Vector3(0f, heightOffset, 0f);
        }

        transform.localScale = Vector3.one * GetScale(t);

        if (t < 1f) return;

        bFlying = false;
        ArrivedEvent?.Invoke(this);
    }

    /// <summary>0.4까지 스프링 댐퍼로 커지고 0.7부터 줄어드는 스케일 연출(LogItem과 동일).</summary>
    private static float GetScale(float _t)
    {
        if (_t < 0.4f)
        {
            float nt = _t / 0.4f;
            const float s = 1.70158f;   // BackEaseOut 탄성 계수
            float t1 = nt - 1f;
            return Mathf.Max(0f, t1 * t1 * ((s + 1f) * t1 + s) + 1f);
        }

        if (_t > 0.7f)
        {
            return 1f - ((_t - 0.7f) / 0.3f);
        }

        return 1f;
    }
}
