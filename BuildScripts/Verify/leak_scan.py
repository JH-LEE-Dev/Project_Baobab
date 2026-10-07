# -*- coding: utf-8 -*-
"""
배포 빌드 유출 검사 (Docs/BuildLeakReview.md 의 자동 검사 부분)

빌드 폴더만 보고 다음을 검사합니다.
  1. 소스·내부 파일 유출 : .cs/.pdb/프로젝트 파일, IL2CPP 백업 폴더, Mono 어셈블리, 업로드 스크립트, 설정 파일
  2. 비밀값 유출         : Sentry 토큰, 개인 키, sentry.properties, steam_appid.txt
  3. 개발자 개인정보     : git 작성자 이름·메일, 이 PC 계정명, 사용자 폴더 경로 (저장소에는 적어 두지 않고 실행 때 모읍니다)
  4. 디버그·치트 기능    : global-metadata.dat 의 디버그 이름 (배포 0건 / 촬영용 예외)
  5. 스토어 섞임         : Steam 빌드와 그 외 빌드의 Steam 흔적
  6. 빌드 스탬프         : DEVELOPMENT=false, GIT_DIRTY=false, PURPOSE

사용법 (프로젝트 루트에서):
  python BuildScripts/Verify/leak_scan.py "C:/Unity Build/STEAM_DEMO"

data.unity3d 안쪽(그림·소리·데이터)은 압축돼 있어 문자열로 볼 수 없습니다. 그쪽은
extract_check.py(UnityPy) 로 따로 확인합니다.

종료 코드: 실패가 있으면 1, 없으면 0.
"""
import os
import re
import subprocess
import sys

PROJECT_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))

# ---------------------------------------------------------------------------
# 1. 빌드에 있으면 안 되는 파일 (이름 규칙). 소스·중간 산출물·내부 도구 파일입니다.
# ---------------------------------------------------------------------------
FORBIDDEN_FILE_PATTERNS = [
    (r"\.cs$", "C# 소스"),
    (r"\.pdb$", "디버그 심볼(PDB) - 함수 이름·소스 경로가 들어 있음"),
    (r"\.(csproj|sln|slnx|asmdef|asmref)$", "프로젝트 파일"),
    (r"\.(unity|prefab|meta|mat|controller|anim)$", "Unity 원본 에셋 파일"),
    (r"_BackUpThisFolder_ButDontShipItWithYourGame", "IL2CPP 백업 폴더 - 변환된 C++ 소스 전체"),
    (r"_BurstDebugInformation_DoNotShip", "Burst 디버그 정보"),
    (r"(^|/)Managed/Assembly-CSharp(-firstpass)?\.dll$", "Mono 어셈블리 - 원본 C# 으로 그대로 디컴파일됨(IL2CPP 여야 함)"),
    (r"(^|/)sentry\.properties$", "Sentry 업로드 설정(토큰이 들어갈 수 있음)"),
    (r"(^|/)steam_appid\.txt$", "개발용 Steam 앱 ID 파일 - 있으면 Steam 실행 확인을 건너뜀"),
    (r"\.vdf$", "SteamPipe 업로드 스크립트"),
    (r"(^|/)\.git", "git 데이터"),
    (r"(^|/)\.env", "환경 변수 파일"),
    (r"PerformanceTestRunInfo", "Performance Testing 산출물(빌드 PC 이름·사양)"),
    (r"(^|/)(Player|Editor)\.log$", "로그 파일"),
    (r"\.(md|psd|blend|fbx|aseprite|xlsx|docx)$", "작업 문서·원본 리소스"),
]

# 빌드 폴더에 있어도 되는 json/txt
ALLOWED_TEXT_FILES = {"BUILD_STAMP.txt", "RuntimeInitializeOnLoads.json", "ScriptingAssemblies.json", "boot.config", "app.info",
                      "Steamworks.NET.txt"}

