# 배포 빌드 유출 검토

배포 빌드(Steam·itch·STOVE, 데모·정식)를 올리기 전에 **밖으로 나가면 안 되는 것이 빌드에 섞이지 않았는지** 확인하는 문서입니다.

> **AI 세션 지시**: 빌드, 빌드 검수, 업로드(desc·명령·코멘트) 요청을 받으면 작업 전에 이 문서를 끝까지 읽고, 아래 항목을 **빠짐없이** 수행한 뒤 결과를 표로 보고하십시오. 확인하지 못한 항목은 "안 했다"고 적으십시오. 이 문서에 없는 새 유출 경로를 찾으면 이 문서에 추가하십시오.

빌드 절차(모드 전환, 빌드, 데모 제외 요약 수치, Sentry, 글자 누락 등)는 `BuildScripts/README.md` 에 있습니다. 이 문서는 그중 **유출**에 관한 것만 모았습니다.

---

## 1. 빠른 실행 순서

```bash
# 1) 빌드 폴더 유출 검사 (소스·비밀값·개발자 정보·디버그 이름·스토어 흔적·스탬프). 실패 0 이어야 함
python BuildScripts/Verify/leak_scan.py "C:/Unity Build/STEAM_DEMO"

# 2) 빌드 안쪽 리소스 검사 (UnityPy 필요. 데모면 미공개 그림·정식판 데이터 0 이어야 함)
#    세 번째 인자로 직전 배포 빌드의 출력 폴더를 주면 늘고 준 그림을 비교합니다
<upy 파이썬> BuildScripts/Verify/extract_check.py "C:/Unity Build/STEAM_DEMO" <출력 폴더> [직전 출력 폴더]

# 3) 글자 누락 (프로젝트 루트에서)
python BuildScripts/Verify/glyph_check.py
```

에디터 쪽 검사(Unity 에서 실행):
- `Tools > 빌드 > 업로드 전 검사` (`UploadPreflightCheck`)
- 빌드 직후 콘솔에 `[DemoStrip] 미공개 원목 그림이 데모 빌드에 실렸습니다` 오류가 **없어야** 합니다.
- `BuildReport.GetLatestReport().packedAssets` 를 `BuildScripts/Verify/full_only_assets.txt` 와 대조합니다(데모). 증분 빌드면 목록이 비어 나올 수 있으니 그때는 2) 의 추출 검사로 대신합니다.

UnityPy 준비(한 번만, 저장소 밖 폴더에서):
```bash
python -m venv <폴더>/upy
<폴더>/upy/Scripts/python -m pip install UnityPy
```

---

## 2. 범주별 검토 항목

### 2-1. 소스 코드·내부 파일

| 나가면 안 되는 것 | 막는 장치 | 확인 방법 · 정상 결과 |
|---|---|---|
| C# 원본 (Mono 빌드면 `Assembly-CSharp.dll` 이 그대로 디컴파일됨) | `ReleaseScriptingBackendGuard` (IL2CPP 강제) | `leak_scan` : `IL2CPP 빌드` OK, `Managed/Assembly-CSharp.dll` 없음 |
| IL2CPP 백업 폴더(변환된 C++ 소스 전체), Burst 디버그 정보, `.pdb` | `BuildOutputSanitizer`, depot `FileExclusion` | `leak_scan` : 해당 파일·폴더 없음. Sentry 업로드 로그가 정리 로그보다 위 |
| `.cs`, 프로젝트 파일, `.vdf`, `.git`, `.env`, 원본 에셋(`.prefab` 등), 작업 문서 | 빌드 폴더가 프로젝트 밖이고 `BuildRunner` 가 비우고 빌드 | `leak_scan` : 소스·내부 파일 없음 |
| `PerformanceTestRunInfo.json` (빌드 PC 이름·사양) | `PerformanceTestArtifactStripper` | `leak_scan` 파일 검사, 업로드 전 검사의 포장 목록 |

**막을 수 없는 것(수용)**: `global-metadata.dat` 는 암호화하지 않아 클래스·함수·필드 **이름**은 보입니다(Il2CppDumper). 동작 코드는 복원되지 않습니다. 난독화·암호화는 관리 부담에 비해 이득이 작아 하지 않기로 했습니다.

