using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 던전 입장 시 소환되어 캐릭터를 계속 따라다니는 드론. 평소에는 그냥 따라다니기만 하다가,
/// 캐릭터가 공격 키를 누르는 순간(Character.SetbCanAction) Activate가 호출되어 일정 시간(지속시간)
/// 동안 활성화된다. 활성화된 동안에는 Character가 지정해준 나무 하나(다른 드론과 겹치지 않도록
/// Character가 미리 배정한다)를 일정 주기로 계속 공격한다. 부메랑과 달리 투사체로 날아가지 않고
/// 제자리(캐릭터를 따라다니는 위치)에서 사거리 내의 목표를 원거리로 타격하는 방식이다.
///
/// 스프라이트는 CharacterAnimator.GetBaseSprites와 동일한 8방향 dirIndex 규칙(0=R,1=RU,2=U,3=RU 반전,
/// 4=R 반전,5=RD 반전,6=D,7=RD)을 그대로 따른다. 실제로 갖고 있는 건 R/RU/U/RD/D 5방향뿐이고
/// 나머지 3방향은 FlipX로 좌우 반전해서 만든다. 평소엔 이동 방향(따라가는 방향), 공격 모션 재생 중엔
/// 공격 대상 방향을 기준으로 dirIndex를 갱신한다.
///
/// 캐릭터가 배정한 슬롯(followOffset - 캐릭터 조준 반대 방향을 기준으로 한 타원 대형 위치이며,
/// Character가 매 프레임 새로 계산해 갱신해준다)을 목표점으로 삼아 그 자리를 향해 움직인다. 목표점과
/// 거리가 arrivalTolerance 이내면 그 자리에 그대로 머물고, 벗어나면 SmoothDamp(임계 감쇠 스프링)로
/// 속도 0에서 서서히 가속하며 쫓아가다가 다시 가까워지면 서서히 감속하며 멈춘다 - 딱딱하게 순간이동하듯
/// 튀지 않는다. 공격은 damageInterval마다 한 번씩 공격 모션(0~5번, 6프레임)을 재생하되, 실제 데미지는
/// 그 모션이 5번(마지막) 프레임에 도달하는 순간에 딱 한 번만 들어간다.
/// </summary>
public class Drone : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private float followMaxSpeed = 4f; // 슬롯에서 벗어났을 때 쫓아가는 최고 속도
    [SerializeField] private float followSmoothTime = 0.35f; // SmoothDamp 완화 시간 - 클수록 가감속이 더 부드럽고 느긋해진다
    [SerializeField] private float minMoveSqrForFacing = 0.0004f; // 방향 벡터 크기가 이 값보다 작으면 갱신하지 않고 직전 방향을 유지(제자리 떨림 방지)

    [Header("Hover Bob")]
    [SerializeField] private float bobAmplitude = 0.08f; // 위아래로 둥둥 떠다니는 폭
    [SerializeField] private float bobFrequency = 1.4f; // 초당 왕복 횟수
    private float hoverHeight; // 그림자(바닥)와 본체 스프라이트 사이의 고정 간격 - 클수록 더 높이 떠 있는 것처럼 보인다. Character가 대형 슬롯(역할)에 따라 지정한다.

    [Header("Sprite Animation (CharacterAnimator와 동일한 8방향 dirIndex 규칙)")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private List<Sprite> attackR;  // 공격 모션 6프레임(0~5번 열)
    [SerializeField] private List<Sprite> attackRU;
    [SerializeField] private List<Sprite> attackU;
    [SerializeField] private List<Sprite> attackRD;
    [SerializeField] private List<Sprite> attackD;
    [SerializeField] private Sprite idleR; // 각 행의 마지막(6번째) 열 = 해당 방향의 Idle 정지 프레임
    [SerializeField] private Sprite idleRU;
    [SerializeField] private Sprite idleU;
    [SerializeField] private Sprite idleRD;
    [SerializeField] private Sprite idleD;

    [Header("Sprite Animation - Overheat (6~10행, 과열 상태일 때 위 5개 대신 사용)")]
    [SerializeField] private List<Sprite> attackR_Overheat;
    [SerializeField] private List<Sprite> attackRU_Overheat;
    [SerializeField] private List<Sprite> attackU_Overheat;
    [SerializeField] private List<Sprite> attackRD_Overheat;
    [SerializeField] private List<Sprite> attackD_Overheat;
    [SerializeField] private Sprite idleR_Overheat;
    [SerializeField] private Sprite idleRU_Overheat;
    [SerializeField] private Sprite idleU_Overheat;
    [SerializeField] private Sprite idleRD_Overheat;
    [SerializeField] private Sprite idleD_Overheat;

    [SerializeField] private float attackSampleRate = 12f;
    private const int ImpactFrameIndex = 5; // 공격 모션 0~5번 중 5번(마지막) 프레임에서 실제 데미지 판정

    [Header("Shadow (Boomerang/LogItem과 동일한 방식)")]
    [SerializeField] private SpriteRenderer shadowSpriteRenderer; // Shadow Material을 쓰는 별도 렌더러. 본체와 동일한 프레임/FlipX를 매 프레임 그대로 따라간다.

    [Header("Muzzle Points (연쇄 타격 VFX 시작점)")]
    [SerializeField] private Transform muzzleRight;
    [SerializeField] private Transform muzzleRightUp;
    [SerializeField] private Transform muzzleUp;
    [SerializeField] private Transform muzzleRightDown;
    [SerializeField] private Transform muzzleDown;

    [Header("Chain Attack VFX")]
    [SerializeField] private PresentationLayer.VFX.VFX_LightningZap chainZap; // 드론 전용 인스턴스(풀링 없이 상시 보유) - 연쇄 타격 시 muzzle에서 각 나무 top으로 이어지는 번개 연출
    [SerializeField] private Color chainZapNormalColor = Color.yellow;
    [SerializeField] private Color chainZapOverheatColor = Color.red; // 과열 상태(isOverheat)일 때 레이저 색상
    [SerializeField] private float chainZapIntensity = 1f; // HDR Intensity (Inspector HDR 컬러 피커의 Intensity 슬라이더와 동일)

    [Header("Charging VFX (공격 모션 시작 ~ 임팩트 프레임 직전까지 Muzzle에서 Loop 재생)")]
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private string chargingVfxTag = "DroneCharging";
    private ParticleSystem chargingVfx;
    private ParticleSystemRenderer[] chargingVfxRenderers; // chargingVfx 본체 + 자식(VFX_OverHeating 등) 렌더러 전부 - Muzzle Y좌표 기준으로 매 프레임 정렬 순서를 맞춘다

    [Header("Attack Hit VFX (주 타겟/연쇄 타겟 각각 맞은 자리에 1회성 재생)")]
    [SerializeField] private string atkHitVfxTag = "DroneAtkHit";

    private Transform followTarget;
    private Vector3 followOffset; // 캐릭터 기준 목표 슬롯(타원 대형 위치). Character가 매 프레임 갱신해준다.
    private float arrivalTolerance = 0.15f; // 슬롯과 이 거리 이내면 도착한 것으로 보고 멈춘다.
    private float currentFollowSpeed; // 0에서 시작해 SmoothDamp로 부드럽게 가속/감속되는 현재 이동 속도
    private float followSpeedVelocity; // Mathf.SmoothDamp가 내부적으로 쓰는 속도 상태(가감속 곡선을 자연스럽게 만든다)
    private float bobPhase; // 드론마다 다른 위상에서 시작해 여러 대가 똑같이 맞춰서 둥둥 뜨지 않게 한다

    // 타겟을 잃는 그 프레임에 동기적으로 대체 타겟을 요청하는 콜백(Character가 소환 시 등록).
    // 드론끼리 같은 나무를 동시에 물지 않도록 하는 조율은 Character가 계속 담당하되, 응답은
    // 다음 프레임까지 기다리지 않고 즉시 오므로 스윙 애니메이션이 끊기지 않는다.
    private System.Func<Drone, ITreeObj> requestRetarget;

    // 주 타겟에 데미지를 입히는 순간(임팩트 프레임) Character에게 통지하는 콜백(Character가 소환 시
    // 등록). 연쇄공격 전이 대상을 찾는 것도 Character의 공간 검색 책임이므로, Drone은 "누구를
    // 때렸는지"만 알려주고 실제 전이 판정/데미지 적용은 Character가 전담한다.
    private System.Action<Drone, ITreeObj> requestChainAttack;

    private Vector2 lastFacingDir = Vector2.down;
    private Vector2 characterAimDir = Vector2.down; // 캐릭터의 조준 방향. Character가 매 프레임 갱신해준다.
    private int dirIndex = 6; // CharacterAnimator 규칙상 6 = Down
    private bool isOverheat; // 과열 버프(+"드론 과부하" 특성) 상태. Character가 매 프레임 갱신해준다 - true면 6~10행(Overheat 세트)을 사용한다.

    private bool isActive; // 공격 키로 활성화되어 지속시간 동안 대상을 계속 노리는 상태
    private bool isSwinging; // damageInterval마다 한 번씩, 공격 모션이 재생되는 짧은 구간
    private bool prevIsSwinging;
    private bool damageAppliedThisSwing;
    private float activeTimer;
    private float activeDuration;
    private float swingTimer;

    private float damage;
    private float damageInterval;
    private float damageTickTimer;
    private float attackRange;
    private ITreeObj currentTarget;

    /// <summary>
    /// 현재 이 드론이 물고 있는 유효한 타겟. Character가 다음 활성화 때 새로 타겟을 골라줄지 판단하는
    /// 데 쓴다(유효한 타겟을 이미 물고 있으면 Character는 그 나무를 다른 드론에게 주지 않고, 이
    /// 드론에게도 새 타겟을 강제로 바꿔주지 않는다). 내부 필드(currentTarget)를 그대로 노출하지 않고
    /// 조회 시점에 즉시 유효성(죽었는지/묘목으로 리셋됐는지)을 다시 확인한다 - Update() 주기가 한 번
    /// 돌기 전이라도(예: 킬 직후 바로 공격 버튼을 다시 누르는 경우) Character가 이미 무효해진 타겟을
    /// "아직 살아있다"고 착각해 그대로 넘겨받는 일이 없도록 한다.
    /// </summary>
    public ITreeObj CurrentTarget => IsTargetValid(currentTarget) ? currentTarget : null;

    private static bool IsTargetValid(ITreeObj _target)
    {
        if (_target == null || _target.bDead) return false;

        // InDungeonObjectManager.OnTreeDead -> treePool.Release -> OnReleaseTree가 같은 프레임 안에서
        // bDead/bIsSapling을 다시 false로 리셋해버리기 때문에(풀에 반환된 나무를 재사용 준비하는
        // 초기화 과정), bDead 하나만으로는 "방금 죽어서 풀로 돌아간" 나무를 걸러낼 수 없다.
        // 그 시점에 실제로 달라지는 건 GameObject의 활성 여부뿐이라 여기서 반드시 같이 확인한다.
        Transform targetTransform = _target.GetTransform();
        if (targetTransform == null || !targetTransform.gameObject.activeInHierarchy) return false;

        return (_target as IDamageable)?.bCanApplyDamage ?? false;
    }

    private bool isPaused;

    private float frameTimer;
    private int currentFrameIndex;

    private CustomSortable customSortable;

    /// <summary>
    /// DroneCreator가 풀에서 꺼낼 때 호출한다. _followTarget은 매 프레임 위치를 다시 읽으므로
    /// 캐릭터가 이동 중이어도 자연스럽게 따라간다.
    /// </summary>
    public void Spawn(Vector3 _position, Transform _followTarget)
    {
        transform.position = _position;
        transform.localScale = Vector3.one;
        followTarget = _followTarget;
        followOffset = Vector3.zero;
        hoverHeight = 0f;
        currentFollowSpeed = 0f;
        followSpeedVelocity = 0f;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);

        lastFacingDir = Vector2.down;
        characterAimDir = Vector2.down;
        dirIndex = 6;
        isOverheat = false;

        isActive = false;
        isSwinging = false;
        prevIsSwinging = false;
        damageAppliedThisSwing = false;
        activeTimer = 0f;
        swingTimer = 0f;
        damageTickTimer = 0f;
        currentTarget = null;
        StopChargingVfx();

        frameTimer = 0f;
        currentFrameIndex = 0;
        ApplyCurrentFrame();
    }

    /// <summary>
    /// 타겟을 잃는 순간 즉시 대체 타겟을 요청할 콜백. Character가 소환 시 등록한다.
    /// </summary>
    public void SetRetargetCallback(System.Func<Drone, ITreeObj> _callback)
    {
        requestRetarget = _callback;
    }

    /// <summary>
    /// 주 타겟에 데미지를 적용하는 순간마다 호출될 콜백. Character가 소환 시 등록하며, 연쇄공격
    /// 전이 대상 탐색/데미지 적용을 전담한다.
    /// </summary>
    public void SetChainAttackCallback(System.Action<Drone, ITreeObj> _callback)
    {
        requestChainAttack = _callback;
    }

    /// <summary>
    /// 현재 dirIndex(공격 대상을 바라보는 방향)에 해당하는 MuzzlePoints 위치를 월드 좌표로 반환한다.
    /// GetAttackSprites/GetIdleSprite와 동일하게 좌우 반전 방향(3/4/5)은 원본 5방향 Transform의 로컬
    /// x좌표만 미러링해서 구한다(스프라이트 FlipX가 실제 트랜스폼을 뒤집지 않는 것과 동일한 방식).
    /// Character가 연쇄 타격 VFX의 시작점을 계산할 때 호출한다.
    /// </summary>
    public Vector3 GetMuzzlePosition()
    {
        Transform muzzle = GetMuzzleTransform(dirIndex, out bool flipX);
        if (muzzle == null) return transform.position;

        Vector3 localPos = transform.InverseTransformPoint(muzzle.position);
        if (flipX) localPos.x = -localPos.x;
        return transform.TransformPoint(localPos);
    }

    /// <summary>
    /// 연쇄 타격 결과로 얻어진 좌표 목록(muzzle → 각 나무 top 위치)을 이 드론 전용 LightningZap으로
    /// 재생한다. Character.OnDroneChainAttack이 매 임팩트 프레임마다 호출한다.
    /// </summary>
    public void PlayChainZap(IReadOnlyList<Vector3> _points, int _count)
    {
        if (chainZap == null || _count < 2) return;
        chainZap.SetColor(isOverheat ? chainZapOverheatColor : chainZapNormalColor, chainZapIntensity);
        chainZap.PlayZap(_points, _count);
    }

    /// <summary>
    /// 이 드론에게 맞은 나무 위치(top)마다 1회성 피격 이펙트를 재생한다. Character.OnDroneChainAttack이
    /// 주 타겟과 연쇄로 전이된 타겟 각각에 데미지를 적용할 때마다 호출한다 - 한 번의 연쇄공격 안에서
    /// 여러 나무가 동시에 맞을 수 있으므로 chargingVfx와 달리 재생 중인 인스턴스를 따로 추적하지 않고
    /// VFXComponent 풀에서 매번 새로 꺼내 쓴다.
    /// </summary>
    public void PlayAtkHitVfx(Vector3 _position)
    {
        if (vfxComponent == null) return;
        vfxComponent.Play(new VFXPlaySettings(atkHitVfxTag, _position, Quaternion.identity));
    }

    /// <summary>
    /// 목표 슬롯에 얼마나 가까이 있으면 "도착"으로 볼지. Character가 소환 시 지정한다.
    /// </summary>
    public void SetArrivalTolerance(float _tolerance)
    {
        arrivalTolerance = Mathf.Max(_tolerance, 0.01f);
    }

    /// <summary>
    /// 캐릭터 기준 목표 슬롯(타원 대형 위치). Character가 캐릭터의 조준 방향이 바뀔 때마다
    /// 매 프레임 다시 계산해서 갱신해준다 - 그래서 대형 자체가 캐릭터 조준 방향에 맞춰 부드럽게 회전한다.
    /// </summary>
    public void SetFollowOffset(Vector3 _offset)
    {
        followOffset = _offset;
    }

    /// <summary>
    /// 캐릭터의 조준 방향. Character가 매 프레임 갱신해준다 - 공격 모션이 재생 중이 아닐 때
    /// Idle 포즈가 이 방향을 바라보게 하는 데 쓰인다.
    /// </summary>
    public void SetCharacterAimDir(Vector2 _aimDir)
    {
        if (_aimDir.sqrMagnitude > 0.0001f)
        {
            characterAimDir = _aimDir.normalized;
        }
    }

    /// <summary>
    /// 과열 버프 상태(OverheatComponent.IsActive && "드론 과부하" 특성). Character가 매 프레임
    /// 갱신해준다 - true인 동안은 Attack/Idle 스프라이트를 6~10행(Overheat 세트)에서 골라 쓴다.
    /// </summary>
    public void SetOverheatState(bool _isOverheat)
    {
        isOverheat = _isOverheat;
    }

    /// <summary>
    /// 그림자와 본체 스프라이트 사이의 고정 간격(둥둥 뜨는 높이). 대형에서 캐릭터 바로 뒤(꼭짓점)
    /// 슬롯을 맡은 드론만 이 값을 크게 줘서 더 높이 떠 있는 것처럼 보이게 하고, 양옆 슬롯은 0으로
    /// 둬서 같은 높이를 유지한다. Character가 소환 시(대형 역할이 정해질 때) 지정한다.
    /// </summary>
    public void SetHoverHeight(float _hoverHeight)
    {
        hoverHeight = Mathf.Max(_hoverHeight, 0f);
    }

    /// <summary>
    /// 공격 키가 눌렸을 때 Character가 호출한다. 지속시간은 매번 새로 갱신되지만, _target이 지금
    /// 물고 있는 타겟과 같으면(=Character가 살아있는 기존 타겟을 그대로 넘겨준 경우) 진행 중인 공격
    /// 모션이나 다음 타격까지 남은 시간은 건드리지 않는다 - 그래야 스윙 도중 다른 나무로 튀거나
    /// 판정 주기가 끊기지 않는다. _target이 이전과 다를 때(=새로 타겟팅됨)만 그 상태를 초기화한다.
    /// </summary>
    public void Activate(float _damage, float _damageInterval, float _duration, float _attackRange, ITreeObj _target)
    {
        damage = _damage;
        damageInterval = Mathf.Max(_damageInterval, 0.01f);
        activeDuration = Mathf.Max(_duration, 0f);
        attackRange = Mathf.Max(_attackRange, 0f);

        bool isNewTarget = _target != currentTarget;
        currentTarget = _target;

        isActive = true;
        activeTimer = 0f; // 공격 키를 누를 때마다 지속시간은 항상 갱신된다

        if (isNewTarget)
        {
            isSwinging = false;
            swingTimer = 0f;
            damageTickTimer = 0f;
        }
    }

    /// <summary>
    /// 활성화 구간(지속시간) 안에 있는지. Character가 타겟을 잃은 드론에게 자동으로 새 타겟을
    /// 다시 채워줄지 판단하는 데 쓴다(지속시간이 끝난 드론은 다음 공격 키 입력을 기다려야 한다).
    /// </summary>
    public bool IsActive => isActive;

    /// <summary>
    /// Activate()와 달리 지속시간(activeTimer/activeDuration)을 건드리지 않고 타겟만 채워준다.
    /// 원래 타겟이 범위를 벗어나거나 죽어서 currentTarget이 비었을 때, Character가 주변에 다른
    /// 나무를 찾아 여기로 넘겨주면 공격 키를 다시 누르지 않아도 물 흐르듯 다음 나무로 이어서
    /// 공격한다. 이미 타겟이 있는 상태에서는 아무 효과가 없다(원래 타겟을 절대 뺏기지 않는다).
    /// </summary>
    public void AssignTarget(ITreeObj _target)
    {
        if (_target == null || currentTarget != null) return;

        currentTarget = _target;
        isSwinging = false;
        swingTimer = 0f;
        damageTickTimer = 0f;
    }

    /// <summary>
    /// 던전을 나가는 등, 드론을 풀로 돌려보내기 전에 활성 상태를 정리한다.
    /// </summary>
    public void Despawn()
    {
        isActive = false;
        isSwinging = false;
        currentTarget = null;
        followTarget = null;
        StopChargingVfx();
    }

    private void Awake()
    {
        customSortable = GetComponent<CustomSortable>();
        if (customSortable != null)
        {
            customSortable.Initialize(transform);
        }

        chainZap?.SetColor(chainZapNormalColor, chainZapIntensity);
    }

    private void Update()
    {
        if (followTarget == null || isPaused) return;

        UpdateTargetRangeCheck();
        UpdateFollowMovement(Time.deltaTime);
        UpdateBob(Time.deltaTime);
        UpdateSwingTimer(Time.deltaTime); // isSwinging이 이번 프레임에 자연 종료될 수 있으므로 UpdateFacingDirection보다 먼저 실행한다
        UpdateFacingDirection();
        UpdateAnimationFrame(Time.deltaTime);
        UpdateChargingVfxPosition(); // dirIndex가 이번 프레임에 바뀌었을 수 있으므로 UpdateFacingDirection 이후에 위치를 갱신한다

        if (isActive)
        {
            UpdateActiveTimer(Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        if (customSortable != null)
        {
            customSortable.ManualLateUpdate();
        }
    }

    // followOffset(Character가 매 프레임 갱신하는, 캐릭터 조준 반대 방향 기준 타원 대형 슬롯)을
    // 목표점으로 삼는다. 슬롯과 거리가 arrivalTolerance 이내면 도착한 것으로 보고 그 자리에 머물고,
    // 벗어나면 SmoothDamp로 속도 0에서 서서히 가속하며 쫓아가다가 다시 가까워지면 서서히 감속하며
    // 멈춘다. 슬롯 자체는 Character 쪽에서 캐릭터 조준 방향에 맞춰 부드럽게 회전시켜주므로, 여기서는
    // 그 슬롯을 그냥 쫓아가기만 하면 대형이 자연스럽게 캐릭터를 따라 회전한다.
    private void UpdateFollowMovement(float _deltaTime)
    {
        Vector3 targetPos = followTarget.position + followOffset;
        float distToSlot = Vector3.Distance(transform.position, targetPos);

        // 선형 가속 대신 SmoothDamp(임계 감쇠 스프링)를 써서 가감속 곡선 자체가 부드럽게 휘어지도록 한다
        // - 목표 속도(0 또는 최고 속도)로 딱딱하게 꺾이지 않고 자연스럽게 이어진다.
        float targetSpeed = distToSlot > arrivalTolerance ? followMaxSpeed : 0f;
        currentFollowSpeed = Mathf.SmoothDamp(currentFollowSpeed, targetSpeed, ref followSpeedVelocity, followSmoothTime);

        if (currentFollowSpeed <= 0.001f)
        {
            currentFollowSpeed = 0f;
            return; // 완전히 멈췄으면 그 자리에 그대로 머문다
        }

        Vector3 toTarget = targetPos - transform.position;
        float dist = toTarget.magnitude;
        if (dist < 0.0001f)
        {
            currentFollowSpeed = 0f;
            return;
        }

        float step = Mathf.Min(currentFollowSpeed * _deltaTime, dist);
        transform.position += (toTarget / dist) * step;
    }

    // 본체 스프라이트(spriteRenderer)만 위아래로 살짝 흔들어 둥둥 떠다니는 느낌을 낸다. 논리적 위치인
    // transform(루트)은 건드리지 않아서 leash 거리 판정/공격 사거리/CustomSortable 정렬에는 전혀
    // 영향이 없다 - 그림자(shadowSpriteRenderer)는 바닥에 고정된 채라 위에 떠 있는 느낌이 강조된다.
    // 드론마다 bobPhase가 달라서 여러 대가 똑같은 타이밍으로 맞춰 떠다니지 않는다.
    private void UpdateBob(float _deltaTime)
    {
        if (spriteRenderer == null) return;

        bobPhase += _deltaTime * bobFrequency * Mathf.PI * 2f;
        float bobY = hoverHeight + Mathf.Sin(bobPhase) * bobAmplitude;

        Transform visualTransform = spriteRenderer.transform;
        Vector3 localPos = visualTransform.localPosition;
        localPos.y = bobY;
        visualTransform.localPosition = localPos;
    }

    // 공격 모션 재생 중엔 공격 대상 방향을, 그 외엔 캐릭터의 조준 방향(characterAimDir)을 바라보게
    // 한다. 스윙이 막 끝나 목표 방향이 사라졌을 때만 직전 방향을 잠깐 유지해서(dir이 거의 0일 때)
    // 제자리에서 방향이 파르르 떨리지 않게 한다.
    private void UpdateFacingDirection()
    {
        Vector2 dir;

        if (isSwinging && currentTarget != null)
        {
            dir = (Vector2)currentTarget.GetTransform().position - (Vector2)transform.position;
        }
        else
        {
            dir = characterAimDir;
        }

        if (dir.sqrMagnitude >= minMoveSqrForFacing)
        {
            lastFacingDir = dir.normalized;
        }

        float angle = Mathf.Atan2(lastFacingDir.y, lastFacingDir.x) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360f;
        dirIndex = Mathf.RoundToInt(angle / 45f) % 8;
    }

    // 공격 모션(attackSprites)은 damageInterval마다 한 번, 재생 시간(프레임 수 / attackSampleRate)만큼
    // 켜졌다가 저절로 꺼진다. 지속시간(activeDuration) 내내 반복 재생되지 않는다.
    private void UpdateSwingTimer(float _deltaTime)
    {
        if (!isSwinging) return;

        swingTimer += _deltaTime;

        List<Sprite> attackSprites = GetAttackSprites(dirIndex, out _);
        float swingDuration = (attackSprites != null && attackSprites.Count > 0 && attackSampleRate > 0f)
            ? attackSprites.Count / attackSampleRate
            : 0f;

        if (swingTimer >= swingDuration)
        {
            isSwinging = false;
            StopChargingVfx(); // 정상적으로는 임팩트 프레임에서 이미 꺼졌겠지만, 만약을 대비한 안전망
        }
    }

    private void UpdateActiveTimer(float _deltaTime)
    {
        activeTimer += _deltaTime;
        if (activeTimer >= activeDuration)
        {
            isActive = false;
            isSwinging = false;
            currentTarget = null;
            StopChargingVfx();
            return;
        }

        damageTickTimer += _deltaTime;
        if (damageTickTimer < damageInterval) return;
        damageTickTimer -= damageInterval;

        StartSwing();
    }

    // 죽었거나(또는 죽은 뒤 그루터기->묘목으로 리셋되어 bDead가 다시 false로 돌아간 경우 포함,
    // TreeObj.ResetTree/SetIsSapling 참고) 공격 범위를 벗어난 타겟은 매 프레임 여기서 걸러낸다.
    // 유효한 동안은 currentTarget이 그대로 유지되어, 더 가까운 나무가 나타나도 바뀌지 않는다.
    //
    // 무효해지는 순간, Idle로 빠지기 전에 requestRetarget 콜백으로 그 자리에서 즉시 대체 타겟을
    // 요청한다 - 대체 타겟을 찾으면 currentTarget만 바꿔치기해서, 공격 모션이 끊기지 않고 방향만
    // 자연스럽게 새 나무 쪽으로 바뀐다(UpdateFacingDirection이 currentTarget 방향을 다시 계산한다).
    // 주변에 정말 대체할 나무가 없을 때만(콜백이 null을 반환) Idle로 취소된다.
    private void UpdateTargetRangeCheck()
    {
        if (currentTarget == null) return;

        if (IsTargetValid(currentTarget))
        {
            float distSqr = ((Vector2)currentTarget.GetTransform().position - (Vector2)transform.position).sqrMagnitude;
            if (distSqr <= attackRange * attackRange) return; // 아직 유효하고 범위 안 - 그대로 유지
        }

        currentTarget = requestRetarget?.Invoke(this);

        if (currentTarget == null)
        {
            isSwinging = false;
            StopChargingVfx();
        }
        // 대체 타겟을 찾았다면 isSwinging/swingTimer/currentFrameIndex는 전혀 건드리지 않는다.
        // UpdateAnimationFrame이 방향 전환 자체로는 프레임을 리셋하지 않으므로(스윙 중일 때는
        // isSwinging 전환에만 반응), 지금 재생 중이던 프레임 번호 그대로 새 방향의 스프라이트로
        // 이어서 재생된다 - 물 흐르듯 방향만 바뀌어 공격이 계속된다.
    }

    private void StartSwing()
    {
        if (!IsTargetValid(currentTarget)) return;

        isSwinging = true;
        swingTimer = 0f;
        damageAppliedThisSwing = false;
        PlayChargingVfx(); // 공격 모션이 시작되는 이 시점부터 임팩트 프레임 직전까지 Muzzle에서 루프 재생
    }

    private void ApplyDamageToCurrentTarget()
    {
        if (currentTarget == null || currentTarget.bDead) return;

        (currentTarget as IDamageable)?.TakeDamage(damage);
        requestChainAttack?.Invoke(this, currentTarget);
    }

    // Character.SetChainAttackCallback으로 등록된 requestChainAttack과 별개로, 드론 자신이 실제로
    // "공격하는" 순간(임팩트 프레임)에 맞춰 충전 이펙트를 끈다. ApplyDamageToCurrentTarget은 타겟이
    // 이미 무효해진 경우 아무 일도 하지 않고 조용히 반환하므로, 충전 이펙트 정지는 타겟 유효성과
    // 무관하게 임팩트 프레임 도달 자체에 걸어야 확실히 꺼진다(UpdateAnimationFrame 참고).
    private void PlayChargingVfx()
    {
        if (vfxComponent == null || chargingVfx != null) return;

        // spriteRenderer.transform(Visual)의 자식으로 붙여 Hierarchy상 드론 소속으로 정리해둔다.
        // 다만 위치 추적 자체는 부모-자식 상속에 기대지 않고 UpdateChargingVfxPosition이 매 프레임
        // 직접 재계산해서 강제로 맞춘다 - 상속에만 맡겼더니 드론이 이동 중일 때 이펙트가 따라오지
        // 못하고 뒤에 남는 현상이 있었다.
        Transform parent = spriteRenderer != null ? spriteRenderer.transform : transform;
        chargingVfx = vfxComponent.Play(new VFXPlaySettings(chargingVfxTag, GetMuzzlePosition(), Quaternion.identity, parent));

        // 본체 + 자식(VFX_OverHeating 등)의 ParticleSystemRenderer를 전부 캐싱해둔다 - CustomSortable이
        // SpriteRenderer만 자동 수집하므로(Drone.Awake), 파티클 렌더러는 정렬 순서를 직접 챙겨줘야 한다.
        if (chargingVfx != null)
        {
            chargingVfxRenderers = chargingVfx.GetComponentsInChildren<ParticleSystemRenderer>(true);
        }
    }

    private void StopChargingVfx()
    {
        if (vfxComponent == null || chargingVfx == null) return;
        vfxComponent.Stop(chargingVfx, true);
        chargingVfx = null;
        chargingVfxRenderers = null;
    }

    // dirIndex(공격 대상을 바라보는 방향)가 스윙 도중 바뀔 수 있으므로(재타겟팅), 재생 중인 동안
    // 매 프레임 Muzzle 위치로 다시 옮겨 따라가게 한다. 같은 김에 정렬 순서도 그 Muzzle의 Y좌표 기준으로
    // 맞춘다 - CustomSortable.ComputeSortingOrder에 드론 본체와 동일한 precision/offset 규칙을 그대로
    // 위임하므로, 드론 스프라이트와 항상 같은 기준으로 앞뒤가 맞는다.
    private void UpdateChargingVfxPosition()
    {
        if (chargingVfx == null) return;

        Vector3 muzzlePos = GetMuzzlePosition();
        chargingVfx.transform.position = muzzlePos;

        if (chargingVfxRenderers != null && customSortable != null)
        {
            int order = customSortable.ComputeSortingOrder(muzzlePos.y);
            for (int i = 0; i < chargingVfxRenderers.Length; i++)
            {
                if (chargingVfxRenderers[i] != null)
                {
                    chargingVfxRenderers[i].sortingOrder = order;
                }
            }
        }
    }

    private void UpdateAnimationFrame(float _deltaTime)
    {
        // 스윙이 새로 시작/종료될 때만 프레임을 0으로 되돌린다. 스윙 도중 방향(dirIndex)만 바뀌는
        // 경우(재타겟팅으로 다른 나무를 보게 됨)는 일부러 리셋하지 않는다 - 지금 재생 중이던 프레임
        // 번호를 그대로 유지한 채 그 프레임에 해당하는 새 방향의 스프라이트로 갈아 끼워서, 공격
        // 모션이 처음부터 다시 재생되지 않고 물 흐르듯 방향만 바뀌며 이어지게 한다.
        if (isSwinging != prevIsSwinging)
        {
            currentFrameIndex = 0;
            frameTimer = 0f;
            prevIsSwinging = isSwinging;
        }

        if (isSwinging)
        {
            List<Sprite> attackSprites = GetAttackSprites(dirIndex, out bool flipX);
            if (attackSprites == null || attackSprites.Count == 0) return;

            float frameTime = attackSampleRate > 0f ? 1f / attackSampleRate : 0.1f;
            frameTimer += _deltaTime;
            if (frameTimer >= frameTime)
            {
                frameTimer -= frameTime;
                currentFrameIndex = Mathf.Min(currentFrameIndex + 1, attackSprites.Count - 1); // 한 번만 재생하고 마지막 프레임에서 멈춘다(반복 없음)
            }

            // 공격 모션이 5번(마지막) 프레임에 도달하는 그 순간에만 실제 타격 판정을 1회 적용한다.
            int impactFrame = Mathf.Min(ImpactFrameIndex, attackSprites.Count - 1);
            if (!damageAppliedThisSwing && currentFrameIndex >= impactFrame)
            {
                StopChargingVfx(); // 실제로 "공격하는" 순간 - 타겟 유효성과 무관하게 항상 여기서 끈다
                ApplyDamageToCurrentTarget();
                damageAppliedThisSwing = true;
            }

            ApplyFrame(attackSprites[Mathf.Clamp(currentFrameIndex, 0, attackSprites.Count - 1)], flipX);
        }
        else
        {
            Sprite idleSprite = GetIdleSprite(dirIndex, out bool flipX);
            ApplyFrame(idleSprite, flipX);
        }
    }

    private void ApplyCurrentFrame()
    {
        Sprite idleSprite = GetIdleSprite(dirIndex, out bool flipX);
        ApplyFrame(idleSprite, flipX);
    }

    private void ApplyFrame(Sprite _sprite, bool _flipX)
    {
        if (spriteRenderer == null || _sprite == null) return;
        spriteRenderer.sprite = _sprite;
        spriteRenderer.flipX = _flipX;

        // Shadow Material이 SpriteRenderer.flipX를 무시하므로 localScale.x로 뒤집는다.
        if (shadowSpriteRenderer != null)
        {
            shadowSpriteRenderer.sprite = _sprite;
            Transform shadowTf = shadowSpriteRenderer.transform;
            Vector3 scale = shadowTf.localScale;
            scale.x = _flipX ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
            shadowTf.localScale = scale;
        }
    }

    // CharacterAnimator.GetBaseSprites와 동일한 규칙: R/RU/U/RD/D 5방향만 원본으로 갖고 있고,
    // 나머지(RU 반전=좌상단, R 반전=좌측, RD 반전=좌하단) 3방향은 FlipX로 만든다.
    // isOverheat이 true면(과열 버프 + "드론 과부하" 특성) 같은 5방향의 Overheat 세트(6~10행)를 대신 쓴다.
    private List<Sprite> GetAttackSprites(int _dirIndex, out bool _flipX)
    {
        _flipX = false;
        if (isOverheat)
        {
            switch (_dirIndex)
            {
                case 0: return attackR_Overheat;
                case 1: return attackRU_Overheat;
                case 2: return attackU_Overheat;
                case 3: _flipX = true; return attackRU_Overheat;
                case 4: _flipX = true; return attackR_Overheat;
                case 5: _flipX = true; return attackRD_Overheat;
                case 6: return attackD_Overheat;
                case 7: return attackRD_Overheat;
            }
            return null;
        }

        switch (_dirIndex)
        {
            case 0: return attackR;
            case 1: return attackRU;
            case 2: return attackU;
            case 3: _flipX = true; return attackRU;
            case 4: _flipX = true; return attackR;
            case 5: _flipX = true; return attackRD;
            case 6: return attackD;
            case 7: return attackRD;
        }
        return null;
    }

    private Sprite GetIdleSprite(int _dirIndex, out bool _flipX)
    {
        _flipX = false;
        if (isOverheat)
        {
            switch (_dirIndex)
            {
                case 0: return idleR_Overheat;
                case 1: return idleRU_Overheat;
                case 2: return idleU_Overheat;
                case 3: _flipX = true; return idleRU_Overheat;
                case 4: _flipX = true; return idleR_Overheat;
                case 5: _flipX = true; return idleRD_Overheat;
                case 6: return idleD_Overheat;
                case 7: return idleRD_Overheat;
            }
            return null;
        }

        switch (_dirIndex)
        {
            case 0: return idleR;
            case 1: return idleRU;
            case 2: return idleU;
            case 3: _flipX = true; return idleRU;
            case 4: _flipX = true; return idleR;
            case 5: _flipX = true; return idleRD;
            case 6: return idleD;
            case 7: return idleRD;
        }
        return null;
    }

    // GetAttackSprites/GetIdleSprite와 동일한 dirIndex 규칙: R/RU/U/RD/D 5개 Transform만 원본으로 갖고
    // 있고, 나머지(좌상단/좌측/좌하단) 3방향은 GetMuzzlePosition에서 로컬 x좌표를 미러링해서 만든다.
    private Transform GetMuzzleTransform(int _dirIndex, out bool _flipX)
    {
        _flipX = false;
        switch (_dirIndex)
        {
            case 0: return muzzleRight;
            case 1: return muzzleRightUp;
            case 2: return muzzleUp;
            case 3: _flipX = true; return muzzleRightUp;
            case 4: _flipX = true; return muzzleRight;
            case 5: _flipX = true; return muzzleRightDown;
            case 6: return muzzleDown;
            case 7: return muzzleRightDown;
        }
        return null;
    }
}