# ---------------------------------------------------------------------------
# 2. 비밀값 문자열 (정규식). 형식 문자열("-----BEGIN CERTIFICATE-----" 를 파싱하는 코드 등)은 걸리지 않게
#    실제 값의 모양까지 요구합니다.
# ---------------------------------------------------------------------------
SECRET_PATTERNS = [
    (rb"sntrys_[A-Za-z0-9+/=_-]{20,}", "Sentry 조직 토큰"),
    (rb"sntryu_[A-Za-z0-9]{20,}", "Sentry 사용자 토큰"),
    (rb"SENTRY_AUTH_TOKEN\s*=\s*\S{10,}", "Sentry 토큰 값"),
    (rb"-----BEGIN [A-Z ]*PRIVATE KEY-----\s*[A-Za-z0-9+/]{40}", "개인 키 본문"),
    (rb"ssh-(rsa|ed25519) AAAA[A-Za-z0-9+/]{40}", "SSH 키"),
    (rb"\+login [A-Za-z0-9_]+ \S", "steamcmd 로그인 명령"),
]

# 외부 SDK 빌드 서버의 계정 폴더입니다(우리 개발자가 아님). 발견되면 경고로만 남깁니다.
THIRD_PARTY_BUILD_ACCOUNTS = {"runneradmin", "buildworker", "containeradministrator", "vssadministrator", "builder"}

# 코드에 흔히 나오는 단어와 겹치는 git 작성자 이름입니다(예: 'VOID' ↔ VT_VOID). 이름 대신 메일 아이디로 검사합니다.
COMMON_WORD_IDS = {"void", "null", "test", "build", "admin", "unity", "debug", "main", "user", "dev"}

# 경로 검사에서 우리가 만든 파일만 봅니다. 엔진·SDK 바이너리 안의 경로는 그 회사 빌드 서버 경로입니다.
OWN_BINARIES = ("GameAssembly.dll", "global-metadata.dat")

# ---------------------------------------------------------------------------
# 4. 배포 빌드에서 0건이어야 하는 디버그·테스트 이름 (DebugSwitchBuildGuard 와 짝)
#    TRAILER_ONLY 는 촬영용 빌드에서만 1건 이상이 정상입니다.
# ---------------------------------------------------------------------------
DEBUG_NAMES = [
    "debugUnlockAllMaps", "debugForceUnlockAll", "ForceUnlockAllMapsForDebug", "enableDebugSound", "debugSoundId",
    "debugUseManualLighting", "useDebugValues", "bDebug", "isDebug", "enableLJDebugLog", "showOnScreenDebugGui",
    "debugIncreaseAmount", "debugEffectExperience", "showScreenTestButton", "testNodes", "skipIntroInEditor",
    "bForceShowInitialSetupPopup", "TryApplySkillWithoutCost", "TestFire", "DebugPlayPrestigeLevelUpWave",
    "TestFillMotion", "showFPS", "showDebug", "BenchmarkHarness", "showDebugOverlay",
]
TRAILER_ONLY = {"debugForceUnlockAll", "ForceUnlockAllMapsForDebug"}
# 검사가 제대로 도는지 보는 대조군: 배포 빌드에도 반드시 있는 이름
CONTROL_NAMES = ["isTestMode", "isTempScene", "enableTutorial"]


class Report:
    def __init__(self):
        self.fails = []
        self.warns = []
        self.oks = []

    def fail(self, _msg): self.fails.append(_msg)
    def warn(self, _msg): self.warns.append(_msg)
    def ok(self, _msg): self.oks.append(_msg)


def read_stamp(_root):
    _path = os.path.join(_root, "BUILD_STAMP.txt")
    _stamp = {}
    if os.path.isfile(_path):
        for _line in open(_path, encoding="utf-8", errors="replace"):
            if "=" in _line and not _line.startswith("#"):
                _k, _v = _line.rstrip("\n").split("=", 1)
                _stamp[_k.strip()] = _v.strip()
    return _stamp


