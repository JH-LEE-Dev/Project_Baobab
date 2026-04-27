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
    public int maxLevel;
    public AbilityLevelCostJson[] levelCosts;
    public int gridX;
    public int gridY;
    public AbilityParentJson[] parents;
    public string[] parentSkillTypes;
    public AbilityParentLineRouteJson[] parentLineRoutes;

    public string[] GetParentSkillTypeNames()
    {
        if (parents != null && parents.Length > 0)
        {
            string[] parentNames = new string[parents.Length];
            for (int i = 0; i < parents.Length; i++)
                parentNames[i] = parents[i] != null ? parents[i].skillType : string.Empty;

            return parentNames;
        }

        return parentSkillTypes ?? Array.Empty<string>();
    }

    public AbilityParentLineRouteJson FindParentLineRoute(SkillType _parentSkillType)
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

                return new AbilityParentLineRouteJson
                {
                    parentSkillType = parent.skillType,
                    usePivot = true,
                    pivotX = parent.pivotX,
                    pivotY = parent.pivotY
                };
            }

            return null;
        }

        if (parentLineRoutes == null || parentLineRoutes.Length == 0)
            return null;

        for (int i = 0; i < parentLineRoutes.Length; i++)
        {
            AbilityParentLineRouteJson route = parentLineRoutes[i];
            if (route == null || string.IsNullOrWhiteSpace(route.parentSkillType))
                continue;

            if (Enum.TryParse(route.parentSkillType, true, out SkillType parsedParentSkillType) == false)
                continue;

            if (parsedParentSkillType == _parentSkillType)
                return route;
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
public class AbilityParentLineRouteJson
{
    public string parentSkillType;
    public bool usePivot;
    public int pivotX;
    public int pivotY;
}

[Serializable]
public class AbilityLevelCostJson
{
    public int level;
    public string moneyType;
    public int amount;
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
