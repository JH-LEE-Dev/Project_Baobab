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
    /// <summary>직전 웨이브와 티어가 달라 지형 전환 연출이 필요한지. 웨이브 1은 false(런 시작 때 이미 깔린다). 언리밋 1은 30웨이브(4티어) → 1티어라 true.</summary>
    public bool bTierChanged;
    public int killTarget;
    /// <summary>
    /// 웨이브 시작 때 이미 자라 있는 초기 나무 수(기본 임계치의 절반, 목표 처치 수에 포함, 기획서 v2.7 3-1). 이후 스폰은 목표와 무관하게 웨이브 끝까지 계속된다.
    /// 계약으로 임계치가 줄면 런 컨트롤러가 그 임계치로 다시 계산한다.
    /// </summary>
    public int initialTrees;
    public float spawnInterval;
    /// <summary>묶음 크기(그루, 기획서 v2.6 3-1). 0이면 옛 방식(spawnInterval과 묶음 주기로 계산, 소수 이월).</summary>
    public int batchSize;
    /// <summary>묶음 주기(초). batchSize가 있을 때만 쓴다.</summary>
    public float batchSeconds;
    /// <summary>나무의 절대 HP(부록 A-2). 언리밋 N은 N웨이브 줄 HP에 웨이브마다 증가율을 누적한다(부록 A "언리밋 곡선").</summary>
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

        // 언리밋 N은 N웨이브 줄(30을 넘으면 30웨이브 줄)의 티어 · 나무 종류 · 묶음 · 주기 · 목표 처치 수 · 피해량을 쓰고, 나무 HP만 웨이브마다 강해진다(기획서 v3.0 7-2 11-12).
        // 그래서 언리밋도 본 런처럼 1티어부터 다시 돌고, 티어가 바뀌는 라운드에서 지형 전환이 나온다.
        int rowWave = spec.bUnlimit ? Mathf.Min(spec.unlimitIndex, clearWave) : spec.waveNumber;
        RoguelikeBalanceSO.WaveRow row = _balance.GetWaveRow(rowWave);

        spec.tierIndex = _balance.GetTierIndex(rowWave);
        RoguelikeBalanceSO.TierRow tier = _balance.GetTier(spec.tierIndex);

        spec.killTarget = Mathf.Max(1, row.killTarget);
        spec.spawnInterval = Mathf.Max(0.01f, row.spawnInterval);
        spec.batchSize = Mathf.Max(0, row.batchSize);
        spec.batchSeconds = Mathf.Max(0f, row.batchSeconds);
        spec.initialTrees = TreeSpawnRules.InitialTreeCount(_balance.threshold, _balance.initialTreeThresholdRatio);
        spec.treeHp = Mathf.Max(1f, row.treeHp);
        spec.treeKind = Mathf.Clamp(row.treeKind, 0, RoguelikeBalanceSO.TreeKindsPerTier - 1);
        spec.staminaDamage = tier.staminaDamage;

        if (spec.bUnlimit)
        {
            // Unlimit-N: HP = N웨이브 줄 HP × (1 + 증가율)^(N−1). 목표 처치 수는 N웨이브 줄 값과 unlimitMaxKillTarget 중 작은 값.
            spec.treeHp = Mathf.Max(1f, row.treeHp) * Mathf.Pow(1f + Mathf.Max(0f, _balance.unlimitHpGrowthPerWave), spec.unlimitIndex - 1);
            spec.killTarget = Mathf.Clamp(row.killTarget, 1, Mathf.Max(1, _balance.unlimitMaxKillTarget));
        }

        // 직전 웨이브의 줄. 언리밋 1은 30웨이브 다음이고, 언리밋 2 이상은 직전 언리밋의 줄이다(30을 넘으면 30에 머문다).
        int prevRowWave = 0;
        if (spec.bUnlimit) prevRowWave = 1 == spec.unlimitIndex ? clearWave : Mathf.Min(spec.unlimitIndex - 1, clearWave);
        else if (spec.waveNumber > 1) prevRowWave = spec.waveNumber - 1;

        if (prevRowWave > 0)
        {
            int prevTier = _balance.GetTierIndex(prevRowWave);
            spec.bTierChanged = prevTier != spec.tierIndex;

            // 티어 안에서 나무 종류가 바뀌는 웨이브는 이전 종류가 일부 남는다(기획서 7-1). 티어가 바뀌는 웨이브는
            // 지형째 바뀌므로 다른 숲의 나무를 섞지 않는다.
            if (false == spec.bTierChanged)
            {
                int prevKind = Mathf.Clamp(_balance.GetWaveRow(prevRowWave).treeKind, 0, RoguelikeBalanceSO.TreeKindsPerTier - 1);
                if (prevKind != spec.treeKind)
                {
                    spec.previousTreeKind = prevKind;
                    spec.previousTreeKindShare = Mathf.Clamp01(_balance.previousTreeKindShare);
                }
            }
        }

        return spec;
    }
}
