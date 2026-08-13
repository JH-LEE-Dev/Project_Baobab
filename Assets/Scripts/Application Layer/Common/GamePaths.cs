using System;
using System.IO;

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
    private const string SETTINGS_FILE_NAME = "Settings.json";
    private const string KEY_BINDINGS_FILE_NAME = "KeyBindings.json";

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

    /// <summary>환경설정 (평문 JSON)</summary>
    public static string SettingsFile => Path.Combine(SaveFolder, SETTINGS_FILE_NAME);

    /// <summary>키 바인딩 오버라이드 (평문 JSON)</summary>
    public static string KeyBindingsFile => Path.Combine(SaveFolder, KEY_BINDINGS_FILE_NAME);
}
