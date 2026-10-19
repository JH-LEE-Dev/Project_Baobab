using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기획서 v2.1 부록 A "밸런스 수치"를 한곳에 모은 데이터. 코드에는 수치를 두지 않는다.
///
/// 처음 만든 에셋은 비어 있다. 인스펙터 우측 메뉴의 "기획서 v2.1 부록 A 초안 값으로 채우기"를 누르면
/// 부록 A의 초안 값이 들어간다. 값은 모두 초안이며 프로토타입 실측 후 덮어쓴다.
///
/// 연출 시간(진입 연출·카드 연출·코인 연출)은 이 에셋이 아니라 연출 쪽 설정에서 관리한다.
/// </summary>
[CreateAssetMenu(fileName = "RoguelikeBalance", menuName = "Baobab/Roguelike/Balance")]
public class RoguelikeBalanceSO : ScriptableObject
{
    /// <summary>한 티어 안의 나무 종류 수(초기 · 중간 · 후반). 기획서 7-1 "나무 비주얼 진화".</summary>
    public const int TreeKindsPerTier = 3;

    [Serializable]
    public struct TierRow
    {
        [Tooltip("이 티어가 시작되는 웨이브(1부터). 첫 티어는 반드시 1.")]
        public int startWave;
        [Tooltip("임계치 피해 1회의 스태미너 피해량.")]
        public float staminaDamage;
        [Tooltip("나무 1그루당 골드. 소수는 웨이브 종료 시 합산해 반올림한다(부록 A-1).")]
        public float goldPerTree;
    }

    [Serializable]
    public struct WaveRow
    {
        [Tooltip("목표 처치 수.")]
        public int killTarget;
        [Tooltip("나무 1그루 스폰 주기(초) = 1 ÷ 초당 스폰.")]
        public float spawnInterval;
        [Tooltip("이 웨이브 나무의 절대 HP(도끼 공격력 10 기준). 나무 종류와 무관하게 이 값을 쓴다(부록 A-2).")]
        public float treeHp;
        [Tooltip("티어 안의 나무 종류 순번. 0 = 초기, 1 = 중간, 2 = 후반(기획서 7-1). 실제 종류는 RoguelikeSystem의 티어 지형 설정에 있다.")]
        public int treeKind;
    }

    [Serializable]
    public struct AppraisalOutcome
    {
        [Tooltip("현재 레벨 유지 가중치.")]
        public float stay;
        [Tooltip("+1 레벨 가중치.")]
        public float plusOne;
        [Tooltip("+2 레벨 가중치.")]
        public float plusTwo;
        [Tooltip("MAX 가중치.")]
        public float max;
    }

    public enum ERerollCostMode
    {
        Linear = 0,
        Multiply = 1,
    }

    [Header("웨이브")]
    [Tooltip("이 웨이브를 클리어하면 런 클리어(이후 언리밋).")]
    public int clearWave = 30;
    [Tooltip("웨이브별 목표 처치 수·스폰 주기·나무 HP·나무 종류. [0]=웨이브 1. 모자라면 마지막 줄을 그대로 쓴다.")]
    public List<WaveRow> waves = new List<WaveRow>();
    [Tooltip("티어 구간. startWave 오름차순.")]
    public List<TierRow> tiers = new List<TierRow>();
    [Tooltip("티어 안에서 나무 종류가 바뀌는 웨이브에 이전 종류가 섞여 나오는 비율(0~1). 기획서 7-1 초안 30%.")]
    [Range(0f, 1f)] public float previousTreeKindShare = 0.3f;

    [Header("필드")]
    [Tooltip("임계치 T. 필드의 나무 수가 이 값 이상이면 유예 타이머가 돈다.")]
    public int threshold = 40;
    [Tooltip("스폰 상한 = T + 이 값(기획서 3-1의 +α, 10~15그루). 제약: T ≤ 스폰 상한.")]
    public int spawnExtra = 12;

    [Header("스태미너")]
    public float maxStamina = 100f;
    [Tooltip("T 이상이 된 뒤 첫 피해까지의 시간(초).")]
    public float graceTime = 5f;
    [Tooltip("첫 피해 이후 반복 피해 간격(초).")]
    public float damageCooldown = 3f;
    [Tooltip("패시브로 유예 시간이 늘어도 넘지 않는 상한(초). 압박 규칙이 무력화되지 않게 한다.")]
    public float maxGraceTime = 15f;
    [Tooltip("패시브로 피해 간격이 늘어도 넘지 않는 상한(초).")]
    public float maxDamageCooldown = 10f;
    [Tooltip("루비 등으로 T가 줄어도 이 값 밑으로는 내려가지 않는다.")]
    public int minThreshold = 5;

