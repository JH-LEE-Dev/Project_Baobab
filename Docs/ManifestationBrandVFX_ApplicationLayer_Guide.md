# 발현 낙인(별자리 각인/별 감쌈) 이펙트 - Application Layer 적용 가이드

> 대상 독자: Application Layer 스크립트 수정 권한이 있는 프로그래머, 또는 그 프로그래머가 사용하는 코딩 에이전트.
> 이 문서만 보고 동일한 변경을 재현할 수 있도록 작성했다. 문서 맨 아래 **부록 A**에 실제 적용했던 `git diff` 원문이 있으며,
> 본문 코드와 diff가 다르면 **diff를 정본**으로 본다.

---

## 0. 요약

| 항목 | 내용 |
|---|---|
| 기능 | 별자리 광선(Constellation Beam)에 맞은 나무에 (1) 각인 VFX `VFX_BrandStamp` 재생, (2) 살아남은 나무는 낙인 유지 동안 `VFX_BrandStarWrap`(나무를 감싸고 도는 별) 표시 |
| 기존 동작 | 낙인 나무 위에서 `VFX_Spark`(`ManifestationBrandSparkEffect`)를 랜덤 인터벌로 반복 재생 |
| 추가 수정 | 광선에 맞아 **죽은** 나무에도 각인 재생, 그리고 `!tree.bDead` 죽음 판별 버그 수정 |
| 수정 파일(3개) | `InDungeonVFXManager.cs`, `InDungeonObjectManager.cs`, `GameInstaller.prefab` |
| 수정하지 않는 것 | 나머지 Application Layer 코드, Presentation Layer(이미 브랜치에 있음), 씬 |

### 0-1. 변경의 두 종류를 분리해서 볼 것

1. **이펙트 교체(선택 가능)** - 스파크를 각인+별 감쌈으로 바꾼다. 3개 파일 모두 관여.
2. **버그 수정(이펙트와 무관하게 권장)** - `ApplyConstellationBeamHit`의 `!tree.bDead` 판정이 항상 통과되는 문제. 5장 "죽음 판별" 참고. 1번을 적용하지 않더라도 이 수정은 별도로 적용할 가치가 있다.

---

## 1. 전제 조건 (이미 저장소에 있어야 하는 에셋)

아래 파일은 Application Layer가 아니므로 이미 브랜치(`SW-Branch`)에 들어 있다. 없으면 먼저 확보한다.

| 종류 | 경로 | 비고 |
|---|---|---|
| 스크립트 | `Assets/Scripts/Presentation Layer/VFX/VFX_BrandStarWrap.cs` (+ `.meta`) | 네임스페이스 `PresentationLayer.VFX` |
| 스크립트 | `Assets/Scripts/Presentation Layer/VFX/VFX_BrandStampBurst.cs` (+ `.meta`) | `VFX_BrandStamp` 프리팹의 자식에 붙어 있음 |
| 프리팹 | `Assets/Prefabs/VFX/Constellation/VFX_BrandStamp.prefab` | GUID `38080eb86596a2d47ad5ffd15146ba9d`, 루트 `ParticleSystem` fileID `2504916916603310124` |
| 프리팹 | `Assets/Prefabs/VFX/Constellation/VFX_BrandStarWrap.prefab` | GUID `8929e93c80181374cba3211c79d18a0f`, 루트 `VFX_BrandStarWrap` 컴포넌트 fileID `137350371921299027` |
| 머티리얼 | `M_BrandStamp`, `M_BrandStampFlash`, `M_BrandStampBurst`, `M_BrandStarWrap` (`Assets/Graphics/VFX/Materials/`) | |
| 셰이더 | `Assets/Shaders/VFX/BrandSparkle.shader`, `BrandWrapGlow.shader` | |
| 텍스처 | `Assets/Graphics/VFX/Resources/ManifestationBrand/BrandStamp.png` | |

확인 방법: 각 `.meta`의 `guid`가 위 표와 같은지, 프리팹이 Unity에서 오류 없이 열리는지 확인한다.
GUID가 다르면(에셋이 새로 임포트된 경우) 3장의 `GameInstaller.prefab` 참조를 실제 GUID로 바꿔야 한다.

기존에 `Assets/Prefabs/VFX/Lightning/VFX_Spark.prefab`(GUID `ae8dfa960fb2f644f89d0e6dd44ea1da`)이 사용되던 자리를 대체하는 것이다. 이 프리팹을 지울지는 6-7 참고.

---

## 2. `InDungeonVFXManager.cs` 수정

경로: `Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs`

새 `using`은 필요 없다(`ObjectPool`, `List`는 이미 사용 중이고, 새 타입은 `PresentationLayer.VFX.VFX_BrandStarWrap`로 완전 한정해서 쓴다).

### 2-1. 직렬화 필드 + 풀 필드 추가

`private IObjectPool<TreeTransformVFX> treeTransformVfxPool;` 바로 아래, `public void Initialize()` 위에 추가:

```csharp
    [Header("Manifestation Brand Star Wrap (낙인 나무를 감싸는 별)")]
    [SerializeField] private PresentationLayer.VFX.VFX_BrandStarWrap brandStarWrapPrefab;
    [SerializeField] private int brandStarWrapPoolDefaultCapacity = 8;
    [SerializeField] private int brandStarWrapPoolMaxSize = 32;
    // 화면 가득 낙인 나무일 때도 부하가 커지지 않도록 동시에 별이 감싸는 나무 수의 상한
    [SerializeField] private int brandStarWrapMaxActive = 20;

    private IObjectPool<PresentationLayer.VFX.VFX_BrandStarWrap> brandStarWrapPool;
    private readonly List<PresentationLayer.VFX.VFX_BrandStarWrap> activeBrandStarWraps = new List<PresentationLayer.VFX.VFX_BrandStarWrap>(32);
```

주의: 필드 이름을 바꾸면 `GameInstaller.prefab`의 직렬화 키(`brandStarWrapPrefab` 등)와 어긋나 참조가 조용히 null이 된다.

### 2-2. `Initialize()`에서 풀 생성

`shootingStarVfxPool == null && shootingStarVfxPrefab != null` 블록 **앞**에 추가:

```csharp
        if (brandStarWrapPool == null && brandStarWrapPrefab != null)
        {
            brandStarWrapPool = new ObjectPool<PresentationLayer.VFX.VFX_BrandStarWrap>(
                createFunc: CreateBrandStarWrap,
                actionOnGet: OnGetBrandStarWrap,
                actionOnRelease: OnReleaseBrandStarWrap,
                actionOnDestroy: OnDestroyBrandStarWrap,
                collectionCheck: true,
                defaultCapacity: brandStarWrapPoolDefaultCapacity,
                maxSize: brandStarWrapPoolMaxSize
            );
        }
```

### 2-3. 풀 콜백 + 공개 API 추가

`PlayMultiHitVFX`류 메서드들이 시작되기 전, 기존 풀 콜백(예: `OnDestroy...`) 바로 뒤에 아래 블록 전체를 추가한다.