def developer_identifiers():
    """git 작성자 이름·메일과 이 PC 계정명을 모읍니다. 저장소 파일에 개인정보를 적지 않기 위해 실행 때 만듭니다."""
    _ids = set()
    try:
        _out = subprocess.run(["git", "-C", PROJECT_ROOT, "log", "--all", "--format=%an%n%ae%n%cn%n%ce"],
                              capture_output=True, text=True, encoding="utf-8", errors="replace").stdout
        for _line in _out.splitlines():
            _line = _line.strip()
            if not _line:
                continue
            _ids.add(_line)
            if "@" in _line:
                _local = _line.split("@")[0]
                # noreply 메일의 숫자+아이디 형태(12345+name@users.noreply.github.com)는 아이디만 뽑는다.
                _ids.add(_local.split("+")[-1])
    except Exception as _e:
        print("  (git 작성자 목록을 읽지 못했습니다: %s)" % _e)

    for _env in ("USERNAME", "USER"):
        if os.environ.get(_env):
            _ids.add(os.environ[_env])
    _profile = os.environ.get("USERPROFILE") or os.path.expanduser("~")
    if _profile:
        _ids.add(os.path.basename(_profile))

    # 너무 짧거나 흔한 단어는 오탐만 낸다.
    _common = {"root", "admin", "user", "unity", "noreply", "users", "github", "default", "public"} | COMMON_WORD_IDS
    return sorted(_i for _i in _ids if len(_i) >= 4 and _i.lower() not in _common)


def iter_files(_root):
    for _dp, _dn, _fn in os.walk(_root):
        for _f in _fn:
            _full = os.path.join(_dp, _f)
            yield _full, os.path.relpath(_full, _root).replace("\\", "/")


def check_files(_root, _report):
    _bad = 0
    for _full, _rel in iter_files(_root):
        for _pat, _why in FORBIDDEN_FILE_PATTERNS:
            if re.search(_pat, _rel, re.I):
                _report.fail("[파일] %s  - %s" % (_rel, _why))
                _bad += 1
                break
        else:
            _name = os.path.basename(_rel)
            if re.search(r"\.(txt|json|xml|ini|cfg|yaml|yml|csv)$", _name, re.I) and _name not in ALLOWED_TEXT_FILES:
                _report.warn("[파일] 예상하지 못한 텍스트 파일: %s  (내용 확인 필요)" % _rel)
    for _dp, _dn, _fn in os.walk(_root):
        for _d in _dn:
            _rel = os.path.relpath(os.path.join(_dp, _d), _root).replace("\\", "/")
            for _pat, _why in FORBIDDEN_FILE_PATTERNS:
                if re.search(_pat, _rel, re.I):
                    _report.fail("[폴더] %s  - %s" % (_rel, _why))
                    _bad += 1
                    break
    if 0 == _bad:
        _report.ok("소스·내부 파일 없음 (.cs/.pdb/프로젝트 파일/IL2CPP 백업/Mono 어셈블리/설정 파일)")

    if os.path.isdir(os.path.join(_root, "LumberBoy_Data", "il2cpp_data")):
        _report.ok("IL2CPP 빌드 (C# 원본으로 디컴파일되지 않음)")
    else:
        _report.fail("[소스] il2cpp_data 가 없습니다. Mono 빌드면 C# 코드가 그대로 복원됩니다")


