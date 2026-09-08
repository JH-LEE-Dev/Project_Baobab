using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// 게임이 사용하는 영구 저장 경로를 한곳에서 정의합니다.
/// 세이브 데이터와 환경설정은 같은 폴더를 쓰되 파일은 반드시 분리합니다.
/// (환경설정 때문에 세이브 파일이 생기면 HasSaveData 판정이 깨집니다)
/// </summary>
public static class GamePaths
{
    /// <summary>
    /// 세이브 폴더 이름입니다. 플랫폼마다 다릅니다.
    ///
    /// [왜 나누는가]
    /// STOVE 클라우드는 SDK 호출이 아니라 런처가 지정된 폴더를 통째로 동기화하는 방식입니다.
    /// 폴더를 공유하면 한 로컬 파일을 서로 모르는 두 동기화 시스템(STOVE 런처 / Steam)이 각자
    /// 백업하고 각자 복원하게 되어, 양쪽을 다 산 유저가 기기를 옮길 때 한쪽의 오래된 사본이
    /// 다른 쪽 진행도를 덮을 수 있습니다. 나누면 서로를 아예 보지 못합니다.
    ///
    /// itch는 클라우드가 아예 없어 STOVE와 이유가 다릅니다. 위험은 한 방향으로만 남습니다 -
    /// 폴더를 공유하면, Steam 데모를 깔아 둔 기기에서 itch 빌드가 만든 세이브를 <b>Steam
    /// 클라우드가 자기 것으로 올려갑니다.</b> itch 쪽은 그런 일이 벌어지는지조차 모릅니다.
    /// 두 데모를 다 받은 사람이 진행도를 이어받지 못하는 것이 그 대가인데, 통제하지 못하는
    /// 동기화가 세이브를 덮는 쪽이 훨씬 비쌉니다.
    ///
    /// itch 데모와 itch 정식은 이 폴더를 함께 씁니다. 스토어가 폴더를 가르고 변형이 파일 안을
    /// 가르는 구조 그대로이며, Steam에서 이미 그렇게 돌고 있습니다.
    ///
    /// 세이브 변형(SaveBuildVariant)은 건드리지 않습니다. 폴더가 이미 갈라져 있어 서로 만날 일이
    /// 없고, 출시 후에 enum에 값을 더하면 배포된 구버전 빌드가 그 값을 "모르는 미래 값"으로 보고
    /// 세이브를 덮어씁니다. 데모/정식 구분은 지금처럼 변형이 계속 담당합니다.
    ///
    /// [극성 주의 - 디파인이 없는 쪽이 Steam입니다]
    /// 루트는 두 플랫폼이 공유하므로(ResolveRootFolder) 갈리는 것은 이 폴더 이름 하나뿐입니다.
    /// 반대로 두면 Steam 빌드에서 디파인을 깜빡하는 순간 세이브 폴더가 통째로 바뀌어 게임이 새 설치처럼
    /// 보이고, 출시 후에는 그 실수를 되돌릴 수 없습니다.
    /// 지금 방향에서는 STOVE 빌드에서 깜빡했을 때 런처가 빈 폴더를 동기화할 뿐이고,
    /// 로컬 플레이는 멀쩡합니다. 실수의 대가가 훨씬 싼 쪽입니다.
    ///
    /// 다만 SteamManager의 RestartAppIfNecessary는 안전한 방향이 정반대입니다(STOVE에서 깜빡하면
    /// 유저가 게임을 못 켭니다). 둘이 같은 디파인에 매달리므로 "STOVE인데 디파인 없음"은 전체적으로
    /// 치명적이며, 사람 기억이 아니라 빌드 전 정합성 검사로 막아야 합니다.
    ///
    /// [STOVE Studio와 반드시 같아야 합니다]
    /// 파트너 사이트의 클라우드 세이빙 설정에 ($APPDATA_LOCAL) + 아래 문자열을 그대로 넣습니다.
    /// 한 글자라도 어긋나면 런처가 엉뚱한(빈) 폴더를 동기화하며, 에러는 나지 않습니다.
    /// 매크로도 ResolveRootFolder가 고르는 루트와 반드시 같은 곳을 가리켜야 합니다.
    /// </summary>
#if BAOBAB_STOVE
    private const string FOLDER_NAME = "LumberBoy_STOVE";
#elif BAOBAB_ITCH
    private const string FOLDER_NAME = "LumberBoy_ITCH";
#else
    private const string FOLDER_NAME = "LumberBoy";
#endif
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

