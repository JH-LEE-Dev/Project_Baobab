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
    // 캐릭터 혼자 쓸 때는 2~6개면 충분했지만, 이제 럼버잭 NPC들이 별도 인스턴스로 이 풀을 같이
    // 쓸 수 있다(InDungeonUnitSpawner.sharedBoomerangCreator). NPC가 몇 명이든, "부메랑" 스킬이
    // 최대 몇 개까지 오르든 여유 있게 감당하도록 기본값을 넉넉히 잡았다 - 정확한 상한을 몰라도
    // 부족하진 않게, 다만 아이들 상태로 과도하게 남지도 않게 하려는 절충값이다.
    // 주의: 이미 저장된 프리팹/씬 인스턴스는 여기 기본값이 아니라 직렬화된 값을 그대로 쓰므로,
    // 기존 캐릭터용 BoomerangCreator나 새로 만드는 NPC 전용 인스턴스는 인스펙터에서 직접
    // Default Capacity / Max Size를 맞춰줘야 한다.
    [SerializeField] private int defaultCapacity = 5;
    [SerializeField] private int maxSize = 20;

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

    public Boomerang ThrowBoomerang(Vector3 _origin, Vector3 _direction, float _maxDistance, Transform _returnTarget, Action _onFinished, bool _bIsOverheat = false)
    {
        if (boomerangPool == null || statComponent == null) return null;

        Boomerang boomerang = boomerangPool.Get();

        // 데미지, 범위, 속도 스탯 계산
        float finalDamage = statComponent.boomerangDamage;
        float finalHitRadius = statComponent.boomerangHitRadius;
        float finalSpeedMul = 1f;

        if (_bIsOverheat)
        {
            // 과열 효과: 기본 데미지 10000% 추가 증가 (x101), 범위 300% 추가 증가 (x4), 속도 200% 증가 (x3)
            finalDamage *= 101f;
            finalHitRadius *= 4f;
            finalSpeedMul = 3f;
        }

        // 치명타는 최종적으로 증폭된 기본 데미지를 바탕으로 계산됨
        if (statComponent.bBoomerangCritical && UnityEngine.Random.value < statComponent.criticalChance)
        {
            finalDamage *= statComponent.ciriticalDamageMul;
        }

        boomerang.SetDamage(finalDamage);
        boomerang.SetHitRadius(finalHitRadius);
        boomerang.SetSpeedMultiplier(finalSpeedMul);
        // damageInterval은 변동 없음
        boomerang.SetDamageInterval(statComponent.boomerangDamageInterval);

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
        // 스탯 세팅은 _bIsOverheat 상태를 알 수 있는 ThrowBoomerang 내부로 이동됨.
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
