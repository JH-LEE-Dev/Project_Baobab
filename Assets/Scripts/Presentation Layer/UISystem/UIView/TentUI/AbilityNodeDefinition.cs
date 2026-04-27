using System;

[Serializable]
public class AbilityNodeDatabaseJson
{
    public AbilityNodeDefinitionJson[] nodes;
}

[Serializable]
public class AbilityNodeDefinitionJson
{
    public string skillType;
    public string displayName;
    public string description;
    public int gridX;
    public int gridY;
    public AbilityParentJson[] parents;

    public string[] GetParentSkillTypeNames()
    {
        if (parents != null && parents.Length > 0)
        {
            string[] parentNames = new string[parents.Length];
            for (int i = 0; i < parents.Length; i++)
                parentNames[i] = parents[i] != null ? parents[i].skillType : string.Empty;

            return parentNames;
        }

        return Array.Empty<string>();
    }

    public AbilityParentJson FindParentLineRoute(SkillType _parentSkillType)
    {
        if (parents != null && parents.Length > 0)
        {
            for (int i = 0; i < parents.Length; i++)
            {
                AbilityParentJson parent = parents[i];
                if (parent == null || string.IsNullOrWhiteSpace(parent.skillType))
                    continue;

                if (Enum.TryParse(parent.skillType, true, out SkillType parsedParentSkillType) == false)
                    continue;

                if (parsedParentSkillType != _parentSkillType || parent.usePivot == false)
                    continue;

                return parent;
            }

            return null;
        }

        return null;
    }
}

[Serializable]
public class AbilityParentJson
{
    public string skillType;
    public bool usePivot;
    public int pivotX;
    public int pivotY;
}

[Serializable]
public class AbilityPictureBinding
{
    public SkillType skillType;
    public UnityEngine.Sprite sprite;
}

public enum AbilityLineSegmentSpriteType
{
    Row4,
    Col4,
    DiagSENW4,
    DiagSWNE4,
    Row8,
    Col8,
    DiagSENW8,
    DiagSWNE8,
}

[Serializable]
public class AbilityLineSegmentSpriteBinding
{
    public AbilityLineSegmentSpriteType lineType;
    public UnityEngine.Sprite sprite;
}