```csharp
    private PresentationLayer.VFX.VFX_BrandStarWrap CreateBrandStarWrap()
    {
        PresentationLayer.VFX.VFX_BrandStarWrap instance = Instantiate(brandStarWrapPrefab, transform);
        instance.ReturnToPoolEvent += OnBrandStarWrapReturned;
        instance.gameObject.SetActive(false);
        return instance;
    }

    private void OnGetBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
    {
        _instance.gameObject.SetActive(true);
    }

    private void OnReleaseBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
    {
        _instance.gameObject.SetActive(false);
    }

    private void OnDestroyBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
    {
        if (_instance != null)
        {
            _instance.ReturnToPoolEvent -= OnBrandStarWrapReturned;
            Destroy(_instance.gameObject);
        }
    }

    // 연출이 끝나 스스로 반환을 요청하면 활성 목록에서 빼고 풀에 되돌린다.
    private void OnBrandStarWrapReturned(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
    {
        activeBrandStarWraps.Remove(_instance);
        brandStarWrapPool?.Release(_instance);
    }

    /// <summary>
    /// 낙인이 찍힌 나무를 별들이 감싸고 도는 유지 이펙트를 시작합니다. 동시 개수 상한을 넘거나 풀/프리팹이 없으면 null을 반환합니다.
    /// 반환된 인스턴스는 EndBrandStarWrap으로 종료해야 합니다(낙인이 풀리거나 나무가 사라질 때).
    /// </summary>
    public PresentationLayer.VFX.VFX_BrandStarWrap BeginBrandStarWrap(TreeVisualComponent _visual)
    {
        if (brandStarWrapPool == null || _visual == null) return null;
        if (activeBrandStarWraps.Count >= brandStarWrapMaxActive) return null;

        PresentationLayer.VFX.VFX_BrandStarWrap wrap = brandStarWrapPool.Get();
        activeBrandStarWraps.Add(wrap);
        wrap.Begin(_visual, TreeSortingLayerName);
        return wrap;
    }

    /// <summary>
    /// 별 감싸기를 퇴장 연출과 함께 종료합니다. 이미 반환되었거나 다른 나무에 재사용된 인스턴스(_visual 불일치)는 무시합니다.
    /// </summary>
    public void EndBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _wrap, TreeVisualComponent _visual)
    {
        if (_wrap == null) return;
        if (_wrap.Visual != _visual) return;

        _wrap.End();
    }

    /// <summary>
    /// 던전 이탈 등으로 재생 중인 별 감싸기를 연출 없이 전부 풀에 되돌립니다.
    /// </summary>
    public void ReleaseAllBrandStarWraps()
    {
        for (int i = activeBrandStarWraps.Count - 1; i >= 0; i--)
        {
            if (i >= activeBrandStarWraps.Count) continue;

            PresentationLayer.VFX.VFX_BrandStarWrap wrap = activeBrandStarWraps[i];
            if (wrap != null) wrap.ForceRelease();
        }
        activeBrandStarWraps.Clear();
    }
```

주의: `ReleaseAllBrandStarWraps`의 역방향 루프와 `if (i >= activeBrandStarWraps.Count) continue;`를 `foreach`로 "정리"하면 안 된다.
`ForceRelease()` -> `ReturnToPoolEvent` -> `OnBrandStarWrapReturned`가 순회 중인 리스트에서 원소를 제거하기 때문에 `foreach`는 `InvalidOperationException`이 난다.

### 2-4. 기존 `PlayManifestationBrandVFX`를 각인 재생 API로 교체

기존 메서드(스파크 재생, 태그 `ManifestationBrandSparkEffect`)를 **삭제**하고 아래로 교체한다. 메서드 이름이 바뀌므로 다른 호출처가 없는지 검색(`PlayManifestationBrandVFX`)한다. 호출처는 `InDungeonObjectManager.PlayManifestationBrandVfxRoutine` 한 곳뿐이다(3장에서 같이 수정).

```csharp
    /// <summary>
    /// 발현 낙인이 나무에 처음 찍히는 순간 1회 재생되는 각인 VFX(VFX_BrandStamp)입니다. 별 섬광, 점선 충격파, 별가루 분출,
    /// 미니 별자리를 한꺼번에 보여준다. 이후 낙인이 유지되는 동안은 BeginBrandStarWrap의 별 감싸기가 이어받는다.
    /// parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
    /// </summary>
    public void PlayManifestationBrandStampVFX(TreeVisualComponent _visual)
    {
        if (_visual == null) return;

        PlayManifestationBrandStampVFX(_visual.GetTopRootPosition(), _visual.GetTopRootRotation(), _visual.GetTopHighlightSortingOrder());
    }

    /// <summary>
    /// 각인 VFX를 지정한 좌표에 재생합니다. 별자리 빔에 맞아 죽는 나무는 즉시 풀로 반환되어 비주얼 좌표를 더는 믿을 수 없으므로,
    /// 호출부가 타격 전에 저장해 둔 나무 꼭대기 좌표/회전/캐노피 하이라이트 소팅 오더를 그대로 받아 재생합니다.
    /// </summary>
    public void PlayManifestationBrandStampVFX(Vector3 _position, Quaternion _rotation, int _topHighlightSortingOrder)
    {
        if (vfxComponent == null) return;

        vfxComponent.Play(new VFXPlaySettings(
            "ManifestationBrandStampEffect",
            _position,
            _rotation,
            _topHighlightSortingOrder + 2,
            null
        ));
    }
```

- VFX 태그 문자열 `"ManifestationBrandStampEffect"`는 `GameInstaller.prefab`의 `vfxTag`와 **글자 하나까지 같아야** 한다(3장).
- 소팅 오더 `+2`: 스파크 때의 `+1`에서 올렸다(각인이 캐노피 하이라이트 위에 확실히 보이도록).

---

## 3. `GameInstaller.prefab` 수정

경로: `Assets/Prefabs/Installer/Installer/GameInstaller.prefab`

Unity Inspector에서 하는 것을 권장한다. YAML 직접 수정은 fileID/GUID가 틀리면 조용히 null이 되므로 마지막 수단이다.

### 3-1. Inspector 방식

1. `GameInstaller` 프리팹을 연다.
2. `InDungeonVFXManager` 컴포넌트의 새 섹션 **Manifestation Brand Star Wrap**에서:
   - `Brand Star Wrap Prefab` = `VFX_BrandStarWrap.prefab`의 루트 오브젝트(컴포넌트 `VFX_BrandStarWrap`)
   - `Brand Star Wrap Pool Default Capacity` = 8
   - `Brand Star Wrap Pool Max Size` = 32
   - `Brand Star Wrap Max Active` = 20
3. `VFXComponent`의 풀 목록에서 `vfxTag`가 `ManifestationBrandSparkEffect`인 항목을 찾아 **수정**:

| 항목 | 기존 | 변경 |
|---|---|---|
| `vfxTag` | `ManifestationBrandSparkEffect` | `ManifestationBrandStampEffect` |
| `effectPrefab` | `VFX_Spark` | `VFX_BrandStamp` (루트 `ParticleSystem`) |
| `initialPoolSize` | 40 | 12 |
| `maxPoolSize` | 70 | 40 |

### 3-2. YAML 방식 (참고, 실제 적용했던 변경)

