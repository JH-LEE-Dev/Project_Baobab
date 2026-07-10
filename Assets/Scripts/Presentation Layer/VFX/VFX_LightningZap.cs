using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

namespace PresentationLayer.VFX
{
    [RequireComponent(typeof(LineRenderer))]
    public class VFX_LightningZap : MonoBehaviour
    {
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

        private LineRenderer originalLineRenderer;
        private List<LineRenderer> lineSegments = new List<LineRenderer>();
        private Material instancedMaterial;
        
        // 델리게이트 캐싱 (GC Alloc 방지)
        private TweenCallback onZapCompleteCallback;

        private void Awake()
        {
            originalLineRenderer = GetComponent<LineRenderer>();
            
            // 머티리얼을 복제하여 캐싱 (여러 개의 번개가 독립적으로 작동하게 함)
            instancedMaterial = originalLineRenderer.material; 
            originalLineRenderer.enabled = false;
            
            lineSegments.Add(originalLineRenderer); // 기본 1개는 자신의 것을 사용

            // 델리게이트 미리 생성 (GC Alloc 완전 방지)
            onZapCompleteCallback = OnZapComplete;
        }

        /// <summary>
        /// (테스트용 버튼) 컴포넌트를 우클릭하거나 점 3개를 눌러서 'Test Fire (PlayZap)'을 누르면 실행됩니다.
        /// </summary>
        [ContextMenu("Test Fire (PlayZap)")]
        public void TestFire()
        {
            // 실행 모드가 아닐 때 트윈이 도는 것을 방지
            if (!Application.isPlaying) 
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
            if (_points == null || _points.Length < 2) return;

            for (int i = 0; i < lineSegments.Count; i++) lineSegments[i].enabled = false;

            int segmentsCount = _points.Length - 1;
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

            GameObject go = new GameObject($"LineSegment_{_index}");
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
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            
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
            if (disableObjectOnComplete)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // 복제된 머티리얼 메모리 릭(누수) 방지
            if (instancedMaterial != null)
            {
                Destroy(instancedMaterial);
            }
        }
    }
}
