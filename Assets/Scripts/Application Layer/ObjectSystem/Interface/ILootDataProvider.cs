using System;
using System.Collections.Generic;

public interface ILootDataProvider
{
    IReadOnlyList<LootType> CurrentOwnedLoots { get; }
    event Action<LootType> LootAcquiredEvent;
}
