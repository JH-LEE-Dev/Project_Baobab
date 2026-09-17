using UnityEngine;

/// <summary>
/// 원석 알갱이의 크기. 같은 종류(황금/다이아/프리즘)라도 크기에 따라 다른 그림을 쓰고,
/// 담고 있는 재화량도 달라진다(GemOreItemController의 크기 가중치).
/// </summary>
public enum GemOreSize
{
    Small,
    Medium,
    Large,
}

/// <summary>
/// 원석 종류별 외형. 크기마다 그림이 따로 있다(Jewel_Gold_S / _M / _L 식).
///
/// 재화량은 여기 있지 않다. 나무 한 그루에서 나오는 총 재화량이 먼저 정해지고(GemOreDropData)
/// 그 총량을 떨어진 알갱이들에 크기 비율로 나눠 담기 때문이다.
/// </summary>
[System.Serializable]
public struct GemOreTypeData
{
    public GemOreType gemOreType;

    [Tooltip("작은 원석 그림")]
    public Sprite smallSprite;

    [Tooltip("중간 원석 그림")]
    public Sprite mediumSprite;

    [Tooltip("큰 원석 그림")]
    public Sprite largeSprite;

    [Tooltip("그림에 입힐 색. 그림 자체에 색이 들어있다면 흰색으로 둔다.")]
    public Color color;

    public Sprite GetSprite(GemOreSize _size)
    {
        switch (_size)
        {
            case GemOreSize.Large: return largeSprite != null ? largeSprite : mediumSprite != null ? mediumSprite : smallSprite;
            case GemOreSize.Medium: return mediumSprite != null ? mediumSprite : smallSprite;
            default: return smallSprite;
        }
    }
}

/// <summary>
/// "어떤 나무가 어떤 보석 단계로 쓰러졌을 때 무엇이 얼마나 나오는가"를 정하는 한 줄.
///
/// 기획 표가 총 재화량과 알갱이 개수를 따로 적어두는데, 이 둘은 고정 배수 관계가 아니다
/// (예: 자작나무는 S 4~6 + M 2~3에 총 36~50이라, 크기마다 고정값을 주는 방식으로는 재현이 안 된다).
/// 그래서 <b>총 재화량이 기준</b>이고 알갱이 개수는 그것을 어떻게 나눠 보여줄지에 대한 값이다.
/// </summary>
[System.Serializable]
public struct GemOreDropData
{
    [Tooltip("이 줄이 적용될 나무 종류")]
    public TreeType treeType;

    [Tooltip("이 줄이 적용될 보석 단계 (황금/다이아/프리즘)")]
    public GemOreType gemOreType;

    [Header("총 재화량 (이 값이 기준이다)")]
    [Tooltip("캐릭터가 이 나무 한 그루에서 최종적으로 얻게 될 재화량의 최솟값")]
    public int minTotalCurrency;
    [Tooltip("최댓값")]
    public int maxTotalCurrency;

    [Header("떨어질 알갱이 개수")]
    public int minSmallCnt;
    public int maxSmallCnt;
    public int minMediumCnt;
    public int maxMediumCnt;
    public int minLargeCnt;
    public int maxLargeCnt;

    /// <summary>값이 하나도 채워지지 않은 빈 줄인지. 미정 조합을 걸러내는 데 쓴다.</summary>
    public bool IsEmpty =>
        maxTotalCurrency <= 0 &&
        maxSmallCnt <= 0 && maxMediumCnt <= 0 && maxLargeCnt <= 0;
}