    /// <summary>폴더 생성 실패 로그를 상태가 바뀔 때만 찍기 위한 플래그입니다. (SaveFolder는 매우 자주 불립니다)</summary>
    private static bool bReportedCreateFailure;

    /// <summary>저장 폴더 경로입니다. 접근 시 폴더가 없으면 생성합니다.</summary>
    public static string SaveFolder
    {
        get
        {
            if (string.IsNullOrEmpty(cachedFolder))
            {
                cachedFolder = Path.Combine(ResolveRootFolder(), FOLDER_NAME);
            }

            // 유저가 실행 중에 폴더를 지울 수도 있으므로 매번 확인한다.
            if (false == Directory.Exists(cachedFolder))
            {
                try
                {
                    Directory.CreateDirectory(cachedFolder);
                    bReportedCreateFailure = false;
                }
                catch (Exception _e)
                {
                    // 여기서 예외를 그대로 흘리면 이 프로퍼티를 읽기만 해도 터진다. HasSaveData처럼
                    // try 밖에서 부르는 곳이 있어 메인 메뉴가 통째로 깨질 수 있다. 경로는 그대로
                    // 돌려주고, 실제 실패는 파일을 만지는 각 호출부가 자기 문맥으로 처리하게 둔다.
                    if (false == bReportedCreateFailure)
                    {
                        bReportedCreateFailure = true;
                        Debug.LogError(Redact($"[GamePaths] Failed to create save folder: {_e.Message}"));
                    }
                }
            }

            return cachedFolder;
        }
    }

    /// <summary>
    /// 저장 폴더가 들어갈 루트를 고릅니다. 두 플랫폼 모두 AppData\Local을 씁니다.
    ///
    /// [문서 폴더를 쓰지 않는 이유]
    /// OneDrive의 "알려진 폴더 이동"은 바탕화면/문서/사진을 OneDrive 안으로 리디렉션합니다. 켜져 있으면
    /// 세이브 파일이 동기화 폴더에 놓이고, 파일 온디맨드가 이를 "온라인 전용"으로 탈수화할 수 있습니다.
    /// 그 상태에서 오프라인이면 읽기가 실패하고, 동기화 중에는 파일이 잠깁니다. 진행도를 담은 파일이
    /// 우리가 통제하지 못하는 동기화 시스템 위에 놓이는 셈입니다.
    /// AppData\Local은 알려진 폴더 이동의 대상이 아니고, 기업용 폴더 리디렉션도 Roaming만 옮기고
    /// Local은 건드리지 않습니다. 그래서 여기를 씁니다.
    ///
    /// 대신 숨김 폴더라 유저가 직접 백업하거나 서포트에 파일을 보내기가 번거롭습니다.
    /// 안내할 때는 탐색기 주소창에 %LOCALAPPDATA%를 붙여넣게 하면 됩니다.
    ///
    /// [STOVE Studio의 $APPDATA_LOCAL과 같은 곳이어야 합니다]
    /// 런처는 파트너 사이트에 적힌 경로를 동기화할 뿐, 게임이 실제로 어디에 쓰는지 모릅니다.
    /// 둘이 어긋나면 런처가 빈 폴더를 동기화하고 에러는 나지 않습니다. 이 함수를 고칠 때는
    /// 파트너 사이트 설정도 반드시 함께 고쳐야 합니다.
    /// AppData\Local과 AppData\LocalLow는 다른 폴더입니다. Unity의 persistentDataPath는 후자이므로,
    /// 아래 폴백을 타는 순간에는 런처와 경로가 어긋납니다. 그래서 폴백은 최후의 수단이며 로그를 남깁니다.
    /// </summary>
    private static string ResolveRootFolder()
    {
        string _root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // 빈 문자열이 오면 Path.Combine이 상대 경로를 만들어 실행 파일 옆에 저장해버린다.
        // 설치 폴더는 쓰기 권한이 없을 수 있고, 게임을 지우면 세이브도 함께 사라진다.
        if (true == string.IsNullOrEmpty(_root))
        {
            Debug.LogError("[GamePaths] Could not resolve the OS save root; falling back to persistentDataPath. Cloud saving will not see this location.");
            return Application.persistentDataPath;
        }

        return _root;
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
    /// 저장 경로는 AppData 아래라 항상 C:\Users\{계정명}\... 형태이고, 계정명을 실명으로 쓰는
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

        // 계정 폴더 하나만 지우면 AppData, 문서 폴더, OneDrive 리디렉션, 임시 폴더가 전부 그 아래라 함께 처리된다.
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
