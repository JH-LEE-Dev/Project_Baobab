# 계획표 v2: 언리밋 목표 처치 수 상한 100 · 언리밋 난이도 상향 · 다다익선 디메리트 교체

## 1. 언리밋 목표 처치 수 (요청서 md로 넘김)

### 현황 (코드 확인)
- `Assets/Scripts/Application Layer/RoguelikeSystem/Core/WaveSchedule.cs` 48~54행: 언리밋이면 30웨이브 줄을 읽어 `killTarget` **960 고정**.
- 소비처 `RoguelikeSystem.cs:529/531`, `RoguelikeSystem.Flow.cs:421`, `RoguelikeSystem.Debug.cs:107`은 모두 `spec.killTarget`만 읽음 → WaveSchedule 한 곳 변경으로 HUD까지 반영.

### 요청 내용
Unlimit-N 목표 처치 수 = `min(N웨이브 줄의 목표 처치 수, 언리밋 상한 100)`.
- 결과: Unlimit-1~9 = 20 / 30 / 30 / 45 / 55 / 60 / 75 / 95 / **100**, 이후 100 유지.
- `RoguelikeBalanceSO` 언리밋 헤더에 필드 추가: `public int unlimitMaxKillTarget = 100;` (FillTemporaryValues에도 100).
- `WaveSchedule.cs` `if (spec.bUnlimit)` 블록 안:
```csharp
int killRow = Mathf.Min(spec.unlimitIndex, clearWave);
spec.killTarget = Mathf.Clamp(_balance.GetWaveRow(killRow).killTarget, 1, Mathf.Max(1, _balance.unlimitMaxKillTarget));
```

## 2. 언리밋 난이도 상향 (요청서 md에 포함)

처치 수가 960 → 최대 100으로 줄어드는 만큼 HP와 생성 속도로 난이도를 올린다. 값은 `Assets/Data/Roguelike/RoguelikeBalance.asset` 225~228행과 `RoguelikeBalanceSO.FillTemporaryValues` 기본값.

| 필드 | 현재 | 변경 |
|---|---|---|
| unlimitHpGrowthPerWave | 0.10 (×1.10/웨이브) | **0.15 (×1.15/웨이브)** |
| unlimitBatchSizeAddPerWave | +1그루 | **+2그루** |
| unlimitBatchSecondsAddPerWave | −0.02초 | **−0.05초** |
| unlimitMinBatchSeconds | 1.0초 | 1.0초 유지 |

예시(Unlimit-10): HP 896 → **1,337**, 묶음 30 → **39그루**, 주기 2.32 → **2.05초**(초당 생성 12.9 → 19.0그루). 주기 하한 1.0초 도달은 Unlimit-31.

## 3. 다다익선 디메리트: 이동속도 저하 → 나무 생성 증가 (직접 작업, 소넷 작업자)

- `Assets/Data/Roguelike/Cards/Draft/ruby_more_the_merrier.asset`
  1. `effect: 12 / value: -30`(MoveSpeedPercent) 항목 삭제.
  2. `effect: 23`(TreeSpawnRatePercent) `value: 30` → **60**.
  3. `designerNote`: 디메리트를 "나무 스폰 속도 +60%"로, 이동속도 언급 제거.
- `Assets/Resources/Localization/RoguelikeUI.json` 1182행 `Roguelike_Card_ruby_more_the_merrier_Desc`
  - kr: `{+}발동 수와 공격력 증가{/}.\n대신 {-}나무 생성 속도 증가{/}.`
  - en: `{+}More activations and increased damage{/}.\nHowever, {-}trees spawn faster{/}.`
  - 다른 언어는 원래 비어 있으므로 그대로.

## 작업 분담
- 1·2: `Docs/Roguelike/UnlimitKillTarget_AppLayer_Request.md` 작성(기존 *_AppLayer_Request.md 형식). 밸런스 에셋 수치도 시스템 담당이 반영.
- 3: 카드 에셋·현지화 JSON 직접 수정.

## 바꾸지 않는 것
- 변수명, 다른 카드, `TreeSpawnRatePercent` 계산식, Application Layer 코드(직접 수정 안 함).
- 기획서 md(Roguelike_Planning_v2.9.md, UI_WorkList.md)는 요청 시에만 갱신.

## 검수
- 에셋 YAML 들여쓰기·효과 목록 5개 확인(git diff).
- JSON 파싱 확인, 다른 키 변경 없음.
- Unity 콘솔 에러 없음.
