using System;
using UnityEngine;

/// <summary>
/// 4스테이지(MagmaForest) 맵 규칙인 과열 수치. 불씨를 주울 때마다 오르고(0~최대치, 최대치는 기본 100이며 특성이 N씩 올린다. 특성에 따라 소수점도 가능)
/// 줄어들지 않으며, 원정이 바뀔 때(던전 입장·마을 귀환·재도전)만 0으로 돌아간다. 특성 없이 작동한다.
///
/// 공식에는 항상 내림한 정수(OverheatLevel, HUD에 보이는 값)를 쓴다.
/// - 피해: 화상을 뺀 모든 공격의 마지막에 (1 + 과열 * overheatDamagePerLevel * 효율)을 곱한다(TreeObj가 InDungeonObjectManager를 통해 읽는다).
/// - 도끼 공격속도: 마지막에 (1 + 과열 * overheatAxeAttackSpeedPerLevel * 효율)을 곱한다(StatComponent.overheatAxeAttackSpeedMultiplier).
/// 효율은 "과열 효율 증가" 특성 배율(StatComponent.overheatEfficiencyMultiplier, 특성이 없으면 1)이다.
/// </summary>
public class OverheatComponent : PComponent, IOverheatComponent
{
    // 이펙트 단계(1~33 예열 / 34~66 가열 / 67~ 푸른 불꽃)의 시작 값. 0은 과열 없음.
    private static readonly int[] StageStartLevels = { 1, 34, 67 };

    // 과열 수치는 0.01 단위 정수로 쌓는다. 소수 획득량(예: 0.1씩)을 float로 더하면 오차가 누적되어(1000번이면 99.99…)
    // 정수 경계를 못 넘고, 내림한 값(HUD·공식)이 1 낮게 나온다.
    private const int ValueScale = 100;

    private int overheatScaled = 0;
    private int overheatLevel = 0;
    private int overheatStage = 0;

    [Header("VFX Settings")]
    [SerializeField] private VFXComponent vfxComponent;
    [SerializeField] private string overheatVfxTag = "OverheatLoopEffect";
    [Tooltip("이펙트 단계(1~3)별 루프 이펙트 태그. 비어 있는 단계는 overheatVfxTag를 쓴다. 단계가 바뀔 때 태그가 다르면 이펙트를 바꿔 낀다.")]
    [SerializeField] private string[] stageVfxTags = new string[3];
    [SerializeField] private Vector3 vfxOffset = new Vector3(0f, 0.5f, 0f);

    private ParticleSystem activeVfx;
    private string activeVfxTag;
    private ParticleSystemRenderer activeVfxRenderer;
    private PresentationLayer.VFX.VFX_OverheatAura activeAura; // 켜진 오라의 단계 안 진행도(SetStageProgress)를 넣을 대상. 없으면 null
    private int appliedVfxSortingOrder = int.MinValue; // Update가 마지막으로 적용한 소팅 오더(같으면 세터 생략)
    // 꺼지거나 다른 단계 이펙트로 바뀌어 페이드아웃 중인 오라. 페이드 도중 다시 켜지면 이 인스턴스를 즉시 회수해
    // 새 오라와 두 개가 겹쳐 보이지 않게 한다.
    private ParticleSystem fadingVfx;
    private CustomSortable customSortable;

    public bool IsActive => overheatLevel > 0;
    public float OverheatValue => (float)overheatScaled / ValueScale;
    public int OverheatLevel => overheatLevel;
    public int Stage => overheatStage;
    public float StageProgress => CalculateStageProgress(overheatLevel, overheatStage);
    // 캐릭터 초기화(Initialize) 전에 UI 등이 읽어도 터지지 않도록 스탯이 없으면 기본값을 쓴다.
    private StatComponent Stat => ctx != null ? ctx.characterStat : null;
    public float MaxValue => Stat != null ? Stat.overheatMaxValue : 100f;
    private int MaxScaled => Mathf.RoundToInt(MaxValue * ValueScale);
    public bool IsMax => overheatScaled >= MaxScaled;
    public float DamageMultiplier => Stat != null ? 1f + overheatLevel * Stat.overheatDamagePerLevel * Stat.overheatEfficiencyMultiplier : 1f;
    public float AxeAttackSpeedMultiplier => Stat != null ? 1f + overheatLevel * Stat.overheatAxeAttackSpeedPerLevel * Stat.overheatEfficiencyMultiplier : 1f;

