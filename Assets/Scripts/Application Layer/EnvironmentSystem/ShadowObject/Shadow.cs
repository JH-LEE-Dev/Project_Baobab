using UnityEngine;

[ExecuteAlways]
public class Shadow : MonoBehaviour
{
    [Header("Editor Default Pose")]
    [SerializeField] private float defaultRotationZ = 225f;
    [SerializeField] private float defaultScaleY = 1f;

    private Collider2D shadowCollider;

    public void Initialize()
    {
        shadowCollider = GetComponent<Collider2D>();
        ApplyDefaultPose();
    }

    public void ManualUpdate(Quaternion _rotation, float _scaleY, bool _isActive)
    {
        transform.localRotation = _rotation;
        transform.localScale = new Vector3(1f, _scaleY, 1f);

        if (shadowCollider != null)
        {
            shadowCollider.enabled = _isActive;
        }
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