    [Header("이동")]
    [Tooltip("카드로 오르는 이동속도의 합산 상한(%). 0이면 상한 없음(부록 A-5 +60%).")]
    public float maxMoveSpeedPercent = 60f;

    [Header("언리밋")]
    [Tooltip("언리밋 웨이브마다 마지막 웨이브 HP에 더하는 비율. 0.1이면 Unlimit-1은 ×1.1, Unlimit-2는 ×1.2.")]
    public float unlimitHpGrowthPerWave = 0.1f;

    [Header("카드 가격")]
    [Tooltip("ECardGrade 순서(브론즈, 실버, 골드, 루비, 프리즘)의 레벨 I 가격 = 기준 가격 P × 등급 비율. 브론즈는 100 이내.")]
    public List<int> gradeBasePrices = new List<int>();
    [Tooltip("레벨 배율. [0]=레벨 I, [1]=II ... 가격 = 레벨 I 가격 × 이 값. 모자라면 마지막 값을 쓴다.")]
    public List<float> levelPriceMultipliers = new List<float>();
    [Tooltip("사면 MAX가 되는 구매(에메랄드)는 무료.")]
    public bool bMaxLevelFree = true;

    [Header("카드 제시")]
    [Tooltip("한 번에 제시하는 카드 수 N.")]
    public int offerCount = 3;
    [Tooltip("보유 골드로 단독 구매 가능한 카드를 최소 몇 장 섞을지(슬롯 3).")]
    public int affordableGuaranteeCount = 2;
    [Tooltip("에메랄드·프리즘 보장 노출을 한 번에 몇 장까지 넣을지(슬롯 1). 나머지는 다음 선택으로 이월. 서로 배타인 프리즘은 이 상한의 예외.")]
    public int guaranteedSlotCap = 2;
    [Tooltip("선택마다 액티브를 최소 몇 장 넣을지(슬롯 2).")]
    public int activeGuaranteeCount = 1;
    [Tooltip("보유 액티브가 이 수 이상이면 액티브 보장(슬롯 2)을 해제한다.")]
    public int activeGuaranteeReleaseOwnedCount = 3;
    [Tooltip("첫 선택(웨이브 1)은 무료.")]
    public bool firstPickFree = true;
    [Tooltip("첫 선택은 액티브만 제시(등급 무관).")]
    public bool firstPickActiveOnly = true;
    [Tooltip("첫 선택의 무료 리롤 횟수. 리롤 비용 상승 횟수에 포함하지 않는다.")]
    public int firstPickFreeRerolls = 1;
    [Tooltip("첫 선택에서 건너뛰기를 허용할지. 끄면 반드시 한 장을 골라야 한다.")]
    public bool firstPickSkippable = false;
    [Tooltip("리롤로 다시 뽑을 때 이번 선택에서 이미 노출된 보장 카드가 다시 나올 확률(0~1).")]
    [Range(0f, 1f)] public float rerollGuaranteedReshowChance = 0.5f;

    [Header("노출 가중치")]
    public float baseWeight = 1f;
    [Tooltip("ECardGrade 순서(브론즈, 실버, 골드, 루비, 프리즘)의 등급 비중. 브론즈 > 실버 > 골드 > 루비.")]
    public List<float> gradeWeights = new List<float>();
    [Tooltip("이미 보유한 카드의 다음 레벨은 가중치 × (1 + 이 값).")]
    public float upgradeWeightBonus = 1f;
    [Tooltip("조합 재료 한 장이 이 레벨 이상이면 그 파트너 카드의 유도를 시작한다(기본 II).")]
    public int partnerStartLevel = 2;
    [Tooltip("파트너 유도 중인 카드는 가중치 × (1 + 이 값).")]
    public float partnerWeightBonus = 1f;
    [Tooltip("편향 상한. 어떤 카드의 가중치도 baseWeight × 이 값을 넘지 않는다.")]
    public float maxWeightMultiplier = 4f;
    [Tooltip("조합 재료 한 장이 MAX면 파트너를 남은 칸에 확실히 넣는다(한 선택에 1장).")]
    public bool forcePartnerWhenMaterialMax = true;

    [Header("리롤")]
    public int rerollBaseCost = 20;
    public ERerollCostMode rerollCostMode = ERerollCostMode.Multiply;
    [Tooltip("Linear: 사용할 때마다 이 값만큼 오른다.")]
    public int rerollCostStep = 10;
    [Tooltip("Multiply: 사용할 때마다 이 배율만큼 오른다.")]
    public float rerollCostMultiplier = 1.2f;
    [Tooltip("0이면 상한 없음.")]
    public int rerollCostCap = 0;

