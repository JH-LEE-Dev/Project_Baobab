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
            collectionCheck: true,
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
        _shockWave.gameObject.SetActive(true);
    }

    private void ApplyShockWaveValues(ShockWave _shockWave, bool _bIsOverheat)
    {
        float finalDamage = stat.shockWaveDamage;
        float finalDuration = stat.shockWaveDuration;

        if (_bIsOverheat)
        {
            finalDamage *= 101f;
            finalDuration *= 4f;
        }

        bool bCritical = stat.bShockWaveCritical && UnityEngine.Random.value < stat.criticalChance;
        if (bCritical)
        {
            finalDamage *= stat.ciriticalDamageMul;
        }

        _shockWave.SetValue(finalDamage, stat.shockWaveSpeed, finalDuration);
        _shockWave.SetEnforced(stat.bShockWaveEnforcement);
        _shockWave.SetOverheat(_bIsOverheat);
        _shockWave.SetCritical(bCritical);
    }

    private void OnReleaseShockWave(ShockWave _shockWave)
    {
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
}
