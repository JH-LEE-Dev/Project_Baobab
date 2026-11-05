# 전리품 상세 HUD 발광 (UI_LootRevealGlow) 가이드

마을 전리품 기둥에 상호작용하면 뜨는 상세 화면(`HUD_LootReveal`)의 발광입니다. 기존에는 방사형 광선(`VFX_Radial`, `UI_ItemAuraEffectController`)이 1.2초 동안 한 번 터지고 끝나서, 평상시에는 빛이 없었습니다. 이제는 등장 순간의 발광 뒤에 마을 기둥 빛과 같은 오라가 계속 이어집니다.

- 승인된 시뮬레이션 시안 2: https://claude.ai/artifact/7ohbbwSAE8RV4UPFrbUqMf (바닥 오라는 v2 기본값으로 은은하게, 둘레 별 5개 제외, 광선 개선판)
- 구현 후 Unity 실제 렌더·성능 측정: https://claude.ai/artifact/9JvEF3mBsHMMA7rAcgJKrD
- 마을 기둥 빛: `Docs/LootPillarGlowVFX_Guide.md` (팔레트·입자 식을 공유)

---

## 1. 연출

| 구간 | 내용 |
|---|---|
| ① 등장 (뽀잉 시작 = 0초 ~ 1.25초) | 전리품 백열(0.15초 최대 유지 → 0.7초에 원색), 0 / 0.18 / 0.36초에 세 번 번쩍이는 리듬(100 / 70 / 45%), 큰 오라 섬광(덮기 지름 128px + 가산 지름 120px + 심 52px, 번쩍마다 8px 커짐), 가로 렌즈 줄(심 220x5.2px, 넓은 줄 140x12px) + 세로 렌즈 줄(5.2x88px, 번쩍마다 15% 길어짐), 4갈래 별빛(지름 116px, 초당 120도 회전, 초당 3번 15% 맥동), 광선 개선판(12줄, 긴 70px/짧은 44px, 초당 0.25rad 회전, 번쩍마다 18% 길어짐) |
| ② 평상시 (0.05초부터 0.7초에 걸쳐 차오름) | 바닥 웅덩이(덮기 96x40 알파 0.10, 가산 88x36 세기 0.12~0.16), 빛 줄기(폭 40, 높이 92, 결이 위로 흐름), 뒤 빛 원반(덮기 지름 120 0.18, 가산 지름 104 0.17~0.24), 실루엣 후광(확대 스프라이트 실루엣을 시그마 4.4px로 번지게, 0.3~0.5), 떠오르는 단색 입자 10개, 가장자리 반짝임 5곳 |

- 숨쉬기 주기는 2.6초입니다.
- `Pop` 파티클(VFXComponent)은 그대로 유지했습니다(사용자 결정).
- 색은 성장 단계 색(`LootGrowthVisualData.tierAuraColors`)을 쓰고, 단계가 없으면 인스펙터 `fallbackColor`(금색)를 씁니다.

## 2. 연결 (`HUD_LootReveal`)

| 시점 | 호출 |
|---|---|
| `Awake` | `revealGlow.Stop()` (숨겨 둠) |
| `PlayVFX` (전리품 뽀잉 시작) | `revealGlow.Play(단계 색, 색 있음 여부)`. `revealGlow`가 연결돼 있으면 기존 `auraEffect`는 재생하지 않음 |
| `Hide` / `ResetState` / `OnDestroy` | `revealGlow.Stop()` (즉시 숨김) |

- 기존 `auraEffect` 필드와 코드는 남아 있고, 프리팹에서 참조만 비웠습니다. `VFX_Radial` 오브젝트는 꺼 두었습니다.
- 시간은 `Time.unscaledDeltaTime`을 씁니다.

## 3. 계층과 그리는 순서

