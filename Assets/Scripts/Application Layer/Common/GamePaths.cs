using System;
using System.IO;
using System.Text;

/// <summary>
/// 게임이 사용하는 영구 저장 경로를 한곳에서 정의합니다.
/// 세이브 데이터와 환경설정은 같은 폴더를 쓰되 파일은 반드시 분리합니다.
/// (환경설정 때문에 세이브 파일이 생기면 HasSaveData 판정이 깨집니다)
/// </summary>
public static class GamePaths
{
    private const string FOLDER_NAME = "LumberBoy";
    private const string GAME_SAVE_FILE_NAME = "SaveData.dat";
    private const string GAME_SAVE_BACKUP_FILE_NAME = "SaveData.dat.bak";
    private const string GAME_SAVE_TEMP_FILE_NAME = "SaveData.dat.tmp";
    private const string GAME_SAVE_FOREIGN_BACKUP_FILE_NAME = "SaveData.other-build.bak";
    private const string GAME_SAVE_CLOUD_TOMBSTONE_FILE_NAME = "SaveData.cloud-deleted";
    private const string SETTINGS_FILE_NAME = "Settings.json";
    private const string KEY_BINDINGS_FILE_NAME = "KeyBindings.json";

    /// <summary>Redact가 사용자 계정 폴더 경로를 대체할 때 쓰는 표식입니다.</summary>
    private const string USER_PROFILE_TOKEN = "<user>";

    private static string cachedFolder;

    /// <summary>저장 폴더 경로입니다. 접근 시 폴더가 없으면 생성합니다.</summary>
    public static string SaveFolder
    {
        get
        {
            if (string.IsNullOrEmpty(cachedFolder))
            {
                cachedFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), FOLDER_NAME);
            }

            // 유저가 실행 중에 폴더를 지울 수도 있으므로 매번 확인한다.
            if (false == Directory.Exists(cachedFolder))
            {
                Directory.CreateDirectory(cachedFolder);
            }

            return cachedFolder;
        }
    }

    /// <summary>플레이 진행 상황 (암호화 바이너리)</summary>
    public static string GameSaveFile => Path.Combine(SaveFolder, GAME_SAVE_FILE_NAME);

    /// <summary>직전 저장본 백업 (GameSaveFile을 원자적으로 교체할 때 File.Replace가 자동으로 채움). 반드시 SaveFolder 기준이어야 File.Replace의 "동일 볼륨" 조건이 항상 성립한다.</summary>
    public static string GameSaveBackupFile => Path.Combine(SaveFolder, GAME_SAVE_BACKUP_FILE_NAME);

    /// <summary>원자적 쓰기용 임시 파일. GameSaveFile과 반드시 같은 폴더(SaveFolder)에 있어야 한다.</summary>
    public static string GameSaveTempFile => Path.Combine(SaveFolder, GAME_SAVE_TEMP_FILE_NAME);

    /// <summary>
    /// 다른 빌드 변형의 세이브를 덮어쓰기 직전에 보존해두는 파일. (데모 빌드가 정식 세이브를 만났을 때만 사용)
    /// 게임이 자동으로 다시 읽지는 않으며, 사고 시 수동 복구용이다.
    /// </summary>
    public static string GameSaveForeignBackupFile => Path.Combine(SaveFolder, GAME_SAVE_FOREIGN_BACKUP_FILE_NAME);

    /// <summary>
    /// "새로하기"로 세이브를 지웠지만 그 시점에 스팀 클라우드까지 지우지 못했을 때(오프라인 등) 남기는 표식.
    /// 내용은 삭제 시각(UTC ticks) 한 줄이다. 다음 실행에서 SyncCloudSaveIfNewer가 이 표식을 보고
    /// 잔존 클라우드 세이브를 복원하는 대신 지운다. 클라우드 정리나 새 저장이 성공하면 제거된다.
    /// </summary>
    public static string GameSaveCloudTombstoneFile => Path.Combine(SaveFolder, GAME_SAVE_CLOUD_TOMBSTONE_FILE_NAME);

    /// <summary>환경설정 (평문 JSON)</summary>
    public static string SettingsFile => Path.Combine(SaveFolder, SETTINGS_FILE_NAME);

    /// <summary>키 바인딩 오버라이드 (평문 JSON)</summary>
    public static string KeyBindingsFile => Path.Combine(SaveFolder, KEY_BINDINGS_FILE_NAME);

    /// <summary>
    /// 로그에 실을 수 있도록 문자열에서 사용자 계정 폴더 경로를 지웁니다.
    ///
    /// 저장 경로는 문서 폴더 아래라 항상 C:\Users\{계정명}\... 형태이고, 계정명을 실명으로 쓰는
    /// 사람이 적지 않습니다. 이 문자열이 Debug.Log로 나가면 Sentry가 브레드크럼으로 주워 크래시
    /// 리포트에 함께 올려버립니다.
    ///
    /// Sentry의 SendDefaultPii 설정으로는 막을 수 없습니다. 그 옵션은 SDK가 자동 수집하는 항목(IP,
    /// 계정명)에만 적용되고, 게임이 직접 만들어 찍은 로그 내용은 그대로 통과시킵니다. 그래서
    /// 로그를 만드는 쪽에서 미리 지워야 합니다.
    ///
    /// 예외 메시지에도 전체 경로가 들어오므로(File 계열이 경로를 그대로 넣습니다), 경로 변수만이
    /// 아니라 완성된 로그 문장 전체를 통과시키는 것을 전제로 만들었습니다.
    /// </summary>
    public static string Redact(string _text)
    {
        if (true == string.IsNullOrEmpty(_text)) return _text;

        // 계정 폴더 하나만 지우면 문서 폴더, OneDrive 리디렉션, 임시 폴더가 전부 그 아래라 함께 처리된다.
        string _userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (true == string.IsNullOrEmpty(_userProfile)) return _text;

        return ReplaceIgnoreCase(_text, _userProfile, USER_PROFILE_TOKEN);
    }

    /// <summary>
    /// 대소문자를 무시하고 치환합니다. 윈도우 경로는 같은 폴더라도 대소문자가 다르게 적힐 수 있어
    /// string.Replace로는 놓치는 경우가 생깁니다.
    /// </summary>
    private static string ReplaceIgnoreCase(string _text, string _from, string _to)
    {
        StringBuilder _sb = null;
        int _start = 0;

        while (true)
        {
            int _index = _text.IndexOf(_from, _start, StringComparison.OrdinalIgnoreCase);

            if (0 > _index) break;

            if (null == _sb) _sb = new StringBuilder(_text.Length);

            _sb.Append(_text, _start, _index - _start);
            _sb.Append(_to);

            _start = _index + _from.Length;
        }

        if (null == _sb) return _text;

        _sb.Append(_text, _start, _text.Length - _start);

        return _sb.ToString();
    }
}
