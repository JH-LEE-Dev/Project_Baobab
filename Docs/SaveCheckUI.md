# 세이브 확인 화면 (SaveCheck UI) 작업 가이드

메인 메뉴가 뜨기 직전에 세이브 파일을 읽을 수 있는지 확인하고, 그 결과를 유저에게 보여주는 화면입니다.
**UI 프리팹과 그 안의 연출만 만들면 되며, 시스템 연결은 이미 끝나 있습니다.**

---

## 1. 이 화면이 왜 필요한가

세이브 파일이 백신 검사나 STOVE 런처의 클라우드 복원 때문에 **잠깐 안 읽히는** 일이 있습니다.
파일 내용은 멀쩡한데도요.

그때 아무 말 없이 메인 메뉴를 띄우면 "이어하기" 버튼이 사라져 보입니다. 유저는 세이브가 날아간 줄 알고
새 게임을 누르고, 그러면 **멀쩡한 진행도가 진짜로 지워집니다.**

이 화면 하나가 그걸 막습니다. 그래서 "예쁘게 보여주는 것"보다 **뒤가 눌리지 않는 것**이 훨씬 중요합니다.

---

## 2. 담당 구분

| | 담당 | 파일 |
|---|---|---|
| 세이브 읽기, 재시도, 상태 판정 | 시스템 | `Application Layer/SaveSystem/SaveManager.cs` |
| 시스템 ↔ 뷰 연결, 버튼 동작 실행 | 시스템 | `Application Layer/UISystem/Coordinator/SaveCheck/SaveCheckCoordinator.cs` |
| 뷰의 상태 전환 로직 | 시스템 | `UIView_SaveCheck.cs` 의 `ApplyState` / `RefreshPanels` |
| **프리팹, 레이아웃, 텍스트, 연출** | **UI** | 새로 만들 프리팹 + `UIView_SaveCheck.cs` 의 `[SerializeField]` 와 연출 훅 |

`UIView_SaveCheck.cs` 안에 `// // 시스템 담당 영역` 주석으로 경계를 표시해 두었습니다.
그 구간의 `ApplyState`, `RefreshPanels`, 버튼 콜백은 건드리지 마세요.

---

## 3. 만들 것 — 프리팹 하나

`UIView_SaveCheck` 컴포넌트를 붙인 프리팹을 만듭니다. 안에 화면 세 개가 들어갑니다.

```
SaveCheckView (Canvas + UIView_SaveCheck)
├── CheckingPanel        "세이브 파일을 확인 중입니다..."
├── FailedPanel          다시 시도 / 새로 시작 / 게임 종료
└── AbandonConfirmPanel  "정말 새로 시작할까요?" 최종 확인
```

세 패널은 **서로 배타적**입니다. 동시에 두 개가 켜지는 일은 없습니다.
켜고 끄는 것은 시스템이 하므로, 여러분은 **각 패널의 내용만** 만들면 됩니다.

### 인스펙터에 채울 것

| 필드 | 넣을 것 |
|---|---|
| `checkingPanel` | 확인 중 화면 루트 |
| `failedPanel` | 실패 화면 루트 |
| `abandonConfirmPanel` | 포기 확인 팝업 루트 |
| `retryButton` | 실패 화면의 "다시 시도" |
| `abandonButton` | 실패 화면의 "새로 시작" |
| `quitButton` | 실패 화면의 "게임 종료" |
| `abandonConfirmButton` | 확인 팝업의 "예" |
| `abandonCancelButton` | 확인 팝업의 "아니오" |
| `checkingPanelDelaySeconds` | 기본값 0.3 그대로 두세요 (아래 4-2 참고) |

---

## 4. 반드시 지켜야 하는 것

### 4-1. 뒤가 눌리면 안 됩니다

**세 패널 각각이 자기만의 전체 화면 블로커를 가져야 합니다.** `raycastTarget`이 켜진 이미지를
패널 맨 아래에 깔면 됩니다. 알파는 0이어도 됩니다.

캔버스 `sortingOrder`는 메인 메뉴보다 위여야 합니다.

`UIView`의 **`bCloseableByESC`는 반드시 꺼둔 상태**로 두세요. 켜면 ESC로 닫히고, 그 순간 이 화면을
만든 이유가 사라집니다.

### 4-2. 확인 화면은 대부분 안 뜹니다

정상적인 경우 확인은 **한 프레임 안에** 끝납니다. 그래서 `checkingPanelDelaySeconds`(0.3초)보다
빨리 끝나면 화면을 아예 띄우지 않습니다. 0으로 바꾸면 게임을 켤 때마다 화면이 한 번 깜빡입니다.

즉 `CheckingPanel`은 **거의 안 보이는 화면**입니다. 연출에 힘을 많이 줄 필요는 없습니다.
반대로 `FailedPanel`은 유저가 당황한 상태로 오래 들여다볼 화면입니다.

### 4-3. 게임패드로 조작할 수 있어야 합니다

