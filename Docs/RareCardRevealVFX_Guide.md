# 희귀 카드 등장 연출 (UI_RoguelikeRareRevealFx) 가이드

로그라이크 카드 선택 화면(`UIView_RoguelikeCardSelect`)에서 희귀 카드(에메랄드 · 프리즘 · 루비)가 처음 뒤집혀 앞면이 되는 순간의 연출입니다. 일반 카드는 예전과 똑같이 뒤집히고(화면·동작 변화 없음), 희귀 카드만 두근 → 뒤집힘 → 팡 · 타다다닥 → 유지 순서로 진행됩니다.

- 계획서: `Docs/Roguelike/_Work/RareCardReveal_Plan.md` (git 제외 폴더)
- 값·곡선의 기준: 시뮬레이션 시안 `template.html`의 `popStyle 'G'` (계획서와 시안이 다르면 시안이 기준)
- 렌더·그리는 방식은 `Docs/LootRevealGlowVFX_Guide.md`와 같은 규약(구운 알파 텍스처 + 정점 알파에 HDR 세기)입니다.

---

## 1. 연출 (희귀 카드 시작 = 0초)

| 구간 | 내용 |
|---|---|
| ① 두근 (0 ~ 0.45초) | 뒷면에 등급색 실루엣, 0 / 0.2 / 0.34초에 세 번 박동, 박동마다 가로 1px 흔들림(3/30초), 카드 가장자리 링 발광. 이 구간부터 유지 시작 전까지 **다른 카드는 어둡게** |
| ② 뒤집힘 (0.45 ~ 0.95초) | 가로 폭이 줄었다 늘며 뜀 10px. 앞면이 드러나는 지점 R = 0.65초 |
| ③ 팡 (R 직후) | 후광 3겹, 잔상 2장, 충격 링 1개, 광선 12줄(6그룹 x 2), 파편 22개, 하얀 번쩍임 |
| ④ 타다다닥 (0.99초 ~ 1.394초 만남) | 카드 둘레를 두 줄기가 달려 맞닿음(`MeetHalo` · `MeetLine`) |
| ⑤ 유지 (1.594초 ~) | 등급색 후광·가장자리 반짝임이 계속 이어짐 (`Bind` · `Unbind` · `PlayIntro`로 멈춤) |

- 시작 지연(`UIView_RoguelikeCardSelect.ComputeIntroDelays`): 희귀가 없으면 예전과 같은 `i * 0.13초`. 희귀가 있으면 일반 카드를 0, 0.13, ... 초에 먼저 뒤집고, **희귀 카드는 마지막 일반 카드 시작 + 0.3초**, 희귀가 여럿이면 앞 희귀 시작 + 0.9초씩.
- 두근 ~ 유지 시작 구간은 슬롯이 `RareSpotlightEvent(슬롯, 켬/끔)`을 내고, 뷰가 `ApplyDimming`에서 그 카드를 뺀 나머지를 어둡게 합니다(호버 포커스 어둡게보다 우선).
- 시간은 `Time.unscaledDeltaTime`.

## 2. 연결

| 위치 | 내용 |
|---|---|
| `UI_RoguelikeCardSlot.PlayIntro(float _delay, bool _bRare)` | 희귀 + `rareFx` 연결이면 `rareFx.Play`. 아니면 일반 카드와 동일 |
| `UI_RoguelikeCardSlot.UpdateRareFx` | 매 프레임 `rareFx.Tick(dt, 슬롯 위치, 알파, 뒤집힘 폭, 뜀, 호버 위치 오프셋, 앞면 여부)`. 실루엣 색이 바뀌면 `card.SetSilhouette` |
| `UI_RoguelikeCardSlot.StopRareFx` | `Bind` · `Unbind` · `PlayIntro` · 두근 중 선택(공개 전)에서 즉시 멈춤 + 실루엣 해제 + 스포트라이트 해제 알림 |
| `UI_RoguelikeCard.SetSilhouette` | 기존 `flashImage`를 덮기 머티리얼(`M_UILootGlowTint`)로 잠시 바꿔 카드 모양 그대로 한 색을 덮음. 끝나면 되돌림 |
| `UI_RoguelikeCard.PlayGradeReveal(bool)` · `UI_RoguelikeCardAura.SetRevealedImmediate` | 연출이 정한 시점에 등급 겉면·오라를 공개 상태로 맞춤 |
| `UIView_RoguelikeCardSelect` | 시작 지연 · 스포트라이트 어둡게 (플랜 4-7) |

- 호버로 카드가 뜨면(`FlipLiftPixels` · `VisualOffset`) 층 루트가 같이 따라갑니다.
- Application Layer와 SoundID는 건드리지 않았습니다. 효과음 지점은 `UI_RoguelikeRareRevealFx.cs` 안에 `[효과음 지점]` 주석으로 표시해 두었습니다.

## 3. 계층과 그리는 순서

`UIView_RoguelikeCardSelect.prefab`의 `Content` 아래에 슬롯마다 한 쌍.