```
DimBG (Mask: 높이 132px 띠)
└ IMG_Pillar
  ├ VFX_Radial (꺼짐)
  ├ RevealGlowBack  [자식 Canvas, UI_LootRevealGlow]  ← 기둥과 같은 사각형·피벗
  │   PoolTint, PoolAdd, BeamTint, BeamCore, CoreTint, CoreAdd,
  │   BurstTint, BurstAdd, BurstCore, Rays, LensWide, LensCore, LensV, Star, Halo, Motes
  ├ IMG_Loot
  │   └ RevealFlash  [자식 Canvas, 백열]
  └ RevealGlowFront [자식 Canvas]  Twinkles
```

- UGUI는 계층 순서대로 그리므로 뒤 층 → 전리품 → 백열 → 반짝임 순서입니다. 돌 조각(Particles)은 런타임에 기둥 부모의 마지막 자식으로 옮겨져 맨 위에 그려집니다.
- **마스크 (사용자 결정 B안)**: 발광 그래픽은 모두 `maskable = false`라서 DimBG 띠 마스크 밖으로도 빛 줄기·입자·오라가 올라가 보입니다. 기둥·전리품·텍스트는 그대로 띠 안에만 보입니다. 발광은 뽀잉 시작부터 켜지므로, 기둥이 띠 아래에서 솟는 동안에는 보이지 않습니다.

## 4. 그리는 방식과 성능

| 항목 | 방식 |
|---|---|
| 부드러운 빛 | 빌더가 구운 텍스처 `Image`. 매 프레임에는 위치·회전·배율·색만 씀(GPU가 그림) |
| HDR 세기 | UI 정점 색은 0~1로 잘리므로 rgb = 정규화 색, 알파 = 세기 x 색 최대 채널 / 2.5. 셰이더가 rgb x 알파 x 2.5로 복원. **머티리얼 인스턴스 없음** → 배칭 유지, 머티리얼 더티 없음 |
| 크기 | `localScale`로만 바꿈(메쉬 재생성 없음). 슬롯마다 지난 값 캐시, 바뀐 값만 씀 |
| 캔버스 격리 | 뒤 층·앞 층·백열이 각각 자식 Canvas라 매 프레임 변화가 HUD 텍스트·디졸브 배경을 다시 배칭하지 않음 |
| 픽셀 입자·반짝임 | `UI_LootGlowParticles`(MaskableGraphic)가 미리 잡은 배열(256쿼드)을 재사용. 30fps 틱이나 기준 칸이 바뀔 때만 `SetVerticesDirty` |
| 등장 빛 | 1.25초가 지나면 Image를 꺼서 그리지 않음 |
| 할당 | 초기화 이후 0 |

측정(편집기, 1000프레임 평균, 리플렉션 호출 포함):

| 구간 | 이펙트 갱신 | GC 할당 | 갱신 + 캔버스 리빌드 |
|---|---|---|---|
| 등장 | 7.1 µs/프레임 | 0 B | 0.20 ms/프레임 |
| 평상시 | 6.2 µs/프레임 | 0 B | 0.03 ms/프레임 |

시뮬레이션 페이지가 끊긴 것은 웹에서 CPU로 모든 빛과 블룸을 픽셀 단위로 계산했기 때문이며(프레임당 약 140ms), 게임에는 해당하지 않습니다.

## 5. 셰이더

| 셰이더 | 용도 |
|---|---|
| `Custom/UI/UIGlow` | `_Mode` 0 덮기(`M_UILootGlowTint`, 백열은 `_TintScale` 2인 `M_UILootGlowFlash`), 1 가산(`M_UILootGlowAdd`). 텍스처 알파 x 정점 알파가 1/510 미만이면 조기 discard |
| `Custom/UI/UIBeamFlow` | 빛 줄기 심. 마을 기둥 `SpriteBeamFlow`와 같은 흐름 식(`_Time`) |
| `Custom/UI/UIPixelGlow` | 픽셀 입자. rgb x 알파 x 2.5, 알파는 saturate(정점 알파 x 3) → 사라지는 입자가 검은 점이 되지 않음 |

