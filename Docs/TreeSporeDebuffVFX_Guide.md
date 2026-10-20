# 나무 포자 디버프 VFX 연동 가이드

포자 대폭발에 맞아 **받는 피해가 영구히 늘어난(증폭) 나무**를 알아보게 하는 상시 이펙트입니다. 이펙트(Presentation Layer)는 구현되어 있고, **`TreeObj`에 호출 3줄을 넣으면 게임에서 재생됩니다.** 연결 전에는 아무것도 나오지 않고 기존 동작은 깨지지 않습니다. (Application Layer는 수정하지 않습니다.)

---

## 1. 한 장 요약

| 순서 | 어디를 | 무엇을 |
|---|---|---|
| C1 | `TreeObj.ApplySporeDamageAmplify` | 증폭이 걸릴 때 `TreeSporeDebuffVfx.Apply(this, sporeDamageAmplify)` |
| C2 | `TreeObj.ResetTree` | 증폭이 풀릴 때 `TreeSporeDebuffVfx.Clear(this)` |
| C3 | `TreeObj` 피해 처리(증폭 곱하는 곳) | 증폭 나무가 맞을 때 `TreeSporeDebuffVfx.NotifyHit(this)` |

세 줄 모두 `using PresentationLayer.VFX;`가 필요합니다. 관리자 프리팹은 `Resources`에서 첫 `Apply` 때 자동으로 만들어지므로 씬이나 풀에 따로 등록할 것이 없습니다.

---

## 2. 연동 코드 (`Assets/Scripts/Presentation Layer/ObjectSystem/TreeObj.cs`)

```csharp
using PresentationLayer.VFX;   // 파일 상단에 추가

public void ApplySporeDamageAmplify(float _amplify)
{
    if (_amplify > sporeDamageAmplify) sporeDamageAmplify = _amplify;
    TreeSporeDebuffVfx.Apply(this, sporeDamageAmplify);                // C1
}

public void ResetTree()
{
    ...
    sporeDamageAmplify = 0f;
    TreeSporeDebuffVfx.Clear(this);                                    // C2
    ...
}

// 피해에 증폭을 곱하는 곳(기존 코드): "포자 대폭발에 맞은 나무 - 이후 받는 피해 증폭"
if (0f < sporeDamageAmplify)
{
    _damage *= 1f + sporeDamageAmplify;
    TreeSporeDebuffVfx.NotifyHit(this);                                // C3
}
```

- `Apply`는 여러 번 불러도 됩니다. 이미 걸려 있으면 더 높은 단계로만 올라갑니다(`ApplySporeDamageAmplify`와 같은 규칙).
- `Clear`를 못 불러도 비활성화된 나무는 다음 틱(1/24초)에 관리자가 스스로 정리합니다.
- **ApplySporeDamageAmplify의 시점**: 이 메서드는 대폭발 피해를 준 직후(`ApplySporeBigExplosion`) 불립니다. 이 피해로 쓰러진 나무에는 효과가 걸리지 않도록 `bDead`/`IsPooled`를 확인하는 것이 안전합니다.

---

## 3. 단계 매핑

`TreeObj`는 증폭률만 알고 단계를 모릅니다. Application Layer를 건드리지 않으려고 **증폭률로 단계를 정합니다**(`TreeSporeDebuffVfx.TierFromAmplify`).

| 증폭률 | 단계 | 색 |
|---|---|---|
| 0.25 미만 (기본 +20%) | 1 | 독성 초록 |
| 0.4 미만 (기본 +30%) | 2 | 암자색 |
| 0.4 이상 (기본 +50%) | 3 | 녹슨 주황 |

`InDungeonObjectManager`의 단계별 증폭 수치(`sporeBigExplosionTier1~3DamageAmplify`)를 바꾸면 위 기준(`Tier2Threshold`, `Tier3Threshold`)도 같이 확인해 주세요.

---

## 4. 구성과 성능 설계

| 구성 | 내용 |
|---|---|
| 나무 오버레이 (나무 `BaseVisual/Top`의 자식 SpriteRenderer 1개) | 어두운 체커 디더, 각진 얼룩, 흘러내린 점액 줄무늬, 균열 선을 **미리 구운 스프라이트**. 나무 색/머티리얼은 건드리지 않음 |
| 바닥 링 (스프라이트 1개) + 공유 바닥 메쉬 | 점선 링과 안쪽 디더는 구운 스프라이트, 링을 도는 밝은 구간과 바닥 싹은 공유 메쉬 |
| 공유 상단 메쉬 | 아래를 가리키는 표식(▼, 단계 수), 맺혔다 떨어지는 독액, 부패 조각, 흐르는 빛, 타격 반응 |

- **위치는 나무 모양에서 뽑습니다.** `TreeSilhouetteMask`가 나무 스프라이트의 알파 마스크를 스프라이트당 한 번만 읽어 캐시하고(텍스처가 읽기 불가여도 GPU 복사로 읽음), `TreeDebuffLayout`이 불투명 픽셀 안에서 반점/줄무늬/균열/독액 위치를 고릅니다. 나무 종류가 달라도 같은 코드로 맞습니다.
- **갱신은 24Hz 틱**에서만 합니다. 틱이 같고 변화가 없으면 아무것도 하지 않습니다.
- **상한**: 화면 안(+여백 4유닛) 나무만 처리하고, 카메라 중심에 가까운 순으로 `maxFullTrees`(16)그루만 전체 연출입니다. 나머지는 링과 표식만 그립니다. 등록 상한은 128그루입니다.
- 위치는 틱 시각과 나무별 해시로 정하는 닫힌 식이라 입자 목록이 없고, 재생 중 힙 할당이 없습니다.