```yaml
  # InDungeonVFXManager 직렬화 블록, treeTransformVfxYOffset 다음 줄들에 추가
  brandStarWrapPrefab: {fileID: 137350371921299027, guid: 8929e93c80181374cba3211c79d18a0f, type: 3}
  brandStarWrapPoolDefaultCapacity: 8
  brandStarWrapPoolMaxSize: 32
  brandStarWrapMaxActive: 20
```

```yaml
  # VFXComponent 풀 목록 - 기존 ManifestationBrandSparkEffect 항목을 아래로 교체
  - vfxTag: ManifestationBrandStampEffect
    effectPrefab: {fileID: 2504916916603310124, guid: 38080eb86596a2d47ad5ffd15146ba9d, type: 3}
    initialPoolSize: 12
    allowDynamicExpansion: 1
    maxPoolSize: 40
    uiParticleScale: 0
```

`effectPrefab`의 fileID는 반드시 프리팹 **루트 `ParticleSystem` 컴포넌트**의 fileID여야 한다. 풀이 `ParticleSystem` 타입을 요구하기 때문이다
(`VFXComponent.CreateNewInstance`가 `_config.EffectPrefab`을 `ParticleSystem`으로 취급하고 `stopAction = Callback`으로 강제한다).

---

## 4. `InDungeonObjectManager.cs` 수정

경로: `Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonObjectManager.cs`

### 4-1. 필드 정리 및 추가

- **삭제**: `ManifestationBrandVfxIntervalMin`, `ManifestationBrandVfxIntervalMax` (스파크 반복 간격 상수 2개와 그 설명 주석). 다른 곳에서 쓰이지 않는다(검색으로 확인).
- **추가**: `manifestationBrandVfxTrees` 선언 바로 아래에

```csharp
    // 별자리 빔이 지금 타격 중인 나무와, 그 나무가 이번 타격으로 죽었는지. 죽은 나무는 TakeDamage 안에서 곧바로 풀로
    // 반환되며 bDead가 false로 되돌아가므로, 죽음은 OnTreeDead에서 이 플래그로만 알 수 있다(ApplyConstellationBeamHit 참고).
    private TreeObj constellationHitTree;
    private bool bConstellationHitTreeDied;
```

### 4-2. `ClearTrees()` - 던전 이탈 시 별 감쌈 회수

`inDungeonVFXManager.ClearAllConstellationDottedLines();` 바로 다음 줄에 추가:

```csharp
            inDungeonVFXManager.ReleaseAllBrandStarWraps();
```

(같은 `if (inDungeonVFXManager != null)` 블록 안이다.)

### 4-3. `OnTreeDead(TreeObj _treeObj)` - 죽음 플래그

`if (_treeObj.PoolIndex == -1) return;` **바로 다음**, `character?.OnTreeFelled();` 이전에 추가:

```csharp
        // 별자리 빔이 타격 중인 나무가 이번 타격으로 죽었음을 알린다(ApplyConstellationBeamHit이 타격 직후 읽는다).
        if (_treeObj == constellationHitTree) bConstellationHitTreeDied = true;
```

### 4-4. `ApplyConstellationBeamHit(ConstellationBeamHit _hit, float _damage)` 교체

기존 `try { tree.TakeDamage(...); if (manifestationBrandBonusMultiplier > 0f && !tree.bDead) {...} } catch {...}` 구간을 아래로 교체한다.
(메서드 앞부분의 `tree == null || !tree.bCanApplyDamage` 가드와 `rootPos` 위치 검사는 **그대로 둔다**.)

```csharp
        // 낙인 특성이 있을 때, 나무가 이번 타격으로 죽는 경우를 대비해 각인을 재생할 위치를 타격 전에 저장해 둔다.
        // 죽은 나무는 즉시 풀로 반환되어 비활성화되므로 타격 뒤에는 비주얼의 좌표를 믿을 수 없다.
        bool bBrandActive = manifestationBrandBonusMultiplier > 0f;
        TreeVisualComponent visual = tree.treeVisualComponent;
        Vector3 stampPosition = Vector3.zero;
        Quaternion stampRotation = Quaternion.identity;
        int stampSortingOrder = 0;
        if (bBrandActive && visual != null)
        {
            stampPosition = visual.GetTopRootPosition();
            stampRotation = visual.GetTopRootRotation();
            stampSortingOrder = visual.GetTopHighlightSortingOrder();
        }

        // 한 그루의 타격이 연쇄(사망 처리, 다른 그룹 발현 등) 중 예외로 실패해도 스윕의 나머지 나무는 계속 맞힌다.
        try
        {
            // TakeDamage로 나무가 죽으면 즉시 풀로 반환되며 ResetTree()가 bDead를 false로 되돌리고 브랜드 배율도 1로
            // 되돌린다. 그래서 tree.bDead로는 죽음을 알 수 없고, OnTreeDead가 세우는 플래그로만 판단한다.
            constellationHitTree = tree;
            bConstellationHitTreeDied = false;

            tree.TakeDamage(_damage);

            bool bDied = bConstellationHitTreeDied;

            if (bBrandActive)
            {
                if (bDied)
                {
                    // 빔에 맞아 죽은 나무: 낙인은 남지 않지만, 별자리에 맞았다는 각인은 저장해 둔 자리에서 재생한다.
                    if (visual != null && inDungeonVFXManager != null)
                    {
                        inDungeonVFXManager.PlayManifestationBrandStampVFX(stampPosition, stampRotation, stampSortingOrder);
                    }
                }
                else
                {
                    // 죽지 않고 살아남은 나무에만 낙인을 적용한다.
                    tree.health.ApplyDamageBrand(1f + manifestationBrandBonusMultiplier);
                    StartManifestationBrandVfx(tree);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[InDungeonObjectManager] 별자리 광선 타격 처리 중 예외가 발생했습니다. 나머지 타격은 계속 진행합니다.");
            Debug.LogException(e);
        }
        finally
        {
            constellationHitTree = null;
            bConstellationHitTreeDied = false;
        }
```

### 4-5. `PlayManifestationBrandVfxRoutine(TreeObj _tree)` 교체

스파크를 반복 재생하던 루틴을 "각인 1회 + 별 감쌈 유지"로 교체한다. 메서드 위 설명 주석도 같이 갱신한다(주석은 diff 참고).

```csharp
    private IEnumerator PlayManifestationBrandVfxRoutine(TreeObj _tree)
    {
        // 낙인이 유지되는 동안 나무를 감싸고 도는 별 이펙트(풀 인스턴스)와, 그 인스턴스를 시작할 때의 나무 비주얼.
        PresentationLayer.VFX.VFX_BrandStarWrap wrap = null;
        TreeVisualComponent wrapVisual = null;

        try
        {
            // 처음 한 번만 각인 VFX + 별 감싸기를 시작한다(비주얼이 아직 없으면 생길 때까지 다음 프레임에 다시 시도).
            bool bStampPending = true;

            // 매 프레임 낙인 상태를 확인한다 - 낙인이 풀리거나 나무가 죽는 즉시 별 감싸기를 걷어야, 풀로 돌아간 나무의
            // 비주얼이 다른 나무로 재사용될 때 별이 그쪽으로 따라가지 않는다.
            while (_tree != null && _tree.health != null && _tree.health.IsBranded)
            {
                TreeVisualComponent visual = _tree.treeVisualComponent;

                if (bStampPending && visual != null)
                {
                    inDungeonVFXManager.PlayManifestationBrandStampVFX(visual);
                    wrapVisual = visual;
                    wrap = inDungeonVFXManager.BeginBrandStarWrap(visual);
                    bStampPending = false;
                }

                yield return null;
            }
        }
        finally
        {
            inDungeonVFXManager?.EndBrandStarWrap(wrap, wrapVisual);
            manifestationBrandVfxTrees.Remove(_tree);
        }
    }
```

