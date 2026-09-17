// ──────────────────────────────────────────────────────────────────────────
// [사용 안 함 — 기획 결정]  총(소총 / 리코셰) 계열
//
// 이 계열은 쓰지 않기로 결정된 기능입니다. 배선이 빠진 것이 아니라 "안 쓰기로 한 것"이므로,
// 동작하지 않는다고 해서 버그로 보고 되살리거나 배선을 복구하지 마십시오.
//
// 현재 상태: SkillDataBase_Full에 소총·리코셰 커맨드 8종 항목이 없어 WeaponMode 전환이 열리지 않습니다.
//            공격은 항상 도끼 모드로 고정됩니다.
//
// 코드를 지우지 않고 주석만 남긴 이유: WeaponMode가 AttackComponent(15곳) · ArmComponent(6곳) 같은
//   "지금 돌아가는 공격 경로"에 박혀 있어, 삭제가 곧 전투 경로 리팩터가 됩니다.
// 정리하려면 에디터에서 컴파일이 도는 상태로 독립 커밋으로 진행하십시오.
// ──────────────────────────────────────────────────────────────────────────
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
