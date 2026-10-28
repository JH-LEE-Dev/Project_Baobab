using System;
using UnityEngine;

/// <summary>
/// 4스테이지(MagmaForest) 불씨. 주우면 과열 수치가 오른다(EmberItemController → InDungeonObjectManager).
///
/// 인벤토리 칸을 쓰지 않으므로 원목처럼 공간 판정을 하지 않고, 캐릭터의 아이템 감지(Character.OnItemsDetected)에
/// 걸리는 즉시 빨려간다. 감지 반경이 원목과 같아서 "아이템 획득 범위" 특성의 영향을 그대로 받는다.
/// 움직임(나무에서 튀어나오는 포물선, 착지 스프링, 뒤로 살짝 튕겼다 가속하는 흡입)은 원목(LogItem/GemOreItem)과 같은 공식이다.
///
/// 그림과 이펙트는 이펙트 담당 몫이다. 여기에는 연결 지점만 있다.
/// - 프리팹의 SpriteRenderer(spriteRenderer): 지금은 임시 그림
/// - EmberItemController의 VFX 태그(바닥 루프·획득·소멸): 비어 있으면 재생하지 않는다
/// </summary>
public class EmberItem : Item, IStaticCollidable
{
    public event Action<EmberItem> EmberAcquiredEvent;
    public event Action<EmberItem> EmberExpiredEvent;
    // 바닥에 자리 잡음(true) / 바닥을 떠남(false, 흡입 시작·소멸·회수). 컨트롤러가 칸 점유와 바닥 루프 이펙트에 쓴다.
    public event Action<EmberItem, bool> EmberGroundedChangedEvent;
    // 매 프레임 갱신이 필요해짐(true) / 필요 없어짐(false). 바닥에 가만히 놓여 수명도 없는 불씨는 갱신할 일이 없어서,
    // 컨트롤러가 이 신호로 업데이트 목록에서 빼고 넣는다(원목·원석의 Activated/DeActivated와 같은 구조).
    public event Action<EmberItem, bool> EmberUpdateNeededChangedEvent;

    // IStaticCollidable 구현 - 캐릭터의 아이템 감지(Character.OnItemsDetected)가 이 등록을 보고 흡입을 건다
    public Vector2 Position => transform.position;
    public Vector2 Offset => Vector2.zero;
    public float Radius => 0.1f;
    public int Layer => gameObject.layer;

    public ItemMoveState MoveState => state;
    public bool bGrounded { get; private set; }

    // 관리용 인덱스. PoolIndex는 꺼내 쓰는 중인 전체 목록, UpdateIndex는 매 프레임 갱신하는 목록(-1이면 빠져 있음).
    public int PoolIndex { get; set; } = -1;
    public int UpdateIndex { get; set; } = -1;
    // 이 오브젝트가 현재 풀 안에 들어가 있는지. 이중 반납을 막는다(풀의 actionOnGet/actionOnRelease에서만 갱신).
    public bool IsPooled { get; set; } = false;

    // 바닥 루프 이펙트(컨트롤러가 붙이고 뗀다).
    public ParticleSystem GroundVfx { get; set; }
    // 바닥에 놓여 차지한 칸(컨트롤러가 칸별 개수를 셀 때 쓴다). bHasGroundCell이 false면 아무 칸도 세지 않은 상태다.
    public Vector3Int GroundCell { get; set; }
    public bool bHasGroundCell { get; set; }

    private bool bNeedsUpdate;

    [SerializeField] private SpriteRenderer spriteRenderer;
    private Transform visualTransform;
    private CustomSortable customSortable;
    private ICharacter character;

    private ItemMoveState state = ItemMoveState.None;
    private Transform suckTarget;

    // 남은 수명(초). 0 이하로 정해지면 사라지지 않는다. 흡입 중에는 줄지 않는다.
    private float remainingLifetime;
    private bool bExpires;

