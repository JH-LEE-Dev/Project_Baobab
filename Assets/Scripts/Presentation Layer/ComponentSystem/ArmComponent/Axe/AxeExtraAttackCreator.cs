using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class AxeExtraAttackCreator : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private ShockWave shockWavePrefab;

    // 내부 의존성
    private IObjectPool<ShockWave> shockWavePool;
    private ComponentCtx ctx;

    public void Initialize(ComponentCtx _ctx)
    {
        ctx = _ctx;

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
        _shockWave.GetComponent<ShockWaveVisualComponent>()?.Play(ctx.characterStat.shockWaveDuration);
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
        float finalDamage = ctx.characterStat.shockWaveDamage;

        if (ctx.characterStat.bShockWaveCritical && UnityEngine.Random.value < ctx.characterStat.criticalChance)
        {
            finalDamage *= ctx.characterStat.ciriticalDamageMul;
        }

        _shockWave.SetValue(finalDamage, ctx.characterStat.shockWaveSpeed, ctx.characterStat.shockWaveDuration);
        _shockWave.SetEnforced(ctx.characterStat.bShockWaveEnforcement);
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
