using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 목표 방향으로 날아가다가 사거리(화면 경계 부근)에서 완전히 멈추고, 잠깐 멈춰있다가
/// 다시 가속하며 소유자에게 돌아오는 투사체. ShockWave.cs와 동일하게 오브젝트 풀에서
/// 재사용된다(BoomerangCreator). 회전 스프라이트는 Animator/애니메이션 클립이 아니라
/// CharacterAnimator처럼 스크립트에서 프레임 리스트를 직접 재생하는 방식으로 처리한다
/// (Boomerang_Base 본체 + Boomerang_Effect 덧그림을 같은 인덱스로 24fps 동시 반복).
/// 비행 중에는 afterimageInterval마다 그 순간의 Base 프레임을 그 자리에 잔상으로 남기고
/// 알파를 서서히 줄인다(BoomerangAfterimage).
///
/// Outbound(감속)와 Returning(가속)은 완전히 대칭이다: 같은 시간(outboundDuration) 동안
/// 같은 최고 속도(throwSpeed)를 기준으로 선형으로 감속/가속한다. Returning은 최고 속도에
/// 도달한 뒤에는 캐릭터까지 거리와 무관하게 그 속도를 그대로 유지하며 쫓아가고,
/// catchRadius 안에 들어오는 순간에만 즉시 흡수(Finish)된다 — 근접 시 별도로 감속하지
/// 않으므로 "캐릭터 코앞에서 속도가 줄어 계속 따라다니는" 현상이 없다.
/// </summary>
public class Boomerang : MonoBehaviour
{
    // BoomerangCreator가 구독해서 풀로 되돌리는 용도로만 사용한다 (ShockWave.ReturnToPoolEvent와 동일한 역할).
    public event Action<Boomerang> ReturnToPoolEvent;

    private enum Phase { Outbound, Holding, Returning }

    [Header("Movement Settings")]
    [SerializeField] private float throwSpeed = 7f; // 던지는 순간의 최고 속도이자, 복귀 시 도달하는 최고 속도(대칭)
    [SerializeField] private float holdAtPeakDuration = 0.12f; // 사거리 끝에서 완전히 멈춰있는 시간
    [SerializeField] private float catchRadius = 0.25f;

    [Header("Sprite Animation (CharacterAnimator와 동일한 프레임 리스트 재생 방식)")]
    [SerializeField] private SpriteRenderer spriteRenderer; // Boomerang_Base
    [SerializeField] private SpriteRenderer effectSpriteRenderer; // Boomerang_Effect. 본체 바로 위에 덧그린다.
    [SerializeField] private List<Sprite> baseSprites;   // 비행 내내(왕복 전 구간) 반복 재생
    [SerializeField] private List<Sprite> effectSprites; // baseSprites와 같은 인덱스로 동시에 재생
    [SerializeField] private float sampleRate = 24f;

    [Header("Visual Size (공격 범위에 맞춤)")]
    // 스케일 1일 때 Boomerang_Base의 실제로 보이는(불투명) 가로폭(월드 유닛). 128x64 칸 안에서 그림이
    // 차지하는 폭은 프레임마다 89~96px인데, 가장 넓은 96px / 32PPU = 3.0. 리소스가 바뀌면 이 값만 다시 잰다.
    // 발사할 때 이 폭이 판정 지름(hitRadius * 2)과 같아지도록 스케일을 맞춰서, 눈에 보이는 부메랑 끝이
    // 곧 나무가 맞는 경계가 되게 한다("부메랑 범위" 스킬/과열로 hitRadius가 커지면 부메랑도 같이 커진다).
    [SerializeField] private float visualWidthAtUnitScale = 3f;

    [Header("Afterimage (잔상)")]
    [SerializeField] private float afterimageInterval = 0.28f; // 초당 약 3.5회
    [SerializeField] private float afterimageFadeDuration = 0.45f;
    [SerializeField, Range(0f, 1f)] private float afterimageStartAlpha = 0.5f;

    [Header("Shadow (LogItem과 동일한 방식)")]
    [SerializeField] private SpriteRenderer shadowSpriteRenderer; // Shadow Material을 쓰는 별도 렌더러. 본체와 동일한 프레임을 매 프레임 그대로 따라간다.