def scan_strings(_root, _report, _developer_ids):
    """모든 파일을 바이트로 읽어 비밀값·개발자 정보·절대 경로를 찾습니다 (ASCII 와 UTF-16 모두)."""
    _id_patterns = []
    for _i in _developer_ids:
        # 앞뒤가 영숫자·밑줄이면 다른 단어의 일부다.
        _id_patterns.append((re.compile(rb"(?<![A-Za-z0-9_])" + re.escape(_i.encode("utf-8")) + rb"(?![A-Za-z0-9_])", re.I), _i))
        _id_patterns.append((re.compile(re.escape(_i.encode("utf-16-le")), re.I), _i + " (UTF-16)"))

    _secret_hits = 0
    _id_hits = 0
    _project_marker = os.path.basename(os.path.dirname(PROJECT_ROOT)).encode()  # 예: HiddenStageGames
    _path_re = re.compile(rb"[A-Za-z]:\\[ -!#-;=?-\[\]-~]{2,60}(?:\\[ -!#-;=?-\[\]-~]{1,60}){1,10}")
    _users_re = re.compile(rb"[A-Za-z]:[\\/]Users[\\/]([ -.0-9;=?-\[\]-~]{1,40})", re.I)

    for _full, _rel in iter_files(_root):
        try:
            with open(_full, "rb") as _f:
                _data = _f.read()
        except Exception:
            continue

        for _pat, _why in SECRET_PATTERNS:
            if re.search(_pat, _data):
                _report.fail("[비밀값] %s 에 %s" % (_rel, _why))
                _secret_hits += 1

        for _re, _label in _id_patterns:
            _m = _re.search(_data)
            if _m:
                _ctx = _data[max(0, _m.start() - 40):_m.end() + 40].decode("latin-1").replace("\x00", "")
                _report.fail("[개발자 정보] %s 에 '%s'  …%s…" % (_rel, _label, re.sub(r"[^\x20-\x7e]", ".", _ctx)))
                _id_hits += 1

        _accounts = set()
        for _m in _users_re.finditer(_data):
            _who = _m.group(1).decode("latin-1").strip()
            # SDK 안에 들어 있는 형식 문자열(C:\Users\%s 같은 것)은 계정명이 아니다.
            if not _who or re.fullmatch(r"[\]\[%{}*?<>$. ]*", _who) or _who.lower() in ("public", "default", "all users"):
                continue
            _accounts.add(_who)
        for _who in sorted(_accounts):
            if _who.lower() in THIRD_PARTY_BUILD_ACCOUNTS:
                _report.warn("[경로] %s 에 외부 SDK 빌드 서버 계정 경로 'C:\\Users\\%s' (우리 개발자 아님)" % (_rel, _who))
            else:
                _report.fail("[개발자 정보] %s 에 사용자 폴더 경로 'C:\\Users\\%s'" % (_rel, _who))
                _id_hits += 1

        # 우리가 만든 바이너리에 개발 PC 의 프로젝트 경로가 남았는지 (계정명이 없으면 경고만)
        if _rel.endswith(OWN_BINARIES):
            _paths = sorted(set(_m.group(0)[:140].decode("latin-1") for _m in _path_re.finditer(_data) if _project_marker in _m.group(0)))
            for _p in _paths:
                _report.warn("[경로] %s 에 개발 PC 프로젝트 경로 (계정명 없음): %s" % (_rel, _p))

    if 0 == _secret_hits:
        _report.ok("비밀값 없음 (Sentry 토큰, 개인 키, steamcmd 로그인)")
    if 0 == _id_hits:
        _report.ok("개발자 정보 없음 (git 작성자 %d개 식별자, PC 계정명, 사용자 폴더 경로)" % len(_developer_ids))


def check_metadata(_root, _report, _purpose):
    _meta = os.path.join(_root, "LumberBoy_Data", "il2cpp_data", "Metadata", "global-metadata.dat")
    if not os.path.isfile(_meta):
        _report.fail("[디버그] global-metadata.dat 를 찾지 못했습니다")
        return
    _data = open(_meta, "rb").read()

    def count(_word):
        return len(re.findall(rb"(?<![A-Za-z0-9_])" + re.escape(_word.encode()) + rb"(?![A-Za-z0-9_])", _data))

    _bad = []
    for _w in DEBUG_NAMES:
        _c = count(_w)
        if "TRAILER" == _purpose and _w in TRAILER_ONLY:
            if 0 == _c:
                _report.warn("[디버그] 촬영용 빌드인데 %s 가 없습니다 (촬영 기능이 빠졌을 수 있음)" % _w)
            continue
        if 0 != _c:
            _bad.append("%s=%d" % (_w, _c))
    if _bad:
        _report.fail("[디버그] 배포 빌드에 디버그·테스트 이름이 남음: " + ", ".join(_bad))
    else:
        _report.ok("디버그·테스트 이름 %d개 모두 0건" % len(DEBUG_NAMES))

    _missing = [_w for _w in CONTROL_NAMES if 0 == count(_w)]
    if _missing:
        _report.warn("[디버그] 대조군 이름이 없습니다(검사 방식이 틀렸을 수 있음): " + ", ".join(_missing))


