using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PresentationLayer.VFX
{
    [RequireComponent(typeof(LineRenderer))]
    public class VFX_LightningZap : MonoBehaviour
    {
        // LightningZapCreator가 구독해서 풀로 되돌리는 용도로만 사용한다 (Boomerang.ReturnToPoolEvent와 동일한 역할).
        public event Action<VFX_LightningZap> ReturnToPoolEvent;

        [Header("번개 연출 세팅")]
        [SerializeField] private float zapDuration = 0.15f;    // 팍! 하고 사라지기까지의 시간
        [SerializeField] private float startThickness = 0.15f; // 처음 터질 때의 최대 굵기
        [SerializeField] private Ease zapEase = Ease.OutQuad;  // 서서히 얇아지는 곡선

        [Header("다중 타겟 (체인) 세팅")]
        [SerializeField, Tooltip("인스펙터에서 고정된 발사 좌표를 직접 지정할 수 있습니다. (동적으로 쏠 때는 비워두셔도 됩니다)")]
        private Vector3[] fixedPoints;

        [Header("색상 세팅")]
        [SerializeField, ColorUsage(true, true), Tooltip("번개의 색상을 지정합니다. (HDR을 켜면 빛이 번집니다)")]
        private Color lightningColor = new Color(0.5f, 0.8f, 1f, 1f);

        [Header("최적화 세팅")]
        [SerializeField, Tooltip("애니메이션이 끝나면 이 게임오브젝트 자체를 꺼버립니다. (오브젝트 풀링용)")]
        private bool disableObjectOnComplete = true;
        [SerializeField, Tooltip("사전 생성할 최대 번개 분절(Segment) 개수입니다.")]
        private int maxSegments = 8;

        private LineRenderer originalLineRenderer;
        private List<LineRenderer> lineSegments = new List<LineRenderer>();
        private Material instancedMaterial;
        
        // 델리게이트 캐싱 (GC Alloc 방지)
        private TweenCallback onZapCompleteCallback;

        private void Awake()
        {
            originalLineRenderer = GetComponent<LineRenderer>();

            // 프리팹에 미리 세팅된 정점 컬러(예: 주황빛 colorGradient)를 중립(흰색)으로 리셋한다.
            // Lightning2D 셰이더가 "정점 컬러 * _Color"로 가장자리 글로우 색을 계산하는데, 동적으로
            // 생성되는 나머지 세그먼트는 기본값인 흰색 정점 컬러를 쓰므로, 원본만 흰색이 아니면 SetColor로
            // 지정한 색이 원본 세그먼트에서만 다른 색과 섞여버린다. 흰색으로 맞춰야 _Color가 그대로 나온다.
            originalLineRenderer.colorGradient = BuildNeutralGradient();

            // 머티리얼을 복제하여 캐싱 (여러 개의 번개가 독립적으로 작동하게 함)
            instancedMaterial = originalLineRenderer.material;
            originalLineRenderer.enabled = false;

            lineSegments.Add(originalLineRenderer); // 기본 1개는 자신의 것을 사용

            // 델리게이트 미리 생성 (GC Alloc 완전 방지)
            onZapCompleteCallback = OnZapComplete;

            // 풀 사전 생성 (Pre-warm)
            for (int i = 1; maxSegments > i; i++)
            {
                GetOrCreateSegment(i);
            }
        }

        private static Gradient BuildNeutralGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) }
            );
            return gradient;
        }

        /// <summary>
        /// (테스트용 버튼) 컴포넌트를 우클릭하거나 점 3개를 눌러서 'Test Fire (PlayZap)'을 누르면 실행됩니다.
        /// </summary>
        [ContextMenu("Test Fire (PlayZap)")]
        public void TestFire()
        {
            // 실행 모드가 아닐 때 트윈이 도는 것을 방지
            if (false == Application.isPlaying) 
            {
                Debug.LogWarning("Test Fire는 플레이 모드에서만 작동합니다.");
                return;
            }
            PlayZap();
        }

        /// <summary>
        /// 코드로 고정 좌표(Fixed Points)를 덮어씌울 때 사용합니다.
        /// </summary>
        public void SetFixedPoints(Vector3[] _newPoints)
        {
            fixedPoints = _newPoints;
        }

        /// <summary>
        /// 인스펙터에서 설정해둔 fixedPoints 배열을 사용하여 번개 발사
        /// </summary>
        public void PlayZap()
        {
            PlayZap(fixedPoints);
        }


        /// <summary>
        /// 코드로 번개의 색상(HDR)을 실시간으로 변경할 때 사용합니다.
        /// </summary>
        public void SetColor(Color _hdrColor)
        {
            lightningColor = _hdrColor;
        }

        /// <summary>
        /// 색상과 HDR Intensity(노출값)를 함께 지정합니다. Unity HDR 컬러 피커의 Intensity 슬라이더와
        /// 동일하게 RGB를 2^_intensity 배로 스케일합니다(알파는 그대로 유지). _intensity가 0이면
        /// _baseColor 그대로 적용됩니다.
        /// </summary>
        public void SetColor(Color _baseColor, float _intensity)
        {
            float scale = Mathf.Pow(2f, _intensity);
            lightningColor = new Color(_baseColor.r * scale, _baseColor.g * scale, _baseColor.b * scale, _baseColor.a);
        }

        /// <summary>
        /// 지정된 시작점(A)과 끝점(B)에 맞춰 번개를 쏘고 파사삭 사라지는 연출 재생 (가비지 프리)
        /// </summary>
        public void PlayZap(Vector3 _startPos, Vector3 _endPos)
        {
            for (int i = 0; i < lineSegments.Count; i++) lineSegments[i].enabled = false;

            LineRenderer lr = GetOrCreateSegment(0);
            lr.positionCount = 2;
            lr.SetPosition(0, _startPos);
            lr.SetPosition(1, _endPos);
            lr.enabled = true;
            
            ExecuteZap();
        }

        /// <summary>
        /// N개의 점을 이어주는 체인 라이트닝 연출 재생 (배열 재사용 시 가비지 프리)
        /// </summary>
        /// <param name="_points">A -> B -> C 순서대로 이어질 좌표 배열</param>
        public void PlayZap(Vector3[] _points)
        {
            if (null == _points) return;
            PlayZapInternal(_points, _points.Length);
        }

        /// <summary>
        /// N개의 점을 이어주는 체인 라이트닝 연출 재생. 개수가 매번 달라지는 호출부(드론 연쇄공격 등)가
        /// List&lt;Vector3&gt;를 배열로 변환(ToArray)하지 않고 그대로 넘길 수 있도록 개수를 별도로 받는다.
        /// </summary>
        /// <param name="_points">A -> B -> C 순서대로 이어질 좌표 목록(리스트의 실제 Count보다 클 수 있으므로 _count로 유효 구간을 지정)</param>
        /// <param name="_count">_points 중 실제로 사용할 앞쪽 개수</param>
        public void PlayZap(IReadOnlyList<Vector3> _points, int _count)
        {
            PlayZapInternal(_points, _count);
        }

        private void PlayZapInternal(IReadOnlyList<Vector3> _points, int _count)
        {
            if (null == _points || 2 > _count) return;

            for (int i = 0; i < lineSegments.Count; i++) lineSegments[i].enabled = false;

            int segmentsCount = _count - 1;
            for (int i = 0; i < segmentsCount; i++)
            {
                LineRenderer lr = GetOrCreateSegment(i);
                lr.positionCount = 2;
                lr.SetPosition(0, _points[i]);
                lr.SetPosition(1, _points[i + 1]);
                lr.enabled = true;
            }

            ExecuteZap();
        }

        private LineRenderer GetOrCreateSegment(int _index)
        {
            if (_index < lineSegments.Count) return lineSegments[_index];

            Debug.LogWarning($"[VFX_LightningZap] maxSegments({maxSegments}) 부족으로 런타임에 동적 생성됨. 성능 최적화를 위해 인스펙터에서 값을 늘려주세요.");

            GameObject go = new GameObject("LineSegment_Pool");
            go.transform.SetParent(this.transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            
            // 핵심 세팅 복사
            lr.sharedMaterial = instancedMaterial;
            lr.positionCount = 2;
            lr.startWidth = originalLineRenderer.startWidth;
            lr.endWidth = originalLineRenderer.endWidth;
            lr.textureMode = originalLineRenderer.textureMode;
            lr.alignment = originalLineRenderer.alignment;
            lr.sortingLayerID = originalLineRenderer.sortingLayerID;
            lr.sortingOrder = originalLineRenderer.sortingOrder;
            lr.numCapVertices = originalLineRenderer.numCapVertices;
            lr.numCornerVertices = originalLineRenderer.numCornerVertices;

            lr.enabled = false;
            lineSegments.Add(lr);
            
            return lr;
        }

        private void ExecuteZap()
        {
            // 게임오브젝트가 꺼져있었다면 다시 켜기
            if (false == gameObject.activeSelf) gameObject.SetActive(true);
            
            // 2. 진행 중인 트윈 취소 및 초기 굵기/색상 강제 설정
            DOTween.Kill(instancedMaterial);
            instancedMaterial.SetFloat("_Thickness", startThickness);
            instancedMaterial.SetColor("_Color", lightningColor);

            // 3. 두께(Thickness)를 0으로 빠르게 줄이면서 파사삭 사라지는 연출
            instancedMaterial.DOFloat(0f, "_Thickness", zapDuration)
                             .SetEase(zapEase)
                             .SetTarget(instancedMaterial)
                             .OnComplete(onZapCompleteCallback);
        }

        private void OnZapComplete()
        {
            for (int i = 0; i < lineSegments.Count; i++) lineSegments[i].enabled = false;

            // 최적화: 애니메이션이 끝나면 오브젝트 자체를 비활성화
            if (true == disableObjectOnComplete)
            {
                gameObject.SetActive(false);
            }

            // 풀에서 꺼내 쓴 경우(구독자가 있는 경우)에만 반환한다. 드론처럼 풀 없이 전용 인스턴스로
            // 쓰는 경우엔 구독자가 없으므로 이 호출은 아무 영향이 없다.
            ReturnToPoolEvent?.Invoke(this);
        }

        private void OnDestroy()
        {
            // 복제된 머티리얼 메모리 릭(누수) 방지
            if (null != instancedMaterial)
            {
                Destroy(instancedMaterial);
            }
        }
    }
}
