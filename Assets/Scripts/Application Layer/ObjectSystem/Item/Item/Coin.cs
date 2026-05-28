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

    // 원본 스케일 보관 필드
    private Vector3 originalScale;
    private Vector3 originalVisualScale;
    private bool bHasCachedOriginalScale = false;

    // 이동 관련 필드
    private Vector3 startPos;
    private Transform dynamicTarget;
    private float height;
    private float duration;
    private Vector3 trajectoryJitter;
    private float rotationSpeed;
    private float elapsed;

    private void Awake()
    {
        CacheOriginalScale();
    }

    public void Initailize(CoinType _coinType)
    {
        CacheOriginalScale();
        coinType = _coinType;
        isArrived = false;
        elapsed = 0f;
        if (sr != null)
        {
            sr.transform.localPosition = Vector3.zero;
            sr.transform.localRotation = Quaternion.identity;
            sr.transform.localScale = originalVisualScale;
        }
        transform.localScale = Vector3.zero;
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
        transform.localScale = Vector3.zero;
    }

    public void ManualUpdate(float _deltaTime)
    {
        if (isArrived || dynamicTarget == null) return;

        float currentT = duration > 0 ? (elapsed / duration) : 1f;
        
        // 정점 부근에서 느려지고, 낙하할 때 빨라지도록 비선형 속도 보정 (Disney Slow-in/Slow-out) - 후반 가속도 극대화
        float speedMultiplier = 1f;
        if (currentT < 0.35f)
        {
            speedMultiplier = 1.3f; // 처음엔 빠르게 튀어 오름
        }
        else if (currentT >= 0.35f && currentT <= 0.6f)
        {
            speedMultiplier = 0.5f; // 정점에서는 좀 더 체공 시간이 길어지는 느낌
        }
        else
        {
            speedMultiplier = 2.4f; // 캐릭터에게 빨려 들어갈 땐 초고속 가속
        }

        elapsed += _deltaTime * speedMultiplier;
        float t = Mathf.Clamp01(elapsed / duration);

        Vector3 endPos = dynamicTarget.position;
        float jitterFactor = 4f * t * (1f - t);
        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, t) + (trajectoryJitter * jitterFactor);

        float heightOffset = -4f * height * (t - 0.5f) * (t - 0.5f) + height;

        // Y축 속도 유추 (위치 변화율 미분)
        float verticalVelocity = -8f * height * (t - 0.5f) / duration;

        if (sr != null)
        {
            transform.position = currentGroundPos;
            sr.transform.localPosition = new Vector3(0f, heightOffset, 0f);

            // 후반 흡수될 때 회전 속도를 최대 5배까지 증폭
            float currentRotationSpeed = rotationSpeed;
            if (t > 0.7f)
            {
                float rotAccel = (t - 0.7f) / 0.3f;
                currentRotationSpeed = rotationSpeed * (1f + rotAccel * 4.0f);
            }
            sr.transform.Rotate(Vector3.forward, currentRotationSpeed * _deltaTime);

            // Squash & Stretch 연출 강화 (변형 폭 상향)
            float stretchY = Mathf.Min(Mathf.Abs(verticalVelocity) * 0.08f, 0.5f);
            float squashX = stretchY * 0.6f;

            // 후반부에 캐릭터 쪽으로 흡수될 때 추가적인 방향성 연장 효과
            if (t > 0.7f)
            {
                float suckStretch = (t - 0.7f) / 0.3f * 0.3f;
                stretchY += suckStretch;
                squashX += suckStretch * 0.5f;
            }

            sr.transform.localScale = new Vector3(
                originalVisualScale.x * (1f - squashX), 
                originalVisualScale.y * (1f + stretchY), 
                originalVisualScale.z
            );
        }
        else
        {
            transform.position = currentGroundPos + new Vector3(0f, heightOffset, 0f);
        }

        // 전체 스케일 곡선 (최대 1.5배 팝업 및 빠른 소멸)
        float targetScale = 1f;
        if (t < 0.3f)
        {
            // 탄성 있게 커짐 (Overshoot 효과 강화)
            float nt = t / 0.3f;
            const float s = 2.0f;
            float t1 = nt - 1f;
            targetScale = Mathf.Max(0f, (t1 * t1 * ((s + 1f) * t1 + s) + 1f)) * 1.5f;
        }
        else if (t > 0.7f)
        {
            // 캐릭터에게 도달할 때 빠르게 작아지며 흡수됨
            float nt = (t - 0.7f) / 0.3f;
            targetScale = Mathf.Lerp(1.5f, 0f, nt);
        }
        else
        {
            targetScale = 1.5f;
        }

        transform.localScale = originalScale * targetScale;

        if (t >= 1.0f)
        {
            isArrived = true;
            transform.position = endPos;
            if (sr != null)
            {
                sr.transform.localPosition = Vector3.zero;
                sr.transform.localRotation = Quaternion.identity;
                sr.transform.localScale = originalVisualScale;
            }
            transform.localScale = originalScale;
        }
    }

    private void CacheOriginalScale()
    {
        if (bHasCachedOriginalScale) return;
        originalScale = transform.localScale;
        if (sr != null)
        {
            originalVisualScale = sr.transform.localScale;
        }
        else
        {
            originalVisualScale = Vector3.one;
        }
        bHasCachedOriginalScale = true;
    }
}
