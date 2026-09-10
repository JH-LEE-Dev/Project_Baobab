# 스토어 업로드 스크립트

스토어마다 올리는 도구가 다릅니다. **Steam은 `steamcmd`(SteamPipe), itch.io는 `butler`입니다.**
어느 쪽이든 웹에서 손으로 올리지 말고 여기 있는 것만 쓰세요. **업로드 단계는 에디터 쪽
안전장치(`PlatformBuildModeSwitcher`, `PlatformConsistencyGuard`, `BuildOutputSanitizer`,
`DemoContentStripper`)가 닿지 않는 유일한 구간이고,
출시 사고는 대부분 정확히 여기서 납니다.**

itch.io 절차는 이 문서 맨 아래에 있습니다. 아래는 Steam입니다.

---

# Steam (SteamPipe)

| 파일 | 앱 | 앱 ID |
|---|---|---|
| `app_build_release_5129170.vdf` + `depot_build_release_5129171.vdf` | LumberBoy | 5129170 |
| `app_build_demo_5135490.vdf` + `depot_build_demo_5135491.vdf` | LumberBoy Demo | 5135490 |

앱 ID의 출처는 `BuildInfo.STEAM_APP_ID_RELEASE` / `STEAM_APP_ID_DEMO` 입니다. 코드와
스크립트가 어긋나면 안 되므로, 한쪽을 고치면 반드시 다른 쪽도 확인하세요.

---

## 0. 최초 1회 — depot ID 확인

스크립트에 적힌 depot ID(`5129171`, `5135491`)는 **신규 앱의 관례값(앱 ID + 1)이라 아직
검증되지 않았습니다.**

Steamworks > 해당 앱 > SteamPipe > Depots 에서 실제 번호를 확인하고, 다르면 두 곳을
함께 고치세요.

- `app_build_*.vdf` 의 `depots` 블록
- `depot_build_*.vdf` 의 `DepotID`

틀리면 steamcmd가 업로드를 거부합니다. 조용히 잘못될 일은 없으니 겁내지 말고 한 번
돌려보셔도 됩니다.

---

## 1. 폴더 구조 전제

빌드 출력은 프로젝트 **바깥**의 고정 경로를 씁니다. 경로에 공백이 있으므로 명령줄에서
따옴표로 감싸야 합니다.

```
C:\Unity Build\                 <- 빌드 출력 루트 (프로젝트 밖, git 대상 아님)
  STEAM_FULL\                   <- Steam 정식 출력 (LumberBoy.exe 가 바로 아래)
  STEAM_DEMO\                   <- Steam 데모 출력
  STOVE_DEMO\  ITCH_DEMO\       <- 다른 스토어도 같은 규칙
  _SteamPipeOutput\             <- SteamPipe 로그·청크 캐시
```

`STEAM_FULL` 과 `STEAM_DEMO` 는 **반드시 분리**하세요. 같은 폴더에 두 모드를 번갈아
빌드하면 이전 모드의 잔여 파일이 섞입니다. 특히 두 모드는 Steam에서 서로 다른 앱이라,
섞인 채로 올리면 데모 앱에 정식 콘텐츠가 실립니다.

폴더 이름은 `Tools > 빌드` 메뉴가 스토어와 배포에 맞춰 `<스토어>_<배포>` 로 정합니다.
루트만 한 번 지정해 두면(`Tools > 빌드 > 빌드 출력 폴더 지정`) 나머지는 따라옵니다.
손으로 다른 곳을 고르면 vdf 의 `contentroot` 와 어긋나고, 그 사실은 업로드할 때야 드러납니다.

`_SteamPipeOutput` 이 `contentroot` **바깥**에 있는 것이 중요합니다. 안에 두면 로그와
청크 캐시가 그대로 depot에 실립니다.

이름 규칙을 바꾸시려면 `PlatformBuildModeSwitcher.BuildFolderName` 과 함께
`app_build_*.vdf` 의 `buildoutput` / `contentroot`, `depot_build_*.vdf` 의 `contentroot`
를 **모두** 맞춰야 합니다. 한쪽만 고치면 steamcmd 가 없는 폴더를 가리킵니다.

---

## 2. 업로드 절차

### ① 모드 전환