    [Header("카드 감정")]
    [Tooltip("감정사가 등장할 수 있는 웨이브 범위(포함). 런 시작 때 이 안에서 한 웨이브를 무작위로 정한다.")]
    public int appraiserWaveMin = 10;
    public int appraiserWaveMax = 20;
    [Tooltip("감정사가 숲을 찾아온다는 예고를 띄우는 웨이브(실제 등장 웨이브는 알려주지 않는다).")]
    public int appraiserNoticeWave = 10;
    [Tooltip("한 판에 감정할 수 있는 카드 수(서로 다른 카드).")]
    public int appraisalMaxCards = 3;
    [Tooltip("레벨 I~III 감정비 = 다음 레벨 정가 × 이 값.")]
    public float appraisalNextLevelFeeMultiplier = 1.15f;
    [Tooltip("레벨 IV 감정비(MAX 도전) = 레벨 I 가격 × 이 값.")]
    public float appraisalMaxChallengeFeeMultiplier = 1.1f;
    [Tooltip("보유 레벨별 결과 분포. [0]=레벨 I ... [3]=레벨 IV. 결과가 MAX를 넘으면 MAX로 맞춘다.")]
    public List<AppraisalOutcome> appraisalOutcomes = new List<AppraisalOutcome>();

    // // 조회

    public int GetGradeBasePrice(ECardGrade _grade)
    {
        int idx = (int)_grade;
        if (null == gradeBasePrices || idx < 0 || idx >= gradeBasePrices.Count) return 0;
        return gradeBasePrices[idx];
    }

    /// <summary>_level(1부터)의 가격 배율. 비어 있으면 1.</summary>
    public float GetLevelPriceMultiplier(int _level)
    {
        if (null == levelPriceMultipliers || 0 == levelPriceMultipliers.Count) return 1f;
        return levelPriceMultipliers[Mathf.Clamp(_level - 1, 0, levelPriceMultipliers.Count - 1)];
    }

    public float GetGradeWeight(ECardGrade _grade)
    {
        int idx = (int)_grade;
        if (null == gradeWeights || idx < 0 || idx >= gradeWeights.Count) return 1f;
        return Mathf.Max(0f, gradeWeights[idx]);
    }

    /// <summary>보유 레벨 _level(1부터)의 감정 결과 분포. 없으면 전부 유지.</summary>
    public AppraisalOutcome GetAppraisalOutcome(int _level)
    {
        if (null == appraisalOutcomes || _level < 1 || _level > appraisalOutcomes.Count) return new AppraisalOutcome { stay = 1f };
        return appraisalOutcomes[_level - 1];
    }

    public WaveRow GetWaveRow(int _waveNumber)
    {
        if (null == waves || 0 == waves.Count) return default;

        int idx = Mathf.Clamp(_waveNumber - 1, 0, waves.Count - 1);
        return waves[idx];
    }

    /// <summary>_waveNumber(1부터)가 속한 티어 인덱스(0부터).</summary>
    public int GetTierIndex(int _waveNumber)
    {
        if (null == tiers || 0 == tiers.Count) return 0;

        int result = 0;
        for (int i = 0; i < tiers.Count; i++)
        {
            if (tiers[i].startWave <= _waveNumber) result = i;
        }

        return result;
    }

    public TierRow GetTier(int _tierIndex)
    {
        if (null == tiers || 0 == tiers.Count) return default;
        return tiers[Mathf.Clamp(_tierIndex, 0, tiers.Count - 1)];
    }

    public int TierCount => null == tiers ? 0 : tiers.Count;

    public int SpawnCap => threshold + Mathf.Max(0, spawnExtra);

    // // 초안 값

