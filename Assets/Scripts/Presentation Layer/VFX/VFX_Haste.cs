using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 캐릭터의 헤이스트(이동속도 버프) 연출 중 VFX 풀로 재생하는 부분. 화살표나 상승 표시, 직선 꼬리 없이 절제된 연출만 남긴다.
    /// 버프가 유지되는 동안 캐릭터를 따라다니며, Begin으로 시작하고 End로 끝낸다(끝나면 스스로 풀에 반환).
    ///
    /// 구성(하늘색 한 계열, 별 표식 팔레트와 같은 흰색/하늘색/파랑)
    /// - 발동 연출(0.85초): 입자가 나선으로 감아 오르며 가슴 높이에 `◆`가 번쩍인다(바닥 고리는 없다).
    /// - 걸음 먼지: 걸음마다 좌우 발 번갈아 발밑에서 `●` -> `+` -> `.`로 줄어드는 먼지 2~3개가 이동 반대쪽으로 밀린다.
    /// - 발끝 스파크: 걸음마다 3~4개가 이동 반대쪽으로 튀어 오르고 중력으로 떨어지며 `x` -> `+` -> `.`로 줄어든다.
    /// - 발끝 반짝임: 멈춰 있어도 버프 중임을 알리는 `◆` `+` `.` 반짝임이 몸 주변에서 올라간다.
    ///
    /// 몸 스프라이트를 복제하는 잔상은 쓰지 않는다(복제본의 정렬 순서가 만든 시점에 고정돼 위로 달릴 때 캐릭터를 덮고 버벅여 보였다).
    /// 속도와 방향은 대상 Transform의 위치 변화량으로 스스로 계산하므로 이동 코드와 결합하지 않는다.
    /// 아이소 지면 평면 기준(화면 y는 지면 y의 절반)으로 방향을 환산한다.
    /// 캐릭터 속도 자체는 이 이펙트가 바꾸지 않는다(실제 이동속도 버프와 같이 쓴다).
    ///
    /// 렌더링: 뒤쪽 메쉬(캐릭터 소팅 -1, 먼지), 앞쪽 메쉬(캐릭터 소팅 +1, 스파크/반짝임/발동 입자).
    /// 캐릭터 소팅 오더는 Begin에 넘긴 SpriteRenderer를 매 프레임 따라간다(캐릭터가 움직이며 y 기반 오더가 바뀐다).
    ///
    /// 픽셀 규칙: 선분 없이 점/도형만 쓰므로 DOUBLE PIXEL(L자 모서리), HUGGING 문제가 생기지 않는다.
    /// 입자는 `.` `+` `x` `●` `◆`만 쓰고 모든 그림이 매 프레임 다시 그려진다. 정적 버퍼와 고정 크기 배열만 써서 재생 중 힙 할당이 없다.
    /// </summary>
    [DefaultExecutionOrder(10000)] // 캐릭터 이동과 카메라 갱신이 끝난 뒤의 위치를 따라가도록 다른 LateUpdate보다 늦게 실행한다
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_Haste : MonoBehaviour
    {
        [Header("뒤쪽 메쉬 (캐릭터 뒤에 가려지는 먼지)")]
        [SerializeField] private MeshFilter backMeshFilter;
        [SerializeField] private MeshRenderer backMeshRenderer;

        private const float PixelsPerUnit = 32.0f;
        private const float TwoPi = Mathf.PI * 2.0f;
        private const float BurstLength = 0.85f;
        private const float EndDrainSeconds = 0.6f;
        private const float StrideLengthPx = 13.0f;
        private const float MovingThresholdPx = 8.0f;
        private const float TeleportUnits = 4.0f;
        private const float MotionBlendRate = 45.0f;
        private const int PuffCapacity = 64;
        private const int SparkCapacity = 40;
        private const float SparkGravity = 95.0f;
        private const int QuadBudget = 1500;

        // 팔레트 (별 표식 / 스텔라 폭발과 같은 하늘색 계열)
        private static readonly Color32 ColorWhite = new Color32(255, 255, 255, 255);
        private static readonly Color32 ColorIce = new Color32(191, 230, 255, 255);
        private static readonly Color32 ColorIce2 = new Color32(140, 196, 255, 255);
        private static readonly Color32 ColorBlue = new Color32(91, 146, 230, 255);
        private static readonly Color32 ColorBlue2 = new Color32(52, 86, 158, 255);

        // 시간 단계별 색(밝음 -> 어두움). 정점 알파 = 발광 세기(블룸 임계값 근처로 낮춰 색이 흰색으로 날아가지 않게 한다).
        private static readonly Color32[] Steps =
        {
            GlowColor(ColorWhite, 0.3f), GlowColor(ColorIce, 0.22f), GlowColor(ColorIce2, 0.16f), GlowColor(ColorBlue, 0.1f), GlowColor(ColorBlue2, 0.06f),
        };

        private struct HastePuff
        {
            public float x;
            public float y;
            public float velocityX;
            public float velocityY;
            public float age;
            public float life;
            public bool bCross;
            public bool bBig;
        }

        private struct HasteSpark
        {
            public float x;
            public float y;
            public float velocityX;
            public float velocityY;
            public float age;
            public float life;
        }

        private static readonly PixelQuadBuffer frontBuffer = new PixelQuadBuffer(QuadBudget);
        private static readonly PixelQuadBuffer backBuffer = new PixelQuadBuffer(QuadBudget / 2);

        // 외부 의존성
        private Transform target;
        private SpriteRenderer sortingSource;

        // 내부 의존성
        private readonly PixelPainter frontPainter = new PixelPainter();
        private readonly PixelPainter backPainter = new PixelPainter();
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh frontMesh;
        private Mesh backMesh;
        private ParticleSystemRenderer rootParticleRenderer;
        private ParticleSystem rootParticleSystem;

        private readonly HastePuff[] puffs = new HastePuff[PuffCapacity];
        private readonly HasteSpark[] sparks = new HasteSpark[SparkCapacity];

        // 상태 변수
        private bool bActive;
        private bool bEnding;
        private bool bInitialized;
        private bool bHasLast;
        private float elapsed;
        private float burstTime = -1.0f;
        private float endTimer;
        private Vector3 lastPosition;
        private float velocityX;
        private float velocityY;
        private float groundSpeed;
        private float moveDirX = 1.0f;
        private float moveDirY;
        private float groundDirX = 1.0f;
        private float groundDirY;
        private float gait;
        private int lastStep;
        private int stepCounter;
        private uint randomState = 1u;
        private int puffCount;
        private int sparkCount;
        private int centerX;
        private int centerY;
        private Vector3 cullingSize = new Vector3(8.0f, 8.0f, 10.0f);

        private int appliedLayerId = int.MinValue;
        private int appliedOrder = int.MinValue;
        private ulong frontHash;
        private int frontVertexCount = -1;
        private ulong backHash;
        private int backVertexCount = -1;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>
        /// 헤이스트 연출을 시작한다. _target은 따라다닐 캐릭터(발밑 위치), _sortingSource는 캐릭터 본체 SpriteRenderer(소팅 기준).
        /// 풀에서 꺼내 활성화한 직후에 부른다. _sortingSource가 null이면 루트 렌더러의 소팅을 그대로 쓴다.
        /// </summary>
        public void Begin(Transform _target, SpriteRenderer _sortingSource)
        {
            EnsureInitialized();

            target = _target;
            sortingSource = _sortingSource;

            if (null != target) transform.position = target.position;

            elapsed = 0.0f;
            burstTime = 0.0f;
            bEnding = false;
            endTimer = 0.0f;
            bHasLast = false;
            velocityX = 0.0f;
            velocityY = 0.0f;
            groundSpeed = 0.0f;
            moveDirX = 1.0f;
            moveDirY = 0.0f;
            groundDirX = 1.0f;
            groundDirY = 0.0f;
            gait = 0.0f;
            lastStep = 0;
            stepCounter = 0;
            puffCount = 0;
            sparkCount = 0;
            frontVertexCount = -1;
            backVertexCount = -1;
            appliedLayerId = int.MinValue;
            appliedOrder = int.MinValue;
            randomState = unchecked((uint)(Time.frameCount * 2654435761u) | 1u);
            bActive = true;
        }

        /// <summary>
        /// 헤이스트가 끝났다. 새 입자 생성을 멈추고 남은 입자가 사라진 뒤(0.6초) 스스로 풀에 반환한다.
        /// </summary>
        public void End()
        {
            if (false == bActive || true == bEnding) return;

            bEnding = true;
            endTimer = 0.0f;
        }

        /// <summary>연출 없이 즉시 풀에 반환한다(캐릭터 사망, 씬 이탈 등).</summary>
        public void Cancel()
        {
            if (false == bActive) return;

            Finish();
        }

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;
            rootParticleSystem = null != transform.parent ? transform.parent.GetComponent<ParticleSystem>() : null;

            frontMesh = CreateMesh(meshFilter, meshRenderer, "VFX_Haste_Front");

            if (null != backMeshFilter && null != backMeshRenderer) backMesh = CreateMesh(backMeshFilter, backMeshRenderer, "VFX_Haste_Back");
        }

        private static Mesh CreateMesh(MeshFilter _filter, MeshRenderer _renderer, string _name)
        {
            Mesh mesh = new Mesh { name = _name };
            mesh.MarkDynamic();
            _filter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 600);

            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
            return mesh;
        }

        // 계산

        private static Color32 GlowColor(Color32 _base, float _glow)
        {
            byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(_glow) * 255.0f);
            return new Color32(_base.r, _base.g, _base.b, alpha);
        }

        private float Rand01()
        {
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            return ((randomState >> 8) & 0xFFFF) / 65535.0f;
        }

        private static float EaseOutCubic(float _t)
        {
            float inv = 1.0f - _t;
            return 1.0f - inv * inv * inv;
        }

        // 대상의 위치 변화량으로 속도(px/s)와 방향, 걸음 수를 갱신한다. 순간이동(큰 변화)은 속도로 보지 않는다.
        // 화면 방향(moveDir)과 아이소 지면 방향(groundDir, 화면 y를 2배로 환산)은 움직이는 동안에만 갱신해 멈춘 뒤에도 마지막 방향을 유지한다.
        private void UpdateMotion(float _dt)
        {
            Vector3 position = transform.position;
            if (true == bHasLast)
            {
                Vector3 delta = position - lastPosition;
                if (delta.magnitude < TeleportUnits)
                {
                    float rawX = delta.x * PixelsPerUnit / _dt;
                    float rawY = delta.y * PixelsPerUnit / _dt;
                    float blend = 1.0f - Mathf.Exp(-_dt * MotionBlendRate);
                    velocityX += (rawX - velocityX) * blend;
                    velocityY += (rawY - velocityY) * blend;
                }
                else
                {
                    velocityX = 0.0f;
                    velocityY = 0.0f;
                }
            }

            lastPosition = position;
            bHasLast = true;

            groundSpeed = Mathf.Sqrt(velocityX * velocityX + 4.0f * velocityY * velocityY);

            float screenSpeed = Mathf.Sqrt(velocityX * velocityX + velocityY * velocityY);
            if (1.0f <= screenSpeed)
            {
                moveDirX = velocityX / screenSpeed;
                moveDirY = velocityY / screenSpeed;

                float groundX = moveDirX;
                float groundY = moveDirY * 2.0f;
                float groundLength = Mathf.Sqrt(groundX * groundX + groundY * groundY);
                groundDirX = groundX / groundLength;
                groundDirY = groundY / groundLength;
            }

            if (MovingThresholdPx < groundSpeed) gait += groundSpeed * _dt / StrideLengthPx;
        }

        // 걸음 먼지 - 좌우 발 번갈아 발밑에서 2~3개가 이동 반대쪽으로 밀린다. 같은 발 위치에 스파크도 함께 만든다.
        private void SpawnPuffs()
        {
            int side = 0 == (stepCounter & 1) ? -1 : 1;
            stepCounter++;

            float footX = transform.position.x * PixelsPerUnit + side * (-groundDirY) * 3.0f;
            float footY = transform.position.y * PixelsPerUnit + side * groundDirX * 1.5f;
            int count = 2 + (Rand01() < 0.6f ? 1 : 0);

            for (int i = 0; i < count && puffCount < PuffCapacity; i++)
            {
                HastePuff puff = new HastePuff();
                puff.x = footX + (Rand01() - 0.5f) * 4.0f;
                puff.y = footY + 1.0f;
                puff.velocityX = -moveDirX * (14.0f + Rand01() * 22.0f) + (Rand01() - 0.5f) * 6.0f;
                puff.velocityY = -moveDirY * (8.0f + Rand01() * 12.0f) * 0.7f + 4.0f + Rand01() * 10.0f;
                puff.age = 0.0f;
                puff.life = 0.28f + Rand01() * 0.3f;
                puff.bCross = Rand01() < 0.3f;
                puff.bBig = Rand01() < 0.25f;
                puffs[puffCount++] = puff;
            }

            SpawnSparks(footX, footY);
        }

        // 발끝 스파크 - 3~4개가 이동 반대쪽으로 튀어 오른다
        private void SpawnSparks(float _footX, float _footY)
        {
            int count = 3 + (Rand01() < 0.5f ? 1 : 0);

            for (int i = 0; i < count && sparkCount < SparkCapacity; i++)
            {
                HasteSpark spark = new HasteSpark();
                spark.x = _footX;
                spark.y = _footY + 2.0f;
                spark.velocityX = -moveDirX * (26.0f + Rand01() * 34.0f) + (Rand01() - 0.5f) * 14.0f;
                spark.velocityY = -moveDirY * (10.0f + Rand01() * 14.0f) + 20.0f + Rand01() * 26.0f;
                spark.age = 0.0f;
                spark.life = 0.32f + Rand01() * 0.22f;
                sparks[sparkCount++] = spark;
            }
        }

        // 수명이 끝난 입자를 뒤 원소로 채워 지운다
        private void AgeParticles(float _dt)
        {
            for (int i = puffCount - 1; i >= 0; i--)
            {
                puffs[i].age += _dt;
                if (puffs[i].age >= puffs[i].life) puffs[i] = puffs[--puffCount];
            }

            for (int i = sparkCount - 1; i >= 0; i--)
            {
                sparks[i].age += _dt;
                if (sparks[i].age >= sparks[i].life) sparks[i] = sparks[--sparkCount];
            }
        }

        // 그리기

        private void RebuildMesh()
        {
            Vector3 pos = transform.position;
            centerX = Mathf.RoundToInt(pos.x * PixelsPerUnit);
            centerY = Mathf.RoundToInt(pos.y * PixelsPerUnit);

            frontPainter.Begin(frontBuffer, transform.worldToLocalMatrix, pos.z);
            PixelPainter backTarget = frontPainter;
            if (null != backMesh)
            {
                backPainter.Begin(backBuffer, backMeshRenderer.transform.worldToLocalMatrix, pos.z);
                backTarget = backPainter;
            }

            DrawPuffs(backTarget);
            DrawSparks(frontPainter);
            DrawBurstAir(frontPainter);
            DrawGlints(frontPainter);

            frontPainter.Commit(frontMesh, meshRenderer, ref frontHash, ref frontVertexCount);
            if (null != backMesh) backPainter.Commit(backMesh, backMeshRenderer, ref backHash, ref backVertexCount);
        }

        // 발동 - 입자가 나선으로 감아 오르고 가슴 높이에 ◆가 번쩍인다
        private void DrawBurstAir(PixelPainter _p)
        {
            if (burstTime < 0.0f || burstTime >= BurstLength) return;

            float t = burstTime;
            for (int i = 0; i < 14; i++)
            {
                float f = Mathf.Clamp01((t - i * 0.018f) / 0.55f);
                if (f <= 0.0f || f >= 1.0f) continue;

                float angle = TwoPi * i / 14.0f * 2.0f + t * 9.0f;
                float radius = (1.0f - f) * 20.0f + 4.0f;
                float height = f * 34.0f;
                int x = centerX + Mathf.FloorToInt(Mathf.Cos(angle) * radius);
                int y = centerY + Mathf.FloorToInt(height - Mathf.Sin(angle) * radius * 0.5f);
                int shape = f < 0.35f ? (0 == (i % 3) ? PixelPainter.ShapeDiamond : PixelPainter.ShapeCircle) : (f < 0.7f ? (0 != (i & 1) ? PixelPainter.ShapePlus : PixelPainter.ShapeCross) : PixelPainter.ShapeDot);
                _p.Shape(shape, x, y, Steps[f < 0.25f ? 0 : (f < 0.55f ? 1 : 2)]);
            }

            if (t < 0.12f) _p.Shape(PixelPainter.ShapeDiamond, centerX, centerY + 12, Steps[0]);
        }

        // 걸음 먼지 - ● -> + -> . 로 줄어들며 이동 반대쪽으로 밀린다
        private void DrawPuffs(PixelPainter _p)
        {
            for (int i = 0; i < puffCount; i++)
            {
                float f = puffs[i].age / puffs[i].life;
                float x = puffs[i].x + puffs[i].velocityX * puffs[i].age;
                float y = puffs[i].y + puffs[i].velocityY * puffs[i].age - 30.0f * puffs[i].age * puffs[i].age;
                int shape = f < 0.3f ? (true == puffs[i].bBig ? PixelPainter.ShapeCircle : PixelPainter.ShapePlus) : (f < 0.65f ? (true == puffs[i].bCross ? PixelPainter.ShapeCross : PixelPainter.ShapePlus) : PixelPainter.ShapeDot);
                _p.Shape(shape, Mathf.RoundToInt(x), Mathf.RoundToInt(y), Steps[f < 0.2f ? 0 : (f < 0.5f ? 1 : (f < 0.8f ? 2 : 3))]);
            }
        }

        // 발끝 스파크 - x -> + -> . 로 줄어들며 튀어 올랐다 중력으로 떨어진다
        private void DrawSparks(PixelPainter _p)
        {
            for (int i = 0; i < sparkCount; i++)
            {
                float f = sparks[i].age / sparks[i].life;
                float x = sparks[i].x + sparks[i].velocityX * sparks[i].age;
                float y = sparks[i].y + sparks[i].velocityY * sparks[i].age - SparkGravity * sparks[i].age * sparks[i].age;
                int shape = f < 0.3f ? PixelPainter.ShapeCross : (f < 0.65f ? PixelPainter.ShapePlus : PixelPainter.ShapeDot);
                _p.Shape(shape, Mathf.RoundToInt(x), Mathf.RoundToInt(y), Steps[f < 0.25f ? 0 : (f < 0.6f ? 1 : 2)]);
            }
        }

        // 멈춰 있어도 버프 중임을 알리는 몸 주변 반짝임
        private void DrawGlints(PixelPainter _p)
        {
            if (true == bEnding) return;

            for (int i = 0; i < 3; i++)
            {
                float cycle = elapsed * 0.9f + i * 0.37f;
                float phase = Mathf.Repeat(cycle, 1.0f);
                if (phase > 0.55f) continue;

                float angle = i * 2.1f + Mathf.Floor(cycle) * 1.7f;
                int x = centerX + Mathf.RoundToInt(Mathf.Cos(angle) * 9.0f);
                int y = centerY + Mathf.RoundToInt(4.0f + phase * 22.0f);
                _p.Shape(phase < 0.2f ? PixelPainter.ShapeDiamond : (phase < 0.4f ? PixelPainter.ShapePlus : PixelPainter.ShapeDot), x, y, Steps[phase < 0.25f ? 0 : 1]);
            }
        }

        private void ApplySorting()
        {
            int layerId;
            int order;
            if (null != sortingSource)
            {
                layerId = sortingSource.sortingLayerID;
                order = sortingSource.sortingOrder;
            }
            else if (null != rootParticleRenderer)
            {
                layerId = rootParticleRenderer.sortingLayerID;
                order = rootParticleRenderer.sortingOrder;
            }
            else
            {
                return;
            }

            // 소팅은 VFXComponent가 자식 렌더러 전체에 같은 값으로 덮어쓰므로, 매번 캐릭터 기준으로 다시 맞춘다(바뀔 때만 쓴다)
            if (layerId != appliedLayerId || order != appliedOrder)
            {
                appliedLayerId = layerId;
                appliedOrder = order;
                meshRenderer.sortingLayerID = layerId;
                meshRenderer.sortingOrder = order + 1;
                if (null != backMeshRenderer)
                {
                    backMeshRenderer.sortingLayerID = layerId;
                    backMeshRenderer.sortingOrder = order - 1;
                }
            }
        }

        // 렌더러를 끄고 루트 ParticleSystem을 멈춘다(VFXComponent가 풀에 반환한다)
        private void Finish()
        {
            bActive = false;
            if (null != meshRenderer && true == meshRenderer.enabled) meshRenderer.enabled = false;
            if (null != backMeshRenderer && true == backMeshRenderer.enabled) backMeshRenderer.enabled = false;

            if (null != rootParticleSystem && true == rootParticleSystem.isPlaying) rootParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            bActive = false;

            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != backMeshRenderer) backMeshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (false == bActive) return;

            float dt = Time.deltaTime;
            if (dt <= 0.0f) return;

            // 대상이 사라졌으면(풀 반환 등) 연출 없이 끝낸다
            if (null != target && false == target.gameObject.activeInHierarchy)
            {
                Finish();
                return;
            }

            if (null != target) transform.position = target.position;

            elapsed += dt;
            if (burstTime >= 0.0f)
            {
                burstTime += dt;
                if (burstTime >= BurstLength) burstTime = -1.0f;
            }

            UpdateMotion(dt);
            AgeParticles(dt);

            if (false == bEnding)
            {
                int stepNo = Mathf.FloorToInt(gait);
                if (stepNo != lastStep)
                {
                    lastStep = stepNo;
                    SpawnPuffs();
                }
            }
            else
            {
                endTimer += dt;
                if (EndDrainSeconds <= endTimer)
                {
                    Finish();
                    return;
                }
            }

            ApplySorting();

            float reach = 80.0f / PixelsPerUnit;
            cullingSize = new Vector3(reach * 2.0f + 2.0f * PixelVfxCulling.MarginUnits, reach * 2.0f + 2.0f * PixelVfxCulling.MarginUnits, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(transform.position, cullingSize))
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                if (null != backMeshRenderer && true == backMeshRenderer.enabled) backMeshRenderer.enabled = false;
                frontVertexCount = -1;
                backVertexCount = -1;
                return;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            bActive = false;
            target = null;
            sortingSource = null;

            if (null != meshRenderer) meshRenderer.enabled = false;
            if (null != backMeshRenderer) backMeshRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (null != frontMesh)
            {
                Destroy(frontMesh);
                frontMesh = null;
            }

            if (null != backMesh)
            {
                Destroy(backMesh);
                backMesh = null;
            }
        }
    }
}
