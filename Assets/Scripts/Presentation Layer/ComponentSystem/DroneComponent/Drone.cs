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
/// 나머지 3방향은 FlipX로 좌우 반전해서 만든다. 활성 상태에서 공격 대상이 있으면 그 대상 방향을,
/// 그 외엔 캐릭터의 조준 방향을 목표로 삼되, 실제 각도는 MoveTowardsAngle로 서서히 돌려 dirIndex가
/// 중간 방향을 거쳐 단계적으로 바뀐다(UpdateFacingDirection 참고).
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
    [SerializeField] private float teleportSnapDistance = 3f; // 캐릭터(followTarget)가 한 프레임에 이 거리 이상 이동했으면 걷기가 아닌 순간이동(던전 시작 위치 배치, 차량 승/하차 등)으로 보고 드론을 슬롯에 즉시 스냅한다
    [SerializeField] private float minMoveSqrForFacing = 0.0004f; // 방향 벡터 크기가 이 값보다 작으면 갱신하지 않고 직전 방향을 유지(제자리 떨림 방지)

    [Header("Facing Turn (8방향 스프라이트가 한 번에 튀지 않고 중간 방향을 거쳐 돌아가도록 각도를 보간)")]
    [SerializeField] private float facingReturnTurnSpeed = 900f; // 공격이 끝나 캐릭터 조준 방향으로 되돌아올 때 각속도(도/초). 900 = 45도(한 섹터)에 0.05초, 60fps에서 한 장당 3프레임 - 중간 방향이 눈에 읽히는 상한
    [SerializeField] private float facingTargetTurnSpeed = 900f; // 공격 대상을 향해 돌아설 때 각속도(도/초). 기본은 복귀와 같은 '읽히는 최대 속도'이고, 타격 시점이 촉박할 때만 GetTimeUntilImpact 기준으로 자동으로 빨라진다
    [SerializeField] private float facingDirHysteresisDeg = 6f; // 45도 섹터 경계에서 이만큼 더 넘어가야 dirIndex를 바꾼다(경계 근처에서 두 스프라이트 사이를 떨지 않게)
    [SerializeField] private float facingMinDwellTime = 0.05f; // 한 방향 스프라이트를 최소 이 시간(초)은 유지한다. 어떤 회전 속도에서도 중간 방향이 최소 3프레임은 보이도록 보장(타격이 촉박할 때는 무시)
    [SerializeField] private float facingImpactSafetyMargin = 0.05f; // 타격 프레임보다 이만큼(초) 먼저 총구 방향이 맞도록 회전을 마친다
    private float facingDwellTimer; // 현재 dirIndex를 보여준 시간(초)

    [Header("Attack Timing")]
    [SerializeField] private float firstShotDelay = 0.2f; // 새 타겟을 물었을 때 첫 스윙까지의 짧은 예열 시간(초). 180도 회전(facingTargetTurnSpeed 900 기준 0.2초)이 충전 이펙트가 켜지기 전에 끝나도록 맞춘 값. 예전엔 damageInterval(기본 1초)을 꽉 채워 기다렸다
    [SerializeField] private float attackStaggerJitter = 0.05f; // 새 타겟을 물 때마다 첫 발에 0~이 값(초)의 무작위 지연을 더한다. 드론별 고정 위상(attackPhaseOffset)에 얹혀 사격 리듬이 매번 미세하게 달라진다
    private float attackPhaseOffset; // 드론별 고정 첫 발 지연(초). Character가 소환 시 슬롯 순서대로 계단식으로 지정해 여러 대가 같은 프레임에 쏘지 않게 한다. 이후 사격은 damageInterval 주기를 그대로 따르므로 이 위상 차가 계속 유지된다
    [SerializeField] private float targetReleaseRangeMultiplier = 1.2f; // 타겟 해제 거리 = attackRange × 이 값. 획득(attackRange)보다 넉넉하게 잡아 사거리 경계에 걸린 나무가 프레임마다 들락날락하지 않게 한다(히스테리시스)

    [Header("Hover Bob")]
    [SerializeField] private float bobAmplitude = 0.08f; // 위아래로 둥둥 떠다니는 폭
    [SerializeField] private float bobFrequency = 1.4f; // 초당 왕복 횟수

    [Header("Shadow (고도에 따라 그림자 크기 변화)")]
    [SerializeField] private float shadowShrinkAltitude = 0.5f; // 본체가 바닥에서 이 높이까지 떠오르는 동안 그림자가 shadowMinScale까지 선형으로 줄어든다. 고도 0(바닥)에서 그림자 원래 크기
    [SerializeField] private float shadowMinScale = 0.55f; // 그림자가 줄어드는 하한 배율(원래 크기 기준). 1이면 변화 없음
    private Vector3 shadowBaseScale = Vector3.one; // 프리팹에 설정된 그림자 원래 스케일(절댓값). 최대 크기 = 이 값
    private bool shadowFlipX; // 현재 프레임의 좌우 반전 여부. Shadow Material이 SpriteRenderer.flipX를 무시하므로 스케일 x 부호로 뒤집는다
    private float currentAltitude; // 이번 프레임의 본체 고도(바닥 기준, 0 이상). UpdateBob이 갱신하고 그림자 스케일 계산에 쓴다

    [Header("Spawn / Power Down / Individual Variation")]
    [SerializeField] private float spawnScaleDuration = 0.2f; // 소환 시 0 -> 1로 커지는 스케일 인 시간(초). 한 프레임에 팝 하고 나타나지 않게 한다
    [SerializeField] private float spawnScaleOvershoot = 1.15f; // 스케일 인 중간에 살짝 넘쳤다가 1로 돌아오는 정도(뽀잉). 1이면 오버슈트 없음
    [SerializeField] private float powerDownHoverMultiplier = 0.35f; // 캐릭터 사망 시 호버 높이를 이 배율로 낮춘다("전원이 약해진" 느낌)
    [SerializeField] private float powerDownBobMultiplier = 0.3f; // 캐릭터 사망 시 흔들림 진폭/주기를 이 배율로 줄인다
    [SerializeField] private float powerTransitionSpeed = 2f; // 전원 상태(0~1)가 바뀔 때의 전환 속도(초당). 2면 0.5초에 걸쳐 서서히 바뀐다
    [SerializeField] private float individualVariation = 0.15f; // 드론마다 회전 각속도/흔들림 주기/추종 완화 시간에 주는 무작위 편차(±비율). 세 대가 기계처럼 똑같이 움직이지 않게 한다
    [SerializeField] private float faceMoveSpeedThreshold = 1.2f; // 슬롯을 향해 이 속도 이상으로 이동 중이면 조준 방향 대신 이동 방향을 바라본다(옆으로 미끄러지는 인상 방지)
    private float spawnScaleTimer; // 스케일 인 진행 시간. spawnScaleDuration 이상이면 완료
    private float powerLevel = 1f; // 현재 전원 상태(1 = 정상, 0 = 완전 다운). powerTarget으로 서서히 수렴한다
    private float powerTarget = 1f;
    private float varTurnMul = 1f; // 이 드론 고유의 복귀 회전 각속도 배율(소환 시 무작위)
    private float varBobMul = 1f; // 이 드론 고유의 흔들림 주기 배율(소환 시 무작위)
    private float varFollowMul = 1f; // 이 드론 고유의 추종 완화 시간 배율(소환 시 무작위)
    private Vector2 lastMoveDir = Vector2.down; // 직전에 슬롯을 향해 실제로 이동한 방향. 이동 중 시선 결정에 쓴다
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
    [SerializeField] private PresentationLayer.VFX.VFX_DroneLaser chainZap; // 드론 전용 인스턴스(풀링 없이 상시 보유) - 연쇄 타격 시 muzzle에서 각 나무 top으로 이어지는 픽셀 번개 연출
    [SerializeField] private Color chainZapNormalColor = Color.yellow;
    [SerializeField] private Color chainZapOverheatColor = Color.red; // 과열 상태(isOverheat)일 때 레이저 색상
    [SerializeField] private float chainZapIntensity = 1f; // HDR Intensity (Inspector HDR 컬러 피커의 Intensity 슬라이더와 동일)

    [Header("Charging VFX (공격 모션 시작 ~ 임팩트 프레임 직전까지 Muzzle에서 Loop 재생)")]
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private string chargingVfxTag = "DroneCharging";
    private ParticleSystem chargingVfx;
    private Transform chargingVfxParent; // 재생 시 붙인 부모(Visual). 풀이 회수해 다른 곳에 재발급한 인스턴스를 우리 것으로 착각하지 않도록 소유 확인에 쓴다
    private float chargingVfxMaxLifetime; // 재생 시 한 번 계산한 파티클(자식 포함) 최대 수명(초). 취소 페이드 대기 시간의 기준
    private bool bChargingVfxFading; // 발사 또는 취소로 방출만 멈춘 채 남은 입자가 사라지길 기다리는 중. 풀 반환은 chargingVfxFadeTimer가 끝나면 드론이 직접 한다
    private float chargingVfxFadeTimer; // 페이드 잔여 시간(초). 파티클(자식 포함) 최대 수명 + 여유
    private ParticleSystemRenderer[] chargingVfxRenderers; // chargingVfx 루트 렌더러 - Muzzle Y좌표 기준으로 매 프레임 정렬 순서를 맞춘다(픽셀 소용돌이 메쉬가 이 값 + 1로 그려진다)
    private PresentationLayer.VFX.VFX_ChargeVortex chargingVortex; // 충전 이펙트의 픽셀 소용돌이. 과열 색 전환과 발사 연출(Release)을 전달한다
    // 풀 인스턴스(DroneCharging 하드 캡 3개)별 자식 조회 결과. 계층이 고정이라 처음 한 번만 훑고 재사용한다(스윙마다 배열 2개 + 계층 탐색 3회 절약).
    private struct ChargingVfxCacheEntry
    {
        public ParticleSystemRenderer[] renderers;
        public PresentationLayer.VFX.VFX_ChargeVortex vortex;
        public float maxLifetime;
    }
    private readonly Dictionary<ParticleSystem, ChargingVfxCacheEntry> chargingVfxCache = new Dictionary<ParticleSystem, ChargingVfxCacheEntry>(4);
    // 피격 이펙트 풀 인스턴스별 VFX_LaserHit 조회 결과(연쇄 타격마다 GetComponentInChildren을 반복하지 않도록).
    private readonly Dictionary<ParticleSystem, PresentationLayer.VFX.VFX_LaserHit> atkHitVfxCache = new Dictionary<ParticleSystem, PresentationLayer.VFX.VFX_LaserHit>(16);
    // ApplyFrame이 마지막으로 렌더러에 넣은 프레임/반전 - 같으면 세터 호출을 건너뛴다
    private Sprite appliedFrameSprite;
    private bool appliedFrameFlipX;
    private bool bChargingVfxReleased; // 페이드 원인이 발사(흡수 연출)인지. 다음 충전이 시작될 때 발사 연출은 끝까지 재생하게 은퇴 슬롯으로 넘기고, 취소 소화는 즉시 끊는다
    private ParticleSystem retiredChargingVfx; // 새 충전에 자리를 넘기고 발사 연출을 마무리 중인 직전 이펙트(은퇴 슬롯, 하나만 쓴다)
    private Transform retiredChargingVfxParent; // 은퇴 이펙트의 소유 확인용 부모(chargingVfxParent와 같은 역할)
    private ParticleSystemRenderer[] retiredChargingVfxRenderers; // 은퇴 이펙트도 본체와 앞뒤가 맞도록 정렬 순서를 계속 맞춘다
    private float retiredChargingVfxTimer; // 은퇴 이펙트의 남은 페이드 시간(초). 끝나면 풀로 돌려보낸다

    [Header("Overheat Aura VFX (과열 상태 - 드론 몸 실루엣을 따라 감싸는 은은한 푸른 아우라)")]
    [SerializeField] private string overheatAuraTag = "DroneOverheatAura";
    private ParticleSystem overheatAuraVfx;
    private ParticleSystemRenderer overheatAuraRenderer; // 소화 연출 중에도 참조를 유지해서 끝날 때까지 위치/정렬 추적을 계속한다

    [Header("Attack Hit VFX (주 타겟/연쇄 타겟 각각 맞은 자리에 1회성 재생)")]
    [SerializeField] private string atkHitVfxTag = "DroneAtkHit";

    [Header("Charge / Fire Sound")]
    [SerializeField] private float chargeSoundStartPitch = 0.25f; // 충전 시작 피치. 매우 낮게 깔고 시작해서
    [SerializeField] private float chargeSoundEndPitch = 0.6f; // 임팩트 프레임에 이 피치에 도달하도록 계속 올린다
    [SerializeField] private float chargeSoundCancelFadeTime = 0.15f; // 헛스윙 취소 시 충전음이 전원 꺼지듯 잦아드는 시간(초)
    private AudioHandle chargeSoundHandle = AudioHandle.Invalid; // 총구를 따라다니는 3D 충전음(SFX_ChargeUp). 볼륨은 AudioDatabase에서 잡는다

    private Transform followTarget;
    private Vector3 followOffset; // 캐릭터 기준 목표 슬롯(타원 대형 위치). Character가 매 프레임 갱신해준다.
    private float arrivalTolerance = 0.15f; // 슬롯과 이 거리 이내면 도착한 것으로 보고 멈춘다.
    private float currentFollowSpeed; // 0에서 시작해 SmoothDamp로 부드럽게 가속/감속되는 현재 이동 속도
    private float followSpeedVelocity; // Mathf.SmoothDamp가 내부적으로 쓰는 속도 상태(가감속 곡선을 자연스럽게 만든다)
    private Vector3 lastFollowTargetPos; // 직전 프레임의 followTarget 위치. 한 프레임 이동량으로 순간이동을 감지하는 데 쓴다
    private bool bSnapToSlotPending; // 다음 UpdateFollowMovement에서 슬롯 위치로 즉시 스냅해야 하는지(소환 직후/순간이동 직후)
    private float bobPhase; // 드론마다 다른 위상에서 시작해 여러 대가 똑같이 맞춰서 둥둥 뜨지 않게 한다

    // 타겟을 잃는 그 프레임에 동기적으로 대체 타겟을 요청하는 콜백(Character가 소환 시 등록).
    // 드론끼리 같은 나무를 동시에 물지 않도록 하는 조율은 Character가 계속 담당하되, 응답은
    // 다음 프레임까지 기다리지 않고 즉시 오므로 스윙 애니메이션이 끊기지 않는다.
    private System.Func<Drone, ITreeObj> requestRetarget;

    // 주 타겟에 데미지를 입히는 순간(임팩트 프레임) Character에게 통지하는 콜백(Character가 소환 시
    // 등록). 연쇄공격 전이 대상을 찾는 것도 Character의 공간 검색 책임이므로, Drone은 "누구를
    // 때렸는지"만 알려주고 실제 전이 판정/데미지 적용은 Character가 전담한다.
    private System.Action<Drone, ITreeObj> requestChainAttack;

    private Vector2 lastFacingDir = Vector2.down; // 목표로 삼는 방향(즉시 갱신). 실제 스프라이트 방향은 facingAngle이 이 방향으로 서서히 돌아가며 따라간다
    private float facingAngle = 270f; // 현재 실제로 바라보는 각도(도, 0=R 반시계). MoveTowardsAngle로 lastFacingDir 각도까지 보간된다. 270 = Down
    private Vector2 characterAimDir = Vector2.down; // 캐릭터의 조준 방향. Character가 매 프레임 갱신해준다.
    private int dirIndex = 6; // CharacterAnimator 규칙상 6 = Down
    private bool isOverheat; // 과열 버프(+"드론 과부하" 특성) 상태. Character가 매 프레임 갱신해준다 - true면 6~10행(Overheat 세트)을 사용한다.

    private bool isActive; // 공격 키로 활성화되어 지속시간 동안 대상을 계속 노리는 상태
    private bool isSwinging; // damageInterval마다 한 번씩, 공격 모션이 재생되는 짧은 구간
    private bool prevIsSwinging;
    private bool damageAppliedThisSwing;
    private bool bDrySwing; // 스윙 도중 타겟을 잃거나 사망/귀환으로 취소된 "헛스윙". 모션은 끝까지 재생하되 타격 프레임에서 데미지/연쇄만 건너뛴다
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
        BeginSpawnScale(); // spawnScaleDuration 동안 0 -> 1로 키운다(0 이하면 즉시 1)
        powerLevel = 1f;
        powerTarget = 1f;
        lastMoveDir = Vector2.down;

        // 드론마다 미세하게 다른 성격을 준다 - 회전이 조금 빠르거나 느리고, 흔들림 주기가 조금 다르고,
        // 슬롯을 따라가는 완화 시간이 조금 다르다. 세 대가 완전히 같은 타이밍으로 움직이는 기계적인 인상을 없앤다.
        float variation = Mathf.Clamp01(individualVariation);
        varTurnMul = Random.Range(1f - variation, 1f + variation);
        varBobMul = Random.Range(1f - variation, 1f + variation);
        varFollowMul = Random.Range(1f - variation, 1f + variation);

        followTarget = _followTarget;
        followOffset = Vector3.zero;
        hoverHeight = 0f;
        attackPhaseOffset = 0f;
        currentFollowSpeed = 0f;
        followSpeedVelocity = 0f;
        lastFollowTargetPos = _followTarget != null ? _followTarget.position : _position;
        // 소환 시점의 캐릭터 위치는 아직 던전 시작 위치가 아닐 수 있다(SetWhereIsCharacter → SpawnDrones가
        // DungeonStartSignal → SetCharacterPos보다 먼저 실행된다). Character가 SetFollowOffset으로 슬롯을
        // 배정한 뒤 첫 Update에서 슬롯 위치에 바로 놓이도록 스냅을 예약해, 엉뚱한 좌표에서 쫓아오지 않게 한다.
        bSnapToSlotPending = true;
        bobPhase = Random.Range(0f, Mathf.PI * 2f);

        lastFacingDir = Vector2.down;
        facingAngle = 270f;
        facingDwellTimer = facingMinDwellTime; // 첫 방향 전환이 유지 시간에 걸려 늦어지지 않게
        characterAimDir = Vector2.down;
        dirIndex = 6;
        isOverheat = false;

        isActive = false;
        isSwinging = false;
        bDrySwing = false;
        prevIsSwinging = false;
        damageAppliedThisSwing = false;
        activeTimer = 0f;
        swingTimer = 0f;
        damageTickTimer = 0f;
        currentTarget = null;
        // 경고 UI에서 귀환을 확정하면 Pause만 걸린 채 풀로 돌아온다(Resume은 취소 시에만 호출). 여기서 풀지 않으면
        // 다음 던전에서 같은 인스턴스가 Update 첫 줄에서 계속 빠져나가 스케일 0으로 이전 좌표에 갇힌다.
        isPaused = false;
        StopAllChargingVfx();
        StopOverheatAura(true);
        StopChargeSound(false);

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
        ParticleSystem hitVfx = vfxComponent.Play(new VFXPlaySettings(atkHitVfxTag, _position, Quaternion.identity));
        if (hitVfx == null) return;

        // Play가 같은 프레임에 활성화(OnEnable에서 색 초기화)한 뒤이므로 여기서 과열 색(평소 노랑, 과열 파랑)을 덮어쓴다
        if (!atkHitVfxCache.TryGetValue(hitVfx, out PresentationLayer.VFX.VFX_LaserHit laserHit))
        {
            laserHit = hitVfx.GetComponentInChildren<PresentationLayer.VFX.VFX_LaserHit>(true);
            atkHitVfxCache[hitVfx] = laserHit;
        }
        if (laserHit != null) laserHit.SetOverheat(isOverheat);
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
        bool bChanged = isOverheat != _isOverheat;
        isOverheat = _isOverheat;

        // 상태가 바뀔 때만 아우라를 켜고 끈다(Character가 매 프레임 같은 값을 넘겨주므로)
        if (!bChanged) return;

        if (chargingVortex != null) chargingVortex.SetOverheat(isOverheat); // 충전 도중 과열이 바뀌어도 색이 바로 따라간다
        if (isOverheat) StartOverheatAura();
        else StopOverheatAura(false);
    }

    /// <summary>
    /// 캐릭터 사망 시 true - 호버 높이를 낮추고 흔들림을 줄여 "전원이 약해진" 상태로 서서히 전환한다.
    /// 재시작(Character.ResetStatus) 시 false로 되돌리면 다시 서서히 정상 호버로 올라온다.
    /// </summary>
    public void SetPoweredDown(bool _poweredDown)
    {
        powerTarget = _poweredDown ? 0f : 1f;
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
            BeginAttackOnNewTarget();
        }
    }

    // 새 타겟을 물었을 때의 공통 처리. 헛스윙(타겟을 잃고 모션만 마무리 중)이 진행 중이면 모션을 끊지 않고 그
    // 스윙을 새 타겟에 대한 실제 공격으로 되살린다 - 임팩트 전이면 충전 이펙트를 다시 켜고 임팩트에서 타격까지
    // 들어가며, 이 스윙이 "첫 발"이 되므로 다음 스윙은 평소 주기 뒤에 온다. 임팩트가 이미 지났으면 모션만 끝까지
    // 재생하고 첫 발은 firstShotDelay 뒤에 나간다. 헛스윙이 아니면 예전처럼 스윙을 접고 첫 발을 예약한다.
    private void BeginAttackOnNewTarget()
    {
        if (isSwinging && bDrySwing)
        {
            bDrySwing = false;
            if (!damageAppliedThisSwing)
            {
                PlayChargingVfx(); // 취소 소화 중인 직전 이펙트는 PlayChargingVfx가 새 인스턴스를 꺼낸 뒤에 정리한다
                PlayChargeSound();
                damageTickTimer = 0f;
                return;
            }
            damageTickTimer = GetFirstShotTickTimer();
            return;
        }

        StopChargeSound(true);
        isSwinging = false;
        swingTimer = 0f;
        damageTickTimer = GetFirstShotTickTimer();
    }

    // 새 타겟을 물었을 때 damageTickTimer를 "거의 다 찬" 상태로 시작시켜, 첫 스윙이 firstShotDelay 뒤에
    // 바로 나가게 한다. 이후 스윙은 평소처럼 damageInterval 주기로 이어진다. 그 사이 UpdateFacingDirection이
    // facingTargetTurnSpeed로 타겟을 향해 돌아서므로(180도에 약 0.125초) 예열 시간 안에 방향은 맞춰진다.
    private float GetFirstShotTickTimer()
    {
        // 첫 발 지연 = 공통 예열 + 드론별 고정 위상 + 이번 활성화의 무작위 지터. 판정 주기(DPS)는 그대로 두고 위상만
        // 어긋나게 해서, 같은 프레임에 활성화된 드론들이 기계적으로 동시에 쏘지 않고 차례로 리듬을 타며 쏘게 한다.
        float delay = Mathf.Max(firstShotDelay, 0f)
                    + Mathf.Max(attackPhaseOffset, 0f)
                    + Random.Range(0f, Mathf.Max(attackStaggerJitter, 0f));
        return Mathf.Max(damageInterval - delay, 0f);
    }

    /// <summary>
    /// 드론별 고정 첫 발 지연(초). Character가 소환 시 슬롯 순서(0번 꼭짓점부터)로 계단식 값을 넘겨준다.
    /// </summary>
    public void SetAttackPhaseOffset(float _seconds)
    {
        attackPhaseOffset = Mathf.Max(_seconds, 0f);
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
        BeginAttackOnNewTarget();
    }

    /// <summary>
    /// 활성 상태(공격)만 끝내고 따라다니기는 그대로 유지한다. 캐릭터 사망/귀환 확정처럼 "더는 공격하면
    /// 안 되지만 드론은 계속 옆에 있어야 하는" 상황에서 Character가 호출한다. Despawn과 달리 followTarget은
    /// 남긴다.
    /// </summary>
    public void Deactivate()
    {
        isActive = false;
        currentTarget = null;
        activeTimer = 0f;
        CancelSwing(); // 재생 중이던 공격 모션은 헛스윙으로 끝까지 마무리한다(데미지 없음)
    }

    /// <summary>
    /// 귀환 경고 UI가 떠 있는 동안 NPC/FlyingItem/부메랑과 함께 그 자리에서 멈춘다. Update 전체를 건너뛰므로
    /// 이동·애니메이션·타이머가 모두 얼어붙고, Resume 시 멈춘 지점부터 그대로 이어진다.
    /// </summary>
    public void Pause()
    {
        isPaused = true;
        Sound.SetTrackedVolume(chargeSoundHandle, 0f); // 멈춰 있는 동안 충전음이 계속 나면 어색하므로 잠시 끈다
    }

    public void Resume()
    {
        isPaused = false;
        Sound.SetTrackedVolume(chargeSoundHandle, 1f);
    }

    /// <summary>
    /// 캐릭터가 차량에 탑승해 숨겨지는 동안(Character.OnDisable) 드론도 함께 숨기고, 하차해 다시 나타나면
    /// (Character.OnEnable) 함께 보이게 한다. 다시 보일 때는 현재 슬롯 위치로 즉시 스냅한 뒤 스케일 인을
    /// 다시 재생해서, 차에서 드론이 튀어나오는 것처럼 읽힌다. 풀 활성 상태(DroneCreator)와는 별개로
    /// GameObject 활성만 토글하므로 activeDrones 목록과 풀 소속은 그대로 유지된다.
    /// </summary>
    public void SetVisible(bool _visible)
    {
        if (gameObject.activeSelf == _visible) return;

        if (!_visible)
        {
            // 숨겨진 동안 자식 이펙트의 페이드/재생이 이어질 수 없으므로 여기서 즉시 풀로 돌려보낸다(누수 방지)
            StopAllChargingVfx();
            StopOverheatAura(true);
            StopChargeSound(false);
        }

        gameObject.SetActive(_visible);

        if (_visible)
        {
            bSnapToSlotPending = true;
            BeginSpawnScale();

            // 숨겨진 동안 과열 상태가 그대로였다면 다시 보일 때 아우라도 다시 켠다
            if (isOverheat) StartOverheatAura();
        }
    }

    /// <summary>
    /// 던전을 나가는 등, 드론을 풀로 돌려보내기 전에 활성 상태를 정리한다.
    /// </summary>
    public void Despawn()
    {
        isActive = false;
        isSwinging = false;
        bDrySwing = false;
        isPaused = false;
        currentTarget = null;
        followTarget = null;
        StopAllChargingVfx(); // 풀로 돌아가므로 페이드 없이 즉시 정리
        StopOverheatAura(true);
        StopChargeSound(false);
    }

    /// <summary>
    /// 시선과 이동 방향 기준을 기본(아래)으로 되돌리고 다음 Update에서 슬롯 위치로 즉시 스냅한다.
    /// 던전 안 리셋(사망 후 재시작)에서 드론을 재소환하지 않고 유지할 때, 예전 재소환이 해주던 초기화를
    /// 대신한다 - 캐릭터가 아래를 보고 서는 것과 대형/시선이 어긋나지 않게 한다.
    /// </summary>
    public void ResetFacingToDefault()
    {
        lastFacingDir = Vector2.down;
        facingAngle = 270f;
        facingDwellTimer = facingMinDwellTime;
        characterAimDir = Vector2.down;
        lastMoveDir = Vector2.down;
        dirIndex = 6;
        currentFollowSpeed = 0f;
        followSpeedVelocity = 0f;
        bSnapToSlotPending = true;
        ApplyCurrentFrame();
    }

    private void Awake()
    {
        customSortable = GetComponent<CustomSortable>();
        if (customSortable != null)
        {
            customSortable.Initialize(transform);
        }

        chainZap?.SetColor(chainZapNormalColor, chainZapIntensity);

        // 그림자 원래 크기를 기억해둔다 - 고도에 따라 줄였다 늘렸다 해도 이 값을 넘지 않는다(부호는 뒤집기용이라 절댓값만)
        if (shadowSpriteRenderer != null)
        {
            Vector3 baseScale = shadowSpriteRenderer.transform.localScale;
            shadowBaseScale = new Vector3(Mathf.Abs(baseScale.x), Mathf.Abs(baseScale.y), Mathf.Abs(baseScale.z));
        }
    }

    private void Update()
    {
        if (followTarget == null || isPaused) return;

        UpdateSpawnScale(Time.deltaTime);
        UpdateTargetRangeCheck();
        UpdateFollowMovement(Time.deltaTime);
        UpdateBob(Time.deltaTime);
        UpdateSwingTimer(Time.deltaTime); // isSwinging이 이번 프레임에 자연 종료될 수 있으므로 UpdateFacingDirection보다 먼저 실행한다
        UpdateFacingDirection(Time.deltaTime);
        UpdateAnimationFrame(Time.deltaTime);
        UpdateChargingVfxPosition(); // dirIndex가 이번 프레임에 바뀌었을 수 있으므로 UpdateFacingDirection 이후에 위치를 갱신한다
        if (chargeSoundHandle.IsValid) Sound.UpdateTrackedPosition(chargeSoundHandle, GetMuzzlePosition());

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

        UpdateOverheatAura();

        // 페이드가 끝난 충전 이펙트의 풀 반납은 Update(StartSwing의 재생)가 모두 끝난 뒤에 한다. Update에서 반납하면 같은 프레임의
        // 다음 스윙이 방금 반납된 인스턴스를 곧바로 다시 꺼내게 되고, 반납 때의 Stop+Clear로 생긴 정지 콜백이 그 새 재생을 회수할 수 있다.
        if (followTarget != null && !isPaused)
        {
            UpdateChargingVfxFade(Time.deltaTime);
            UpdateRetiredChargingVfx(Time.deltaTime);
        }
    }

    // 과열 아우라를 켠다. 드론이 스폰 연출로 스케일이 바뀌므로 자식으로 붙이지 않고 월드에서 따로 재생하며, 위치/정렬은 UpdateOverheatAura가
    // 매 프레임 따라간다. 실루엣을 읽을 대상은 SetSourceRoot로 이 드론을 알려 준다.
    private void StartOverheatAura()
    {
        if (vfxComponent == null || !gameObject.activeInHierarchy) return;

        // 직전 소화 연출이 아직 남아 있으면 즉시 정리하고 새로 켠다(두 개가 겹치지 않게)
        if (overheatAuraVfx != null) StopOverheatAura(true);

        int sortingOrder = customSortable != null ? customSortable.CurrentSortingOrder + 1 : 0;
        overheatAuraVfx = vfxComponent.Play(new VFXPlaySettings(overheatAuraTag, transform.position, Quaternion.identity, sortingOrder, null));
        if (overheatAuraVfx == null) return;

        overheatAuraRenderer = overheatAuraVfx.GetComponent<ParticleSystemRenderer>();

        PresentationLayer.VFX.VFX_OverheatAura aura = overheatAuraVfx.GetComponentInChildren<PresentationLayer.VFX.VFX_OverheatAura>(true);
        if (aura != null) aura.SetSourceRoot(transform);
    }

    // _immediate가 true면 즉시 지우고 풀로 돌려보낸다(숨김, 풀 반환, 재시작). false면 방출만 멈추고 소화 연출이 끝날 때까지 위치를 따라간다.
    private void StopOverheatAura(bool _immediate)
    {
        if (overheatAuraVfx == null) return;

        if (vfxComponent != null && overheatAuraVfx.gameObject.activeSelf)
        {
            vfxComponent.Stop(overheatAuraVfx, _immediate);
        }

        // 즉시 정리면 참조를 놓고, 아니면 소화 연출이 끝날 때까지(UpdateOverheatAura가 비활성을 감지할 때까지) 참조를 유지한다
        if (_immediate)
        {
            overheatAuraVfx = null;
            overheatAuraRenderer = null;
        }
    }

    // 아우라가 이 드론을 매 프레임 따라가게 한다(위치 + 드론 본체보다 한 칸 위의 정렬 순서). 풀이 이미 회수한 인스턴스(비활성)면 참조를 놓는다.
    private void UpdateOverheatAura()
    {
        if (overheatAuraVfx == null) return;

        if (!overheatAuraVfx.gameObject.activeSelf)
        {
            overheatAuraVfx = null;
            overheatAuraRenderer = null;
            return;
        }

        overheatAuraVfx.transform.position = transform.position;
        if (overheatAuraRenderer != null && customSortable != null)
        {
            overheatAuraRenderer.sortingOrder = customSortable.CurrentSortingOrder + 1;
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

        // 캐릭터가 한 프레임에 teleportSnapDistance 이상 움직였으면 걸어서 이동한 것이 아니라 순간이동
        // (던전 시작 위치 배치, 차량 승/하차 등)이다. 이때 SmoothDamp로 쫓아가면 화면 밖에서 날아오는
        // 것처럼 보이므로, 드론도 함께 순간이동한 것처럼 슬롯 위치에 즉시 놓는다.
        float targetMoveSqr = (followTarget.position - lastFollowTargetPos).sqrMagnitude;
        lastFollowTargetPos = followTarget.position;
        if (bSnapToSlotPending || targetMoveSqr > teleportSnapDistance * teleportSnapDistance)
        {
            bSnapToSlotPending = false;
            transform.position = targetPos;
            currentFollowSpeed = 0f;
            followSpeedVelocity = 0f;
            return;
        }

        float distToSlot = Vector3.Distance(transform.position, targetPos);

        // 선형 가속 대신 SmoothDamp(임계 감쇠 스프링)를 써서 가감속 곡선 자체가 부드럽게 휘어지도록 한다
        // - 목표 속도(0 또는 최고 속도)로 딱딱하게 꺾이지 않고 자연스럽게 이어진다.
        float targetSpeed = distToSlot > arrivalTolerance ? followMaxSpeed : 0f;
        currentFollowSpeed = Mathf.SmoothDamp(currentFollowSpeed, targetSpeed, ref followSpeedVelocity, followSmoothTime * varFollowMul);

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
        Vector3 moveDir = toTarget / dist;
        transform.position += moveDir * step;
        lastMoveDir = moveDir;
    }

    // 스케일 인을 시작한다. spawnScaleDuration이 0 이하면(연출 끔) 곧바로 1로 두어, UpdateSpawnScale이 첫 줄에서
    // 완료 판정으로 빠져나가도 스케일 0에 갇히지 않게 한다.
    private void BeginSpawnScale()
    {
        spawnScaleTimer = 0f;
        transform.localScale = spawnScaleDuration > 0f ? Vector3.zero : Vector3.one;
    }

    // 소환 직후 spawnScaleDuration 동안 0 -> spawnScaleOvershoot -> 1로 커진다(뽀잉). 전체 시간의 앞 60%에
    // 이즈 아웃으로 정확히 spawnScaleOvershoot까지 올라갔다가, 뒤 40%에 스무스스텝으로 1에 안착한다 - 피크가
    // 파라미터 값과 정확히 일치한다. 완료된 뒤에는 아무 일도 하지 않는다. 풀 반환/소멸은 씬 전환 시점에만
    // 일어나 화면에 보이지 않으므로 스케일 아웃은 두지 않는다.
    private void UpdateSpawnScale(float _deltaTime)
    {
        if (spawnScaleDuration <= 0f || spawnScaleTimer >= spawnScaleDuration) return;

        spawnScaleTimer += _deltaTime;
        float t = Mathf.Clamp01(spawnScaleTimer / spawnScaleDuration);

        const float peakT = 0.6f;
        float peak = Mathf.Max(spawnScaleOvershoot, 1f);
        float scale;
        if (t < peakT)
        {
            float u = t / peakT;
            scale = Mathf.Lerp(0f, peak, 1f - (1f - u) * (1f - u)); // 이즈 아웃으로 피크까지
        }
        else
        {
            float u = (t - peakT) / (1f - peakT);
            scale = Mathf.Lerp(peak, 1f, u * u * (3f - 2f * u)); // 스무스스텝으로 1에 안착
        }
        if (t >= 1f) scale = 1f;

        transform.localScale = Vector3.one * scale;
    }

    // 본체 스프라이트(spriteRenderer)만 위아래로 살짝 흔들어 둥둥 떠다니는 느낌을 낸다. 논리적 위치인
    // transform(루트)은 건드리지 않아서 leash 거리 판정/공격 사거리/CustomSortable 정렬에는 전혀
    // 영향이 없다 - 그림자(shadowSpriteRenderer)는 바닥에 고정된 채라 위에 떠 있는 느낌이 강조된다.
    // 드론마다 bobPhase가 달라서 여러 대가 똑같은 타이밍으로 맞춰 떠다니지 않는다.
    private void UpdateBob(float _deltaTime)
    {
        if (spriteRenderer == null) return;

        // 전원 상태(사망 시 0, 정상 1)로 서서히 수렴시켜 호버 높이와 흔들림을 함께 줄이거나 되살린다
        powerLevel = Mathf.MoveTowards(powerLevel, powerTarget, powerTransitionSpeed * _deltaTime);
        float hoverMul = Mathf.Lerp(powerDownHoverMultiplier, 1f, powerLevel);
        float bobMul = Mathf.Lerp(powerDownBobMultiplier, 1f, powerLevel);

        bobPhase += _deltaTime * bobFrequency * varBobMul * bobMul * Mathf.PI * 2f;
        float bobY = hoverHeight * hoverMul + Mathf.Sin(bobPhase) * bobAmplitude * bobMul;

        Transform visualTransform = spriteRenderer.transform;
        Vector3 localPos = visualTransform.localPosition;
        localPos.y = bobY;
        visualTransform.localPosition = localPos;

        currentAltitude = Mathf.Max(bobY, 0f); // 실제 그림자 스케일 적용은 같은 프레임 뒤에 오는 ApplyFrame이 한 번만 한다
    }

    // 본체가 높이 떠 있을수록 그림자를 작게 그린다(바닥 = 원래 크기, shadowShrinkAltitude 이상 = shadowMinScale).
    // 호버 높이가 높은 꼭짓점 드론은 그림자가 작고, 사망으로 전원이 약해져 고도가 내려오면 그림자가 다시 커진다.
    // 좌우 반전(shadowFlipX)은 x 스케일 부호로 함께 처리한다.
    private void ApplyShadowScale()
    {
        if (shadowSpriteRenderer == null) return;

        float t = shadowShrinkAltitude > 0f ? Mathf.Clamp01(currentAltitude / shadowShrinkAltitude) : 0f;
        float scaleMul = Mathf.Lerp(1f, Mathf.Clamp01(shadowMinScale), t);

        Vector3 scale = shadowBaseScale * scaleMul;
        scale.x = shadowFlipX ? -scale.x : scale.x;
        shadowSpriteRenderer.transform.localScale = scale;
    }

    // 활성 상태(지속시간 안)에서 유효한 공격 대상이 있으면 스윙 중이 아니어도 계속 그 대상을 바라본다.
    // 예전처럼 스윙 순간에만 대상을 보면 damageInterval마다 "대상 -> 조준 방향 -> 대상"으로 왕복하며
    // 방향이 계속 튀었다. 대상이 없으면(비활성/사망/범위 이탈) 캐릭터의 조준 방향(characterAimDir)을 본다.
    //
    // 목표 방향(lastFacingDir)은 즉시 갱신하되, 실제로 바라보는 각도(facingAngle)는 MoveTowardsAngle로
    // 서서히 돌린다. 8방향 스프라이트라도 각도가 중간 섹터를 거쳐 가므로 Down -> DownRight -> Right처럼
    // 단계적으로 돌아가 보인다. 대상을 향할 때는 빠르게(타격 프레임 전에 도착해야 muzzle 위치가 맞는다),
    // 조준 방향으로 복귀할 때는 느긋하게 돌아서 "공격을 마치고 천천히 자세를 되돌리는" 인상을 준다.
    private void UpdateFacingDirection(float _deltaTime)
    {
        bool lookAtTarget = (isActive || isSwinging) && currentTarget != null;

        // 헛스윙(타겟을 잃은 채 모션만 마무리 중)일 때는 마지막으로 보던 방향을 그대로 유지한다. 모션이 끝난 뒤에야
        // 조준 방향으로 돌아서기 시작해야 "공격을 마치고 자세를 되돌리는" 순서가 자연스럽다.
        bool holdFacing = isSwinging && !lookAtTarget;

        if (!holdFacing)
        {
            Vector2 dir;
            if (lookAtTarget)
            {
                dir = (Vector2)currentTarget.GetTransform().position - (Vector2)transform.position;
            }
            else if (currentFollowSpeed >= faceMoveSpeedThreshold)
            {
                // 슬롯을 향해 빠르게 이동 중이면 이동 방향을 본다 - 조준 방향만 고정해서 보면 옆/뒤로 미끄러지는
                // 인상이 난다. 속도가 떨어져 슬롯에 정착하면 다시 조준 방향으로 돌아온다(각도 보간이 전환을 잇는다).
                dir = lastMoveDir;
            }
            else
            {
                dir = characterAimDir;
            }

            if (dir.sqrMagnitude >= minMoveSqrForFacing)
            {
                lastFacingDir = dir.normalized;
            }
        }

        float targetAngle = Mathf.Atan2(lastFacingDir.y, lastFacingDir.x) * Mathf.Rad2Deg;
        float turnSpeed = lookAtTarget ? facingTargetTurnSpeed : facingReturnTurnSpeed * varTurnMul;

        // 타격 시점 안전장치: 타겟을 향해 도는 중이면 "남은 각도 / (타격까지 남은 시간 - 여유)"를 최소 속도로 삼는다.
        // 평소엔 읽히는 속도로 느긋하게 돌고, 스윙 도중 재타겟팅처럼 시간이 촉박할 때만 필요한 만큼 빨라져서
        // 레이저 시작점(총구)이 타격 프레임에 반드시 타겟 방향을 향한다. 촉박할 때는 최소 유지 시간도 무시한다.
        bool bUrgent = false;
        if (lookAtTarget)
        {
            float remainingDeg = Mathf.Abs(Mathf.DeltaAngle(facingAngle, targetAngle));
            float timeBudget = GetTimeUntilImpact() - facingImpactSafetyMargin;
            float requiredSpeed = remainingDeg / Mathf.Max(timeBudget, 0.02f);
            if (requiredSpeed > turnSpeed)
            {
                turnSpeed = requiredSpeed;
                bUrgent = true;
            }
        }

        facingAngle = Mathf.MoveTowardsAngle(facingAngle, targetAngle, turnSpeed * _deltaTime);
        facingAngle = Mathf.Repeat(facingAngle, 360f);

        // 45도 섹터 경계에 히스테리시스를 둔다: 현재 dirIndex 섹터의 중심에서 (22.5 + 여유) 이상 벗어났을 때만
        // 새 섹터로 넘어간다. 보간 중 각도가 경계 근처를 천천히 지나갈 때 두 스프라이트가 번갈아 깜빡이는 것을 막는다.
        // 여기에 최소 유지 시간을 더해, 한 방향 스프라이트가 facingMinDwellTime보다 짧게 스치고 지나가지 않게 한다
        // - 회전 속도가 어떻든 중간 방향이 최소 몇 프레임은 보이므로 "휙" 하고 바뀌는 인상이 사라진다.
        facingDwellTimer += _deltaTime;
        float sectorDelta = Mathf.DeltaAngle(dirIndex * 45f, facingAngle); // 현재 섹터 중심 -> 실제 각도(부호 = 회전 방향)
        if (Mathf.Abs(sectorDelta) > 22.5f + facingDirHysteresisDeg && (bUrgent || facingDwellTimer >= facingMinDwellTime))
        {
            if (bUrgent)
            {
                dirIndex = Mathf.RoundToInt(facingAngle / 45f) % 8; // 촉박하면 중간 단계를 건너뛰고 바로 맞춘다
            }
            else
            {
                // 각도가 유지 시간 동안 한 섹터 이상 앞서갔더라도 스프라이트는 회전 방향으로 한 섹터씩만 넘긴다.
                // 그래야 어떤 속도에서도 중간 방향이 빠지지 않고 차례로 보인다(표시가 각도를 잠시 뒤따라가다 따라잡는다).
                dirIndex = (dirIndex + (sectorDelta > 0f ? 1 : 7)) % 8;
            }
            facingDwellTimer = 0f;
        }
    }

    // 다음 타격 판정(임팩트 프레임)까지 남은 시간(초). 스윙 중이면 임팩트 프레임까지, 아직 스윙 전이면 다음 스윙
    // 시작(damageInterval - damageTickTimer)에 임팩트 프레임 도달 시간을 더한 값이다. 이번 스윙의 타격이 이미
    // 끝났거나 비활성이면 제한이 없는 것으로 본다. UpdateFacingDirection이 회전 속도의 하한을 정하는 데 쓴다.
    private float GetTimeUntilImpact()
    {
        List<Sprite> attackSprites = GetAttackSprites(dirIndex, out _);
        int frameCount = attackSprites != null ? attackSprites.Count : 0;
        if (frameCount <= 0) return float.MaxValue;

        float frameTime = 1f / GetEffectiveSampleRate(frameCount);
        float impactOffset = Mathf.Min(ImpactFrameIndex, frameCount - 1) * frameTime;

        if (isSwinging)
        {
            return damageAppliedThisSwing ? float.MaxValue : Mathf.Max(impactOffset - swingTimer, 0f);
        }

        if (!isActive) return float.MaxValue;
        return Mathf.Max(damageInterval - damageTickTimer, 0f) + impactOffset;
    }

    // 공격 모션(attackSprites)은 damageInterval마다 한 번, 재생 시간(프레임 수 / attackSampleRate)만큼
    // 켜졌다가 저절로 꺼진다. 지속시간(activeDuration) 내내 반복 재생되지 않는다.
    private void UpdateSwingTimer(float _deltaTime)
    {
        if (!isSwinging) return;

        swingTimer += _deltaTime;

        List<Sprite> attackSprites = GetAttackSprites(dirIndex, out _);
        float swingDuration = (attackSprites != null && attackSprites.Count > 0)
            ? attackSprites.Count / GetEffectiveSampleRate(attackSprites.Count)
            : 0f;

        if (swingTimer >= swingDuration)
        {
            isSwinging = false;
            bDrySwing = false;
            if (!bChargingVfxFading) StopChargingVfx(false); // 정상적으로는 임팩트 프레임에서 이미 페이드로 넘어갔겠지만, 만약을 대비한 안전망. 같은 프레임에 다음 스윙이 같은 인스턴스를 다시 꺼내지 않도록 즉시 반납 대신 페이드로 넘긴다
            StopChargeSound(true);

            // 지속시간이 끝난 뒤 "마지막 한 발"을 마저 쏘도록 남겨뒀던 타겟은 스윙이 끝난 지금 놓아준다.
            if (!isActive)
            {
                currentTarget = null;
            }
        }
    }

    private void UpdateActiveTimer(float _deltaTime)
    {
        activeTimer += _deltaTime;
        if (activeTimer >= activeDuration)
        {
            // 지속시간 만료는 새 스윙 예약만 막는다. 이미 충전까지 시작한 스윙은 타격까지 마치게 두는 편이
            // 플레이어 입장에서 공정하다 - 타겟은 UpdateSwingTimer가 스윙 종료 시점에 놓아준다.
            isActive = false;
            if (!isSwinging)
            {
                currentTarget = null;
            }
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
            // 해제 거리는 획득 거리(attackRange)보다 targetReleaseRangeMultiplier배 넉넉하다. 드론은 슬롯 주변을
            // 계속 움직이므로 딱 attackRange로 자르면 경계에 걸린 나무가 프레임마다 유효/무효를 오가며
            // 재타겟팅과 스윙 취소를 반복한다.
            float releaseRange = attackRange * Mathf.Max(targetReleaseRangeMultiplier, 1f);
            float distSqr = ((Vector2)currentTarget.GetTransform().position - (Vector2)transform.position).sqrMagnitude;
            if (distSqr <= releaseRange * releaseRange) return; // 아직 유효하고 해제 범위 안 - 그대로 유지
        }

        // 지속시간이 이미 끝난 "마지막 한 발" 도중이라면 새 타겟을 찾지 않고 그대로 헛스윙으로 마무리한다.
        currentTarget = isActive ? requestRetarget?.Invoke(this) : null;

        if (currentTarget == null)
        {
            CancelSwing(); // 대체 나무가 없다 - 모션은 끝까지 재생하되 타격은 하지 않는다
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
        bDrySwing = false;
        swingTimer = 0f;
        damageAppliedThisSwing = false;

        // 프레임을 여기서 직접 0으로 되돌린다. UpdateAnimationFrame의 isSwinging 전환 감지에만 기대면, 공격 속도가
        // 높아 이전 스윙이 아직 끝나기 전에(또는 끝난 바로 그 프레임에) 다음 스윙이 시작될 때 프레임이 마지막(5번)에
        // 고정된 채 즉시 타격 판정만 반복되는 문제가 생긴다.
        currentFrameIndex = 0;
        frameTimer = 0f;
        prevIsSwinging = true;

        PlayChargingVfx(); // 공격 모션이 시작되는 이 시점부터 임팩트 프레임 직전까지 Muzzle에서 루프 재생
        PlayChargeSound();
    }

    // 공격 속도 업그레이드로 damageInterval이 기본 스윙 길이(프레임 수 / attackSampleRate, 기본 0.5초)보다
    // 짧아지면 모션 재생 속도를 그만큼 올려서 스윙 한 번이 정확히 damageInterval 안에 끝나게 한다.
    // 판정 주기를 스윙 길이로 클램프하는 대신 모션을 빠르게 하는 쪽이 "공격 속도가 올랐다"는 체감에 맞는다.
    private float GetEffectiveSampleRate(int _frameCount)
    {
        float baseRate = attackSampleRate > 0f ? attackSampleRate : 10f;
        if (_frameCount <= 0 || damageInterval <= 0f) return baseRate;

        float baseSwingDuration = _frameCount / baseRate;
        if (damageInterval >= baseSwingDuration) return baseRate;

        return _frameCount / damageInterval;
    }

    // 진행 중인 스윙을 "헛스윙"으로 전환한다. 모션을 그 자리에서 자르면 스프라이트가 재생 중 프레임에서
    // Idle로 튀고 충전 파티클도 한 프레임에 사라지므로, 대신 모션은 끝까지 재생하고(0.5초) 타격 프레임의
    // 데미지/연쇄만 건너뛴다. 충전 파티클은 방출만 멈춰 남은 입자가 수명대로 사라지게 한다(페이드).
    // 스윙 중이 아니면 아무 일도 하지 않는다(재생 중인 충전 파티클도 없다).
    private void CancelSwing()
    {
        if (!isSwinging) return;

        bDrySwing = true;
        StopChargingVfx(false);
        StopChargeSound(true);
    }

    private void ApplyDamageToCurrentTarget()
    {
        if (currentTarget == null || currentTarget.bDead) return;

        PlayFireSound();

        // 레이저라 발사와 동시에 맞는다 - 도끼 타격음 대신 위의 발사음만 들리게 한다
        if (currentTarget is TreeObj tree)
        {
            tree.TakeDamageWithoutHitSound(damage);
        }
        else
        {
            (currentTarget as IDamageable)?.TakeDamage(damage);
        }
        requestChainAttack?.Invoke(this, currentTarget);
    }

    // 레이저 발사음. 발사/타격/전기 세 사운드를 같은 프레임에 겹쳐 한 번의 "레이저 적중"으로 들리게 한다.
    // 연쇄로 여러 그루가 맞아도 발사는 한 번이므로 여기서 한 번만 울린다.
    private void PlayFireSound()
    {
        Vector3 muzzlePos = GetMuzzlePosition();
        Sound.Play(SoundID.SFXBeamFire, muzzlePos);
        Sound.Play(SoundID.SFXPunchImpact, muzzlePos);
        Sound.Play(SoundID.SFXVoltageImpact, muzzlePos);
    }

    // 충전음(SFX_ChargeUp)을 매우 낮은 피치에서 시작해 임팩트 프레임까지 계속 끌어올린다. 헛스윙이 되살아나는
    // 경우처럼 스윙 도중 다시 불려도 남은 시간 기준으로 새로 올린다.
    private void PlayChargeSound()
    {
        StopChargeSound(false);

        chargeSoundHandle = Sound.PlayTracked(SoundID.SFXChargeUp, GetMuzzlePosition(), 1f, true, chargeSoundStartPitch);
        float timeUntilImpact = GetTimeUntilImpact();
        if (timeUntilImpact < float.MaxValue)
        {
            Sound.RampTrackedPitch(chargeSoundHandle, chargeSoundEndPitch, timeUntilImpact);
        }
    }

    // _powerDown이 true면 헛스윙 취소처럼 전원 꺼지듯 잦아들며 멈추고, false면 즉시 끊는다(발사 순간, 숨김, 풀 반환).
    private void StopChargeSound(bool _powerDown)
    {
        if (!chargeSoundHandle.IsValid) return;

        if (_powerDown)
        {
            Sound.StopTrackedWithPowerDown(chargeSoundHandle, chargeSoundCancelFadeTime, chargeSoundStartPitch * 0.5f);
        }
        else
        {
            Sound.StopTracked(chargeSoundHandle);
        }
        chargeSoundHandle = AudioHandle.Invalid;
    }

    // 공격 모션이 시작될 때 Muzzle에서 충전 이펙트를 켠다. 끄는 쪽은 드론 자신이 실제로 "공격하는" 순간(임팩트 프레임)에
    // 걸려 있다(ReleaseChargingVfx). ApplyDamageToCurrentTarget은 타겟이 이미 무효해진 경우 아무 일도 하지 않고 조용히
    // 반환하므로, 충전 이펙트 정리는 타겟 유효성과 무관하게 임팩트 프레임 도달 자체에 걸어야 확실히 꺼진다(UpdateAnimationFrame 참고).
    //
    // 직전 이펙트(발사 연출 또는 취소 소화 중)가 남아 있으면 새 인스턴스를 먼저 꺼낸 뒤에 정리한다. 먼저 반납하면 풀이 방금 반납된
    // 인스턴스를 같은 프레임에 다시 내주는데, 반납 때의 Stop+Clear로 생긴 정지 콜백(OnParticleSystemStopped)이 그 새 재생을
    // 곧바로 회수해 충전 이펙트가 사라질 수 있다. 그래서 DroneCharging 풀은 진행 중 + 은퇴 + 새 충전 = 3개를 둔다.
    private void PlayChargingVfx()
    {
        if (vfxComponent == null) return;

        // 풀이 먼저 회수했다면 참조만 버린다. 새 인스턴스를 꺼내기 전에 두 슬롯 모두 확인해야 한다 - 회수된 인스턴스가 이번 재생으로
        // 재발급되면 다시 활성 상태 + Visual 자식이 되어 소유 확인을 통과하므로, 낡은 참조가 나중에 지금 충전 중인 이펙트를 반납해 버린다.
        if (chargingVfx != null && !IsChargingVfxOwned()) ClearChargingVfxRefs();
        if (retiredChargingVfx != null && !IsVfxOwned(retiredChargingVfx, retiredChargingVfxParent)) ClearRetiredChargingVfxRefs();
        if (chargingVfx != null && !bChargingVfxFading) return; // 이미 충전 중

        // spriteRenderer.transform(Visual)의 자식으로 붙여 Hierarchy상 드론 소속으로 정리해둔다.
        // 다만 위치 추적 자체는 부모-자식 상속에 기대지 않고 UpdateChargingVfxPosition이 매 프레임
        // 직접 재계산해서 강제로 맞춘다 - 상속에만 맡겼더니 드론이 이동 중일 때 이펙트가 따라오지
        // 못하고 뒤에 남는 현상이 있었다.
        Transform parent = spriteRenderer != null ? spriteRenderer.transform : transform;
        VFXPlaySettings settings = new VFXPlaySettings(chargingVfxTag, GetMuzzlePosition(), Quaternion.identity, parent);
        ParticleSystem newVfx = vfxComponent.Play(settings);
        if (newVfx == null && chargingVfx != null)
        {
            // 풀이 모자라면(풀 크기를 줄인 경우) 직전 이펙트를 즉시 반납하고 그 인스턴스를 다시 꺼낸다 - 이펙트가 아예 안 나오는 것보다 낫다
            StopChargingVfx(true);
            newVfx = vfxComponent.Play(settings);
        }

        if (chargingVfx != null) RetireChargingVfx();
        if (newVfx == null) return;

        chargingVfx = newVfx;
        chargingVfxParent = parent;

        // ParticleSystemRenderer를 캐싱해둔다 - CustomSortable이 SpriteRenderer만 자동 수집하므로(Drone.Awake),
        // 파티클 렌더러는 정렬 순서를 직접 챙겨줘야 한다. 픽셀 소용돌이 메쉬는 이 루트 렌더러의 정렬 순서를 따라 그린다.
        // 페이드에 쓸 최대 수명도 같은 자리에서 한 번만 계산해둔다(취소마다 자식을 다시 훑지 않도록).
        if (!chargingVfxCache.TryGetValue(chargingVfx, out ChargingVfxCacheEntry cacheEntry))
        {
            cacheEntry.renderers = chargingVfx.GetComponentsInChildren<ParticleSystemRenderer>(true);
            cacheEntry.vortex = chargingVfx.GetComponentInChildren<PresentationLayer.VFX.VFX_ChargeVortex>(true);
            cacheEntry.maxLifetime = ComputeChargingVfxMaxLifetime(chargingVfx);
            chargingVfxCache[chargingVfx] = cacheEntry;
        }
        chargingVfxRenderers = cacheEntry.renderers;
        chargingVfxMaxLifetime = cacheEntry.maxLifetime;

        // 소용돌이의 충전 진행도(원 크기, 입자 수)가 임팩트 순간 꽉 차도록 남은 시간과 현재 과열 색을 넘긴다
        chargingVortex = cacheEntry.vortex;
        if (chargingVortex != null)
        {
            chargingVortex.SetOverheat(isOverheat);
            float timeUntilImpact = GetTimeUntilImpact();
            if (timeUntilImpact < float.MaxValue) chargingVortex.SetChargeDuration(timeUntilImpact);
        }

        UpdateChargingVfxPosition(); // 첫 프레임부터 올바른 정렬 순서로 그려지게 한다(Update의 갱신은 이미 지나갔을 수 있다)
    }

    // 새 충전에 자리를 넘기는 직전 이펙트를 정리한다. 발사 연출(흡수) 중이면 은퇴 슬롯으로 옮겨 끝까지 재생하게 하고(공격 속도가
    // 빨라도 연출이 잘리지 않게), 취소 소화 중이면 새 충전과 겹쳐 보이지 않게 예전처럼 즉시 끊는다. 새 인스턴스를 이미 꺼낸 뒤에만 부른다.
    private void RetireChargingVfx()
    {
        if (chargingVfx == null) return;

        if (!IsChargingVfxOwned())
        {
            ClearChargingVfxRefs();
            return;
        }

        if (bChargingVfxReleased)
        {
            StopRetiredChargingVfx(); // 은퇴 슬롯은 하나만 쓴다 - 더 오래된 발사 연출은 이미 거의 끝났다
            retiredChargingVfx = chargingVfx;
            retiredChargingVfxParent = chargingVfxParent;
            retiredChargingVfxRenderers = chargingVfxRenderers;
            retiredChargingVfxTimer = chargingVfxFadeTimer;
        }
        else
        {
            vfxComponent.Stop(chargingVfx, true);
        }

        ClearChargingVfxRefs();
    }

    // 지금 들고 있는 충전 이펙트가 아직 이 드론 소유인지. 풀(VFXPoolInstanceHelper)이 OnParticleSystemStopped나
    // StopAll로 먼저 회수했다면 비활성화되거나 다른 부모로 옮겨져 있으므로, 그 인스턴스의 위치/정렬을 건드리거나
    // Stop을 다시 호출하면 안 된다(재발급된 다른 재생을 가로채게 된다).
    private bool IsChargingVfxOwned()
    {
        return IsVfxOwned(chargingVfx, chargingVfxParent);
    }

    private static bool IsVfxOwned(ParticleSystem _vfx, Transform _parent)
    {
        if (_vfx == null) return false;
        if (!_vfx.gameObject.activeSelf) return false;
        return _parent != null && _vfx.transform.IsChildOf(_parent);
    }

    private void ClearChargingVfxRefs()
    {
        chargingVfx = null;
        chargingVortex = null;
        chargingVfxRenderers = null;
        chargingVfxParent = null;
        bChargingVfxFading = false;
        bChargingVfxReleased = false;
    }

    // _immediate가 true면 남은 입자까지 즉시 지우고 풀로 돌려보낸다(풀 반환, 숨김, 페이드 종료).
    // false면 방출만 멈추고(페이드 - 충전 중이던 소용돌이는 이를 감지해 취소 소화 연출을 재생한다) 참조는 드론이 계속
    // 들고 있다가 UpdateChargingVfxFade가 수명이 끝난 뒤 직접 풀로 돌려보낸다. VFXPoolInstanceHelper의 지연 반환 코루틴에 맡기지
    // 않는 이유: 이펙트가 드론 Visual의 자식이라 페이드 도중 드론이 숨겨지거나(SetVisible) 풀로 돌아가면(Despawn) 그 코루틴이 죽어
    // 이펙트가 영영 반환되지 않았다. 참조를 유지하므로 페이드 중에도 총구 위치 추적과 정렬 순서 갱신이 그대로 이어진다.
    private void StopChargingVfx(bool _immediate = true)
    {
        if (vfxComponent == null || chargingVfx == null) return;

        // 풀이 먼저 회수한 인스턴스면 우리 손을 이미 떠났다 - Stop을 다시 부르지 않고 참조만 정리한다
        if (!IsChargingVfxOwned())
        {
            ClearChargingVfxRefs();
            return;
        }

        if (_immediate || !gameObject.activeInHierarchy)
        {
            vfxComponent.Stop(chargingVfx, true);
            ClearChargingVfxRefs();
            return;
        }

        if (bChargingVfxFading) return; // 이미 페이드 중

        chargingVfx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        bChargingVfxFading = true;
        chargingVfxFadeTimer = chargingVfxMaxLifetime + 0.2f;
    }

    // 숨김, 풀 반환, 재소환처럼 이 드론의 충전 이펙트를 남김없이 즉시 치워야 할 때 쓴다(은퇴 슬롯 포함).
    private void StopAllChargingVfx()
    {
        StopChargingVfx(true);
        StopRetiredChargingVfx();
    }

    // 임팩트(발사) 순간의 충전 이펙트 정리. 소용돌이에 발사 연출(남은 입자가 중심으로 빠르게 흡수, 코어가 부풀었다 줄어듦)을
    // 시킨 뒤 방출만 멈춰 페이드로 넘긴다 - 풀 반환은 UpdateChargingVfxFade(또는 다음 충전 때 은퇴 슬롯)가 맡는다.
    // 같은 프레임에 다음 스윙이 시작돼도 방금 반납한 인스턴스를 다시 꺼내지 않도록 여기서는 즉시 반납하지 않는다.
    private void ReleaseChargingVfx()
    {
        if (chargingVfx == null) return;

        if (!IsChargingVfxOwned())
        {
            ClearChargingVfxRefs();
            return;
        }

        if (chargingVortex != null) chargingVortex.Release();
        bChargingVfxReleased = true; // StopChargingVfx가 즉시 정리 경로로 빠지면 ClearChargingVfxRefs가 다시 false로 되돌린다
        StopChargingVfx(false);
    }

    // 페이드가 끝난 뒤(남은 입자가 모두 사라진 뒤) 이펙트를 풀로 돌려보낸다. 그 전에 풀이 회수했다면 참조만 버린다. LateUpdate에서 부른다.
    private void UpdateChargingVfxFade(float _deltaTime)
    {
        if (!bChargingVfxFading || chargingVfx == null) return;

        if (!IsChargingVfxOwned())
        {
            ClearChargingVfxRefs();
            return;
        }

        chargingVfxFadeTimer -= _deltaTime;
        if (chargingVfxFadeTimer <= 0f)
        {
            StopChargingVfx(true);
        }
    }

    // 은퇴한 이펙트(발사 연출 마무리 중)의 남은 페이드 시간이 끝나면 풀로 돌려보낸다. 보통은 그 전에 루트 입자가 모두 사라져 풀이 먼저 회수한다. LateUpdate에서 부른다.
    private void UpdateRetiredChargingVfx(float _deltaTime)
    {
        if (retiredChargingVfx == null) return;

        if (!IsVfxOwned(retiredChargingVfx, retiredChargingVfxParent))
        {
            ClearRetiredChargingVfxRefs();
            return;
        }

        retiredChargingVfxTimer -= _deltaTime;
        if (retiredChargingVfxTimer <= 0f)
        {
            StopRetiredChargingVfx();
        }
    }

    private void StopRetiredChargingVfx()
    {
        if (retiredChargingVfx == null) return;

        if (vfxComponent != null && IsVfxOwned(retiredChargingVfx, retiredChargingVfxParent))
        {
            vfxComponent.Stop(retiredChargingVfx, true);
        }
        ClearRetiredChargingVfxRefs();
    }

    private void ClearRetiredChargingVfxRefs()
    {
        retiredChargingVfx = null;
        retiredChargingVfxParent = null;
        retiredChargingVfxRenderers = null;
    }

    // 본체 + 자식 파티클 중 가장 긴 startLifetime. VFXPoolInstanceHelper.CoWaitAndReturnToPool과 같은 기준이다.
    // PlayChargingVfx에서 한 번만 호출해 chargingVfxMaxLifetime에 보관한다.
    private static float ComputeChargingVfxMaxLifetime(ParticleSystem _vfx)
    {
        if (_vfx == null) return 0f;

        float maxLifetime = _vfx.main.startLifetime.constantMax;
        ParticleSystem[] children = _vfx.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] == null) continue;
            float lifetime = children[i].main.startLifetime.constantMax;
            if (lifetime > maxLifetime) maxLifetime = lifetime;
        }
        return maxLifetime;
    }

    // dirIndex(공격 대상을 바라보는 방향)가 스윙 도중 바뀔 수 있으므로(재타겟팅), 재생 중인 동안
    // 매 프레임 Muzzle 위치로 다시 옮겨 따라가게 한다. 같은 김에 정렬 순서도 그 Muzzle의 Y좌표 기준으로
    // 맞춘다 - CustomSortable.ComputeSortingOrder에 드론 본체와 동일한 precision/offset 규칙을 그대로
    // 위임하므로, 드론 스프라이트와 항상 같은 기준으로 앞뒤가 맞는다. 은퇴한 이펙트는 위치는 Visual 자식으로서 그대로 두고
    // (발사한 자리에서 마무리) 정렬 순서만 같이 맞춘다.
    private void UpdateChargingVfxPosition()
    {
        // 풀이 먼저 회수한 인스턴스의 위치/정렬을 건드리지 않는다
        if (chargingVfx != null && !IsChargingVfxOwned()) ClearChargingVfxRefs();
        if (retiredChargingVfx != null && !IsVfxOwned(retiredChargingVfx, retiredChargingVfxParent)) ClearRetiredChargingVfxRefs();
        if (chargingVfx == null && retiredChargingVfx == null) return;

        Vector3 muzzlePos = GetMuzzlePosition();
        if (chargingVfx != null) chargingVfx.transform.position = muzzlePos;

        if (customSortable == null) return;

        // 총구 이펙트의 앞뒤는 총구 Y가 아니라 "보는 방향"으로 정한다. 총구가 화면 안쪽을 향하는 위쪽 세 방향(1 우상, 2 상, 3 좌상)에서만
        // 본체 뒤에 가려지고, 수평(0 우, 4 좌)과 아래쪽(5 좌하, 6 하, 7 우하)에서는 본체 앞에 보여야 자연스럽다.
        // (예전엔 총구 Y로만 계산해 수평일 때 총구가 떠 있는 본체 Visual에 붙어 루트보다 Y가 높다는 이유로 본체 뒤에 깔렸다)
        // 본체 순서는 CurrentSortingOrder(직전 LateUpdate 값)가 아니라 이번 프레임 위치로 직접 계산한다 - 이동 중이면
        // 직전 값이 이번 프레임 본체 순서와 달라 한 프레임 어긋난다(CustomSortable의 정렬 기준점이 이 드론 루트다).
        int bodyOrder = customSortable.ComputeSortingOrder(transform.position.y);
        bool bFacingUp = dirIndex >= 1 && dirIndex <= 3;
        int order = bFacingUp ? bodyOrder - 1 : bodyOrder + 1;

        ApplySortingOrder(chargingVfxRenderers, order);
        ApplySortingOrder(retiredChargingVfxRenderers, order);
    }

    private static void ApplySortingOrder(ParticleSystemRenderer[] _renderers, int _order)
    {
        if (_renderers == null) return;

        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].sortingOrder = _order;
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

            float frameTime = 1f / GetEffectiveSampleRate(attackSprites.Count);
            frameTimer += _deltaTime;
            // 공격 속도가 매우 높아 frameTime이 한 Update 간격보다 짧아지면 한 프레임에 여러 장을 넘겨야 한다.
            // 한 장씩만 넘기면 다음 StartSwing(프레임 0 리셋)이 오기 전에 임팩트 프레임에 닿지 못해 타격이 영영 빠진다.
            while (frameTimer >= frameTime)
            {
                frameTimer -= frameTime;
                currentFrameIndex = Mathf.Min(currentFrameIndex + 1, attackSprites.Count - 1); // 한 번만 재생하고 마지막 프레임에서 멈춘다(반복 없음)
            }

            // 공격 모션이 5번(마지막) 프레임에 도달하는 그 순간에만 실제 타격 판정을 1회 적용한다.
            int impactFrame = Mathf.Min(ImpactFrameIndex, attackSprites.Count - 1);
            if (!damageAppliedThisSwing && currentFrameIndex >= impactFrame)
            {
                if (!bChargingVfxFading) ReleaseChargingVfx(); // 실제로 "공격하는" 순간 - 충전이 흡수되며 소모되는 발사 연출로 넘긴다(헛스윙 취소로 페이드 중이면 그대로 페이드를 마치게 둔다)
                StopChargeSound(false); // 충전음도 발사음에 자리를 넘기며 끊는다
                if (!bDrySwing)
                {
                    ApplyDamageToCurrentTarget(); // 헛스윙이면 모션만 마무리하고 데미지/연쇄는 건너뛴다
                }
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
        // 대기 중에는 같은 프레임이 반복되므로 바뀐 경우에만 세터를 호출한다(이 메서드만 본체/그림자 스프라이트를 쓴다).
        bool bFrameChanged = !ReferenceEquals(_sprite, appliedFrameSprite) || _flipX != appliedFrameFlipX;
        if (bFrameChanged)
        {
            appliedFrameSprite = _sprite;
            appliedFrameFlipX = _flipX;
            spriteRenderer.sprite = _sprite;
            spriteRenderer.flipX = _flipX;
        }

        // Shadow Material이 SpriteRenderer.flipX를 무시하므로 localScale.x 부호로 뒤집는다 - 실제 스케일 적용은
        // 고도 기반 크기 조절과 함께 ApplyShadowScale이 한 곳에서 처리한다.
        if (shadowSpriteRenderer != null)
        {
            if (bFrameChanged) shadowSpriteRenderer.sprite = _sprite;
            shadowFlipX = _flipX;
            ApplyShadowScale();
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