    [Header("Damage Settings")]
    [SerializeField] private LayerMask targetLayer; // 나무(Tree) 레이어
    [SerializeField] private float hitRadius = 0.5f; // 현재 위치 기준 판정 반경. 타일 1칸(Grid CellSize x=1)의 지름과 맞도록 반지름 0.5로 설정.
    [SerializeField] private float damageInterval = 0.3f; // 왕복 전 구간(가는 길/오는 길 모두) 동안 이 주기로 판정

    private Phase phase;
    private Vector3 originPosition;
    private Vector3 moveDirection;
    private float maxDistance;
    private float outboundDuration; // 등감속 총 소요 시간(2*maxDistance/throwSpeed). Returning의 가속 시간도 동일하게 재사용해서 대칭을 맞춘다.
    private float outboundTimer;
    private float holdTimer;
    private float returnTimer;
    private Transform returnTarget;
    private Action onFinished;

    private float frameTimer;
    private int currentFrameIndex;

    private BoomerangAfterimage[] afterimages; // 링 버퍼. 페이드 시간 동안 동시에 보일 수 있는 최대 장수만큼 미리 만든다.
    private int nextAfterimageIndex;
    private float afterimageTimer;

    private float damage;

    // 치명타는 발사 시점이 아니라 판정 틱마다 새로 굴린다. 확률과 배율은 발사 시점의 값을 스냅샷으로
    // 들고 있는데(과열 배율과 같은 이유), 비행 중 스킬 레벨이 올라도 이미 날아간 부메랑의 기대값이
    // 도중에 바뀌지 않게 하기 위해서다.
    private bool bCriticalEnabled;
    private float criticalChance;
    private float criticalDamageMul;

    private float damageCheckTimer;
    private Vector2 lastDamageCheckPosition; // 터널링 방지: 직전 판정 시점의 위치. 이 위치~현재 위치 사이 선분 전체를 검사한다.
    private readonly List<IStaticCollidable> hitScanResults = new List<IStaticCollidable>(16);

    // 캐릭터가 던진 부메랑만 true(BoomerangCreator가 발사마다 덮어씀). 나무의 도끼용 진동(TreeImpact/TreeDestroy)은
    // 항상 끄고, 대신 가벼운 BoomerangImpact를 울린다 - 판정 한 번에 여러 그루가 맞아도
    // HapticPresets의 묶음 간격이 한 번으로 묶어준다.
    private bool bPlayHaptic;

    private CustomSortable customSortable;

    // 그림자는 본체 자식이라 본체 스케일에 오프셋까지 같이 줄어든다. 오프셋은 "공중에 떠 있는 높이"라
    // 크기와 무관하게 월드 기준으로 유지해야 하므로, 프리팹의 원래 위치를 기억해 두고 스케일로 나눠 보정한다.
    private Vector3 shadowBaseLocalPosition;

    private bool isPaused; // WarningUI가 떠 있는 동안 그 자리에서 완전히 멈춘다 (이동/애니메이션/데미지 판정 전부 정지)
    private bool isDismissing; // 마을로 돌아가기 확정 시 축소 애니메이션 재생 중 (Update의 나머지 로직과 무관하게 별도 코루틴으로 처리)
    private Coroutine dismissRoutine;

    private float currentThrowSpeed;

    public bool IsActive { get; private set; }

    /// <summary>
    /// 속도 배율을 설정한다. (과열 시 3배)
    /// </summary>
    public void SetSpeedMultiplier(float _multiplier)
    {
        currentThrowSpeed = throwSpeed * _multiplier;
    }

    /// <summary>
    /// BoomerangCreator가 풀에서 꺼낼 때(OnGet) 설정하는 공격력. 도끼 스탯과 무관한 부메랑 전용 값이다.
    /// </summary>
    public void SetDamage(float _damage)
    {
        damage = _damage;
    }

