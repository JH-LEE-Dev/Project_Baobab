using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 기획서 v2.6 부록 A "밸런스 수치"를 한곳에 모은 데이터. 코드에는 수치를 두지 않는다.
///
/// 처음 만든 에셋은 비어 있다. 인스펙터 우측 메뉴의 "기획서 v2.6 부록 A 초안 값으로 채우기"를 누르면
/// 부록 A의 초안 값이 들어간다. 값은 모두 초안이며 프로토타입 실측 후 덮어쓴다.
///
/// [스폰] v2.6부터 나무는 티어별 정수 묶음으로 나온다. 웨이브마다 (묶음 크기, 묶음 주기, HP, 목표 처치 수)를 둔다(WaveRow.batchSize / batchSeconds).
/// 묶음 크기가 0인 줄은 옛 방식(spawnInterval과 spawnBatchSeconds로 계산, 소수 이월)을 그대로 쓴다.
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
        [Tooltip("나무 1그루 스폰 주기(초) = 묶음 주기 ÷ 묶음 크기 = 1 ÷ 초당 스폰. 묶음 크기가 있으면 참고용이고, 없으면(0) 이 값과 spawnBatchSeconds로 묶음을 계산한다.")]
        public float spawnInterval;
        [Tooltip("묶음 크기(그루). 묶음 주기마다 이 수만큼 정수로 한꺼번에 심는다(소수 이월 없음, 기획서 v2.6 3-1). 0이면 옛 방식(spawnInterval × spawnBatchSeconds, 소수 이월).")]
        public int batchSize;
        [Tooltip("묶음 주기(초). 티어마다 다르다(1티어 3 / 2티어 2.5 / 3티어 2 / 4티어 1.5). batchSize가 있을 때만 쓴다.")]
        public float batchSeconds;
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
    [Tooltip("스폰 상한 = T + 이 값(기획서 v2.5 3-1의 +α, 20그루). 제약: T ≤ 스폰 상한.")]
    public int spawnExtra = 20;

    [Header("초기 나무 · 묶음 스폰 · 생성 위치 (기획서 v2.5 3-1, 부록 A-1 초안 - 실측 후 조정)")]
    [Tooltip("웨이브 시작 초기 나무 = 현재 임계치 T × 이 비율(내림). T 40이면 20그루, 웨이브 번호와 무관하다. 목표 처치 수에 포함되고 T보다 항상 낮다.")]
    [Range(0f, 1f)] public float initialTreeThresholdRatio = 0.5f;
    [Tooltip("묶음 주기 P(초). 나무는 한 그루씩 나오지 않고 P마다 초당 스폰 × P그루가 한꺼번에 나온다(소수는 다음 묶음으로 이월).")]
    public float spawnBatchSeconds = 3f;
    [Tooltip("나무끼리의 최소 간격(월드, 등각 보정 거리, TBD). 간격을 지킬 빈칸이 없으면 간격만 포기하고 심는다.")]
    public float treeMinSpacing = 1f;
    [Tooltip("플레이어 바로 옆에는 심지 않는 반경(월드, 등각 보정 거리, TBD).")]
    public float playerExclusionRadius = 1.2f;
    [Tooltip("섬 가장자리에서 이 칸 수만큼은 나무를 심지 않는다(맵 끝 나무가 HUD 밖으로 나가 보이는 문제). 물 · 용암 칸과 8방향으로 이 거리 안이면 뺀다. 0이면 끈다.")]
    public int fieldEdgeInsetCells = 1;
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
    [Tooltip("카드로 오르는 이동속도의 합산 상한(%). 0이면 상한 없음(부록 A-5 합산 상한 +100%).")]
    public float maxMoveSpeedPercent = 100f;

    [Header("언리밋 (기획서 v2.6 7-2, 부록 A \"언리밋 곡선\")")]
    [Tooltip("언리밋 웨이브마다 나무 HP에 곱하는 증가율(누적). Unlimit-1은 마지막 티어의 첫 웨이브(4티어 최소 수치) HP에서 시작하고, Unlimit-N의 HP = 시작 HP × (1 + 이 값)^(N−1). 0.1이면 웨이브마다 ×1.10.")]
    public float unlimitHpGrowthPerWave = 0.1f;
    [Tooltip("언리밋 웨이브마다 묶음 크기에 더하는 그루 수. Unlimit-N의 묶음 = 시작 묶음 + 이 값 × (N−1). 스폰 상한을 넘는 몫은 소멸한다.")]
    public int unlimitBatchSizeAddPerWave = 1;
    [Tooltip("언리밋 웨이브마다 묶음 주기에 더하는 초(음수면 빨라진다). Unlimit-N의 주기 = 시작 주기 + 이 값 × (N−1), 하한은 아래 값.")]
    public float unlimitBatchSecondsAddPerWave = -0.02f;
    [Tooltip("언리밋 묶음 주기의 하한(초).")]
    public float unlimitMinBatchSeconds = 1f;

    [Header("카드 제시")]
    [Tooltip("한 번에 제시하는 카드 수 N(기획서 v2.5 4장).")]
    public int offerCount = 4;
    [Tooltip("프리즘·에메랄드(MAX) 보장 노출을 합쳐 한 번에 몇 장까지 넣을지. 나머지는 다음 선택으로 이월. 서로 배타인 프리즘은 이 상한의 예외.")]
    public int guaranteedSlotCap = 2;
    [Tooltip("프리즘·MAX 다음으로 무조건 넣는 공통 패시브 수(기획서 v2.5 4-2 ③). 첫 선택에는 적용하지 않는다. 노출 가능한 패시브가 모자라면 있는 만큼.")]
    public int guaranteedPassiveCount = 2;
    [Tooltip("첫 선택(웨이브 1)은 액티브만 제시.")]
    public bool firstPickActiveOnly = true;
    [Tooltip("선택 단계마다 쓸 수 있는 리롤 횟수(첫 선택 포함). 쓰지 않아도 다음 선택으로 이월하지 않는다(기획서 4-1). 리롤은 우선순위 없는 진짜 랜덤이다(4-2).")]
    public int rerollsPerSelection = 1;

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

    [ContextMenu("기획서 v2.6 부록 A 초안 값으로 채우기")]
    public void FillTemporaryValues()
    {
        clearWave = 30;

        // 부록 A-2(v2.6): (묶음 크기, 묶음 주기, 나무 HP, 목표 처치 수, 티어 안 나무 종류 순번). 스폰 주기 = 묶음 주기 ÷ 묶음 크기(참고용).
        // 1티어 3~7그루 / 3초, 2티어 7~14그루 / 2.5초, 3티어 14~21그루 / 2초, 4티어 21~30그루 / 1.5초. HP는 3티어 100 / 105 / 115, 4티어 230 / 300 / 350.
        float[,] table =
        {
            { 3f, 3f, 20f, 20f, 0f },
            { 4f, 3f, 20f, 30f, 0f },
            { 4f, 3f, 30f, 30f, 1f },
            { 5f, 3f, 30f, 45f, 1f },
            { 6f, 3f, 30f, 55f, 1f },
            { 6f, 3f, 40f, 60f, 2f },
            { 7f, 3f, 40f, 75f, 2f },
            { 7f, 2.5f, 45f, 95f, 0f },
            { 8f, 2.5f, 50f, 115f, 0f },
            { 9f, 2.5f, 50f, 140f, 1f },
            { 10f, 2.5f, 55f, 165f, 1f },
            { 11f, 2.5f, 55f, 190f, 1f },
            { 12f, 2.5f, 55f, 215f, 2f },
            { 13f, 2.5f, 55f, 245f, 2f },
            { 14f, 2.5f, 55f, 275f, 2f },
            { 14f, 2f, 100f, 355f, 0f },
            { 15f, 2f, 100f, 400f, 0f },
            { 16f, 2f, 105f, 440f, 1f },
            { 18f, 2f, 105f, 515f, 1f },
            { 19f, 2f, 105f, 565f, 1f },
            { 20f, 2f, 115f, 615f, 2f },
            { 21f, 2f, 115f, 665f, 2f },
            { 21f, 1.5f, 230f, 915f, 0f },
            { 22f, 1.5f, 230f, 990f, 0f },
            { 24f, 1.5f, 300f, 1115f, 1f },
            { 25f, 1.5f, 300f, 1195f, 1f },
            { 26f, 1.5f, 300f, 1280f, 1f },
            { 28f, 1.5f, 350f, 1415f, 2f },
            { 29f, 1.5f, 350f, 1505f, 2f },
            { 30f, 1.5f, 350f, 1600f, 2f },
        };

        int rows = table.GetLength(0);
        waves = new List<WaveRow>(rows);
        for (int i = 0; i < rows; i++)
        {
            waves.Add(new WaveRow
            {
                batchSize = (int)table[i, 0],
                batchSeconds = table[i, 1],
                spawnInterval = table[i, 1] / table[i, 0],
                treeHp = table[i, 2],
                killTarget = (int)table[i, 3],
                treeKind = (int)table[i, 4],
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
        spawnExtra = 20;

        initialTreeThresholdRatio = 0.5f;
        spawnBatchSeconds = 3f;
        treeMinSpacing = 1f;
        playerExclusionRadius = 1.2f;
        fieldEdgeInsetCells = 1;
        idleMoveReach = 1.5f;

        maxStamina = 100f;
        graceTime = 5f;
        damageCooldown = 3f;
        maxGraceTime = 15f;
        maxDamageCooldown = 10f;
        maxMoveSpeedPercent = 100f;

        unlimitHpGrowthPerWave = 0.1f;
        unlimitBatchSizeAddPerWave = 1;
        unlimitBatchSecondsAddPerWave = -0.02f;
        unlimitMinBatchSeconds = 1f;

        offerCount = 4;
        guaranteedSlotCap = 2;
        guaranteedPassiveCount = 2;
        firstPickActiveOnly = true;
        rerollsPerSelection = 1;

        // 종류 비중: 액티브 1.0 / 공통 패시브 1.0 / 계약 0.3(v2.5에서 0.15 → 0.3) / 프리즘(보장 노출이라 가중치 추첨에 거의 오지 않는다) 1.0.
        baseWeight = 1f;
        kindWeights = new List<float>(4) { 1f, 1f, 0.3f, 1f };
        upgradeWeightBonus = 1f;
        partnerStartLevel = 2;
        partnerWeightBonus = 1f;
        maxWeightMultiplier = 4f;
        forcePartnerWhenMaterialMax = true;
    }
}
