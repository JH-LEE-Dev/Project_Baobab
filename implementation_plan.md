# [v5 추가] HUD_Loot(포션 슬롯 HUD) 제거

## A. 현황 (전수 추적)
| 대상 | 위치 | 내용 |
|---|---|---|
| `HUD_Loot.cs` | `UISystem/UIView/HUD/` | 포션 슬롯 한 칸. 외부 호출은 `UIView_HUD`뿐(`PlayUsePotionMotion` · `SetSlotActive`는 호출처 0 — 포션 키 제거 커밋 이후 잔재) |
| `HUD_Loot.prefab` | `Prefabs/UI/HUD/Loot/` | `HUD_UI.prefab`의 `hudLootPrefab` 하나만 참조 |
| `UIView_HUD.cs` | HUD 뷰 | `hudLootPrefab` · `hudLoot` · `lootDataProvider` · `DependencyInjection(ILootDataProvider)` · `OnLootAcquired` · `Init_HUDLoot` · `Refresh`의 소지 전리품 복구 · `ChangedActiveStateLoot` · `HUDGoDown/Up`의 `hudLoot?.` 호출. `lootItemTypeDataBase`는 선언만 있고 이미 미사용 |
| `GameplayUIManager.cs` L55 (Application Layer) | UI 매니저 | `hudUI.DependencyInjection(... as ILootDataProvider)` — HUD_Loot 전용 주입 |
| **유지** | | `HUD_LootReveal`(전리품 상호작용 화면, ScreenModal) · `ILootDataProvider` · 전리품 시스템 · 세이브는 그대로 (다른 곳에서 사용) |

## B. 작업
1. **`UIView_HUD.cs`** — A표의 HUD_Loot 관련 필드 · 메서드 · 호출을 모두 제거(미사용 `lootItemTypeDataBase` 포함). `Refresh`는 장비 HUD 갱신만 남김. 다른 기존 변수명 · 동작 불변.
2. **주입 경로** (아래 질문으로 결정)
   - (권장) `UIView_HUD.DependencyInjection` 삭제 + `GameplayUIManager` L55 한 줄 삭제 (Application Layer 1줄 수정)
   - (대안) Application Layer는 건드리지 않고 `DependencyInjection`을 빈 메서드로 남김(잔재 코드 1개)
3. **파일 삭제**: `HUD_Loot.cs` / `.meta`, `HUD_Loot.prefab` / `.meta`, 빈 폴더 `Prefabs/UI/HUD/Loot` / `.meta` (git으로 복구 가능).
4. **`HUD_UI.prefab` 재저장** — 지워진 직렬화 필드(`hudLootPrefab` · `lootItemTypeDataBase`) 잔재 정리.
5. `HUD_EquipmentAxe.cs` L273 주석의 "HUD_Loot와 같은" 문구를 실제 내용으로 정정.
6. 컴파일 경고 0 · Missing Script/참조 0 확인.

---

# [v4 추가] 게이지 좌우 떨림 연출 제거

## A. 현황
- 떨림은 100%가 아니라 **4단계(75% 이상)부터** 켜진다(`stageSettings[4].bShake = true`). `HUD_HeatGauge.Update → UpdateShake`가 12Hz로 `shakeTarget`(Bar/Shake)을 ±1px 옮긴다.

## B. 작업 (기능 자체를 삭제, 잔재 정리)
1. **`HUD_HeatGauge.cs`**
   - 삭제: `HeatStageSettings.bShake`, `shakeTarget` 필드, `ShakeTicksPerSecond`, `shakeBasePosition` · `shakeTick` · `shakeOffset`, `UpdateShake()`, `Update()`(떨림 전용), Initialize의 기준 위치 캐시.
   - 클래스 주석 · stageSettings 기본값에서 떨림 항목 제거.
2. **`HUD_HeatGaugeBuilder.cs`** — `Shake` 컨테이너 제거. `HeatBar` · `Sparks`를 `Bar` 바로 아래로(그리는 순서 · 위치 동일). `shakeTarget` 연결 제거.
3. 빌더 재실행으로 프리팹 갱신, 컴파일 경고 0 확인.

## C. 영향
- 다른 연출(바운스 · 번쩍임 · 불티 · 셰이더 4단계 테두리 깜빡임)은 그대로. 테두리 깜빡임도 없애려면 별도 요청.

---

# [v3 추가] 과열 게이지 단계별 셰이더 · 불티 구현 계획 (시뮬레이션 승인안)

