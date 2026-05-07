using UnityEngine;

[ExecuteAlways]
public class OffsetShadow : MonoBehaviour
{
    // 내부 의존성
    private SpriteRenderer shadowRenderer;

    [Header("Settings")]
    [SerializeField] private bool isSelfRotating = true;
    [SerializeField] private Vector2 offset = Vector2.zero;
    [SerializeField] private float defaultRotationZ = 0f;

    [Header("Orbit Settings")]
    [SerializeField] private float majorAxisLength = 1f;
    [SerializeField] private float minorAxisLength = 0.5f;

    [Header("Scale Settings")]
    [SerializeField] private float minScaleYFactor = 0.5f;

    [Header("Debug Settings")]
    [SerializeField] private bool useDebugValues = false;
    [Range(0, 360)]
    [SerializeField] private float debugAngle = 0f;
    [Range(0f, 1f)]
    [SerializeField] private float debugScaleY = 0.5f;
    [SerializeField] private bool debugIsActive = true;

    public void Initialize()
    {
        shadowRenderer = GetComponent<SpriteRenderer>();
        ApplyDefaultPose();
    }

    private void Update()
    {
        if (useDebugValues)
        {
            ManualUpdate(debugAngle, debugScaleY, debugIsActive);
        }
    }

    public void ManualUpdate(float _angle, float _scaleY, bool _isActive)
    {
        if (shadowRenderer == null) shadowRenderer = GetComponent<SpriteRenderer>();
        if (shadowRenderer != null) shadowRenderer.enabled = _isActive;

        if (!_isActive) return;

        float radian = _angle * Mathf.Deg2Rad;

        // 1 & 2. 위치 결정: 자전(제자리) 또는 공전(오프셋 기준 타원 궤도)
        if (isSelfRotating)
        {
            transform.localPosition = (Vector3)offset;
        }
        else
        {
            // 3 & 4. 공전 반경 및 타원 궤도 계산
            float x = majorAxisLength * Mathf.Cos(radian);
            float y = minorAxisLength * Mathf.Sin(radian);
            transform.localPosition = (Vector3)offset + new Vector3(x, y, 0f);
        }

        // 회전 적용 (Shadow.cs와 동일하게 defaultRotationZ를 오프셋으로 사용)
        //transform.rotation = Quaternion.Euler(0, 0, _angle + defaultRotationZ);

        // 5. 스케일 계산: 장축쪽으로 회전할수록 y스케일이 줄어든다.
        // 실제 길이가 더 긴 축(장축) 방향을 향할 때 감쇠가 최대(minScaleYFactor 적용)가 되도록 합니다.
        float absCos = Mathf.Abs(Mathf.Cos(radian));
        float absSin = Mathf.Abs(Mathf.Sin(radian));
        float reductionFactor = (Mathf.Abs(majorAxisLength) >= Mathf.Abs(minorAxisLength)) ? absCos : absSin;

        float targetScaleY = Mathf.Lerp(_scaleY, _scaleY * minScaleYFactor, reductionFactor);

        transform.localScale = new Vector3(1f, targetScaleY, 1f);
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
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
        transform.localPosition = (Vector3)offset;
        transform.localRotation = Quaternion.Euler(0f, 0f, defaultRotationZ);
        transform.localScale = new Vector3(1f, 1f, 1f);
    }
}