    // 포물선
    private Vector3 startPos;
    private Vector3 endPos;
    private float height;
    private float duration;
    private float elapsed;

    // 흡입
    private float suckSpeed;
    private const float SuckAccel = 16f;
    private const float MinAcquireDist = 0.2f;

    // 착지 스프링 연출
    private float landingDampTime = LandingDampDuration;
    private const float LandingDampDuration = 0.5f;

    private void Awake()
    {
        CacheComponents();
    }

    private void CacheComponents()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null && visualTransform == null) visualTransform = spriteRenderer.transform;

        if (customSortable == null)
        {
            customSortable = GetComponent<CustomSortable>();
            if (customSortable != null)
            {
                // 정렬 기준(Anchor)을 상하 이동하는 visualTransform으로 둔다(원목과 같다)
                customSortable.Initialize(visualTransform != null ? visualTransform : transform);
                customSortable.AddSpriteRenderer(spriteRenderer);
            }
        }
    }

    /// <summary>풀에서 꺼낸 직후 부른다. _lifetime이 0 이하면 사라지지 않는다.</summary>
    public void Initialize(ICharacter _character, float _lifetime)
    {
        base.Initialize(ItemType.None); // 인벤토리를 거치지 않는 아이템이라 ItemType이 필요 없다

        CacheComponents();

        character = _character;
        bExpires = _lifetime > 0f;
        remainingLifetime = _lifetime;
        state = ItemMoveState.None;
        suckTarget = null;
        elapsed = 0f;
        landingDampTime = LandingDampDuration;
        transform.localScale = Vector3.one;
    }

    public void SetCharacter(ICharacter _character)
    {
        character = _character;
    }

    /// <summary>원목과 같은 포물선으로 던진다(비행시간은 높이에서만 유도, 중력상수 15).</summary>
    public void Launch(Vector3 _start, Vector3 _end, float _height)
    {
        startPos = _start;
        endPos = _end;
        height = _height;

        duration = Mathf.Max(0.1f, 2f * Mathf.Sqrt(2f * _height / 15f));
        elapsed = 0f;
        state = ItemMoveState.Launching;
        transform.position = _start;
        transform.localScale = Vector3.zero;

        RegisterCollision();
        SetNeedsUpdate(true);
    }

    /// <summary>제자리에 바로 놓는다(입장 시 배치·자연 생성). 작은 착지 스프링으로 나타난다.</summary>
    public void Place(Vector3 _position)
    {
        transform.position = GlobalPixelSnapper.Snap(_position);
        transform.localScale = Vector3.one;

        RegisterCollision();
        Land();
    }

    private void RegisterCollision()
    {
        if (gameObject.activeInHierarchy)
        {
            CollisionSystem.Instance?.Register(this, false);
        }
    }

    private void OnEnable()
    {
        // Launch/Place가 이미 호출된 상태에서 활성화될 때만 등록
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

        CacheComponents();
        SetGrounded(false);
        SetNeedsUpdate(false);

        state = ItemMoveState.None;
        suckTarget = null;
        elapsed = 0f;
        suckSpeed = 0f;
        bExpires = false;
        remainingLifetime = 0f;
        landingDampTime = LandingDampDuration;

        transform.localScale = Vector3.one;

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
            visualTransform.localScale = Vector3.one;
        }

        if (customSortable != null) customSortable.SetHeight(0f);
    }

    public void ManualUpdate(float _deltaTime)
    {
        switch (state)
        {
            case ItemMoveState.Launching:
                UpdateLaunching(_deltaTime);
                break;
            case ItemMoveState.Dropped:
                UpdateDropped(_deltaTime);
                break;
            case ItemMoveState.Sucking:
                UpdateSucking(_deltaTime);
                break;
        }

        if (state == ItemMoveState.None) return; // 이번 프레임에 획득·소멸로 풀에 돌아갔다

        if (bExpires && state != ItemMoveState.Sucking)
        {
            remainingLifetime -= _deltaTime;
            if (remainingLifetime <= 0f)
            {
                EmberExpiredEvent?.Invoke(this);
                return;
            }
        }

        // 바닥에 놓인 불씨는 움직이지 않으므로 착지할 때 한 번만 정렬한다(Land). 움직이는 동안만 매 프레임 정렬한다.
        if ((state == ItemMoveState.Launching || state == ItemMoveState.Sucking) && customSortable != null)
        {
            customSortable.ManualLateUpdate();
        }
    }

    // LogItem.UpdateLaunching과 같은 공식(가로 등속 + 포물선 높이 + 수직속도 비례 부피감 + BackEaseOut 팝업).
    private void UpdateLaunching(float _deltaTime)
    {
        elapsed += _deltaTime;
        float t = Mathf.Clamp01(elapsed / duration);

        Vector3 groundPos = Vector3.Lerp(startPos, endPos, t);
        float heightOffset = -4f * height * (t - 0.5f) * (t - 0.5f) + height;

        transform.position = groundPos;
        if (visualTransform != null)
        {
            visualTransform.localPosition = new Vector3(0f, heightOffset, 0f);

            float verticalVelocity = -8f * height * (t - 0.5f) / duration;
            float pulse = Mathf.Min(Mathf.Abs(verticalVelocity) * 0.03f, 0.2f);
            visualTransform.localScale = Vector3.one * (1f + pulse);
        }
        if (customSortable != null) customSortable.SetHeight(heightOffset);

        float targetScale = 1f;
        if (t < 0.8f)
        {
            float nt = t / 0.8f;
            const float s = 2.5f;
            float t1 = nt - 1f;
            targetScale = Mathf.Max(0f, t1 * t1 * ((s + 1f) * t1 + s) + 1f);
        }
        transform.localScale = Vector3.one * targetScale;

        CollisionSystem.Instance?.UpdatePosition(this, transform.position);

        if (t >= 1f)
        {
            transform.position = GlobalPixelSnapper.Snap(endPos);
            transform.localScale = Vector3.one;
            CollisionSystem.Instance?.UpdatePosition(this, transform.position);
            Land();
        }
    }

    private void Land()
    {
        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.zero;
            visualTransform.localRotation = Quaternion.identity;
        }
        if (customSortable != null)
        {
            customSortable.SetHeight(0f);
            customSortable.ManualLateUpdate();
        }

        landingDampTime = 0f;
        state = ItemMoveState.Dropped;
        SetGrounded(true);
        SetNeedsUpdate(true); // 착지 스프링 연출 동안은 갱신한다

        // 비행 중에 흡입 대상이 잡혀 있었다면 바로 이어서 빨려간다
        if (suckTarget != null) StartSucking(suckTarget);
    }

    private void UpdateDropped(float _deltaTime)
    {
        if (landingDampTime < LandingDampDuration)
        {
            // 착지 후 스프링 댐퍼 쫀득한 Scale 연출 (LogItem과 동일한 계수)
            landingDampTime += _deltaTime;
            if (visualTransform != null)
            {
                float springEffect = Mathf.Cos(landingDampTime * 25f) * Mathf.Exp(-landingDampTime * 7f) * 0.4f;
                visualTransform.localScale = landingDampTime >= LandingDampDuration
                    ? Vector3.one
                    : new Vector3(1f + springEffect, 1f - springEffect, 1f);
            }
            if (landingDampTime < LandingDampDuration) return;
        }

        // 착지 연출이 끝났다. 수명이 없는 불씨는 흡입이 걸릴 때(SetSuckTarget)까지 갱신할 일이 없다.
        SetNeedsUpdate(bExpires);
    }

    // LogItem.UpdateSucking과 동일한 "뒤로 살짝 튕겼다가 가속해서 빨려드는" 흡입 연출.
    private void UpdateSucking(float _deltaTime)
    {
        if (suckTarget == null || (character != null && character.bDead))
        {
            // 흡입이 취소되면 그 자리 바닥으로 돌아간다
            suckTarget = null;
            transform.localScale = Vector3.one;
            if (visualTransform != null)
            {
                visualTransform.localPosition = Vector3.zero;
                visualTransform.localScale = Vector3.one;
            }
            if (customSortable != null)
            {
                customSortable.SetHeight(0f);
                customSortable.ManualLateUpdate();
            }
            landingDampTime = LandingDampDuration;
            state = ItemMoveState.Dropped;
            SetGrounded(true);
            SetNeedsUpdate(bExpires);
            return;
        }

        Vector3 targetPos = suckTarget.position;
        Vector3 diff = targetPos - transform.position;
        float sqrDistance = diff.sqrMagnitude;

        // 프레임 드랍 방어: 다음 이동 거리가 남은 거리 이상이면 오버슈트하지 않고 바로 획득
        if (suckSpeed > 0f)
        {
            float nextMoveStep = suckSpeed * _deltaTime;
            if (nextMoveStep * nextMoveStep >= sqrDistance || sqrDistance < MinAcquireDist * MinAcquireDist)
            {
                transform.position = targetPos;
                EmberAcquiredEvent?.Invoke(this);
                return;
            }
        }

        if (suckSpeed < 0f)
        {
            suckSpeed += SuckAccel * 2f * _deltaTime; // 뒤로 튕기는 구간
        }
        else
        {
            float dynamicAccel = SuckAccel * 2.5f * (1f + suckSpeed * 0.15f);
            suckSpeed = Mathf.Min(suckSpeed + dynamicAccel * _deltaTime, 35f);
        }

        transform.position += diff.normalized * suckSpeed * _deltaTime;

        if (visualTransform != null)
        {
            visualTransform.localPosition = Vector3.Lerp(visualTransform.localPosition, Vector3.zero, _deltaTime * 10f);
            visualTransform.localScale = Vector3.one;
            if (customSortable != null) customSortable.SetHeight(visualTransform.localPosition.y);
        }

        // 타겟에 매우 가까워지면 전체 스케일 축소 (최소 0.35 유지)
        if (suckSpeed > 0f && sqrDistance < 1.5f * 1.5f)
        {
            transform.localScale = Vector3.one * Mathf.Max(0.35f, Mathf.Sqrt(sqrDistance) / 1.5f);
        }

        CollisionSystem.Instance?.UpdatePosition(this, transform.position);
    }

    /// <summary>
    /// 캐릭터의 아이템 감지가 Item 기반으로 호출한다. 원목과 같이 바닥에 놓인 뒤에만 흡입을 받는다.
    /// 과열이 최대여도 주울 수는 있다(효과만 없다 - InDungeonObjectManager.EmberItemAcquired).
    /// </summary>
    public override void SetSuckTarget(Transform _target)
    {
        if (state != ItemMoveState.Dropped) return;

        StartSucking(_target);
    }

    private void StartSucking(Transform _target)
    {
        if (state == ItemMoveState.Sucking) return;

        suckTarget = _target;
        suckSpeed = -5f; // 뒤로 튕기는 동작
        state = ItemMoveState.Sucking;
        SetGrounded(false);
        SetNeedsUpdate(true);
    }

    private void SetGrounded(bool _bGrounded)
    {
        if (bGrounded == _bGrounded) return;

        bGrounded = _bGrounded;
        EmberGroundedChangedEvent?.Invoke(this, _bGrounded);
    }

    private void SetNeedsUpdate(bool _bNeedsUpdate)
    {
        if (bNeedsUpdate == _bNeedsUpdate) return;

        bNeedsUpdate = _bNeedsUpdate;
        EmberUpdateNeededChangedEvent?.Invoke(this, _bNeedsUpdate);
    }
}
