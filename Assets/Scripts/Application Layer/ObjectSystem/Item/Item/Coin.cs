using UnityEngine;

public enum CoinType
{
    Bronze,
    Silver,
    Gold
}

public class Coin : MonoBehaviour
{
    // 외부 의존성
    [SerializeField] private SpriteRenderer sr;
    [SerializeField] private Animator anim;

    // 내부 의존성
    public CoinType coinType { get; private set; }
    public bool isArrived { get; private set; } = false;

    // 이동 관련 필드
    private Vector3 startPos;
    private Transform dynamicTarget;
    private float height;
    private float duration;
    private Vector3 trajectoryJitter;
    private float rotationSpeed;
    private float elapsed;

    public void Initailize(CoinType _coinType)
    {
        coinType = _coinType;
        isArrived = false;
        elapsed = 0f;
        if (sr != null)
        {
            sr.transform.localPosition = Vector3.zero;
            sr.transform.localRotation = Quaternion.identity;
        }
        transform.localScale = Vector3.one;
    }

    public void DynamicTransferLaunch(Vector3 _start, Transform _target, float _height, float _duration, Vector3 _jitter, float _rotationSpeed)
    {
        startPos = _start;
        dynamicTarget = _target;
        height = _height;
        duration = _duration;
        trajectoryJitter = _jitter;
        rotationSpeed = _rotationSpeed;
        elapsed = 0f;
        isArrived = false;
        transform.position = _start;
    }

    public void ManualUpdate(float _deltaTime)
    {
        if (isArrived || dynamicTarget == null) return;

        float currentT = duration > 0 ? (elapsed / duration) : 1f;
        float speedMultiplier = 1f;

        if (currentT > 0.7f)
        {
            speedMultiplier = 1f + (currentT - 0.7f) * 3f;
        }

        elapsed += _deltaTime * speedMultiplier;
        float t = Mathf.Clamp01(elapsed / duration);

        Vector3 endPos = dynamicTarget.position;
        float jitterFactor = 4f * t * (1f - t);
        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, t) + (trajectoryJitter * jitterFactor);

        float heightOffset = -4f * height * (t - 0.5f) * (t - 0.5f) + height;

        if (sr != null)
        {
            transform.position = currentGroundPos;
            sr.transform.localPosition = new Vector3(0f, heightOffset, 0f);
            sr.transform.Rotate(Vector3.forward, rotationSpeed * _deltaTime);
        }
        else
        {
            transform.position = currentGroundPos + new Vector3(0f, heightOffset, 0f);
        }

        // Scale 연출
        float targetScale = 1f;
        if (t < 0.4f)
        {
            float nt = t / 0.4f;
            const float s = 1.70158f;
            float t1 = nt - 1f;
            targetScale = Mathf.Max(0f, (t1 * t1 * ((s + 1f) * t1 + s) + 1f));
        }
        else if (t > 0.7f)
        {
            float nt = (t - 0.7f) / 0.3f;
            targetScale = 1f - nt;
        }

        transform.localScale = Vector3.one * targetScale;

        if (t >= 1.0f)
        {
            isArrived = true;
            transform.position = endPos;
            if (sr != null)
            {
                sr.transform.localPosition = Vector3.zero;
                sr.transform.localRotation = Quaternion.identity;
            }
            transform.localScale = Vector3.one;
        }
    }
}
