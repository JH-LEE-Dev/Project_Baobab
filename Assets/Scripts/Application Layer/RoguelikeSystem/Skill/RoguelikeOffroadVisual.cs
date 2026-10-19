using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 오프로드 카드의 차량(기획서 6-1 "본편 오프로드 비주얼을 그대로 사용한다. 필드 끝 면 타일 위에 랜덤하게 플래시되며
/// 소환되고, 끝 면에서 정면 방향으로 돌진"). 본편 차량(OffRoadVehicle.prefab)의 차체·내부·바퀴·그림자 그림과 바퀴 애니메이터를 쓴다.
///
/// 위치·속도·피해는 RoguelikeSkillController가 정하고, 이 컴포넌트는 차체의 움직임 손맛만 맡는다.
///   예열: 진행 방향을 바라보고, 본편 시동처럼 차체가 스프링으로 출렁이며 떨린다. 뒤로 살짝 웅크렸다가(예비 동작) 하얀 플래시가 깜빡이고
///         시동음이 난다. 바퀴는 멈춰 있다.
///   출발: 웅크림이 풀리며 앞으로 늘어났다 돌아온다. 바퀴가 속도에 맞춰 돈다.
///   들이받기: 차체가 앞뒤로 눌렸다 튀고 살짝 들썩인다(Bump).
///   퇴장: 감속 없이 섬 끝에 닿기 직전, 달리는 채로 소환 때처럼 하얀 플래시가 깜빡이다 하얗게 번쩍이며 사라진다(BeginExit).
/// 움직임은 차체(Flip)에만 주고 그림자는 그대로 두어 차가 땅에 붙어 보이게 한다.
/// 정렬은 본편 CustomSortable과 같은 규칙(-y × 100)으로 매 프레임 맞춘다.
/// </summary>
public class RoguelikeOffroadVisual : MonoBehaviour
{
    private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");

    [SerializeField] private Transform flipRoot;
    [Tooltip("차량 그림이 오른쪽을 보고 있는지. 진행 방향이 반대면 좌우를 뒤집는다.")]
    [SerializeField] private bool bSpriteFacesRight = true;
    [SerializeField] private SpriteRenderer[] flashRenderers;
    [SerializeField] private Animator wheelAnimator;
    [SerializeField] private SortingGroup sortingGroup;
    [Tooltip("예열 동안 플래시가 깜빡이는 횟수.")]
    [SerializeField] private int warmupFlashCount = 2;
    [Tooltip("바퀴 애니메이션 속도 = 차량 속도 × 이 값(본편은 0.2).")]
    [SerializeField] private float wheelSpeedPerUnit = 0.2f;

    [Header("예열 (본편 시동 스프링과 같은 식)")]
    [SerializeField] private float ignitionScaleIntensity = 0.25f;
    [SerializeField] private float ignitionSpringFrequency = 18f;
    [SerializeField] private float ignitionSpringDamping = 6f;
    [Tooltip("예열 동안 차체 떨림(월드). 시작이 가장 세고 줄어든다.")]
    [SerializeField] private float ignitionShake = 0.04f;
    [Tooltip("출발 직전 진행 반대쪽으로 웅크리는 거리(월드).")]
    [SerializeField] private float anticipationDistance = 0.15f;

    [Header("주행")]
    [Tooltip("달리는 동안 차체 떨림(월드).")]
    [SerializeField] private float driveShake = 0.012f;
    [Tooltip("출발 순간 앞으로 늘어나는 정도(가로 +, 세로 -).")]
    [SerializeField] private float launchStretch = 0.22f;
    [SerializeField] private float launchStretchSeconds = 0.18f;

    [Header("퇴장 (섬 끝 직전)")]
    [Tooltip("사라지기 전 플래시가 깜빡이는 횟수. 마지막 깜빡임은 하얗게 멈춘 채 사라진다.")]
    [SerializeField] private int exitFlashCount = 2;

    [Header("들이받기")]
    [Tooltip("들이받을 때 차체가 눌리는 정도(나무 수에 따라 조금 커진다).")]
    [SerializeField] private float bumpSquash = 0.14f;
    [Tooltip("들이받을 때 들썩이는 높이(월드).")]
    [SerializeField] private float bumpHop = 0.06f;
    [SerializeField] private float bumpSeconds = 0.14f;

    private enum EState
    {
        Hidden,
        Warmup,
        Drive,
        Exit,
    }

    private MaterialPropertyBlock propertyBlock;
    private EState state = EState.Hidden;
    private Vector3 basePosition;
    private Vector2 direction = Vector2.right;
    private float facingSign = 1f;

    private float warmupStartTime;
    private float warmupDuration;
    private float launchTime = -100f;
    private float exitStartTime;
    private float exitDuration = 0.35f;
    private float bumpTime = -100f;
    private float bumpStrength;
    private float speed;

    /// <summary>
    /// 필드 끝에 소환된다. _seconds 동안 예열한 뒤 BeginDash로 돌진한다.
    /// _bPlaySound: 시동음을 낼지. 여러 대가 함께 나가면 첫 차만 낸다(엔진음과 같은 규칙 - 같은 소리가 겹치면 뭉개진다).
    /// </summary>
    public void BeginWarmup(Vector3 _from, Vector3 _to, float _seconds, bool _bPlaySound)
    {
        basePosition = _from;
        Vector3 dir = _to - _from;
        direction = dir.sqrMagnitude > 0.0001f ? ((Vector2)dir).normalized : Vector2.right;
        Face(direction);

        warmupStartTime = Time.time;
        warmupDuration = Mathf.Max(0.01f, _seconds);
        launchTime = -100f;
        bumpTime = -100f;
        speed = 0f;
        state = EState.Warmup;

        if (null != wheelAnimator) wheelAnimator.speed = 0f;
        ApplyPose();
        if (_bPlaySound) Sound.Play(SoundID.OffroadNonEdit, _from);
    }

    public void BeginDash()
    {
        state = EState.Drive;
        launchTime = Time.time;
        SetFlash(0f);
    }

    /// <summary>섬 끝에 닿기 직전이다. 달리는 채로 _seconds 동안 플래시가 깜빡이며 사라질 준비를 한다(끝나면 컨트롤러가 Hide한다).</summary>
    public void BeginExit(float _seconds)
    {
        state = EState.Exit;
        exitStartTime = Time.time;
        exitDuration = Mathf.Max(0.01f, _seconds);
    }

    /// <summary>컨트롤러가 정한 이번 프레임의 위치와 속도. 손맛 오프셋은 이 위치 위에 얹는다.</summary>
    public void SetPose(Vector3 _position, float _speed)
    {
        basePosition = _position;
        speed = Mathf.Max(0f, _speed);
    }

    /// <summary>나무를 들이받았다. _trees는 같은 순간에 맞은 나무 수.</summary>
    public void Bump(int _trees)
    {
        bumpTime = Time.time;
        bumpStrength = 1f + 0.25f * Mathf.Clamp(_trees - 1, 0, 4);
    }

    public void Hide()
    {
        state = EState.Hidden;
        SetFlash(0f);
        gameObject.SetActive(false);
    }

    private void Face(Vector2 _direction)
    {
        if (Mathf.Abs(_direction.x) < 0.0001f) return;

        bool bRight = _direction.x > 0f;
        facingSign = bRight == bSpriteFacesRight ? 1f : -1f;
    }

    private void LateUpdate()
    {
        if (EState.Hidden == state) return;
        ApplyPose();
    }

    private void ApplyPose()
    {
        float now = Time.time;
        Vector2 scale = Vector2.one;
        Vector3 offset = Vector3.zero;
        Vector3 bodyOffset = Vector3.zero;

        if (EState.Warmup == state)
        {
            float t = Mathf.Clamp01((now - warmupStartTime) / warmupDuration);

            // 본편 IgnitionImpactSequence와 같은 감쇠 스프링: 가로로 퍼졌다 세로로 눌렸다 하며 잦아든다.
            float spring = Mathf.Exp(-ignitionSpringDamping * t) * Mathf.Sin(ignitionSpringFrequency * t);
            float pulse = spring * ignitionScaleIntensity;
            scale += new Vector2(pulse, -pulse * 0.7f);

            float shake = ignitionShake * Mathf.Lerp(1f, 0.35f, t);
            bodyOffset += new Vector3(Random.Range(-shake, shake), Random.Range(-shake, shake), 0f);

            // 후반에 진행 반대쪽으로 웅크린다(출발 직전 힘을 모으는 예비 동작).
            float crouch = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.4f, 1f, t));
            offset -= (Vector3)(direction * anticipationDistance * crouch);
            scale += new Vector2(-0.08f, 0.06f) * crouch;

            // 깜빡임마다 1에서 0으로 빠르게 빠지는 플래시(본편 ShinyRoutine과 같은 하얀 번쩍임).
            float phase = t * Mathf.Max(1, warmupFlashCount);
            float local = phase - Mathf.Floor(phase);
            SetFlash(t >= 1f ? 0f : 1f - local * local);
        }
        else if (EState.Exit == state)
        {
            // 퇴장: 섬 끝에 닿기 직전, 달리는 채로 하얀 플래시가 깜빡이다가 마지막엔 하얗게 번쩍인 채 사라진다.
            // 소환(예열)의 깜빡임을 거꾸로 돌린 모습이다. 감속하지 않으므로 바퀴와 차체 떨림은 주행 때 그대로다.
            float t = Mathf.Clamp01((now - exitStartTime) / exitDuration);

            // 깜빡임마다 0에서 1로 차오르는 플래시. 마지막 깜빡임은 1까지 차오른 뒤 하얗게 멈춘 채 사라진다.
            int flashes = Mathf.Max(1, exitFlashCount);
            float phase = t * flashes;
            float local = phase - Mathf.Floor(phase);
            bool bLastFlash = t >= 1f || phase >= flashes - 1;
            SetFlash(bLastFlash ? Mathf.Clamp01((phase - (flashes - 1)) * 1.5f) : local * local);

            float shake = driveShake;
            bodyOffset += new Vector3(Random.Range(-shake, shake), Random.Range(-shake, shake), 0f);
            if (null != wheelAnimator) wheelAnimator.speed = speed * wheelSpeedPerUnit;
        }
        else
        {
            // 출발: 웅크림이 풀리며 앞으로 늘어났다가 스프링처럼 돌아온다.
            float launch = Mathf.Clamp01((now - launchTime) / Mathf.Max(0.01f, launchStretchSeconds));
            if (launch < 1f)
            {
                float k = Mathf.Sin(launch * Mathf.PI) * (1f - launch * 0.5f);
                scale += new Vector2(launchStretch, -launchStretch * 0.6f) * k;
            }

            // 들이받기: 앞뒤로 눌리고 살짝 들썩인다.
            float bump = Mathf.Clamp01((now - bumpTime) / Mathf.Max(0.01f, bumpSeconds));
            if (bump < 1f)
            {
                float decay = 1f - bump;
                float wave = Mathf.Cos(bump * Mathf.PI * 2.5f) * decay;
                scale += new Vector2(-bumpSquash, bumpSquash * 0.8f) * (wave * bumpStrength);
                bodyOffset.y += bumpHop * bumpStrength * Mathf.Sin(bump * Mathf.PI) * decay;
            }

            float shake = driveShake;
            bodyOffset += new Vector3(Random.Range(-shake, shake), Random.Range(-shake, shake), 0f);

            if (null != wheelAnimator) wheelAnimator.speed = speed * wheelSpeedPerUnit;
        }

        transform.position = basePosition + offset;

        if (null != flipRoot)
        {
            flipRoot.localScale = new Vector3(facingSign * Mathf.Max(0.1f, scale.x), Mathf.Max(0.1f, scale.y), 1f);
            flipRoot.localPosition = bodyOffset;
        }

        UpdateSorting();
    }

    private void UpdateSorting()
    {
        if (null != sortingGroup) sortingGroup.sortingOrder = -Mathf.RoundToInt(basePosition.y * 100f);
    }

    private void SetFlash(float _amount)
    {
        if (null == flashRenderers) return;
        if (null == propertyBlock) propertyBlock = new MaterialPropertyBlock();

        for (int i = 0; i < flashRenderers.Length; i++)
        {
            SpriteRenderer renderer = flashRenderers[i];
            if (null == renderer) continue;

            renderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(FlashAmountId, _amount);
            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    private void OnDisable()
    {
        state = EState.Hidden;
        SetFlash(0f);
        if (null != flipRoot)
        {
            flipRoot.localScale = new Vector3(facingSign, 1f, 1f);
            flipRoot.localPosition = Vector3.zero;
        }
    }
}
