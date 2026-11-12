# 언리밋 목표 처치 수 상한 및 난이도 상향 요청 (Application Layer)

> ⚠️ **2026-11-12 갱신**: 이 문서의 **난이도 상향(HP ×1.15 · 묶음 +2 · 주기 −0.05초, 4티어 시작) 부분은 폐기**되었다. 언리밋은 N웨이브의 맵 · 나무 · 스폰을 다시 따라가고 나무 HP만 N웨이브 HP × 1.35^(N−1)로 올린다. 기준은 [Roguelike_Planning_v3.0.md](Roguelike_Planning_v3.0.md) 7-2 · 부록 A "언리밋 곡선" · 부록 C-3 4-16이다. **"1. 언리밋 목표 처치 수 상한"(min(N웨이브 목표, 100))은 그대로 유효하다.**

## 현황
- `Assets/Scripts/Application Layer/RoguelikeSystem/Core/WaveSchedule.cs` 48~54행: 언리밋이면 30웨이브 줄을 읽어 `killTarget`을 **960으로 고정**한다.
- 소비처 `RoguelikeSystem.cs:529/531`, `RoguelikeSystem.Flow.cs:421`, `RoguelikeSystem.Debug.cs:107`은 모두 `spec.killTarget`만 읽는다. WaveSchedule 한 곳만 바꾸면 HUD까지 반영된다.
- 언리밋 난이도 수치는 `Assets/Data/Roguelike/RoguelikeBalance.asset` 225~228행과 `RoguelikeBalanceSO.FillTemporaryValues` 기본값에 있다.

## 요청

### 1. 언리밋 목표 처치 수 상한
Unlimit-N 목표 처치 수 = `min(N웨이브 줄의 목표 처치 수, 언리밋 상한 100)`.

- 결과: Unlimit-1~9 = 20 / 30 / 30 / 45 / 55 / 60 / 75 / 95 / **100**, 이후 100 유지.
- `RoguelikeBalanceSO` 언리밋 헤더에 필드를 추가해 주세요: `public int unlimitMaxKillTarget = 100;` (`FillTemporaryValues`에도 100).
- `WaveSchedule.cs`의 `if (spec.bUnlimit)` 블록 안을 아래처럼 바꿔 주세요.

```csharp
int killRow = Mathf.Min(spec.unlimitIndex, clearWave);
spec.killTarget = Mathf.Clamp(_balance.GetWaveRow(killRow).killTarget, 1, Mathf.Max(1, _balance.unlimitMaxKillTarget));
```

- 47행 주석도 "언리밋 목표 처치 수는 N웨이브 줄 값과 `unlimitMaxKillTarget` 중 작은 값"으로 갱신해 주세요.

### 2. 언리밋 난이도 상향
처치 수가 960에서 최대 100으로 줄어드는 만큼 HP와 생성 속도로 난이도를 올립니다. `RoguelikeBalance.asset` 225~228행과 `RoguelikeBalanceSO.FillTemporaryValues` 기본값 두 곳 모두 아래 값으로 바꿔 주세요.

| 필드 | 현재 | 변경 |
|---|---|---|
| unlimitHpGrowthPerWave | 0.10 (x1.10/웨이브) | **0.15 (x1.15/웨이브)** |
| unlimitBatchSizeAddPerWave | +1그루 | **+2그루** |
| unlimitBatchSecondsAddPerWave | -0.02초 | **-0.05초** |
| unlimitMinBatchSeconds | 1.0초 | 1.0초 유지 |

예시(Unlimit-10): HP 896 -> **1,337**, 묶음 30 -> **39그루**, 주기 2.32 -> **2.05초**(초당 생성 12.9 -> 19.0그루). 주기 하한 1.0초 도달은 Unlimit-31.

## 반영 후
- 언리밋 모드 HUD 목표 처치 수가 Unlimit-9부터 100으로 표시됩니다(이전 960).
- 기획서 md(`Roguelike_Planning_v2.9.md`)와 UI 작업 목록은 요청 시에만 갱신합니다.
- 반영 전까지는 현재 값(960 고정, 기존 난이도 수치)으로 동작합니다.
