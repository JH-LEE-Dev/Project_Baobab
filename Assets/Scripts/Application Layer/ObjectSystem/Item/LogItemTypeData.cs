
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// LogState별로 갈아끼울 스프라이트 묶음.
/// 비워 둔 항목은 기본 스프라이트(sprite / timberSprite)로 폴백한다.
/// </summary>
[System.Serializable]
public struct LogStateSpriteData
{
    public LogState logState;

    [Tooltip("드랍/인벤토리에 보이는 원목 스프라이트.")]
    public Sprite sprite;

    [Tooltip("커터에서 가공된 뒤의 제재목 스프라이트.")]
    public Sprite timberSprite;
}

[System.Serializable]
public class LogItemTypeData
{
    public ItemType itemType;
    public TreeType treeType;
    public float durability;
    public Sprite sprite;
    public Sprite timberSprite;
    public Color color;

    // 나무 등급이 높을수록(황금/다이아/무지개) 다른 스프라이트로 드랍/가공된다.
    // 상태별 스프라이트가 없는 나무는 비워 두면 기본 스프라이트로 폴백한다.
    public List<LogStateSpriteData> stateSprites;

    /// <summary>
    /// 이 원목 종류가 해당 LogState에서 쓸 원목 스프라이트를 돌려준다.
    /// 매핑이 없거나 비어 있으면 기본 스프라이트를 쓴다.
    /// </summary>
    public Sprite GetSprite(LogState _logState)
    {
        if (stateSprites != null)
        {
            for (int i = 0; i < stateSprites.Count; i++)
            {
                if (stateSprites[i].logState == _logState && stateSprites[i].sprite != null)
                {
                    return stateSprites[i].sprite;
                }
            }
        }

        return sprite;
    }

    /// <summary>
    /// 이 원목 종류가 해당 LogState에서 쓸 제재목 스프라이트를 돌려준다.
    /// 매핑이 없거나 비어 있으면 기본 제재목 스프라이트를 쓴다.
    /// </summary>
    public Sprite GetTimberSprite(LogState _logState)
    {
        if (stateSprites != null)
        {
            for (int i = 0; i < stateSprites.Count; i++)
            {
                if (stateSprites[i].logState == _logState && stateSprites[i].timberSprite != null)
                {
                    return stateSprites[i].timberSprite;
                }
            }
        }

        return timberSprite;
    }
}
