using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;


/// <summary>
/// BootStrap의 자동저장(enableAutoSave)이 꺼진 채로 빌드되는 것을 막습니다.
///
/// [왜 필요한가]
/// enableAutoSave는 개발자가 에디터에서 세이브를 오염시키지 않고 테스트하려고 끄는 스위치입니다.
/// MainMenuScene에 직렬화되는 값이라 끈 채로 씬을 커밋하기 쉽고, 그대로 빌드되면 유저 진행이
/// 저장되지 않는 빌드가 나갑니다. 그래서 Development Build 여부와 상관없이 항상 중단합니다.
///
/// [이중 안전장치]
/// 빌드된 게임에서는 BootStrap.IsAutoSaveEnabled가 인스펙터 값과 무관하게 항상 true를 돌려줍니다.
/// 이 가드를 어떻게든 우회하더라도 런타임에서는 자동저장이 켜진 채로 동작합니다.
/// 이 가드는 "꺼진 값이 커밋되어 있다"는 사실을 빌드 시점에 드러내는 역할입니다.
///
/// [어디를 보는가]
/// 1) 빌드에 포함된 씬 파일(디스크)의 BootStrap 컴포넌트와 프리팹 인스턴스 오버라이드
/// 2) 에디터에 열려 있는 씬의 BootStrap (저장하지 않은 변경까지 잡기 위해)
/// </summary>
public class AutoSaveBuildGuard : IPreprocessBuildWithReport
{
    private const string FIELD_NAME = "enableAutoSave";

    /// <summary>
    /// 빌드를 실패시키는 검사라 에셋을 수정하는 스트리퍼(DemoContentStripper 0, AbilityBuildVariantStripper -100)보다
    /// 먼저 돌아야 합니다. 백엔드 가드(-1000)와 플랫폼 정합성 가드(-900) 사이에 둡니다.
    /// </summary>
    public int callbackOrder => -950;

    public void OnPreprocessBuild(BuildReport _report)
    {
        List<string> _offenders = new List<string>();

        CollectFromOpenScenes(_offenders);
        CollectFromBuildScenes(_offenders);

        if (0 == _offenders.Count) return;

        throw new BuildFailedException(
            "[AutoSaveGuard] 빌드를 중단했습니다. BootStrap의 자동저장(Enable Auto Save)이 꺼져 있습니다.\n" +
            "  " + string.Join("\n  ", _offenders) + "\n" +
            "\n" +
            "자동저장이 꺼진 빌드는 유저 진행이 저장되지 않습니다. 개발용 빌드라도 허용하지 않습니다.\n" +
            "해결: MainMenuScene의 BootStrap 인스펙터에서 Save > Enable Auto Save 를 켜고 씬을 저장하십시오.");
    }

    private static void CollectFromOpenScenes(List<string> _offenders)
    {
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            UnityEngine.SceneManagement.Scene _scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);

            if (false == _scene.isLoaded) continue;

            // 빌드에 들어가지 않는 씬(테스트용 임시 씬, _Recovery 등)은 무관하다. 여기서 막으면 거짓 양성이다.
            if (false == IsEnabledBuildScene(_scene.path)) continue;

            GameObject[] _roots = _scene.GetRootGameObjects();

            for (int r = 0; r < _roots.Length; r++)
            {
                BootStrap[] _bootStraps = _roots[r].GetComponentsInChildren<BootStrap>(true);

                for (int b = 0; b < _bootStraps.Length; b++)
                {
                    SerializedProperty _prop = new SerializedObject(_bootStraps[b]).FindProperty(FIELD_NAME);

                    if (null == _prop || true == _prop.boolValue) continue;

                    string _dirty = _scene.isDirty ? " (저장 안 된 변경 포함)" : "";
                    _offenders.Add($"열린 씬 {_scene.path}{_dirty} - {_bootStraps[b].gameObject.name}");
                }
            }
        }
    }

    /// <summary>
    /// 씬 파일을 텍스트로 읽어 검사합니다. 빌드는 디스크의 씬을 쓰므로, 열려 있지 않은 씬도 여기서 잡힙니다.
    /// 씬을 실제로 열면 사용자의 에디터 상태를 바꾸게 되므로 열지 않습니다.
    /// </summary>
    private static void CollectFromBuildScenes(List<string> _offenders)
    {
        string _bootStrapGuid = FindBootStrapScriptGuid();

        if (string.IsNullOrEmpty(_bootStrapGuid)) return;

        EditorBuildSettingsScene[] _scenes = EditorBuildSettings.scenes;

        for (int i = 0; i < _scenes.Length; i++)
        {
            if (false == _scenes[i].enabled) continue;

            string _path = _scenes[i].path;

            if (false == File.Exists(_path)) continue;

            if (true == SceneFileDisablesAutoSave(File.ReadAllText(_path), _bootStrapGuid))
            {
                _offenders.Add($"씬 파일 {_path}");
            }
        }
    }

    private static bool IsEnabledBuildScene(string _path)
    {
        if (string.IsNullOrEmpty(_path)) return false;

        EditorBuildSettingsScene[] _scenes = EditorBuildSettings.scenes;

        for (int i = 0; i < _scenes.Length; i++)
        {
            if (true == _scenes[i].enabled && _scenes[i].path == _path) return true;
        }

        return false;
    }

    private static string FindBootStrapScriptGuid()
    {
        string[] _guids = AssetDatabase.FindAssets("t:MonoScript BootStrap");

        for (int i = 0; i < _guids.Length; i++)
        {
            MonoScript _script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(_guids[i]));

            if (null != _script && typeof(BootStrap) == _script.GetClass()) return _guids[i];
        }

        return null;
    }

    private static bool SceneFileDisablesAutoSave(string _yaml, string _bootStrapGuid)
    {
        // Unity YAML은 "--- !u!" 로 오브젝트 블록이 나뉜다.
        string[] _blocks = _yaml.Replace("\r\n", "\n").Split(new[] { "\n--- !u!" }, System.StringSplitOptions.None);

        for (int i = 0; i < _blocks.Length; i++)
        {
            string _block = _blocks[i];

            // 씬에 직접 놓인 BootStrap 컴포넌트
            if (_block.Contains("guid: " + _bootStrapGuid) && _block.Contains("m_Script:"))
            {
                if (_block.Contains($"\n  {FIELD_NAME}: 0")) return true;
            }

            // 프리팹 인스턴스의 오버라이드 (propertyPath 다음 줄에 value가 온다)
            int _idx = _block.IndexOf($"propertyPath: {FIELD_NAME}\n");

            while (_idx >= 0)
            {
                int _valueStart = _block.IndexOf("value:", _idx);

                if (_valueStart >= 0)
                {
                    int _lineEnd = _block.IndexOf('\n', _valueStart);
                    string _value = (_lineEnd < 0 ? _block.Substring(_valueStart) : _block.Substring(_valueStart, _lineEnd - _valueStart)).Trim();

                    if ("value: 0" == _value) return true;
                }

                _idx = _block.IndexOf($"propertyPath: {FIELD_NAME}\n", _idx + 1);
            }
        }

        return false;
    }
}
