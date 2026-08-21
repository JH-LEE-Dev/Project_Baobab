using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 이 프로젝트에는 전역 네임스페이스에 자체 SceneManager 클래스가 있어서 이름이 가려진다.
using UnitySceneManager = UnityEngine.SceneManagement.SceneManager;

/// <summary>
/// 프리팹·씬의 모든 TMP 텍스트에 <see cref="LocalizedFontTracker"/>를 미리 부착합니다.
///
/// 왜 미리 붙여야 하는가:
/// FontLocalizer는 TMP의 "텍스트 갱신됨" 이벤트로 새 텍스트를 발견하는데, 이 이벤트는
/// 이미 한 번 그려진 뒤에 온다. 그래서 런타임에 Instantiate된 UI는 첫 프레임을 갈무리로 그린다.
/// 갈무리11.ttf에는 가나 187자와 한자 6,477자가 들어 있고 Galmuri11_Optimum이 Dynamic 아틀라스라,
/// 그 한 프레임에 한국식 자형이 실제로 그려지고 그 글리프가 갈무리 아틀라스에 계속 쌓인다.
///
/// 트래커가 프리팹에 미리 붙어 있으면 Instantiate 직후 OnEnable에서 교체가 끝나므로,
/// 캔버스가 처음 그리기 전에 이미 올바른 폰트가 된다. 위 문제가 원천적으로 사라진다.
///
/// 중첩 프리팹은 건드리지 않는다. 원본 프리팹 쪽에 붙으면 인스턴스가 상속받으므로,
/// 여기서 또 붙이면 오버라이드가 생기고 결국 컴포넌트가 두 개가 된다.
/// </summary>
public static class LocalizedFontTrackerInstaller
{
    private const string PREFAB_SEARCH_ROOT = "Assets/Prefabs";
    private const string MENU_ROOT = "Tools/Localization/Font Tracker/";

    private static readonly List<TMP_Text> textBuffer = new List<TMP_Text>(32);

    // //메뉴 진입점
    [MenuItem(MENU_ROOT + "검사 (변경 없음)", false, 1)]
    private static void Inspect()
    {
        InspectAll();
    }

    [MenuItem(MENU_ROOT + "전체 부착", false, 2)]
    private static void Attach()
    {
        if (false == EditorUtility.DisplayDialog(
            "Localized Font Tracker 부착",
            $"{PREFAB_SEARCH_ROOT} 아래 프리팹과 빌드 세팅에 등록된 씬의 모든 TMP 텍스트에 " +
            "LocalizedFontTracker를 부착합니다.\n\n" +
            "여러 에셋을 수정하므로 실행 전 커밋해두는 것을 권장합니다. 계속할까요?",
            "부착", "취소"))
        {
            return;
        }

        AttachAll();
    }

    [MenuItem(MENU_ROOT + "전체 제거", false, 3)]
    private static void Remove()
    {
        if (false == EditorUtility.DisplayDialog(
            "Localized Font Tracker 제거",
            "부착된 LocalizedFontTracker를 모두 제거합니다. 계속할까요?",
            "제거", "취소"))
        {
            return;
        }

        RemoveAll();
    }

    // //퍼블릭 제어 메서드 (확인 다이얼로그 없이 바로 실행한다. 자동화·스크립트용)
    /// <summary>부착이 필요한 텍스트 수만 세어 로그로 남깁니다. 에셋을 바꾸지 않습니다.</summary>
    public static int InspectAll()
    {
        return Execute(EMode.Inspect);
    }

    /// <summary>모든 대상에 트래커를 부착하고, 실제로 부착한 수를 반환합니다.</summary>
    public static int AttachAll()
    {
        return Execute(EMode.Attach);
    }

    /// <summary>부착된 트래커를 모두 제거하고, 제거한 수를 반환합니다.</summary>
    public static int RemoveAll()
    {
        return Execute(EMode.Remove);
    }

    // //내부 로직
    private enum EMode
    {
        Inspect,
        Attach,
        Remove
    }