    /// <summary>
    /// 치명타 설정을 넘긴다. 예전에는 BoomerangCreator가 발사 시점에 한 번만 굴려 damage에 곱해
    /// 넣었는데, 판정은 왕복 내내 damageInterval마다 계속 일어나므로 <b>한 부메랑이 왕복 전체를
    /// 통째로 치명타이거나 통째로 아니거나</b>가 되어 편차가 지나치게 컸다. 이제 판정 틱마다
    /// 새로 굴린다(ApplyDamageInRange 참고).
    /// </summary>
    public void SetCritical(bool _enabled, float _chance, float _damageMul)
    {
        bCriticalEnabled = _enabled;
        criticalChance = _chance;
        criticalDamageMul = _damageMul;
    }

    /// <summary>
    /// "부메랑 범위" 스킬 값을 반영하기 위해 BoomerangCreator가 풀에서 꺼낼 때(OnGet) 판정 반경을 덮어쓴다.
    /// </summary>
    public void SetHitRadius(float _hitRadius)
    {
        hitRadius = Mathf.Max(_hitRadius, 0f);
    }

    /// <summary>
    /// "부메랑 공격 속도" 스킬 값을 반영하기 위해 BoomerangCreator가 풀에서 꺼낼 때(OnGet) 판정 주기를 덮어쓴다.
    /// </summary>
    public void SetDamageInterval(float _damageInterval)
    {
        damageInterval = Mathf.Max(_damageInterval, 0.01f);
    }

    /// <summary>
    /// 타격 진동을 울릴지. 플레이어(캐릭터)가 던진 부메랑만 true다.
    /// </summary>
    public void SetPlayHaptic(bool _bPlayHaptic)
    {
        bPlayHaptic = _bPlayHaptic;
    }

    /// <summary>
    /// 부메랑을 발사한다. _returnTarget은 매 프레임 위치를 다시 읽으므로, 캐릭터가 이동 중이어도
    /// 그 방향으로 자연스럽게 돌아온다. _onFinished는 왕복이 끝나 풀로 돌아가기 직전에 1회 호출된다.
    /// </summary>
    public void Launch(Vector3 _origin, Vector3 _direction, float _maxDistance, Transform _returnTarget, Action _onFinished)
    {
        transform.position = _origin;
        originPosition = _origin;
        moveDirection = _direction.sqrMagnitude > 0.0001f ? _direction.normalized : Vector3.right;
        maxDistance = Mathf.Max(_maxDistance, 0.1f);
        returnTarget = _returnTarget;
        onFinished = _onFinished;

        if (currentThrowSpeed <= 0f) currentThrowSpeed = throwSpeed;

        // 등감속 운동 공식(d = v0*t - 0.5*a*t^2, v = v0 - a*t)에서 유도되는 총 소요 시간(t = 2d/v0).
        // ease-out 곡선 1-(1-t)^2 의 t=0 기울기가 throwSpeed와 정확히 일치하도록 이 값으로 정규화한다.
        outboundDuration = Mathf.Max(2f * maxDistance / Mathf.Max(currentThrowSpeed, 0.01f), 0.01f);
        outboundTimer = 0f;
        holdTimer = 0f;
        returnTimer = 0f;
        phase = Phase.Outbound;
        IsActive = true;

        frameTimer = 0f;
        currentFrameIndex = 0;
        ApplyCurrentFrame();

        afterimageTimer = 0f;

        damageCheckTimer = 0f;
        lastDamageCheckPosition = _origin;

        isPaused = false;
        isDismissing = false;
        dismissRoutine = null;
        ApplyVisualScale();
    }

    // hitRadius는 BoomerangCreator가 Launch 직전에 SetHitRadius로 넣어주므로(스킬/과열 반영), 여기서 매 발사마다
    // 다시 계산한다. DismissRoutine은 이 스케일에서 0으로 줄이고, 잔상은 lossyScale을 그대로 복사한다.
    private void ApplyVisualScale()
    {
        float scale = visualWidthAtUnitScale > 0f ? (hitRadius * 2f) / visualWidthAtUnitScale : 1f;
        scale = Mathf.Max(scale, 0.01f);
        transform.localScale = new Vector3(scale, scale, 1f);

        if (shadowSpriteRenderer != null)
        {
            shadowSpriteRenderer.transform.localPosition = shadowBaseLocalPosition / scale;
        }
    }

