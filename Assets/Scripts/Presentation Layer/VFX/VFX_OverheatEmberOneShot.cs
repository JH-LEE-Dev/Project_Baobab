using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 4스테이지 불씨의 단발 연출. EmberItemController의 획득·소멸 태그로 VFXComponent 풀에서 재생된다(루트 ParticleSystem + 이 메쉬 자식).
    ///
    /// - Acquire(획득): 불씨가 캐릭터 발밑 피벗에 도착한 자리에서 재생된다. 몸통 높이(9px 위)에서
    ///   `x` `+` `.` 불길 조각 9개 + 꼬리 달린 불꽃 7개 + 돌던 방향으로 튕겨 나가는 불티 8개 + 늦게 천천히 떠오르는 잔불 3개.
    /// - Expire(소멸): 바닥 불씨가 0.6초 동안 식어 사라진다. 처음부터 타오르는 상태로 보이도록 불덩이를 미리 데운 뒤 열을 줄이고,
    ///   불티는 0.4초 안에 어두워지며 사라진다. 끝에 그을음 2개가 오른다.
    ///
    /// 링·충격파 같은 고리 모양은 쓰지 않는다. 입자는 `.` `+` `x`만 쓴다. 재생 중 힙 할당이 없다.
    /// 위치 기준: 이 오브젝트의 월드 위치가 불씨(또는 캐릭터 피벗) 위치다. 루트 ParticleSystem 수명이 끝나면 풀로 돌아간다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_OverheatEmberOneShot : MonoBehaviour, IPixelVfxDeferred
    {
        public enum EKind
        {
            Acquire = 0,
            Expire = 1,
        }

        [SerializeField] private EKind kind = EKind.Acquire;
        [SerializeField, Range(0.0f, 2.0f), Tooltip("정점 알파(발광 세기) 배율. 블룸이 과하거나 약할 때 조절한다.")]
        private float glowScale = 1.0f;
        [SerializeField, Tooltip("메쉬를 루트 렌더러(VFXComponent가 정한 소팅)보다 이 값만큼 위에 그린다.")]
        private int sortingOrderOffset = 0;

        private const float PixelsPerUnit = 32.0f;
        private const float MaxDeltaTime = 0.05f;
        private const float AcquireLength = 2.0f;
        private const float ExpireFadeLength = 0.6f;
        private const float ExpireLength = 1.4f;
        private const int AcquireBodyY = 9;
        private const int ExpireOrbY = 5;
        private const int MoteCount = 8;
        private const int ParticleCapacity = 48;
        private const int QuadBudget = 900;

        private static readonly int[] ChunkShapes = { PixelPainter.ShapeCross, PixelPainter.ShapePlus, PixelPainter.ShapeDot, PixelPainter.ShapeCross, PixelPainter.ShapePlus };

        // 쿼드를 모으는 스레드별 작업 버퍼. 픽셀 채우기가 일꾼 스레드에서 돌 수 있어(PixelVfxWorkQueue) 스레드마다 하나씩 둔다.
        // 한 번의 계산 묶음(PixelVfxWorkQueue.Serial) 동안 그 스레드가 맡은 인스턴스들의 그림을 구간으로 이어 담고, 묶음이 바뀌면 비운다.
        private sealed class Work
        {
            [System.ThreadStatic] private static Work current;

            public readonly PixelQuadBuffer buffer = new PixelQuadBuffer(QuadBudget);
            private int serial;
            private bool bUsed;

            public static Work ForCurrentThread()
            {
                Work work = current;
                if (null == work)
                {
                    work = new Work();
                    current = work;
                }

                int queueSerial = PixelVfxWorkQueue.Serial;
                if (false == work.bUsed || work.serial != queueSerial)
                {
                    work.bUsed = true;
                    work.serial = queueSerial;
                    work.buffer.Clear();
                }

                return work;
            }
        }

        // 내부 의존성
        private readonly OverheatEmberOrb orb = new OverheatEmberOrb();
        private readonly OverheatEmberParticles particles = new OverheatEmberParticles(ParticleCapacity);
        private readonly PixelPainter painter = new PixelPainter();
        private readonly Color32[] tones = new Color32[OverheatEmberOrb.ToneCount];
        private readonly Color32[] darks = new Color32[OverheatEmberOrb.DarkCount];
        private readonly float[] moteAngle = new float[MoteCount];
        private readonly float[] moteSpeed = new float[MoteCount];
        private readonly float[] moteRx = new float[MoteCount];
        private readonly float[] moteRy = new float[MoteCount];
        private Color32 smokeColor;
        private MeshFilter meshFilter;
        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ParticleSystemRenderer rootParticleRenderer;

        // 상태 변수
        private bool bInitialized;
        private bool bBeginPending;
        private bool bFirstFrame;
        private bool bSmokeDone;
        private float elapsed;
        private float phase;
        private int originX;
        private int originY;
        private uint randomState = 1u;
        private ulong meshHash;
        private int meshVertexCount = -1;
        private int appliedRootSortingLayerID = int.MinValue;
        private int appliedRootSortingOrder = int.MinValue;

        // 미룬 그리기(PixelVfxWorkQueue): LateUpdate가 담아 두는 입력과, ComputeDeferred가 남겨 ApplyDeferred가 메쉬에 올리는 결과
        private int deferredSlot = -1;            // 대기열 자리. -1이면 미뤄 둔 그리기가 없다
        private Matrix4x4 deferredWorldToLocal;   // LateUpdate에서 읽은 월드 -> 로컬 변환
        private float deferredMeshZ;
        private PixelPainter.Segment drawSegment;

        // 퍼블릭 초기화 및 제어 메서드

        /// <summary>종류별 전체 재생 시간(초). 루트 ParticleSystem 수명은 이 값 이상이어야 한다(빌더가 맞춘다).</summary>
        public static float DurationOf(EKind _kind)
        {
            return EKind.Acquire == _kind ? AcquireLength : ExpireLength;
        }

        // 일반 비즈니스 로직 및 내부 메서드

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<MeshRenderer>();
            rootParticleRenderer = null != transform.parent ? transform.parent.GetComponent<ParticleSystemRenderer>() : null;

            mesh = new Mesh { name = "VFX_OverheatEmberOneShot" };
            mesh.MarkDynamic();
            meshFilter.sharedMesh = mesh;
            PixelQuadBuffer.ReserveMesh(mesh, 400);

            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;

            BuildTones();
        }

        private void BuildTones()
        {
            OverheatEmberOrb.BuildPalette(tones, darks, glowScale);

            Color smoke = new Color32(0x5d, 0x4b, 0x49, 255);
            if (ColorSpace.Linear == QualitySettings.activeColorSpace) smoke = smoke.linear;
            smoke.a = 0.0f;
            smokeColor = smoke;
        }

        // 풀에서 꺼내져 활성화되면 위치가 정해질 시간을 주기 위해 시작을 첫 LateUpdate로 미룬다.
        private void Begin()
        {
            EnsureInitialized();

            Vector3 pos = transform.position;
            originX = Mathf.RoundToInt(pos.x * PixelsPerUnit);
            originY = Mathf.RoundToInt(pos.y * PixelsPerUnit);

            randomState = unchecked((uint)(Time.frameCount * 2654435761u) ^ (uint)Mathf.FloorToInt(pos.x * 97.0f) ^ ((uint)Mathf.FloorToInt(pos.y * 57.0f) << 7));
            if (0u == randomState) randomState = 1u;

            elapsed = 0.0f;
            bFirstFrame = true;
            bSmokeDone = false;
            phase = Rand01();
            meshVertexCount = -1;
            particles.Clear();

            if (EKind.Acquire == kind) SetupAcquire();
            else SetupExpire();
        }

        private float Rand01()
        {
            randomState ^= randomState << 13;
            randomState ^= randomState >> 17;
            randomState ^= randomState << 5;
            return (randomState & 0xFFFFFF) / 16777216.0f;
        }

        private float RandRange(float _min, float _max)
        {
            return _min + (_max - _min) * Rand01();
        }

        private void SetupAcquire()
        {
            float cx = originX;
            float cy = originY + AcquireBodyY;
            const float TwoPi = Mathf.PI * 2.0f;

            // 불길 조각 9개: 사방으로 터지며 떠오르고 식는다
            for (int k = 0; k < 9; k++)
            {
                float a = k / 9.0f * TwoPi + Rand01() * 0.3f;
                float speed = RandRange(20.0f, 36.0f);
                int shape = ChunkShapes[Mathf.Min(ChunkShapes.Length - 1, (int)(Rand01() * ChunkShapes.Length))];
                particles.SpawnChunk(cx + Mathf.Round(Mathf.Cos(a) * 3.0f), cy - Mathf.Round(Mathf.Sin(a) * 3.0f),
                    Mathf.Cos(a) * speed, -Mathf.Sin(a) * speed * 0.7f + 8.0f, RandRange(0.6f, 0.9f), shape, true, 3.5f, 45.0f, 0.0f, Rand01() * 6.28f);
            }

            // 꼬리 달린 불꽃 7개
            for (int k = 0; k < 7; k++)
            {
                float a = k / 7.0f * TwoPi + Rand01() * 0.3f;
                float speed = RandRange(30.0f, 48.0f);
                int shape = 0 == k % 3 ? PixelPainter.ShapePlus : (1 == k % 3 ? PixelPainter.ShapeCross : PixelPainter.ShapeDot);
                particles.SpawnSpark(cx, cy, Mathf.Cos(a) * speed, -Mathf.Sin(a) * speed * 0.6f + 10.0f, RandRange(0.45f, 0.7f), shape, true, 4.5f, 0.0f, 0.0f);
            }

            // 돌던 불티 8개: 궤도 위 자리에서 돌던 방향(접선) + 바깥쪽으로 튕겨 나간다
            for (int k = 0; k < MoteCount; k++)
            {
                float a = k / (float)MoteCount * TwoPi + Rand01() * 0.4f;
                float rx = RandRange(6.0f, 9.0f);
                float ry = RandRange(2.3f, 3.5f);
                float ox = Mathf.Cos(a) * rx;
                float oy = Mathf.Sin(a) * ry;
                float tx = -Mathf.Sin(a) * rx + ox;
                float ty = Mathf.Cos(a) * ry + oy;
                float length = Mathf.Sqrt(tx * tx + ty * ty);
                if (0.0f == length) length = 1.0f;

                float speed = RandRange(38.0f, 52.0f);
                particles.SpawnSpark(cx + Mathf.Round(ox), cy - Mathf.Round(oy), tx / length * speed, -(ty / length * speed - 6.0f), 0.55f,
                    PixelPainter.ShapeDot, true, 3.0f, 0.0f, 0.0f);
            }

            // 잔불 3개: 조금 늦게 천천히 떠오른다
            for (int k = 0; k < 3; k++)
            {
                particles.SpawnSpark(cx + Mathf.Round(RandRange(-4.0f, 4.0f)), cy + Mathf.Round(RandRange(-2.0f, 2.0f)), RandRange(-2.5f, 2.5f),
                    RandRange(9.0f, 18.0f), RandRange(1.0f, 1.5f), PixelPainter.ShapeDot, false, 0.0f, RandRange(0.15f, 0.4f), Rand01() * 6.28f);
            }
        }

        private void SetupExpire()
        {
            orb.Reset(randomState ^ 0x9E3779B9u);
            orb.Prewarm(30, 0.0f, phase);

            for (int k = 0; k < MoteCount; k++)
            {
                moteAngle[k] = k / (float)MoteCount * Mathf.PI * 2.0f + Rand01() * 0.4f;
                moteSpeed[k] = RandRange(1.5f, 2.4f);
                moteRx[k] = RandRange(4.0f, 6.5f) * 1.45f;
                moteRy[k] = RandRange(1.6f, 2.4f) * 1.45f;
            }
        }

        private void Step(float _dt)
        {
            elapsed += _dt;

            if (EKind.Expire == kind)
            {
                float fade = Mathf.Max(0.0f, 1.0f - elapsed / ExpireFadeLength);
                orb.Step(_dt, 1.0f + elapsed, phase, fade, 0.0f);

                for (int k = 0; k < MoteCount; k++) moteAngle[k] += moteSpeed[k] * _dt;

                if (false == bSmokeDone && ExpireFadeLength <= elapsed)
                {
                    bSmokeDone = true;
                    float cy = originY + ExpireOrbY;
                    particles.SpawnSmoke(originX - 1, cy, RandRange(6.0f, 7.0f), 0.7f, 0.0f);
                    particles.SpawnSmoke(originX + 1, cy, RandRange(8.0f, 9.0f), 0.7f, 2.0f);
                }
            }

            particles.Step(_dt, elapsed);
        }

        // 메쉬를 이번 프레임에 다시 그리도록 대기열에 넣는다. 픽셀 채우기는 미뤄서 일꾼 스레드에서 돌리고(ComputeDeferred),
        // 그리기가 읽을 변환은 예전과 같은 시점인 여기서 읽어 둔다.
        private void RebuildMesh()
        {
            deferredWorldToLocal = transform.worldToLocalMatrix;
            deferredMeshZ = transform.position.z;
            deferredSlot = PixelVfxWorkQueue.Enqueue(this);
        }

        // 미뤄 둔 그리기: 작업 버퍼의 새 구간에 그린다(일꾼 스레드에서 불릴 수 있다). 예전 RebuildMesh에서 메쉬에 올리는 부분만 뺀 것이고 순서는 같다.
        void IPixelVfxDeferred.ComputeDeferred()
        {
            Work work = Work.ForCurrentThread();
            painter.BeginSegment(work.buffer, deferredWorldToLocal, deferredMeshZ);

            if (EKind.Expire == kind && elapsed < ExpireFadeLength)
            {
                float fade = 1.0f - elapsed / ExpireFadeLength;
                int cy = originY + ExpireOrbY;

                DrawExpireMotes(cy, fade, false);
                orb.Draw(painter, originX, cy, 1.0f + elapsed, phase, fade, OverheatEmberOrb.Pulse(elapsed, phase), 0.0f, 1.0f, tones, darks);
                DrawExpireMotes(cy, fade, true);
            }

            particles.Draw(painter, tones, darks, smokeColor);
            painter.EndSegment(out drawSegment);
        }

        // 미뤄 둔 그리기의 결과를 메쉬에 올린다(주 스레드)
        void IPixelVfxDeferred.ApplyDeferred()
        {
            deferredSlot = -1;
            PixelPainter.CommitSegment(in drawSegment, mesh, meshRenderer, ref meshHash, ref meshVertexCount);
            drawSegment.buffer = null;
        }

        // 미뤄 둔 그리기가 있으면 지금 끝낸다(꺼지거나 색이 바뀌기 전에: 예전에는 LateUpdate에서 이미 끝나 있던 일이다)
        private void CompleteDeferred()
        {
            if (0 <= deferredSlot) PixelVfxWorkQueue.CompleteNow(deferredSlot, this);
        }

        // 소멸 중 불티: 궤도를 돌며 어두워지다 0.4초(fade 0.33)에 사라진다
        private void DrawExpireMotes(int _centerY, float _fade, bool _bFront)
        {
            if (0.33f > _fade) return;

            for (int k = 0; k < MoteCount; k++)
            {
                float d = Mathf.Sin(moteAngle[k]);
                if ((0.0f < d) != _bFront) continue;

                int x = originX + Mathf.RoundToInt(Mathf.Cos(moteAngle[k]) * moteRx[k]);
                int y = _centerY - Mathf.RoundToInt(d * moteRy[k]);
                Color32 color = true == _bFront ? (0.66f < _fade ? tones[3] : tones[2]) : tones[1];
                painter.Dot(x, y, color);
            }
        }

        // 유니티 이벤트 함수

        private void Awake()
        {
            EnsureInitialized();
        }

        private void OnEnable()
        {
            appliedRootSortingLayerID = int.MinValue;
            appliedRootSortingOrder = int.MinValue;
            bBeginPending = true;
            elapsed = 0.0f;

            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        private void LateUpdate()
        {
            if (true == bBeginPending)
            {
                bBeginPending = false;
                Begin();
            }

            if (DurationOf(kind) <= elapsed)
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                return;
            }

            // 첫 프레임은 시간을 더하지 않아서 t=0 장면이 반드시 한 번 그려진다
            float dt = true == bFirstFrame ? 0.0f : Mathf.Min(Time.deltaTime, MaxDeltaTime);
            bFirstFrame = false;
            Step(dt);

            // 소팅은 VFXComponent가 자식 렌더러 전체에 같은 값으로 덮어쓰므로, 루트 렌더러 기준으로 다시 맞춘다
            if (null != rootParticleRenderer)
            {
                int rootLayer = rootParticleRenderer.sortingLayerID;
                int rootOrder = rootParticleRenderer.sortingOrder;
                if (rootLayer != appliedRootSortingLayerID || rootOrder != appliedRootSortingOrder)
                {
                    appliedRootSortingLayerID = rootLayer;
                    appliedRootSortingOrder = rootOrder;
                    meshRenderer.sortingLayerID = rootLayer;
                    meshRenderer.sortingOrder = rootOrder + sortingOrderOffset;
                }
            }

            // 화면 밖이면 메쉬 재구성을 건너뛰고 시간만 흘린다
            float margin = 2.0f * PixelVfxCulling.MarginUnits;
            Vector3 size = new Vector3(96.0f / PixelsPerUnit + margin, 96.0f / PixelsPerUnit + margin, PixelVfxCulling.BoundsDepth);
            if (false == PixelVfxCulling.IsVisible(transform.position + new Vector3(0.0f, 12.0f / PixelsPerUnit, 0.0f), size))
            {
                if (true == meshRenderer.enabled) meshRenderer.enabled = false;
                meshVertexCount = -1;
                return;
            }

            RebuildMesh();
        }

        private void OnDisable()
        {
            CompleteDeferred();
            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (null != mesh)
            {
                Destroy(mesh);
                mesh = null;
            }
        }

        private void OnValidate()
        {
            glowScale = Mathf.Clamp(glowScale, 0.0f, 2.0f);

#if UNITY_EDITOR
            if (true == bInitialized)
            {
                CompleteDeferred();
                BuildTones();
            }
#endif
        }
    }
}
