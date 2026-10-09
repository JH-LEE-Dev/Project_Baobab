using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class AxeExtraAttackCreator : MonoBehaviour, IShockWaveCreator
{
    // 외부 의존성
    [SerializeField] private ShockWave shockWavePrefab;
    // 과열 상태("화염 참격" 특성)에서 나가는 충격파 전용 프리팹(OverheatShockWaveVisualComponent 이펙트).
    // 비어 있으면 일반 프리팹으로 대체한다.
    [SerializeField] private ShockWave overheatShockWavePrefab;

    // 내부 의존성
    private IObjectPool<ShockWave> shockWavePool;
    private IObjectPool<ShockWave> overheatShockWavePool;
    // 풀에서 꺼낸 충격파가 어느 풀 소속인지 기억해 두었다가 반환할 때 같은 풀로 돌려보낸다.
    private readonly HashSet<ShockWave> overheatShockWaves = new HashSet<ShockWave>();
    // 지금 꺼내 쓰는 중인 충격파(두 풀 공통). 풀의 Clear()는 풀 안에 들어 있는 것만 지우므로,
    // 이 생성기가 파괴될 때 날아가는 중인 것까지 지우려고 따로 들고 있는다(OnDestroy 참고).
    private readonly HashSet<ShockWave> activeShockWaves = new HashSet<ShockWave>();
    // ComponentCtx 전체가 아니라 실제로 쓰는 스탯 값만 이 인터페이스로 좁혀서 들고 있는다.
    // 캐릭터의 AttackComponent(Initialize(ComponentCtx))든 럼버잭 NPC(Initialize(ICharacterStatForNPC))든
    // 이 인터페이스만 만족하면 동일하게 동작한다.
    private ICharacterStatForNPC stat;
    private OverheatComponent overheatComponent;

    public void Initialize(ComponentCtx _ctx)
    {
        overheatComponent = _ctx.overheatComponent;
        Initialize((ICharacterStatForNPC)_ctx.characterStat);
    }

    public void Initialize(ICharacterStatForNPC _stat)
    {
        stat = _stat;

        if (shockWavePool != null) return;

        shockWavePool = CreatePool(shockWavePrefab);
        if (overheatShockWavePrefab != null)
        {
            overheatShockWavePool = CreatePool(overheatShockWavePrefab);
        }
    }

    // 퍼블릭 제어 메서드

    public ShockWave CreateShockWave(Vector3 _position)
    {
        // 생성기가 파괴된 뒤(메인 메뉴 이탈 중)에 들어온 요청. 호출부는 null을 거른다.
        if (shockWavePool == null) return null;

        // "화염 참격" 특성을 찍어야만 과열 상태의 충격파 강화 효과가 적용된다.
        bool bIsOverheat = (overheatComponent != null && overheatComponent.IsActive && stat.bShockWaveOverheatBoost);
        bool bUseOverheatPool = bIsOverheat && overheatShockWavePool != null;

        ShockWave sw = bUseOverheatPool ? overheatShockWavePool.Get() : shockWavePool.Get();
        if (bUseOverheatPool) overheatShockWaves.Add(sw);

        sw.transform.position = _position;
        sw.SetVisualOrigin(sw.transform);
        sw.Reset();
        // Reset이 강화/과열/치명타 플래그를 비우므로 수치 적용은 반드시 Reset 이후에 한다.
        ApplyShockWaveValues(sw, bIsOverheat);

        return sw;
    }

    public void PlayShockWaveVisual(ShockWave _shockWave)
    {
        _shockWave.VisualComponent?.Play(_shockWave.LifeTime);
    }

    public void ReturnShockWave(ShockWave _shockWave)
    {
        // 생성기가 파괴된 뒤에 돌아온 충격파는 돌려보낼 풀이 없다. 그대로 두면 DontDestroyOnLoad라
        // 고아로 남으므로 지운다. (OnDestroy가 반환 이벤트를 먼저 끊으므로 평소에는 오지 않는다)
        if (shockWavePool == null)
        {
            if (_shockWave != null) Destroy(_shockWave.gameObject);
            return;
        }

        if (overheatShockWaves.Remove(_shockWave))
        {
            overheatShockWavePool.Release(_shockWave);
            return;
        }

        shockWavePool.Release(_shockWave);
    }

    // 내부 풀 관리 메서드

    private IObjectPool<ShockWave> CreatePool(ShockWave _prefab)
    {
        return new ObjectPool<ShockWave>(
            createFunc: () => CreateShockWave(_prefab),
            actionOnGet: OnGetShockWave,
            actionOnRelease: OnReleaseShockWave,
            actionOnDestroy: OnDestroyShockWave,
            collectionCheck: PoolSettings.CollectionCheck,
            defaultCapacity: 5,
            maxSize: 20
        );
    }

    private ShockWave CreateShockWave(ShockWave _prefab)
    {
        ShockWave newSW = Instantiate(_prefab);
        newSW.Initialize();
        newSW.VisualComponent?.Initialize(newSW);

        newSW.ReturnToPoolEvent -= ReturnShockWave;
        newSW.ReturnToPoolEvent += ReturnShockWave;

        DontDestroyOnLoad(newSW);

        return newSW;
    }

    private void OnGetShockWave(ShockWave _shockWave)
    {
        activeShockWaves.Add(_shockWave);
        _shockWave.gameObject.SetActive(true);
    }

    private void ApplyShockWaveValues(ShockWave _shockWave, bool _bIsOverheat)
    {
        float finalDamage = stat.shockWaveDamage;
        float finalDuration = stat.shockWaveDuration;

        if (_bIsOverheat)
        {
            finalDamage *= stat.shockWaveOverheatDamageMul;
            finalDuration *= stat.shockWaveOverheatDurationMul;
        }

        bool bCritical = stat.bShockWaveCritical && UnityEngine.Random.value < stat.criticalChance;
        if (bCritical)
        {
            finalDamage *= stat.ciriticalDamageMul;
        }

        _shockWave.SetValue(finalDamage, stat.shockWaveSpeed, finalDuration);
        _shockWave.SetEnforced(stat.bShockWaveEnforcement);
        _shockWave.SetOverheat(_bIsOverheat, stat.overheatDotDamagePerTick, stat.overheatDotTickCount, stat.overheatDotTickInterval);
        _shockWave.SetCritical(bCritical);
    }

    private void OnReleaseShockWave(ShockWave _shockWave)
    {
        activeShockWaves.Remove(_shockWave);
        _shockWave.gameObject.SetActive(false);
    }

    private void OnDestroyShockWave(ShockWave _shockWave)
    {
        if (_shockWave != null)
        {
            _shockWave.ReturnToPoolEvent -= ReturnShockWave;
            Destroy(_shockWave.gameObject);
        }
    }

    // 충격파는 DontDestroyOnLoad로 띄우므로 이 생성기(캐릭터 소속 → GameInstaller 하위)가 파괴돼도 그대로 남는다.
    // 메인 메뉴 이탈 후 새 게임을 시작하면 새 생성기가 새 풀을 만들기 때문에, 여기서 지우지 않으면 왕복할 때마다
    // 한 벌씩 고아로 쌓인다. 게임 중 동작(씬을 넘어 살아남는 것)은 그대로 두고, 생성기가 파괴되는 순간에만 정리한다.
    //
    // ConstellationPixelLaserCreator는 같은 문제를 "풀러의 자식으로 생성"해서 풀었지만, 이 생성기는 움직이고
    // 차량 탑승 때 꺼지는 캐릭터에 붙어 있어 같은 방법을 쓰면 충격파가 캐릭터를 따라 끌려가고 함께 꺼진다.
    //
    // 함께 지워도 되는 근거: 비주얼 러너(ShockWaveVisualComponent의 정적 풀)는 충격파의 자식이 아니고,
    // 주인이 사라져도 타이머로 스스로 끝나 그 풀로 돌아간다. 충격파는 CollisionSystem을 조회만 하고
    // 등록하지 않으며 추적 사운드도 쓰지 않으므로, 앱 수명 시스템에 죽은 참조가 남지 않는다.
    // OnDisable이 아니라 OnDestroy인 이유: 캐릭터는 차량 탑승 때 꺼졌다 켜지므로 그때 지우면 안 된다.
    private void OnDestroy()
    {
        // 꺼내 쓰는 중인 것 - 반환 이벤트를 먼저 끊어 이미 사라진 풀로 돌아오지 않게 한 뒤 지운다.
        // (Destroy는 프레임 끝에 처리되므로 순회 중에 이 집합이 바뀌지 않는다)
        foreach (ShockWave _shockWave in activeShockWaves)
        {
            if (_shockWave == null) continue;
            _shockWave.ReturnToPoolEvent -= ReturnShockWave;
            Destroy(_shockWave.gameObject);
        }
        activeShockWaves.Clear();
        overheatShockWaves.Clear();

        // 풀 안에 쉬고 있는 것 - Clear가 actionOnDestroy(OnDestroyShockWave)로 지운다.
        shockWavePool?.Clear();
        overheatShockWavePool?.Clear();
        shockWavePool = null;
        overheatShockWavePool = null;
    }
}
