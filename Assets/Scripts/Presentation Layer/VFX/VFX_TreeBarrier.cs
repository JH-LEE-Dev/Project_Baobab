using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 3스테이지 배리어 나무를 감싸는 별빛 보호막(셰이더 Custom/VFX/TreeBarrier). 나무 프리팹 BaseVisual(SortingGroup)의 비활성 자식이고 TreeObj가 켜고 끈다.
    ///
    /// - 평소: 머티리얼 기본값만으로 그린다(MaterialPropertyBlock 없음). 컴포넌트는 꺼져 있어 Update 비용이 없고, 같은 머티리얼끼리 SRP 배처에 묶인다.
    /// - 피격(PlayHit): 맞은 자리를 넘기고 0.45초 동안 _HitAge를 올린다. 끝나면 블록을 지우고 다시 평소 상태로 돌아간다.
    /// - 파괴(PlayBreak): 맞은 자리에서 금이 번지고 조각이 떨어져 나가는 0.5초 연출 뒤 스스로 비활성화된다.
    ///
    /// 껍질 모양(밑동 기준 px)은 상수로 고정이며 머티리얼 값도 같은 값이다(TreeBarrierBuilder가 맞춘다).
    /// 셰이더가 월드 픽셀 격자로 그리므로 메쉬는 껍질을 덮는 사각형 하나면 된다(인스턴스끼리 공유).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class VFX_TreeBarrier : MonoBehaviour
    {
        // 껍질 타원(밑동 기준 px). 3스테이지 나무 수관은 밑동 위 12~46px, 좌우 ±16px이다.
        public const float ShellCenterYPx = 22.0f;
        public const float ShellRadiusXPx = 21.0f;
        public const float ShellRadiusYPx = 30.0f;

        public const float HitDuration = 0.45f;
        public const float BreakDuration = 0.5f;

        private const float PixelsPerUnit = 32.0f;
        private const float MeshMarginPx = 2.0f;

        private static readonly int HitPointId = Shader.PropertyToID("_HitPoint");
        private static readonly int HitAgeId = Shader.PropertyToID("_HitAge");
        private static readonly int BreakAgeId = Shader.PropertyToID("_BreakAge");

        private static Mesh sharedQuad;
        private static MaterialPropertyBlock sharedBlock;

        //내부 의존성
        private MeshRenderer meshRenderer;

        //상태 변수
        private bool bInitialized;
        private bool bHitPlaying;
        private bool bBreakPlaying;
        private float hitAge;
        private float breakAge;
        private Vector2 hitPointPx;

        /// <summary>껍질 중심(월드). _root는 나무 밑동 위치.</summary>
        public static Vector3 GetShellCenter(Vector3 _root)
        {
            return _root + new Vector3(0.0f, ShellCenterYPx / PixelsPerUnit, 0.0f);
        }

        /// <summary>
        /// _source(공격 쪽 위치) 방향으로 껍질 표면 위의 점(월드)을 돌려준다. 방향이 없으면 껍질 위쪽 점.
        /// _outwardNormal은 그 점에서 바깥을 향하는 화면상 법선이다(스파크 방향).
        /// </summary>
        public static Vector3 GetImpactPoint(Vector3 _root, Vector3 _source, out Vector2 _outwardNormal)
        {
            Vector3 center = GetShellCenter(_root);
            float rx = ShellRadiusXPx / PixelsPerUnit;
            float ry = ShellRadiusYPx / PixelsPerUnit;

            Vector2 d = new Vector2((_source.x - center.x) / rx, (_source.y - center.y) / ry);
            if (d.sqrMagnitude < 0.0001f) d = Vector2.up;
            d.Normalize();

            // 타원 위 점의 법선은 (x/rx^2, y/ry^2) 방향이다
            _outwardNormal = new Vector2(d.x / rx, d.y / ry).normalized;
            return center + new Vector3(d.x * rx * 0.97f, d.y * ry * 0.97f, 0.0f);
        }

        // ---------- 외부 제어 ----------

        /// <summary>배리어를 평소 상태로 켠다(연출 없음). 진행 중이던 피격/파괴 연출은 지운다.</summary>
        public void Show()
        {
            EnsureInitialized();
            bHitPlaying = false;
            bBreakPlaying = false;
            meshRenderer.SetPropertyBlock(null);
            if (false == gameObject.activeSelf) gameObject.SetActive(true);
            enabled = false;
        }

        /// <summary>연출 없이 즉시 끈다(나무가 풀로 돌아가거나 별 표식 나무로 바뀔 때).</summary>
        public void HideImmediate()
        {
            bHitPlaying = false;
            bBreakPlaying = false;
            if (null != meshRenderer) meshRenderer.SetPropertyBlock(null);
            if (true == gameObject.activeSelf) gameObject.SetActive(false);
        }

        public void SetSortingOrder(int _order)
        {
            EnsureInitialized();
            if (meshRenderer.sortingOrder != _order) meshRenderer.sortingOrder = _order;
        }

        /// <summary>막힌 타격의 번쩍임과 고리. _worldImpact는 껍질 위 맞은 자리.</summary>
        public void PlayHit(Vector3 _worldImpact)
        {
            if (false == gameObject.activeSelf || true == bBreakPlaying) return;

            hitPointPx = ToLocalPx(_worldImpact);
            hitAge = 0.0f;
            bHitPlaying = true;
            enabled = true;
            Push();
        }

        /// <summary>깨지는 연출. 끝나면 스스로 비활성화된다.</summary>
        public void PlayBreak(Vector3 _worldImpact)
        {
            if (false == gameObject.activeSelf) return;

            hitPointPx = ToLocalPx(_worldImpact);
            breakAge = 0.0f;
            bBreakPlaying = true;
            bHitPlaying = false;
            enabled = true;
            Push();
        }

        // ---------- 내부 ----------

        private Vector2 ToLocalPx(Vector3 _world)
        {
            Vector3 origin = transform.position;
            return new Vector2(
                Mathf.Floor(_world.x * PixelsPerUnit) - Mathf.Floor(origin.x * PixelsPerUnit + 0.5f) + 0.5f,
                Mathf.Floor(_world.y * PixelsPerUnit) - Mathf.Floor(origin.y * PixelsPerUnit + 0.5f) + 0.5f);
        }

        private void EnsureInitialized()
        {
            if (true == bInitialized) return;
            bInitialized = true;

            meshRenderer = GetComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            if (null == sharedQuad) sharedQuad = BuildQuad();
            GetComponent<MeshFilter>().sharedMesh = sharedQuad;

            if (null == sharedBlock) sharedBlock = new MaterialPropertyBlock();
        }

        private static Mesh BuildQuad()
        {
            float x0 = -(ShellRadiusXPx + MeshMarginPx) / PixelsPerUnit;
            float x1 = (ShellRadiusXPx + MeshMarginPx) / PixelsPerUnit;
            float y0 = (ShellCenterYPx - ShellRadiusYPx - MeshMarginPx) / PixelsPerUnit;
            float y1 = (ShellCenterYPx + ShellRadiusYPx + MeshMarginPx) / PixelsPerUnit;

            Mesh mesh = new Mesh { name = "VFX_TreeBarrier_Quad" };
            mesh.vertices = new[] { new Vector3(x0, y0, 0.0f), new Vector3(x1, y0, 0.0f), new Vector3(x1, y1, 0.0f), new Vector3(x0, y1, 0.0f) };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private void Push()
        {
            sharedBlock.Clear();
            sharedBlock.SetVector(HitPointId, new Vector4(hitPointPx.x, hitPointPx.y, 0.0f, 0.0f));
            sharedBlock.SetFloat(HitAgeId, true == bHitPlaying ? hitAge : 10.0f);
            sharedBlock.SetFloat(BreakAgeId, true == bBreakPlaying ? breakAge : -1.0f);
            meshRenderer.SetPropertyBlock(sharedBlock);
        }

        private void Awake()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (true == bBreakPlaying)
            {
                breakAge += dt;
                if (BreakDuration <= breakAge)
                {
                    HideImmediate();
                    return;
                }

                Push();
                return;
            }

            if (true == bHitPlaying)
            {
                hitAge += dt;
                if (HitDuration <= hitAge)
                {
                    bHitPlaying = false;
                    meshRenderer.SetPropertyBlock(null);
                    enabled = false;
                    return;
                }

                Push();
                return;
            }

            enabled = false;
        }

        private void OnDisable()
        {
            // 비활성화(풀 반환 등)되면 연출 상태가 남지 않게 지운다. 다시 켜질 때는 Show가 평소 상태로 시작한다.
            bHitPlaying = false;
            bBreakPlaying = false;
        }
    }
}