```
Content
├ RareFxBack_0..3   [자식 Canvas, UI_RoguelikeRareRevealFx]  ← 맨 앞 자식(카드들보다 먼저 그림)
│   HaloDeep, HaloMain, HaloCore, PrismBlob_0..5, WhiteCore, Rays/Ray_0..5
├ Slot_0..3 ...
└ RareFxFront_0..3  [자식 Canvas]                            ← 맨 뒤 자식(카드 위)
    EdgeGlow, GhostWide_0/1, GhostCore_0/1, ShockMain, ShockCore, MeetHalo, MeetLine, SoftGlow, Pixels
```

- 층 루트는 카드 중심(슬롯 위치 + (25, 41 + 호버 뜀))에 놓이고 자식 위치는 루트 기준 px(y 위)입니다. 시안 좌표(y 아래, 카드 좌상단 기준)는 (u - 25, 41 - v)로 옮깁니다.
- 자식 Canvas 격리라 매 프레임 변화가 카드·텍스트 배칭을 다시 만들지 않습니다.

## 4. 그리는 방식과 성능

| 항목 | 방식 |
|---|---|
| 부드러운 빛 | 빌더가 구운 텍스처 `Image`. 매 프레임에는 위치·크기·색만 씀. 머티리얼 인스턴스 없음 |
| HDR 세기 | rgb = 감마 색, 알파 = 세기 / 2.5 (셰이더가 rgb x 알파 x 2.5). 가산 `_Mode 1` = `M_UILootGlowAdd`, 덮기 `_Mode 0` = `M_UILootGlowTint` |
| 링 | 9-슬라이스 스프라이트(PPU 32). 크기는 `sizeDelta`로만 바꿈 |
| 크기 | 나머지는 `localScale`. 슬롯마다 지난 값 캐시, 바뀐 값만 씀 |
| 입자 | `UI_RoguelikeRareRevealParticles`(MaskableGraphic) 2개: `SoftGlow`(번지는 점, `M_UILootGlowAdd`)와 `Pixels`(픽셀 입자, `M_UILootParticles`). 쿼드 배열 768칸을 재사용하고 부족하면 두 배로 늘림 |
| 할당 | 초기화 이후 0 |

측정(편집기 Mono, 카드 4장, 프레임당 슬롯 갱신 + 카드 모션 구간 / `Canvas.ForceUpdateCanvases` 구간, JIT 경고 프레임 포함):

| 상황 | 갱신 평균 | 갱신 최대 | 캔버스 리빌드 평균 | GC 할당 |
|---|---|---|---|---|
| 일반 카드 4장 (등장 0 ~ 4.3초) | 1.4 µs | 20 µs | 7.4 µs | 0 B |
| 희귀 1장 프리즘 (등장 0 ~ 4.3초) | 10.4 µs | 58 µs | 0.067 ms | 0 B |
| 희귀 1장 루비 (등장 0 ~ 4.3초) | 7.9 µs | 53 µs | 0.053 ms | 0 B |
| 희귀 4장 (등장 0 ~ 6.3초) | 22.6 µs | 79 µs | 0.145 ms | 0 B |
| 유지 구간 희귀 1장 (1000프레임 평균) | 6.0 µs | 516 µs(첫 호출 JIT로 추정) | 0.037 ms | 0 B |
| 유지 구간 희귀 4장 (1000프레임 평균) | 14.8 µs | 79 µs | 0.093 ms | 0 B |

- 입자 쿼드 최대 사용량은 한 층 369개(희귀 4장이어도 층당 최대 같음)로 배열 768칸 이내입니다.
- 첫 호출 한 번(JIT)이 1 ~ 3 ms까지 튀는 것은 편집기 측정이며 빌드에서는 해당하지 않습니다.
- 위 수치는 `cam.Render()`(GPU 제출)를 뺀 CPU 구간입니다.

## 5. 셰이더 · 머티리얼

새 셰이더·머티리얼은 만들지 않았습니다(전리품 발광 것을 공유).

| 머티리얼 | 용도 |
|---|---|
| `M_UILootGlowAdd` | 후광 · 광선 · 링 · 잔상 · 번지는 점 (가산) |
| `M_UILootGlowTint` | 카드 위 실루엣(덮기) |
| `M_UILootParticles` | 픽셀 입자 |

- 빌더 실행 전에 `LumberBoy/VFX/Build Loot Reveal Glow`가 먼저 실행돼 있어야 합니다(머티리얼이 없으면 빌더가 오류를 냅니다).

## 6. 파일

