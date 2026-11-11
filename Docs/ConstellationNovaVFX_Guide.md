# 별자리 노바 VFX 가이드 (픽셀 각인 마법진)

별자리 발현 때 별자리 중심에서 재생되는 **픽셀 각인 마법진** 이펙트(`VFX_ConstellationNova`)의 구성, 튜닝, 검수 결과를 정리한 문서입니다.
기존 "은하 별 폭발 + 9각 별 마법진"을 승인된 시안 3(타원 1개 + 부드러운 빛)으로 전면 교체했습니다.

- 계획표: `Docs/Roguelike/_Work/ConstellationNova_PixelSigil_Plan.md`
- 시안 렌더 기준 코드: `renderV2(opt.v3 = true)` 경로(`nova/core.js`)
- **외부 참조는 그대로**입니다. `InDungeonVFXManager`, `InDungeonObjectManager`, `Stage3TreeGenerationStrategySO` 수정 없음.

---

## 1. 만든 것 / 바꾼 것

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/VFX_ConstellationNova.cs` | 이펙트 본체 전면 재작성 |
| `Assets/Shaders/VFX/NovaSoftGlow.shader` | 부드러운 빛용 가산 셰이더(`Blend One One`, 출력 = 정점 RGB x 정점 A x `_Glow`, 기여 1/510 미만 조기 discard) |
| `Assets/Graphics/VFX/Materials/M_ConstellationNovaSoft.mat` | 소프트 메쉬 머티리얼(`_Glow` 2.0 = 코드 상수 `SoftGlowMultiplier`) |
| `Assets/Graphics/VFX/Materials/M_ConstellationNovaPixel.mat` | 마법진/공중 픽셀 머티리얼(`BrandWrapGlow`, `_GlowMin` 1.3 / `_GlowMax` 4.2) |
| `Assets/Scripts/Presentation Layer/VFX/Editor/ConstellationNovaBuilder.cs` | 메뉴 `LumberBoy/VFX/Build Constellation Nova` (머티리얼 2종 생성, SoftMesh 자식 추가, 직렬화 필드 기록) |
| `Assets/Prefabs/VFX/Constellation/VFX_ConstellationNova.prefab` | 빌더 결과(SoftMesh 자식 추가, 머티리얼 교체, 새 필드) |

- 빌더는 여러 번 실행해도 같은 결과로 맞춥니다. 파생 프리팹(StarRain, DashKickoff, SporeBigExplosion)은 이미 복제되어 있어 영향이 없고, 각 빌더는 대상 프리팹이 없을 때만 원본을 복사합니다.
- **`M_BrandStampBurst`를 쓰지 않는 이유**: 시안의 밝기 곡선은 `정점 알파 a -> 배율 1.3 + 2.9a`(= `_GlowMin` 1.3, `_GlowMax` 4.2)입니다. 기존 `M_BrandStampBurst`(1.2 ~ 6.0)를 쓰면 흰색 단계가 3.1배가 아니라 4.2배로 밝아져 승인된 화면과 달라지고, 다른 이펙트도 같은 머티리얼을 쓰므로 값을 바꿀 수 없어 전용 머티리얼을 만들었습니다.
- 정점 색은 sRGB 팔레트를 **선형 값으로 바꿔** 넣습니다. 선형 색 공간에서 화면에 팔레트 색이 그대로 나오게 하기 위함입니다(시안도 같은 방식).

### 유지한 외부 API

`GroundSquashY`(0.5), `SetRadius`, `CurrentRadius`, `TotalDuration`(2.8), `EvaluateRadius`, `EvaluateShockArrivalTime`(`ShockDuration` 0.4 수식 그대로), `GetGroundDistance`, `OnDrawGizmosSelected`. 인스펙터 필드 `maxRadius`, `sortingOrderOffset`, `groundMeshFilter`, `groundMeshRenderer`, `groundSortingLayerName`, `groundSortingOrder`도 이름 그대로입니다.

### 삭제한 필드

`particleSpread`, `density`, `starsPerUnit`, `heroStarCount`, `embersPerUnit`, `sparksPerUnit`(새 연출에서 안 씀). 프리팹의 옛 값은 재저장으로 정리되었습니다.

### 새 인스펙터 필드

| 필드 | 기본 | 용도 |
|---|---|---|
| `palette` | Star | Star(파랑 선 + 금빛 룬) / Gold(금빛 선 + 얼음빛 룬) / Violet(보라 선 + 금빛 룬) |
| `bSoftLight` | 켬 | 부드러운 빛(소프트 메쉬) 전체 켜기/끄기 |
| `curtainHeightPx` | 74 | 빛 커튼 최대 높이(반지름 128px 기준, 반지름에 비례) |
| `pillarHeightPx` | 130 | 빛기둥 최대 높이(반지름 128px 기준, 반지름에 비례) |
| `softMeshFilter` / `softMeshRenderer` | SoftMesh | 소프트 메쉬 연결(빌더가 설정) |

---

## 2. 연출 타임라인

| 시각(초) | 내용 |
|---|---|
| 0프레임 | 마법진 전체가 흰색(팔레트 4단계)으로 한 번에 각인. 중심 4갈래 별(팔 7) + 1px 가로/세로 섬광 줄, 꼭짓점 8곳 스파크 상승, 부드러운 빛 최대 |
| 0 ~ 0.3 | 3프레임마다 팔레트 한 단계씩 기본 단계로 식음. 중심 별 7 -> 5 -> 3 |
| 0 ~ 0.6 | 빛 커튼 0.6초에 소멸, 빛기둥 0.5초, 임팩트 섬광 0.16초 |
| 0.1 ~ 1.05 | 남은 기운: 바깥 링 빛 머리 2개가 감속하며 돌고 약해짐, 룬은 반대 방향 순서로 한 단계 밝아짐, 안쪽 도형은 여진 2번(0.12초, 0.5초) |
| 0.03 ~ 2.1 | 떠오르는 입자(`+` `x` `.`) 54개, 초반(제곱 분포)에 몰림 |
| 1.2 ~ 2.35 | 해체: 바깥부터(안쪽은 0.4초 지연) 0.1초마다 팔레트 한 단계씩 꺼짐, 5% 픽셀이 `+` -> `.`로 떠오름, 룬은 최대 3px 상승 |
| 2.4 | 모든 렌더러 끔(`TimelineLength` 2.8은 루트 ParticleSystem 수명 3초 안에서 유지) |

픽셀 부분(마법진, 별, 입자)은 **30fps 프레임 단위**로 다시 그리고, 소프트 메쉬의 빛은 매 프레임 연속 시간으로 정점 값을 갱신합니다.

### 마법진 구성 (반지름 R = 반지름 유닛 x 32px, 기준 128px)

- 바깥 링 타원 **1개**(R, R/2). `PixelEllipse` 윤곽에서 1사분면만 취해 네 사분면으로 거울 복사합니다.
- 팔각별 = 마름모(정확한 2:1 계단, D = 0.8R을 짝수로 반올림) + 직사각형(수평/수직, Q = 0.707D를 짝수로 반올림).
- 직사각형 모서리 바깥 2:1 눈금 4곳(길이는 `8 x R / 128`px), 중심 마름모(반지름 0.156R을 짝수로).
- 세운 5x5 룬 12개(반지름 0.9R). 룬 7종은 전부 가운데 열 기준 좌우 대칭으로 손으로 찍은 글리프(마름모 별, 위 화살, 삼지창, 눈, 모래시계, 갈래, 십자 문). 0번(정면)과 6번(뒤쪽)은 단독, 1~5번과 11~7번은 같은 룬을 짝으로 둡니다. 위치는 0~6번만 계산하고 7~11번은 x 부호만 뒤집어 복사합니다(57.5px 같은 자리의 반올림 어긋남 방지).
- 픽셀 표는 **반지름(px)이 같으면 모든 인스턴스가 공유**하고(정적), 격자를 행 -> 열 순서로 훑어 정렬해 같은 줄 쿼드 병합이 잘 되게 합니다(반지름 4: 픽셀 1,640개 -> 쿼드 약 756개).

### 레이어 구조

| 자식 | 소팅 | 내용 | 머티리얼 |
|---|---|---|---|
| GroundMesh | ShockWave / 0 (나무/캐릭터 아래) | 마법진 픽셀 전체 | M_ConstellationNovaPixel |
| NovaMesh (루트 컴포넌트) | 루트 렌더러(FlyingItem) + `sortingOrderOffset` | 중심 별, 섬광 줄, 스파크, 떠오르는 입자, 해체 입자 | M_ConstellationNovaPixel |
| SoftMesh | 루트 렌더러 + `sortingOrderOffset` + 1 | 임팩트 섬광, 바닥 빛, 빛 커튼, 빛기둥 | M_ConstellationNovaSoft (가산) |

부드러운 빛은 시안과 같이 나무 위에 가산으로 겹칩니다.

### 소프트 메쉬

- 메쉬 형태(정점 858개)는 반지름과 무관해 **재생 인스턴스당 1회**만 만들고, 매 프레임은 정점 위치/색(float 4채널, 8비트 계단 없음)만 갱신합니다.
- 부분 순서를 끝나는 시각 순(섬광, 기둥, 커튼, 바닥 빛)으로 두어, 끝난 부분은 인덱스 범위에서 빼고 정점 업로드도 살아 있는 뒤쪽 구간만 합니다. 0.6초 이후에는 바닥 빛만 남습니다.
- 빛 커튼은 링 접선 기울기만큼 좌우 끝 정점 밝기를 높입니다(시안의 "링 픽셀마다 기둥 하나"가 좌우 끝에서 겹쳐 밝아지는 효과, 상한 10배).

---

## 3. 픽셀 규칙 검수표

| 규칙 | 적용 | 결과 |
|---|---|---|
| JAGGIES | 타원은 `PixelEllipse` midpoint 한 가지만 사용. 마름모는 정확히 2칸-1칸 계단, 직사각형은 수평/수직, 눈금도 2:1 계단. 회전 없음(팔레트 사이클링으로 움직임) | 위반 없음 |
| DOUBLE PIXEL | `PixelEllipse` 윤곽의 L자 모서리 제거 사용, 모든 선은 1px 한 줄 | 위반 없음 |
| HUGGING | 타원이 1개뿐이라 평행 링 없음. 반지름 4(128px) 실측: 룬-링 3px 간격(칸 거리 4), 룬-마름모 3px 간격, 눈금-링 6px 간격, 직사각형-링 11px 간격, 마름모-링 12px 간격. 반지름 6(192px)은 룬-링 6px 간격 | 위반 없음 (반지름이 작을수록 룬-링 간격이 줄어드는 점은 미검증) |
| 대칭 | 룬 글리프 7종 좌우 대칭, 룬 배치 7~11번은 0~6번의 거울 복사, 팔각별/눈금/중심 마름모/링도 좌우 대칭. 구현 후 편집 모드 하네스로 t = 1.15초(빛 머리 구간 아님)의 바닥 메쉬 쿼드를 셀로 펴서 좌우 거울 비교 | 반지름 2.5 / 3 / 4 / 5 / 6.5 / 10 모두 불일치 0 (반지름 4: 1,640 셀) |
| 룬 내부 | 5x5 글자 안에서 L자 이중 픽셀과 2x2 뭉침 없음. 5px 글자라 획 사이 1px 간격은 피할 수 없어 입자와 같은 모양 예외로 둠 | 예외 1건(룬) |
| 입자 | `+` `x` `.` 최소 크기만 사용, 30fps 스텝 | 규칙 준수 |
| 입자 겹침 | 입자는 먼저 그린 위에 나중 것을 덮는 순서(시안과 동일). 별도 간격 검사 없음 | 시안과 동일 |

참고: 링을 `PixelEllipse` 한 번에 그리지 않고 1사분면 거울 복사로 만든 이유는, `PixelEllipse.Build`가 경로를 따라 순차적으로 L자 모서리를 지우기 때문에 사분면별 결과가 어긋날 수 있기 때문입니다(좌우 대칭 보장).

---

## 4. 최적화 (가이드 10.11 / DungeonVFXOptimization 8장)

| 항목 | 적용 |
|---|---|
| 공용 버퍼 | `static PixelQuadBuffer`(2,048쿼드) + `static PixelPainter`를 바닥 -> 공중 순서로 번갈아 씀. 메쉬는 `ReserveMesh`로 미리 확보 |
| 업로드 생략 | `PixelPainter.Commit`의 정점 수 + `ContentHash` 비교. `Begin`에서 -1로 초기화 |
| 프레임 생략 | 픽셀 부분은 30fps 프레임이 바뀔 때만 다시 계산(60fps 화면의 절반 프레임은 계산 자체를 건너뜀) |
| 경계 | `PixelPainter.Commit`이 실제 그린 픽셀 범위로 경계를 좁힘. 소프트 메쉬는 고정 경계(반지름 기준) |
| 화면 밖 | `PixelVfxCulling.IsVisible`로 메쉬 재구성 생략. 건너뛰는 동안 렌더러를 끄고 해시 비교값 무효화 |
| 표/색 캐시 | 마법진 픽셀 표(반지름별 1회, 모든 인스턴스 공유), 팔레트 x 단계 색표(`Begin`/`OnValidate`), 소프트 둘레 삼각함수 표 |
| 세터 | 루트 소팅 값, 바닥 소팅 값은 마지막 적용값과 비교 후 쓰기. `OnEnable`에서 캐시 무효화 |
| 셰이더 | `NovaSoftGlow`는 기여가 1/510 미만이면 조기 discard |
| GC | LINQ/람다/클로저 없음, 고정 크기 배열과 정적 버퍼만 사용. 40개 동시 재생 측정에서 할당 0바이트 |

---

## 5. 튜닝

| 증상 | 조절 |
|---|---|
| 마법진 크기 | 인스펙터 `maxRadius` 또는 재생 직전 `SetRadius` (피격 범위와 같은 값) |
| 색 분위기 | `palette` (Star / Gold / Violet) |
| 부드러운 빛이 과함 | `bSoftLight` 끄기, `curtainHeightPx` / `pillarHeightPx` 낮추기, 머티리얼 `M_ConstellationNovaSoft`의 `_Glow` 조절(코드 `SoftGlowMultiplier`와 같이 바꾸면 밝기만 달라짐) |
| 픽셀 발광이 과함/약함 | `M_ConstellationNovaPixel`의 `_GlowMin` / `_GlowMax` (시안 값 1.3 / 4.2) |
| 소팅 | 공중/소프트는 `sortingOrderOffset`, 바닥은 `groundSortingLayerName` / `groundSortingOrder` |

코드 상수로 조정하는 값(요청 시 변경): 타임라인(`DissolveStart` 등), 팔레트 램프(`RampBlue`/`RampGold`/`RampViolet`), 발광 단계표(`GlowTable` = 0, 0.04, 0.12, 0.26, 0.62), 입자 개수(`MoteCount` 54).

---

## 6. 검증 방법과 결과

편집 모드 하네스(임시 에디터 스크립트, 검증 후 삭제)로 프리팹을 세우고 시간을 직접 정해 HDR(640x360, 선형)로 렌더링한 뒤, 같은 조건의 시안 렌더(블룸 없는 HDR)와 수치 비교했습니다.

| 시각(초) | 시안 합계(RGB 총합) | 유니티 합계 | 비고 |
|---|---|---|---|
| 0 | 35,592 | 35,631 | 임팩트 |
| 0.2 | 39,239 | 39,560 | 빛 커튼 |
| 0.45 | 17,991 | 16,900 | 입자 난수 차이 포함 |
| 1.15 | 8,291 | 8,263 | 룬/링 유지 구간 |

- 입자 난수는 시안과 같은 분포의 클래스형 LCG이며 값은 일치하지 않습니다(재생마다 달라짐).
- 플레이 모드 실제 화면(URP 블룸 포함, 카메라 후처리)은 **미확인**입니다. 게임에서 별자리 발현을 재생해 확인이 필요합니다.
- 힙 할당 0은 편집 모드 하네스(40개 인스턴스, 반지름 4)에서 `GC.GetAllocatedBytesForCurrentThread` 기준으로 측정했습니다.

## 변경 이력

- 2026-11-11: B 빛 번짐 띠 제거(사용자 피드백 "살짝 오버스러움"). 소프트 메쉬 정점 1,434 -> 858개, 빛 커튼(A)만 유지.