---

## 5. 죽음 판별 버그 (`!tree.bDead`) - 이펙트와 무관하게 중요

원래 코드:

```csharp
tree.TakeDamage(_damage);
if (manifestationBrandBonusMultiplier > 0f && !tree.bDead) { ApplyDamageBrand(...); StartManifestationBrandVfx(tree); }
```

`TakeDamage`가 나무를 죽이면 같은 호출 안에서 다음이 연쇄된다.

`TreeObj.TakeDamage` -> `TreeIsDead`(`bDead = true`) -> `EnemyIsDeadEvent` -> `InDungeonObjectManager.OnTreeDead` -> `treePool.Release` -> `OnReleaseTree` -> `ResetTree()`(**`bDead = false`**, 체력/브랜드 배율 리셋) -> 게임오브젝트 비활성.

즉 `TakeDamage`가 돌아온 시점의 `bDead`는 죽은 나무에서도 항상 `false`다. 결과:

- 죽은 나무에 `ApplyDamageBrand`가 걸리고 `StartManifestationBrandVfx`가 시작된다.
- 이미 풀로 돌아간 나무에 대해 코루틴이 돌면서 별 감쌈 슬롯(`brandStarWrapMaxActive`)을 점유하거나, 풀에서 재사용된 다른 나무에 낙인이 붙는 것처럼 보일 수 있다.

수정 방식: `OnTreeDead`에서 세우는 플래그(`constellationHitTree` + `bConstellationHitTreeDied`)로만 죽음을 판별한다(4-1, 4-3, 4-4).

---

## 6. 잠재적 문제와 주의점

### 6-1. 죽음 플래그의 전제
- 플래그는 `OnTreeDead`의 `if (_treeObj.PoolIndex == -1) return;` **뒤**에 세운다. 이미 풀에 있는 나무(`PoolIndex == -1`)가 같은 타격에서 다시 `OnTreeDead`로 들어오면 플래그가 서지 않아 "생존"으로 오판할 수 있다. `bCanApplyDamage` 가드가 있어 정상 흐름에서는 발생하지 않지만, 방어를 더 원하면 `TakeDamage` 직후 `tree.IsPooled`도 함께 확인한다(`TreeObj.IsPooled` 존재).
- `ApplyConstellationBeamHit`이 **동기적으로 중첩 호출되지 않는다**는 전제다. 현재 광선 스윕은 코루틴(`ConstellationBeamRoutine`)에서 프레임에 걸쳐 진행되므로 중첩이 없다. 중첩 호출이 생기면 안쪽 `finally`가 필드를 null로 만들어 바깥 호출이 플래그를 잃는다. 그런 구조가 되면 `finally`에서 "이전 값 복원" 방식(로컬 변수에 보관 후 복원)으로 바꿔야 한다.

### 6-2. 죽은 나무의 좌표
죽은 나무는 `TakeDamage` 안에서 즉시 풀로 반환되어 비활성화되고 다른 나무로 재사용될 수 있다. 그래서 각인 재생용 좌표(`GetTopRootPosition/Rotation/HighlightSortingOrder`)는 **`TakeDamage` 전에** 저장해야 한다(4-4). 타격 뒤에 `visual`에서 다시 읽으면 엉뚱한 위치에 재생된다.

### 6-3. VFX 태그/직렬화 불일치는 조용히 실패한다
- `vfxTag`가 코드의 `"ManifestationBrandStampEffect"`와 다르면 각인이 재생되지 않는다(에러 없이 무시되거나 로그만 남음).
- `brandStarWrapPrefab`가 null이면 `brandStarWrapPool`이 만들어지지 않고 `BeginBrandStarWrap`이 null을 반환한다 - 별 감쌈만 조용히 사라지고 나머지는 정상이라 놓치기 쉽다. 적용 후 반드시 화면에서 확인한다.
- `effectPrefab`은 루트 `ParticleSystem` fileID여야 한다(3장).

### 6-4. 풀 용량
- 각인 풀 `initialPoolSize 12 / maxPoolSize 40`: 광선 한 번에 여러 나무가 동시에 맞으면 각인이 한꺼번에 재생된다. 동시 재생이 풀 최대를 넘으면 `allowDynamicExpansion`이 켜져 있어도 `maxPoolSize`에서 막힌다. 나무 밀도가 높은 스테이지(예: StarrootForest)에서 각인이 빠지는 것 같으면 값을 올린다.
- 별 감쌈 동시 상한 `brandStarWrapMaxActive = 20`: 상한을 넘은 나무는 낙인은 걸리지만 별이 감싸지 않는다(의도된 동작). 성능 여유가 있으면 올린다.
- 각인 인스턴스는 자식에 `MeshRenderer`(절차적 메쉬)를 갖는다. 인스턴스당 메쉬 1개(정점 수천 개 이하)라 메모리는 작지만, 풀을 무작정 크게 잡지 않는다.

### 6-5. 낙인 코루틴의 폴링 비용
`PlayManifestationBrandVfxRoutine`은 나무마다 매 프레임(`yield return null`) `IsBranded`를 확인한다. 낙인은 "나무가 죽어 리셋될 때까지" 유지되므로 낙인 나무가 수백 그루면 코루틴 수백 개가 돈다. 실제 부하는 작지만(비교 몇 번), 이벤트 방식(낙인 해제 이벤트)이 있다면 그쪽이 더 좋다. 현재 `EHealthComponent`에는 낙인 해제 이벤트가 없어 폴링을 썼다. 종료 조건은 반드시 `health.IsBranded`로 판단해야 한다(`bDead`, `activeInHierarchy`는 신뢰 불가 - 코드 주석 참고).

### 6-6. 코루틴이 중단될 때의 정리
- 던전 이탈: `ClearTrees()`에서 `ReleaseAllBrandStarWraps()`로 별 감쌈을 강제 회수한다(4-2). 이 호출이 빠지면 던전을 나갔다 들어올 때 이전 판의 별이 남는다.
- `manifestationBrandVfxTrees`(중복 코루틴 방지 `HashSet`)는 기존 코드부터 `ClearTrees()`에서 비워지지 않는다. 코루틴이 `finally`까지 못 가고 중단되는 경로(매니저 비활성화 등)가 있으면 항목이 남아 해당 나무에 낙인 VFX가 다시 시작되지 않을 수 있다. 이번 변경으로 생긴 문제는 아니지만, 재현되면 `ClearTrees()`에서 `Clear()`를 추가하는 것을 검토한다.

