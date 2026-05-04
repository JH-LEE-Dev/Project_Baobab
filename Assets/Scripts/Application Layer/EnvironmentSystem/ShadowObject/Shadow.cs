using UnityEngine;

[ExecuteAlways]
public class Shadow : MonoBehaviour
{
    [Header("Editor Default Pose")]
    [SerializeField] private float defaultRotationZ = 90f;
    [SerializeField] private float defaultScaleY = 1f;

    // 내부 의존성
    private SpriteRenderer shadowRenderer;

    public void Initialize()
    {
        shadowRenderer = GetComponent<SpriteRenderer>();
        ApplyDefaultPose();
    }

    public void ManualUpdate(Quaternion _rotation, float _scaleY, bool _isActive)
    {
        // 렌더러 활성화 상태 제어 (RaymarchingShadow와 동일한 동작)
        if (shadowRenderer == null) shadowRenderer = GetComponent<SpriteRenderer>();
        if (shadowRenderer != null) shadowRenderer.enabled = _isActive;

        if (!_isActive) return;

        // 부모의 회전에 영향을 받지 않도록 전역 회전(rotation)을 사용하고,
        // 스프라이트 고유의 방향 보정을 위해 defaultRotationZ를 오프셋으로 적용합니다.
        transform.rotation = _rotation * Quaternion.Euler(0, 0, defaultRotationZ);
        
        // 스케일은 여전히 로컬 스케일을 사용합니다.
        transform.localScale = new Vector3(1f, _scaleY, 1f);
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            return;
        }

        ApplyDefaultPose();
    }

    private void Awake()
    {
        if (!Application.isPlaying)
        {
            ApplyDefaultPose();
        }
    }

    private void ApplyDefaultPose()
    {
        transform.localRotation = Quaternion.Euler(0f, 0f, defaultRotationZ);
        transform.localScale = new Vector3(1f, defaultScaleY, 1f);
    }
}
