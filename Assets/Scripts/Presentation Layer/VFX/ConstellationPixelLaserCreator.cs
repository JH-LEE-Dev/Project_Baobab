using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// VFX_ConstellationPixelLaser 생성/재사용을 담당하는 오브젝트 풀. LightningZapCreator와 동일한 구조다.
/// 서로 다른 별자리 그룹이 동시에 발현될 수 있으므로(별자리 잔상 스킬이면 같은 그룹도 여러 발),
/// 매번 풀에서 새 인스턴스를 꺼내 각자 독립적으로 재생되게 한다.
/// </summary>
public class ConstellationPixelLaserCreator : MonoBehaviour
{
    [SerializeField] private PresentationLayer.VFX.ConstellationPixelLaser laserPrefab;
    [SerializeField] private int defaultCapacity = 2;
    [SerializeField] private int maxSize = 8;

    private IObjectPool<PresentationLayer.VFX.ConstellationPixelLaser> laserPool;

    // 동시에 쓰인 레이저가 maxSize를 넘어 반환 시 풀이 인스턴스를 파괴할 때 발생 - 인스턴스별 문맥을
    // 캐싱해둔 쪽(InDungeonObjectManager)이 파괴된 인스턴스의 항목을 지울 수 있게 한다.
    public event System.Action<PresentationLayer.VFX.ConstellationPixelLaser> LaserDestroyedEvent;

    public void Initialize()
    {
        if (null != laserPool) return;

        laserPool = new ObjectPool<PresentationLayer.VFX.ConstellationPixelLaser>(
            createFunc: CreateLaser,
            actionOnGet: OnGetLaser,
            actionOnRelease: OnReleaseLaser,
            actionOnDestroy: OnDestroyLaser,
            collectionCheck: PoolSettings.CollectionCheck,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );

        Prewarm();
    }

    // ObjectPool은 defaultCapacity만큼 미리 만들어두지 않으므로, 첫 발현 프레임에 프리팹 생성과 세그먼트 생성이
    // 몰리지 않도록 초기화 시점에 미리 꺼냈다가 돌려놓는다. (꺼낸 동안 동시에 들고 있어야 서로 다른 인스턴스가 생성된다)
    private void Prewarm()
    {
        if (null == laserPrefab || 0 >= defaultCapacity) return;

        PresentationLayer.VFX.ConstellationPixelLaser[] prewarmed = new PresentationLayer.VFX.ConstellationPixelLaser[defaultCapacity];
        for (int i = 0; i < prewarmed.Length; i++)
        {
            prewarmed[i] = laserPool.Get();
        }
        for (int i = 0; i < prewarmed.Length; i++)
        {
            laserPool.Release(prewarmed[i]);
        }
    }

    /// <summary>
    /// 풀에서 인스턴스를 하나 꺼낸다. 이 인스턴스는 연출이 끝나면(또는 외부에서 ReturnToPool()을 호출하면)
    /// 스스로 ReturnToPoolEvent를 발생시켜 자동으로 풀에 반환되므로, 호출부가 따로 반환할 필요는 없다.
    /// 프리팹이 연결되지 않았으면 null을 반환한다.
    /// </summary>
    public PresentationLayer.VFX.ConstellationPixelLaser Get()
    {
        if (null == laserPrefab) return null;
        if (null == laserPool) Initialize();
        return laserPool.Get();
    }

    private PresentationLayer.VFX.ConstellationPixelLaser CreateLaser()
    {
        // 루트에 DontDestroyOnLoad로 두면 메인 메뉴 복귀(GameInstaller 파괴 -> 재생성) 때마다 이전 풀의 레이저가
        // 고아로 남아 쌓인다. 이 풀러(GameInstaller 하위, DontDestroyOnLoad)의 자식으로 두면 Town↔Dungeon
        // 전환에는 살아남고 GameInstaller와 함께 파괴된다. 레이저는 월드 좌표를 자기 Transform 기준으로
        // 변환해 그리므로 부모 위치와 무관하다.
        PresentationLayer.VFX.ConstellationPixelLaser newLaser = Instantiate(laserPrefab, transform);

        newLaser.ReturnToPoolEvent -= ReturnLaser;
        newLaser.ReturnToPoolEvent += ReturnLaser;

        return newLaser;
    }

    private void ReturnLaser(PresentationLayer.VFX.ConstellationPixelLaser _laser) => laserPool.Release(_laser);

    private void OnGetLaser(PresentationLayer.VFX.ConstellationPixelLaser _laser) => _laser.gameObject.SetActive(true);

    private void OnReleaseLaser(PresentationLayer.VFX.ConstellationPixelLaser _laser) => _laser.gameObject.SetActive(false);

    private void OnDestroyLaser(PresentationLayer.VFX.ConstellationPixelLaser _laser)
    {
        if (null != _laser)
        {
            LaserDestroyedEvent?.Invoke(_laser);
            _laser.ReturnToPoolEvent -= ReturnLaser;
            Destroy(_laser.gameObject);
        }
    }
}
