using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 키 바인딩 오버라이드의 파일 영속성만 담당합니다.
/// SettingsRepository와 동일하게 평문 JSON, 임시 파일 교체 저장 방식을 씁니다.
/// </summary>
public static class KeyBindingRepository
{
    private const int CURRENT_VERSION = 1;
    private const string TEMP_SUFFIX = ".tmp";

    [Serializable]
    private class KeyBindingFileModel
    {
        public int version;
        public string overridesJson;
    }

    /// <summary>
    /// 저장된 바인딩 오버라이드 JSON(InputActionAsset.SaveBindingOverridesAsJson 결과)을 읽습니다.
    /// 파일이 없거나 손상되었으면 false를 반환하며, 이 경우 호출부는 기본 바인딩을 그대로 쓰면 됩니다.
    /// </summary>
    public static bool TryLoad(out string _overridesJson)
    {
        _overridesJson = null;

        try
        {
            string _path = GamePaths.KeyBindingsFile;
            if (false == File.Exists(_path)) return false;

            string _json = File.ReadAllText(_path);
            if (string.IsNullOrEmpty(_json)) return false;

            KeyBindingFileModel _model = JsonUtility.FromJson<KeyBindingFileModel>(_json);
            if (null == _model || string.IsNullOrEmpty(_model.overridesJson)) return false;

            if (CURRENT_VERSION != _model.version)
            {
                Debug.LogWarning($"[KeyBindingRepository] Version mismatch (file={_model.version}, current={CURRENT_VERSION}). Using defaults.");
                return false;
            }

            _overridesJson = _model.overridesJson;
            return true;
        }
        catch (Exception _e)
        {
            // 키 바인딩 파일 하나 때문에 게임이 부팅되지 않는 상황을 만들지 않는다.
            Debug.LogWarning($"[KeyBindingRepository] Load failed, using defaults: {_e.Message}");
            _overridesJson = null;
            return false;
        }
    }

    /// <summary>
    /// 바인딩 오버라이드 JSON을 저장합니다.
    /// 임시 파일에 먼저 쓰고 교체하므로, 저장 도중 강제 종료되어도 기존 파일이 손상되지 않습니다.
    /// </summary>
    public static void Save(string _overridesJson)
    {
        string _path = GamePaths.KeyBindingsFile;
        string _tempPath = _path + TEMP_SUFFIX;

        try
        {
            KeyBindingFileModel _model = new KeyBindingFileModel
            {
                version = CURRENT_VERSION,
                overridesJson = _overridesJson
            };

            File.WriteAllText(_tempPath, JsonUtility.ToJson(_model, true));

            if (true == File.Exists(_path))
            {
                File.Replace(_tempPath, _path, null);
            }
            else
            {
                File.Move(_tempPath, _path);
            }
        }
        catch (Exception _e)
        {
            Debug.LogError($"[KeyBindingRepository] Save failed: {_e.Message}");

            try
            {
                if (true == File.Exists(_tempPath)) File.Delete(_tempPath);
            }
            catch { /* 정리 실패는 무시 */ }
        }
    }
}
