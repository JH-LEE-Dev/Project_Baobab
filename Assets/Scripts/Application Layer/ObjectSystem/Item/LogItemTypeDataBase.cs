using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LogItemTypeDataBase", menuName = "Game/Log Item Type Database")]
public class LogItemTypeDataBase : ScriptableObject
{
    public List<LogItemTypeData> datas;

    // TreeType을 인덱스로 바로 꺼내기 위한 캐시.
    //
    // 원래 Get()은 람다를 쓰는 List.Find라 호출마다 클로저(디스플레이 클래스 + Predicate)가
    // 할당됐는데, 이 조회는 원목 드랍(나무 하나당 4~7회)과 습득 판정처럼 잦은 경로에서 불린다.
    // 캐시를 호출하는 쪽마다 따로 두면 조회 경로가 갈라지므로 데이터베이스가 직접 들고 있는다.
    private LogItemTypeData[] dataByTreeType;

    public LogItemTypeData Get(TreeType _type)
    {
        if (dataByTreeType == null)
        {
            BuildCache();
        }

        int index = (int)_type;
        if (index < 0 || index >= dataByTreeType.Length) return null;

        return dataByTreeType[index];
    }

    private void BuildCache()
    {
        dataByTreeType = new LogItemTypeData[(int)TreeType.Max + 1];

        if (datas == null) return;

        for (int i = 0; i < datas.Count; i++)
        {
            LogItemTypeData data = datas[i];
            if (data == null) continue;

            int index = (int)data.treeType;
            if (index < 0 || index >= dataByTreeType.Length) continue;

            // 원래 쓰던 List.Find와 의미를 맞춘다 - 같은 수종이 여러 번 들어 있으면 첫 항목이 이긴다.
            if (dataByTreeType[index] != null) continue;

            dataByTreeType[index] = data;
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// 인스펙터에서 목록을 고치면 캐시를 버린다. 에디터 플레이 중 데이터를 만지는 동안에도
    /// Get()이 항상 최신 목록을 보도록 하기 위한 장치다(빌드에는 들어가지 않는다).
    /// </summary>
    private void OnValidate()
    {
        dataByTreeType = null;
    }
#endif
}
