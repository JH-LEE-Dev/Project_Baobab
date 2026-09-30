# 드론 충전 / 피격 픽셀 VFX 적용 가이드

이 문서는 드론 레이저 **충전 이펙트**와 **피격 이펙트**를 절차적 픽셀 VFX로 교체하면서 생긴 `Drone.cs` / `Drone.prefab` 변경을, 다른 프로그래머(또는 에이전트)가 그대로 적용할 수 있도록 정리한 것입니다.

- 기준 시점: 커밋 `eee10499` 위에서 작업했던 **변경 내용**
- **현재 저장소 상태**: `Drone.cs`와 `Drone.prefab`은 이 변경을 **되돌려 놓은 상태**(커밋 `eee10499`와 동일)입니다. 이 문서의 3장, 4장으로 **다른 프로그래머가 직접 적용**해야 합니다. 신규 스크립트, 프리팹, 머티리얼 6종은 저장소에 그대로 남아 있습니다(아직 커밋되지 않았을 수 있으므로 적용 전에 `git status`로 확인하세요).
- 적용 후에는 **9장의 기존 이펙트 잔여물 정리**까지 진행해 주세요.
- 이미 커밋된 부분(과열 아우라 연동, `PixelQuadBuffer` 공용화)은 커밋 `d98898b8`에 들어 있으며 이 문서의 범위가 아닙니다.
- Application Layer와 `Character.cs`는 **수정하지 않았습니다.**
- 검증 수준: 편집 모드에서 프리팹을 직접 렌더해 모양과 색만 확인했습니다. 플레이 모드, 블룸이 걸린 실제 화면, 풀 반환 동작은 **검증하지 않았습니다.** 6장의 위험을 꼭 읽어 주세요.

---

## 1. 무엇이 바뀌는가

| 항목 | 이전 | 이후 |
|---|---|---|
| 충전 이펙트 | `VFX_Charging.prefab` (순수 ParticleSystem) | `VFX_DroneChargeVortex.prefab` + `VFX_ChargeVortex` (절차적 픽셀 메쉬) |
| 충전 색 | 과열 구분 없음 | 평소 노랑 계열, 과열 파랑 계열 (충전 도중 과열이 바뀌어도 즉시 반영) |
| 발사(임팩트) 때 충전 이펙트 | 즉시 정지 | 새 입자를 멈추고 남은 입자가 빠르게 중심으로 흡수되며 사라짐 |
| 취소(헛스윙) 때 충전 이펙트 | 방출만 멈춤(페이드) | 입자가 온 길을 되짚어 물러나며 사라지고 코어가 줄며 깜빡임 |
| 피격 이펙트 | `VFX_DroneAtkHit.prefab` (`VFX_Spark` 복제) | `VFX_DroneLaserHit.prefab` + `VFX_LaserHit` (중심 충격, 번개, 스파크, 잔불) |
| 피격 색 | 과열 구분 없음 | 평소 노랑 계열, 과열 파랑 계열 |
| 아래 방향 정렬 | 충전 이펙트가 드론 밑에 깔림 | 아래(5, 6, 7) 방향일 때 드론보다 한 칸 위로 보정 |

기존 프리팹(`VFX_Charging.prefab`, `VFX_DroneAtkHit.prefab`)은 **삭제하지 않았습니다.** 풀 항목이 가리키는 대상만 바뀝니다(롤백 방법은 8장).

---

## 2. 함께 가져가야 하는 파일 (모두 신규, `.meta` 포함)

`Drone.prefab`의 풀 항목이 GUID로 아래 프리팹을 참조합니다. **한 파일이라도 빠지면 참조가 끊기므로 `.meta`까지 반드시 같이 커밋/적용해야 합니다.**

| 종류 | 경로 | GUID |
|---|---|---|
| 스크립트 | `Assets/Scripts/Presentation Layer/VFX/VFX_ChargeVortex.cs` | (`.meta` 참조) |
| 스크립트 | `Assets/Scripts/Presentation Layer/VFX/VFX_LaserHit.cs` | (`.meta` 참조) |
| 프리팹 | `Assets/Prefabs/VFX/Drone/VFX_DroneChargeVortex.prefab` | `7a482d111a2444d4db1dd0071d0e4df9` |
| 프리팹 | `Assets/Prefabs/VFX/Drone/VFX_DroneLaserHit.prefab` | `fe5b879de8801ec4caa46d202f7bbf9a` |
| 머티리얼 | `Assets/Graphics/VFX/Materials/M_DroneChargeVortex.mat` | (`.meta` 참조) |
| 머티리얼 | `Assets/Graphics/VFX/Materials/M_DroneLaserHit.mat` | (`.meta` 참조) |

