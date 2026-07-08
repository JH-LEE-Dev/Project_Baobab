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

    public void Initialize(StatComponent _statComponent)
    {
        statComponent = _statComponent;

        if (dronePool != null) return;

        dronePool = new ObjectPool<Drone>(
            createFunc: CreateDrone,
            actionOnGet: OnGetDrone,
            actionOnRelease: OnReleaseDrone,
            actionOnDestroy: OnDestroyDrone,
            collectionCheck: true,
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
        dronePool.Release(_drone);
    }

    private Drone CreateDrone()
    {
        Drone newDrone = Instantiate(dronePrefab);
        DontDestroyOnLoad(newDrone);
        return newDrone;
    }

    private void OnGetDrone(Drone _drone) => _drone.gameObject.SetActive(true);

    private void OnReleaseDrone(Drone _drone) => _drone.gameObject.SetActive(false);

    private void OnDestroyDrone(Drone _drone)
    {
        if (_drone != null)
        {
            Destroy(_drone.gameObject);
        }
    }
}
