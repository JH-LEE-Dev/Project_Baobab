using System;
using System.Collections.Generic;
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

    // 지금 날아가는 중인 부메랑. 풀의 Clear()는 풀 안에 들어 있는 것만 지우므로,
    // 이 생성기가 파괴될 때 비행 중인 것까지 지우려고 따로 들고 있는다(OnDestroy 참고).
    private readonly HashSet<Boomerang> activeBoomerangs = new HashSet<Boomerang>();

    public void Initialize(StatComponent _statComponent)
    {
        statComponent = _statComponent;

        if (boomerangPool != null) return;

        boomerangPool = new ObjectPool<Boomerang>(
            createFunc: CreateBoomerang,
            actionOnGet: OnGetBoomerang,
            actionOnRelease: OnReleaseBoomerang,
            actionOnDestroy: OnDestroyBoomerang,
            collectionCheck: PoolSettings.CollectionCheck,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    public Boomerang ThrowBoomerang(Vector3 _origin, Vector3 _direction, float _maxDistance, Transform _returnTarget, Action _onFinished, bool _bIsOverheat = false, bool _bPlayHaptic = false)
    {
        Boomerang boomerang = PrepareBoomerang(_bIsOverheat, _bPlayHaptic);
        if (boomerang == null) return null;

        boomerang.Launch(_origin, _direction, _maxDistance, _returnTarget, _onFinished);
        return boomerang;
    }

    /// <summary>
    /// 완료 콜백으로 부메랑 자신을 넘기는 오버로드. 호출자가 발사마다 클로저를 만들지 않고 캐싱된 핸들러 하나를 재사용할 수 있다.
    /// </summary>
    public Boomerang ThrowBoomerang(Vector3 _origin, Vector3 _direction, float _maxDistance, Transform _returnTarget, Action<Boomerang> _onFinished, bool _bIsOverheat = false, bool _bPlayHaptic = false)
    {
        Boomerang boomerang = PrepareBoomerang(_bIsOverheat, _bPlayHaptic);
        if (boomerang == null) return null;

        boomerang.Launch(_origin, _direction, _maxDistance, _returnTarget, _onFinished);
        return boomerang;
    }

    // 풀에서 꺼내 스탯/상태를 세팅한다(발사 직전 공통 처리).
    private Boomerang PrepareBoomerang(bool _bIsOverheat, bool _bPlayHaptic)
    {
        if (boomerangPool == null || statComponent == null) return null;

        Boomerang boomerang = boomerangPool.Get();

        // 데미지, 범위, 속도 스탯 계산
        float finalDamage = statComponent.boomerangDamage;
        float finalHitRadius = statComponent.boomerangHitRadius;
        float finalSpeedMul = 1f;

        if (_bIsOverheat)
        {
            // 과열 효과 배율은 StatComponent의 "Overheat - 화염 부메랑" 항목에서 조정한다
            finalDamage *= statComponent.boomerangOverheatDamageMul;
            finalHitRadius *= statComponent.boomerangOverheatHitRadiusMul;
            finalSpeedMul = statComponent.boomerangOverheatSpeedMul;
        }

        boomerang.SetDamage(finalDamage);

        // 치명타는 여기서 굴리지 않는다. 판정이 왕복 내내 damageInterval마다 반복되는데 발사 시점에
        // 한 번만 굴려 데미지에 곱해두면 한 부메랑이 통째로 치명타이거나 통째로 아니게 되어 편차가
        // 지나치게 커진다. 확률/배율만 넘기고 실제 판정은 Boomerang이 틱마다 한다.
        // (도끼 치명타 확률·배율을 그대로 계승한다 - bBoomerangCritical은 그 적용 여부를 여는 스위치일 뿐이다)
        boomerang.SetCritical(
            statComponent.bBoomerangCritical,
            statComponent.criticalChance,
            statComponent.ciriticalDamageMul);
        boomerang.SetHitRadius(finalHitRadius);
        boomerang.SetSpeedMultiplier(finalSpeedMul);
        // 과열 상태면 과열 전용 스프라이트(OverHeatBoomerang_Base/Effect)로 재생한다.
        boomerang.SetOverheat(_bIsOverheat);
        // damageInterval은 변동 없음
        boomerang.SetDamageInterval(statComponent.boomerangDamageInterval);
        // 풀에서 재사용되므로 이전 소유자(캐릭터/NPC)의 값이 남지 않도록 매번 덮어쓴다.
        boomerang.SetPlayHaptic(_bPlayHaptic);

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

    private void ReturnBoomerang(Boomerang _boomerang)
    {
        // 생성기가 파괴된 뒤에 돌아온 부메랑은 돌려보낼 풀이 없다. 그대로 두면 DontDestroyOnLoad라
        // 고아로 남으므로 지운다. (OnDestroy가 반환 이벤트를 먼저 끊으므로 평소에는 오지 않는다)
        if (boomerangPool == null)
        {
            if (_boomerang != null) Destroy(_boomerang.gameObject);
            return;
        }

        boomerangPool.Release(_boomerang);
    }

    private void OnGetBoomerang(Boomerang _boomerang)
    {
        activeBoomerangs.Add(_boomerang);
        // 스탯 세팅은 _bIsOverheat 상태를 알 수 있는 ThrowBoomerang 내부로 이동됨.
        _boomerang.gameObject.SetActive(true);
    }

    private void OnReleaseBoomerang(Boomerang _boomerang)
    {
        activeBoomerangs.Remove(_boomerang);
        _boomerang.gameObject.SetActive(false);
    }

    private void OnDestroyBoomerang(Boomerang _boomerang)
    {
        if (_boomerang != null)
        {
            _boomerang.ReturnToPoolEvent -= ReturnBoomerang;
            Destroy(_boomerang.gameObject);
        }
    }

    // 부메랑은 DontDestroyOnLoad로 띄우므로 이 생성기(캐릭터 소속 또는 GameInstaller의 NPC 공용)가 파괴돼도
    // 그대로 남는다. 메인 메뉴 이탈 후 새 게임마다 새 풀이 생기므로, 여기서 지우지 않으면 왕복할 때마다 풀 한 벌
    // (최대 maxSize개)과 부메랑마다 딸린 열기 꼬리·잔상이 고아로 쌓인다. 게임 중 동작은 그대로 두고 생성기가
    // 파괴되는 순간에만 정리한다(AxeExtraAttackCreator.OnDestroy와 같은 이유 - 자식 생성 방식은 쓰지 않는다).
    //
    // 함께 지워도 되는 근거: 열기 꼬리·잔상·오라는 부메랑이 직접 만든 것이라 Boomerang.OnDestroy가 함께 지우고
    // 회전 루프음도 거기서 끈다. CollisionSystem은 조회만 하고 등록하지 않는다.
    // OnDisable이 아니라 OnDestroy인 이유: 캐릭터는 차량 탑승 때 꺼졌다 켜지므로 그때 지우면 안 된다.
    private void OnDestroy()
    {
        // 비행 중인 것 - 반환 이벤트를 먼저 끊어 이미 사라진 풀로 돌아오지 않게 한 뒤 지운다.
        // (Destroy는 프레임 끝에 처리되므로 순회 중에 이 집합이 바뀌지 않는다)
        foreach (Boomerang _boomerang in activeBoomerangs)
        {
            if (_boomerang == null) continue;
            _boomerang.ReturnToPoolEvent -= ReturnBoomerang;
            Destroy(_boomerang.gameObject);
        }
        activeBoomerangs.Clear();

        // 풀 안에 쉬고 있는 것 - Clear가 actionOnDestroy(OnDestroyBoomerang)로 지운다.
        boomerangPool?.Clear();
        boomerangPool = null;
    }
}
