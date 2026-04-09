using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ZoneDatabase", menuName = "ScriptableObjects/Zone/ZoneDatabase")]
public class ZoneDatabase : ScriptableObject
{
    //외부 의존성
    [SerializeField] private List<ZoneData> zoneDatas;

    //퍼블릭 초기화 및 제어 메서드
    public ZoneData GetZoneData(int _regionId, int _zoneId)
    {
        if (zoneDatas == null) return null;

        for (int i = 0; i < zoneDatas.Count; i++)
        {
            if (zoneDatas[i].RegionId == _regionId && zoneDatas[i].ZoneId == _zoneId)
            {
                return zoneDatas[i];
            }
        }
        return null;
    }
}