### 6-7. `VFX_Spark` 프리팹 정리
이 변경 이후 `ManifestationBrandSparkEffect` 항목은 사라지므로 `VFX_Spark.prefab`(`Assets/Prefabs/VFX/Lightning/`)은 낙인 용도로 쓰이지 않는다.
- 삭제하려면 먼저 프로젝트 전체에서 GUID `ae8dfa960fb2f644f89d0e6dd44ea1da`를 검색해 다른 참조가 없는지 확인한다(작성 시점에는 `GameInstaller.prefab`만 참조했다).
- **순서 주의**: `GameInstaller.prefab`의 항목을 먼저 교체하고(3장), 그 다음에 프리팹을 삭제한다. 프리팹만 지우고 설치기 항목을 남기면 해당 풀 엔트리의 `effectPrefab`이 null이 된다(`VFXComponent.CreateNewInstance`는 null을 무시하고 반환하므로 크래시는 아니지만, 낙인 VFX가 아무것도 재생되지 않는다).

### 6-7-1. 반쪽 적용 금지
스크립트는 원래 상태(스파크)인데 `GameInstaller.prefab`만 바뀌었거나, 그 반대이면 낙인 이펙트가 조용히 사라진다. 2, 3, 4장은 **한 번에 함께** 적용한다.

### 6-8. 정렬(소팅) 관련
- `VFXComponent.ApplySortingSettings`는 프리팹 자식의 **모든** 렌더러에 같은 `sortingOrder`를 덮어쓴다. `VFX_BrandStampBurst`는 이 때문에 매 프레임 루트 파티클 렌더러의 소팅 오더 + 1로 자신을 다시 맞춘다. 프리팹 구조를 바꾸거나 루트를 다른 것으로 교체하면 이 보정이 깨질 수 있다.
- `VFX_BrandStarWrap`은 `TreeSortingLayerName`을 `Begin`에서 받는다. 이 값이 실제 나무가 쓰는 소팅 레이어와 다르면 별이 나무 뒤/앞 엉뚱한 곳에 그려진다.

### 6-9. 프레임/시간 관련
- 두 이펙트 모두 `Time.deltaTime`으로 진행한다. `Time.timeScale = 0`(일시정지)이면 멈춘다(의도). 이펙트만 계속 재생해야 하는 UI 연출에는 쓰지 말 것.
- `VFX_BrandStamp` 루트 파티클 수명을 0.95초로 맞춰 두었다. 루트 수명을 줄이면 풀에 너무 일찍 반환되어 폭발이 중간에 끊긴다(`VFX_BrandStampBurst.totalDuration`은 루트 수명 이하여야 한다).

### 6-10. 머지 충돌 예상 지점
- `InDungeonObjectManager.cs`: `ApplyConstellationBeamHit`, `PlayManifestationBrandVfxRoutine`, `OnTreeDead` 세 곳이 다른 브랜치와 자주 겹친다. 충돌 시 4장의 앵커(주석/가드 문구)를 기준으로 재적용한다.
- `GameInstaller.prefab`: YAML 충돌은 수동 병합이 위험하다. 충돌하면 상대 버전을 채택한 뒤 3장 Inspector 방식으로 다시 적용하는 것이 안전하다.

---

## 7. 적용 후 검증 체크리스트

컴파일
- [ ] Unity 콘솔에 컴파일 에러/신규 경고(`CS0414`, `CS0168` 등)가 없다.
- [ ] `PlayManifestationBrandVFX`(구 이름) 검색 결과가 0건이다.
- [ ] `ManifestationBrandVfxIntervalMin/Max` 검색 결과가 0건이다.

에디터 연결
- [ ] `GameInstaller`의 `Brand Star Wrap Prefab`이 비어 있지 않다.
- [ ] `vfxTag = ManifestationBrandStampEffect` 항목이 있고 `effectPrefab`이 `VFX_BrandStamp`이다.

플레이
- [ ] 낙인 특성을 얻은 뒤 별자리 광선에 맞은 **생존** 나무에 각인이 1회 재생되고, 이후 별이 나무를 감싸고 돈다.
- [ ] 광선에 맞아 **죽은** 나무에도 각인이 재생된다(죽은 자리에서).
- [ ] 낙인 나무를 벌목하면 별 감쌈이 퇴장 연출과 함께 사라지고, 풀에서 재사용된 다른 나무에 별이 붙지 않는다.
- [ ] 던전을 나갔다 다시 들어와도 이전 판의 별이 남아 있지 않다.
- [ ] 낙인 특성이 **없을 때**는 각인/별 감쌈이 전혀 나오지 않는다.
- [ ] 화면에 낙인 나무가 많을 때(20그루 이상) 프레임이 급락하지 않는다.
- [ ] 콘솔에 "별자리 광선 타격 처리 중 예외" 로그가 없다.

테스트 팁: 별 표식 나무를 한 방에 죽게 만들어 죽는 경로를 확인하면 편하다(에디터에서 체력을 직접 낮춘다).

---

## 8. 롤백

세 파일을 이 변경 이전 커밋 상태로 되돌리면 된다.

```bash
git checkout <이전 커밋> -- "Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonObjectManager.cs" "Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs" "Assets/Prefabs/Installer/Installer/GameInstaller.prefab"
```

`VFX_Spark.prefab`을 삭제했다면 함께 복구해야 스파크 항목이 살아난다.

---

## 9. 에이전트에게 그대로 붙여 넣을 지시문

````text
다음 문서 `Docs/ManifestationBrandVFX_ApplicationLayer_Guide.md`의 2, 3, 4장을 그대로 적용해 주세요.

규칙
- 프로젝트 CLAUDE.md의 규칙(한국어 응답, 승인 게이트, 네이밍/컨벤션, LINQ/람다 금지, Yoda 표기, 명시적 접근 제어자)을 지켜 주세요.
- 문서에 없는 코드는 수정하지 마세요. 기존 변수명은 바꾸지 마세요.
- 문서의 앵커(주석/가드 문구)로 변경 위치를 찾고, 위치가 모호하면 수정하지 말고 저에게 물어봐 주세요.
- 2, 3, 4장은 반드시 한 번에 함께 적용해 주세요(반쪽 적용 금지).

절차
1. 문서 1장의 에셋(프리팹/스크립트/머티리얼)이 존재하고 .meta의 GUID가 표와 같은지 확인합니다. 다르면 3장의 GUID를 실제 값으로 바꿔 적용하고 그 사실을 보고합니다.
2. 2장(InDungeonVFXManager), 4장(InDungeonObjectManager)의 코드를 적용합니다. 필요한 using이 있는지 확인합니다.
3. 3장의 GameInstaller.prefab 변경은 Unity에서 Inspector로 적용합니다(에디터 연결이 없으면 YAML로 적용하되 fileID/GUID를 재확인합니다).
4. 컴파일하고 콘솔에 에러/경고가 없는지 확인합니다.
5. 7장 체크리스트를 순서대로 수행하고 결과를 항목별로 보고합니다. 수행하지 못한 항목은 못 했다고 명시합니다.
6. 6장의 잠재 문제 중 현재 코드베이스에 해당하는 것이 있는지(예: ApplyConstellationBeamHit이 중첩 호출되는지, 다른 곳에서 PlayManifestationBrandVFX를 호출하는지) 실제 코드를 열어 확인하고 보고합니다.
````

