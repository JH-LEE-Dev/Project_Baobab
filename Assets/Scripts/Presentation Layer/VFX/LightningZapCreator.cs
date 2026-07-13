using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// VFX_LightningZap 생성/재사용을 담당하는 오브젝트 풀. BoomerangCreator/DroneCreator와 동일한 구조다.
/// 별자리 발현처럼 여러 그룹이 동시에 광선을 쏠 수 있는 경우, 인스턴스를 하나만 공유하면 나중에 쏜
/// 광선이 먼저 쏜 광선의 라인/트윈을 덮어써버리므로(VFX_LightningZap.ExecuteZap의 DOTween.Kill),
/// 매번 풀에서 새 인스턴스를 꺼내 쓰게 해서 동시에 여러 개가 독립적으로 재생되도록 한다.
/// </summary>
public class LightningZapCreator : MonoBehaviour
{
    [SerializeField] private PresentationLayer.VFX.VFX_LightningZap lightningZapPrefab;
    [SerializeField] private int defaultCapacity = 2;
    [SerializeField] private int maxSize = 8;

    private IObjectPool<PresentationLayer.VFX.VFX_LightningZap> zapPool;

    public void Initialize()
    {
        if (zapPool != null) return;

        zapPool = new ObjectPool<PresentationLayer.VFX.VFX_LightningZap>(
            createFunc: CreateZap,
            actionOnGet: OnGetZap,
            actionOnRelease: OnReleaseZap,
            actionOnDestroy: OnDestroyZap,
            collectionCheck: true,
            defaultCapacity: defaultCapacity,
            maxSize: maxSize
        );
    }

    /// <summary>
    /// 풀에서 인스턴스를 하나 꺼낸다. 이 인스턴스는 재생이 끝나면(VFX_LightningZap.OnZapComplete)
    /// 스스로 ReturnToPoolEvent를 발생시켜 자동으로 풀에 반환되므로, 호출부가 따로 반환할 필요는 없다.
    /// </summary>
    public PresentationLayer.VFX.VFX_LightningZap Get()
    {
        if (zapPool == null) Initialize();
        return zapPool?.Get();
    }

    private PresentationLayer.VFX.VFX_LightningZap CreateZap()
    {
        PresentationLayer.VFX.VFX_LightningZap newZap = Instantiate(lightningZapPrefab);

        newZap.ReturnToPoolEvent -= ReturnZap;
        newZap.ReturnToPoolEvent += ReturnZap;

        DontDestroyOnLoad(newZap);

        return newZap;
    }

    private void ReturnZap(PresentationLayer.VFX.VFX_LightningZap _zap) => zapPool.Release(_zap);

    private void OnGetZap(PresentationLayer.VFX.VFX_LightningZap _zap)
    {
        _zap.gameObject.SetActive(true);
        _zap.SetColor(Color.blue); // 별자리 발현 광선은 푸른색으로 고정
    }

    private void OnReleaseZap(PresentationLayer.VFX.VFX_LightningZap _zap) => _zap.gameObject.SetActive(false);

    private void OnDestroyZap(PresentationLayer.VFX.VFX_LightningZap _zap)
    {
        if (_zap != null)
        {
            _zap.ReturnToPoolEvent -= ReturnZap;
            Destroy(_zap.gameObject);
        }
    }
}
