using UnityEngine;

[ExecuteAlways]
public class Shadow : MonoBehaviour
{
    [Header("Editor Default Pose")]
    [SerializeField] private float defaultRotationZ = 90f;
    [SerializeField] private float defaultScaleY = 1f;

    // 내부 의존성
    private SpriteRenderer shadowRenderer;
    private Transform cachedTransform;

    private bool lastActiveState = false;
    private float lastAngle = float.MinValue;
    private float lastScaleY = float.MinValue;

    public void Initialize()
    {
        shadowRenderer = GetComponent<SpriteRenderer>();
        cachedTransform = transform;
        ApplyDefaultPose();
    }

    public void ManualUpdate(float _angle, float _scaleY, bool _isActive)
    {
        // 렌더러 활성화 상태 제어 - 상태가 변경될 때만 호출하여 불필요한 네이티브 호출 방지
        if (lastActiveState != _isActive)
        {
            if (shadowRenderer == null) shadowRenderer = GetComponent<SpriteRenderer>();
            if (shadowRenderer != null) shadowRenderer.enabled = _isActive;
            lastActiveState = _isActive;
        }

        if (!_isActive) return;

        // 각도나 스케일에 변화가 없다면 트랜스폼 업데이트 건너뜀
        if (Mathf.Approximately(lastAngle, _angle) && Mathf.Approximately(lastScaleY, _scaleY))
        {
            return;
        }

        // 부모의 회전에 영향을 받지 않도록 전역 회전(rotation)을 사용하고,
        // 스프라이트 고유의 방향 보정을 위해 defaultRotationZ를 오프셋으로 적용합니다.
        cachedTransform.rotation = Quaternion.Euler(0, 0, _angle + defaultRotationZ);
        
        // 스케일은 여전히 로컬 스케일을 사용합니다.
        cachedTransform.localScale = new Vector3(1f, _scaleY, 1f);

        lastAngle = _angle;
        lastScaleY = _scaleY;
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