## A. 범위
- **게이지만** 적용한다. 단계 구분(0 / 1~24% / 25~49% / 50~74% / 75% 이상)은 `HUD_HeatGauge` 안에서 최대치 대비 비율로 따로 계산한다.
- `OverheatComponent.Stage`(캐릭터 오라 · VFX 단계)는 **수정하지 않는다**(절대값 1·25·50·75 그대로).

## B. 구성 (게이지 Bar 하위)
```
Bar (슬라이드 · 바운스 대상, 그대로)
 ├ Shake            ← 4단계 1px 떨림만 담당(슬라이드 · 바운스와 분리)
 │  ├ HeatBar       Image(HeatBar_Base 9x50) + 신규 셰이더 머티리얼. 테두리 · 빈칸 달아오름 · 용암 채움 · 번쩍임을 한 장에서 그린다
 │  └ Sparks        RawImage 21x80 + HUD_HeatSparks (불티 '.' '+')
 └ LevelNumber      (그대로, 떨지 않음)
```
- 기존 `Base` · `Fill` · `Flash` 이미지 3장 → `HeatBar` 1장으로 통합(드로우 감소). 빌더가 재생성한다.

## C. 파일별 작업
1. **신규 `Assets/Shaders/UI/UI_HeatGaugeFill.shader`** (UI 기본 셰이더 규약: 스텐실 · ClipRect · AlphaClip 유지)
   - 픽셀 좌표 = 스프라이트 UV 범위(`_SpriteUVRect`, 아틀라스 대응)를 9x50 격자로 내림. 바닥 스프라이트 색으로 테두리(흰) / 검은 줄 / 안쪽을 구분.
   - 안쪽 & 채움 높이 미만: 높이 단계 + 값 노이즈 2겹(위로 흐름) + 기포 + 꼭대기 백열 테두리 → 팔레트 8색 중 하나(단계별 상한 `_PaletteCap`). MAX면 팔레트 +1 펄스.
   - 안쪽 & 채움 위 6px: 바닥색 → 달아오름 색 보간(`_FrameHeat`, 맥동).
   - 4단계: 흰 테두리가 채움 높이 +4px까지 주황으로 깜빡임.
   - 시간은 `_TickRate`(기본 24)로 끊는다. 해시 노이즈만 쓰고 텍스처 샘플은 바닥 스프라이트 1회.
   - `_Flash`(0~1)로 채움 픽셀을 흰색 보간 → 기존 Flash 이미지 대체.
2. **신규 `M_HeatGaugeFill.mat`** (`Assets/Graphics/HUD/Bar/HeatBar/`) — 팔레트 8색 · 노이즈 값 인스펙터 조정 가능.
3. **신규 `HUD_HeatSparks.cs`** (Equipment 폴더)
   - 구조체 배열 풀(용량 64), 30fps 고정 스텝, unscaled 시간. 단계별 초당 생성 수, 4단계와 단계 상승 버스트는 바 밖으로 튐(숫자 위까지), 그 외는 바 안쪽 빈칸에서만.
   - CPU 텍스처 21x80(Point)에 직접 찍음. 입자가 없고 직전도 비었으면 업로드 생략.
4. **수정 `HUD_HeatGauge.cs`**
   - 단계 계산을 비율 기준으로 교체(`GetGaugeStage`), 단계별 설정 구조체 배열(팔레트 상한 · 흐름 속도 · 기포 · 달아오름 · 기준 열 · 불티 수 · 떨림) 직렬화.
   - 머티리얼 인스턴스 1개 생성/파괴, 값이 바뀔 때만 `SetFloat`(채움 px · 비율 · 단계 · MAX · Flash).
   - 단계 상승 시 기존 바운스 + 번쩍 + **불티 버스트**. 4단계 & MAX 아님이면 수치 숫자 살짝 따뜻한 색.
   - 4단계 떨림: `Shake` 자식만 12Hz로 ±1px(정수).
   - **잔재 정리**: `flashImage` · `stageFillColors` · Filled `fillAmount` 경로 제거(`fillImage`는 이름 유지, HeatBar 이미지를 가리킴).
5. **수정 `HUD_HeatGaugeBuilder.cs`** — B 계층으로 생성, 머티리얼 · 스파크 참조 연결. 실행 후 프리팹 재생성.

