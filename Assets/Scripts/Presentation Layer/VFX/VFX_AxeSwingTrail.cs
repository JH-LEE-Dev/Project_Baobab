using UnityEngine;
using UnityEngine.Rendering;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 도끼를 휘두를 때 도끼날 바깥을 따라 남는 반달 궤적. 나무를 맞혔는지와 상관없이(허공 스윙 포함) 스윙마다 나와서
    /// "지금 휘둘렀다"를 알려 준다. 32 PPU 픽셀 격자에 스냅한 절차적 쿼드 메쉬로 그린다(VFX_CriticalImpact와 같은 방식, BrandWrapGlow 셰이더).
    ///
    /// 장면(약 0.15초)
    /// - 휘두르는 동안(0.05초): 도끼날 끝이 지나간 호를 따라 궤적 머리가 함께 뻗는다. 궤적은 도끼날 끝보다 1px 바깥이라 도끼 그림을 거의 덮지 않는다.
    /// - 휘두르기가 끝나면 머리는 끝 각도에 남고, 꼬리가 처음엔 천천히·나중엔 빠르게 머리를 따라잡으며 사라진다(도끼가 돌아오는 동안).
    /// - 굵기는 머리 3px -> 2px -> 꼬리 1px, 색은 머리 흰색 -> 연한 흰 회색 -> 꼬리는 체커로 점이 빠진다. 발광은 1배라 블룸이 생기지 않는다.
    ///
    /// 각도는 도끼의 실제 회전(트윈이 돌린 Axe 로컬 회전)을 그대로 읽어 팔(Arm) 좌표계에서 그린다. 팔 좌우 반전·조준 회전을 따라가고, 도끼가 멈추면 같이 멈춘다.
    /// 그림일 뿐이라 판정·타이밍과 무관하다. 고정 크기 계산만 해서 재생 중 힙 할당이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFX_AxeSwingTrail : MonoBehaviour, IPixelVfxDeferred
    {
        // //공용 상수

        private const float PixelsPerUnit = 32f;
        private const string ShaderName = "Custom/VFX/BrandWrapGlow";

        // 도끼 스프라이트(Arm_Axe_D, 32x32, 중심 피벗)를 쉬는 자세로 둔 팔 좌표계에서 잰 도끼날 끝(가장 바깥 날 픽셀)의 각도와 반지름.
        // 날 픽셀은 반지름 7.1~11.1px, 각도 -45~-13도에 있고 가장 바깥 끝이 (10.5, -3.5)px(-18.4도, 11.1px)다.
        private const float BladeTipAngleDeg = -18.4f;
        // 궤적 바깥 반지름(px). 날 끝보다 약 1.4px 바깥이라 굵기 3px이어도 도끼 머리(반지름 10px 안쪽)와 거의 겹치지 않는다.
        private const float TrailOuterRadiusPx = 12.5f;
        private const int TrailMaxWidthPx = 3;

        // 시간
        private const float TailCatchUpDuration = 0.09f; // 휘두르기가 끝난 뒤 꼬리가 머리를 따라잡는 시간
        private const float HeadDoneProgress = 0.999f;
        private const float MaxLifetime = 0.4f;          // 트윈이 끊겨 머리가 끝까지 못 가도 이 시간 뒤에는 끝낸다

        // 팔레트(화면에 보일 sRGB). 리니어 색 공간이면 EnsureShared에서 한 번 변환한다.
        private static readonly Color32 SrgbHead = new Color32(255, 255, 255, 0);
        private static readonly Color32 SrgbBody = new Color32(228, 236, 244, 0);
        private static readonly Color32 SrgbTail = new Color32(198, 212, 226, 0);
        private static Color32 ColorHead;
        private static Color32 ColorBody;
        private static Color32 ColorTail;
        private static bool bPaletteReady;

        // //공유 자원

        // 스레드별 작업 버퍼(VFX_CriticalImpact와 같은 구조). 픽셀 채우기가 일꾼 스레드에서 돌 수 있다(PixelVfxWorkQueue).
        private sealed class Work
        {
            [System.ThreadStatic] private static Work current;

            public readonly PixelQuadBuffer buffer = new PixelQuadBuffer(256);
            public readonly PixelPainter painter = new PixelPainter();
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

        private static Material sharedMaterial;
        private static bool bShaderMissingWarned;

        // //인스턴스 상태

        private MeshRenderer meshRenderer;
        private Mesh mesh;
        private ulong meshHash;
        private int meshVertexCount = -1;

        private Transform axeTransform;
        private Transform armTransform;
        private SpriteRenderer axeRenderer;
        private int sortOffset;          // 도끼 정렬 순서에 더할 값(도끼가 몸 앞이면 +1, 뒤면 -1)

        private bool bPlaying;
        private float age;
        private float swingRotationDeg;  // 이번 스윙이 돌리는 각도(AxeAnimation.endRotationZ, 음수면 시계 방향)
        private float startAxeZ;         // 스윙을 시작할 때의 도끼 로컬 회전(도)
        private float headProgress;      // 궤적 머리: 스윙 진행도(0~1)의 최댓값
        private float headDoneAge = -1f; // 머리가 끝에 닿은 시각. 음수면 아직

        // 미룬 그리기(PixelVfxWorkQueue): LateUpdate가 담아 두는 입력과, ComputeDeferred가 남겨 ApplyDeferred가 메쉬에 올리는 결과
        private PixelPainter painter;
        private int deferredSlot = -1;
        private Matrix4x4 deferredWorldToLocal;
        private PixelPainter.Segment drawSegment;
        private int pivotPxX;            // 도끼 회전 중심(월드 px, 정수로 스냅)
        private int pivotPxY;
        private Vector2 worldToArmX;     // 월드 방향 -> 팔 좌표계 방향(회전·좌우 반전만, 단위 px)
        private Vector2 worldToArmY;
        private float drawTail;          // 그릴 구간(진행도) [drawTail, drawHead]
        private float drawHead;

        public bool IsPlaying => bPlaying;

        // //퍼블릭

        /// <summary>
        /// _parent 아래에 궤적을 하나 만든다. 셰이더를 찾지 못하면 null(궤적 없이 진행).
        /// _parent는 회전·스케일이 없어야 하고(픽셀 격자), 캐릭터 SortingGroup 안에 두어야 몸·도끼와 함께 정렬된다.
        /// </summary>
        public static VFX_AxeSwingTrail Create(Transform _parent)
        {
            if (false == EnsureShared()) return null;

            GameObject go = new GameObject("AxeSwingTrailVFX");
            go.transform.SetParent(_parent, false);
            VFX_AxeSwingTrail vfx = go.AddComponent<VFX_AxeSwingTrail>();
            vfx.Initialize();
            return vfx;
        }

        /// <summary>
        /// 스윙 하나를 그리기 시작한다. 도끼 회전 트윈을 건 직후(같은 프레임)에 부른다.
        /// _axe: 회전하는 도끼(Axe), _arm: 그 부모 팔(Arm), _swingRotationDeg: 스윙이 더하는 로컬 회전(도),
        /// _sortOffset: 도끼 정렬 순서에 더할 값(도끼가 몸 앞이면 +1, 뒤면 -1).
        /// </summary>
        public void Play(Transform _axe, Transform _arm, SpriteRenderer _axeRenderer, float _swingRotationDeg, int _sortOffset)
        {
            CompleteDeferred();

            if (null == _axe || null == _arm || 0f == _swingRotationDeg)
            {
                StopImmediate();
                return;
            }

            axeTransform = _axe;
            armTransform = _arm;
            axeRenderer = _axeRenderer;
            swingRotationDeg = _swingRotationDeg;
            sortOffset = _sortOffset;
            startAxeZ = _axe.localEulerAngles.z;
            headProgress = 0f;
            headDoneAge = -1f;
            age = 0f;
            bPlaying = true;
            meshHash = 0UL;
            meshVertexCount = -1;
            meshRenderer.enabled = false;

            // 쉬는 동안 꺼 둔 LateUpdate를 다시 켠다.
            enabled = true;
        }

        /// <summary>스윙 도중 도끼가 몸 앞/뒤로 바뀌면 정렬 기준을 따라 바꾼다(도끼가 몸 앞이면 +1, 뒤면 -1).</summary>
        public void SetSortOffset(int _sortOffset)
        {
            sortOffset = _sortOffset;
        }

        /// <summary>재생 중인 것을 바로 끝낸다(스윙 취소·무기 꺼짐·회전 베기 등).</summary>
        public void StopImmediate()
        {
            StopInternal();
            if (enabled) enabled = false;
        }

        private void StopInternal()
        {
            CompleteDeferred();

            bPlaying = false;
            axeTransform = null;
            armTransform = null;
            axeRenderer = null;
            if (null != meshRenderer) meshRenderer.enabled = false;
        }

        // //초기화

        private static bool EnsureShared()
        {
            if (false == bPaletteReady) BuildPalette();
            if (null != sharedMaterial) return true;

            Shader shader = Shader.Find(ShaderName);
            if (null == shader)
            {
                if (false == bShaderMissingWarned)
                {
                    bShaderMissingWarned = true;
                    Debug.LogWarning("[VFX_AxeSwingTrail] 셰이더 " + ShaderName + "를 찾지 못해 도끼 궤적을 건너뜁니다.");
                }
                return false;
            }

            // 정점 알파 0~1 -> 발광 배율 1~1. 궤적은 블룸 없이 또렷하게만 보인다(충격파·치명타의 HDR 발광과 겹치지 않게).
            sharedMaterial = new Material(shader) { name = "M_AxeSwingTrail (Runtime)" };
            sharedMaterial.SetFloat("_GlowMin", 1f);
            sharedMaterial.SetFloat("_GlowMax", 1f);
            return true;
        }

        private void Initialize()
        {
            mesh = new Mesh { name = "VFX_AxeSwingTrail" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;

            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = sharedMaterial;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            meshRenderer.enabled = false;

            // 재생(Play) 전까지 LateUpdate를 돌리지 않는다.
            enabled = false;
        }

        private static void BuildPalette()
        {
            bool bLinear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            ColorHead = ToDisplay(SrgbHead, bLinear);
            ColorBody = ToDisplay(SrgbBody, bLinear);
            ColorTail = ToDisplay(SrgbTail, bLinear);
            bPaletteReady = true;
        }

        private static Color32 ToDisplay(Color32 _srgb, bool _bLinear)
        {
            if (false == _bLinear) return _srgb;
            return new Color32(ToLinearByte(_srgb.r), ToLinearByte(_srgb.g), ToLinearByte(_srgb.b), _srgb.a);
        }

        private static byte ToLinearByte(byte _value)
        {
            return (byte)Mathf.RoundToInt(Mathf.GammaToLinearSpace(_value / 255f) * 255f);
        }

        // //매 프레임

        private void LateUpdate()
        {
            if (false == bPlaying) return;

            if (null == axeTransform || null == armTransform || false == axeTransform.gameObject.activeInHierarchy)
            {
                StopImmediate();
                return;
            }

            age += Time.deltaTime;

            // 스윙 진행도: 시작 회전에서 지금까지 돈 각도 / 스윙 각도. 돌아오는 동안 줄어들지만 머리는 최댓값에 남는다.
            float turned = Mathf.DeltaAngle(startAxeZ, axeTransform.localEulerAngles.z);
            float progress = Mathf.Clamp01(turned / swingRotationDeg);
            if (progress > headProgress) headProgress = progress;
            if (headDoneAge < 0f && headProgress >= HeadDoneProgress) headDoneAge = age;

            // 도끼가 이미 돌아오기 시작했다면(복귀 트윈은 스윙이 끝까지 간 뒤에만 걸린다) 끝 각도를 보는 프레임을 놓친 것이다
            // (프레임 타이밍·저부하 30fps). 머리를 끝까지 채우고 꼬리를 출발시킨다.
            if (headDoneAge < 0f && 0.5f < headProgress && progress < headProgress - 0.01f)
            {
                headProgress = 1f;
                headDoneAge = age;
            }

            float tail = 0f;
            if (0f <= headDoneAge)
            {
                // 처음엔 천천히, 나중엔 빠르게 따라잡는다(머리 쪽이 조금 더 오래 남는다).
                float k = Mathf.Clamp01((age - headDoneAge) / TailCatchUpDuration);
                tail = headProgress * k * k;
            }

            if (age >= MaxLifetime || (0f <= headDoneAge && tail >= headProgress - 0.0001f))
            {
                StopImmediate();
                return;
            }

            // 팔 좌표계: 도끼 회전 중심을 정수 픽셀에 스냅하고, 월드 방향을 팔 좌표계 방향으로 바꾸는 회전·반전만 쓴다
            // (캐릭터가 소수 픽셀만큼 움직여도 궤적 모양이 흔들리지 않게).
            Vector3 pivotWorld = axeTransform.position;
            pivotPxX = Mathf.RoundToInt(pivotWorld.x * PixelsPerUnit);
            pivotPxY = Mathf.RoundToInt(pivotWorld.y * PixelsPerUnit);
            Matrix4x4 armWorldToLocal = armTransform.worldToLocalMatrix;
            worldToArmX = new Vector2(armWorldToLocal.m00, armWorldToLocal.m01);
            worldToArmY = new Vector2(armWorldToLocal.m10, armWorldToLocal.m11);
            drawTail = tail;
            drawHead = headProgress;

            deferredWorldToLocal = transform.worldToLocalMatrix;
            deferredSlot = PixelVfxWorkQueue.Enqueue(this);
        }

        // //그리기

        void IPixelVfxDeferred.ComputeDeferred()
        {
            Work work = Work.ForCurrentThread();
            painter = work.painter;
            painter.BeginSegment(work.buffer, deferredWorldToLocal, 0f);

            DrawTrail();

            painter.EndSegment(out drawSegment);
            painter = null;
        }

        void IPixelVfxDeferred.ApplyDeferred()
        {
            deferredSlot = -1;

            // 정렬은 모든 LateUpdate(캐릭터가 도끼 정렬 순서를 정하는 곳 포함)가 끝난 뒤인 여기서 맞춘다.
            if (null != axeRenderer)
            {
                int order = axeRenderer.sortingOrder + sortOffset;
                if (meshRenderer.sortingLayerID != axeRenderer.sortingLayerID) meshRenderer.sortingLayerID = axeRenderer.sortingLayerID;
                if (meshRenderer.sortingOrder != order) meshRenderer.sortingOrder = order;
            }

            PixelPainter.CommitSegment(in drawSegment, mesh, meshRenderer, ref meshHash, ref meshVertexCount);
            drawSegment.buffer = null;
        }

        private void CompleteDeferred()
        {
            if (0 <= deferredSlot) PixelVfxWorkQueue.CompleteNow(deferredSlot, this);
        }

        // 궤적: 팔 좌표계에서 도끼날 끝이 지나간 호(시작 각도 -> 시작 + 스윙 각도) 중 [drawTail, drawHead] 구간을 띠로 칠한다.
        // 각 월드 픽셀 중심을 팔 좌표계 극좌표로 바꿔 반지름·각도로 판정한다(범위는 반지름 13px 사각형이라 픽셀 수백 개).
        private void DrawTrail()
        {
            float span = drawHead - drawTail;
            if (span <= 0.0001f) return;

            float startAngle = BladeTipAngleDeg + startAxeZ;
            int extent = Mathf.CeilToInt(TrailOuterRadiusPx) + 1;
            float outerSq = TrailOuterRadiusPx * TrailOuterRadiusPx;
            float innerMin = TrailOuterRadiusPx - TrailMaxWidthPx;
            float innerMinSq = innerMin * innerMin;

            for (int dy = -extent; dy <= extent; dy++)
            {
                float wy = dy + 0.5f;
                for (int dx = -extent; dx <= extent; dx++)
                {
                    float wx = dx + 0.5f;
                    float lx = worldToArmX.x * wx + worldToArmX.y * wy;
                    float ly = worldToArmY.x * wx + worldToArmY.y * wy;
                    float rSq = lx * lx + ly * ly;
                    if (rSq > outerSq || rSq <= innerMinSq) continue;

                    float angle = Mathf.Atan2(ly, lx) * Mathf.Rad2Deg;
                    float s = Mathf.DeltaAngle(startAngle, angle) / swingRotationDeg;
                    if (s < drawTail || s > drawHead) continue;

                    // u: 꼬리 0 -> 머리 1
                    float u = (s - drawTail) / span;
                    int width = u > 0.66f ? 3 : (u > 0.33f ? 2 : 1);
                    float inner = TrailOuterRadiusPx - width;
                    if (rSq <= inner * inner) continue;

                    int px = pivotPxX + dx;
                    int py = pivotPxY + dy;

                    Color32 color;
                    if (u > 0.66f) color = ColorHead;
                    else if (u > 0.33f) color = ColorBody;
                    else
                    {
                        // 꼬리는 체커로 점이 빠진다
                        if (0 != ((px + py) & 1)) continue;
                        color = ColorTail;
                    }

                    painter.Dot(px, py, color);
                }
            }
        }

        // //수명

        private void OnDisable()
        {
            // 스스로 꺼질 때(StopImmediate)도 불리므로 여기서는 enabled를 건드리지 않는다.
            StopInternal();
        }

        private void OnDestroy()
        {
            if (null != mesh)
            {
                Destroy(mesh);
                mesh = null;
            }
        }
    }
}
