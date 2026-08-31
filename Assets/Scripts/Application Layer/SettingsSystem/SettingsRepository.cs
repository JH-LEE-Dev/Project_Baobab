using System;
using System.IO;
using UnityEngine;

/// <summary>설정 파일 로드 결과입니다.</summary>
public enum ESettingsLoadResult
{
    /// <summary>파일이 없습니다. (첫 실행)</summary>
    NotFound,

    /// <summary>정상적으로 읽었습니다.</summary>
    Loaded,

    /// <summary>파일은 있으나 버전이 다르거나 손상되어 폐기했습니다. 기본값으로 덮어써 정리해야 합니다.</summary>
    Discarded
}

/// <summary>
/// 환경설정의 파일 영속성만 담당합니다.
/// 세이브 데이터와 달리 평문 JSON으로 저장합니다.
/// (치트 방지 대상이 아니고, 잘못된 해상도로 화면이 안 나올 때 유저가 직접 고칠 수 있어야 합니다)
/// </summary>
public static class SettingsRepository
{
    // v2: EResolution 맨 앞에 Res640x360이 추가되어 기존 인덱스가 한 칸씩 밀렸다.
    // v3: 비정수 배율(2.5x)인 Res1600x900을 제거해 그 뒤 인덱스가 한 칸씩 당겨졌다.
    //     구버전 파일은 Discarded로 폐기되어 기본값으로 정리된다. (출시 전이라 마이그레이션 불필요)
    private const int CURRENT_VERSION = 3;
    private const string TEMP_SUFFIX = ".tmp";

    /// <summary>
    /// hapticStrength 필드가 파일에 들어 있는지 확인할 때 찾는 JSON 키입니다.
    /// 필드 이름을 바꾸면 이 상수도 함께 바꿔야 합니다. (TryLoad의 보정 참고)
    /// </summary>
    private const string MIGRATION_KEY_HAPTIC_STRENGTH = "\"hapticStrength\"";

    /// <summary>
    /// virtualCursorSensitivity 필드가 파일에 들어 있는지 확인할 때 찾는 JSON 키입니다.
    /// 필드 이름을 바꾸면 이 상수도 함께 바꿔야 합니다. (TryLoad의 보정 참고)
    /// </summary>
    private const string MIGRATION_KEY_CURSOR_SENSITIVITY = "\"virtualCursorSensitivity\"";

    [Serializable]
    private class SettingsFileModel
    {
        public int version;
        public SettingsData data;
    }

    /// <summary>
    /// 저장된 설정을 읽습니다. 읽지 못한 경우 _result에는 기본값이 담기므로
    /// 호출부는 실패해도 그대로 진행하면 됩니다.
    /// 반환값으로 "파일이 아예 없었는지(NotFound)"와 "못 쓰는 파일이 남아 있는지(Discarded)"를
    /// 구분해, 후자는 호출부가 정리(덮어쓰기)할 수 있게 합니다.
    /// </summary>
    public static ESettingsLoadResult TryLoad(out SettingsData _result)
    {
        _result = SettingsData.CreateDefault();

        try
        {
            string _path = GamePaths.SettingsFile;
            if (false == File.Exists(_path)) return ESettingsLoadResult.NotFound;

            string _json = File.ReadAllText(_path);
            if (string.IsNullOrEmpty(_json)) return ESettingsLoadResult.Discarded;

            SettingsFileModel _model = JsonUtility.FromJson<SettingsFileModel>(_json);
            if (null == _model) return ESettingsLoadResult.Discarded;

            // 버전이 다르면 기본값을 쓴다. (마이그레이션이 필요해지면 여기서 분기)
            if (CURRENT_VERSION != _model.version)
            {
                Debug.LogWarning($"[SettingsRepository] Version mismatch (file={_model.version}, current={CURRENT_VERSION}). Using defaults.");
                return ESettingsLoadResult.Discarded;
            }

            _result = _model.data;

            // 버전을 올리지 않고 추가된 필드의 보정.
            //
            // JsonUtility는 JSON에 없는 키를 default(0)로 채우는데, hapticStrength는 0이
            // "진동 끔"이라는 유효한 값이라 "없음"과 구분되지 않는다. 그대로 두면 이 필드가
            // 생기기 전에 만들어진 설정 파일을 쓰는 유저는 진동이 조용히 꺼진 채로 시작한다.
            // (gamepadIconPreference는 0이 곧 기본값 Auto라 이런 보정이 필요 없다)
            //
            // 버전을 올려 해결하지 않는 이유: 그러면 파일이 통째로 폐기되어 유저가 맞춰둔
            // 해상도·볼륨까지 전부 날아간다.
            if (false == _json.Contains(MIGRATION_KEY_HAPTIC_STRENGTH))
            {
                _result.hapticStrength = SettingsData.SLIDER_MAX;
            }

            // 커서 감도도 같은 문제를 가진다. 기본값이 가운데(50)라 키가 없는 파일을 그대로 쓰면
            // 0으로 읽혀 커서가 최저 감도로 시작한다.
            if (false == _json.Contains(MIGRATION_KEY_CURSOR_SENSITIVITY))
            {
                _result.virtualCursorSensitivity = SettingsData.SLIDER_CENTER_DEFAULT;
            }

            return ESettingsLoadResult.Loaded;
        }
        catch (Exception _e)
        {
            // 설정 파일 하나 때문에 게임이 부팅되지 않는 상황을 만들지 않는다.
            Debug.LogWarning(GamePaths.Redact($"[SettingsRepository] Load failed, using defaults: {_e.Message}"));
            _result = SettingsData.CreateDefault();
            return ESettingsLoadResult.Discarded;
        }
    }

    /// <summary>
    /// 설정을 저장합니다. 임시 파일에 먼저 쓰고 교체하므로,
    /// 저장 도중 강제 종료되어도 기존 파일이 손상되지 않습니다.
    /// </summary>
    public static void Save(in SettingsData _data)
    {
        string _path = GamePaths.SettingsFile;
        string _tempPath = _path + TEMP_SUFFIX;

        try
        {
            SettingsFileModel _model = new SettingsFileModel
            {
                version = CURRENT_VERSION,
                data = _data
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
            Debug.LogError(GamePaths.Redact($"[SettingsRepository] Save failed: {_e.Message}"));

            // 교체에 실패한 임시 파일은 남겨두지 않는다.
            try
            {
                if (true == File.Exists(_tempPath)) File.Delete(_tempPath);
            }
            catch { /* 정리 실패는 무시 */ }
        }
    }
}