    public event Action<bool> OverheatStateChangedEvent;
    public event Action<int> OverheatLevelChangedEvent;
    public event Action<int> OverheatStageChangedEvent;
    public event Action<float> OverheatMaxChangedEvent;

    // 최대치 변경 구독용. 구독/해제에 같은 델리게이트 인스턴스를 써야 하므로 한 번만 만든다.
    private StatComponent subscribedStat;
    private Action<float> cachedOnMaxValueChanged;

    public override void Initialize(ComponentCtx _ctx)
    {
        base.Initialize(_ctx);
        customSortable = GetComponentInParent<CustomSortable>();

        UnbindStat();
        subscribedStat = Stat;
        if (null != subscribedStat)
        {
            if (null == cachedOnMaxValueChanged)
                cachedOnMaxValueChanged = OnMaxValueChanged;

            subscribedStat.OverheatMaxValueChangedEvent += cachedOnMaxValueChanged;
        }
    }

    private void UnbindStat()
    {
        if (null == subscribedStat)
            return;

        subscribedStat.OverheatMaxValueChangedEvent -= cachedOnMaxValueChanged;
        subscribedStat = null;
    }

    // 최대치가 내려가 지금 수치가 넘치면 새 최대치로 잘라낸다(단계 · 공속 배율은 Refresh가 다시 맞춘다).
    // 올라간 경우는 수치는 그대로이고 IsMax만 풀린다(불씨를 다시 주울 수 있다).
    private void OnMaxValueChanged(float _maxValue)
    {
        if (overheatScaled > MaxScaled)
        {
            overheatScaled = MaxScaled;
            Refresh();
        }

        OverheatMaxChangedEvent?.Invoke(MaxValue);
    }

    /// <summary>
    /// 불씨를 주워 과열 수치를 올린다. 이미 최대면 아무 효과가 없고 false를 돌려준다.
    /// </summary>
    public bool AddOverheat(float _amount)
    {
        int amountScaled = Mathf.RoundToInt(_amount * ValueScale);
        if (amountScaled <= 0 || IsMax) return false;

        overheatScaled = Mathf.Min(overheatScaled + amountScaled, MaxScaled);
        Refresh();
        return true;
    }

    /// <summary>원정이 바뀔 때(Character.SetWhereIsCharacter) 0으로 되돌린다.</summary>
    public void ResetOverheat()
    {
        overheatScaled = 0;
        Refresh();
    }

    private void Refresh()
    {
        int newLevel = overheatScaled / ValueScale;
        if (newLevel == overheatLevel) return;

        bool wasActive = IsActive;
        overheatLevel = newLevel;

        Stat?.SetOverheatAxeAttackSpeedMultiplier(AxeAttackSpeedMultiplier);

        int newStage = CalculateStage(overheatLevel);
        if (newStage != overheatStage)
        {
            overheatStage = newStage;
            RefreshLoopVfx();
            OverheatStageChangedEvent?.Invoke(overheatStage);
        }

        ApplyStageProgress();
        OverheatLevelChangedEvent?.Invoke(overheatLevel);

        if (wasActive != IsActive)
        {
            OverheatStateChangedEvent?.Invoke(IsActive);
        }
    }

    private static int CalculateStage(int _level)
    {
        int stage = 0;
        for (int i = 0; i < StageStartLevels.Length; i++)
        {
            if (_level >= StageStartLevels[i]) stage = i + 1;
        }
        return stage;
    }