전제(이미 저장소에 있어야 하는 것)
- `PixelQuadBuffer.cs` (`PresentationLayer.VFX`, 커밋 `d98898b8`에 포함)
- 셰이더 `Custom/VFX/BrandWrapGlow` (`Assets/Shaders/VFX/BrandWrapGlow.shader`, 두 머티리얼이 사용)
- 두 프리팹의 소팅 레이어: `VFX_DroneLaserHit`은 기존 `VFX_DroneAtkHit`의 루트 렌더러와 같은 레이어(ID `1613316897`, 오더 0)를 씁니다.

머티리얼 값
- `M_DroneChargeVortex.mat`: `_GlowMin 1.3`, `_GlowMax 4.2`
- `M_DroneLaserHit.mat`: `_GlowMin 1.3`, `_GlowMax 4.2`

---

## 3. `Drone.cs` 변경 (8곳) - 아직 적용되지 않은 상태

파일: `Assets/Scripts/Presentation Layer/ComponentSystem/DroneComponent/Drone.cs`

### 3-1. 필드 추가 (`chargingVfxFadeTimer` 아래)
```csharp
private PresentationLayer.VFX.VFX_ChargeVortex chargingVortex; // 충전 이펙트의 픽셀 소용돌이 컴포넌트. 충전 도중 과열 상태가 바뀌면 색 팔레트를 갱신한다
```

### 3-2. `SetOverheatState` — 충전 중이면 색 갱신
`isOverheat = _isOverheat;` 바로 아래에 추가:
```csharp
if (bChanged && chargingVortex != null) chargingVortex.SetOverheat(isOverheat);
```
(`bChanged`는 커밋된 아우라 연동에서 이미 정의되어 있습니다.)

### 3-3. `PlayAtkHitVfx` — 재생 결과를 받아 과열 상태 전달
```csharp
public void PlayAtkHitVfx(Vector3 _position)
{
    if (vfxComponent == null) return;
    ParticleSystem hitVfx = vfxComponent.Play(new VFXPlaySettings(atkHitVfxTag, _position, Quaternion.identity));
    if (hitVfx == null) return;

    // 픽셀 피격 이펙트가 지금 과열 상태에 맞는 색(평소 노랑, 과열 파랑)으로 나오게 한다
    PresentationLayer.VFX.VFX_LaserHit laserHit = hitVfx.GetComponentInChildren<PresentationLayer.VFX.VFX_LaserHit>(true);
    if (laserHit != null) laserHit.SetOverheat(isOverheat);
}
```

### 3-4. `PlayChargingVfx` — 컴포넌트 연결, 과열 상태와 충전 시간 전달
`chargingVfxMaxLifetime = ComputeChargingVfxMaxLifetime();` 바로 아래(같은 `if (chargingVfx != null)` 블록 안):
```csharp
// 픽셀 소용돌이에게 지금 과열 상태와 임팩트까지 남은 시간을 알려 준다(충전 진행도와 색이 여기에 맞춰진다)
chargingVortex = chargingVfx.GetComponentInChildren<PresentationLayer.VFX.VFX_ChargeVortex>(true);
if (chargingVortex != null)
{
    chargingVortex.SetOverheat(isOverheat);
    float timeUntilImpact = GetTimeUntilImpact();
    if (timeUntilImpact < float.MaxValue) chargingVortex.SetChargeDuration(timeUntilImpact);
}
```

### 3-5. `ClearChargingVfxRefs` — 참조 정리
`chargingVfx = null;` 아래에 `chargingVortex = null;` 추가.

### 3-6. `ReleaseChargingVfx` 신규 메서드 (`StopChargingVfx` 아래, `UpdateChargingVfxFade` 위)
```csharp
private void ReleaseChargingVfx()
{
    if (chargingVortex == null || !IsChargingVfxOwned())
    {
        StopChargingVfx(true);
        return;
    }

    chargingVortex.Release();
    StopChargingVfx(false);
}
```
동작: 소용돌이 컴포넌트가 없거나 이 드론 소유가 아니면 예전처럼 즉시 정리하고, 있으면 발사 연출을 시킨 뒤 방출만 멈춰 페이드 상태로 둡니다. 풀 반환은 기존 `UpdateChargingVfxFade`가 맡습니다.

