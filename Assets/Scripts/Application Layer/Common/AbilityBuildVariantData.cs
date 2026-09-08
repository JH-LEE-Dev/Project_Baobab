using UnityEngine;

/// <summary>
/// 데모/정식 특성 데이터 세트를 한 곳에서 선택합니다.
/// AbilityTool과 런타임 UI가 같은 기준을 써서 JSON과 SkillDataBase가 서로 엇갈리지 않게 합니다.
/// </summary>
[CreateAssetMenu(fileName = "AbilityBuildVariantData", menuName = "Game/Ability Build Variant Data")]
public class AbilityBuildVariantData : ScriptableObject
{
    [Header("Demo")]
    [SerializeField] private TextAsset demoAbilityNodeDatabase;
    [SerializeField] private SkillDataBase demoSkillDataBase;

    [Header("Full")]
    [SerializeField] private TextAsset fullAbilityNodeDatabase;
    [SerializeField] private SkillDataBase fullSkillDataBase;

    public TextAsset CurrentAbilityNodeDatabase =>
        BuildInfo.IsDemo ? demoAbilityNodeDatabase : fullAbilityNodeDatabase;

    public SkillDataBase CurrentSkillDataBase =>
        BuildInfo.IsDemo ? demoSkillDataBase : fullSkillDataBase;

    public TextAsset DemoAbilityNodeDatabase => demoAbilityNodeDatabase;
    public SkillDataBase DemoSkillDataBase => demoSkillDataBase;
    public TextAsset FullAbilityNodeDatabase => fullAbilityNodeDatabase;
    public SkillDataBase FullSkillDataBase => fullSkillDataBase;

    public string CurrentVariantLabel => BuildInfo.IsDemo ? "DEMO" : "FULL";

    public bool HasCurrentDataSet =>
        null != CurrentAbilityNodeDatabase && null != CurrentSkillDataBase;
}
