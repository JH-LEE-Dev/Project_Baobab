# 마을 전리품 기둥 빛 (VFX_LootPillarGlow) 가이드

마을의 전리품 기둥 위에서, 전리품 뒤로 계속 은은하게 빛나는 유지 이펙트입니다. 기존 '등'(`ItemAuraOrbitController`의 중앙 원형 글로우 하나)이 정지한 빛 덩어리라 심심하다는 요청으로 새로 만들었습니다.

- 승인된 시뮬레이션 시안 2: https://claude.ai/artifact/4sMdnLqzcyVM8bs7L6BHQ5 (시안 1에서 빛살을 빼고, 떠오르는 입자를 단색으로 바꾼 안)
- 구현 후 Unity 실제 렌더(30fps, 2.6초): https://claude.ai/artifact/YFhHYTrhovH54giPREDaXK
- 적용 대상: `LootPillar.prefab`을 쓰는 모든 마을 전리품 기둥

---

## 1. 구성 요소

| 요소 | 내용 | 수치(px, 1px = 1/32유닛) |
|---|---|---|
| 기둥 윗면 빛 웅덩이 | 흙 윗면 중심의 납작한 빛. 반투명 덮기 + 가산 | 덮기 24x10 (알파 0.45) / 가산 22x9 (0.55~0.70, 숨쉬기) |
| 위로 솟는 빛 줄기 | 흙 윗면에서 위로. 위로 갈수록 폭이 65%까지 좁아짐. 반투명 덮기 + 결이 위로 흐르는 가산 심(`SpriteBeamFlow`) | 폭 20, 높이 46 (윗면 아래 2px까지 텍스처) |
| 뒤 빛 원반 | 그림 중심(불투명 칸 평균)의 둥근 빛. 반투명 덮기 + 가산 | 지름 30 (0.28) / 지름 26 (0.28~0.40) |
| 실루엣 후광 | 전시 프레임마다 구운 실루엣 블러(시그마 2.2px, 알파^0.8)를 가산 | 0.42~0.70, 숨쉬기 |
| 떠오르는 픽셀 입자 | 기둥마다 7개. 웅덩이 타원 안에서 출발해 18~34px 떠오름. `.` → `+`/`●` → `◆` `+` `●` `x` → `.`. **입자 하나 = 한 색·한 밝기**. 전리품 뒤 | 주기 54~71틱(30fps) |
| 가장자리 반짝임 | 그림 경계 바깥 5곳, 2.2초마다 `.` `+` 5x5`+` `x` `.` 순서. 가운데 한 칸만 밝은 색. 전리품 앞 | 14틱 |

- 숨쉬기: 2.6초 주기. 위상은 기둥마다 다릅니다(위치 해시, 재생 시작 때 1회).
- 빛살(시안 1의 회전 빛살)은 사용자 결정으로 넣지 않았습니다.
- 입자 `●`는 시안과 같은 2x2입니다(공용 `PixelPainter.Shape`의 `●`는 4x4라 쓰지 않음). `◆` `+` `x` `.`는 공용 정의와 같습니다.

## 2. 색

- `LootDisplayObject.ApplyGrowthVisual`이 정한 아우라 색(성장 단계 색 `LootGrowthVisualData.tierAuraColors`, 단계가 없으면 종류별 `auraSettings`)을 `SetColor`로 받습니다. 색을 정하지 못하면 인스펙터 `fallbackColor`(나무 단계 금색)를 씁니다.
- 아우라 색은 **선형 HDR 값 그대로** 해석합니다(시뮬레이션과 같음). 렌더러 색은 Unity가 선형으로 바꾸므로 코드가 gamma로 바꿔 넘깁니다(`ToRendererColor`).
- 팔레트: 원색 `hdr`, 덮기 `tint` = 정규화 색을 흰색 쪽으로 55%, 밝은 심 `core` = 원색을 2.2(흰색) 쪽으로 45%.
- 입자는 정점 rgb = 정규화 색, 정점 알파 = 발광 배율 / 2.5 (`M_LootPillarParticles`: BrandWrapGlow `_GlowMin` 0, `_GlowMax` 2.5 = `ParticleGlowMax`). 인스펙터 `particleGlow`로 입자·반짝임 발광을 함께 조절합니다.

