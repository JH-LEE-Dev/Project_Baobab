using UnityEngine;
using UnityEngine.Pool;

public class BulletObjManager : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Bullet bulletPrefab;

    // 내부 의존성
    private IObjectPool<Bullet> bulletPool;

    private ComponentCtx ctx;

    public void Initialize(ComponentCtx _ctx)
    {
        ctx = _ctx;

        bulletPool = new ObjectPool<Bullet>(
            createFunc: CreateBullet,
            actionOnGet: OnGetBullet,
            actionOnRelease: OnReleaseBullet,
            actionOnDestroy: OnDestroyBullet,
            collectionCheck: true,
            defaultCapacity: 30,
            maxSize: 100
        );
    }

    // 퍼블릭 제어 메서드

    public Bullet GetBullet(Vector3 _position, Quaternion _rotation)
    {
        Bullet bullet = bulletPool.Get();
        bullet.transform.position = _position;
        bullet.transform.rotation = _rotation;
        bullet.Reset();

        return bullet;
    }

    public void ReturnBullet(Bullet _bullet)
    {
        bulletPool.Release(_bullet);
    }

    // 내부 풀 관리 메서드

    private Bullet CreateBullet()
    {
        Bullet newBullet = Instantiate(bulletPrefab);
        newBullet.Initialize();

        newBullet.ReturnToPoolEvent -= ReturnBullet;
        newBullet.ReturnToPoolEvent += ReturnBullet;

        DontDestroyOnLoad(newBullet);

        return newBullet;
    }

    private void OnGetBullet(Bullet _bullet)
    {
        _bullet.SetDamage(ctx.characterStat.rifleDamage);
        _bullet.gameObject.SetActive(true);
    }

    private void OnReleaseBullet(Bullet _bullet)
    {
        _bullet.gameObject.SetActive(false);
    }

    private void OnDestroyBullet(Bullet _bullet)
    {
        if (_bullet != null)
        {
            _bullet.ReturnToPoolEvent -= ReturnBullet;
            Destroy(_bullet.gameObject);
        }
    }
}
