using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// "시간 0에 상수 개수 버스트 1회"로만 이루어진 단발 파티클 프리팹을 인스턴스 하나로 공유해 재생합니다.
    ///
    /// 풀 방식(인스턴스를 꺼내 SetParent → SetActive(true) → Play)은 타격마다 오브젝트 활성화 비용과 인스턴스별
    /// 드로우콜이 들고, 과열 충격파처럼 한 프레임에 수십 그루가 맞으면 그 비용이 그대로 스파이크가 됩니다.
    /// 여기서는 프리팹을 한 번만 인스턴스화해 원점에 고정해 두고(부모 없음 - 풀 인스턴스가 재생 중 분리돼 있던 것과 같은 조건),
    /// 재생 요청마다 각 시스템에 EmitParams.position(요청 위치, Shape 오프셋 적용)으로 버스트 개수만큼 Emit합니다.
    /// 루트를 움직이지 않으므로 시뮬레이션 공간이 Local이어도 이미 나간 입자가 따라 움직이지 않습니다.
    ///
    /// 회전은 받지 않습니다. 시스템 트랜스폼이 항상 identity이므로 요청 회전이 identity가 아니면 Emit이 false를 돌려주고
    /// 호출부가 풀 경로로 재생합니다(나무의 top/bottom 루트는 회전하지 않아 실제로는 항상 identity입니다).
    ///
    /// 호환되는 프리팹만 받습니다(IsCompatible). 조건을 벗어나는 프리팹(지연 버스트, 반복 버스트, 확률 버스트,
    /// 곡선 개수, 서브이미터, 트레일, 속도 상속, 의미 있는 시간당 방출)은 Emit만으로는 같은 모양을 만들 수 없으므로
    /// 호출부가 기존 풀 경로를 그대로 쓰게 둡니다.
    /// </summary>
    public sealed class SharedBurstEmitter
    {
        private struct SystemEntry
        {
            public ParticleSystem system;
            public ParticleSystem.MainModule main;
            public Transform transform;
            public int burstCount;
        }

        // 내부 의존성
        private GameObject rootObject;
        private Transform rootTransform;
        private readonly SystemEntry[] systems; // [0]이 루트, 나머지는 자식(프리팹 계층 순서)

        public GameObject RootObject => rootObject;

        /// <summary>
        /// 이 프리팹을 Emit만으로 똑같이 재생할 수 있는지 검사합니다.
        /// </summary>
        public static bool IsCompatible(ParticleSystem _prefab)
        {
            if (null == _prefab) return false;

            ParticleSystem[] all = _prefab.GetComponentsInChildren<ParticleSystem>(true);
            if (null == all || 0 == all.Length) return false;

            for (int i = 0; i < all.Length; i++)
            {
                ParticleSystem ps = all[i];
                if (null == ps) return false;

                ParticleSystem.MainModule main = ps.main;
                if (true == main.loop) return false;
                // Custom 시뮬레이션 공간은 외부 트랜스폼을 따라가므로 제외. Local/World는 루트가 원점에 고정이라 같은 결과다.
                if (ParticleSystemSimulationSpace.Custom == main.simulationSpace) return false;
                // World 공간 자식이 로컬 오프셋을 가지면 네이티브 Emit이 Shape 위치(자식 위치 포함)에 EmitParams.position을 더해
                // 오프셋이 두 번 들어갈 수 있다. 현재 프리팹에는 없지만 확정할 수 없는 경우라 풀 경로에 맡긴다.
                if (ParticleSystemSimulationSpace.World == main.simulationSpace && ps.transform != _prefab.transform && Vector3.zero != ps.transform.localPosition) return false;
                if (false == IsZeroConstant(main.startDelay)) return false;

                if (true == ps.subEmitters.enabled) return false;
                if (true == ps.trails.enabled) return false;
                if (true == ps.inheritVelocity.enabled) return false;

                ParticleSystem.EmissionModule emission = ps.emission;
                if (true == emission.enabled)
                {
                    if (false == IsZeroConstant(emission.rateOverDistance)) return false;

                    // 시간당 방출은 Emit으로 재현하지 않는다. 다만 "duration 동안 누적해도 입자 1개에 못 미치는" 값은
                    // 풀 경로에서도 실제로 입자를 내지 않으므로(누적기가 재생 시 0에서 시작) 같은 결과로 보고 허용한다.
                    // (예: rate 10/s × 0.05s = 0.5개. 재생 프레임에 0.1초 이상 끊기는 극단 상황에서만 예전 경로가 1개를 더 낼 수 있다)
                    ParticleSystem.MinMaxCurve rateOverTime = emission.rateOverTime;
                    if (ParticleSystemCurveMode.Constant != rateOverTime.mode) return false;
                    if (0f != rateOverTime.constant && 1f <= rateOverTime.constant * main.duration) return false;

                    int burstCount = emission.burstCount;
                    for (int b = 0; b < burstCount; b++)
                    {
                        ParticleSystem.Burst burst = emission.GetBurst(b);
                        if (0f != burst.time) return false;
                        if (1 != burst.cycleCount) return false;
                        if (1f > burst.probability) return false;
                        if (ParticleSystemCurveMode.Constant != burst.count.mode) return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// 프리팹을 부모 없이 원점에 한 번 인스턴스화하고 공유 재생용으로 설정합니다. 씬 전환에도 살아남으며 Dispose로 지웁니다.
        /// _capacityMultiplier는 동시에 겹칠 수 있는 재생 수(예전 풀 상한)로, 시스템별 최대 입자 수를 버스트 개수 × 이 값까지 늘립니다.
        /// </summary>
        public SharedBurstEmitter(ParticleSystem _prefab, int _capacityMultiplier, string _name)
        {
            ParticleSystem instance = Object.Instantiate(_prefab, Vector3.zero, Quaternion.identity);
            instance.gameObject.name = _name;
            if (true == Application.isPlaying) Object.DontDestroyOnLoad(instance.gameObject); // 에디터(편집 모드) 검증 코드에서도 만들 수 있게 플레이 중에만 건다
            rootObject = instance.gameObject;
            rootTransform = instance.transform;

            ParticleSystem[] all = instance.GetComponentsInChildren<ParticleSystem>(true);
            systems = new SystemEntry[all.Length];

            for (int i = 0; i < all.Length; i++)
            {
                ParticleSystem ps = all[i];

                // Instantiate 직후 playOnAwake로 원점에서 터진 버스트를 같은 프레임 안에 지운다
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = ps.main;
                int burstCount = SumBurstCount(ps);

                main.playOnAwake = false;
                main.loop = true; // 방출 없이 계속 "재생 중" 상태를 유지해 Emit으로 넣은 입자가 항상 시뮬레이션되게 한다
                main.stopAction = ParticleSystemStopAction.None;
                // 풀 방식의 단발 이펙트는 ApplyPoolInstanceSettings가 AlwaysSimulate로 바꿔 화면 밖에서도 끝까지 돌렸다. 같은 조건을 유지한다.
                main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
                if (0 < burstCount)
                {
                    main.maxParticles = Mathf.Max(main.maxParticles, burstCount * Mathf.Max(1, _capacityMultiplier));
                }

                // 자체 방출(버스트/rateOverTime)은 끄고 Emit으로만 입자를 넣는다
                ParticleSystem.EmissionModule emission = ps.emission;
                emission.enabled = false;

                systems[i] = new SystemEntry { system = ps, main = main, transform = ps.transform, burstCount = burstCount };
            }

            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].system.Play(false);
            }
        }

        /// <summary>
        /// 요청 위치에서 프리팹의 버스트를 한 번 재생합니다. 재생했으면 true를 돌려줍니다.
        /// 요청 회전이 루트 회전(identity)과 다르면 false를 돌려주고, 호출부가 풀 경로로 재생해야 합니다.
        /// 색 덮어쓰기 규칙은 VFXComponent.Play(VFXPlaySettings)와 같습니다: 루트는 _overrideColor일 때,
        /// 자식은 _overrideColor && _overrideChildrenColor일 때 startColor를 덮어씁니다.
        /// </summary>
        public bool Emit(Vector3 _position, Quaternion _rotation, bool _overrideColor, ParticleSystem.MinMaxGradient _startColor, bool _overrideChildrenColor)
        {
            if (null == rootTransform) return false;
            if (rootTransform.rotation != _rotation) return false;

            for (int i = 0; i < systems.Length; i++)
            {
                SystemEntry entry = systems[i];
                if (null == entry.system) continue;

                if (true == _overrideColor && (0 == i || true == _overrideChildrenColor))
                {
                    entry.main.startColor = _startColor;
                }

                if (0 < entry.burstCount)
                {
                    // 풀 인스턴스에서는 루트가 요청 위치에 놓이고 자식은 프리팹의 로컬 오프셋만큼 떨어져 있었다.
                    // 여기서는 루트가 원점에 고정이므로 "요청 위치 + 자식의 로컬 오프셋"을 각 시스템의 시뮬레이션 공간 좌표로 넘긴다.
                    Vector3 worldPos = _position + (entry.transform.position - rootTransform.position);
                    ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams();
                    emitParams.position = ParticleSystemSimulationSpace.World == entry.main.simulationSpace
                        ? worldPos
                        : entry.transform.InverseTransformPoint(worldPos);
                    emitParams.applyShapeToPosition = true; // Shape 모듈의 위치 분포는 그대로 더해진다(방향/속도는 평소대로 Shape가 정한다)

                    entry.system.Emit(emitParams, entry.burstCount);
                }
            }

            return true;
        }

        /// <summary>
        /// 살아있는 입자를 전부 지웁니다(풀 방식의 StopAll과 같은 시점에 호출).
        /// </summary>
        public void Clear()
        {
            for (int i = 0; i < systems.Length; i++)
            {
                if (null != systems[i].system)
                {
                    systems[i].system.Clear(false);
                }
            }
        }

        /// <summary>
        /// 공유 인스턴스를 파괴합니다(소유 매니저가 파괴될 때).
        /// </summary>
        public void Dispose()
        {
            if (null != rootObject)
            {
                if (true == Application.isPlaying) Object.Destroy(rootObject);
                else Object.DestroyImmediate(rootObject); // 편집 모드(에디터 검증 코드)에서는 Destroy를 쓸 수 없다
            }

            rootObject = null;
            rootTransform = null;
        }

        private static bool IsZeroConstant(ParticleSystem.MinMaxCurve _curve)
        {
            switch (_curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return 0f == _curve.constant;
                case ParticleSystemCurveMode.TwoConstants:
                    return 0f == _curve.constantMin && 0f == _curve.constantMax;
                default:
                    return false;
            }
        }

        private static int SumBurstCount(ParticleSystem _ps)
        {
            ParticleSystem.EmissionModule emission = _ps.emission;
            if (false == emission.enabled) return 0;

            int total = 0;
            int burstCount = emission.burstCount;
            for (int b = 0; b < burstCount; b++)
            {
                ParticleSystem.Burst burst = emission.GetBurst(b);
                total += Mathf.RoundToInt(burst.count.constant);
            }

            return total;
        }
    }
}
