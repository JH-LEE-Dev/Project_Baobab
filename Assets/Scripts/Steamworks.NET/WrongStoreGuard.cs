using System.IO;
using UnityEngine;

#if UNITY_STANDALONE_WIN
using System;
using System.Runtime.InteropServices;
#endif

/// <summary>
/// Steam 빌드가 <b>다른 스토어의 런처</b>에서 실행되고 있는지 알아채고, 그렇다면 Steam 을 띄우는 대신
/// 사람이 읽을 수 있는 메시지를 보여 주고 종료합니다.
///
/// [왜 필요한가]
/// 2026-09-25, Steam 빌드가 실수로 STOVE 에 올라갔습니다. Steam 빌드는 시작하자마자
/// SteamAPI.RestartAppIfNecessary 를 부르고, Steam 밖에서 실행되면 이 함수가 <b>Steam 클라이언트를
/// 켜고 게임을 종료</b>합니다. STOVE 검수자가 본 것은 "다운로드 클릭 시 Steam 이 활성화됨"이었고,
/// 그 증상만으로는 원인을 짚기 어려웠습니다.
///
/// 업로드 전 검사(UploadPreflightCheck)가 1차 방어입니다. 이 클래스는 그것을 건너뛰고 잘못 올라간
/// 경우의 <b>마지막 방어</b>로, 사고를 없애지는 못하지만 증상을 "Steam 이 켜짐"에서
/// "잘못된 빌드라는 안내"로 바꿔 줍니다. 검수자도 개발자도 즉시 원인을 압니다.
///
/// [무엇으로 판별하는가]
/// STOVE 는 업로드된 빌드에 DRM Maker 를 적용하면서 exe 옆에 자기 DLL 을 넣습니다
/// (2026-09-09 무결성 검사 파일 목록에서 확인: BaseSDK.dll / DrmCheckerV3.dll / OwnershipSDK.dll).
/// 우리 빌드 산출물에는 이 파일들이 없으므로, 있다면 <b>STOVE 를 거쳐 설치된 것</b>입니다.
/// Steam 빌드 코드가 그 파일을 본다 = Steam 빌드가 STOVE 에 올라갔다.
///
/// [실패 방향]
/// STOVE 가 DLL 이름을 바꾸면 이 판별은 조용히 꺼지고, 게임은 지금처럼(Steam 을 켬) 동작합니다.
/// 즉 잘못 판단해서 정상 Steam 유저를 막는 방향으로는 실패하지 않습니다. 정상 Steam 설치에
/// 저 파일들이 있을 이유가 없습니다.
///
/// STOVE 빌드에서는 이 클래스를 부르는 SteamManager 코드 자체가 컴파일에서 빠집니다(DISABLESTEAMWORKS).
/// </summary>
public static class WrongStoreGuard
{
    /// <summary>STOVE DRM Maker 가 빌드에 주입하는 파일들입니다. 하나라도 있으면 STOVE 설치본입니다.</summary>
    private static readonly string[] STOVE_INJECTED_FILES =
    {
        "DrmCheckerV3.dll",
        "BaseSDK.dll",
        "OwnershipSDK.dll",
    };

    public static bool IsRunningInsideForeignStore()
    {
        string _exeDir = Path.GetDirectoryName(Application.dataPath);

        if (true == string.IsNullOrEmpty(_exeDir)) return false;

        for (int i = 0; i < STOVE_INJECTED_FILES.Length; i++)
        {
            if (true == File.Exists(Path.Combine(_exeDir, STOVE_INJECTED_FILES[i]))) return true;
        }

        return false;
    }

    /// <summary>
    /// 로그와 함께 네이티브 메시지 상자를 띄우고 종료합니다. Unity UI 는 아직 뜨기 전이라 쓸 수 없고,
    /// Debug.LogError 만으로는 검수자가 볼 수 없습니다.
    /// </summary>
    public static void ShowWrongBuildMessageAndQuit()
    {
        const string _message =
            "이 실행 파일은 Steam 전용 빌드입니다. STOVE 용 빌드가 아닌 파일이 업로드되었습니다.\n" +
            "개발팀에 알려 주세요: STOVE_DEMO 폴더의 빌드로 교체해야 합니다.\n\n" +
            "This executable is the Steam build. The wrong build was uploaded to this store.\n" +
            "Please report to the developer: the STOVE_DEMO build must be uploaded instead.";

        Debug.LogError("[WrongStoreGuard] " + _message.Replace("\n", " "));

#if UNITY_STANDALONE_WIN
        try
        {
            // MB_OK | MB_ICONERROR | MB_TOPMOST
            MessageBoxW(IntPtr.Zero, _message, "LumberBoy — 잘못된 빌드 / Wrong build", 0x00000000 | 0x00000010 | 0x00040000);
        }
        catch (Exception)
        {
            // 메시지 상자를 못 띄워도 종료는 한다. 로그는 남았다.
        }
#endif

        Application.Quit();
    }

#if UNITY_STANDALONE_WIN
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr _hWnd, string _text, string _caption, uint _type);
#endif
}
