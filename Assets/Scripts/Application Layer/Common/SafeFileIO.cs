using System;
using System.IO;
using System.Threading;

/// <summary>
/// 세이브/설정 파일 조작을 짧은 재시도로 감싸는 공용 헬퍼입니다.
///
/// [왜 필요한가]
/// 우리 파일을 만지는 프로세스가 우리만이 아닙니다. 백신 실시간 검사, 백업 도구, STOVE 런처의
/// 클라우드 동기화가 같은 파일을 잠깐씩 잡습니다. 그 순간의 실패는 "파일이 망가졌다"가 아니라
/// "지금은 안 된다"이며, 수백 밀리초 뒤에는 대개 풀립니다.
///
/// [UnauthorizedAccessException도 재시도하는 이유]
/// 권한 문제는 재시도해도 결과가 같다는 게 상식이지만, 백신 실시간 검사는 공유 위반(IOException)뿐
/// 아니라 액세스 거부도 냅니다. 진짜 권한 문제라면 재시도 비용은 아래 상수만큼의 지연 한 번뿐이고,
/// 반대로 재시도하지 않아 놓치는 쪽의 대가는 세이브 손실입니다. 값이 훨씬 싼 쪽을 택했습니다.
///
/// [호출부가 예외를 돌려받는 이유]
/// 로그 문구는 호출부마다 달라야 의미가 있어서(어느 시스템의 어느 단계인지) 여기서는 찍지 않고
/// 마지막 예외를 그대로 넘깁니다. 호출부는 반드시 GamePaths.Redact를 통과시켜 찍어야 합니다.
/// </summary>
public static class SafeFileIO
{
    /// <summary>기본 재시도 횟수입니다. 첫 시도를 포함합니다.</summary>
    public const int MAX_ATTEMPTS_DEFAULT = 5;

    /// <summary>재시도 간 대기입니다. 최악의 경우 (MAX_ATTEMPTS_DEFAULT - 1) * 이 값만큼 멈춥니다.</summary>
    private const int RETRY_DELAY_MS = 120;

    /// <summary>
    /// 잠깐 뒤 풀릴 수 있는 실패인지 판정합니다.
    /// 이 판정이 false면 재시도해도 소용없는 문제(경로가 없음, 인자가 틀림 등)입니다.
    /// </summary>
    public static bool IsTransient(Exception _e)
    {
        // 대상이 없는 것은 기다린다고 풀릴 문제가 아니다.
        // 둘 다 IOException의 하위 형식이라 아래 판정에 걸리므로 여기서 먼저 걸러낸다.
        if (_e is FileNotFoundException || _e is DirectoryNotFoundException) return false;

        return _e is IOException || _e is UnauthorizedAccessException;
    }

    /// <summary>
    /// 재시도 없이 딱 한 번만 시도할 때 넘기는 값입니다.
    ///
    /// 대기를 호출부가 직접 쥐어야 하는 경우에 씁니다. 대표적으로 메인 메뉴의 세이브 확인은
    /// 코루틴이 프레임을 넘기며 기다려야 "확인 중" 화면이 실제로 그려지므로, 이 안에서
    /// Thread.Sleep으로 멈추면 안 됩니다.
    /// </summary>
    public const int SINGLE_ATTEMPT = 1;

    /// <summary>파일을 통째로 읽습니다. 실패 시 _error에 마지막 예외가 담깁니다.</summary>
    public static bool TryReadAllBytes(string _path, out byte[] _bytes, out Exception _error, int _maxAttempts = MAX_ATTEMPTS_DEFAULT)
    {
        byte[] _read = null;
        bool _ok = Run(() => { _read = File.ReadAllBytes(_path); }, out _error, _maxAttempts);

        _bytes = _ok ? _read : null;
        return _ok;
    }

    /// <summary>텍스트 파일을 통째로 읽습니다. 실패 시 _error에 마지막 예외가 담깁니다.</summary>
    public static bool TryReadAllText(string _path, out string _text, out Exception _error)
    {
        string _read = null;
        bool _ok = Run(() => { _read = File.ReadAllText(_path); }, out _error);

        _text = _ok ? _read : null;
        return _ok;
    }

    /// <summary>파일을 통째로 씁니다.</summary>
    public static bool TryWriteAllBytes(string _path, byte[] _bytes, out Exception _error)
    {
        return Run(() => File.WriteAllBytes(_path, _bytes), out _error);
    }

    /// <summary>텍스트 파일을 통째로 씁니다.</summary>
    public static bool TryWriteAllText(string _path, string _text, out Exception _error)
    {
        return Run(() => File.WriteAllText(_path, _text), out _error);
    }

    /// <summary>파일을 지웁니다. 대상이 없으면 File.Delete가 그냥 통과하므로 true가 됩니다.</summary>
    public static bool TryDelete(string _path, out Exception _error)
    {
        return Run(() => File.Delete(_path), out _error);
    }

    /// <summary>
    /// 임시 파일을 대상 자리로 원자적으로 옮깁니다.
    ///
    /// 대상이 이미 있으면 File.Replace가 교체와 동시에 기존 파일을 _backupPath로 밀어 넣습니다.
    /// 대상이 없으면 백업할 것도 없으므로 File.Move로 옮기며, 이때 기존 백업은 건드리지 않습니다.
    /// (_backupPath에 null을 주면 백업을 남기지 않습니다)
    ///
    /// 세 경로는 반드시 같은 폴더여야 합니다. File.Replace가 동일 볼륨을 요구하고,
    /// 같은 폴더면 그 조건이 언제나 성립하기 때문입니다.
    /// </summary>
    public static bool TryReplaceOrMove(string _tempPath, string _path, string _backupPath, out Exception _error)
    {
        return Run(() =>
        {
            if (true == File.Exists(_path))
            {
                File.Replace(_tempPath, _path, _backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(_tempPath, _path);
            }
        }, out _error);
    }

    /// <summary>
    /// 교체에 실패해 남은 임시 파일을 조용히 치웁니다.
    /// 정리 자체가 실패해도 알릴 것이 없습니다(다음 저장이 어차피 덮어씁니다).
    /// </summary>
    public static void CleanUpTempFile(string _tempPath)
    {
        try
        {
            if (true == File.Exists(_tempPath)) File.Delete(_tempPath);
        }
        catch { /* 정리 실패는 무시 */ }
    }

    /// <summary>
    /// 일시적 실패에 한해 재시도합니다.
    /// 대리자 할당이 한 번 생기지만, 이 경로는 저장/로드처럼 드물게만 도는 데다
    /// 바로 앞 단계의 JSON 직렬화가 비교도 안 되게 많이 할당합니다.
    /// </summary>
    private static bool Run(Action _op, out Exception _error, int _maxAttempts = MAX_ATTEMPTS_DEFAULT)
    {
        _error = null;

        if (1 > _maxAttempts) _maxAttempts = 1;

        for (int _attempt = 1; _attempt <= _maxAttempts; _attempt++)
        {
            try
            {
                _op();
                return true;
            }
            catch (Exception _e)
            {
                _error = _e;

                // 재시도해도 같은 결과인 실패는 여기서 끝낸다.
                if (false == IsTransient(_e)) return false;

                if (_attempt >= _maxAttempts) return false;

                Thread.Sleep(RETRY_DELAY_MS);
            }
        }

        return false;
    }
}