### 3-7. 임팩트 프레임 호출 교체 (`UpdateAnimationFrame`)
```csharp
// 이전
if (!bChargingVfxFading) StopChargingVfx(true);
// 이후
if (!bChargingVfxFading) ReleaseChargingVfx();
```

### 3-8. `UpdateChargingVfxPosition` — 아래 방향 정렬 보정
`int order = customSortable.ComputeSortingOrder(muzzlePos.y);` 바로 아래:
```csharp
// 아래를 볼 때(왼쪽 아래 5, 아래 6, 오른쪽 아래 7) 총구가 본체보다 위쪽이라 순서가 본체보다 낮게 나와 이펙트가 드론 밑에 깔린다 - 본체보다 한 칸 위로 끌어올린다
if (5 <= dirIndex && 7 >= dirIndex) order = Mathf.Max(order, customSortable.CurrentSortingOrder + 1);
```

---

## 4. `Drone.prefab` 변경 (풀 항목 2줄) - 아직 적용되지 않은 상태

`vfxPoolDataList`의 두 항목에서 `effectPrefab` 한 줄씩만 바뀝니다. 풀 크기(`initialPoolSize`, `maxPoolSize`, `allowDynamicExpansion`)는 **그대로**입니다.

```yaml
  - vfxTag: DroneCharging
-    effectPrefab: {fileID: 3687614857192216124, guid: 5a7db7e738c592d44a3ec5eb91ec9777, type: 3}
+    effectPrefab: {fileID: 7006864300503741061, guid: 7a482d111a2444d4db1dd0071d0e4df9, type: 3}
    initialPoolSize: 1
    allowDynamicExpansion: 0
    maxPoolSize: 1
  - vfxTag: DroneAtkHit
-    effectPrefab: {fileID: 4414206344302795060, guid: 9a9fb228c74b39f4f872c5d7d6ba19e9, type: 3}
+    effectPrefab: {fileID: 7006864300503741061, guid: fe5b879de8801ec4caa46d202f7bbf9a, type: 3}
    initialPoolSize: 10
    allowDynamicExpansion: 1
    maxPoolSize: 20
```

`fileID 7006864300503741061`은 두 새 프리팹의 루트 GameObject ID입니다(같은 값이지만 GUID가 달라 충돌하지 않습니다). 텍스트로 직접 고치지 말고 Unity 인스펙터에서 풀 항목의 프리팹을 지정하는 편이 안전합니다.

---

## 5. 기대 동작과 수동 테스트 체크리스트

1. **충전**: 공격 스윙이 시작되면 총구 중앙에 픽셀 코어가 생기고 사방에서 작은 `.` `+` `x` 입자가 곧장 중심으로 모입니다. 시간이 갈수록 코어가 커지고 입자가 늘어납니다.
2. **발사**: 임팩트 프레임에서 새 입자가 멈추고 남은 입자가 빠르게 흡수됩니다. 코어는 한 번 부풀었다 줄어듭니다.
3. **취소(대상 상실, 헛스윙)**: 입자가 물러나며 작아지고 깜빡이다 사라지고 코어가 줄며 깜빡입니다. **한 프레임에 뚝 사라지면 안 됩니다.**
4. **과열**: 과열 상태에서는 충전과 피격 이펙트가 파랑 계열입니다. 충전 도중 과열이 켜지거나 꺼져도 색이 바로 바뀝니다.
5. **아래 방향**: 드론이 아래, 왼쪽 아래, 오른쪽 아래를 볼 때 충전 이펙트가 드론 본체 위에 보입니다.
6. **피격**: 나무 꼭대기에 중심 충격, 지직거리는 번개, 스파크, 잔불이 나오고, 번개는 점멸하며 무작위로 사라집니다. 연쇄 타격으로 여러 나무가 동시에 맞아도 각각 재생됩니다.
7. **재충전**: 공격 속도가 빠른 상태에서 연속으로 충전해도 이펙트가 겹치거나 누수되지 않는지 확인합니다.
8. **던전 이동, 드론 숨김, 풀 반환** 직후 이펙트가 남아 있지 않은지 확인합니다.

---

## 6. 잠재적 문제와 위험

각 항목에 확인 수준을 표시했습니다. **[코드 확인]** 은 코드를 읽어 확인한 사실, **[추정]** 은 실행으로 검증하지 못한 추정입니다.