    /// <summary>
    /// 캐릭터가 죽거나 던전을 나가는 등, 왕복이 끝나기 전에 강제로 회수해야 할 때 사용한다.
    /// </summary>
    public void ForceStop()
    {
        if (!IsActive) return;

        if (dismissRoutine != null)
        {
            StopCoroutine(dismissRoutine);
            dismissRoutine = null;
        }

        // 정상 회수(Finish)와 달리 강제 회수는 캐릭터 사망/던전 이탈 등이라 남은 잔상도 바로 지운다.
        HideAllAfterimages();
        Finish();
    }

    /// <summary>
    /// WarningUI가 뜨는 동안 그 자리에서 완전히 멈춘다. 이동/애니메이션/데미지 판정이 모두 정지된다.
    /// </summary>
    public void Pause()
    {
        if (!IsActive || isDismissing) return;
        isPaused = true;
        SetAfterimagesPaused(true);
    }

    /// <summary>
    /// Pause() 이전 상태 그대로 이어서 다시 움직인다.
    /// </summary>
    public void Resume()
    {
        if (!IsActive || isDismissing) return;
        isPaused = false;
        SetAfterimagesPaused(false);
    }

    /// <summary>
    /// 마을로 돌아가기가 확정됐을 때 호출한다. 그 자리에서 스케일을 0으로 줄이며 사라진 뒤 풀로 돌아간다.
    /// </summary>
    public void DismissWithShrink(float _duration = 0.25f)
    {
        if (!IsActive || isDismissing) return;

        isDismissing = true;
        isPaused = true; // 축소되는 동안 이동/판정/애니메이션은 멈춘 상태를 유지
        SetAfterimagesPaused(false); // Pause()로 멈춰있던 잔상은 본체가 줄어드는 동안 마저 사라지게 둔다
        dismissRoutine = StartCoroutine(DismissRoutine(_duration));
    }