## D. 최적화 · 규칙 점검 (VFX_Procedural_Pixel_Guide 10장 · DungeonVFXOptimization 8장)
- GC 0(람다 · LINQ 없음, 구조체 배열, 델리게이트 캐시), 머티리얼 세터 · 텍스처 업로드는 바뀔 때만.
- `_CameraSortingLayerTexture` 미사용 → 캡처 게이트 등록 불필요.
- 픽셀 규칙: 바 · 테두리는 스프라이트 격자 그대로(선 · 타원 없음, JAGGIES/DOUBLE/HUGGING 해당 없음). 불티는 입자 예외(`.` `+`).
- 셰이더 시간은 `_Time`(게임 시간) 기준 → 일시정지 중엔 흐름이 멈춤(HUD가 숨는 상황이라 허용).

## E. 검증
- 컴파일 경고 0, 단계별(0·1·2·3·4·MAX) 정지 렌더 캡처 비교, 플레이 모드 확인은 사용자 몫으로 남김.

---

# [v2 추가] 과열 최대치 가변(N 상승) 대응 계획

## A. 현황 (코드 확인)

| 항목 | 위치 | 상태 |
|---|---|---|
| 최대치 원본 | `StatComponent.overheatMaxValue` (L170, 기본 100) | 퍼블릭 필드만 있음. **런타임에 바꾸는 메서드 · 변경 이벤트 없음** |
| 최대 판정 | `OverheatComponent.IsMax` / `MaxScaled` | 매번 스탯을 읽으므로 값 자체는 가변 대응됨. 불씨 수집 차단(`InDungeonObjectManager` L2352 `IsOverheatMax`)도 실시간으로 따라감 |
| 게이지 | `HUD_HeatGauge.ApplyValues` | 채움 비율 = 수치 / MaxValue로 이미 가변 계산. 단, **수치가 바뀔 때만 갱신** → 최대치만 오르면 바가 안 줄어들고 MAX 강조도 안 풀림 |
| 숫자 | `HUD_HeatNumber` | 999까지 그림 |
| 이펙트 단계 | `OverheatComponent.StageStartLevels {1,25,50,75}` | 절대값 고정 (채움 색 · 오라 단계가 여기서 나옴) |
| 화상 구간 | `StatComponent.overheatTierThresholds {34,67,100}` | 절대값 고정 |

## B. 작업

1. **`StatComponent.cs`** — `IncreaseOverheatMaxValue(float _amount)` 추가(특성 Execute/Undo가 부를 진입점, 다른 Increase 계열과 같은 형태). 값이 바뀌면 `OverheatMaxValueChangedEvent(float)` 발행.
2. **`OverheatComponent.cs`** — 위 이벤트를 받아
   - 최대치가 **내려가서** 현재 수치가 넘치면 새 최대치로 잘라내고 `Refresh()`(단계 · 공속 배율 재계산)
   - 인터페이스 이벤트 `OverheatMaxChangedEvent(float)` 재발행
   - 클래스 주석의 "0~100" 표기를 "0~최대치"로 정정
3. **`IOverheatComponent.cs`** — `OverheatMaxChangedEvent` 추가, 주석 "0~100" 정정.
4. **`HUD_HeatGauge.cs`** — `OverheatMaxChangedEvent` 구독/해제. 최대치가 바뀌면
   - 채움 비율 재계산(최대치 상승 시 바가 1px 단위로 **내려앉는** 트윈)
   - MAX였다가 풀리면 숫자 색을 흰색으로 복귀
   - 상승 연출: 바 전체 위로 늘어나는 바운스 + 번쩍(“그릇이 커졌다” 피드백)
5. 아래 C의 결정에 따라 숫자 표시 · 단계 기준 반영.

## C. 결정 필요 (질문)
- 수치 표시: 지금처럼 **현재값만**("100") vs **현재/최대**("100/150")
- 채움 색 단계(1·25·50·75)와 오라 단계: **절대값 유지** vs **최대치 비율**(1% · 25% · 50% · 75%)
  - 화상 구간(34·67·100)은 전투 밸런스라 이번 범위에서 건드리지 않음(필요 시 별도)

## D. 범위 제외
- 최대치를 올리는 특성 자체(SkillCommand) — 진입점 메서드만 마련

---

# 과열 게이지 HUD (도끼 옆) 구현 계획표

## 0. 현황 분석 (코드로 확인한 사실)