### 6-1. 발사 연출이 다음 충전에 잘릴 수 있음 [코드 확인 + 추정]
발사 뒤에는 방출만 멈춘 채 페이드 상태(`bChargingVfxFading = true`)로 남고, 풀 반환은 `chargingVfxFadeTimer`(= 루트 파티클 최대 수명 0.7초 + 0.2초 = 약 0.9초)가 끝나야 이뤄집니다. `PlayChargingVfx`는 페이드 중인 이펙트가 남아 있으면 즉시 정리(`StopChargingVfx(true)`)하고 새로 켭니다. 따라서 **공격 속도가 올라 다음 충전이 0.9초 안에 시작되면**, 직전 발사의 흡수 연출(약 0.13~0.25초)이 끝나기 전에 통째로 잘릴 수 있습니다. 실제로 그런 공격 간격이 나오는지는 확인하지 못했습니다.

### 6-2. 충전 풀 크기가 1이고 확장 불가 [코드 확인]
`DroneCharging` 항목은 `initialPoolSize 1`, `allowDynamicExpansion 0`, `maxPoolSize 1`입니다. 이전에도 같은 설정이지만, 새 이펙트는 발사 뒤에도 약 0.7초 동안 인스턴스가 살아 있으므로(6-3 참고) 풀에서 꺼낼 수 없는 순간이 생길 위험이 커졌습니다. 풀이 비었을 때 `VFXComponent.Play`가 `null`을 반환하는지는 확인하지 못했습니다. `PlayChargingVfx`는 결과가 `null`이어도 참조를 정리하지 않고 진행하므로 충전 이펙트가 조용히 안 나올 수 있습니다. 문제가 보이면 풀 크기를 2로 올리고 확장을 허용하는 것을 검토하세요.

### 6-3. 인스턴스 수명이 루트 ParticleSystem의 "보이지 않는 입자"에 의존 [코드 확인 + 추정]
풀 헬퍼(`VFXPoolInstanceHelper`)는 루트 ParticleSystem이 멈추면 인스턴스를 회수하는 것으로 알고 있습니다(정확한 조건은 이 작업에서 다시 읽어 검증하지 않았습니다).
- 충전 프리팹 루트: 방출 30개/초, 수명 0.7초, 최대 32개, 렌더러 비활성. 취소 또는 발사 순간에도 살아 있는 입자가 항상 있게 해서 소화 연출 중 인스턴스가 회수되지 않게 하려는 설정입니다.
- 피격 프리팹 루트: 비루프, `duration 0.1`, 시작 시점에 입자 1개(수명 0.7초). 이 값보다 이펙트가 길어지면(현재 약 0.55초) 중간에 회수될 수 있습니다. 나중에 이펙트 길이를 늘릴 때는 루트 수명도 함께 늘려야 합니다.
- 이 값이 바뀌면 취소 시 이펙트가 **소화 연출 없이 즉시 사라지는** 증상(예전에 한 번 회귀했던 문제)이 재발할 수 있습니다.

### 6-4. 아래 방향 이외에서는 드론 뒤로 갈 수 있음 [코드 확인]
정렬 보정은 `dirIndex` 5, 6, 7일 때만 적용됩니다. 옆이나 위쪽 방향은 기존처럼 총구 Y 기준으로 정렬되므로, 총구가 본체 뒤로 계산되는 자세에서는 이펙트가 드론에 가려질 수 있습니다(위쪽에서는 의도된 동작입니다). 다른 방향에서도 가려지는 사례가 보이면 범위를 넓히거나 정책을 정해야 합니다.

### 6-5. 피격 이펙트의 호출 순서 의존성 [코드 확인]
`VFX_LaserHit.SetOverheat`는 `Play` 직후에 호출됩니다. 컴포넌트는 `OnEnable`에서 `Begin()`을 실행하며 과열 플래그를 `false`로 초기화하므로, **`Play`가 오브젝트를 같은 프레임 안에서 활성화하는 전제**입니다. 풀 헬퍼가 활성화를 지연시키면 색이 잘못 나올 수 있습니다(충전 이펙트의 `SetOverheat`도 동일한 전제).

### 6-6. 히트마다 `GetComponentInChildren` 호출 [추정]
`PlayAtkHitVfx`는 히트마다 `GetComponentInChildren<VFX_LaserHit>`를 호출합니다. 연쇄 타격으로 한 프레임에 여러 번 호출될 수 있습니다. 할당이나 비용을 측정하지 않았습니다. 프로파일러에서 문제가 되면 풀 인스턴스별로 캐싱하는 방식을 검토하세요.

