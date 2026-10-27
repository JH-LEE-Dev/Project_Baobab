using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 모닥불 휴식 범위(타원) 안에서 스멀스멀 올라오는 `+` 입자. 장판 셰이더(RestAreaEllipse)는 Indicators 레이어에서 사물 밑에 깔리고,
    /// 입자는 한 알마다 MeshRenderer를 따로 두어 Objects 레이어에서 지면 Y 기준으로 정렬한다.
    /// 그래야 나무, 오프로드 같은 사물과 사물 사이에 끼어들 수 있다(메쉬 하나나 ParticleSystem으로는 입자별 정렬이 안 된다).
    /// 모양은 다른 픽셀 이펙트(PixelPainter, PixelQuadBuffer)와 같은 방식이다. 텍스처 없이 정수 픽셀 좌표 사각형 쿼드에 정점 색만 담아서
    /// 화면 배율이나 카메라 위상과 관계없이 팔 길이가 좌우, 상하로 똑같이 그려진다. 색 단계마다 메쉬 하나를 만들어 모든 입자가 나눠 쓴다.
    /// 입자는 동시에 최대 15개 안팎이라 렌더러를 미리 만들어 두고 켜고 끄기만 한다. 할당은 없다.
    /// </summary>
    public class VFX_RestAreaPlus : MonoBehaviour
    {
        private const float PixelsPerUnit = 32f;
        private const int ArmLength = 1;
        private const int ShapeDot = 0;
        private const int ShapePlus = 1;
        private const int ShapeCount = 2;
        private const float SpawnPerSecond = 2.7f;
        private const float MinSpawnRadius = 0.2f;
        private const float MaxSpawnRadius = 0.86f;
        private const float LiftUnits = 0.1875f;
        private const float SwayUnits = 0.16f;
        private const float SwaySpeed = 2f;
        private const float MinRiseSpeed = 0.22f;
        private const float MaxRiseSpeed = 0.34f;
        // 수명의 앞뒤 이 비율만큼은 점(.)으로 그려서 `+`가 점에서 자라나고 점으로 줄어들며 사라진다.
        private const float DotLifeRatio = 0.12f;
        // 밝은 구간에서 0.9초마다 0.1초씩 한 단계 더 밝게 번쩍인다.
        private const float TwinkleRate = 10f;
        private const int TwinklePeriod = 9;
        private const float MinLife = 3.2f;
        private const float MaxLife = 4.8f;
        private const float ClosingTimeScale = 4f;
        private const float CullingMargin = 1f;

        // 입자 색 4단계. 수명에 따라 연한 녹색 -> 밝은 연두 -> 연한 녹색으로 식고, 마지막 단계는 밝은 구간의 번쩍임에만 쓴다.
        // 회복을 떠올리게 하는 밝은 연두라 청록 바닥에서 눈에 잘 띈다.
        private static readonly Color32[] StepColors =
        {
            new Color32(0x6F, 0xD6, 0x72, 0xFF),
            new Color32(0xA6, 0xF2, 0x7A, 0xFF),
            new Color32(0xD4, 0xFF, 0x9A, 0xFF),
            new Color32(0xF0, 0xFF, 0xC8, 0xFF)
        };
        private const int TwinkleStep = 3;

        private struct Plus
        {
            public bool bAlive;
            public float x;
            public float groundY;
            public float swayPhase;
            public float riseSpeed;
            public float age;
            public float life;
        }

        // 외부 의존성
        [Header("Render")]
        [Tooltip("정점 색 Unlit 머티리얼(BrandWrapGlow 셰이더, 발광 배율 1). 비우면 입자가 그려지지 않는다.")]
        [SerializeField] private Material plusMaterial;
        [SerializeField] private string sortingLayerName = "Objects";
        [Tooltip("같은 지면 Y에 선 사물보다 입자를 한 칸 앞에 그린다.")]
        [SerializeField] private int sortingOrderOffset = 1;

        [Header("Particle")]
        [SerializeField] private int maxCount = 15;

        [Header("Range")]
        [Tooltip("범위 타원 반지름(월드 유닛). RestAreaVisual.Initialize가 덮어쓴다.")]
        [SerializeField] private float radiusX = 3f;
        [SerializeField] private float radiusY = 1.5f;

        private CustomSortable sortable;

        // 내부 의존성
        private Plus[] plusArray;
        private MeshRenderer[] plusRenderers;
        private MeshFilter[] plusFilters;
        private Transform[] plusTransforms;
        private Vector3[] lastPositions;
        private int[] lastSortingOrders;
        private int[] lastMeshIndices;
        private Mesh[] shapeMeshes;
        private int sortingLayerId;

        // 상태 변수
        private bool bOpen = false;
        private bool bAllHidden = true;

        public void Initialize(CustomSortable _sortable)
        {
            sortable = _sortable;
        }

        public void SetRadius(float _radiusX, float _radiusY)
        {
            radiusX = _radiusX;
            radiusY = _radiusY;
        }

        /// <summary>열리면 입자가 생기기 시작하고, 닫히면 새로 생기지 않으며 남은 입자는 빠르게 사그라든다.</summary>
        public void SetOpen(bool _bOpen)
        {
            bOpen = _bOpen;
        }

        private void BuildRenderers()
        {
            // 점(.)과 3x3 `+`(팔 길이 1px, 굵기 1px)를 색 단계마다 한 메쉬로 만든다. 가운데 픽셀의 왼쪽 아래 모서리가 원점이고,
            // `+`는 가로 줄 [-1,2) x [0,1)과 그 위, 아래 세로 팔 [0,1) x [1,2), [0,1) x [-1,0)이 서로 겹치지 않는 쿼드 3개다.
            // 메쉬 번호는 모양 * 색 단계 수 + 색 단계다.
            PixelQuadBuffer buffer = new PixelQuadBuffer(3);
            shapeMeshes = new Mesh[ShapeCount * StepColors.Length];
            for (int shape = 0; shape < ShapeCount; shape++)
            {
                for (int s = 0; s < StepColors.Length; s++)
                {
                    Color32 color = ToVertexColor(StepColors[s]);
                    buffer.Clear();
                    if (ShapePlus == shape)
                    {
                        buffer.AddLocalRect(-ArmLength, 0, ArmLength + 1, 1, color);
                        buffer.AddLocalRect(0, 1, 1, ArmLength + 1, color);
                        buffer.AddLocalRect(0, -ArmLength, 1, 0, color);
                    }
                    else
                    {
                        buffer.AddLocalRect(0, 0, 1, 1, color);
                    }

                    Mesh mesh = new Mesh();
                    mesh.name = "RestAreaPlus_" + shape + "_" + s;
                    buffer.Upload(mesh, new Vector3(1f, 1f, 1f));
                    shapeMeshes[shape * StepColors.Length + s] = mesh;
                }
            }

            sortingLayerId = SortingLayer.NameToID(sortingLayerName);

            int count = Mathf.Max(1, maxCount);
            plusArray = new Plus[count];
            plusRenderers = new MeshRenderer[count];
            plusFilters = new MeshFilter[count];
            plusTransforms = new Transform[count];
            lastPositions = new Vector3[count];
            lastSortingOrders = new int[count];
            lastMeshIndices = new int[count];

            for (int i = 0; i < count; i++)
            {
                GameObject go = new GameObject("Plus");
                go.transform.SetParent(transform, false);

                MeshFilter mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = shapeMeshes[0];

                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sortingLayerID = sortingLayerId;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                if (null != plusMaterial) mr.sharedMaterial = plusMaterial;
                mr.enabled = false;

                plusRenderers[i] = mr;
                plusFilters[i] = mf;
                plusTransforms[i] = go.transform;
                lastSortingOrders[i] = int.MinValue;
                lastMeshIndices[i] = -1;
            }
        }

        private void Spawn(Vector3 _center)
        {
            for (int i = 0; i < plusArray.Length; i++)
            {
                if (true == plusArray[i].bAlive) continue;

                float r = Mathf.Lerp(MinSpawnRadius, MaxSpawnRadius, Mathf.Sqrt(Random.value));
                float angle = Random.value * Mathf.PI * 2f;

                plusArray[i].bAlive = true;
                plusArray[i].x = _center.x + Mathf.Cos(angle) * r * radiusX;
                plusArray[i].groundY = _center.y + Mathf.Sin(angle) * r * radiusY;
                plusArray[i].swayPhase = Random.value * Mathf.PI * 2f;
                plusArray[i].riseSpeed = Random.Range(MinRiseSpeed, MaxRiseSpeed);
                plusArray[i].age = 0f;
                plusArray[i].life = Random.Range(MinLife, MaxLife);
                return;
            }
        }

        // 정점 색은 셰이더가 그대로 출력하므로, 선형 색 공간에서는 sRGB 목표 색을 선형으로 바꿔 넣어야 화면에서 같은 색으로 보인다.
        private static Color32 ToVertexColor(Color32 _color)
        {
            Color color = _color;
            if (ColorSpace.Linear == QualitySettings.activeColorSpace) color = color.linear;
            return color;
        }

        // 정수 픽셀 격자의 왼쪽 아래 모서리. 메쉬 원점이 가운데 픽셀의 왼쪽 아래 모서리라서 쿼드 정점이 항상 픽셀 경계에 놓인다.
        private static float Snap(float _value)
        {
            return Mathf.Floor(_value * PixelsPerUnit) / PixelsPerUnit;
        }

        private static int ColorStepOf(float _u)
        {
            if (_u < 0.15f) return 0;
            if (_u < 0.35f) return 1;
            if (_u < 0.65f) return 2;
            if (_u < 0.85f) return 1;
            return 0;
        }

        private void Render(int _index, float _z)
        {
            ref Plus plus = ref plusArray[_index];

            float u = plus.age / plus.life;
            float sway = Mathf.Sin(plus.age * SwaySpeed + plus.swayPhase) * SwayUnits;
            // 처음엔 빠르게 솟았다가 갈수록 느려진다(시작 속도 1.5배, 끝 속도 0.5배). 수명 동안 오르는 총 높이는 riseSpeed * life다.
            float rise = plus.riseSpeed * plus.life * (u * (1.5f - 0.5f * u));
            float worldX = Snap(plus.x + sway);
            float worldY = Snap(plus.groundY + LiftUnits + rise);

            // 이전 프레임과 같은 픽셀이면 트랜스폼을 다시 쓰지 않는다.
            Vector3 last = lastPositions[_index];
            if (worldX != last.x || worldY != last.y || _z != last.z)
            {
                Vector3 position = new Vector3(worldX, worldY, _z);
                plusTransforms[_index].position = position;
                lastPositions[_index] = position;
            }

            int order = (null != sortable ? sortable.ComputeSortingOrder(plus.groundY) : 0) + sortingOrderOffset;
            if (order != lastSortingOrders[_index])
            {
                plusRenderers[_index].sortingOrder = order;
                lastSortingOrders[_index] = order;
            }

            int step = ColorStepOf(u);
            if (2 == step && 0 == Mathf.FloorToInt(plus.age * TwinkleRate + plus.swayPhase * 3f) % TwinklePeriod) step = TwinkleStep;

            int shape = (u < DotLifeRatio || 1f - DotLifeRatio < u) ? ShapeDot : ShapePlus;
            int meshIndex = shape * StepColors.Length + step;
            if (meshIndex != lastMeshIndices[_index])
            {
                plusFilters[_index].sharedMesh = shapeMeshes[meshIndex];
                lastMeshIndices[_index] = meshIndex;
            }

            if (false == plusRenderers[_index].enabled) plusRenderers[_index].enabled = true;
        }

        private void HideAllRenderers()
        {
            if (true == bAllHidden) return;

            for (int i = 0; i < plusRenderers.Length; i++)
            {
                if (true == plusRenderers[i].enabled) plusRenderers[i].enabled = false;
            }
            bAllHidden = true;
        }

        private void ClearAll()
        {
            for (int i = 0; i < plusArray.Length; i++)
            {
                plusArray[i].bAlive = false;
            }
            HideAllRenderers();
        }

        private void Awake()
        {
            BuildRenderers();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float timeScale = true == bOpen ? 1f : ClosingTimeScale;
            Vector3 center = transform.position;

            Vector3 cullSize = new Vector3(radiusX * 2f + CullingMargin * 2f, radiusY * 2f + CullingMargin * 2f, PixelVfxCulling.BoundsDepth);
            bool bVisible = PixelVfxCulling.IsVisible(center, cullSize);

            int aliveCount = 0;
            for (int i = 0; i < plusArray.Length; i++)
            {
                if (false == plusArray[i].bAlive) continue;

                plusArray[i].age += dt * timeScale;
                if (plusArray[i].age >= plusArray[i].life)
                {
                    plusArray[i].bAlive = false;
                    if (true == plusRenderers[i].enabled) plusRenderers[i].enabled = false;
                    continue;
                }

                aliveCount++;
                if (true == bVisible) Render(i, center.z);
            }

            if (false == bVisible)
            {
                HideAllRenderers();
                return;
            }

            bAllHidden = (0 == aliveCount);

            if (true == bOpen && aliveCount < plusArray.Length && Random.value < SpawnPerSecond * dt)
            {
                Spawn(center);
            }
        }

        private void OnDisable()
        {
            if (null != plusArray) ClearAll();
        }

        private void OnDestroy()
        {
            if (null == shapeMeshes) return;

            for (int i = 0; i < shapeMeshes.Length; i++)
            {
                if (null != shapeMeshes[i]) Destroy(shapeMeshes[i]);
            }
        }
    }
}