| 항목 | 위치 | 내용 |
|---|---|---|
| 과열 데이터 | `OverheatComponent.cs` / `IOverheatComponent.cs` | `OverheatLevel`(0~100 정수), `Stage`(0~4), `IsMax`, `DamageMultiplier`(1+lv×0.02, 최대 x3.00), `AxeAttackSpeedMultiplier`(1+lv×0.005, 최대 x1.50). 이벤트 `OverheatLevelChangedEvent` / `OverheatStageChangedEvent` / `OverheatStateChangedEvent` |
| 리셋 시점 | `Character.SetWhereIsCharacter` L413 | 씬 전환(던전 입장·귀환·재도전)마다 `ResetOverheat()` |
| 장비 HUD 바인딩 | `HUD_Equipment.BindingRef` | 이미 `OverheatStateChangedEvent`만 구독(아우라용). Level/Stage 이벤트는 미구독 |
| 표시 판단 상위 | `UIView_HUD.SetCurrentMapType` / `HUDGoUp` / `HUDGoDown` / `SetEquipmentHiddenForRun` | 장비 HUD는 `moveHUD` 자식이라 맵 전환 GoDown/GoUp 모션에 함께 실려 움직임 |
| 호출 순서 | `GameplayUICoordinator` | 던전 선언 → `SetCurrentMapType(mapType, forestType)` → `PopupUIUp` → `HUDGoUp(onCompleted)` |
| "예열" 특성 | 전수 검색 | **존재하지 않음** (`bOverheatPermanent`는 옛 지속시간제 미사용 필드) → 연결 자리만 마련 |
| 리소스 | `Graphics/HUD/Bar/HeatBar/` | Base 9×50, Fill 9×50(흰색 → 색은 코드로 틴트), Damage/AttackSpeed 아이콘 16×16, PPU 32, Point |
| 숫자 폰트 | `Graphics/Font/SmallCurrencyNum.png` | 96×8, 12칸(1~9, 0, `.`, `x` 순서로 보임 — 구현 시 `_10`·`_11` 글리프 실측 확인) |
| 기존 숫자 합성기 | `HUD_BuffNumberFont` (`HUD_BuffPixelUtil.cs`) | 숫자 0~9만 지원, `.`/`x` 미지원, 버프·로그라이크·수리키트가 공유 |

## 1. 등장 / 퇴장 타이밍 규칙

**표시 조건** `bHeatGaugeRequired = (MapType.MagmaForest == currentMapType || true == bPreheatActive) && false == bEquipmentHiddenForRun`

| 시점 | 처리 |
|---|---|
| 던전 선언 `SetCurrentMapType` | 조건 true → 게이지를 **화면 밖 위치에 즉시 대기**(값 0, x1.00으로 초기화). false → 즉시 숨김 |
| `HUDGoUp` 완료 콜백 | 조건 true면 **슬라이드 인**. (HUD가 올라오는 모션과 겹치지 않고, 다 올라온 뒤 "툭" 튀어나오게) |
| `HUDGoDown` (던전 이탈·재도전·결과창) | 보이는 중이면 **슬라이드 아웃** (moveHUD 하강과 동시에 진행) |
| 마을 진입 | 즉시 숨김 (장비 HUD 자체가 꺼짐) |
| 로그라이크 `SetEquipmentHiddenForRun(true)` | 즉시 숨김. 이후 "예열" 특성이 로그라이크에도 붙으면 이 조건만 풀면 됨 |
| 던전 도중 예열 상태 변경 `SetPreheatActive` | 조건 결과가 바뀌면 그 자리에서 슬라이드 인/아웃 |
| 어빌리티 트리 등으로 HUD 비활성화 | `OnDisable`에서 트윈 Kill + 최종 위치로 스냅 (중간 위치로 남지 않게) |

**"예열" 연결 자리** (특성 미존재)
- `UIView_HUD.SetPreheatActive(bool _bActive)` 퍼블릭 진입점 1개 + 주석으로 연결 방법 명시.
- 실제 특성이 생기면 특성 SkillCommand → 신호/코디네이터 → 이 메서드 호출만 추가하면 됨. 과열 수치 선부여 로직(입장 시 `AddOverheat`)은 이번 범위에서 **구현하지 않음**.

## 2. 레이아웃 (시안 기준, 도끼 HUD 왼쪽)

