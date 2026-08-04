# 오디오 시스템 사용 가이드

Project_Baobab 오디오 시스템의 사용법을 정리한 문서입니다. 사운드를 재생/제어하는 대부분의 작업은 `Sound` 클래스 하나만 알면 충분합니다.

---

## 1. 핵심 개념

| 구성 요소 | 경로 | 역할 |
|---|---|---|
| `Sound` | `Assets/Scripts/Application Layer/AudioSystem/Sound.cs` | **다른 스크립트가 실제로 호출하는 정적(static) 진입점.** 이 클래스만 사용하세요. |
| `AudioManager` | `Assets/Scripts/Application Layer/AudioSystem/AudioManager.cs` | 실제 재생/풀링/믹서 처리를 담당하는 싱글턴. 직접 참조할 필요 없음. |
| `SoundID` / `MixerID` | `Assets/Scripts/Application Layer/AudioSystem/AudioSystemUsingData.cs` | 자동 생성되는 enum. 사운드/믹서 그룹 식별자. |
| `AudioDatabase` | `Assets/Scriptable Obj/Audio/AudioDatabase.asset` | `SoundID → 클립/믹서/볼륨` 매핑 데이터. |
| `AudioIDGenerator` | `Assets/Scripts/Editor/AudioSystem/AudioIDGenerator.cs` | `Assets/Sounds` 폴더를 스캔해서 위 ID/DB를 자동 생성하는 에디터 툴. |

> **컨벤션**: 코드에서는 항상 `Sound.XXX(...)` 형태로만 호출하고, `AudioManager.Instance`를 직접 만지지 마세요. `AudioManager.Instance`가 아직 없는 시점(씬 초기 로드 등)에도 `Sound`는 안전하게 무시(no-op)합니다.

---

## 2. 기본 사용법

### 2.1 일반 SFX 재생 (3D 위치 사운드)

```csharp
Sound.Play(SoundID.GrassFootstep, transform.position);
```

- 즉시 재생되지 않고 내부 큐에 등록된 뒤, 다음 프레임에 재생됩니다.
- 볼륨/3D 여부/피치를 함께 지정할 수도 있습니다.

```csharp
// 볼륨 1.0, 3D 사운드, 피치 1.2로 재생
Sound.Play(SoundID.CoinGet, coinPosition, 1f, true, 1.2f);
```

### 2.2 UI 사운드 재생 (2D, 위치 없음)

```csharp
Sound.PlayUI(SoundID.OutItem);
```

버튼 클릭, 아이템 획득/인출 등 위치가 의미 없는 사운드는 `PlayUI`를 사용하세요. 내부적으로 `is3D = false`로 고정되어 재생됩니다.

### 2.3 BGM 재생/정지

```csharp
Sound.PlayBGM(SoundID.TownBGM);        // 재생 (볼륨 기본값 1)
Sound.FadeOutBGM(1.5f);                // 1.5초에 걸쳐 페이드아웃 후 정지
Sound.StopBGM();                       // 즉시 정지
Sound.PauseBGM();
Sound.ResumeBGM();
```

BGM은 SFX와 별도의 전용 `AudioSource`(`bgmSource`)에서 재생되며, 동시에 하나만 재생됩니다.

---

## 3. 루프/트랙형 사운드 (기계 가동음, 엔진음 등)

시작-정지를 코드에서 직접 제어해야 하는 루프 사운드(제재소 커터, 차량 엔진 등)는 `PlayTracked` 계열을 사용하고, 반환된 `AudioHandle`을 반드시 변수에 보관해야 합니다.

```csharp
private AudioHandle cuttingSoundHandle;

// 재생 시작 (핸들 저장)
cuttingSoundHandle = Sound.PlayTracked(SoundID.Cutter, transform.position, volume, true);

// 재생 중 위치/피치 갱신
Sound.UpdateTrackedPosition(cuttingSoundHandle, transform.position);
Sound.SetTrackedPitch(cuttingSoundHandle, 1.2f);
Sound.SetTrackedVolume(cuttingSoundHandle, 0.8f);

// 정지
Sound.StopTracked(cuttingSoundHandle);
cuttingSoundHandle = AudioHandle.Invalid;
```