Unity 에디터에서 `Tools > 빌드 > 스토어 - Steam` 과 `Tools > 빌드 > 배포 - 정식`
(또는 `- 데모`)을 각각 고릅니다. 축이 둘(스토어 × 배포)이라 두 번 고르며, 한쪽만 바꿔도
나머지는 그대로 유지됩니다.

디파인·`steam_appid.txt`·Sentry environment·GameAnalytics build 문자열이 한꺼번에
맞춰집니다. **손으로 하나씩 바꾸지 마세요.**

메뉴를 누른 뒤 **스크립트 재컴파일이 끝날 때까지 기다렸다가** 빌드하세요. 바로 빌드하면
설정과 실제 코드가 다른 물건이 나오는데, `PlatformConsistencyGuard` 가 그 상태를 잡아
빌드를 멈춥니다.

### ② 확인

`Tools > 빌드 > 현재 빌드 설정 확인` 을 열어 스토어 / 배포 / 디파인 / 세이브 변형 /
세이브 폴더 / 앱 ID / `steam_appid.txt` / Sentry env / GA build 가 전부 의도한 값인지 봅니다.

이 확인을 걸러도 `PlatformConsistencyGuard` 가 빌드 직전에 같은 것을 검사해 어긋나면
빌드를 중단합니다. 다만 가드는 배포 빌드에서만 막고 Development Build는 통과시킵니다.

`steam_appid.txt` 가 **480(Spacewar)** 으로 되어 있으면 개발용 임시 설정이 남은 것입니다.
①로 돌아가세요.

### ③ 빌드

`Tools > 빌드` 가 잡아준 경로(`C:\Unity Build\STEAM_DEMO` 등)로 출력합니다.
대상 폴더를 **비우고** 빌드하세요.

### ④ 예행 연습 (제외 규칙을 고쳤거나 오랜만이라면)

`app_build_*.vdf` 의 `preview` 를 `"1"` 로 바꾸고 한 번 돌립니다. 업로드는 일어나지 않고
무엇이 올라갈지만 로그로 남습니다.

