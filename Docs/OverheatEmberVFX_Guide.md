# 4스테이지 불씨 VFX 가이드

4스테이지(MagmaForest) 바닥 불씨(`OverheatEmber`)의 연출 구성, 게임 연결 상태, 튜닝 방법을 정리한 문서입니다.

- 승인 시뮬레이션: https://claude.ai/artifact/8rGYnBcQEAmuQhUar3GZwe ("A+B 불덩이" 화면)
- 코드는 전부 Presentation Layer입니다. Application Layer 코드는 수정하지 않았고, `GameInstaller` 프리팹의 `EmberItemController` 값(태그 2칸, VFXComponent 풀 목록)만 빌더가 채웠습니다.
- 불씨 본체 연출은 **불씨 상태를 읽지 않고 움직임으로 판단**합니다. `EmberItem`이 바뀌어도 위치·스케일만 지금처럼 움직이면 그대로 동작합니다.

---

## 1. 만든 것

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/OverheatEmberOrb.cs` | 불덩이 계산·그리기 공용 코어(13x15 열 격자, 시뮬레이션 `orbStep`/`orbColor` 이식) |
| `Assets/Scripts/Presentation Layer/VFX/OverheatEmberParticles.cs` | 불티·불길 조각·그을음 입자 묶음(고정 용량 구조체 배열) |
| `Assets/Scripts/Presentation Layer/VFX/VFX_OverheatEmber.cs` | 불씨 본체. 불씨 프리팹 `Visual/EmberOrb`에 붙는다 |
| `Assets/Scripts/Presentation Layer/VFX/OverheatEmberResidue.cs` | 획득되어 꺼진 불씨의 꼬리를 끝까지 그리는 숨김 드라이버 |
| `Assets/Scripts/Presentation Layer/VFX/VFX_OverheatEmberOneShot.cs` | 획득(`Acquire`)·소멸(`Expire`) 단발 연출 |
| `Assets/Scripts/Presentation Layer/VFX/Editor/OverheatEmberBuilder.cs` | 메뉴 `LumberBoy/VFX/Build Overheat Ember`. 아래 에셋과 연결을 여러 번 실행해도 같은 결과로 맞춘다 |
| `Assets/Graphics/VFX/Materials/M_OverheatEmber.mat` | `Custom/VFX/BrandWrapGlow`, `_GlowMin` 1.0 / `_GlowMax` 2.4 |
| `Assets/Prefabs/VFX/Item/OverheatEmber/VFX_OverheatEmberAcquire.prefab`, `VFX_OverheatEmberExpire.prefab` | 풀 규약(루트 ParticleSystem + 메쉬 자식). 루트 수명 2.4초 / 1.8초 |
| `Assets/Prefabs/Objects/Item/OverheatEmber.prefab` | `Visual` 아래 `EmberOrb`(본체) > `EmberGlow`(바닥 붉은 빛, 모닥불의 `M_CampfireGroundGlow` 가산 머티리얼). `Visual`의 임시 마름모 그림은 지웠다(SpriteRenderer는 정렬 기준이라 남김) |
| `Assets/Prefabs/Installer/Installer/GameInstaller.prefab` | `EmberItemController`: `acquireVfxTag = OverheatEmberAcquire`, `expireVfxTag = OverheatEmberExpire`, VFXComponent 풀에 두 태그(4/16, 2/8). `groundLoopVfxTag`는 비워 둔다(본체가 프리팹 자식이라 필요 없음) |

---

## 2. 연출 구성

| 장면 | 내용 |
|---|---|
| 불덩이 | 짙은 빨강 구체(지름 약 9px). 윤곽이 꿈틀대고 둘레 전체에서 불길이 2~4px 핥고 오른다. 구 안에서 나선 결 두 갈래가 돌며 결을 따라 밝아지고, 왼쪽 위 가장자리 1px에 빛을 받는다. 2.2~3.4초마다 주황·노랑으로 번쩍인다 |
| 맴도는 불티 8개 | 불덩이 높이에서 기울어진 궤도를 돈다. 앞쪽은 주황~흰 노랑, 뒤쪽은 어두운 빨강이고 불덩이에 가려진다. 꼬리 3칸 |
| 대기 중 입자 | 위로 튀는 불티(0.35~0.7초마다), 번쩍일 때 4개, 1.4~3초마다 앞쪽 불티 하나가 궤도를 벗어나 솟구침, 0.4~0.9초마다 윗면에서 `+` 조각이 떨어져 나가 `.`로 줄며 식음 |
| 바닥 붉은 빛 | 발밑 13x5 타원을 가산 혼합(`Indicators` 레이어). 맥동·번쩍임에 따라 숨 쉰다. 15Hz 갱신 |
| 흡입(이동 중) | 지나간 구간을 따라 `x` `+` `.` 조각을 촘촘히 남겨 꼬리가 늘어난다(0.3~0.5초 뒤 사라짐). 불티는 각자 다른 지연으로 불덩이를 뒤쫓는다. 루트 스케일이 0.35까지 줄면 구도 작아지고 바깥 불길은 그리지 않는다. 바닥 빛과 대기 입자는 멈추고, 정렬을 `FlyingItem` 레이어로 올린다 |
| 획득 | 날아가던 불씨가 꺼지는 순간 남은 꼬리는 `OverheatEmberResidue`가 이어 그린다(바닥에서 꺼질 때의 대기 불티는 넘기지 않고 버린다). 도착 자리(캐릭터 피벗) 9px 위에서 `x` `+` `.` 조각 9개 + 꼬리 불꽃 7개 + 튕겨 나가는 불티 8개 + 잔불 3개 |
| 소멸 | 미리 데운 불덩이가 0.6초 동안 식어 검붉게 사라지고, 불티는 0.4초 안에 어두워져 사라진다. 끝에 그을음 2개 |

링·충격파 같은 고리 모양은 쓰지 않습니다(사용자 지시).

---

## 3. 구현 방식

- 위치·불티·입자·꼬리 계산은 **60Hz** 고정 간격, 그리기는 매 프레임입니다. 불꽃 격자(`OverheatEmberOrb`)만 30Hz입니다. 격자는 "한 스텝에 한 칸" 열이 오르는 모델이라 60Hz로 올리면 불길이 2배 빨리 오르고 짧아져 승인된 모양이 바뀌기 때문입니다. 획득·소멸 단발 연출과 잔여 꼬리는 매 프레임 실제 시간으로 움직입니다. 재생 중 힙 할당이 없습니다(고정 용량 구조체 배열, 자체 xorshift, static 공유 쿼드 버퍼).
- 화면 밖 불씨는 `PixelVfxCulling`으로 **계산과 그리기를 모두** 멈춥니다(맵에 수십 개가 깔리므로 모닥불과 다름). 다시 들어오면 이전 위치 없이 시작해서 순간 이동을 꼬리로 그리지 않습니다.
- 그리기 건너뛰기: 계산 스텝이 없던 프레임(60Hz보다 빠른 화면)에 위치·크기가 그대로면 메쉬를 다시 만들지 않는다. 움직이는 중에는 메쉬가 로컬 좌표라 픽셀 격자에서 어긋나므로 항상 다시 그린다.
- 칸별 상수 표: 불덩이 칸의 각도·거리와 `sin/cos(2a·3a·5a)`, 가장자리 빛 방향을 처음 한 번만 계산해 둔다. 시간 항은 호출당 한 번 구해 덧셈 정리로 합쳐서 격자 갱신은 칸당 삼각함수 0회, 그리기는 칸당 `Sin` 1회(나선 결)다. 흡입 막바지(축소)만 직접 계산한다. 편집 모드에서 표 경로와 직접 계산 경로를 480프레임 비교해 그림이 완전히 같음을 확인했다.
- 잔여 드라이버: 불씨가 켜질 때(`OnEnable`) `OverheatEmberResidue.Prepare()`로 미리 만든다. 넘겨받는 `Adopt`는 꺼질 때(`OnDisable`) 불리므로 오브젝트를 만들지 않는다. 씬을 닫는 중에 오브젝트를 만들면 "Some objects were not cleaned up when closing the scene" 경고가 나기 때문이다(실제로 겪은 버그).
- "이동 중" 판단: 불덩이 중심이 초당 30px 이상(60Hz 한 스텝에 0.5px) 움직이면 이동 중입니다(흡입, 나무에서 튀어나오는 포물선 모두 해당).
- 크기: 불씨 루트(`groundAnchor`)의 스케일을 읽어 직접 작게 그립니다. `EmberOrb` 오브젝트는 부모 스케일(루트 스케일, 착지 찌그러짐)을 매 프레임 상쇄해 월드 스케일 1을 유지합니다. 그래야 픽셀이 월드 격자에 맞고 메쉬 경계 계산이 맞습니다. 부모 스케일이 0에 가까우면(튀어나오는 첫 순간) 그리지 않습니다.
- 정렬: 바닥에서는 `Visual` SpriteRenderer(CustomSortable이 매 프레임 갱신)의 레이어·순서 + 1, 이동 중에는 `FlyingItem` 0. 단발 연출은 루트 ParticleSystem 렌더러 정렬(`FlyingItem`)을 따릅니다.
- 이 이펙트들은 `_CameraSortingLayerTexture`를 읽지 않으므로 `SortingLayerTextureUser`는 필요 없습니다.

---

## 4. 픽셀 규칙 검수표

| 규칙 | 적용 | 비고 |
|---|---|---|
| JAGGIES | 적용 | 모든 도형이 정수 격자 칸이다. 구 윤곽은 칸 판정으로만 만든다 |
| DOUBLE PIXEL | 적용 | 선 그리기가 없다. 꼬리는 점 조각이다 |
| HUGGING | 적용 | 평행한 선이 없다 |
| 입자 모양 | 적용 | `.` `+` `x`만 쓴다. `+` `x`는 가운데 칸이 밝고 팔 4칸이 한 단계 어둡다 |
| 60fps | 적용 | 불덩이 위치·불티·입자·꼬리 60Hz, 그리기 매 프레임. 불꽃 격자 무늬는 30Hz(승인 모양 유지), 바닥 빛은 15Hz(은은한 빛이라 입자가 아님) |

---

## 5. 시뮬레이션과 달라진 점

| 항목 | 시뮬레이션 | 제작 | 이유 |
|---|---|---|---|
| 바닥 그림자 | 검은 반투명 2줄 | 없음 | 가산 혼합 머티리얼로는 어둡게 못 그린다. 붉은 빛 웅덩이만 남겼다 |
| 흡입 도착 높이 | 몸통 | 발밑 피벗으로 들어가고, 터짐만 9px 위 몸통에서 난다 | 게임의 흡입 목표가 `Character.transform`(발밑)이다 |
| 획득 시 돌던 불티 | 그 불씨의 실제 불티가 튕겨 나감 | 단발 연출이 궤도 위 자리에서 새로 튕겨 냄 | 불씨 본체는 획득 순간 꺼지므로 단발 연출이 같은 모양을 다시 그린다 |
| 블룸 | 캔버스 블러 | 정점 알파 발광 + 게임 블룸 | 실제 밝기는 게임에서 확인해야 한다 |

---

## 6. 튜닝

| 위치 | 필드 | 기본 | 용도 |
|---|---|---|---|
| `OverheatEmber` > `Visual/EmberOrb` (`VFX_OverheatEmber`) | `glowScale` | 1.0 | 불티·번쩍임 발광 배율. 구 몸통은 발광을 거의 주지 않는다 |
| 〃 | `groundGlowStrength` | 0.6 | 바닥 붉은 빛 세기. 0이면 끈다 |
| 〃 | `sortingOrderOffset` | 1 | 바닥에 있을 때 Visual 정렬 순서에 더하는 값 |
| 〃 | `flyingSortingLayerName` | FlyingItem | 날아가는 동안의 정렬 레이어 |
| 단발 프리팹 (`VFX_OverheatEmberOneShot`) | `glowScale` | 1.0 | 획득·소멸 발광 배율 |
| `M_OverheatEmber.mat` | `_GlowMin` / `_GlowMax` | 1.0 / 2.4 | 블룸 세기. 색 단계가 뭉개지면 `_GlowMax`부터 낮춘다 |

코드 상수(필요하면 요청 주세요): 불덩이 크기·냉각(`OverheatEmberOrb`), 불티 수·궤도, 꼬리 조각 간격(2.5px)·수명, 이동 판단 기준(0.5px/스텝, 초당 30px), 불티 꼬리 기록(20스텝 = 0.33초), 입자 용량(본체 160, 잔여 384).

---

## 7. 확인 방법

1. 메뉴 `LumberBoy/VFX/Build Overheat Ember`를 실행하면 머티리얼, 단발 프리팹, 불씨 프리팹 자식, GameInstaller 값이 같은 결과로 다시 맞춰진다(이미 적용되어 있음).
2. 4스테이지에 들어가 바닥 불씨, 나무를 베어 튀어나오는 불씨, 줍기, 시간이 지나 사라지는 불씨(벌목 드랍만 20초)를 본다.
3. 확인할 항목
   - 바닥 불씨가 바닥 용암 무늬와 구분되는가, 불티가 불덩이보다 밝게 보이는가.
   - 블룸이 과하지 않은가(`_GlowMax`, `glowScale`).
   - 흡입 꼬리가 캐릭터 앞에 그려지는가(`FlyingItem`), 도착 순간 꼬리가 끊기지 않고 사라지는가.
   - 캐릭터·나무와 겹칠 때 바닥 불씨의 앞뒤 정렬이 자연스러운가.

---

## 8. 미검증 항목 (게임에서 확인이 필요합니다)

| 항목 | 상태 |
|---|---|
| 플레이 모드 재생 | 60Hz 전환 뒤에도 편집 모드 캡처로 대기·흡입 모양이 30Hz 때와 같은지 대조했다. 편집 모드에서 내부 메서드를 직접 돌려 실제 렌더러로 캡처했다(대기 5장면, 흡입 9장면, 획득·소멸 각 5장면). 모양·꼬리·축소는 확인했다. 플레이 모드 실제 재생, `OnDisable` → 잔여 드라이버 인계는 확인하지 않았다(편집 모드에서는 인계를 하지 않는다) |
| 블룸·HDR 밝기 | 캡처에 포스트 프로세싱이 없었다. 게임 블룸에서 색 단계가 남는지 봐야 한다(과열 오라 때 블룸에 뭉개진 전례가 있다) |
| 정렬 | Visual 정렬 + 1, 이동 중 `FlyingItem`은 코드로만 확인했다 |
| 성능 | 프로파일링하지 않았다. 화면 안 불씨 1개당 격자 30Hz 195칸 + 입자·불티 60Hz 계산, 쿼드 수백 개 이하로 추정한다. 할당은 없다 |
| GameInstaller 재저장 부수 효과 | 빌더가 프리팹을 다시 저장하면서 코드에서 이미 지워진 직렬화 필드(`InDungeonVFXManager.fireExplosionVfx*` 3줄)가 빠지고 새 필드 기본값(`PuppyManager.houseSleepOffset`)이 기록됐다. 동작 변화는 없다. `Assets/Scripts/Editor/Build/StatBaseline_*.txt`에는 옛 필드가 남아 있다 |
