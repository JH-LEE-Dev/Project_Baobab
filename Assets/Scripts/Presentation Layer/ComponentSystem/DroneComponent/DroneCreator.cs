using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// 드론 생성/재사용을 담당하는 오브젝트 풀. BoomerangCreator와 동일한 구조다.
/// 부메랑과 달리 드론은 던전 입장 시 소환되어 캐릭터를 계속 따라다니는 지속형 개체이므로,
/// 데미지/판정 주기 등은 Get 시점에 미리 굽지 않고 Character가 활성화(Activate)할 때마다
/// StatComponent의 최신 값을 그대로 전달한다.
/// </summary>
public class DroneCreator : MonoBehaviour, IDroneCreator
{
    [SerializeField] private Drone dronePrefab;
    [SerializeField] private int defaultCapacity = 4;
    [SerializeField] private int maxSize = 8;

    private StatComponent statComponent;
    private IObjectPool<Drone> dronePool;

    // 지금 소환돼 있는 드론. 풀의 Clear()는 풀 안에 들어 있는 것만 지우므로,
    // 이 생성기가 파괴될 때 소환 중인 것까지 지우려고 따로 들고 있는다(OnDestroy 참고).
    private readonly HashSet<Drone> spawnedDrones = new HashSet<Drone>();

    public void Initialize(StatComponent _statComponent)
    {
        statComponent = _statComponent;

        if (dronePool != null) return;

        dronePool = new ObjectPool<Drone>(
            createFunc: CreateDrone,
            actionOnGet: OnGetDrone,
            actionOnRelease: OnReleaseDrone,
            actionOnDestroy: OnDestroyDrone,
            collectionCheck: PoolSettings.CollectionCheck,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    public Drone SpawnDrone(Vector3 _position, Transform _followTarget)
    {
        if (dronePool == null || statComponent == null) return null;

        Drone drone = dronePool.Get();
        drone.Spawn(_position, _followTarget);
        return drone;
    }

    public void DespawnDrone(Drone _drone)
    {
        if (_drone == null) return;

        // 생성기가 파괴된 뒤에 돌아온 드론은 돌려보낼 풀이 없다. 그대로 두면 DontDestroyOnLoad라
        // 고아로 남으므로 지운다. (Character.ClearActiveDrones는 씬 전환·사망 리셋에서만 불려 평소에는 오지 않는다)
        if (dronePool == null)
        {
            Destroy(_drone.gameObject);
            return;
        }

        dronePool.Release(_drone);
    }

    private Drone CreateDrone()
    {
        Drone newDrone = Instantiate(dronePrefab);
        DontDestroyOnLoad(newDrone);
        return newDrone;
    }

    private void OnGetDrone(Drone _drone)
    {
        spawnedDrones.Add(_drone);
        _drone.gameObject.SetActive(true);
    }

    private void OnReleaseDrone(Drone _drone)
    {
        spawnedDrones.Remove(_drone);
        _drone.gameObject.SetActive(false);
    }

    private void OnDestroyDrone(Drone _drone)
    {
        if (_drone != null)
        {
            Destroy(_drone.gameObject);
        }
    }

    // 드론은 DontDestroyOnLoad로 띄우므로 이 생성기(캐릭터 소속)가 파괴돼도 그대로 남는다. 메인 메뉴 이탈 후 새
    // 게임마다 새 풀이 생기므로, 여기서 지우지 않으면 왕복할 때마다 고아로 쌓이고, 소환 중이던 드론은 주인 없이
    // 활성 상태로 남아 LateUpdate 정렬을 매 프레임 계속 돈다. 게임 중 동작은 그대로 두고 생성기가 파괴되는
    // 순간에만 정리한다(AxeExtraAttackCreator.OnDestroy와 같은 이유 - 자식 생성 방식은 쓰지 않는다).
    //
    // 소환 중인 드론은 지우기 전에 Despawn()을 먼저 태운다. 충전음을 추적 재생(Sound.PlayTracked)하므로,
    // 충전 도중 그냥 지우면 앱 수명 사운드 시스템에 소리가 남는다. Despawn이 충전 이펙트·과열 오라·충전음을
    // 모두 끈다(Character.ClearActiveDrones와 같은 정상 경로). 이펙트는 드론 자신의 VFXComponent에서
    // 꺼낸 것이라 드론과 함께 사라져도 다른 풀에 죽은 참조가 남지 않는다.
    // OnDisable이 아니라 OnDestroy인 이유: 캐릭터는 차량 탑승 때 꺼졌다 켜지므로 그때 지우면 안 된다.
    private void OnDestroy()
    {
        foreach (Drone _drone in spawnedDrones)
        {
            if (_drone == null) continue;
            _drone.Despawn();
            Destroy(_drone.gameObject);
        }
        spawnedDrones.Clear();

        // 풀 안에 쉬고 있는 것 - Clear가 actionOnDestroy(OnDestroyDrone)로 지운다.
        dronePool?.Clear();
        dronePool = null;
    }
}