### 2-2. 비밀값·계정

| 나가면 안 되는 것 | 막는 장치 | 확인 방법 · 정상 결과 |
|---|---|---|
| Sentry 업로드 토큰 (`sntrys_`/`sntryu_`), `sentry.properties` | 토큰은 환경 변수 `SENTRY_AUTH_TOKEN` 으로만 씀(`SentryCliOptions` Auth 는 비움), Sanitizer 가 `sentry.properties` 삭제 | `leak_scan` : 비밀값 없음 |
| 개인 키, SSH 키 | - | `leak_scan` |
| Steam 계정 비밀번호·Steam Guard | steamcmd 에 사람이 직접 입력. 문서·스크립트·대화에 적지 않음 | 업로드 스크립트에는 계정 이름만 |
| `steam_appid.txt` (있으면 Steam 실행 확인을 건너뜀) | 스위처가 관리, depot `FileExclusion`, Sanitizer | `leak_scan` : 없음 |

**공개용이라 빌드에 들어가도 되는 것**: Sentry DSN, GameAnalytics 게임 키·시크릿 키. 둘 다 클라이언트에 넣도록 설계된 값입니다. 그래도 대화·문서에 값을 옮겨 적지는 않습니다.

### 2-3. 개발자 개인정보

| 나가면 안 되는 것 | 확인 방법 · 정상 결과 |
|---|---|
| git 작성자 이름·메일, 개발 PC 계정명, `C:\Users\<계정>` 경로 | `leak_scan` 이 **실행 때** git 기록과 PC 계정에서 식별자를 모아 빌드 전체를 ASCII·UTF-16 으로 검색합니다(이 저장소 파일에는 개인정보를 적지 않습니다). 결과 `개발자 정보 없음` |
| 팀원 실명 | 스크립트가 모릅니다. 크레딧 화면 외에 실명을 넣은 적이 있으면 사람이 따로 검색하십시오 |

**알려진 정상 경고**(무시해도 됨):
- `GameAssembly.dll` 의 PDB 경로 `C:\Unity Project\HiddenStageGames\LumberBoy\Library\Bee\...GameAssembly.pdb`
- `global-metadata.dat` 의 `...\Rendering\HighResolutionBloomFeature.cs` 경로 문자열 1개
- `GameAnalytics.dll` 의 `C:\Users\runneradmin` (GA 제작사 빌드 서버 계정)
- 엔진·SDK DLL(UnityPlayer, crashpad, sentry, dstorage, steam_api64 등) 안의 그 회사 빌드 서버 경로 — 스크립트가 보지 않습니다

계정명이 없는 프로젝트 경로는 회사명·폴더 구조만 보여 개인정보가 아닙니다.

### 2-4. 플레이어 개인정보 (실행 중 수집)

코드가 바뀌었을 때만 다시 보면 됩니다. 기준은 2026-10-07 검토입니다.

| 항목 | 기대 상태 | 어디서 보나 |
|---|---|---|
| 동의 전 수집 | 동의(`EDataConsent.Granted`) 전에는 Sentry 가 시작 자체를 안 함, GA 는 초기화 안 함 | `SentryConsentOptionsConfiguration`, `DataConsentGate` |
| 동의 연결 | `SentryOptions.asset` 의 `OptionsConfiguration` 이 `SentryConsentOptionsConfiguration.asset`(guid `07c7a5d2…`)을 가리킴. **끊기면 동의와 무관하게 Sentry 가 켜집니다** | `Assets/Resources/Sentry/SentryOptions.asset` |
| Sentry 자동 수집 | `SendDefaultPii: 0`, `AttachScreenshot: 0` | 같은 파일 |
| Steam ID | 원본을 보내지 않고 솔트 SHA-256 앞 8바이트만 | `SentryUserContextTagger` |
| 로그 속 경로 | `C:\Users\<계정>` 은 `GamePaths.Redact` 로 가린 뒤 로그에 씀 | 새 `Debug.Log` 에 경로·파일 예외를 넣을 때 `Redact` 를 거쳤는지 |
| 이름·기기 정보 | Steam 닉네임, Windows 사용자명, `SystemInfo.deviceUniqueIdentifier` 를 읽어 보내지 않음 | `grep -rn "GetPersonaName\|Environment.UserName\|deviceUniqueIdentifier" Assets/Scripts` |
| 크래시 덤프 | 동의한 유저의 네이티브 크래시 때 메모리 덤프가 Sentry 로 감(경로 등 포함 가능). 개인정보 안내에 "크래시 정보 수집"이 있어야 함 | 개인정보 처리 안내 문서 |