---

## 부록 A. 실제 적용했던 diff 원문 (정본)

아래는 원래 상태(HEAD) 대비 실제 변경이다. 이 문서의 본문과 다르면 이 diff가 정본이다.

### Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs

```diff
diff --git a/Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs b/Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs
index 394fba53..3b24da16 100644
--- a/Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs	
+++ b/Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonVFXManager.cs	
@@ -130,6 +130,16 @@ public class InDungeonVFXManager : MonoBehaviour
 
     private IObjectPool<TreeTransformVFX> treeTransformVfxPool;
 
+    [Header("Manifestation Brand Star Wrap (낙인 나무를 감싸는 별)")]
+    [SerializeField] private PresentationLayer.VFX.VFX_BrandStarWrap brandStarWrapPrefab;
+    [SerializeField] private int brandStarWrapPoolDefaultCapacity = 8;
+    [SerializeField] private int brandStarWrapPoolMaxSize = 32;
+    // 화면 가득 낙인 나무일 때도 부하가 커지지 않도록 동시에 별이 감싸는 나무 수의 상한
+    [SerializeField] private int brandStarWrapMaxActive = 20;
+
+    private IObjectPool<PresentationLayer.VFX.VFX_BrandStarWrap> brandStarWrapPool;
+    private readonly List<PresentationLayer.VFX.VFX_BrandStarWrap> activeBrandStarWraps = new List<PresentationLayer.VFX.VFX_BrandStarWrap>(32);
+
     public void Initialize()
     {
         if (vfxComponent != null)
@@ -176,6 +186,19 @@ public class InDungeonVFXManager : MonoBehaviour
             );
         }
 
+        if (brandStarWrapPool == null && brandStarWrapPrefab != null)
+        {
+            brandStarWrapPool = new ObjectPool<PresentationLayer.VFX.VFX_BrandStarWrap>(
+                createFunc: CreateBrandStarWrap,
+                actionOnGet: OnGetBrandStarWrap,
+                actionOnRelease: OnReleaseBrandStarWrap,
+                actionOnDestroy: OnDestroyBrandStarWrap,
+                collectionCheck: true,
+                defaultCapacity: brandStarWrapPoolDefaultCapacity,
+                maxSize: brandStarWrapPoolMaxSize
+            );
+        }
+
         if (shootingStarVfxPool == null && shootingStarVfxPrefab != null)
         {
             shootingStarVfxPool = new ObjectPool<ShootingStarVFX>(
@@ -786,6 +809,81 @@ public class InDungeonVFXManager : MonoBehaviour
         if (_instance != null) Destroy(_instance.gameObject);
     }
 
+    private PresentationLayer.VFX.VFX_BrandStarWrap CreateBrandStarWrap()
+    {
+        PresentationLayer.VFX.VFX_BrandStarWrap instance = Instantiate(brandStarWrapPrefab, transform);
+        instance.ReturnToPoolEvent += OnBrandStarWrapReturned;
+        instance.gameObject.SetActive(false);
+        return instance;
+    }
+
+    private void OnGetBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
+    {
+        _instance.gameObject.SetActive(true);
+    }
+
+    private void OnReleaseBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
+    {
+        _instance.gameObject.SetActive(false);
+    }
+
+    private void OnDestroyBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
+    {
+        if (_instance != null)
+        {
+            _instance.ReturnToPoolEvent -= OnBrandStarWrapReturned;
+            Destroy(_instance.gameObject);
+        }
+    }
+
+    // 연출이 끝나 스스로 반환을 요청하면 활성 목록에서 빼고 풀에 되돌린다.
+    private void OnBrandStarWrapReturned(PresentationLayer.VFX.VFX_BrandStarWrap _instance)
+    {
+        activeBrandStarWraps.Remove(_instance);
+        brandStarWrapPool?.Release(_instance);
+    }
+
+    /// <summary>
+    /// 낙인이 찍힌 나무를 별들이 감싸고 도는 유지 이펙트를 시작합니다. 동시 개수 상한을 넘거나 풀/프리팹이 없으면 null을 반환합니다.
+    /// 반환된 인스턴스는 EndBrandStarWrap으로 종료해야 합니다(낙인이 풀리거나 나무가 사라질 때).
+    /// </summary>
+    public PresentationLayer.VFX.VFX_BrandStarWrap BeginBrandStarWrap(TreeVisualComponent _visual)
+    {
+        if (brandStarWrapPool == null || _visual == null) return null;
+        if (activeBrandStarWraps.Count >= brandStarWrapMaxActive) return null;
+
+        PresentationLayer.VFX.VFX_BrandStarWrap wrap = brandStarWrapPool.Get();
+        activeBrandStarWraps.Add(wrap);
+        wrap.Begin(_visual, TreeSortingLayerName);
+        return wrap;
+    }
+
+    /// <summary>
+    /// 별 감싸기를 퇴장 연출과 함께 종료합니다. 이미 반환되었거나 다른 나무에 재사용된 인스턴스(_visual 불일치)는 무시합니다.
+    /// </summary>
+    public void EndBrandStarWrap(PresentationLayer.VFX.VFX_BrandStarWrap _wrap, TreeVisualComponent _visual)
+    {
+        if (_wrap == null) return;
+        if (_wrap.Visual != _visual) return;
+
+        _wrap.End();
+    }
+
+    /// <summary>
+    /// 던전 이탈 등으로 재생 중인 별 감싸기를 연출 없이 전부 풀에 되돌립니다.
+    /// </summary>
+    public void ReleaseAllBrandStarWraps()
+    {
+        for (int i = activeBrandStarWraps.Count - 1; i >= 0; i--)
+        {
+            if (i >= activeBrandStarWraps.Count) continue;
+
+            PresentationLayer.VFX.VFX_BrandStarWrap wrap = activeBrandStarWraps[i];
+            if (wrap != null) wrap.ForceRelease();
+        }
+        activeBrandStarWraps.Clear();
+    }
+
     /// <summary>
     /// 나무 피격 VFX를 재생합니다. parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
     /// Top/Bottom 이펙트는 각각 설정된 컬러를 공유합니다.
@@ -925,20 +1023,30 @@ public class InDungeonVFXManager : MonoBehaviour
     }
 
     /// <summary>
-    /// 발현 낙인이 찍힌 나무 위에서 일정 인터벌마다 재생되는 스파크 VFX(VFX_Spark)입니다.
+    /// 발현 낙인이 나무에 처음 찍히는 순간 1회 재생되는 각인 VFX(VFX_BrandStamp)입니다. 별 섬광, 금빛 링/방사,
+    /// 별자리 그려짐, 별가루 소나기를 한꺼번에 보여준다. 이후 낙인이 유지되는 동안은 BeginBrandStarWrap의 별 감싸기가 이어받는다.
     /// parent는 null로 고정하여 나무 오브젝트와 완전히 분리합니다.
     /// </summary>
-    public void PlayManifestationBrandVFX(TreeVisualComponent _visual)
+    public void PlayManifestationBrandStampVFX(TreeVisualComponent _visual)
     {
-        if (vfxComponent == null || _visual == null) return;
+        if (_visual == null) return;
 
-        int sortingOrder = _visual.GetTopHighlightSortingOrder() + 1;
+        PlayManifestationBrandStampVFX(_visual.GetTopRootPosition(), _visual.GetTopRootRotation(), _visual.GetTopHighlightSortingOrder());
+    }
+
+    /// <summary>
+    /// 각인 VFX를 지정한 좌표에 재생합니다. 별자리 빔에 맞아 죽는 나무는 즉시 풀로 반환되어 비주얼 좌표를 더는 믿을 수 없으므로,
+    /// 호출부가 타격 전에 저장해 둔 나무 꼭대기 좌표/회전/캐노피 하이라이트 소팅 오더를 그대로 받아 재생합니다.
+    /// </summary>
+    public void PlayManifestationBrandStampVFX(Vector3 _position, Quaternion _rotation, int _topHighlightSortingOrder)
+    {
+        if (vfxComponent == null) return;
 
         vfxComponent.Play(new VFXPlaySettings(
-            "ManifestationBrandSparkEffect",
-            _visual.GetTopRootPosition(),
-            _visual.GetTopRootRotation(),
-            sortingOrder,
+            "ManifestationBrandStampEffect",
+            _position,
+            _rotation,
+            _topHighlightSortingOrder + 2,
             null
         ));
     }
```

### Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonObjectManager.cs

```diff
diff --git a/Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonObjectManager.cs b/Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonObjectManager.cs
index 9e25a980..01353340 100644
--- a/Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonObjectManager.cs	
+++ b/Assets/Scripts/Application Layer/ObjectSystem/Dungeon/InDungeonObjectManager.cs	
@@ -385,14 +385,14 @@ public class InDungeonObjectManager : MonoBehaviour, IInDungeonObjProvider, IInD
     // ClearTrees마다 증가 - 대기 중이던 ConstellationBeamRoutine이 이전 런의 발현을 이어서 쏘지 않게 한다.
     private int constellationManifestVersion = 0;
 
-    // 발현 낙인이 찍힌 나무 위에서 반복 재생되는 스파크 VFX(VFX_Spark) - 낙인은 나무가 죽어 리셋될 때까지
-    // 유지되므로(EHealthComponent.brandedDamageMultiplier), 그동안 인터벌마다 계속 재생한다. 매번 똑같은
-    // 박자로 반짝이면 기계적으로 보이므로 인터벌 자체를 매번 랜덤화한다.
-    private const float ManifestationBrandVfxIntervalMin = 0.5f;
-    private const float ManifestationBrandVfxIntervalMax = 0.75f;
     // 나무 한 그루에 낙인 코루틴이 중복으로 여러 개 돌지 않도록(같은 나무가 여러 발현/여러 선분에 맞을 수 있음) 추적
     private readonly HashSet<TreeObj> manifestationBrandVfxTrees = new HashSet<TreeObj>();
 
+    // 별자리 빔이 지금 타격 중인 나무와, 그 나무가 이번 타격으로 죽었는지. 죽은 나무는 TakeDamage 안에서 곧바로 풀로
+    // 반환되며 bDead가 false로 되돌아가므로, 죽음은 OnTreeDead에서 이 플래그로만 알 수 있다(ApplyConstellationBeamHit 참고).
+    private TreeObj constellationHitTree;
+    private bool bConstellationHitTreeDied;
+
     public float StarMarkDamageMultiplier => starMarkDamageMultiplier;
 
     // 별의 주시(Star Gaze) - 모든 숲에서 발동, 화면 범위 내 가장 가까운 나무에 주기적으로 별똥별 낙하
@@ -1117,6 +1117,7 @@ public class InDungeonObjectManager : MonoBehaviour, IInDungeonObjProvider, IInD
         {
             inDungeonVFXManager.ClearAllConstellationGroundMarks();
             inDungeonVFXManager.ClearAllConstellationDottedLines();
+            inDungeonVFXManager.ReleaseAllBrandStarWraps();
         }
 
         // 비행/발광 중이던 별자리 레이저도 강제로 풀에 반환한다. 반환 이벤트(OnConstellationLaserReturned)가
@@ -1287,6 +1288,9 @@ public class InDungeonObjectManager : MonoBehaviour, IInDungeonObjProvider, IInD
         // 여기서 걸러서 아이템 중복 지급, 밀도 카운트 오염, 풀 이중 반환 예외를 막는다.
         if (_treeObj.PoolIndex == -1) return;
 
+        // 별자리 빔이 타격 중인 나무가 이번 타격으로 죽었음을 알린다(ApplyConstellationBeamHit이 타격 직후 읽는다).
+        if (_treeObj == constellationHitTree) bConstellationHitTreeDied = true;
+
         // "열기 회수" 특성 - 과열 상태에서 나무를 벌목하면 과열 지속시간이 회복된다.
         character?.OnTreeFelled();
 
@@ -2705,17 +2709,48 @@ public class InDungeonObjectManager : MonoBehaviour, IInDungeonObjProvider, IInD
         // 계산 이후 다른 원인으로 죽어 풀로 돌아갔다가 다른 자리에 재사용된 나무는 건너뛴다.
         if ((tree.transform.position - _hit.rootPos).sqrMagnitude > 0.0001f) return;
 
+        // 낙인 특성이 있을 때, 나무가 이번 타격으로 죽는 경우를 대비해 각인을 재생할 위치를 타격 전에 저장해 둔다.
+        // 죽은 나무는 즉시 풀로 반환되어 비활성화되므로 타격 뒤에는 비주얼의 좌표를 믿을 수 없다.
+        bool bBrandActive = manifestationBrandBonusMultiplier > 0f;
+        TreeVisualComponent visual = tree.treeVisualComponent;
+        Vector3 stampPosition = Vector3.zero;
+        Quaternion stampRotation = Quaternion.identity;
+        int stampSortingOrder = 0;
+        if (bBrandActive && visual != null)
+        {
+            stampPosition = visual.GetTopRootPosition();
+            stampRotation = visual.GetTopRootRotation();
+            stampSortingOrder = visual.GetTopHighlightSortingOrder();
+        }
+
         // 한 그루의 타격이 연쇄(사망 처리, 다른 그룹 발현 등) 중 예외로 실패해도 스윕의 나머지 나무는 계속 맞힌다.
         try
         {
+            // TakeDamage로 나무가 죽으면 즉시 풀로 반환되며 ResetTree()가 bDead를 false로 되돌리고 브랜드 배율도 1로
+            // 되돌린다. 그래서 tree.bDead로는 죽음을 알 수 없고, OnTreeDead가 세우는 플래그로만 판단한다.
+            constellationHitTree = tree;
+            bConstellationHitTreeDied = false;
+
             tree.TakeDamage(_damage);
 
-            // TakeDamage로 나무가 죽으면 즉시 풀로 반환되며 ResetTree()가 브랜드 배율을 1로 되돌리므로,
-            // 죽지 않고 살아남은 나무에만 낙인을 적용한다.
-            if (manifestationBrandBonusMultiplier > 0f && !tree.bDead)
+            bool bDied = bConstellationHitTreeDied;
+
+            if (bBrandActive)
             {
-                tree.health.ApplyDamageBrand(1f + manifestationBrandBonusMultiplier);
-                StartManifestationBrandVfx(tree);
+                if (bDied)
+                {
+                    // 빔에 맞아 죽은 나무: 낙인은 남지 않지만, 별자리에 맞았다는 각인은 저장해 둔 자리에서 재생한다.
+                    if (visual != null && inDungeonVFXManager != null)
+                    {
+                        inDungeonVFXManager.PlayManifestationBrandStampVFX(stampPosition, stampRotation, stampSortingOrder);
+                    }
+                }
+                else
+                {
+                    // 죽지 않고 살아남은 나무에만 낙인을 적용한다.
+                    tree.health.ApplyDamageBrand(1f + manifestationBrandBonusMultiplier);
+                    StartManifestationBrandVfx(tree);
+                }
             }
         }
         catch (Exception e)
@@ -2723,6 +2758,11 @@ public class InDungeonObjectManager : MonoBehaviour, IInDungeonObjProvider, IInD
             Debug.LogError("[InDungeonObjectManager] 별자리 광선 타격 처리 중 예외가 발생했습니다. 나머지 타격은 계속 진행합니다.");
             Debug.LogException(e);
         }
+        finally
+        {
+            constellationHitTree = null;
+            bConstellationHitTreeDied = false;
+        }
     }
 
     // 낙인이 찍힌 나무에 대해 주기 재생 VFX 루틴을 시작한다(이미 이 나무에 루틴이 돌고 있다면 무시).
@@ -2738,8 +2778,9 @@ public class InDungeonObjectManager : MonoBehaviour, IInDungeonObjProvider, IInD
         }
     }
 
-    // 낙인이 찍힌 나무 위에서 [ManifestationBrandVfxIntervalMin, Max] 사이로 랜덤화된 인터벌마다
-    // VFX_Spark를 재생한다. 종료 조건은 반드시 health.IsBranded(실제 낙인 배율 상태)로만 판단해야 한다 -
+    // 낙인이 찍힌 나무에 각인 VFX(VFX_BrandStamp)를 한 번 재생하고, 낙인이 유지되는 동안 나무를 감싸고 도는 별
+    // (VFX_BrandStarWrap)을 띄워 둔다. 낙인은 나무가 죽어 리셋될 때까지 유지된다(EHealthComponent.brandedDamageMultiplier).
+    // 종료 조건은 반드시 health.IsBranded(실제 낙인 배율 상태)로만 판단해야 한다 -
     // bDead는 나무가 죽는 순간 OnTreeDead -> treePool.Release -> ResetTree()가 같은 프레임 안에서 다시
     // false로 되돌리고, gameObject.activeInHierarchy도 죽음뿐 아니라 카메라 컬링(UpdateTreeVisibility)으로
     // 살아있는 동안에도 꺼졌다 켜졌다 하므로 둘 다 "이 나무가 여전히 낙인 상태인지"를 판단하는 데 쓸 수
@@ -2747,20 +2788,35 @@ public class InDungeonObjectManager : MonoBehaviour, IInDungeonObjProvider, IInD
     // 풀에서 재사용되어 전혀 다른 나무가 되어도(재낙인되지 않는 한) 계속 false를 유지한다.
     private IEnumerator PlayManifestationBrandVfxRoutine(TreeObj _tree)
     {
+        // 낙인이 유지되는 동안 나무를 감싸고 도는 별 이펙트(풀 인스턴스)와, 그 인스턴스를 시작할 때의 나무 비주얼.
+        PresentationLayer.VFX.VFX_BrandStarWrap wrap = null;
+        TreeVisualComponent wrapVisual = null;
+
         try
         {
+            // 처음 한 번만 각인 VFX + 별 감싸기를 시작한다(비주얼이 아직 없으면 생길 때까지 다음 프레임에 다시 시도).
+            bool bStampPending = true;
+
+            // 매 프레임 낙인 상태를 확인한다 - 낙인이 풀리거나 나무가 죽는 즉시 별 감싸기를 걷어야, 풀로 돌아간 나무의
+            // 비주얼이 다른 나무로 재사용될 때 별이 그쪽으로 따라가지 않는다.
             while (_tree != null && _tree.health != null && _tree.health.IsBranded)
             {
-                if (_tree.treeVisualComponent != null)
+                TreeVisualComponent visual = _tree.treeVisualComponent;
+
+                if (bStampPending && visual != null)
                 {
-                    inDungeonVFXManager.PlayManifestationBrandVFX(_tree.treeVisualComponent);
+                    inDungeonVFXManager.PlayManifestationBrandStampVFX(visual);
+                    wrapVisual = visual;
+                    wrap = inDungeonVFXManager.BeginBrandStarWrap(visual);
+                    bStampPending = false;
                 }
 
-                yield return new WaitForSeconds(UnityEngine.Random.Range(ManifestationBrandVfxIntervalMin, ManifestationBrandVfxIntervalMax));
+                yield return null;
             }
         }
         finally
         {
+            inDungeonVFXManager?.EndBrandStarWrap(wrap, wrapVisual);
             manifestationBrandVfxTrees.Remove(_tree);
         }
     }
```