### 6-7. `GetTimeUntilImpact()`가 `PlayChargingVfx`에서 추가로 호출됨 [코드 확인]
이 함수는 내부에서 `GetAttackSprites(dirIndex, out _)`를 호출합니다. 이 호출이 힙 할당을 하는지는 확인하지 못했습니다. 스윙마다 한 번 호출됩니다.

### 6-8. 소팅 레이어와 나무와의 앞뒤 관계 [추정]
피격 이펙트는 기존 프리팹과 같은 소팅 레이어(ID `1613316897`, 오더 0)를 씁니다. 나무 꼭대기 위치에서 나무 스프라이트보다 앞에 그려지는지 플레이 모드에서 확인하지 않았습니다.

### 6-9. 블룸 값은 실제 화면에서 조율되지 않음 [추정]
`_GlowMin 1.3 / _GlowMax 4.2`는 앞선 이펙트에서 사용자가 조정한 수치를 시작값으로 옮긴 것입니다. 블룸이 켜진 게임 화면에서 이 두 이펙트를 직접 보지는 못했습니다. 너무 밝거나 어두우면 머티리얼 값과 프리팹의 `glowScale`로 조정합니다.

### 6-10. 낡은 주석 [코드 확인]
`Drone.cs`의 `ReleaseChargingVfx` 위 주석은 "입자가 바깥으로 퍼지며 사라지는 연출"이라고 적혀 있지만, 최종 동작은 **남은 입자가 중심으로 빠르게 흡수되는 것**입니다. 적용 후 주석을 실제 동작에 맞게 고치세요.

### 6-11. 그 외 참고
- `VFX_ChargeVortex`, `VFX_LaserHit`은 각자 정적 `PixelQuadBuffer`를 씁니다. 메인 스레드에서 각 컴포넌트가 자기 `LateUpdate` 안에서 채우고 곧바로 업로드하므로 인스턴스끼리 겹치지 않습니다. 멀티스레드로 호출하면 안 됩니다.
- `Drone.prefab`은 저장소 설정상 LF/CRLF 변환 경고가 나옵니다. 내용은 풀 항목 2줄뿐이며 나머지 줄바꿈이 대량으로 바뀌지 않았는지 커밋 전에 `git diff --stat`으로 확인하세요.
- 새 프리팹은 이전 작업의 과열 아우라 프리팹을 복제해 만들었기 때문에 루트에 `Shape` 모듈 등 사용하지 않는 설정이 남아 있습니다(렌더러는 비활성).
- 두 컴포넌트는 인스펙터 노출 값이 많습니다. 프리팹에 저장된 값이 스크립트 기본값과 다를 수 있으므로, 값을 바꿀 때는 프리팹 쪽 값이 우선한다는 점에 주의하세요.

---

## 7. 에이전트에게 그대로 붙여 넣을 프롬프트

```
Drone 충전/피격 픽셀 VFX 적용 작업입니다. 아래 문서를 끝까지 읽고 그대로 적용해 주세요: Docs/DroneChargeAndHitVFX_Guide.md

지켜야 할 것
1. 문서 2장의 신규 파일 6종(.meta 포함)이 모두 저장소에 있는지 먼저 확인한다. 하나라도 없으면 멈추고 보고한다.
2. Assets/Scripts/Presentation Layer/ComponentSystem/DroneComponent/Drone.cs 에 문서 3장의 8곳을 적용한다. 기존 변수명은 바꾸지 않는다.
3. Drone.prefab 은 텍스트 편집 대신 Unity에서 DroneCharging 과 DroneAtkHit 풀 항목의 프리팹만 4장대로 교체한다. 풀 크기는 그대로 둔다.
4. Application Layer 와 Character.cs 는 수정하지 않는다.
5. 컴파일 오류와 경고가 0개인지 콘솔로 확인한다.
6. 문서 5장의 수동 테스트 체크리스트를 실행해 결과를 표로 보고하고, 6장의 위험 중 실제로 재현된 것이 있으면 별도로 적는다.
7. 6-10의 낡은 주석은 실제 동작(남은 입자가 중심으로 빠르게 흡수)에 맞게 고친다.
8. 위 테스트가 모두 통과한 뒤, 문서 9장의 절차대로 기존 이펙트 잔여물(옛 충전/피격 프리팹과 그것만 쓰던 리소스)을 찾아 참조 0개를 확인하고 지운다. 삭제는 적용 커밋과 별도 커밋으로 한다.
```

