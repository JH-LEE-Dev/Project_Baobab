using UnityEngine;

/// <summary>웨이브 하나의 확정 수치. 런 컨트롤러가 웨이브 시작 전에 한 번 만들어 쓴다.</summary>
public struct WaveSpec
{
    /// <summary>1부터 세는 통산 웨이브 번호. 언리밋은 clearWave 다음부터 이어진다(31, 32...).</summary>
    public int waveNumber;
    public bool bUnlimit;
    /// <summary>언리밋 표기 번호(Unlimit-1이면 1). 일반 웨이브는 0.</summary>
    public int unlimitIndex;
    public int tierIndex;
    /// <summary>직전 웨이브와 티어가 달라 지형 전환 연출이 필요한지. 웨이브 1은 false(런 시작 때 이미 깔린다).</summary>
    public bool bTierChanged;
    public int killTarget;
    /// <summary>
    /// 웨이브 시작 때 이미 자라 있는 초기 나무 수(기본 임계치의 절반, 목표 처치 수에 포함, 기획서 v2.5 3-1). 이후 스폰은 목표 − 이 수만큼.
    /// 계약으로 임계치가 줄면 런 컨트롤러가 그 임계치로 다시 계산한다.
    /// </summary>
    public int initialTrees;
    public float spawnInterval;
    /// <summary>묶음 크기(그루, 기획서 v2.6 3-1). 0이면 옛 방식(spawnInterval과 묶음 주기로 계산, 소수 이월).</summary>
    public int batchSize;
    /// <summary>묶음 주기(초). batchSize가 있을 때만 쓴다.</summary>
    public float batchSeconds;
    /// <summary>나무의 절대 HP(부록 A-2). 언리밋은 4티어 최소 수치에서 시작해 웨이브마다 증가율을 누적한다(부록 A "언리밋 곡선").</summary>
    public float treeHp;
    /// <summary>티어 안 나무 종류 순번(0 = 초기, 1 = 중간, 2 = 후반).</summary>
    public int treeKind;
    /// <summary>티어 안에서 종류가 바뀌는 웨이브면 직전 종류 순번, 아니면 -1. 이 종류가 previousTreeKindShare만큼 섞인다.</summary>
    public int previousTreeKind;
    public float previousTreeKindShare;
    public float staminaDamage;
}

/// <summary>밸런스 데이터에서 웨이브별 수치를 계산한다. 기획서 3-1, 7-1, 7-2, 부록 A-2.</summary>
public static class WaveSchedule
{
    public static WaveSpec Get(RoguelikeBalanceSO _balance, int _waveNumber)
    {
        WaveSpec spec = new WaveSpec { waveNumber = Mathf.Max(1, _waveNumber), previousTreeKind = -1 };
        if (null == _balance) return spec;

        int clearWave = Mathf.Max(1, _balance.clearWave);
        spec.bUnlimit = spec.waveNumber > clearWave;
        spec.unlimitIndex = spec.bUnlimit ? spec.waveNumber - clearWave : 0;

        // 언리밋은 마지막 웨이브의 처치 수·나무 종류와 마지막 티어의 피해량을 쓰고, 나무 HP · 묶음 크기 · 묶음 주기는 마지막 티어의 첫 웨이브(4티어 최소 수치)에서 시작해 웨이브마다 강해진다.
        int rowWave = spec.bUnlimit ? clearWave : spec.waveNumber;
        RoguelikeBalanceSO.WaveRow row = _balance.GetWaveRow(rowWave);

        spec.tierIndex = _balance.GetTierIndex(rowWave);
        RoguelikeBalanceSO.TierRow tier = _balance.GetTier(spec.tierIndex);

        spec.killTarget = Mathf.Max(1, row.killTarget);
        spec.spawnInterval = Mathf.Max(0.01f, row.spawnInterval);
        spec.batchSize = Mathf.Max(0, row.batchSize);
        spec.batchSeconds = Mathf.Max(0f, row.batchSeconds);
        spec.initialTrees = TreeSpawnRules.InitialTreeCount(_balance.threshold, _balance.initialTreeThresholdRatio, spec.killTarget);
        spec.treeHp = Mathf.Max(1f, row.treeHp);
        spec.treeKind = Mathf.Clamp(row.treeKind, 0, RoguelikeBalanceSO.TreeKindsPerTier - 1);
        spec.staminaDamage = tier.staminaDamage;

        if (spec.bUnlimit)
        {
            // Unlimit-N: HP = 시작 HP × (1 + 증가율)^(N−1), 묶음 = 시작 + 증가분 × (N−1), 주기 = 시작 + 증가분 × (N−1)(하한). 시작값은 마지막 티어의 첫 웨이브 줄이다.
            RoguelikeBalanceSO.WaveRow start = _balance.GetWaveRow(Mathf.Max(1, tier.startWave));
            int steps = spec.unlimitIndex - 1;

            spec.treeHp = Mathf.Max(1f, start.treeHp) * Mathf.Pow(1f + Mathf.Max(0f, _balance.unlimitHpGrowthPerWave), steps);

            if (0 < start.batchSize)
            {
                spec.batchSize = Mathf.Max(1, start.batchSize + _balance.unlimitBatchSizeAddPerWave * steps);
                spec.batchSeconds = Mathf.Max(Mathf.Max(0.1f, _balance.unlimitMinBatchSeconds), start.batchSeconds + _balance.unlimitBatchSecondsAddPerWave * steps);
                spec.spawnInterval = spec.batchSeconds / spec.batchSize;
            }
        }
        else if (spec.waveNumber > 1 && _balance.GetTierIndex(spec.waveNumber - 1) == spec.tierIndex)
        {
            // 티어 안에서 나무 종류가 바뀌는 웨이브는 이전 종류가 일부 남는다(기획서 7-1). 티어가 바뀌는 웨이브는
            // 지형째 바뀌므로 다른 숲의 나무를 섞지 않는다.
            int prevKind = Mathf.Clamp(_balance.GetWaveRow(spec.waveNumber - 1).treeKind, 0, RoguelikeBalanceSO.TreeKindsPerTier - 1);
            if (prevKind != spec.treeKind)
            {
                spec.previousTreeKind = prevKind;
                spec.previousTreeKindShare = Mathf.Clamp01(_balance.previousTreeKindShare);
            }
        }

        if (spec.waveNumber > 1 && false == spec.bUnlimit)
        {
            spec.bTierChanged = _balance.GetTierIndex(spec.waveNumber - 1) != spec.tierIndex;
        }

        return spec;
    }
}
