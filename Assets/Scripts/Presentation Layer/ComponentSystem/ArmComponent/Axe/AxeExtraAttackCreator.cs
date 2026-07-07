using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class AxeExtraAttackCreator : MonoBehaviour, IShockWaveCreator
{
    // 외부 의존성
    [SerializeField] private ShockWave shockWavePrefab;

    // 내부 의존성
    private IObjectPool<ShockWave> shockWavePool;
    // ComponentCtx 전체가 아니라 실제로 쓰는 스탯 값만 이 인터페이스로 좁혀서 들고 있는다.
    // 캐릭터의 AttackComponent(Initialize(ComponentCtx))든 럼버잭 NPC(Initialize(ICharacterStatForNPC))든
    // 이 인터페이스만 만족하면 동일하게 동작한다.
    private ICharacterStatForNPC stat;

    public void Initialize(ComponentCtx _ctx)
    {
        Initialize((ICharacterStatForNPC)_ctx.characterStat);
    }

    public void Initialize(ICharacterStatForNPC _stat)
    {
        stat = _stat;

        if (shockWavePool != null) return;

        shockWavePool = new ObjectPool<ShockWave>(
            createFunc: CreateShockWave,
            actionOnGet: OnGetShockWave,
            actionOnRelease: OnReleaseShockWave,
            actionOnDestroy: OnDestroyShockWave,
            collectionCheck: true,
            defaultCapacity: 5,
            maxSize: 20
        );
    }

    // 퍼블릭 제어 메서드

    public ShockWave CreateShockWave(Vector3 _position)
    {
        ShockWave sw = shockWavePool.Get();
        sw.transform.position = _position;
        sw.SetVisualOrigin(sw.transform);
        sw.Reset();

        return sw;
    }

    public void PlayShockWaveVisual(ShockWave _shockWave)
    {
        _shockWave.GetComponent<ShockWaveVisualComponent>()?.Play(stat.shockWaveDuration);
    }

    public void ReturnShockWave(ShockWave _shockWave)
    {
        shockWavePool.Release(_shockWave);
    }

    // 내부 풀 관리 메서드

    private ShockWave CreateShockWave()
    {
        ShockWave newSW = Instantiate(shockWavePrefab);
        newSW.Initialize();
        newSW.GetComponent<ShockWaveVisualComponent>()?.Initialize(newSW);

        newSW.ReturnToPoolEvent -= ReturnShockWave;
        newSW.ReturnToPoolEvent += ReturnShockWave;

        DontDestroyOnLoad(newSW);

        return newSW;
    }

    private void OnGetShockWave(ShockWave _shockWave)
    {
        float finalDamage = stat.shockWaveDamage;

        if (stat.bShockWaveCritical && UnityEngine.Random.value < stat.criticalChance)
        {
            finalDamage *= stat.ciriticalDamageMul;
        }

        _shockWave.SetValue(finalDamage, stat.shockWaveSpeed, stat.shockWaveDuration);
        _shockWave.SetEnforced(stat.bShockWaveEnforcement);
        _shockWave.gameObject.SetActive(true);
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