---

## 8. 적용 후 롤백 방법 (9장 정리를 하기 전에만 가능)

적용한 뒤 문제가 생기면 풀 항목 2줄만 원래 값으로 돌리면 기존 이펙트로 복귀합니다. **9장에서 기존 프리팹을 삭제한 뒤에는 이 방법을 쓸 수 없고, 삭제 커밋을 되돌려야 합니다.**

- `DroneCharging` → GUID `5a7db7e738c592d44a3ec5eb91ec9777` (`VFX_Charging.prefab`), fileID `3687614857192216124`
- `DroneAtkHit` → GUID `9a9fb228c74b39f4f872c5d7d6ba19e9` (`VFX_DroneAtkHit.prefab`), fileID `4414206344302795060`

`Drone.cs`의 새 코드는 `chargingVortex`가 `null`이거나 피격 이펙트에 `VFX_LaserHit`가 없으면 자동으로 기존 동작(즉시 정지, 색 전달 없음)으로 동작하므로 코드를 되돌리지 않아도 롤백 상태가 안전합니다. 다만 3-8의 정렬 보정은 기존 프리팹에도 적용됩니다.

---

## 9. 기존 이펙트 잔여물 정리 (적용과 QA가 끝난 뒤, 별도 커밋으로)

새 이펙트가 정상 동작하는 것을 확인한 뒤에는 더 이상 쓰이지 않는 옛 이펙트 파일을 지웁니다. **지우기 전에 반드시 아래 참조 검사를 다시 실행하세요.** 아래 숫자는 이 문서를 쓴 시점(저장소가 `eee10499`, Drone.prefab이 옛 프리팹을 가리키는 상태)에서 옛 프리팹 자신을 제외하고 센 값이며, 그사이 다른 작업으로 달라졌을 수 있습니다.

### 9-1. 삭제 대상 (Drone.prefab의 풀 항목이 새 프리팹으로 바뀐 뒤)

| 대상 | 경로 | GUID | 이유 |
|---|---|---|---|
| 옛 충전 프리팹 | `Assets/Prefabs/VFX/Drone/VFX_Charging.prefab` (+ `.meta`) | `5a7db7e738c592d44a3ec5eb91ec9777` | `DroneCharging` 풀 항목이 새 프리팹으로 바뀌면 사용처 없음 |
| 옛 피격 프리팹 | `Assets/Prefabs/VFX/Drone/VFX_DroneAtkHit.prefab` (+ `.meta`) | `9a9fb228c74b39f4f872c5d7d6ba19e9` | `DroneAtkHit` 풀 항목이 새 프리팹으로 바뀌면 사용처 없음 |
| 옛 충전 프리팹만 쓰던 스프라이트 | `Assets/Graphics/VFX/Resources/Basic/Pop.png` (+ `.meta`) | `a1aa6f239eff17d458fe01659ffae7e6` | 작성 시점에 참조가 옛 충전 프리팹 하나뿐이었음(다른 프리팹, 씬, 코드에서 참조 0개) |

`Pop.png`는 `Resources` 폴더 안에 있어서 코드에서 이름 문자열로 불러올 수도 있습니다. 작성 시점에는 `Assets/Scripts` 전체에서 `"Pop"` 또는 `Basic/Pop`을 찾는 코드가 없었지만, 삭제 직전에 다시 검색하세요.

### 9-2. 지우면 안 되는 것 (다른 곳에서 쓰이고 있음)

옛 프리팹이 참조하던 아래 리소스는 다른 곳에서도 쓰이므로 **남겨야 합니다.** 작성 시점의 다른 파일 참조 수입니다.

| 리소스 | 다른 파일 참조 수 |
|---|---|
| `Assets/Graphics/VFX/Materials/Basic/M_VFXBasicHDR.mat` | 18 |
| `Assets/Graphics/VFX/Resources/Basic/Dot.png` | 23 |
| `Assets/Graphics/Character/EditPlayer/Player_Down_D.png` | 8 |
| `Assets/Graphics/VFX/Resources/Tree/SporepuffTree_Top_00.png` | 3 |
| `Assets/Graphics/VFX/Materials/M_2DLightningLine.mat` | 1 |
| `Assets/Graphics/VFX/Materials/Basic/M_VFXDust.mat` | 8 |

