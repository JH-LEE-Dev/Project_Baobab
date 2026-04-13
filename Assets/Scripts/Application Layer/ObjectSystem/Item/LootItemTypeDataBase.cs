using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LootItemTypeDataBase", menuName = "Game/Loot Item Type Database")]
public class LootItemTypeDataBase : ScriptableObject
{
    public List<LootItemTypeData> datas;

    public LootItemTypeData Get(LootType _type)
    {
        return datas.Find(x => x.lootType == _type);
    }
}
