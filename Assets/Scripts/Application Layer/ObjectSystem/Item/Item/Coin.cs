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

    // LogItem과 동일한 스텐실 2-패스 아웃라인 구조(OutlineStencilWriter -> 자식 Outline).
    [Header("Outline (LogItem과 동일한 방식)")]
    [SerializeField] private GameObject outlineObj;
    [SerializeField] private SpriteRenderer outlineStencilSR;
    [SerializeField] private SpriteRenderer outlineSR;
    [ColorUsage(true, true)]
    [SerializeField] private Color outlineColor = Color.white;
    private static readonly int OutlineColorPropertyID = Shader.PropertyToID("_OutlineColor");
    private MaterialPropertyBlock outlineMPB;

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

    // 정점 부근에서 서서히 느려졌다가 다시 빨라지는 느낌을 위한 가감속 계수(0~0.159 권장, 클수록 정점에서 더 느려짐).
    [SerializeField] [Range(0f, 0.159f)] private float apexEaseStrength = 0.13f;

    private void Awake()
    {
        // 프리팹 인스펙터에 sr이 연결되지 않은 경우를 대비한 안전장치.
        // (실측 결과 Gold/Bronze/Silver 코인 프리팹 전부 sr이 비어있어서 회전/Squash&Stretch가
        // 한 번도 재생되지 않고 있었다 - 이 자동 할당으로 그 문제를 함께 해결한다)
        if (sr == null)
        {
            sr = GetComponent<SpriteRenderer>();
        }

        CacheOriginalScale();
        SyncOutlineSprite();
        ApplyOutlineColor();

        if (outlineObj != null)
        {
            outlineObj.SetActive(true);
        }
    }

    // Animator가 매 프레임 sr의 스프라이트를 바꾸는 반짝임 애니메이션을 재생 중이므로,
    // Outline도 그 프레임 변화를 따라가지 않으면 코인 본체와 어긋나 보인다.
    private void Update()
    {
        SyncOutlineSprite();
    }

    // 아웃라인 스프라이트가 본체 스프라이트와 항상 같은 모양을 가리키도록 동기화한다.
    private void SyncOutlineSprite()
    {
        if (sr == null) return;

        if (outlineStencilSR != null) outlineStencilSR.sprite = sr.sprite;
        if (outlineSR != null) outlineSR.sprite = sr.sprite;
    }

    // 인스펙터에서 설정한 outlineColor(HDR)를 셰이더의 _OutlineColor로 전달한다.
    private void ApplyOutlineColor()
    {
        if (outlineSR == null) return;

        if (outlineMPB == null) outlineMPB = new MaterialPropertyBlock();
        outlineSR.GetPropertyBlock(outlineMPB);
        outlineMPB.SetColor(OutlineColorPropertyID, outlineColor);
        outlineSR.SetPropertyBlock(outlineMPB);
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

        elapsed += _deltaTime;
        float t = Mathf.Clamp01(duration > 0 ? (elapsed / duration) : 1f);

        // 정점(t=0.5) 부근에서는 느려지고 시작/도착 구간에서는 빨라지도록 실제 이동에 쓰이는
        // 시간(t)을 왜곡한다. sin 보정이라 t=0, 0.5, 1의 위치는 그대로 유지되고 그 사이의
        // 진행 속도만 바뀐다 (미분값 1±2π*apexEaseStrength, apexEaseStrength<1/2π면 항상 단조증가).
        float easedT = t + apexEaseStrength * Mathf.Sin(2f * Mathf.PI * t);
        float easedTDerivative = 1f + apexEaseStrength * 2f * Mathf.PI * Mathf.Cos(2f * Mathf.PI * t);

        Vector3 endPos = dynamicTarget.position;
        float jitterFactor = 4f * easedT * (1f - easedT);
        Vector3 currentGroundPos = Vector3.Lerp(startPos, endPos, easedT) + (trajectoryJitter * jitterFactor);

        float heightOffset = -4f * height * (easedT - 0.5f) * (easedT - 0.5f) + height;

        // Y축 속도 유추 (위치 변화율 미분, 연쇄법칙으로 easedT 왜곡까지 반영)
        float verticalVelocity = -8f * height * (easedT - 0.5f) * easedTDerivative / duration;

        // sr이 코인 루트 자신에 붙어있는 구조라(별도 자식 트랜스폼 아님) 위치/회전/스케일을
        // 전부 이 하나의 transform에 합쳐서 적용한다 - 나눠서 적용하면 나중 대입이 앞선 대입을 덮어써 버린다.
        transform.position = currentGroundPos + new Vector3(0f, heightOffset, 0f);

        float squashX = 0f;
        float stretchY = 0f;

        if (sr != null)
        {
            // Squash & Stretch: 낙하 속도에 비례해서만 반응한다(도착 직전 별도 보정 없음).
            stretchY = Mathf.Min(Mathf.Abs(verticalVelocity) * 0.08f, 0.4f);
            squashX = stretchY * 0.6f;
        }

        // 전체 스케일: 오버슈트 없이 등장/흡수 구간에서만 짧게 0<->1로 스냅하고,
        // 그 사이 구간에는 Squash&Stretch만 반영한다(비행 내내 1.5배로 부풀어있던 기존 방식 제거).
        const float snapWindow = 0.12f;
        float scaleMultiplier;
        if (t < snapWindow)
        {
            scaleMultiplier = t / snapWindow; // 등장: 0 -> 1
        }
        else if (t > 1f - snapWindow)
        {
            scaleMultiplier = (1f - t) / snapWindow; // 흡수: 1 -> 0
        }
        else
        {
            scaleMultiplier = 1f;
        }

        transform.localScale = new Vector3(
            originalScale.x * (1f - squashX) * scaleMultiplier,
            originalScale.y * (1f + stretchY) * scaleMultiplier,
            originalScale.z * scaleMultiplier
        );

        if (t >= 1.0f)
        {
            isArrived = true;
            transform.position = endPos;
            transform.rotation = Quaternion.identity;
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

#if UNITY_EDITOR
    // 인스펙터에서 outlineColor(HDR)를 바꿀 때 에디터 프리뷰에도 바로 반영되게 한다.
    private void OnValidate()
    {
        if (outlineSR == null) return;
        ApplyOutlineColor();
    }
#endif
}