### 3.1 예열(PowerUp) / 전원차단(PowerDown) 연출

시동음이 따로 없는 루프 사운드를 자연스럽게 시작/정지시키고 싶을 때 사용합니다. 피치를 낮은 값에서 목표 피치까지 서서히 올리며 시작하거나, 피치·볼륨을 함께 낮추며 정지합니다.

```csharp
// 예열하며 시작 (0.4초에 걸쳐 목표 피치까지 상승)
cuttingSoundHandle = Sound.PlayTrackedWithPowerUp(
    SoundID.SawmillCutterLoop, transform.position, volume, true, duration: 0.4f);

// 전원 차단하며 정지 (0.4초에 걸쳐 피치/볼륨 하강 후 정지)
Sound.StopTrackedWithPowerDown(cuttingSoundHandle, duration: 0.4f);
```

실제 사용 예시는 [`LogCutter.cs`](../Assets/Scripts) (제재소 절단기)를 참고하세요. 전진(가공)과 역방향(날 복귀)에 따라 `SawmillCutterLoop`(예열) ↔ `Cutter`(전원차단)를 전환합니다.

### 3.2 클립 길이 조회

```csharp
float length = Sound.GetClipLength(SoundID.OffroadNonEdit);
```

엔진 시동음처럼 "이 클립이 끝나는 시점에 다음 동작을 이어붙이고 싶을 때" 사용합니다.

---

## 4. 3D 사운드 볼륨 연출 (카메라 컷씬 등)

카메라가 특정 연출(하늘로 상승, 던전 이동 등)을 할 때 3D 사운드 전체를 일괄로 줄이거나 복원할 수 있습니다. **UI(2D) 사운드에는 영향을 주지 않습니다.**

```csharp
Sound.SetProduction3DVolumeFactor(0f);              // 즉시 3D 사운드 볼륨 0
Sound.RampProduction3DVolume(1f, 1.5f);              // 1.5초에 걸쳐 원래 볼륨(1)으로 복원
```

> 씬이 로드될 때마다 `AudioManager`가 자동으로 모든 3D 사운드를 정지하고 이 계수를 0으로 초기화합니다(아래 6번 참고). 씬 전환 연출 스크립트에서 복원 타이밍을 직접 맞춰줘야 합니다.

---

## 5. 사운드 애셋 폴더 위치 및 작성 규칙

모든 사운드 원본 파일은 **`Assets/Sounds`** 아래에 위치해야 합니다(`AudioIDGenerator.cs:12`에 하드코딩된 루트). 이 루트 하위는 몇 단계든 자유롭게 폴더를 중첩해도 되며(재귀 스캔), **폴더 이름 끝이 `_Clip` 또는 `_Cue`로 끝나는 폴더만** 스캔 대상이 됩니다(대소문자 무관).

### 5.1 현재 실제 폴더 구조 (예시)

```
Assets/Sounds/
├── Mixer/
│   └── Main.mixer                     # Master/BGM/SFX/UI/Ambience 그룹 (스캔 제외 폴더)
├── BGM_Clip/
│   ├── TownBGM.mp3                    # → SoundID.TownBGM
│   └── WideGreenForest1_BGM.mp3       # → SoundID.WideGreenForest1BGM
├── Character/
│   ├── Axe/
│   │   ├── AxeBreak_Clip/AxeBreaking.ogg, AxeBreaking_Final.ogg
│   │   ├── AxeBreakEx_Cue/AxeBreakingEx.asset (+ 원본 ogg 2개)
│   │   └── Swing_Cue/Swing.asset
│   ├── Die_Clip/ (Character_Die.wav 등 3개)
│   ├── FootStep/
│   │   ├── Grass_Cue/GrassFootstep.asset (+ 원본 ogg 5개)
│   │   └── Ground_Cue/GroundFootstep.asset (+ 원본 ogg 5개)
│   ├── GetItem_Cue/GetItem.asset (+ 원본 ogg 10개)
│   └── OutItem_Cue/OutItem.asset (+ 원본 ogg 2개)
├── Cutter/
│   └── Cutter_Clip/Cutter.ogg, Sawmill_Cutter_Loop.wav
├── Offroad/
│   └── Offroad_Clip/ (Box_Open, Box_Close, Box_Jumping, Offroad_NonEdit 등)
├── Shop/
│   ├── Coin_Clip/Coin_Get.wav, Coin_Out.wav
│   └── Convayer_Clip/Convayer_Loop.wav, Convayer_Put.wav 등
└── Tree/
    ├── Hit_Clip/Tree_Hit.wav, Pitch_Hit.ogg
    ├── Dead_Cue/TreeDead.asset (+ 원본 wav 2개)
    └── Prize_Clip/Prize2.wav
```