### 2-5. 미공개 콘텐츠·리소스 (데모)

리소스 **추출 자체는 막을 수 없습니다**(어떤 Unity 게임이든 UnityPy·AssetRipper 로 그림·소리를 꺼낼 수 있음). 그래서 목표는 "**데모에 정식판 콘텐츠를 아예 넣지 않는 것**"입니다.

| 나가면 안 되는 것 | 막는 장치 | 확인 방법 · 정상 결과 |
|---|---|---|
| 정식판 스킬 DB·능력 노드 DB | `AbilityBuildVariantStripper` | 추출 검사 `정식판 데이터 없음`, 포장 목록에 `_Full.` 0 |
| 미공개 맵 BGM·나무 외형·원석·레시피·아이콘·정식판 기능 에셋 | `DemoContentStripper` (실패하면 빌드 중단, 고친 에셋이 저장 안 되면 빌드 중단) | 빌드 로그 요약 수치(README), `full_only_assets.txt` 대조 |
| 미공개 나무 원목 그림 (Wood04~12, Timber_*, 4스테이지 보석 원목) | `DemoContentStripper.StripLogItemIcons` + 결과 화면·인벤토리 칸·오라 설정 프리팹 | 빌드 직후 `[DemoStrip] 미공개 원목 그림…` 오류 없음, 추출 검사 `미공개 그림 없음` |
| 데모·정식 **공통 에셋**에 섞인 미공개 그림 | (공통 에셋은 `full_only_assets.txt` 대조로 안 잡힘) | **추출 검사로 직전 배포 빌드와 비교** — 늘어난 그림이 공개 범위 안인지 사람이 확인 |
| 영상·테스트용 수치(예: 충격파 50000) | `GameStatBuildGuard` (데모·정식 기준값 대조) | 빌드 전 `GameStatBuildGuard.Validate(release)` 0건 |

### 2-6. 디버그·치트·테스트 기능

| 나가면 안 되는 것 | 막는 장치 | 확인 방법 · 정상 결과 |
|---|---|---|
| 지도 전체 해금, 디버그 소리, FPS 표시, 테스트 버튼 등 | `#if UNITY_EDITOR`(촬영용은 `UNITY_EDITOR \|\| BAOBAB_TRAILER`), `DebugSwitchBuildGuard` | `leak_scan` : 디버그 이름 25개 0건(촬영용 빌드는 지도 해금 이름이 있는 게 정상) |
| 튜토리얼 꺼짐, 테스트 씬, 자동저장 꺼짐 | `DebugSwitchBuildGuard`, `AutoSaveBuildGuard` | 빌드 전 검사 0건 |
| 개발 빌드, 촬영용 빌드, 벤치마크 빌드 | 스탬프 `DEVELOPMENT`/`PURPOSE`, `BenchmarkBuildToggle`, 업로드 전 검사 | `leak_scan` 스탬프 OK, 업로드 전 검사 오류 0 |
| 실행 중 생긴 오브젝트가 프리팹에 저장된 것(`(Clone)`, `_UIParent`, 빠진 스크립트) | `DemoContentStripper.VerifyModifiedAssetsSaved` 가 저장 실패를 잡음 | 빌드 로그에 `Error while saving Prefab … missing script` 없음 |

### 2-7. 스토어 섞임

| 나가면 안 되는 것 | 확인 방법 · 정상 결과 |
|---|---|
| itch·STOVE 빌드에 Steam 연동(실행하면 Steam 이 켜져 심사 반려) | `leak_scan` : Steam 흔적 없음 |
| Steam 빌드에서 Steam 연동 누락 | `leak_scan` : `steam_api64.dll`·`SteamAPI_RestartAppIfNecessary` 있음 |
| 다른 스토어·다른 앱 폴더를 골라 올림 | 업로드 전 검사(클립보드 경로), vdf `contentroot` |
| 데모 앱에 정식 빌드 | 스탬프 `RELEASE`, vdf `appid`·`contentroot` |