## 3. 연결

| 시점 | 호출 |
|---|---|
| 전리품 표시 (`SetLootDisplay`) | `useAuraOrbit`이 켜져 있으면 `pillarGlow.Play()`, 꺼져 있으면 `Stop()` |
| 단계 색 결정 (`ApplyGrowthVisual`) | `SetColor(색)` / 색이 없으면 `ClearColor()` |
| 첫 등장 낙하 대기 (`PrepareFirstReveal`) | `Stop()` |
| 낙하 착지·연출 종료 (`PlayRevealAura`) | `Play()` |
| 진화 연출 (`VFX_LootEvolve.UpdateAura`) | `SetEvolveModulation(크기, 세기, 흰색)` → 원반·후광이 기존 중앙 글로우와 같은 곡선으로 조였다가 터짐. 끝나면 `ClearEvolveModulation()` |

- 기존 중앙 글로우는 빌더가 `ItemAuraOrbitController.useCenterGlow`를 끄는 것으로 숨겼습니다. 컴포넌트와 `LootDisplayObject`의 기존 호출은 그대로 남아 있습니다(위성 0개라 아무것도 그리지 않음).
- 전시 애니메이션이 프레임을 바꾸면(`targetRenderer.sprite`) 이펙트가 다음 `LateUpdate`에서 후광 조각·중심·경계를 바꿉니다. 실행 순서 110으로 `LootDisplayObject`(100)가 위치를 쓴 뒤에 읽습니다.

## 4. 그리는 층과 정렬

`LootDisplayObject.ApplySortingBasis`의 전리품 오프셋을 +2에서 **+4**로 바꿨습니다.

| Order | 대상 |
|---|---|
| P | 기둥 |
| P+1 | 전리품 그림자, 기둥 아웃라인 |
| P+2 | `Back` SortingGroup (안쪽 순서 고정: 0 웅덩이 덮기, 1 웅덩이 가산, 2 빛 줄기 덮기, 3 빛 줄기 심, 4 원반 덮기, 5 원반 가산, 6 후광, 7 입자) |
| P+3 | 진화 연출 후광 메쉬(전리품 −1) |
| P+4 | 전리품 |
| P+5 | 가장자리 반짝임 |

- 이펙트는 기둥 렌더러의 레이어·Order를 매 프레임 읽어, 바뀌었을 때만 다시 씁니다.
- precision 100 기준으로 전리품이 정렬상 0.02유닛(0.64px) 앞당겨진 정도라 다른 오브젝트와의 앞뒤는 사실상 같습니다.

## 5. 파일