    // 지금 단계 안에서의 진행도(0~1). 단계 시작 값이 0, 다음 단계 직전 값이 1이고 마지막 단계는 항상 1이다.
    private static float CalculateStageProgress(int _level, int _stage)
    {
        if (0 >= _stage || StageStartLevels.Length <= _stage) return 1f;

        int start = StageStartLevels[_stage - 1];
        int end = StageStartLevels[_stage] - 1;
        if (end <= start) return 1f;

        return Mathf.Clamp01((float)(_level - start) / (end - start));
    }

    // 켜진 오라에 단계 안 진행도를 넣는다(예열 불티 양, 가열 불꽃 높이, 불씨 양이 따라 오른다)
    private void ApplyStageProgress()
    {
        if (null == activeAura) return;
        activeAura.SetStageProgress(CalculateStageProgress(overheatLevel, overheatStage));
    }

    private void Update()
    {
        if (activeVfxRenderer == null || customSortable == null) return;

        // 매 프레임 캐릭터 본체의 SortingOrder를 추적하여 동기화(값이 바뀐 프레임에만 세터 호출)
        int targetOrder = customSortable.CurrentSortingOrder + 1;
        if (targetOrder != appliedVfxSortingOrder)
        {
            appliedVfxSortingOrder = targetOrder;
            activeVfxRenderer.sortingOrder = targetOrder;
        }
    }

    private string GetStageVfxTag(int _stage)
    {
        int index = _stage - 1;
        if (stageVfxTags != null && index >= 0 && index < stageVfxTags.Length && !string.IsNullOrEmpty(stageVfxTags[index]))
        {
            return stageVfxTags[index];
        }
        return overheatVfxTag;
    }

    private void RefreshLoopVfx()
    {
        if (vfxComponent == null) return;

        string targetTag = overheatStage > 0 ? GetStageVfxTag(overheatStage) : null;
        if (activeVfx != null && targetTag == activeVfxTag) return;

        // 꺼져서 사그라드는 중에 다시 켜지는 경우(재점화)에만 그 오라를 즉시 회수해 두 개가 겹쳐 보이지 않게 한다.
        // 단계가 바뀌어 다른 이펙트로 갈아 끼우는 경우에는 이전 단계 오라가 사그라드는 연출을 그대로 둔다.
        if (activeVfx == null && fadingVfx != null && false == string.IsNullOrEmpty(targetTag))
        {
            if (fadingVfx.gameObject.activeSelf)
            {
                vfxComponent.Stop(fadingVfx, true);
            }
            fadingVfx = null;
        }

        StopActiveVfx();

        if (string.IsNullOrEmpty(targetTag)) return;

        int sortingOrder = customSortable != null ? customSortable.CurrentSortingOrder + 1 : 0;

        activeVfx = vfxComponent.Play(new VFXPlaySettings(
            targetTag,
            transform.position + vfxOffset,
            Quaternion.identity,
            sortingOrder,
            transform
        ));

        if (activeVfx != null)
        {
            activeVfxTag = targetTag;
            activeVfxRenderer = activeVfx.GetComponent<ParticleSystemRenderer>();
            activeAura = activeVfx.GetComponentInChildren<PresentationLayer.VFX.VFX_OverheatAura>(true);
            appliedVfxSortingOrder = int.MinValue; // 새 인스턴스이므로 첫 프레임에 반드시 적용
        }
    }

    private void StopActiveVfx()
    {
        if (activeVfx == null) return;

        if (fadingVfx != null && fadingVfx.gameObject.activeSelf)
        {
            vfxComponent.Stop(fadingVfx, true);
        }

        vfxComponent.Stop(activeVfx, false);
        fadingVfx = activeVfx;
        activeVfx = null;
        activeVfxTag = null;
        activeVfxRenderer = null;
        activeAura = null;
    }

    private void OnDestroy()
    {
        UnbindStat();
    }
}
