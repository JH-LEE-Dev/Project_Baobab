using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// 선택하지 않은 특성 데이터 세트가 빌드에 포함되지 않도록 참조를 잠시 제거합니다.
/// 원본 설정 에셋은 빌드 전에 백업하고 빌드 후 복구하며, 비정상 종료 시 다음 에디터 시작에 복구합니다.
/// </summary>
public class AbilityBuildVariantStripper : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => -100;

    private const string VARIANT_DATA_PATH =
        "Assets/Scriptable Obj/SkillData/AbilityBuildVariantData.asset";
    private const string BACKUP_FOLDER = "AbilityBuildVariantStripBackup";

    public void OnPreprocessBuild(BuildReport _report)
    {
        RestoreIfNeeded(false);

        AbilityBuildVariantData _data =
            AssetDatabase.LoadAssetAtPath<AbilityBuildVariantData>(VARIANT_DATA_PATH);

        if (null == _data)
        {
            throw new BuildFailedException(
                $"특성 빌드 분기 설정을 찾지 못했습니다: {VARIANT_DATA_PATH}");
        }

        if (false == _data.HasCurrentDataSet)
        {
            throw new BuildFailedException(
                $"{_data.CurrentVariantLabel} 특성 데이터 세트가 완성되지 않았습니다. " +
                "AbilityNodeDatabase와 SkillDataBase를 모두 연결해야 합니다.");
        }

        Directory.CreateDirectory(BackupDirectory);
        File.Copy(ToAbsolute(VARIANT_DATA_PATH), BackupFilePath, true);

        SerializedObject _serializedData = new SerializedObject(_data);
        string _unusedNodeProperty = BuildInfo.IsDemo
            ? "fullAbilityNodeDatabase"
            : "demoAbilityNodeDatabase";
        string _unusedSkillProperty = BuildInfo.IsDemo
            ? "fullSkillDataBase"
            : "demoSkillDataBase";

        _serializedData.FindProperty(_unusedNodeProperty).objectReferenceValue = null;
        _serializedData.FindProperty(_unusedSkillProperty).objectReferenceValue = null;
        _serializedData.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(_data);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"[AbilityBuildVariantStrip] {_data.CurrentVariantLabel} 빌드 - 반대 버전 특성 데이터 참조를 제외했습니다.\n" +
            "빌드가 끝나면 설정 에셋을 자동 복구합니다.");
    }

    public void OnPostprocessBuild(BuildReport _report)
    {
        RestoreIfNeeded(false);
    }

    private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;
    private static string BackupDirectory => Path.Combine(ProjectRoot, "Library", BACKUP_FOLDER);
    private static string BackupFilePath =>
        Path.Combine(BackupDirectory, Path.GetFileName(VARIANT_DATA_PATH));

    private static string ToAbsolute(string _assetPath)
    {
        return Path.Combine(ProjectRoot, _assetPath.Replace('/', Path.DirectorySeparatorChar));
    }

    public static void RestoreIfNeeded(bool _isEditorStartup)
    {
        if (false == File.Exists(BackupFilePath)) return;

        File.Copy(BackupFilePath, ToAbsolute(VARIANT_DATA_PATH), true);
        AssetDatabase.ImportAsset(VARIANT_DATA_PATH, ImportAssetOptions.ForceUpdate);
        Directory.Delete(BackupDirectory, true);

        string _message =
            $"[AbilityBuildVariantStrip] 원본 설정 에셋을 복구했습니다: {VARIANT_DATA_PATH}";

        if (true == _isEditorStartup)
        {
            Debug.LogWarning(
                _message + "\n이전 빌드가 정상적으로 끝나지 않은 것 같습니다. 연결 상태를 확인하세요.");
        }
        else
        {
            Debug.Log(_message);
        }
    }
}

[InitializeOnLoad]
internal static class AbilityBuildVariantStripRecovery
{
    static AbilityBuildVariantStripRecovery()
    {
        EditorApplication.delayCall += () => AbilityBuildVariantStripper.RestoreIfNeeded(true);
    }
}