    private static int Execute(EMode _mode)
    {
        // 씬을 열었다 닫으므로, 저장하지 않은 변경이 있으면 먼저 사용자에게 묻는다.
        if (EMode.Inspect != _mode && false == EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return 0;
        }

        string _originalScenePath = UnitySceneManager.GetActiveScene().path;

        int _prefabAssets = 0;
        int _affected = 0;
        StringBuilder _report = new StringBuilder(1024);

        // StartAssetEditing으로 임포트를 묶고 싶겠지만, 그 구간 안에서 LoadPrefabContents가
        // 아직 임포트되지 않은 에셋을 건드리면 동작이 불안정해진다. 프리팹 90개 남짓이라
        // 재임포트를 그냥 감수하는 편이 안전하다.
        try
        {
            _affected += ProcessPrefabs(_mode, _report, out _prefabAssets);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        _affected += ProcessScenes(_mode, _report, _originalScenePath);

        if (EMode.Inspect != _mode)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        string _verb = _mode switch
        {
            EMode.Attach => "부착",
            EMode.Remove => "제거",
            _ => "부착 필요"
        };

        Debug.Log($"[LocalizedFontTrackerInstaller] 프리팹 {_prefabAssets}개 검사 완료. " +
            $"{_verb} 대상 {_affected}개.\n{_report}");

        return _affected;
    }

    private static int ProcessPrefabs(EMode _mode, StringBuilder _report, out int _scannedCount)
    {
        string[] _guids = AssetDatabase.FindAssets("t:Prefab", new[] { PREFAB_SEARCH_ROOT });
        _scannedCount = _guids.Length;

        int _affected = 0;

        for (int i = 0; i < _guids.Length; i++)
        {
            string _path = AssetDatabase.GUIDToAssetPath(_guids[i]);

            if (true == EditorUtility.DisplayCancelableProgressBar(
                "Localized Font Tracker", _path, (float)i / _guids.Length))
            {
                break;
            }

            GameObject _root = PrefabUtility.LoadPrefabContents(_path);
            if (null == _root) continue;

            try
            {
                int _changed = ProcessHierarchy(_root, _mode);
                if (0 == _changed) continue;

                _affected += _changed;
                _report.AppendLine($"  {_path} : {_changed}");

                if (EMode.Inspect != _mode)
                {
                    PrefabUtility.SaveAsPrefabAsset(_root, _path);
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(_root);
            }
        }

        return _affected;
    }

    private static int ProcessScenes(EMode _mode, StringBuilder _report, string _originalScenePath)
    {
        if (EMode.Inspect == _mode)
        {
            // 검사 모드에서는 사용자의 작업 씬을 닫지 않는다. 열려 있는 씬만 훑는다.
            return ProcessOpenScenes(_mode, _report);
        }

        EditorBuildSettingsScene[] _buildScenes = EditorBuildSettings.scenes;
        int _affected = 0;
        bool _touchedScenes = false;

        for (int i = 0; i < _buildScenes.Length; i++)
        {
            if (false == _buildScenes[i].enabled) continue;

            string _path = _buildScenes[i].path;
            if (false == System.IO.File.Exists(_path)) continue;

            Scene _scene = EditorSceneManager.OpenScene(_path, OpenSceneMode.Single);
            _touchedScenes = true;

            int _changed = ProcessScene(_scene, _mode);
            if (0 == _changed) continue;

            _affected += _changed;
            _report.AppendLine($"  {_path} : {_changed}");

            EditorSceneManager.MarkSceneDirty(_scene);
            EditorSceneManager.SaveScene(_scene);
        }

        // 원래 열려 있던 씬으로 되돌려준다.
        if (true == _touchedScenes && false == string.IsNullOrEmpty(_originalScenePath)
            && true == System.IO.File.Exists(_originalScenePath))
        {
            EditorSceneManager.OpenScene(_originalScenePath, OpenSceneMode.Single);
        }

        return _affected;
    }

    private static int ProcessOpenScenes(EMode _mode, StringBuilder _report)
    {
        int _affected = 0;

        for (int i = 0; i < UnitySceneManager.sceneCount; i++)
        {
            Scene _scene = UnitySceneManager.GetSceneAt(i);
            if (false == _scene.isLoaded) continue;

            int _changed = ProcessScene(_scene, _mode);
            if (0 == _changed) continue;

            _affected += _changed;
            _report.AppendLine($"  (열린 씬) {_scene.path} : {_changed}");
        }

        return _affected;
    }

    private static int ProcessScene(Scene _scene, EMode _mode)
    {
        GameObject[] _roots = _scene.GetRootGameObjects();
        int _changed = 0;

        for (int i = 0; i < _roots.Length; i++)
        {
            _changed += ProcessHierarchy(_roots[i], _mode);
        }

        return _changed;
    }

    private static int ProcessHierarchy(GameObject _root, EMode _mode)
    {
        _root.GetComponentsInChildren(true, textBuffer);

        int _changed = 0;

        for (int i = 0; i < textBuffer.Count; i++)
        {
            TMP_Text _text = textBuffer[i];
            if (null == _text) continue;

            GameObject _go = _text.gameObject;

            // 중첩 프리팹(및 프리팹 배리언트의 상속분)은 원본 프리팹 쪽에서 처리한다.
            // 여기서 손대면 프리팹 오버라이드가 되고, 원본에도 붙는 순간 중복이 된다.
            if (true == PrefabUtility.IsPartOfPrefabInstance(_go)) continue;

            bool _hasTracker = _go.TryGetComponent(out LocalizedFontTracker _tracker);

            if (EMode.Remove == _mode)
            {
                if (false == _hasTracker) continue;

                _changed++;
                Object.DestroyImmediate(_tracker, true);
                continue;
            }

            if (true == _hasTracker) continue;

            _changed++;
            if (EMode.Attach == _mode)
            {
                // 원본 폰트 값은 굳이 여기서 기록하지 않는다. 런타임 Awake가 그 시점의 실제
                // 값을 읽어가므로, 나중에 아티스트가 프리팹의 폰트를 바꿔도 기록이 어긋나지 않는다.
                _go.AddComponent<LocalizedFontTracker>();
            }
        }

        textBuffer.Clear();
        return _changed;
    }
}
