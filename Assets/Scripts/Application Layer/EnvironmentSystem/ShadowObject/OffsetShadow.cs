using UnityEngine;

[ExecuteAlways]
public class OffsetShadow : MonoBehaviour
{
    // 내부 의존성
    [Header("Offset Settings")]
    [SerializeField] private float offsetDistance = 0.1f;
    [SerializeField] private float defaultRotationZ = 225f;

    public void Initialize()
    {
        ApplyDefaultPose();
    }

    public void ManualUpdate(Quaternion _rotation, float _scaleY, bool _isActive)
    {
        // 위치 오프셋 적용 (방향 벡터 추출)
        Vector3 direction = _rotation * Vector3.up;
        transform.localPosition = direction * offsetDistance;

        // 스케일 변환 제거: 항상 1:1:1 비율 유지
        transform.localScale = Vector3.one;
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
        Vector3 direction = Quaternion.Euler(0f, 0f, defaultRotationZ) * Vector3.up;
        transform.localPosition = direction * offsetDistance;
        transform.localScale = Vector3.one;
    }
}