- 모두 UGUI 스텐실·RectMask2D 클리핑과 `_UIVertexColorAlwaysGammaSpace`(Unity 6 UI 기본 셰이더와 같은 처리)를 지원합니다. 캔버스 설정이 정점 색을 선형으로 바꾸므로 코드는 감마 rgb를 넘깁니다.

## 6. 파일

| 경로 | 역할 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/UI_LootRevealGlow.cs` | 이펙트 본체 |
| `Assets/Scripts/Presentation Layer/VFX/UI_LootGlowParticles.cs` | 픽셀 입자 그래픽 |
| `Assets/Scripts/Presentation Layer/VFX/LootGlowShared.cs` | 팔레트·입자 식·감쇠 함수(마을 기둥 빛과 공유) |
| `Assets/Scripts/Presentation Layer/VFX/Editor/LootRevealGlowBuilder.cs` | 메뉴 `LumberBoy/VFX/Build Loot Reveal Glow` (마을 기둥 빌더를 먼저 실행해야 함) |
| `Assets/Shaders/VFX/UIGlow.shader`, `UIBeamFlow.shader`, `UIPixelGlow.shader` | UI 셰이더 |
| `Assets/Graphics/VFX/LootReveal/` | 원반 4장(1.3/1.6/1.7/2.0), 광선 `T_LootRevealRays`, 별빛 `T_LootRevealStar`(256), 후광 아틀라스 `T_LootRevealHalo`(확대 스프라이트 11장, 칸 96x96) |
| `Assets/Graphics/VFX/LootPillar/` | 공유: 원반 1.2/1.4/1.5/2.2, 빛 줄기 덮기·심 |
| `Assets/Graphics/VFX/Materials/M_UILoot*.mat` | 머티리얼 5개 |
| `Assets/Scriptable Obj/LootData/LootRevealGlowData.asset` | 확대 스프라이트별 후광·중심·경계(`LootPillarGlowData` 형식) |

- 확대 스프라이트를 바꾸거나 전리품을 추가하면 빌더를 다시 실행해야 합니다. 데이터에 없는 스프라이트(성장 단계가 없는 기본 스프라이트 등)는 후광·반짝임 없이 재생되고, 원반은 사각형 중심에 놓입니다.
- 마을 기둥 빌더의 후광 굽기 코드를 `HaloBakeSettings`로 매개변수화해 같이 씁니다. 리팩터 후 다시 구운 마을 기둥 아틀라스·데이터는 이전과 바이트 단위로 같았습니다.

## 7. 시안과 다른 점

- 광선과 별빛은 모양 전체를 한 장으로 구워 회전·배율만 바꿉니다. 시안에 있던 광선 12줄 각각의 미세한 길이 흔들림(±8%)은 빠졌습니다.
- 셰이더는 계획의 4종 대신 3종입니다(덮기·가산을 한 셰이더의 두 머티리얼로).

## 8. 픽셀 규칙 검수

| 규칙 | 적용 | 판정 |
|---|---|---|
| JAGGIES / DOUBLE PIXEL / HUGGING | 선·링·윤곽을 픽셀로 그리지 않음. 빛은 모두 쌍선형 텍스처 | 해당 없음 |
| 입자 예외(6.5) | `.` `+` `x` `●`(2x2) `◆`(5x5), 반짝임만 5x5 `+`. 떠오르는 입자는 한 색·한 밝기 | 예외 범위 |

## 9. 검증 기록과 주의

- 컴파일 경고·오류 0.
- 편집 모드 임시 하네스(삭제함)로 실제 HUD 프리팹을 `Canvas.prefab`(Screen Space - Camera) 아래에 두고 1920x1080 HDR + Volume Profile 2로 30fps 렌더해 시안과 대조했습니다. 하네스를 다시 만들 때는 매 프레임 `Canvas.ForceUpdateCanvases()` 뒤에 `Camera.Render()`를 부르고, `ReadPixels` 사각형 원점은 왼쪽 아래입니다.
- **플레이 모드(실제 마을에서 기둥 상호작용, 광클 열고 닫기, 전리품 종류·단계별 색)는 아직 확인하지 않았습니다.**
