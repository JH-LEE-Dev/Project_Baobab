using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ZoneData", menuName = "ScriptableObjects/Zone/ZoneData")]
public class ZoneData : ScriptableObject
{
    [Header("Identities")]
    [SerializeField] private int regionId;
    [SerializeField] private int zoneId;

    [Header("Display Info")]
    [SerializeField] private string zoneName;

    [Header("Farming Resources")]
    [SerializeField] private List<FarmingItemData> farmableTrees;
    [SerializeField] private List<FarmingItemData> farmableAnimals;

    public int RegionId => regionId;
    public int ZoneId => zoneId;
    public string ZoneName => zoneName;
    public IReadOnlyList<FarmingItemData> FarmableTrees => farmableTrees;
    public IReadOnlyList<FarmingItemData> FarmableAnimals => farmableAnimals;
}
