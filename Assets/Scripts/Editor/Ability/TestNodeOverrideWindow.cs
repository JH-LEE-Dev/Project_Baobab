using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class TestNodeOverrideWindow : EditorWindow
{
    private const string SkillDataBasePath = "Assets/Scriptable Obj/SkillData/SkillDataBase.asset";

    private SkillDataBase skillDataBase;
    private SkillCommandType skillCommandType = SkillCommandType.AxeDamage;
    private ProgressionType progressionType = ProgressionType.Constant;
    private float baseValue = 10f;
    private int maxLevel = 10;
    private bool zeroCost = true;
    private string statusMessage;

    [MenuItem("Tools/Ability/TestNode Override")]
    public static void Open()
    {
        GetWindow<TestNodeOverrideWindow>("TestNode Override");
    }

    private void OnEnable()
    {
        LoadSkillDataBase();
        LoadCurrentTestNode();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("TestNode Override", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("SkillDataBase", skillDataBase, typeof(SkillDataBase), false);
            EditorGUILayout.EnumPopup("SkillType", SkillType.TestNode);
        }

        EditorGUILayout.Space(6f);
        skillCommandType = (SkillCommandType)EditorGUILayout.EnumPopup("SkillCommandType", skillCommandType);
        progressionType = (ProgressionType)EditorGUILayout.EnumPopup("ProgressionType", progressionType);
        baseValue = EditorGUILayout.FloatField("Base Value", baseValue);
        maxLevel = Mathf.Max(1, EditorGUILayout.IntField("Max Level", maxLevel));
        zeroCost = EditorGUILayout.Toggle("Zero Cost", zeroCost);

        EditorGUILayout.Space(8f);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Reload Current"))
            {
                LoadSkillDataBase();
                LoadCurrentTestNode();
            }

            if (GUILayout.Button("Apply To TestNode"))
                ApplyToTestNode();
        }

        if (string.IsNullOrEmpty(statusMessage) == false)
        {
            EditorGUILayout.Space(6f);
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }
    }

    private void LoadSkillDataBase()
    {
        skillDataBase = AssetDatabase.LoadAssetAtPath<SkillDataBase>(SkillDataBasePath);
    }

    private void LoadCurrentTestNode()
    {
        if (TryFindTestNode(out Skill skill) == false)
        {
            statusMessage = "TestNode data is not found. Apply will create it.";
            return;
        }

        maxLevel = Mathf.Max(1, skill.maxLevel);

        if (skill.skillTypes != null && skill.skillTypes.Count > 0)
        {
            SkillCommandInfo command = skill.skillTypes[0];
            skillCommandType = command.skillCommandType;
            progressionType = command.amountCurve.type;
            baseValue = command.amountCurve.baseValue;
        }

        statusMessage = "Loaded current TestNode data.";
    }

    private void ApplyToTestNode()
    {
        if (skillDataBase == null)
        {
            statusMessage = "SkillDataBase asset not found.";
            return;
        }

        Undo.RecordObject(skillDataBase, "Override TestNode Skill");

        if (skillDataBase.skills == null)
            skillDataBase.skills = new List<Skill>();

        int index = FindTestNodeIndex();
        Skill skill = index >= 0 ? skillDataBase.skills[index] : CreateDefaultTestNode();

        skill.skillType = SkillType.TestNode;
        skill.maxLevel = Mathf.Max(1, maxLevel);
        skill.skillTypes = new List<SkillCommandInfo>
        {
            new SkillCommandInfo
            {
                skillCommandType = skillCommandType,
                amountCurve = new ProgressionCurve
                {
                    type = progressionType,
                    baseValue = baseValue,
                    manualValues = new List<float>()
                }
            }
        };

        if (zeroCost)
            skill.cost = CreateZeroCost();

        if (skill.prerequisiteSkills == null)
            skill.prerequisiteSkills = new List<SkillType>();

        if (index >= 0)
            skillDataBase.skills[index] = skill;
        else
            skillDataBase.skills.Add(skill);

        EditorUtility.SetDirty(skillDataBase);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        statusMessage = $"Applied TestNode: {skillCommandType}, {progressionType}, {baseValue}";
    }

    private bool TryFindTestNode(out Skill _skill)
    {
        int index = FindTestNodeIndex();
        if (index >= 0)
        {
            _skill = skillDataBase.skills[index];
            return true;
        }

        _skill = default;
        return false;
    }

    private int FindTestNodeIndex()
    {
        if (skillDataBase == null || skillDataBase.skills == null)
            return -1;

        for (int i = 0; i < skillDataBase.skills.Count; i++)
        {
            if (skillDataBase.skills[i].skillType == SkillType.TestNode)
                return i;
        }

        return -1;
    }

    private Skill CreateDefaultTestNode()
    {
        return new Skill
        {
            skillType = SkillType.TestNode,
            maxLevel = Mathf.Max(1, maxLevel),
            cost = CreateZeroCost(),
            skillTypes = new List<SkillCommandInfo>(),
            prerequisiteSkills = new List<SkillType>()
        };
    }

    private SkillCost CreateZeroCost()
    {
        return new SkillCost
        {
            moneyCurve = CreateZeroCurve(),
            carrotCurve = CreateZeroCurve()
        };
    }

    private ProgressionCurve CreateZeroCurve()
    {
        return new ProgressionCurve
        {
            type = ProgressionType.Constant,
            baseValue = 0f,
            manualValues = new List<float>()
        };
    }
}