### 2-8. 의도하지 않은 설정 변경

빌드에만 영향을 주고 에디터에서는 안 보이는 변경입니다. 1.0.3 이후처럼 **직전 배포 빌드 커밋과 비교**하십시오.

```bash
git diff <직전 배포 커밋> -- ProjectSettings Packages
```

- 품질 설정의 플랫폼별 기본 품질(`m_PerPlatformDefaultQuality`)이 지워지면 빌드가 어느 품질로 시작할지 정해지지 않습니다(2026-10-07 사례).
- 새 패키지는 런타임에 들어가는지(에디터 전용인지) 확인합니다.

### 2-9. 업로드 단계

- depot vdf 의 `FileExclusion` 에 `steam_appid.txt`, 백업 폴더, Burst 폴더, `*.pdb`, `*.log` 가 있어야 합니다.
- `setlive` 는 비어 있어야 합니다(라이브 전환은 Steamworks 에서 사람이).
- desc 는 빌드 커밋을 적습니다. desc 커밋 뒤 업로드 전 검사는 경고 1건("desc·문서 커밋만")이 정상입니다.

---

## 3. 사고·발견 기록

| 날짜 | 내용 | 조치 |
|---|---|---|
| 2026-09-25 | STOVE 검수용 빌드 대신 옆의 `STEAM_DEMO` 폴더를 골라 올려 반려 | 업로드 전 검사 + 경로 클립보드 복사 |
| 2026-09 | Performance Testing 이 `PerformanceTestRunInfo.json`(빌드 PC 이름·사양)을 출시 빌드에 넣고 있었음 | `PerformanceTestArtifactStripper` |
| 2026-10-01 | 드론 VFX 확인용 `droneCount=1` 이 커밋됨 | 캐릭터 스탯 가드 → `GameStatBuildGuard` |
| 2026-10-06 | 영상 촬영용 수치·테스트 노드·튜토리얼 끔이 공용 브랜치에 커밋됨 | 정식 기준값을 영상 전 수치로, 튜토리얼 복구 |
| ~1.0.3 | 데모에 미공개 나무 원목 아이콘 Wood04~12 가 실려 나감 (STOVE 1.0.2 는 정식판 전용 에셋 177개) | 원목 그림 제외 보강(32a67e01), 추출 검사 도입 |
| 2026-10-07 | 인벤토리 칸 프리팹에 실행 잔여물(빠진 스크립트)이 있어 데모 제외 저장이 실패, Wood04 가 실릴 뻔함 | 잔여물 삭제, 저장 확인(64045fc7) |
| 2026-10-07 | 인벤토리 작업 커밋에 품질 설정 기본값 삭제가 섞여 들어옴 | 1.0.3 상태로 복원(faeaa323) |
| 2026-10-07 | FontMaker 미리보기 폭이 프리팹에 저장돼 매 빌드 `GIT_DIRTY=true` | 0 으로 커밋(199eb96c, e18ef3a0). 빌드 전에 저장 후 dirty 여부 확인 |
| 2026-10-09 | 같은 폭이 빌드 중 다시 0 → 23·7 로 바뀌어 itch 데모가 `GIT_DIRTY=true` (실행 중 다시 계산되는 값이라 빌드 내용과 무관) | `BuildStampWriter` 가 FontMaker 중첩 프리팹의 `m_SizeDelta.x` 값 줄만 바뀐 프리팹은 dirty 로 치지 않음. 다른 줄이 섞이면 그대로 dirty. 빌드 로그에 `[BuildStamp] … GIT_DIRTY 로 치지 않습니다` 가 남음 |

---

## 4. 이 문서 갱신 규칙

- 새 유출 경로를 찾으면 2장 표와 3장 기록에 함께 추가합니다.
- 미공개 범위가 바뀌면(데모 공개 지역 확대, 새 나무·기능) `extract_check.py` 의 `DEMO_LOCKED_IMAGE_PREFIXES` 와 `full_only_assets.txt` 를 갱신합니다.
- 디버그 스위치를 새로 만들면 `DebugSwitchBuildGuard` 와 `leak_scan.py` 의 `DEBUG_NAMES` 에 함께 넣습니다.
