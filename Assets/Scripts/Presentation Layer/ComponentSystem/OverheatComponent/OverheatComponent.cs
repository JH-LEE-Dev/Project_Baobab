using UnityEngine;

public class OverheatComponent : PComponent
{
    private const float MoveSpeedBonus = 20f;
    private const float AttackSpeedBonus = 20f;
    private const float AxeDamageBonus = 20f;

    private float overheatTimer = 0f;
    private bool bActive = false;

    // ActivateBuff 시점의 "과열 강화" 배율을 스냅샷으로 저장해둔다 - 버프가 유지되는 동안 스킬 레벨업으로
    // overheatEfficiencyBonus가 바뀌어도, 실제로 더한 만큼만 정확히 되돌릴 수 있도록 한다.
    private float appliedMoveSpeedBonus = 0f;
    private float appliedAttackSpeedBonus = 0f;
    private float appliedAxeDamageBonus = 0f;

    [Header("VFX Settings")]
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private string overheatVfxTag = "OverheatLoopEffect";
    [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 0.5f, 0f);
    
    private ParticleSystem activeVfx;
    private ParticleSystemRenderer activeVfxRenderer;
    private CustomSortable customSortable;

    public bool IsActive => bActive;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);
        customSortable = GetComponentInParent<CustomSortable>();
    }

    // 나무 열기(+15초), 용암 열기(초당 +2초) 등 "열기에 닿는" 것으로 열기 피해를 받을 때마다 호출된다.
    // "과열" 특성이 없으면 아무 효과가 없고, 지속시간은 상한 없이 계속 합산된다.
    // "열기 포집" 특성이 있으면 그 비율만큼 획득량이 늘어난다.
    public void AddOverheatDuration(float _seconds)
    {
        if (!ctx.characterStat.bOverheat || _seconds <= 0f) return;

        float gainMultiplier = 1f + (ctx.characterStat.overheatGainBonusAlpha / 100f);
        AddOverheatDurationRaw(_seconds * gainMultiplier);
    }

    // "열기 포집" 배율이 적용되지 않는 직접 지속시간 추가. "열기 회수"(나무 벌목 시 회복)처럼 열기 접촉이
    // 아닌 별도의 회복원에서 사용한다.
    public void AddOverheatDurationRaw(float _seconds)
    {
        if (!ctx.characterStat.bOverheat || _seconds <= 0f) return;

        overheatTimer += _seconds;

        if (!bActive)
        {
            ActivateBuff();
        }
    }

    // 던전을 나가는 등 강제로 버프를 끝내야 할 때 호출. 이미 비활성이면 아무 일도 하지 않는다.
    public void ForceEnd()
    {
        if (!bActive) return;
        DeactivateBuff();
    }

    // "화신" 특성이 있으면 던전에 입장하는 즉시 과열 상태로 진입해 그 상태를 계속 유지한다.
    // 던전 입장 시점(Character.SetWhereIsCharacter)에서만 호출되므로, 마을 등 던전 밖에서는 켜지지 않는다.
    public void TryActivatePermanent()
    {
        if (ctx.characterStat.bOverheat && ctx.characterStat.bOverheatPermanent && !bActive)
        {
            ActivateBuff();
        }
    }

    private void Update()
    {
        if (!bActive) return;

        // 매 프레임 캐릭터 본체의 SortingOrder를 추적하여 동기화
        if (activeVfxRenderer != null && customSortable != null)
        {
            activeVfxRenderer.sortingOrder = customSortable.CurrentSortingOrder + 1;
        }

        if (ctx.characterStat.bOverheatPermanent) return; // "화신" - 지속시간이 소모되지 않는다

        // "과열 유지" 특성만큼 지속시간 소모 속도가 줄어든다.
        float consumptionMultiplier = Mathf.Max(0f, 1f - (ctx.characterStat.overheatConsumptionReductionAlpha / 100f));
        overheatTimer -= Time.deltaTime * consumptionMultiplier;
        if (overheatTimer <= 0f)
        {
            DeactivateBuff();
        }
    }

    private void ActivateBuff()
    {
        bActive = true; // 스탯 적용보다 먼저 세워서, 같은 프레임에 트리거가 겹쳐도 중복 적용되지 않게 한다.

        // "과열 강화" 특성만큼 버프 효율이 증가한다 (100%면 기본 20%가 40%가 된다).
        float efficiencyMultiplier = 1f + (ctx.characterStat.overheatEfficiencyBonus / 100f);
        appliedMoveSpeedBonus = MoveSpeedBonus * efficiencyMultiplier;
        appliedAttackSpeedBonus = AttackSpeedBonus * efficiencyMultiplier;
        appliedAxeDamageBonus = AxeDamageBonus * efficiencyMultiplier;

        ctx.characterStat.IncreaseMovementSpeed(appliedMoveSpeedBonus);
        ctx.characterStat.IncreaseAxeAttackSpeed(appliedAttackSpeedBonus);
        ctx.characterStat.IncreaseAxeDamage(appliedAxeDamageBonus);

        if (vfxComponent != null && !string.IsNullOrEmpty(overheatVfxTag))
        {
            int sortingOrder = customSortable != null ? customSortable.CurrentSortingOrder + 1 : 0;

            activeVfx = vfxComponent.Play(new VFXPlaySettings(
                overheatVfxTag,
                transform.position + vfxOffset,
                Quaternion.identity,
                sortingOrder,
                transform
            ));

            if (activeVfx != null)
            {
                activeVfxRenderer = activeVfx.GetComponent<ParticleSystemRenderer>();
            }
        }
    }

    private void DeactivateBuff()
    {
        bActive = false;
        overheatTimer = 0f;
        ctx.characterStat.IncreaseMovementSpeed(-appliedMoveSpeedBonus);
        ctx.characterStat.IncreaseAxeAttackSpeed(-appliedAttackSpeedBonus);
        ctx.characterStat.IncreaseAxeDamage(-appliedAxeDamageBonus);

        if (vfxComponent != null && activeVfx != null)
        {
            vfxComponent.Stop(activeVfx, false);
            activeVfx = null;
            activeVfxRenderer = null;
        }
    }
}
