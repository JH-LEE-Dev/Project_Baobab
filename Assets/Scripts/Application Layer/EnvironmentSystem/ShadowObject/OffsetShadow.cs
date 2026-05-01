using UnityEngine;

[ExecuteAlways]
public class OffsetShadow : MonoBehaviour
{
    //외부 설정 (디버깅용)
    [Header("Debug Settings")]
    [SerializeField] private bool useDebugValues = false;
    [Range(0, 360)]
    [SerializeField] private float debugAngle = 0f;
    [Range(0.1f, 3.0f)]
    [SerializeField] private float debugScaleY = 0.5f;
    [SerializeField] private bool debugIsActive = true;

    //내부 의존성
    private Renderer shadowRenderer;
    private MaterialPropertyBlock propertyBlock;
    
    //쉐이더 속성 ID 캐싱
    private static readonly int shadowAngleId = Shader.PropertyToID("_ShadowAngle");
    private static readonly int maxDistanceId = Shader.PropertyToID("_MaxDistance");

    private void Awake()
    {
        Initialize();
    }

    private void Initialize()
    {
        if (shadowRenderer == null) shadowRenderer = GetComponent<Renderer>();
        if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        // 인스펙터에서 디버그 모드가 활성화된 경우에만 실행
        if (useDebugValues)
        {
            ManualUpdate(Quaternion.Euler(0, 0, debugAngle), debugScaleY, debugIsActive);
        }
    }

    public void ManualUpdate(Quaternion _rotation, float _scaleY, bool _isActive)
    {
        if (shadowRenderer == null || propertyBlock == null) Initialize();
        if (shadowRenderer == null) return;

        // 렌더러 활성화 상태 제어
        shadowRenderer.enabled = _isActive;
        if (!_isActive) return;

        // 쿼터니언으로부터 Z축 회전각(Degree)을 추출
        float angleDeg = _rotation.eulerAngles.z;

        // MaterialPropertyBlock에 값 설정
        shadowRenderer.GetPropertyBlock(propertyBlock);
        
        propertyBlock.SetFloat(shadowAngleId, angleDeg);
        
        // _scaleY를 _MaxDistance 속성에 전달
        propertyBlock.SetFloat(maxDistanceId, _scaleY);

        shadowRenderer.SetPropertyBlock(propertyBlock);
    }
}
