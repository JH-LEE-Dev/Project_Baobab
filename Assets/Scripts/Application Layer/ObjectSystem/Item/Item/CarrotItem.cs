// ──────────────────────────────────────────────────────────────────────────
// [사용 안 함 — 기획 결정]  당근 아이템 / 토끼 계열
//
// 이 계열은 쓰지 않기로 결정된 기능입니다. 배선이 빠진 것이 아니라 "안 쓰기로 한 것"이므로,
// 동작하지 않는다고 해서 버그로 보고 되살리거나 배선을 복구하지 마십시오.
//
// 현재 상태: 호출부가 없고 커맨드 2종(CarrotBundle / RabbitBoom)도 미배선입니다.
//
// ⚠ 혼동 주의: 여기서 말하는 "당근"은 던전에 떨어지는 당근 아이템과 토끼 기능입니다.
//    스킬 비용 통화인 carrot(InventoryManager.carrot / SaveData.carrot /
//    IMoneyData.carrot / SkillManager)은 이 기능과 무관하게 계속 쓰입니다.
//    같이 지우면 세이브 포맷이 깨집니다.
//
// 코드를 지우지 않고 주석만 남긴 이유: ICarrotItemCH가 ICommandHandleSystem과 SkillDispatcher에
//   남아 있고, 위 통화 carrot과 이름이 겹쳐 일괄 삭제가 위험합니다.
// 정리하려면 에디터에서 컴파일이 도는 상태로 독립 커밋으로 진행하십시오.
// ──────────────────────────────────────────────────────────────────────────
using System;
using UnityEngine;

public enum ItemMoveState
{
    None,
    Launching,         // 포물선 비행 중
    Transferring,      // 보관함으로 전송 중 (스케일 애니메이션 포함)
    ContainerTransferring, // 오프로드 컨테이너 전용 전송 (부드러운 가속)
    DynamicTransferring, // 동적 타겟(캐릭터 등)으로 전송 중
    Dropped,           // 바닥에 떨어짐 (습득 대기)
    Sucking            // 캐릭터에게 흡수 중
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

    // 내부 의존성
    private SpriteRenderer spriteRenderer;
    private Transform visualTransform;

    // 상태 변수
    private ItemMoveState state = ItemMoveState.None;
    private Transform suckTarget;
    private bool bDrop = true;
    public float amount { get; private set; } = 0;



    // 이동 관련 변수 (캐싱)
    private Vector3 startPos;
    private Vector3 endPos;
    private float height;
    private float duration;
    private float elapsed;
    private float suckSpeed;
    private const float SuckAccel = 12f;
    private const float MinAcquireDist = 0.2f;

    // 관리용 인덱스
    public int PoolIndex { get; set; } = -1;
    public int UpdateIndex { get; set; } = -1;

    // 이 오브젝트가 현재 풀 안에 들어가 있는지. 이중 반납을 O(1)로 차단하기 위한 플래그로,
    // 풀의 actionOnGet/actionOnRelease에서만 갱신한다. (자세한 배경은 PoolSettings 참조)
    public bool IsPooled { get; set; } = false;

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