| 경로 | 역할 |
|---|---|
| `Assets/Scripts/Presentation Layer/UISystem/UIView/Roguelike/UI_RoguelikeRareRevealFx.cs` | 이펙트 본체(타임라인 · 층 갱신 · 입자) |
| `.../UI_RoguelikeRareRevealParticles.cs` | 입자 그래픽 |
| `.../UI_RoguelikeCardSlot.cs` · `UI_RoguelikeCard.cs` · `UI_RoguelikeCardAura.cs` · `UIView_RoguelikeCardSelect.cs` | 연결 |
| `Assets/Scripts/Presentation Layer/VFX/Editor/RareCardRevealBuilder.cs` | 메뉴 `LumberBoy/VFX/Build Rare Card Reveal` (텍스처 굽기 + 뷰 프리팹 층 생성·연결, 이름으로 재사용해 다시 실행해도 안전) |
| `Assets/Graphics/VFX/RareCardReveal/` | 텍스처 18장: `Dot` · `DiscLinear` · `HaloCore/Main/Deep` · `Rays0..5` · `GhostWide/Core` · `EdgeRing` · `ShockMain/Core` · `MeetHalo/Line` |
| `Assets/Prefabs/Roguelike/UI/UIView_RoguelikeCardSelect.prefab` | 층 8개 + 슬롯 4개의 `rareFx` 연결 |

- 카드 뒷면(`Card_Back`) 모양이나 크기(50 x 82)가 바뀌면 빌더를 다시 실행해야 잔상 텍스처가 맞습니다.
- 링 선 중심 오프셋(`EdgeCentreOffset` 14.5 · `ShockCentreOffset` 21 · `MeetCentreOffset` 25)은 이펙트와 빌더가 같은 공개 상수를 씁니다. 한쪽만 바꾸면 링이 카드에서 벗어납니다.

## 7. 시안과 다른 점

- 후광은 시안의 그라디언트를 세 겹 가산 원반(`HaloCore` · `HaloMain` · `HaloDeep`)으로 근사했습니다.
- 링 모서리 반경이 텍스처에 고정입니다(시안은 크기에 따라 변함). 충격 링의 굵은 층·가는 층도 고정 프로필 텍스처입니다. 충격 링 그림자색은 한 색으로 근사했습니다.
- 두근 가장자리 링의 안쪽 번짐은 3.5px에서 잘립니다.
- 픽셀 입자층은 불투명(`UIPixelGlow`)이라 시안의 반투명 번짐 입자와 다릅니다.
- **그리는 순서**: 뒤 층이 `Content` 맨 앞이라 제목판·새로고침 줄(카드 뒤에 있는 것들)이 후광 위에 그려질 수 있고, 시안에서는 빛이 그 위에 덮입니다. 앞 층(링 · 잔상 · 입자)은 모두 위에 그려집니다.

## 8. 픽셀 규칙 검수

| 규칙 | 적용 | 판정 |
|---|---|---|
| JAGGIES / DOUBLE PIXEL / HUGGING | 링은 선 1px(굵은 링 3px) 격자 정렬: 두근 링 선 중심이 카드 칸 가장자리에서 바깥 0.5px라 선 픽셀이 칸 바로 바깥 1px에 놓이고, 카드 그림 윤곽과는 시안과 같은 간격(렌더 픽셀 측정으로 확인) | 시안과 일치 |
| 빛(후광 · 광선) | 쌍선형 텍스처 | 해당 없음 |
| 입자 예외 | `Pixels` 층은 `UIPixelGlow`로 그리는 정수 픽셀 입자(시안의 입자 모양 규칙을 따름, 모양별 픽셀 검수는 하지 않음) | 예외 범위 |

## 9. 검증 기록과 주의

- 컴파일 경고·오류 0 (임시 하네스 삭제 후 `AssetDatabase.Refresh` 뒤 콘솔 오류 0 · 경고 0).
- **일반 카드만 있을 때 예전과 똑같음**: 편집 모드 임시 하네스로 변경 전 기준 렌더(67프레임, 30fps)와 변경 후 렌더의 프레임별 MD5가 전부 일치하고, 하네스를 지우기 직전에 다시 돌려도 앞 67프레임이 같았습니다.
- 편집 모드 임시 하네스(삭제함, 사본은 작업 폴더 스크래치패드): 프리뷰 씬에 `Canvas.prefab`(Screen Space - Camera)과 뷰 프리팹을 두고 30fps로 시간을 직접 넣어 `Canvas.ForceUpdateCanvases()` 뒤 `Camera.Render()`로 렌더. 640 x 360, 1280 x 720 HDR + `Global Volume Profile`(블룸) 렌더로 시안 시트와 대조했습니다. 편집 모드에서 `Awake`를 `SendMessage`로 부르는 탓에 하네스 실행마다 `NullReferenceException`이 한 번 났지만 프레임 렌더에는 영향이 없었습니다(하네스 한정).
- 확인한 시나리오: 에메랄드 · 프리즘 · 루비 각 3번째 칸, 프리즘 1번째 칸 + 호버, 희귀 2장(칸 2 · 4: 시작 간격 약 0.8 ~ 0.9초, 스포트라이트 어둡게 약 80% 확인).
- **플레이 모드는 아직 확인하지 않았습니다**(실제 카드 제시 흐름에서 광클 선택 · 새로고침 · 게임패드 포커스 · 창 크기 변경 · 18개 언어, 효과음 연결).
- 호버 중 연출 · 두근 중 선택 · 새로고침 중단은 코드 경로와 편집 모드 호버 렌더만 확인했고 실제 입력으로는 확인하지 않았습니다.