키보드/마우스 없이도 세 버튼을 고를 수 있어야 합니다. 프로젝트의 기존 방식은
`UIView_Warning`(`Presentation Layer/UISystem/UIView/WarningUI/`)이 참고 자료입니다.
`UISelectionCursor`와 포커스 처리를 그쪽과 같은 방식으로 맞춰주세요.

---

## 5. 문구 가이드 — 여기가 제일 중요합니다

**"세이브가 손상되었습니다"라고 쓰면 안 됩니다.** 실제로는 파일이 멀쩡한 경우가 대부분이고,
그렇게 쓰면 유저가 포기 버튼을 누르도록 등을 떠미는 셈이 됩니다.

| 화면 | 이렇게 | 이러면 안 됨 |
|---|---|---|
| 확인 중 | "세이브 파일을 확인하고 있습니다..." | — |
| 실패 | "세이브 파일을 읽지 못했습니다.<br>다른 프로그램이 파일을 사용 중일 수 있습니다." | "세이브가 손상되었습니다" |
| 포기 확인 | "새로 시작하면 기존 진행도를 덮어씁니다.<br>클라우드에 저장된 기록도 함께 정리됩니다.<br>계속할까요?" | "정말요?" |

실패 화면에는 **"게임을 다시 시작하면 해결되는 경우가 많습니다"** 같은 안내를 한 줄 넣어주면
실제로 대부분의 유저가 그렇게 해결합니다.

포기 확인 팝업의 기본 선택(포커스)은 **"아니오"** 여야 합니다.

현지화는 다른 UI와 같은 방식으로 붙이면 됩니다. `UIView_Warning`이 로컬라이제이션 JSON을 어떻게
읽는지 그대로 따라가세요.

---

## 6. 붙이는 절차

1. 위 구조로 프리팹을 만듭니다.
2. `BootStrap` 프리팹(= `SaveManager`가 붙어 있는 그 오브젝트)에 **`SaveCheckCoordinator` 컴포넌트를 추가**합니다.
3. 그 컴포넌트의 `saveCheckViewPrefab` 슬롯에 1번 프리팹을 넣습니다.

이게 전부입니다. 코드를 부르는 곳은 없습니다.

> 2·3번을 안 해도 게임은 정상 동작합니다. 다만 읽기에 실패했을 때 유저에게 아무것도 안 보이고,
> 콘솔에만 경고가 찍힙니다.

---

## 7. 테스트 방법

실제로 파일을 잠가서 재현할 수 있습니다. **PowerShell 창을 하나 열고** 아래를 실행하면
그 창이 켜져 있는 동안 세이브 파일이 잠깁니다.

```powershell
$f=[System.IO.File]::Open("$env:LOCALAPPDATA\LumberBoy\SaveData.dat",'Open','Read','None'); Read-Host "엔터를 누르면 잠금 해제"; $f.Close()
```

이 상태로 게임을 켜면:

1. `CheckingPanel`이 뜹니다
2. 8초 뒤 `FailedPanel`로 바뀝니다
3. PowerShell 창에서 **엔터를 누른 뒤** "다시 시도"를 누르면 잠금이 풀려 정상 진입합니다

세이브 파일이 없다면 게임을 한 번 실행해서 새 게임을 시작하고 저장한 뒤에 테스트하세요.

**확인할 것**

- [ ] 확인 중 화면에서 뒤의 아무것도 안 눌린다
- [ ] 실패 화면에서 ESC를 눌러도 안 닫힌다
- [ ] "다시 시도"로 정상 복구된다
- [ ] "새로 시작"이 확인 팝업을 반드시 거친다
- [ ] 게임패드만으로 세 버튼을 다 고를 수 있다
- [ ] 잠그지 않은 평상시에는 아무 화면도 깜빡이지 않는다

---

## 8. 연출을 붙일 자리

`UIView_SaveCheck.cs` 맨 아래에 빈 훅이 준비되어 있습니다. 필요하면 여기를 채우세요.

```csharp
protected override void OnShow() { }                 // 화면이 처음 떠오를 때
protected override void OnHide() { }                 // 화면이 완전히 사라질 때
protected virtual void OnAbandonConfirmOpened() { }  // 포기 확인 팝업이 열릴 때
protected virtual void OnAbandonConfirmClosed() { }  // 취소로 닫힐 때
```

패널을 켜고 끄는 것 자체는 시스템이 처리하므로, 여기서는 페이드나 트윈만 얹으면 됩니다.

---

## 9. 막히면

- 상태가 안 바뀌는 것 같으면 콘솔에서 `[SaveManager]`, `[BootStrap]`, `[SaveCheckCoordinator]` 로그를 보세요.
- 화면이 아예 안 뜨면 6번의 2·3단계를 했는지 확인하세요. 안 했으면 `[SaveCheckCoordinator] No save check view prefab is assigned` 경고가 찍혀 있습니다.
- 상태 전환 규칙 자체를 바꿔야 할 것 같으면 UI에서 고치지 말고 시스템 쪽에 이야기해 주세요.