### 측정한 값 (에디터 편집 모드, 실제 나무 스프라이트 4종)

| 상황 | 상단 쿼드 | 바닥 쿼드 |
|---|---|---|
| 4그루 평상시 | 약 365~400 | 약 30~60 |
| 4그루 타격 직후 | 약 520~600 | 약 50 |
| 24그루 등록(전체 연출 16그루) 평상시 | 약 2,150~2,200 | 약 200~250 |
| 24그루 타격 직후 | 약 2,900~3,100 | 약 200~250 |

메쉬 쿼드 한도(15,000)에 여유가 있습니다. **드로우 콜은 메쉬 2장 + 화면 안 나무의 바닥 링 스프라이트(같은 텍스처라 배칭 가능) + 전체 연출 나무의 오버레이 스프라이트(최대 16)** 입니다.

---

## 5. 만든 파일

| 파일 | 내용 |
|---|---|
| `Assets/Scripts/Presentation Layer/VFX/TreeSporeDebuff/TreeSporeDebuffVfx.cs` | 진입점(`Apply`/`Clear`/`NotifyHit`) |
| `.../TreeSporeDebuffVfxManager.cs` | 관리자(등록, 틱, 컬링, 메쉬 2장) |
| `.../TreeSilhouetteMask.cs` | 나무 스프라이트 알파 마스크 캐시 |
| `.../TreeDebuffLayout.cs` | 반점/줄무늬/균열/독액 배치, 링 모양 |
| `.../TreeDebuffOverlayBaker.cs` | 오버레이/바닥 링 스프라이트 굽기, 단계 팔레트 |
| `Assets/Scripts/Presentation Layer/VFX/Editor/TreeSporeDebuffBuilder.cs` | 메뉴 `LumberBoy/VFX/Build Tree Spore Debuff` (여러 번 실행해도 같은 결과) |
| `Assets/Graphics/VFX/Materials/M_TreeSporeDebuff.mat`, `M_TreeSporeDebuffSprite.mat` | 메쉬용/스프라이트용 머티리얼 |
| `Assets/Graphics/VFX/Resources/TreeSporeDebuff/TreeSporeDebuffVfxManager.prefab` | 관리자 프리팹(`Resources` 로드) |

---

## 6. 튜닝

프리팹 `TreeSporeDebuffVfxManager`의 인스펙터 값입니다.

| 필드 | 기본 | 용도 |
|---|---|---|
| `maxFullTrees` | 16 | 전체 연출을 그리는 최대 나무 수. 프레임이 부족하면 낮춘다 |
| `cullMargin` | 4 | 화면 밖 판정 여백(월드 유닛) |
| `groundSortingLayerName` / `groundSpriteOrder` / `groundMeshOrder` | `ShockWave` / 0 / 1 | 링과 바닥 싹의 소팅 |
| `topSortingLayerName` / `topMeshOrder` | `FlyingItem` / 12 | 표식/독액/조각의 소팅 |

코드 상수로 조정하는 값(필요하면 요청 주세요): 슬롯 수(`SproutSlots`, `FlakeSlots`), 반점/줄무늬/균열 개수(`TreeDebuffLayout.Build`), 링 반지름(`TreeDebuffRing.RadiusX`), 어두운 디더 농도(`TreeDebuffOverlayBaker.DitherColor`), 단계 색(`TreeDebuffPalette.Bytes`).

---

## 7. 주의와 미검증 항목 (게임에서 확인이 필요합니다)

| 항목 | 상태 |
|---|---|
| 오버레이 정렬 | 오버레이 순서를 `GetTopHighlightSortingOrder() + 2`로 매 틱 맞춘다. `CustomSortable`이 나무 렌더러 순서를 갱신하는 시점과 어긋나는지는 미확인 |
| `CustomSortable` 재초기화 | 오버레이는 `Top`의 자식이다. 나무가 풀에서 재사용될 때 `CustomSortable.Initialize`가 자식 전체를 다시 모으면 오버레이 순서를 덮어쓸 수 있다(미확인) |
| 물 위 나무 / 방패 / 젬 나무 | `Visual_ForWater`, 방패, 젬 머티리얼 상태에서의 겹침은 확인하지 못했다. 물 위 나무는 오버레이가 나오지 않을 수 있다 |
| 묘목(`Top(Baby)`) | 묘목에 증폭이 걸리는지, 걸릴 때 모양이 맞는지 미확인 |
| 쓰러짐 연출 | 나무가 죽을 때 페이드(`FadeAlpha`)가 오버레이/바닥 링에는 적용되지 않는다. `ResetTree`가 `Clear`를 부르는 시점에 사라진다 |
| 서브픽셀 정렬 | 나무 스프라이트 피벗이 정수 격자가 아니라(Y 8.32) 메쉬 픽셀과 최대 0.5px 어긋날 수 있다 |
| 좌우 반전 | 나무 스프라이트가 `flipX`로 반전되는 경우는 처리하지 않았다 |
| 플레이 모드 재생 / 실제 프레임 | 에디터 편집 모드 캡처로만 확인했다. 실제 던전 밀도에서의 프레임은 측정하지 못했다 |
| 데미지 숫자 | 이펙트에 숫자는 없다(게임의 기존 피해 표시를 쓴다) |
| 시든 색 | 나무 색을 바꾸지 않는다. 대신 중립 어두운 체커 디더(알파 약 0.4)와 단계색 장식이 "탁한 느낌"을 준다 |
