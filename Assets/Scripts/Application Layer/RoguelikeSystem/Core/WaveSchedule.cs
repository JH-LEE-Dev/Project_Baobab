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
    /// <summary>웨이브 시작 때 이미 자라 있는 초기 나무 수(목표 처치 수에 포함, 기획서 v2.3 3-1). 이후 스폰은 목표 − 이 수만큼.</summary>
    public int initialTrees;
    public float spawnInterval;
    /// <summary>나무의 절대 HP(부록 A-2). 언리밋은 마지막 웨이브 HP에 웨이브마다 증가율을 누적한다.</summary>
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

        // 언리밋은 마지막 웨이브의 처치 수·스폰 주기·나무 종류와 마지막 티어의 피해량을 그대로 쓰고, 나무 HP만 오른다.
        int rowWave = spec.bUnlimit ? clearWave : spec.waveNumber;
        RoguelikeBalanceSO.WaveRow row = _balance.GetWaveRow(rowWave);

        spec.tierIndex = _balance.GetTierIndex(rowWave);
        RoguelikeBalanceSO.TierRow tier = _balance.GetTier(spec.tierIndex);

        spec.killTarget = Mathf.Max(1, row.killTarget);
        spec.spawnInterval = Mathf.Max(0.01f, row.spawnInterval);
        spec.initialTrees = ClusterSpawnRules.InitialTreeCount(spec.spawnInterval, _balance.initialTreeSeconds, _balance.threshold, spec.killTarget);
        spec.treeHp = Mathf.Max(1f, row.treeHp);
        spec.treeKind = Mathf.Clamp(row.treeKind, 0, RoguelikeBalanceSO.TreeKindsPerTier - 1);
        spec.staminaDamage = tier.staminaDamage;

        if (spec.bUnlimit)
        {
            spec.treeHp *= 1f + Mathf.Max(0f, _balance.unlimitHpGrowthPerWave) * spec.unlimitIndex;
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
