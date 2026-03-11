using UnityEngine;

public class Shadow : MonoBehaviour
{
    private Collider2D shadowCollider;

    public void Initialize()
    {
        shadowCollider = GetComponent<Collider2D>();
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
}
