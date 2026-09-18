using System;
using UnityEngine;

/// <summary>
/// 용광로 한 종류의 가공 규칙. 기획 표를 그대로 옮긴 것이다.
///
///   주괴    기본 가치   용광로 가공량   비고
///   황금      100          40          황금 원석 10개로 가공
///   다이아     -            -          다이아 원석 20개로 가공
///   프리즘     -            -          프리즘 원석 30개로 가공
///
/// 가공량은 초당 1씩 소모되므로(가속 특성이 붙으면 그 배율만큼 빨라진다), 황금은 한 개 뽑는 데 40초다.
/// 값이 아직 안 나온 다이아/프리즘도 표에 있는 원석 개수는 채워두고 나머지는 0으로 둔다.
/// 가공량이나 필요 원석이 0 이하면 그 용광로는 가공을 시작하지 않는다.
/// </summary>
[Serializable]
public struct BlastFurnaceRecipe
{
    [Tooltip("이 용광로가 다루는 원석 등급.")]
    public GemOreType gemOreType;

    [Tooltip("주괴 하나를 만드는 데 넣어야 하는 원석 개수. 다 모이면 한 번에 차감되고 가공이 시작된다.")]
    public int orePerIngot;

    [Tooltip("용광로가 쌓아둘 수 있는 원석 수. 0이면 무제한(가진 만큼 다 넣을 수 있다).")]
    public int oreCapacity;

    [Tooltip("주괴 하나의 가공량. 초당 1씩(가속 배율 적용) 줄어든다.")]
    public float workPerIngot;

    [Tooltip("완성된 주괴가 상점NPC에 꽂힐 때 올려줄 돈. 표의 '기본 가치'.")]
    public long ingotValue;

    [Tooltip("캐릭터가 넣을 때 날아가는 원석 그림. 각 보석의 M 사이즈를 쓴다.")]
    public Sprite oreSprite;

    [Tooltip("완성된 주괴가 상점NPC로 날아갈 때 쓰는 그림.")]
    public Sprite ingotSprite;

    /// <summary>값이 채워져 실제로 가공이 가능한 규칙인지.</summary>
    public bool IsValid => orePerIngot > 0 && workPerIngot > 0f;
}