### 9-3. 삭제 절차

1. 3, 4장 적용과 5장 테스트가 끝나 새 이펙트가 정상인지 확인한다.
2. 삭제 대상마다 GUID 참조를 검색한다. 본인의 `.meta`를 제외하고 **0개**여야 한다.
   ```bash
   grep -rl "<GUID>" Assets ProjectSettings --include=*.prefab --include=*.unity --include=*.asset --include=*.mat --include=*.controller --include=*.anim
   ```
3. 참조가 남아 있으면 삭제하지 말고 그 참조를 누가 쓰는지 보고한다.
4. `VFXComponent` 풀 항목은 `vfxTag` 문자열(`DroneCharging`, `DroneAtkHit`)로 참조하고 프리팹 이름으로는 참조하지 않는다. 태그와 `Drone.cs`의 `chargingVfxTag`, `atkHitVfxTag` 필드는 **삭제하지 않는다.**
5. 파일은 `.meta`와 함께 삭제한다.
6. Unity를 열어 콘솔에 `Missing`, `The referenced script/asset is missing` 계열 메시지가 없는지 확인하고, 충전과 피격을 다시 한 번 재생해 본다.
7. 삭제는 적용 커밋과 **별도 커밋**으로 만들어서, 문제가 생기면 삭제 커밋만 되돌릴 수 있게 한다.

### 9-4. 코드 쪽 잔여물 점검

- `Drone.cs`: 적용한 뒤 사용되지 않게 된 변수나 함수가 있는지 확인한다. 기존 `chargingVfxRenderers` 주석에 적힌 "VFX_OverHeating 등" 표현은 새 프리팹과 맞지 않으니 실제 구조에 맞게 고친다(새 충전 프리팹은 루트 렌더러 하나와 픽셀 메쉬 자식 구조). `ReleaseChargingVfx`의 `chargingVortex == null` 분기는 새 프리팹이 없을 때를 위한 안전 경로라서 남긴다.
- 옛 프리팹에만 필요했던 태그, 상수, 주석이 `Drone.cs` 밖(다른 스크립트)에 남아 있지 않은지 `grep -rn "DroneCharging\|DroneAtkHit\|VFX_Charging\|VFX_DroneAtkHit" Assets/Scripts`로 확인한다.

---

## 부록. `Drone.cs` / `Drone.prefab` 전체 diff (커밋 `eee10499` 대비)

```diff
diff --git a/Assets/Prefabs/Objects/Drone/Drone.prefab b/Assets/Prefabs/Objects/Drone/Drone.prefab
--- a/Assets/Prefabs/Objects/Drone/Drone.prefab
+++ b/Assets/Prefabs/Objects/Drone/Drone.prefab
@@ -394,13 +394,13 @@ MonoBehaviour:
   initializeOnAwake: 1
   vfxPoolDataList:
   - vfxTag: DroneCharging
-    effectPrefab: {fileID: 3687614857192216124, guid: 5a7db7e738c592d44a3ec5eb91ec9777, type: 3}
+    effectPrefab: {fileID: 7006864300503741061, guid: 7a482d111a2444d4db1dd0071d0e4df9, type: 3}
     initialPoolSize: 1
     allowDynamicExpansion: 0
     maxPoolSize: 1
     uiParticleScale: 0
   - vfxTag: DroneAtkHit
-    effectPrefab: {fileID: 4414206344302795060, guid: 9a9fb228c74b39f4f872c5d7d6ba19e9, type: 3}
+    effectPrefab: {fileID: 7006864300503741061, guid: fe5b879de8801ec4caa46d202f7bbf9a, type: 3}
     initialPoolSize: 10
     allowDynamicExpansion: 1
     maxPoolSize: 20
     uiParticleScale: 0
```

