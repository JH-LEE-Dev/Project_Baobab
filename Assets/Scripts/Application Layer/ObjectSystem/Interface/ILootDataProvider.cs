using System;
using System.Collections.Generic;

public interface ILootDataProvider
{
    IReadOnlyList<LootType> CurrentOwnedLoots { get; }
    event Action<LootType> LootAcquiredEvent;

    /// <summary>
    /// 성장형 전리품의 지금 단계(1부터)입니다. 성장하지 않는 전리품이거나 아직 얻지 않았으면 0입니다.
    /// 지금은 "원목 보험 증서"(LostAndFoundBox)만 성장합니다(프레스티지 레벨에 따라 I → II → III).
    /// </summary>
    int GetLootGrowthTier(LootType _lootType);

    /// <summary>
    /// 성장형 전리품의 단계가 프레스티지 레벨 상승으로 올랐을 때 (전리품, 새 단계)로 옵니다.
    /// 처음 얻을 때는 오지 않으니, 그때는 LootAcquiredEvent를 받은 뒤 GetLootGrowthTier로 읽으세요.
    /// 효과는 다음 숲 방문부터 적용되지만, 표시는 이 알림 시점에 바꿔도 됩니다.
    /// </summary>
    event Action<LootType, int> LootGrowthTierChangedEvent;
}