### Assets/Prefabs/Installer/Installer/GameInstaller.prefab

```diff
diff --git a/Assets/Prefabs/Installer/Installer/GameInstaller.prefab b/Assets/Prefabs/Installer/Installer/GameInstaller.prefab
index 42444a72..d2c0e576 100644
--- a/Assets/Prefabs/Installer/Installer/GameInstaller.prefab
+++ b/Assets/Prefabs/Installer/Installer/GameInstaller.prefab
@@ -1573,6 +1573,10 @@ MonoBehaviour:
   treeTransformVfxPoolDefaultCapacity: 4
   treeTransformVfxPoolMaxSize: 32
   treeTransformVfxYOffset: -0.25
+  brandStarWrapPrefab: {fileID: 137350371921299027, guid: 8929e93c80181374cba3211c79d18a0f, type: 3}
+  brandStarWrapPoolDefaultCapacity: 8
+  brandStarWrapPoolMaxSize: 32
+  brandStarWrapMaxActive: 20
 --- !u!114 &777467232540049004
 MonoBehaviour:
   m_ObjectHideFlags: 0
@@ -1654,11 +1658,11 @@ MonoBehaviour:
     allowDynamicExpansion: 1
     maxPoolSize: 5
     uiParticleScale: 0
-  - vfxTag: ManifestationBrandSparkEffect
-    effectPrefab: {fileID: 4414206344302795060, guid: ae8dfa960fb2f644f89d0e6dd44ea1da, type: 3}
-    initialPoolSize: 40
+  - vfxTag: ManifestationBrandStampEffect
+    effectPrefab: {fileID: 2504916916603310124, guid: 38080eb86596a2d47ad5ffd15146ba9d, type: 3}
+    initialPoolSize: 12
     allowDynamicExpansion: 1
-    maxPoolSize: 70
+    maxPoolSize: 40
     uiParticleScale: 0
   - vfxTag: TreeHeatEmitEffect
     effectPrefab: {fileID: 8248180672793769683, guid: cede68a0b11b75141b13e1c24e9e404f, type: 3}
```
