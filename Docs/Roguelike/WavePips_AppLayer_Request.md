# 웨이브 진행 점 지역 경계 전달 요청 (Application Layer)

## 현황
- `UI_RoguelikeWaveLabel`의 진행 점(30칸)은 지역이 바뀌는 웨이브 앞에서 간격을 2px 더 벌린다.
- 지역 경계(시작 웨이브)를 UI 상수 `RegionStartWaves = { 8, 16, 23 }`로 직접 들고 있다(1웨이브 제외).
- 이 값은 `Assets/Data/Roguelike/RoguelikeBalance.asset`의 `tiers[].startWave`와 수동으로 맞춘 것이라, 밸런스가 바뀌면 어긋난다.

## 요청
- `RoguelikeUICoordinator.WaveStarted` -> `UIView_RoguelikeHUD.SetWave` 경로로 `RoguelikeBalanceSO.tiers[].startWave` 목록(1웨이브 제외, 오름차순 정수 배열)을 함께 전달해 주세요.
- UI는 받은 배열을 `UI_RoguelikeWaveLabel`에 넘겨 진행 점 간격 계산에 사용합니다.

## 반영 후
- UI의 `RegionStartWaves` 상수를 삭제하고 전달받은 값으로 대체합니다.
- 반영 전까지는 현재 상수로 동작합니다.
