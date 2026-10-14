using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기획서 6장 "튜닝 파라미터 슬롯"의 수치를 한곳에 모은 데이터. 코드에는 수치를 두지 않는다.
///
/// 처음 만든 에셋은 비어 있다. 인스펙터 우측 메뉴의 "임시 값(기획서 10장)으로 채우기"를 누르면
/// 코어 루프를 돌려 볼 수 있는 임시 값이 들어간다. 10장은 삭제 예정이므로 실제 값이 나오면 덮어쓴다.
///
/// 연출 시간(진입 연출·카드 연출·코인 연출)은 이 에셋이 아니라 연출 쪽 설정에서 관리한다.
/// </summary>
[CreateAssetMenu(fileName = "RoguelikeBalance", menuName = "Baobab/Roguelike/Balance")]
public class RoguelikeBalanceSO : ScriptableObject
{
    [Serializable]
    public struct TierRow
    {
        [Tooltip("이 티어가 시작되는 웨이브(1부터). 첫 티어는 반드시 1.")]
        public int startWave;
        [Tooltip("나무 HP 배율(기본 나무 HP 기준).")]
        public float treeHpMultiplier;
        [Tooltip("임계치 피해 1회의 스태미너 피해량.")]
        public float staminaDamage;
        [Tooltip("나무 1그루당 골드.")]
        public int goldPerTree;
    }

    [Serializable]
    public struct WaveRow
    {
        [Tooltip("목표 처치 수.")]
        public int killTarget;
        [Tooltip("나무 1그루 스폰 주기(초).")]
        public float spawnInterval;
    }

    public enum ERerollCostMode
    {
        Linear = 0,
        Multiply = 1,
    }

    [Header("웨이브")]
    [Tooltip("이 웨이브를 클리어하면 런 클리어(이후 언리밋).")]
    public int clearWave = 30;
    [Tooltip("웨이브별 목표 처치 수와 스폰 주기. [0]=웨이브 1. 모자라면 마지막 줄을 그대로 쓴다.")]
    public List<WaveRow> waves = new List<WaveRow>();
    [Tooltip("티어 구간. startWave 오름차순.")]
    public List<TierRow> tiers = new List<TierRow>();

    [Header("필드")]
    [Tooltip("임계치 T. 필드의 나무 수가 이 값 이상이면 유예 타이머가 돈다.")]
    public int threshold = 40;
    [Tooltip("스폰 상한 = T + 이 값. 제약: T ≤ 스폰 상한.")]
    public int spawnExtra = 20;

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

    [Header("언리밋")]
    [Tooltip("언리밋 웨이브마다 마지막 티어 HP에 더하는 비율. 0.1이면 Unlimit-1은 ×1.1, Unlimit-2는 ×1.2.")]
    public float unlimitHpGrowthPerWave = 0.1f;

    [Header("카드 가격")]
    [Tooltip("ECardGrade 순서(브론즈, 실버, 골드, 루비, 프리즘)의 첫 구매가. 브론즈는 100 이내.")]
    public List<int> gradeBasePrices = new List<int>();
    [Tooltip("업그레이드 가격 = 첫 구매가 × (1 + 이 값 × (구매 후 레벨 - 1)).")]
    public float upgradePriceStep = 0.5f;

    [Header("카드 제시")]
    [Tooltip("한 번에 제시하는 카드 수 N.")]
    public int offerCount = 3;
    [Tooltip("보유 골드로 단독 구매 가능한 카드를 최소 몇 장 섞을지.")]
    public int affordableGuaranteeCount = 2;
    [Tooltip("에메랄드·프리즘 보장 노출을 한 번에 몇 장까지 넣을지. 나머지는 다음 선택으로 이월.")]
    public int guaranteedSlotCap = 2;
    [Tooltip("첫 선택(웨이브 1)은 무료.")]
    public bool firstPickFree = true;
    [Tooltip("첫 선택은 브론즈만 제시.")]
    public bool firstPickBronzeOnly = true;
    [Tooltip("첫 선택은 액티브만 제시(기획서 3-3 미정 항목 '첫 무료 카드의 종류'). 끄면 브론즈 전체.")]
    public bool firstPickActiveOnly = false;
    [Tooltip("첫 선택에서 건너뛰기를 허용할지(기획서 미정). 끄면 반드시 한 장을 골라야 한다.")]
    public bool firstPickSkippable = false;

    [Header("노출 가중치")]
    public float baseWeight = 1f;
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
    public ERerollCostMode rerollCostMode = ERerollCostMode.Linear;
    [Tooltip("Linear: 사용할 때마다 이 값만큼 오른다.")]
    public int rerollCostStep = 10;
    [Tooltip("Multiply: 사용할 때마다 이 배율만큼 오른다.")]
    public float rerollCostMultiplier = 1.5f;
    [Tooltip("0이면 상한 없음.")]
    public int rerollCostCap = 0;

    // // 조회

    public int GetGradeBasePrice(ECardGrade _grade)
    {
        int idx = (int)_grade;
        if (null == gradeBasePrices || idx < 0 || idx >= gradeBasePrices.Count) return 0;
        return gradeBasePrices[idx];
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

    // // 임시 값

    [ContextMenu("임시 값(기획서 10장)으로 채우기")]
    public void FillTemporaryValues()
    {
        clearWave = 30;

        waves = new List<WaveRow>(30);
        for (int wave = 1; wave <= 30; wave++)
        {
            // 웨이브 1~22: 0.5초 × 0.95^(웨이브-1), 웨이브 23(마지막 티어)부터 0.12초 고정.
            float interval = wave <= 22 ? 0.5f * Mathf.Pow(0.95f, wave - 1) : 0.12f;
            waves.Add(new WaveRow { killTarget = 50 + wave * 10, spawnInterval = interval });
        }

        tiers = new List<TierRow>(4)
        {
            new TierRow { startWave = 1, treeHpMultiplier = 1f, staminaDamage = 3f, goldPerTree = 1 },
            new TierRow { startWave = 8, treeHpMultiplier = 2.5f, staminaDamage = 6f, goldPerTree = 2 },
            new TierRow { startWave = 16, treeHpMultiplier = 6f, staminaDamage = 10f, goldPerTree = 3 },
            new TierRow { startWave = 23, treeHpMultiplier = 12f, staminaDamage = 15f, goldPerTree = 4 },
        };

        threshold = 40;
        spawnExtra = 20;

        maxStamina = 100f;
        graceTime = 5f;
        damageCooldown = 3f;

        unlimitHpGrowthPerWave = 0.1f;

        gradeBasePrices = new List<int>(5) { 40, 120, 250, 180, 300 };
        upgradePriceStep = 0.5f;

        offerCount = 3;
        affordableGuaranteeCount = 2;
        guaranteedSlotCap = 2;

        rerollBaseCost = 20;
        rerollCostMode = ERerollCostMode.Linear;
        rerollCostStep = 10;
        rerollCostCap = 0;
    }
}
