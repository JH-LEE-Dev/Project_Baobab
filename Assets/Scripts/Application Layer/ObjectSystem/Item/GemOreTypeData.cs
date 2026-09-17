using UnityEngine;

/// <summary>
/// 원석 종류별 외형과 획득 시 지급할 재화량.
/// 어느 재화로 들어갈지는 원석 종류에서 결정되므로(InventoryManager.GemOreTypeToMoneyType)
/// 여기서 MoneyType을 따로 들고 있지 않는다.
/// </summary>
[System.Serializable]
public struct GemOreTypeData
{
    public GemOreType gemOreType;

    [Tooltip("떨어질 때/바닥에 놓였을 때 보이는 원석 스프라이트.")]
    public Sprite sprite;

    [Tooltip("스프라이트에 입힐 색. 스프라이트 자체에 색이 들어있다면 흰색으로 둔다.")]
    public Color color;

    [Tooltip("이 원석 1개를 주웠을 때 재화가 몇 올라가는지.")]
    public long currencyAmount;
}

/// <summary>
/// 보석 단계별 원석 드랍 개수 범위.
/// </summary>
[System.Serializable]
public struct GemOreDropCntData
{
    public GemOreType gemOreType;
    public int minCnt;
    public int maxCnt;
}