```diff
diff --git a/Assets/Scripts/Presentation Layer/ComponentSystem/DroneComponent/Drone.cs b/...
@@ -123,6 +123,7 @@ public class Drone : MonoBehaviour
     private float chargingVfxFadeTimer; // ...
+    private PresentationLayer.VFX.VFX_ChargeVortex chargingVortex; // 충전 이펙트의 픽셀 소용돌이 컴포넌트. 충전 도중 과열 상태가 바뀌면 색 팔레트를 갱신한다
     private ParticleSystemRenderer[] chargingVfxRenderers; // ...

@@ -323,7 +324,12 @@ public class Drone : MonoBehaviour
     public void PlayAtkHitVfx(Vector3 _position)
     {
         if (vfxComponent == null) return;
-        vfxComponent.Play(new VFXPlaySettings(atkHitVfxTag, _position, Quaternion.identity));
+        ParticleSystem hitVfx = vfxComponent.Play(new VFXPlaySettings(atkHitVfxTag, _position, Quaternion.identity));
+        if (hitVfx == null) return;
+
+        // 픽셀 피격 이펙트가 지금 과열 상태에 맞는 색(평소 노랑, 과열 파랑)으로 나오게 한다
+        PresentationLayer.VFX.VFX_LaserHit laserHit = hitVfx.GetComponentInChildren<PresentationLayer.VFX.VFX_LaserHit>(true);
+        if (laserHit != null) laserHit.SetOverheat(isOverheat);
     }

@@ -364,6 +370,8 @@ public class Drone : MonoBehaviour
         bool bChanged = isOverheat != _isOverheat;
         isOverheat = _isOverheat;

+        if (bChanged && chargingVortex != null) chargingVortex.SetOverheat(isOverheat);
+
         // 상태가 바뀔 때만 아우라를 켜고 끈다(Character가 매 프레임 같은 값을 넘겨주므로)
         if (!bChanged) return;

@@ -1133,6 +1141,15 @@ public class Drone : MonoBehaviour
             chargingVfxRenderers = chargingVfx.GetComponentsInChildren<ParticleSystemRenderer>(true);
             chargingVfxMaxLifetime = ComputeChargingVfxMaxLifetime();
+
+            // 픽셀 소용돌이에게 지금 과열 상태와 임팩트까지 남은 시간을 알려 준다(충전 진행도와 색이 여기에 맞춰진다)
+            chargingVortex = chargingVfx.GetComponentInChildren<PresentationLayer.VFX.VFX_ChargeVortex>(true);
+            if (chargingVortex != null)
+            {
+                chargingVortex.SetOverheat(isOverheat);
+                float timeUntilImpact = GetTimeUntilImpact();
+                if (timeUntilImpact < float.MaxValue) chargingVortex.SetChargeDuration(timeUntilImpact);
+            }
         }
     }

@@ -1149,6 +1166,7 @@ public class Drone : MonoBehaviour
     private void ClearChargingVfxRefs()
     {
         chargingVfx = null;
+        chargingVortex = null;
         chargingVfxRenderers = null;

@@ -1184,6 +1202,20 @@ public class Drone : MonoBehaviour
         chargingVfxFadeTimer = chargingVfxMaxLifetime + 0.2f;
     }

+    // 발사 순간의 충전 이펙트 정리. ...
+    private void ReleaseChargingVfx()
+    {
+        if (chargingVortex == null || !IsChargingVfxOwned())
+        {
+            StopChargingVfx(true);
+            return;
+        }
+
+        chargingVortex.Release();
+        StopChargingVfx(false);
+    }
+
     // 페이드가 끝난 뒤(남은 입자가 모두 사라진 뒤) 이펙트를 풀로 돌려보낸다. ...

@@ -1238,6 +1270,10 @@ public class Drone : MonoBehaviour
             int order = customSortable.ComputeSortingOrder(muzzlePos.y);
+
+            // 아래를 볼 때(왼쪽 아래 5, 아래 6, 오른쪽 아래 7) ... 본체보다 한 칸 위로 끌어올린다
+            if (5 <= dirIndex && 7 >= dirIndex) order = Mathf.Max(order, customSortable.CurrentSortingOrder + 1);
+
             for (int i = 0; i < chargingVfxRenderers.Length; i++)

@@ -1280,7 +1316,7 @@ public class Drone : MonoBehaviour
             if (!damageAppliedThisSwing && currentFrameIndex >= impactFrame)
             {
-                if (!bChargingVfxFading) StopChargingVfx(true); // 실제로 "공격하는" 순간 - ...
+                if (!bChargingVfxFading) ReleaseChargingVfx(); // 실제로 "공격하는" 순간 - ...
                 StopChargeSound(false); // 충전음도 발사음에 자리를 넘기며 끊는다
```

(위 diff는 가독성을 위해 일부 주석과 무관한 줄을 `...`으로 줄였습니다. 정확한 원문은 `git diff -- "Assets/Scripts/Presentation Layer/ComponentSystem/DroneComponent/Drone.cs" Assets/Prefabs/Objects/Drone/Drone.prefab`으로 확인하세요.)
