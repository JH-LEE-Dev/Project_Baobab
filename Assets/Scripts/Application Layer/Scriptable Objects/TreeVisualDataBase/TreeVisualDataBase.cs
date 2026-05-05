using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Tree Visual Data Base", menuName = "Game/Objects/Tree Visual Data Base")]
public class TreeVisualDataBase : ScriptableObject
{
    public List<TreeVisualData> treeVisualDatas;

    public TreeVisualData Get(TreeType _type)
    {
        return treeVisualDatas.Find(x => x.treeType == _type);
    }
}

[Serializable]
public struct TreeVisualData
{
    public TreeType treeType;
    public List<Sprite> topSprites;
    public List<Sprite> bottomSprites;
    public List<Sprite> saplingTopSprites;
    public List<Sprite> saplingBottomSprites;

    public Color topColor;
    public Color bottomColor;
}