    [ContextMenu("기획서 v2.1 부록 A 초안 값으로 채우기")]
    public void FillTemporaryValues()
    {
        clearWave = 30;

        // 부록 A-2: (초당 스폰, 나무 HP, 목표 처치 수, 티어 안 나무 종류 순번). 스폰 주기 = 1 ÷ 초당 스폰.
        float[,] table =
        {
            { 0.50f, 20f, 30f, 0f }, { 0.55f, 20f, 35f, 0f },
            { 0.60f, 30f, 35f, 1f }, { 0.65f, 30f, 40f, 1f }, { 0.70f, 30f, 40f, 1f },
            { 0.75f, 40f, 45f, 2f }, { 0.80f, 40f, 50f, 2f },
            { 0.85f, 45f, 50f, 0f }, { 0.90f, 45f, 55f, 0f },
            { 0.90f, 50f, 55f, 1f }, { 0.95f, 50f, 55f, 1f }, { 0.95f, 50f, 55f, 1f },
            { 0.95f, 55f, 55f, 2f }, { 0.95f, 55f, 55f, 2f }, { 0.95f, 55f, 55f, 2f },
            { 1.50f, 100f, 90f, 0f }, { 1.55f, 100f, 95f, 0f },
            { 1.60f, 115f, 95f, 1f }, { 1.65f, 115f, 100f, 1f }, { 1.70f, 115f, 100f, 1f },
            { 1.80f, 130f, 110f, 2f }, { 1.90f, 130f, 115f, 2f },
            { 2.00f, 280f, 120f, 0f }, { 2.10f, 280f, 125f, 0f },
            { 2.20f, 340f, 130f, 1f }, { 2.30f, 340f, 140f, 1f }, { 2.40f, 340f, 145f, 1f },
            { 2.40f, 420f, 145f, 2f }, { 2.40f, 420f, 145f, 2f }, { 2.45f, 420f, 145f, 2f },
        };

        int rows = table.GetLength(0);
        waves = new List<WaveRow>(rows);
        for (int i = 0; i < rows; i++)
        {
            waves.Add(new WaveRow
            {
                spawnInterval = 1f / table[i, 0],
                treeHp = table[i, 1],
                killTarget = (int)table[i, 2],
                treeKind = (int)table[i, 3],
            });
        }

        tiers = new List<TierRow>(4)
        {
            new TierRow { startWave = 1, staminaDamage = 3f, goldPerTree = 2f },
            new TierRow { startWave = 8, staminaDamage = 6f, goldPerTree = 1.75f },
            new TierRow { startWave = 16, staminaDamage = 10f, goldPerTree = 1f },
            new TierRow { startWave = 23, staminaDamage = 15f, goldPerTree = 1f },
        };
        previousTreeKindShare = 0.3f;

        threshold = 40;
        spawnExtra = 12;

        maxStamina = 100f;
        graceTime = 5f;
        damageCooldown = 3f;
        maxGraceTime = 15f;
        maxDamageCooldown = 10f;
        maxMoveSpeedPercent = 60f;

        unlimitHpGrowthPerWave = 0.1f;

        // 기준 가격 P = 60 × 등급 비율(브론즈 1 / 실버 2.5 / 골드 5 / 루비 4). 프리즘은 무료.
        gradeBasePrices = new List<int>(5) { 60, 150, 300, 240, 0 };
        levelPriceMultipliers = new List<float>(4) { 1f, 1.3f, 1.6f, 2f };
        bMaxLevelFree = true;

        offerCount = 3;
        affordableGuaranteeCount = 2;
        guaranteedSlotCap = 2;
        activeGuaranteeCount = 1;
        activeGuaranteeReleaseOwnedCount = 3;
        firstPickFree = true;
        firstPickActiveOnly = true;
        firstPickFreeRerolls = 1;
        firstPickSkippable = false;
        rerollGuaranteedReshowChance = 0.5f;

        baseWeight = 1f;
        gradeWeights = new List<float>(5) { 1f, 0.7f, 0.5f, 0.15f, 1f };
        upgradeWeightBonus = 1f;
        partnerStartLevel = 2;
        partnerWeightBonus = 1f;
        maxWeightMultiplier = 4f;
        forcePartnerWhenMaterialMax = true;

        rerollBaseCost = 20;
        rerollCostMode = ERerollCostMode.Multiply;
        rerollCostStep = 10;
        rerollCostMultiplier = 1.2f;
        rerollCostCap = 0;

        appraiserWaveMin = 10;
        appraiserWaveMax = 20;
        appraiserNoticeWave = 10;
        appraisalMaxCards = 3;
        appraisalNextLevelFeeMultiplier = 1.15f;
        appraisalMaxChallengeFeeMultiplier = 1.1f;
        appraisalOutcomes = new List<AppraisalOutcome>(4)
        {
            new AppraisalOutcome { stay = 55f, plusOne = 25f, plusTwo = 15f, max = 5f },
            new AppraisalOutcome { stay = 50f, plusOne = 25f, plusTwo = 17f, max = 8f },
            new AppraisalOutcome { stay = 45f, plusOne = 28f, plusTwo = 0f, max = 27f },
            new AppraisalOutcome { stay = 65f, plusOne = 0f, plusTwo = 0f, max = 35f },
        };
    }
}