`Character/Axe`, `Character/FootStep`처럼 카테고리 폴더를 자유롭게 몇 단계든 중첩할 수 있습니다. 중요한 건 최종적으로 `_Clip`/`_Cue`로 끝나는 폴더가 어딘가에 있으면 된다는 점입니다. `Mixer` 폴더는 이름에 상관없이 스캔에서 항상 제외됩니다(`AudioIDGenerator.cs:60`).

### 5.2 `_Clip` 폴더 — 개별 클립을 그대로 등록

폴더 이름이 `_Clip`으로 끝나면, **그 폴더에 직접 들어있는** `AudioClip` 파일들이 각각 하나의 사운드로 등록됩니다(하위 폴더에 있는 클립은 등록되지 않음, `AudioIDGenerator.cs:74-83`).

```
Cutter_Clip/
└── Cutter.ogg          → SoundID.Cutter
```

여러 개의 클립을 한 `_Clip` 폴더에 넣으면 각각 별도의 `SoundID`가 됩니다:
```
Offroad_Clip/
├── Box_Open.ogg        → SoundID.BoxOpen
├── Box_Close.wav       → SoundID.BoxClose
└── Offroad_NonEdit.wav → SoundID.OffroadNonEdit
```

> 예외: `Assets/Sounds` 루트에 직접 넣은 클립도 `_Clip` 폴더처럼 취급되어 등록됩니다(`AudioIDGenerator.cs:74`, `normalizedPath == SoundAssetsRoot`). 다만 정리 차원에서 반드시 하위 `XXX_Clip` 폴더를 만들어 사용하는 것을 권장합니다.

### 5.3 `_Cue` 폴더 — 랜덤 재생용 `AudioCueData` 1개 + 원본 클립들

폴더 이름이 `_Cue`로 끝나면, 그 폴더 안(하위 폴더 포함)에서 `AudioCueData` 타입 에셋을 찾아 **그 애셋 하나만** 사운드로 등록합니다(`AudioIDGenerator.cs:64-73`). 폴더 안의 원본 `.ogg`/`.wav` 클립들은 직접 `SoundID`가 되는 게 아니라, `AudioCueData` 애셋의 클립 리스트에 참조로만 채워 넣는 재료입니다.

```
GetItem_Cue/
├── GetItem.asset        ← 이 AudioCueData 애셋 이름이 SoundID가 됨 → SoundID.GetItem
├── GetItem_00.ogg  ┐
├── GetItem_01.ogg  │ AudioCueData(GetItem.asset)의 clips 리스트에 드래그해 넣음
├── ...             │
└── GetItem_09.ogg  ┘
```

**작성 순서**
1. `_Cue` 폴더를 만들고 원본 클립들을 넣습니다.
2. `Project` 창에서 그 폴더 안에 우클릭 → `Create > Game > Audio Cue`로 `AudioCueData` 애셋을 생성합니다. **애셋 이름이 곧 `SoundID` 이름**이 되므로 원하는 사운드 이름으로 지어주세요(예: `GetItem`, `GrassFootstep`).
3. 인스펙터에서 `clips` 리스트에 원본 클립들을 등록하고, 필요하면 `minPitch~maxPitch`, `minVolumeModifier~maxVolumeModifier` 랜덤 범위를 설정합니다.
4. 한 `_Cue` 폴더 안에 `AudioCueData`는 **1개만** 두세요. 여러 개가 있으면 그중 하나만(먼저 검색되는 것) 등록됩니다.

