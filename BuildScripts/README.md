# Steam 업로드 스크립트

`steamcmd`로 depot을 올릴 때 쓰는 SteamPipe 스크립트입니다.
파트너 사이트에서 손으로 올리지 말고 여기 있는 것만 쓰세요. **업로드 단계는 에디터 쪽
안전장치(`SteamBuildModeSwitcher`, `DemoContentStripper`)가 닿지 않는 유일한 구간이고,
출시 사고는 대부분 정확히 여기서 납니다.**

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
  Release\                      <- 정식 빌드 출력 (LumberBoy.exe 가 바로 아래)
  Demo\                         <- 데모 빌드 출력
  _SteamPipeOutput\             <- SteamPipe 로그·청크 캐시
```

`Release` 와 `Demo` 는 **반드시 분리**하세요. 같은 폴더에 두 모드를 번갈아
빌드하면 이전 모드의 잔여 파일이 섞입니다. 특히 두 모드는 Steam에서 서로 다른 앱이라,
섞인 채로 올리면 데모 앱에 정식 콘텐츠가 실립니다.

Unity에서 빌드할 때 출력 경로를 `C:\Unity Build\Demo` (또는 `\Release`) 로 지정하세요.
`C:\Unity Build` 바로 아래에 빌드하면 `_SteamPipeOutput` 과 두 모드가 한 폴더에 섞입니다.

`_SteamPipeOutput` 이 `contentroot` **바깥**에 있는 것이 중요합니다. 안에 두면 로그와
청크 캐시가 그대로 depot에 실립니다.

폴더 이름을 바꾸시려면 `app_build_*.vdf` 의 `buildoutput` / `contentroot` 와
`depot_build_*.vdf` 의 `contentroot` 를 모두 맞춰야 합니다.

---

## 2. 업로드 절차

### ① 모드 전환

Unity 에디터에서 `Tools > Steam > 빌드 모드 - 정식` (또는 `- 데모`).

디파인·`steam_appid.txt`·Sentry environment·GameAnalytics build 문자열이 한꺼번에
맞춰집니다. **손으로 하나씩 바꾸지 마세요.**

### ② 확인

`Tools > Steam > 현재 빌드 모드 확인` 을 열어 모드 / 디파인 / 세이브 변형 / 앱 ID /
`steam_appid.txt` / Sentry env / GA build 가 전부 의도한 값인지 봅니다.

`steam_appid.txt` 가 **480(Spacewar)** 으로 되어 있으면 개발용 임시 설정이 남은 것입니다.
①로 돌아가세요.

### ③ 빌드

`C:\Unity Build\Release` 또는 `C:\Unity Build\Demo` 로 출력합니다. 대상 폴더를 **비우고** 빌드하세요.

### ④ 예행 연습 (제외 규칙을 고쳤거나 오랜만이라면)

`app_build_*.vdf` 의 `preview` 를 `"1"` 로 바꾸고 한 번 돌립니다. 업로드는 일어나지 않고
무엇이 올라갈지만 로그로 남습니다.

`C:\Unity Build\_SteamPipeOutput\<모드>\` 의 로그에서 아래가 **없는지** 확인하세요.

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
