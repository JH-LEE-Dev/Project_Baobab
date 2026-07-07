using System;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 부메랑 생성/재사용을 담당하는 오브젝트 풀. AxeExtraAttackCreator(ShockWave)와 동일한 구조로,
/// Character 등 호출자는 IBoomerangCreator 인터페이스로만 참조한다.
/// </summary>
public class BoomerangCreator : MonoBehaviour, IBoomerangCreator
{
    [SerializeField] private Boomerang boomerangPrefab;
    [SerializeField] private int defaultCapacity = 2;
    [SerializeField] private int maxSize = 6;

    // 데미지/범위/공격속도/치명타는 전부 스킬로 갱신되는 StatComponent 값을 그대로 참조한다
    // (Shockwave가 ICharacterStatForNPC를 통해 stat.shockWaveDamage 등을 참조하는 것과 동일한 방식).
    private StatComponent statComponent;

    private IObjectPool<Boomerang> boomerangPool;

    public void Initialize(StatComponent _statComponent)
    {
        statComponent = _statComponent;

        if (boomerangPool != null) return;

        boomerangPool = new ObjectPool<Boomerang>(
            createFunc: CreateBoomerang,
            actionOnGet: OnGetBoomerang,
            actionOnRelease: OnReleaseBoomerang,
            actionOnDestroy: OnDestroyBoomerang,
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    public Boomerang ThrowBoomerang(Vector3 _origin, Vector3 _direction, float _maxDistance, Transform _returnTarget, Action _onFinished)
    {
        if (boomerangPool == null || statComponent == null) return null;

        Boomerang boomerang = boomerangPool.Get();
        boomerang.Launch(_origin, _direction, _maxDistance, _returnTarget, _onFinished);
        return boomerang;
    }

    private Boomerang CreateBoomerang()
    {
        Boomerang newBoomerang = Instantiate(boomerangPrefab);

        newBoomerang.ReturnToPoolEvent -= ReturnBoomerang;
        newBoomerang.ReturnToPoolEvent += ReturnBoomerang;

        DontDestroyOnLoad(newBoomerang);

        return newBoomerang;
    }

    private void ReturnBoomerang(Boomerang _boomerang) => boomerangPool.Release(_boomerang);

    private void OnGetBoomerang(Boomerang _boomerang)
    {
        // AxeExtraAttackCreator.OnGetShockWave와 동일한 방식: 치명타 적용 스킬이 켜져 있을 때만
        // 공용 치명타 확률/배율(criticalChance, ciriticalDamageMul)로 판정한다.
        float finalDamage = statComponent.boomerangDamage;
        if (statComponent.bBoomerangCritical && UnityEngine.Random.value < statComponent.criticalChance)
        {
            finalDamage *= statComponent.ciriticalDamageMul;
        }

        _boomerang.SetDamage(finalDamage);
        _boomerang.SetHitRadius(statComponent.boomerangHitRadius);
        _boomerang.SetDamageInterval(statComponent.boomerangDamageInterval);
        _boomerang.gameObject.SetActive(true);
    }

    private void OnReleaseBoomerang(Boomerang _boomerang) => _boomerang.gameObject.SetActive(false);

    private void OnDestroyBoomerang(Boomerang _boomerang)
    {
        if (_boomerang != null)
        {
            _boomerang.ReturnToPoolEvent -= ReturnBoomerang;
            Destroy(_boomerang.gameObject);
        }
    }
}