### 5.4 사운드 이름(= SoundID) 규칙

- `SoundID` 이름은 **폴더 이름이 아니라 실제 애셋(클립 파일 또는 `AudioCueData`) 이름**에서 만들어집니다. 이름에서 영문/숫자가 아닌 문자(공백, `_`, `-` 등)는 제거되고 각 단어 첫 글자가 대문자로 합쳐집니다(PascalCase). 예: `WideGreenForest1_BGM.mp3` → `SoundID.WideGreenForest1BGM`.
- **애셋 이름은 `Assets/Sounds` 전체를 통틀어 유일해야 합니다.** 같은 이름이 이미 등록되어 있으면 나중 것은 무시됩니다(`AudioIDGenerator.cs:116`, 이름 기준 dictionary). 서로 다른 폴더에 같은 파일명을 쓰지 마세요.
- 믹서 그룹(`MixerID`)은 `Assets/Sounds/Mixer/Main.mixer` 안의 `Master`/`BGM`/`SFX`/`UI`/`Ambience` 5개 그룹 이름으로 고정되어 있습니다(`AudioIDGenerator.cs:17`). 새 믹서 그룹을 추가해도 자동으로 스캔되지 않으니, 그룹을 늘리려면 `AudioIDGenerator.cs`의 `MixerGroupNames` 배열도 함께 수정해야 합니다.

### 5.5 등록 절차 (자동 생성 실행)

1. 위 규칙에 맞춰 `Assets/Sounds` 아래에 클립/큐 애셋을 배치합니다.
2. 유니티 메뉴에서 **`Tools > Audio > Generate Sound IDs`** 를 실행합니다.
   - `AudioSystemUsingData.cs`에 새 `SoundID`가 자동 추가됩니다.
   - `Assets/Scriptable Obj/Audio/AudioDatabase.asset`에 신규 슬롯이 추가되고, 클립/큐 에셋과 믹서 그룹이 자동으로 매칭됩니다.
3. `AudioDatabase.asset`을 열어 새로 추가된 슬롯의 `mixerId`(원하는 믹서 그룹), `defaultVolume`, `is3D`, `loop` 값을 확인/설정합니다. (믹서 그룹 자동 연결은 `mixerId`를 먼저 지정해줘야 동작합니다 — `AudioIDGenerator.cs:192`.)
4. 이후 코드에서 `SoundID.새사운드이름`으로 바로 사용할 수 있습니다.

> `AudioSystemUsingData.cs`는 `// <auto-generated />` 파일이므로 직접 수정하지 마세요. 수정해도 다음 생성 시 덮어써집니다.

---

## 6. 씬 전환 시 동작 (자동)

`AudioManager`는 `SceneManager.sceneLoaded` 이벤트를 구독하여 씬이 바뀔 때마다 다음을 **자동으로** 수행합니다.

- 대기 중인 사운드 재생 큐를 비웁니다.
- 재생 중인 모든 3D 사운드를 즉시 정지합니다(2D/UI/BGM은 영향 없음).
- `Production3DVolumeFactor`를 0으로 초기화합니다.

따라서 루프 사운드를 재생하는 오브젝트는 **`OnDisable` 등에서 반드시 `Sound.StopTracked(handle)`로 핸들을 정리**하는 습관을 들이세요. 그렇지 않으면 다음에 그 핸들로 `SetTrackedPitch` 등을 호출했을 때 이미 무효화된 핸들을 참조하게 됩니다(내부적으로 안전 처리는 되어 있지만, 로직상 핸들을 들고 있는 쪽에서 상태를 명확히 관리하는 것이 좋습니다).

---

## 7. API 요약