def check_store(_root, _report, _store):
    _dll = []
    for _full, _rel in iter_files(_root):
        if os.path.basename(_rel).lower() == "steam_api64.dll":
            _dll.append(_rel)
    _ga = os.path.join(_root, "GameAssembly.dll")
    _restart = os.path.isfile(_ga) and b"SteamAPI_RestartAppIfNecessary" in open(_ga, "rb").read()

    if "Steam" == _store:
        if _dll and _restart:
            _report.ok("Steam 빌드: steam_api64.dll·SteamAPI_RestartAppIfNecessary 있음")
        else:
            _report.fail("[스토어] Steam 빌드인데 Steam 연동이 빠짐 (dll=%s, restart=%s)" % (bool(_dll), _restart))
    elif _store:
        if _dll or _restart:
            _report.fail("[스토어] %s 빌드에 Steam 흔적 (dll=%s, restart=%s) - 실행 시 Steam 이 켜짐" % (_store, _dll, _restart))
        else:
            _report.ok("%s 빌드: Steam 흔적 없음" % _store)


def check_stamp(_stamp, _report):
    if not _stamp:
        _report.fail("[스탬프] BUILD_STAMP.txt 가 없습니다. BuildRunner 로 빌드하지 않은 폴더입니다")
        return
    if "false" != _stamp.get("DEVELOPMENT"):
        _report.fail("[스탬프] DEVELOPMENT=%s (개발 빌드는 배포 금지)" % _stamp.get("DEVELOPMENT"))
    if "false" != _stamp.get("GIT_DIRTY"):
        _report.fail("[스탬프] GIT_DIRTY=%s (어떤 커밋으로도 재현되지 않는 빌드)" % _stamp.get("GIT_DIRTY"))
    if "TRAILER" == _stamp.get("PURPOSE"):
        _report.fail("[스탬프] PURPOSE=TRAILER (촬영용 빌드는 배포 금지)")
    if not _report.fails or all(not f.startswith("[스탬프]") for f in _report.fails):
        _report.ok("스탬프: %s / %s / %s / GIT=%s" % (_stamp.get("STORE"), _stamp.get("RELEASE"), _stamp.get("PURPOSE", "RELEASE"), _stamp.get("GIT")))


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        return 2
    _root = os.path.abspath(sys.argv[1])
    _stamp = read_stamp(_root)
    _report = Report()

    print("빌드 폴더: %s" % _root)
    print("스탬프   : STORE=%s RELEASE=%s PURPOSE=%s GIT=%s" % (_stamp.get("STORE"), _stamp.get("RELEASE"), _stamp.get("PURPOSE"), _stamp.get("GIT")))

    check_stamp(_stamp, _report)
    check_files(_root, _report)
    scan_strings(_root, _report, developer_identifiers())
    check_metadata(_root, _report, _stamp.get("PURPOSE", "RELEASE"))
    check_store(_root, _report, _stamp.get("STORE"))

    print("\n== 통과 %d" % len(_report.oks))
    for _m in _report.oks: print("  OK   " + _m)
    print("== 경고 %d (사람이 확인)" % len(_report.warns))
    for _m in _report.warns: print("  WARN " + _m)
    print("== 실패 %d" % len(_report.fails))
    for _m in _report.fails: print("  FAIL " + _m)
    return 1 if _report.fails else 0


if __name__ == "__main__":
    sys.exit(main())