`C:\Unity Build\_SteamPipeOutput\<스토어>_<배포>\` 의 로그에서 아래가 **없는지** 확인하세요.

- `steam_appid.txt`
- `LumberBoy_BackUpThisFolder_ButDontShipItWithYourGame/`
- `LumberBoy_BurstDebugInformation_DoNotShip/`

확인했으면 `preview` 를 `"0"` 으로 되돌립니다.

### ⑤ 업로드

```
steamcmd +login <계정> +run_app_build "<절대경로>\BuildScripts\app_build_release_5129170.vdf" +quit
```

`desc` 는 매번 갱신하세요. Steamworks 빌드 목록에서 나중에 어느 빌드가 무엇이었는지
알아볼 유일한 단서입니다. 버전과 커밋 해시를 함께 적으면 좋습니다.

**로그인 정보는 절대 vdf에 적지 마세요.** steamcmd에 직접 넘기거나 Steam Guard 캐시를 씁니다.

### ⑥ 라이브 전환

파트너 사이트에서 눈으로 확인한 뒤 직접 브랜치에 올립니다. 스크립트가 대신 하지 않습니다.
(아래 참고)

---

## 왜 `setlive` 를 비워 뒀는가

`setlive` 에 브랜치 이름을 적으면 업로드가 끝나는 즉시 그 브랜치가 갱신됩니다.
여기에 `default` 를 적어두면 **검수도 안 한 빌드가 곧바로 전체 유저에게 나갑니다.**
되돌릴 수는 있지만 그 사이에 받아 간 사람은 어쩔 수 없습니다.

비워 두면 업로드만 되고 아무 브랜치에도 반영되지 않습니다. 파트너 사이트에서 빌드를
확인한 뒤 직접 올리는 한 단계가 안전장치입니다.

테스트 브랜치가 있다면 그 이름만 넣으세요. `default` 는 넣지 마세요.

---

## 무엇을 빼는가

아래 목록은 이제 **두 겹**으로 걸립니다.

1. `BuildOutputSanitizer` — 빌드 직후 출력 폴더에서 직접 지웁니다. 어느 스토어로 올리든 적용됩니다
2. `depot_build_*.vdf` 의 `FileExclusion` — SteamPipe로 올릴 때만 적용되는 이중 방어

1번이 생긴 이유는 STOVE 때문입니다. STOVE는 depot이 아니라 자체 업로더를 쓰므로 vdf 규칙이
하나도 걸리지 않습니다. 안전장치가 업로드 경로 하나에만 붙어 있으면 스토어가 늘어나는 순간
구멍이 납니다.

`BuildOutputSanitizer` 는 Development Build에서는 아무것도 지우지 않습니다(프로파일링에
심볼이 필요하므로). 배포 빌드 로그에서 `[BuildSanitizer]` 로 검색하면 무엇이 지워졌는지 나옵니다.
**그 줄이 Sentry 심볼 업로드 로그보다 아래에 있는지 한 번은 확인하세요.** 위에 있으면 심볼이
올라가기 전에 지워진 것이라 크래시 리포트가 줄 번호를 잃습니다.


| 제외 | 이유 |
|---|---|
| `steam_appid.txt` | Steam을 거치지 않고 실행해도 API를 쓰게 해주는 개발용 우회 파일. 실리면 `SteamManager`의 `RestartAppIfNecessary` 소유권 확인이 통째로 무의미해진다. 프로젝트 루트에 있어 Unity가 빌드 출력에 복사하지는 않지만 이중으로 막는다 |
| `*_BackUpThisFolder_ButDontShipItWithYourGame*` | IL2CPP가 뱉은 C++ 소스와 심볼. `additionalIl2CppArgs`의 `--emit-source-mapping` 때문에 생기며, Sentry 심볼 업로드가 빌드 직후 **로컬에서** 읽어 간다. depot에 실을 이유가 없고 게임 로직이 그대로 들어 있다 |
| `*_BurstDebugInformation_DoNotShip*` | Burst 디버그 정보 |
| `*.pdb` | 디버그 심볼 |
| `*.log`, `Thumbs.db`, `desktop.ini` | 잡동사니 |

`steam_api64.dll` 은 `LumberBoy_Data/Plugins/x86_64/` 안에 있고 제외 대상이 아닙니다.
없으면 게임이 Steam API를 못 씁니다. ④의 예행 연습에서 이게 **포함되어 있는지** 함께 보세요.

---

## 자주 나는 사고

- **데모 빌드를 정식 앱에 올림** — 앱이 둘이라 실제로 일어납니다. ②의 확인을 거르지 마세요
- **`BAOBAB_FULL_RELEASE` 를 안 켜고 정식 업로드** — 정식 앱인데 데모 세이브 변형으로 돌고, 나중에 디파인을 맞추는 순간 유저 세이브가 호환되지 않는 것으로 취급되어 덮어써집니다
- **심볼 폴더 통째로 업로드** — depot이 몇 GB 부풀고 소스가 새어 나갑니다
- **`setlive` 에 `default`** — 위 참고

---

# itch.io (butler)

`push_itch_demo.ps1` 하나로 끝납니다. 명령을 손으로 조립하지 마세요.

| 스크립트 | 페이지 | 대상 |
|---|---|---|
| `push_itch_demo.ps1` | `hiddenstagegames.itch.io/lumberboy` | `hiddenstagegames/lumberboy:windows` |

정식은 아직 없습니다. 낼 때가 되면 페이지를 새로 만들고 이 스크립트를 복사해
`$Target` 과 `$BuildDir` 만 바꾸세요. 채널은 그때도 `windows` 입니다.

---

## 0. 최초 1회 — butler 설치와 로그인

itch.io 문서(`itch.io/docs/butler`)에서 받거나 itch 데스크톱 앱을 통해 설치합니다.
압축을 풀면 `butler.exe` + `7z.dll` + `c7zip.dll` 셋이 나오는데 **셋 다 같은 폴더에 있어야** 합니다.

**동기화 폴더(OneDrive, 바탕 화면 포함) 아래에 두지 마세요.** 파일 온디맨드가 exe나 DLL을
"온라인 전용"으로 탈수화하면 실행 자체가 실패합니다. 세이브 경로를 `AppData\Local`로 옮긴 것과
같은 이유입니다. (`GamePaths.cs` 주석 참고)

```
C:\butler-windows-amd64\butler.exe    <- 스크립트의 $ButlerExe 가 가리키는 곳
```

```
butler login
```

브라우저 인증이 한 번 뜨고, 이후에는 캐시된 자격증명을 씁니다.
**API 키를 스크립트에 적지 마세요.** SteamPipe vdf에 로그인 정보를 적지 않는 것과 같은 규칙입니다.

---

## 1. 빌드

`Tools > 빌드 > 스토어 - itch.io` + `배포 - 데모` 를 고르고, **재컴파일이 끝날 때까지 기다렸다가**
빌드합니다. 출력은 `C:\Unity Build\ITCH_DEMO` 이고 스위처가 알아서 그 자리를 잡아줍니다.

**대상 폴더를 비우고 빌드하세요.** 다른 스토어의 잔여 파일이 섞입니다.

---

## 2. 업로드

```
.\BuildScripts\push_itch_demo.ps1
```

검사를 통과하면 무엇을 어디로 올리는지 보여주고 한 번 묻습니다. `y` 를 눌러야 올라갑니다.
확인 없이 바로 올리려면 `-Force` 를 붙이지만, 그 한 단계가 안전장치이므로 평소에는 쓰지 마세요.
(SteamPipe의 `setlive` 를 비워 두는 것과 같은 이유입니다)

### 스크립트가 막아 주는 것

| 검사 | 걸리면 |
|---|---|
| 빌드 안에 `LumberBoy_ITCH` 가 있는가 | itch 빌드가 아니므로 중단 |
| 빌드 안에 `LumberBoy_STOVE` 가 없는가 | **다른 스토어의 빌드**이므로 중단 |
| 심볼 폴더 · `*.pdb` · `Assembly-CSharp.dll` · `steam_appid.txt` · `steam_api*.dll` | 하나라도 있으면 중단 |
| 워킹 트리가 깨끗한가 | 경고만 (빌드와 커밋 해시가 어긋날 수 있음) |

두 번째 검사가 실제 사고에서 나왔습니다. **STOVE 빌드가 `ITCH_DEMO` 폴더에 들어가 있던 적이
있고, 폴더 이름만 봐서는 알아채지 못했습니다.** 세이브 폴더 이름이 스토어마다 달라서 빌드 안의
문자열이 유일한 구분자입니다.

`Assembly-CSharp.dll` 검사는 Mono 빌드를 잡습니다. `ReleaseScriptingBackendGuard` 가 이미
막지만, Development Build로 뽑으면 그 가드도 `BuildOutputSanitizer` 도 통과시키므로 여기서 봅니다.

### 버전 문자열

`--userversion` 은 `1.0.0-<커밋해시>` 로 자동 조립됩니다. itch에는 SteamPipe의 `desc` 에
해당하는 자리가 없어서, **나중에 어느 빌드가 어느 코드였는지 아는 유일한 단서**가 이 값입니다.
Steam 쪽에서 `desc` 에 커밋 해시를 박는 습관과 같습니다.

---

## 3. 올린 뒤

파트너 사이트가 아니라 게임 페이지에서 직접 봅니다.

- 파일이 목록에 뜨는지, **플랫폼이 Windows 로 잡혔는지** (채널 이름에 `windows` 가 들어가야 붙습니다)
- itch 앱으로도 설치해서 실행되는지 — **채널 이름이 틀리면 여기서 처음 드러납니다**
- 데모 범위 표기가 실제 빌드와 맞는지 (`HUD_PopupNav_Main.MaxPlayableMapTypeInDemo`)

---

## 자주 나는 사고 (itch)

- **다른 스토어 빌드를 올림** — 스크립트가 막습니다. 막혔다면 폴더를 비우고 itch로 다시 빌드하세요
- **채널 이름에 `windows` 를 안 넣음** — itch 앱이 설치 대상으로 인식하지 못합니다
- **butler를 OneDrive 아래 둠** — 어느 날 갑자기 실행이 안 됩니다
- **오래된 빌드를 올림** — 스크립트가 빌드 시각을 찍어 줍니다. 최근 커밋보다 오래됐으면 다시 빌드하세요
- **zip으로 감싸서 올림** — butler는 폴더를 그대로 올립니다. 한 겹 더 감싸면 itch 앱이 exe를 못 찾습니다
