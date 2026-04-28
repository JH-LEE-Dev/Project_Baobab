using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

public class BulletObjManager : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private Bullet bulletPrefab;

    // 내부 의존성
    private IObjectPool<Bullet> bulletPool;

    private ComponentCtx ctx;

    // 도비탄 타겟 검색을 위한 캐싱 리스트 (GC 최소화)
    private List<IStaticCollidable> ricochetTargets = new List<IStaticCollidable>(10);

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
        _bullet.SetValue(ctx.characterStat);

        _bullet.RicochetEvent -= HandleRicochet;
        _bullet.RicochetEvent += HandleRicochet;

        _bullet.gameObject.SetActive(true);
    }

    private void OnReleaseBullet(Bullet _bullet)
    {
        _bullet.RicochetEvent -= HandleRicochet;
        _bullet.gameObject.SetActive(false);
    }

    private void OnDestroyBullet(Bullet _bullet)
    {
        if (_bullet != null)
        {
            _bullet.ReturnToPoolEvent -= ReturnBullet;
            _bullet.RicochetEvent -= HandleRicochet;
            Destroy(_bullet.gameObject);
        }
    }

    private void HandleRicochet(Bullet _bullet, Vector2 _collisionPos, Vector2 _direction, int _bulletCount, IStaticCollidable _ignoreTarget)
    {
        if (CollisionSystem.Instance == null) return;

        float dist = _bullet.GetRicochetDist();
        float angleLimit = _bullet.GetRicochetAngle() * 0.5f;
        int layerMask = _bullet.GetTargetLayer();

        // 주변 대상 수집
        CollisionSystem.Instance.GetCollidablesInRadius(_collisionPos, dist, layerMask, ricochetTargets);

        int spawnedCount = 0;

        // 각도 내에 있는 대상들을 찾아서 최대 _bulletCount개만큼 발사
        for (int i = 0; i < ricochetTargets.Count; i++)
        {
            if (spawnedCount >= _bulletCount) break;

            IStaticCollidable target = ricochetTargets[i];

            // 자기 자신(현재 충돌한 대상) 제외
            if (Vector2.Distance(target.Position, _collisionPos) < 0.05f) continue;

            // Animal 타입만 대상으로 함
            if (!(target is Animal)) continue;

            Vector2 toTarget = (target.Position - _collisionPos).normalized;
            float angle = Vector2.Angle(_direction, toTarget);

            // 각도 조건 확인
            if (angle <= angleLimit)
            {
                float angleRad = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg;
                Quaternion rotation = Quaternion.Euler(0, 0, angleRad);

                Bullet newBullet = GetBullet(_collisionPos, rotation);

                // 도비탄으로 생성된 총알은 추가 도비탄을 발생시키지 않도록 ricochetCnt를 0으로 설정
                newBullet.SetRicochetValue(
                    ctx.characterStat.ricochetDamage,
                    0,
                    _bullet.GetRicochetAngle(),
                    dist,
                    0,
                    _ignoreTarget
                );

                spawnedCount++;
            }
        }
    }
}