```
           [100]            ← 과열 수치 (SmallCurrencyNum, 정수 스케일 ×2)
 [🗡] x1.00  ┃  ┃            ← 공격력 아이콘 + 배율
 [≫] x1.00  ┃▓▓┃   [도끼]   ← 공격속도 아이콘 + 배율
             ┗━━┛            ← HeatBar_Base + HeatBar_Fill (세로, 아래→위)
```
- 루트 `HUD_HeatGauge`(RectTransform)를 `HUD_Equipment` 프리팹 안, 도끼 HUD 왼쪽에 앵커. 슬라이드는 루트 하위 `SlideRoot`만 움직여 레이아웃 기준점은 고정.
- 픽셀 정수 좌표·정수 스케일 유지(Point 필터 번짐 방지).

## 3. 연출 설계 (쫀쫀·쫀득)

| 연출 | 내용 |
|---|---|
| 슬라이드 인 | 화면 밖 → 목표 지점 `OutBack`(오버슈트) 이동 + 도착 순간 스쿼시(가로 1.15/세로 0.85 → `OutElastic` 복귀). 바 → 아이콘 줄 순서로 0.05초 시차(Stagger) |
| 슬라이드 아웃 | 살짝 반대 방향으로 움찔(Anticipation 0.06초) → `InBack`으로 화면 밖 |
| 수치 증가 | 숫자 펀치 스케일(정수 스냅), 게이지 채움은 `OutQuad`로 차오름(1px 단위 양자화) |
| 배율 변경 | 배율 텍스트 작은 펀치 + 흰색 번쩍 1회 |
| 단계 변경(1/25/50/75) | 채움 색 전환(노랑 → 주황 → 주홍 → 백열 빨강) + 바 전체 바운스 |
| MAX(100) | 바 흰색 번쩍 + 숫자 색 고정 강조 |
| GC | 트윈 콜백은 캐싱된 메서드 참조, 문자열 조립 없음(글리프 직접 합성), 값 변경 이벤트 구동(Update 폴링 없음) |

## 4. 파일별 작업

### 신규
1. **`HUD_HeatGauge.cs`** (`UISystem/UIView/HUD/Equipment/`)
   - `Initialize()`, `Bind(IOverheatComponent)`, `Unbind()`, `SetVisible(bool _bVisible, bool _bAnimate)`, `ResetValues()`
   - 게이지 Fill(Image Filled Vertical) 갱신, 단계 색, 슬라이드/펀치 시퀀스, `OnDisable` 정리
2. **`HUD_HeatNumber.cs`** (같은 폴더) — RawImage 1장에 `0~9`, `.`, `x` 글리프 합성. `SetInt(int)`, `SetMultiplier(float)`(소수 둘째 자리, `x1.00` 형식). `HUD_BuffPixelUtil.TryGetSpritePixels` 재사용, 기존 `HUD_BuffNumberFont`는 **수정하지 않음**(공유 클래스 영향 차단).
3. **`Editor/HUD_HeatGaugeBuilder.cs`** — 기존 `HUD_RepairKitCountBuilder` 패턴대로 메뉴 한 번에 `HUD_Equipment.prefab` 안에 게이지 계층·스프라이트·참조 연결을 생성/재생성.

### 수정
4. **`HUD_Equipment.cs`** — `[SerializeField] HUD_HeatGauge heatGauge` 추가, `BindingRef`에서 `heatGauge.Bind(overheatState)`, `UnbindOverheat`에서 해제, `SetHeatGaugeVisible(bool, bool)` 중계 메서드.
5. **`UIView_HUD.cs`** — `bPreheatActive` 상태 변수, `SetPreheatActive(bool)`, `IsHeatGaugeRequired()` 판정, `SetCurrentMapType`/`HUDGoUp` 완료/`HUDGoDown`/`SetEquipmentHiddenForRun`에 등장·퇴장 호출 삽입. 기존 변수명·동작 유지.

### 영향도 검수 대상
- `HUD_Equipment` 기존 아우라 구독 흐름 유지 확인
- `HUDGoUp` 콜백 체인(`HUDGoUpCompleted`) 동작 불변 확인 — 내부 완료 시점에서만 게이지 호출 추가
- 로그라이크 `RoguelikeUICoordinator` 숨김 경로에서 게이지가 켜지지 않는지 확인

## 5. 이번 범위 제외
- "예열" 특성 자체(SkillCommand, 스탯, 입장 시 과열 선부여)
- 사운드 신규 ID (기존 UI 사운드 재사용 여부는 구현 시 별도 질문)
