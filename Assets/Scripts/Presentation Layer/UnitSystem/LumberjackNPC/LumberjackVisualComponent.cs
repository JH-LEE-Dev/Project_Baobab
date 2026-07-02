using UnityEngine;

public class LumberjackVisualComponent : MonoBehaviour
{
    private Animator anim;
    private SpriteRenderer sr;

    private readonly int facingDirHash = Animator.StringToHash("facingDir");
    private readonly int isMovingHash = Animator.StringToHash("IsMoving");

    public void Initialize()
    {
        anim = GetComponent<Animator>();
        sr = GetComponent<SpriteRenderer>();
    }

    public void SetMoving(bool _isMoving)
    {
        if (anim != null)
        {
            anim.SetBool(isMovingHash, _isMoving);
        }
    }

    public void SetFacingDirection(Vector2 _direction)
    {
        if (_direction.sqrMagnitude < 0.01f || anim == null || sr == null) return;

        float absX = Mathf.Abs(_direction.x);
        float absY = Mathf.Abs(_direction.y);

        int dirIndex = 0;
        bool shouldFlip = false;

        if (absX > absY)
        {
            dirIndex = 0; // Horizontal
            shouldFlip = _direction.x < 0;
        }
        else
        {
            if (_direction.y > 0) dirIndex = 1; // Up
            else dirIndex = 2; // Down
        }

        anim.SetFloat(facingDirHash, dirIndex);
        
        Vector3 scale = transform.localScale;
        scale.x = shouldFlip ? -1f : 1f;
        transform.localScale = scale;
    }
}
