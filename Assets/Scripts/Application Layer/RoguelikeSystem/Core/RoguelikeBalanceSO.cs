using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기획서 v2.4 부록 A "밸런스 수치"를 한곳에 모은 데이터. 코드에는 수치를 두지 않는다.
///
/// 처음 만든 에셋은 비어 있다. 인스펙터 우측 메뉴의 "기획서 v2.4 부록 A 초안 값으로 채우기"를 누르면
/// 부록 A의 초안 값이 들어간다. 값은 모두 초안이며 프로토타입 실측 후 덮어쓴다.
///
/// v2.4에서 골드 시스템이 없어져 골드·가격·리롤 비용·감정 항목이 사라졌다. 카드 선택은 모두 무료이고,
/// 한 판의 빌드 규모는 선택 횟수(웨이브마다 1회)가 정한다.
///
/// 연출 시간(진입 연출·카드 연출)은 이 에셋이 아니라 연출 쪽 설정에서 관리한다.
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

    [Header("초기 나무 · 생성 위치 (기획서 v2.3 3-1, 값은 부록 A TBD - 실측 후 조정)")]
    [Tooltip("웨이브 시작 초기 나무 = 초당 스폰 × 이 시간(초). 목표 처치 수에 포함되고 임계치 T보다 항상 낮다.")]
    public float initialTreeSeconds = 10f;
    [Tooltip("군집 반경(월드, 등각 보정 거리). 새 나무는 군집 중심에서 이 반경 안에 심는다. 다 차면 넓혀 가며 찾는다.")]
    public float clusterRadius = 2.5f;
    [Tooltip("군집 중심을 필드 안 다른 곳으로 옮기는 주기(초). 0이면 웨이브마다만 옮긴다.")]
    public float clusterMoveInterval = 15f;
    [Tooltip("허탕 방지 거리 R. 플레이어에서 가장 가까운 나무가 이보다 멀면 그 나무 쪽 군집에 우선 심는다.")]
    public float gapDistance = 4f;
    [Tooltip("플레이어 바로 옆에는 심지 않는 반경(월드, 등각 보정 거리).")]
    public float playerExclusionRadius = 1.2f;
    [Tooltip("초기 나무 군집의 중심을 고르는 범위. 플레이어 시작 위치에서 이 거리 안(이동 거리가 짧은 곳).")]
    public float initialClusterMaxDistance = 4f;
    [Tooltip("측정용: 이동 중 가장 가까운 나무가 이 거리보다 멀면 '나무 없이 이동한 시간'으로 센다(기획서 C-2 실측 항목).")]
    public float idleMoveReach = 1.5f;

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
    [Tooltip("계약 등으로 T가 줄어도 이 값 밑으로는 내려가지 않는다.")]
    public int minThreshold = 5;

    [Header("이동")]
    [Tooltip("카드로 오르는 이동속도의 합산 상한(%). 0이면 상한 없음(부록 A-5 +60%).")]
    public float maxMoveSpeedPercent = 60f;

    [Header("언리밋")]
    [Tooltip("언리밋 웨이브마다 마지막 웨이브 HP에 더하는 비율. 0.1이면 Unlimit-1은 ×1.1, Unlimit-2는 ×1.2.")]
    public float unlimitHpGrowthPerWave = 0.1f;

    [Header("카드 제시")]
    [Tooltip("한 번에 제시하는 카드 수 N.")]
    public int offerCount = 3;
    [Tooltip("에메랄드·프리즘 보장 노출을 한 번에 몇 장까지 넣을지(슬롯 1). 나머지는 다음 선택으로 이월. 서로 배타인 프리즘은 이 상한의 예외.")]
    public int guaranteedSlotCap = 2;
    [Tooltip("첫 선택(웨이브 1)은 액티브만 제시.")]
    public bool firstPickActiveOnly = true;
    [Tooltip("선택 단계마다 쓸 수 있는 리롤 횟수(첫 선택 포함). 쓰지 않아도 다음 선택으로 이월하지 않는다(기획서 4-1).")]
    public int rerollsPerSelection = 1;
    [Tooltip("리롤로 다시 뽑을 때 이번 선택에서 이미 노출된 보장 카드가 다시 나올 확률(0~1).")]
    [Range(0f, 1f)] public float rerollGuaranteedReshowChance = 0.5f;

    [Header("노출 가중치")]
    public float baseWeight = 1f;
    [Tooltip("ECardKind 순서(액티브, 공통 패시브, 계약, 프리즘)의 종류 비중. 액티브 = 공통 패시브 > 계약(기획서 4-2 가중 3).")]
    public List<float> kindWeights = new List<float>();
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

    // // 조회

    /// <summary>카드 종류의 노출 비중. 비어 있으면 1.</summary>
    public float GetKindWeight(ECardKind _kind)
    {
        int idx = (int)_kind;
        if (null == kindWeights || idx < 0 || idx >= kindWeights.Count) return 1f;
        return Mathf.Max(0f, kindWeights[idx]);
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

    [ContextMenu("기획서 v2.4 부록 A 초안 값으로 채우기")]
    public void FillTemporaryValues()
    {
        clearWave = 30;

        // 부록 A-2: (초당 스폰, 나무 HP, 목표 처치 수, 티어 안 나무 종류 순번). 스폰 주기 = 1 ÷ 초당 스폰.
        // v2.4: 3티어 160 / 185 / 210, 4티어 380 / 450 / 520(골드 제거로 첫 액티브 MAX가 7웨이브로 당겨져 상향).
        float[,] table =
        {
            { 0.50f, 20f, 30f, 0f }, { 0.55f, 20f, 35f, 0f },
            { 0.60f, 30f, 35f, 1f }, { 0.65f, 30f, 40f, 1f }, { 0.70f, 30f, 40f, 1f },
            { 0.75f, 40f, 45f, 2f }, { 0.80f, 40f, 50f, 2f },
            { 0.85f, 45f, 50f, 0f }, { 0.90f, 45f, 55f, 0f },
            { 0.90f, 50f, 55f, 1f }, { 0.95f, 50f, 55f, 1f }, { 0.95f, 50f, 55f, 1f },
            { 0.95f, 55f, 55f, 2f }, { 0.95f, 55f, 55f, 2f }, { 0.95f, 55f, 55f, 2f },
            { 1.50f, 160f, 90f, 0f }, { 1.55f, 160f, 95f, 0f },
            { 1.60f, 185f, 95f, 1f }, { 1.65f, 185f, 100f, 1f }, { 1.70f, 185f, 100f, 1f },
            { 1.80f, 210f, 110f, 2f }, { 1.90f, 210f, 115f, 2f },
            { 2.00f, 380f, 120f, 0f }, { 2.10f, 380f, 125f, 0f },
            { 2.20f, 450f, 130f, 1f }, { 2.30f, 450f, 140f, 1f }, { 2.40f, 450f, 145f, 1f },
            { 2.40f, 520f, 145f, 2f }, { 2.40f, 520f, 145f, 2f }, { 2.45f, 520f, 145f, 2f },
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
            new TierRow { startWave = 1, staminaDamage = 3f },
            new TierRow { startWave = 8, staminaDamage = 6f },
            new TierRow { startWave = 16, staminaDamage = 10f },
            new TierRow { startWave = 23, staminaDamage = 15f },
        };
        previousTreeKindShare = 0.3f;

        threshold = 40;
        spawnExtra = 12;

        initialTreeSeconds = 10f;
        clusterRadius = 2.5f;
        clusterMoveInterval = 15f;
        gapDistance = 4f;
        playerExclusionRadius = 1.2f;
        initialClusterMaxDistance = 4f;
        idleMoveReach = 1.5f;

        maxStamina = 100f;
        graceTime = 5f;
        damageCooldown = 3f;
        maxGraceTime = 15f;
        maxDamageCooldown = 10f;
        maxMoveSpeedPercent = 60f;

        unlimitHpGrowthPerWave = 0.1f;

        offerCount = 3;
        guaranteedSlotCap = 2;
        firstPickActiveOnly = true;
        rerollsPerSelection = 1;
        rerollGuaranteedReshowChance = 0.5f;

        // 종류 비중: 액티브 1.0 / 공통 패시브 1.0 / 계약 0.15 / 프리즘(보장 노출이라 가중치 추첨에 거의 오지 않는다) 1.0.
        baseWeight = 1f;
        kindWeights = new List<float>(4) { 1f, 1f, 0.15f, 1f };
        upgradeWeightBonus = 1f;
        partnerStartLevel = 2;
        partnerWeightBonus = 1f;
        maxWeightMultiplier = 4f;
        forcePartnerWhenMaterialMax = true;
    }
}