| 메서드 | 용도 |
|---|---|
| `Sound.Play(id, position, volume=1, is3D=true, pitch=-1)` | 큐를 통한 일반 SFX 재생 |
| `Sound.PlayUI(id, volume=1, pitch=-1)` | 2D UI 사운드 재생 |
| `Sound.PlayBGM(id, volume=1)` / `StopBGM()` / `FadeOutBGM(duration)` / `PauseBGM()` / `ResumeBGM()` | BGM 제어 |
| `Sound.PlayTracked(id, position, volume=1, is3D=true, pitchOverride=-1)` → `AudioHandle` | 즉시 재생 + 핸들로 이후 제어 (루프/트랙 사운드용) |
| `Sound.PlayTrackedWithPowerUp(id, position, volume, is3D, duration, minPitch, targetPitch)` | 피치를 서서히 올리며 시작 |
| `Sound.StopTracked(handle)` | 트랙 사운드 정지 |
| `Sound.StopTrackedWithPowerDown(handle, duration, minPitch)` | 피치/볼륨을 낮추며 정지 |
| `Sound.UpdateTrackedPosition(handle, position)` | 트랙 사운드 위치 갱신 (이동하는 사운드 소스) |
| `Sound.SetTrackedPitch(handle, pitch)` / `RampTrackedPitch(handle, target, duration)` | 피치 즉시/서서히 변경 |
| `Sound.SetTrackedVolume(handle, scale)` | 볼륨 즉시 변경 |
| `Sound.GetTrackedPitch(handle)` | 현재 피치 조회 |
| `Sound.IsTrackedPlaying(handle)` | 재생 중 여부 확인 |
| `Sound.GetClipLength(id)` | 클립 길이(초) 조회 |
| `Sound.SetProduction3DVolumeFactor(factor)` / `RampProduction3DVolume(target, duration)` | 3D 사운드 전체 볼륨 연출 (2D 제외) |

---

## 8. 실전 예시

**아이템 획득/인출 (UI 사운드)**
```csharp
Sound.PlayUI(SoundID.GetItem);
Sound.PlayUI(SoundID.OutItem);
```

**코인 연속 획득 시 피치 램프업**
```csharp
float pitchT = comboCount > 0 ? Mathf.Clamp01((float)pickupComboCount / comboCount) : 0f;
float coinPitch = Mathf.Lerp(1f, coinPitchMax, pitchT);
Sound.Play(SoundID.CoinGet, coinPosition, 1f, true, coinPitch);
```

**발자국 (지형별 분기)**
```csharp
Sound.Play(isGrass ? SoundID.GrassFootstep : SoundID.GroundFootstep, transform.position);
```

**컨테이너 적재 시 연속 볼륨/피치 부스트**
```csharp
Sound.Play(SoundID.GetItem, transform.position, depositVolumeMul, false, currentDepositPitch);
currentDepositPitch = Mathf.Clamp(currentDepositPitch + depositPitchStep, DEPOSIT_PITCH_MIN, DEPOSIT_PITCH_MAX);
```

---

## 9. 주의사항

- `Sound` 클래스만 호출하고 `AudioManager`를 직접 참조하지 마세요.
- 루프/트랙 사운드는 반드시 `AudioHandle`을 저장해두고, 비활성화/파괴 시점에 `Sound.StopTracked`로 정리하세요.
- 새 사운드는 코드가 아니라 `Assets/Sounds` 폴더 규칙(`XXX_Clip`/`XXX_Cue`) + `Tools > Audio > Generate Sound IDs`로 추가하세요.
- `AudioSystemUsingData.cs`는 자동 생성 파일이므로 직접 편집하지 마세요.
- **알려진 미구현 사항**: 옵션 화면의 마스터/BGM/SFX 볼륨 슬라이더(`UI_Option.cs`)는 `SettingsData`에 값은 저장되지만, 실제 `AudioMixer`의 exposed parameter에 반영하는 코드는 아직 연결되어 있지 않습니다. 볼륨 슬라이더 관련 작업을 하게 될 경우 이 부분을 확인/구현해야 합니다.
