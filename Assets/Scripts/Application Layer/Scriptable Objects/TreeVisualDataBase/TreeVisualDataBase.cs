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
    public List<Sprite> shieldTopSprites;
    public List<Sprite> shieldBottomSprites;
    public List<Sprite> highlightTopSprites;
    public List<Sprite> highlightBottomSprites;
    public List<Sprite> saplingTopSprites;
    public List<Sprite> saplingBottomSprites;
    public float shieldHDRIntensity;
    public float highlightHDRIntensity;

    [Header("VFX Color")]
    public ParticleColorSet vfxColor;
}

[Serializable]
public struct ParticleColorSet
{
    public ParticleSystem.MinMaxGradient startColor;
    public bool overrideChildrenColor;
}