| 경로 | 역할 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/VFX_LootPillarGlow.cs` | 이펙트 본체 |
| `Assets/Scripts/Presentation Layer/VFX/LootPillarGlowData.cs` | 전시 프레임별 후광 조각·중심·경계 |
| `Assets/Scripts/Presentation Layer/VFX/Editor/LootPillarGlowBuilder.cs` | 메뉴 `LumberBoy/VFX/Build Loot Pillar Glow` |
| `Assets/Shaders/VFX/SpriteBeamFlow.shader` | 빛 줄기 심(가산 + 위로 흐르는 결, 마스크 먼저 계산 후 조기 discard) |
| `Assets/Graphics/VFX/LootPillar/T_LootPillarDisc12/14/15/22.png` | 원반 텍스처(감쇠 지수별), 64x64 쌍선형 |
| `Assets/Graphics/VFX/LootPillar/T_LootPillarBeamTint.png`, `T_LootPillarBeamCore.png` | 빛 줄기 덮기·심 마스크 32x96, 피벗 = 기둥 윗면 |
| `Assets/Graphics/VFX/LootPillar/T_LootPillarHalo.png` | 후광 아틀라스 512x256(전시 프레임 40장, 칸 48x48, 조각 이름 `LootPillarHalo_N`) |
| `Assets/Graphics/VFX/Materials/M_LootPillarTint/Add/BeamFlow/Particles.mat` | 머티리얼 4개 |
| `Assets/Scriptable Obj/LootData/LootPillarGlowData.asset` | 구운 데이터 |
| `Assets/Prefabs/VFX/Item/VFX_LootPillarGlow.prefab` | 이펙트 프리팹. `LootPillar.prefab` 루트 아래 `PillarGlow`로 중첩 |

- **전시 스프라이트를 바꾸거나 전리품·단계를 추가하면 빌더를 다시 실행**해야 합니다. 데이터에 없는 프레임은 후광과 반짝임 없이(원반은 렌더러 경계 중심에) 재생됩니다. 같은 이름의 조각은 ID를 유지하므로 다시 구워도 참조가 끊기지 않습니다.
- 빛 줄기 흐름은 셰이더 `_Time`을 씁니다(모든 기둥이 같은 위상).

## 6. 픽셀 규칙 검수

| 규칙 | 적용 | 판정 |
|---|---|---|
| JAGGIES | 선·링·곡선 윤곽을 픽셀로 그리지 않음. 부드러운 빛은 모두 쌍선형 텍스처 | 해당 없음 |
| DOUBLE PIXEL | 픽셀 선 없음 | 해당 없음 |
| HUGGING | 평행선·링 없음. 후광은 블러 텍스처라 외곽선 옆에 평행선을 만들지 않음 | 해당 없음 |
| 입자 예외(6.5) | `.` `+` `x` `●`(2x2) `◆`(5x5), 반짝임만 5x5 `+`. 떠오르는 입자는 한 색·한 밝기, 반짝임의 `+` `x` 5x5`+`만 가운데 한 칸을 밝은 색으로 | 예외 범위 |

## 7. 최적화 체크리스트 (VFX_Procedural_Pixel_Guide 10.11 / DungeonVFXOptimization 8장)

| 항목 | 결과 |
|---|---|
| 정적 공유 `PixelQuadBuffer`(128쿼드) + `ReserveMesh(128)` | 적용. 입자·반짝임 메쉬가 버퍼 하나를 차례로 씀 |
| 정점 수 + 해시 같으면 업로드 생략, 재생 시작·숨김 때 -1 초기화 | 적용(`PixelPainter.Commit`) |
| 재구성 빈도 | 30fps 틱 또는 기준 칸(진화 흔들림·뽀잉으로 전리품·기둥이 움직일 때)이 바뀔 때만 |
| 좁은 경계 + `PixelVfxCulling` | 적용(기둥 윗면 기준 48x96px + 여유). 화면 밖이면 부드러운 빛 갱신과 픽셀 재구성을 모두 건너뜀 |
| 색 미리 계산 | 팔레트·정점 rgb는 `SetColor` 때 계산, `OnValidate`에서 갱신 |
| Transform·색·소팅·enabled는 바뀔 때만 | 슬롯마다 마지막 값 캐시. 소팅은 기둥 Order가 바뀔 때만 |
| 힙 할당 | 초기화(메쉬) 외 0. LINQ·람다·클로저 없음 |
| 셰이더 | `SpriteBeamFlow`는 마스크 알파가 1/510 미만이면 흐름 계산 전에 discard |
| 캡처 텍스처 | 사용 안 함(게이트 등록 불필요) |

## 8. 검증 기록과 주의

- 컴파일 경고·오류 0.
- 편집 모드 임시 하네스(삭제함)로 실제 `LootPillar.prefab` 3개를 1920x1080 HDR + Volume Profile 2로 렌더해 시안과 대조했습니다.
- **하네스 주의**: 편집 모드에서 `Camera.Render`를 직접 부르면 SortingGroup 정렬 데이터가 갱신되지 않아 `Back` 묶음이 전리품 위에 그려집니다. 렌더 전에 `SortingGroup.UpdateAllSortingGroups()`를 불러야 실제 화면과 같습니다(플레이 모드는 엔진이 매 프레임 갱신).
- 편집 모드에서는 이 컴포넌트의 `Awake`/`OnEnable`/`LateUpdate`가 자동으로 불리지 않으므로 하네스가 직접 불러야 합니다.
- **플레이 모드(실제 마을, 첫 등장 낙하, 진화 연출, 성장 뽀잉)는 아직 확인하지 않았습니다.** 특히 진화 연출 중 원반·후광 변조, 낙하 착지 순간 켜짐, 캐릭터가 기둥 앞에 섰을 때의 앞뒤를 확인해 주세요.
