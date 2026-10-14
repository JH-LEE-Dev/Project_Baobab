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
    public float spawnInterval;
    public float treeHpMultiplier;
    public float staminaDamage;
    /// <summary>나무 1그루당 골드. 언리밋은 골드를 쓸 곳이 없어 0(기획서 3-5).</summary>
    public int goldPerTree;
}

/// <summary>밸런스 데이터에서 웨이브별 수치를 계산한다. 기획서 3-1, 3-4, 3-5.</summary>
public static class WaveSchedule
{
    public static WaveSpec Get(RoguelikeBalanceSO _balance, int _waveNumber)
    {
        WaveSpec spec = new WaveSpec { waveNumber = Mathf.Max(1, _waveNumber) };
        if (null == _balance) return spec;

        int clearWave = Mathf.Max(1, _balance.clearWave);
        spec.bUnlimit = spec.waveNumber > clearWave;
        spec.unlimitIndex = spec.bUnlimit ? spec.waveNumber - clearWave : 0;

        // 언리밋은 마지막 웨이브의 처치 수·스폰 주기와 마지막 티어의 피해량을 그대로 쓰고, 나무 HP만 오른다.
        int rowWave = spec.bUnlimit ? clearWave : spec.waveNumber;
        RoguelikeBalanceSO.WaveRow row = _balance.GetWaveRow(rowWave);

        spec.tierIndex = _balance.GetTierIndex(rowWave);
        RoguelikeBalanceSO.TierRow tier = _balance.GetTier(spec.tierIndex);

        spec.killTarget = Mathf.Max(1, row.killTarget);
        spec.spawnInterval = Mathf.Max(0.01f, row.spawnInterval);
        spec.staminaDamage = tier.staminaDamage;
        spec.treeHpMultiplier = Mathf.Max(0.01f, tier.treeHpMultiplier);
        spec.goldPerTree = spec.bUnlimit ? 0 : Mathf.Max(0, tier.goldPerTree);

        if (spec.bUnlimit)
        {
            spec.treeHpMultiplier *= 1f + Mathf.Max(0f, _balance.unlimitHpGrowthPerWave) * spec.unlimitIndex;
        }

        if (spec.waveNumber > 1 && false == spec.bUnlimit)
        {
            spec.bTierChanged = _balance.GetTierIndex(spec.waveNumber - 1) != spec.tierIndex;
        }

        return spec;
    }
}