    private System.Collections.IEnumerator DismissRoutine(float _duration)
    {
        Vector3 startScale = transform.localScale;
        float duration = Mathf.Max(_duration, 0.01f);
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, Mathf.Clamp01(t / duration));
            yield return null;
        }

        transform.localScale = Vector3.zero;
        dismissRoutine = null;
        Finish();
    }

    private void Awake()
    {
        customSortable = GetComponent<CustomSortable>();
        if (customSortable != null)
        {
            // Effect 렌더러는 본체와 같은 order면 그리는 순서가 보장되지 않으므로 여기서 빼고,
            // LateUpdate에서 본체 order + 1로 따로 맞춘다.
            customSortable.Initialize(transform, new[] { spriteRenderer, shadowSpriteRenderer });
        }

        if (shadowSpriteRenderer != null)
        {
            shadowBaseLocalPosition = shadowSpriteRenderer.transform.localPosition;
        }

        CreateAfterimages();
    }

    private void OnDestroy()
    {
        if (afterimages == null) return;

        for (int i = 0; i < afterimages.Length; i++)
        {
            if (afterimages[i] != null)
            {
                Destroy(afterimages[i].gameObject);
            }
        }
    }

    private void Update()
    {
        if (!IsActive || isPaused) return;

        UpdateAnimationFrame(Time.deltaTime);
        UpdateDamageTick(Time.deltaTime); // 가는 길/오는 길 구분 없이 왕복 내내 동일하게 판정

        switch (phase)
        {
            case Phase.Outbound: UpdateOutbound(); break;
            case Phase.Holding: UpdateHolding(); break;
            case Phase.Returning: UpdateReturning(); break;
        }

        // Finish()로 이번 프레임에 회수됐으면 잔상을 찍지 않는다.
        if (IsActive)
        {
            UpdateAfterimage(Time.deltaTime);
        }
    }

    // Character/TreeObj 등 다른 월드 오브젝트와 동일하게, 실제 정렬 순서 갱신은 LateUpdate에서
    // 수행한다(그 프레임의 최종 이동이 끝난 뒤 정렬해야 한 프레임 밀리는 현상이 없다).
    private void LateUpdate()
    {
        if (customSortable != null)
        {
            customSortable.ManualLateUpdate();

            if (effectSpriteRenderer != null)
            {
                effectSpriteRenderer.sortingOrder = customSortable.CurrentSortingOrder + 1;
            }
        }
    }

    // 잔상은 본체가 풀로 돌아가 비활성화돼도 끝까지 페이드돼야 하므로 자식이 아닌 루트 오브젝트로 만든다.
    // 본체(BoomerangCreator)와 같은 이유로 DontDestroyOnLoad, 수명은 OnDestroy에서 본체와 함께 정리한다.
    private void CreateAfterimages()
    {
        int count = Mathf.CeilToInt(afterimageFadeDuration / Mathf.Max(afterimageInterval, 0.01f)) + 1;
        afterimages = new BoomerangAfterimage[count];

        for (int i = 0; i < count; i++)
        {
            afterimages[i] = BoomerangAfterimage.Create(spriteRenderer, "BoomerangAfterimage");
            DontDestroyOnLoad(afterimages[i].gameObject);
        }

        nextAfterimageIndex = 0;
    }

    private void UpdateAfterimage(float _deltaTime)
    {
        if (afterimages == null || afterimages.Length == 0 || spriteRenderer == null) return;

        afterimageTimer += _deltaTime;
        if (afterimageTimer < afterimageInterval) return;
        afterimageTimer -= afterimageInterval;

        // 찍히는 순간 본체 바로 뒤에 깔리도록 현재 위치 기준 정렬값 - 1. 잔상은 제자리에 머무므로 이후 갱신하지 않는다.
        int sortingOrder = customSortable != null
            ? customSortable.ComputeSortingOrder(transform.position.y) - 1
            : spriteRenderer.sortingOrder - 1;

        BoomerangAfterimage afterimage = afterimages[nextAfterimageIndex];
        nextAfterimageIndex = (nextAfterimageIndex + 1) % afterimages.Length;

        afterimage.Show(
            spriteRenderer.sprite,
            spriteRenderer.color,
            spriteRenderer.transform.position,
            spriteRenderer.transform.lossyScale,
            sortingOrder,
            afterimageStartAlpha,
            afterimageFadeDuration);
    }

    private void SetAfterimagesPaused(bool _paused)
    {
        if (afterimages == null) return;

        for (int i = 0; i < afterimages.Length; i++)
        {
            afterimages[i].SetPaused(_paused);
        }
    }

    private void HideAllAfterimages()
    {
        if (afterimages == null) return;

        for (int i = 0; i < afterimages.Length; i++)
        {
            afterimages[i].Hide();
        }
    }

    // ShockWave.ApplyShockWaveDamage와 달리 hitTargets로 1회 제한을 두지 않는다: damageInterval마다
    // 판정 반경 안의 나무를 전부 다시 때려서, 부메랑이 나무 근처에 오래 머물수록 여러 번 맞을 수 있다.
    private void UpdateDamageTick(float _deltaTime)
    {
        damageCheckTimer += _deltaTime;
        if (damageCheckTimer < damageInterval) return;
        damageCheckTimer -= damageInterval;

        ApplyDamageInRange();
    }

    // CollisionSystem은 나무의 밑동(콜라이더 Position) 기준으로만 거리를 재기 때문에, topRoot(나뭇잎 쪽,
    // Character가 조준할 때 쓰는 그 지점)까지 고려하지 못한다. 그래서 일단 넉넉한 반경(TopRootScanMargin
    // 만큼 확장)으로 후보만 넓게 걸러내고, 실제 판정은 각 나무의 topRoot 위치와의 거리로 다시 확인한다.
    // topRoot는 밑동보다 정확히 0.75 위에 있으므로(삼각부등식상 밑동-거리와 topRoot-거리의 차이는
    // 최대 0.75), 그 값과 정확히 맞춰서 후보를 놓치지 않는 최소한의 여유만 둔다.
    private const float TopRootScanMargin = 0.75f;

    // damageInterval(예: 0.3초)마다 "현재 위치 한 점"만 검사하면, throwSpeed가 빠를 때 그 간격 동안
    // 이동한 거리가 hitRadius보다 커져서 나무를 그냥 통과(터널링)해버릴 수 있다. 그래서 직전 판정
    // 위치(lastDamageCheckPosition)부터 현재 위치까지의 선분 전체를 훑어서, 그 사이 어느 순간이든
    // 나무가 hitRadius 안에 들어왔으면 놓치지 않고 맞은 것으로 처리한다.
    private void ApplyDamageInRange()
    {
        if (CollisionSystem.Instance == null) return;

        Vector2 segStart = lastDamageCheckPosition;
        Vector2 segEnd = transform.position;

        Vector2 scanCenter = (segStart + segEnd) * 0.5f;
        float scanRadius = Vector2.Distance(segStart, segEnd) * 0.5f + hitRadius + TopRootScanMargin;

        CollisionSystem.Instance.GetCollidablesInRadius(scanCenter, scanRadius, targetLayer.value, hitScanResults);

        float hitRadiusSqr = hitRadius * hitRadius;

        // 치명타는 이 틱에 대해 한 번만 굴리고, 이번 틱에 맞은 나무 전체에 같은 결과를 적용한다.
        // (나무 하나하나에 따로 굴리고 싶다면 이 두 줄을 아래 루프 안으로 옮기면 된다)
        float tickDamage = damage;
        if (bCriticalEnabled && UnityEngine.Random.value < criticalChance)
        {
            tickDamage *= criticalDamageMul;
        }

        bool bHitAny = false;

        for (int i = 0; i < hitScanResults.Count; i++)
        {
            // 이 루프 안에서 앞의 나무가 죽으며 연쇄(과열 폭발 등)로 뒤쪽 후보가 먼저 죽어 풀로 반납될 수 있다.
            // 반납 시 ResetTree가 bDead를 false로 되돌리므로 IsPooled로 함께 거른다.
            if (hitScanResults[i] is TreeObj treeObj && !treeObj.bDead && !treeObj.IsPooled)
            {
                // topRoot/밑둥 둘 중 하나라도 이동 경로(선분)에 판정 반경만큼 가까웠으면 맞은 것으로
                // 처리한다. ||는 short-circuit이라 topRoot에서 이미 맞았으면 밑동 거리는 계산하지
                // 않고, 두 지점이 동시에 맞아도 TakeDamage는 이 한 번만 호출되어 중복 데미지가 없다.
                bool isHit = DistancePointToSegmentSqr(GetTreeTopPosition(treeObj), segStart, segEnd) <= hitRadiusSqr
                    || DistancePointToSegmentSqr(treeObj.Position, segStart, segEnd) <= hitRadiusSqr;

                if (isHit && treeObj.bCanApplyDamage) // 묘목은 TakeDamage가 무시하므로 진동도 울리지 않게 미리 거른다
                {
                    treeObj.TakeDamage(tickDamage, false);
                    bHitAny = true;
                }
            }
        }

        // 여러 그루가 맞아도 판정 한 번에 진동 한 번.
        if (bHitAny && bPlayHaptic)
        {
            Rumble.Play(EHapticEvent.BoomerangImpact);
        }

        lastDamageCheckPosition = segEnd;
    }

    private static Vector2 GetTreeTopPosition(TreeObj _treeObj)
    {
        return _treeObj.treeVisualComponent != null ? (Vector2)_treeObj.treeVisualComponent.GetTopRootPosition() : _treeObj.Position;
    }

    private static float DistancePointToSegmentSqr(Vector2 _p, Vector2 _a, Vector2 _b)
    {
        Vector2 ab = _b - _a;
        float ab2 = Vector2.Dot(ab, ab);
        if (ab2 < 0.0001f) return (_p - _a).sqrMagnitude; // 선분 길이가 거의 0이면 점 판정과 동일

        float t = Mathf.Clamp01(Vector2.Dot(_p - _a, ab) / ab2);
        Vector2 closest = _a + ab * t;
        return (_p - closest).sqrMagnitude;
    }

    // ease-out 곡선(1-(1-t)^2)으로 위치를 직접 계산한다. t=0에서 속도 throwSpeed로 출발해
    // t=1(= maxDistance 도달)에서 속도가 정확히 0이 되고, 매 프레임 "절대 위치"를 다시 계산하는
    // 방식이라 이전처럼 누적 이동 오차를 끝에서 한번에 보정("점프")할 필요가 없다.
    private void UpdateOutbound()
    {
        outboundTimer += Time.deltaTime;
        float t = Mathf.Clamp01(outboundTimer / outboundDuration);
        float eased = 1f - (1f - t) * (1f - t);

        transform.position = originPosition + moveDirection * (maxDistance * eased);

        if (t >= 1f)
        {
            phase = Phase.Holding;
            holdTimer = 0f;
        }
    }

    private void UpdateHolding()
    {
        holdTimer += Time.deltaTime;
        if (holdTimer >= holdAtPeakDuration)
        {
            phase = Phase.Returning;
            returnTimer = 0f; // Holding의 속도 0과 정확히 이어지도록 가속 타이머를 0부터 다시 시작
        }
    }

    // Outbound의 등감속(throwSpeed → 0, outboundDuration초)과 완전히 대칭이 되도록, 같은 시간 동안
    // 0 → throwSpeed로 선형 가속한다. 최고 속도 도달 후에는 거리와 무관하게 그 속도를 유지한 채
    // 캐릭터의 현재 위치를 계속 쫓아가며(매 프레임 방향 재계산), catchRadius 안에 들어오는 순간에만
    // 즉시 흡수한다. 근접 감속을 두지 않아 "캐릭터 코앞에서 느려져 계속 따라다니는" 현상이 없다.
    private void UpdateReturning()
    {
        if (returnTarget == null)
        {
            Finish();
            return;
        }

        Vector3 toTarget = returnTarget.position - transform.position;
        float dist = toTarget.magnitude;

        if (dist <= catchRadius)
        {
            Finish();
            return;
        }

        returnTimer += Time.deltaTime;

        float t = outboundDuration > 0f ? Mathf.Clamp01(returnTimer / outboundDuration) : 1f;
        float velocity = currentThrowSpeed * t;

        float step = Mathf.Min(velocity * Time.deltaTime, dist);
        transform.position += (toTarget / Mathf.Max(dist, 0.0001f)) * step;
    }

    // CharacterAnimator.UpdateAnimation()과 같은 프레임 리스트 재생. Base/Effect는 같은 인덱스로
    // 묶여 있어서(둘 다 16프레임) 한 타이머로 동시에 넘긴다. 왕복이 끝날 때까지 계속 반복한다.
    private void UpdateAnimationFrame(float _deltaTime)
    {
        if (baseSprites == null || baseSprites.Count == 0) return;

        float frameTime = sampleRate > 0f ? 1f / sampleRate : 0.1f;

        frameTimer += _deltaTime;
        if (frameTimer < frameTime) return;

        while (frameTimer >= frameTime)
        {
            frameTimer -= frameTime;
            currentFrameIndex = (currentFrameIndex + 1) % baseSprites.Count;
        }

        ApplyCurrentFrame();
    }

    private void ApplyCurrentFrame()
    {
        if (spriteRenderer != null && baseSprites != null && baseSprites.Count > 0)
        {
            Sprite baseSprite = baseSprites[currentFrameIndex % baseSprites.Count];
            spriteRenderer.sprite = baseSprite;

            // TreeVisualComponent.SyncShadowSprite와 동일한 방식: 그림자는 본체와 항상 같은 프레임을 보여준다.
            if (shadowSpriteRenderer != null)
            {
                shadowSpriteRenderer.sprite = baseSprite;
            }
        }

        if (effectSpriteRenderer != null && effectSprites != null && effectSprites.Count > 0)
        {
            effectSpriteRenderer.sprite = effectSprites[currentFrameIndex % effectSprites.Count];
        }
    }

    private void Finish()
    {
        IsActive = false;
        returnTarget = null;

        Action callback = onFinished;
        onFinished = null;
        callback?.Invoke();

        ReturnToPoolEvent?.Invoke(this);
    }
}
