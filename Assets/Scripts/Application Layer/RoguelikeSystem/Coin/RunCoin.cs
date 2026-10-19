using System;
using UnityEngine;

/// <summary>
/// 바닥에 흩뿌려지는 로그라이크 코인 한 개. 움직임만 담당하고 값(골드)은 RunCoinController가 들고 있다.
/// 흩뿌림·흡수 궤적 같은 움직임은 이 클래스가 맡고, 스프라이트·반짝임 같은 이펙트는 이펙트 담당이 프리팹과 연출 훅으로 다듬는다.
///
/// 정렬: 바닥에 있는 동안은 나무·원목과 같은 규칙(CustomSortable - 지면 Y가 낮을수록 앞)으로 그려 나무 뒤의 코인은 나무에 가려진다.
/// 흡수되어 HUD로 날아갈 때는 화면 위를 가로지르므로 날아가는 아이템 층(FlyingItem)으로 올린다.
///
/// 알파(기획서 3-3 골드 획득 1~2단계): 흩뿌려지는 동안은 불투명하게 보이고, 바닥에 내려앉은 뒤 잠깐 있다가
/// 전투 중 알파(거의 안 보임)로 옅어진다. 2단계의 "바닥 코인"만 옅게 한다 - 떨어지는 순간부터 투명하면 벌목 보상이 안 보인다.
/// </summary>
public class RunCoin : MonoBehaviour
{
    private enum EState
    {
        Idle,
        Scattering,
        Grounded,
        Absorbing,
    }

    [SerializeField] private SpriteRenderer[] renderers;
    [Tooltip("흩뿌려질 때 걸리는 시간(초).")]
    [SerializeField] private float scatterDuration = 0.35f;
    [Tooltip("흩뿌려질 때 포물선 높이.")]
    [SerializeField] private float scatterHeight = 0.6f;
    [Tooltip("흡수될 때 처음 속도와 가속도.")]
    [SerializeField] private float absorbStartSpeed = 4f;
    [SerializeField] private float absorbAcceleration = 40f;
    [SerializeField] private float absorbArriveDistance = 0.15f;
    [Tooltip("내려앉은 뒤 불투명한 채로 머무는 시간(초). 그 뒤 전투 중 알파로 옅어진다.")]
    [SerializeField, Min(0f)] private float landedHoldSeconds = 0.35f;
    [Tooltip("전투 중 알파로 옅어지는 시간(초).")]
    [SerializeField, Min(0f)] private float groundFadeSeconds = 0.4f;
    [Tooltip("흡수되어 날아가는 동안 쓸 소팅 레이어와 순서.")]
    [SerializeField] private string absorbSortingLayer = "FlyingItem";
    [SerializeField] private int absorbSortingOrder = 10;

    private CustomSortable sortable;
    private int[] groundSortingLayerIds;

    private EState state = EState.Idle;
    private Vector3 scatterFrom;
    private Vector3 scatterTo;
    private float timer;
    private float absorbSpeed;
    private Transform absorbTarget;
    private Action<RunCoin> arrivedCallback;

    // 내려앉은 뒤 옅어질 알파와 진행. bGroundFading이 꺼져 있으면 알파는 SetAlpha로만 바뀐다.
    private float groundAlpha = 1f;
    private float groundTimer;
    private bool bGroundFading;
    // 내려앉을 때 옅어지기 시작할지. Launch가 켜고 SetAlpha(웨이브 종료 알파 복구)가 끈다 - 흩뿌려지는 도중 복구된 코인이
    // 그 뒤에 내려앉으며 다시 옅어지면 거의 투명한 채로 빨려 들어간다.
    private bool bFadeOnLand;

    public long Value { get; private set; }

    /// <summary>지금 알파(렌더러 공통).</summary>
    public float Alpha { get; private set; } = 1f;

    private void Awake()
    {
        sortable = GetComponent<CustomSortable>();
        if (null == sortable) sortable = gameObject.AddComponent<CustomSortable>();
        sortable.Initialize(transform, renderers);

        int count = null != renderers ? renderers.Length : 0;
        groundSortingLayerIds = new int[count];
        for (int i = 0; i < count; i++)
        {
            if (null != renderers[i]) groundSortingLayerIds[i] = renderers[i].sortingLayerID;
        }
    }

    /// <summary>_from에서 _to로 흩뿌린다. 날아가는 동안은 불투명하고, 내려앉은 뒤 _groundAlpha로 옅어진다.</summary>
    public void Launch(Vector3 _from, Vector3 _to, long _value, float _groundAlpha)
    {
        Value = _value;
        scatterFrom = _from;
        scatterTo = _to;
        timer = 0f;
        state = EState.Scattering;
        transform.position = _from;
        SetAlpha(1f);
        groundAlpha = Mathf.Clamp01(_groundAlpha);
        bFadeOnLand = true;
        RestoreGroundSorting();
        UpdateGroundSorting(0f);
    }

    /// <summary>알파를 바로 정한다. 내려앉은 뒤 옅어지던 중이면 멈춘다(웨이브 종료 알파 복구가 이걸로 덮어쓴다).</summary>
    public void SetAlpha(float _alpha)
    {
        bGroundFading = false;
        bFadeOnLand = false;
        ApplyAlpha(_alpha);
    }

    private void ApplyAlpha(float _alpha)
    {
        Alpha = _alpha;
        if (null == renderers) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (null == renderers[i]) continue;
            Color c = renderers[i].color;
            c.a = _alpha;
            renderers[i].color = c;
        }
    }

    public void BeginAbsorb(Transform _target, Action<RunCoin> _onArrived)
    {
        absorbTarget = _target;
        arrivedCallback = _onArrived;
        absorbSpeed = absorbStartSpeed;
        bGroundFading = false;
        bFadeOnLand = false;
        SetAbsorbSorting();

        // 흩뿌려지는 중이었다면 그 자리에서 바로 흡수로 넘어간다.
        state = EState.Absorbing;
    }

    public void ResetCoin()
    {
        state = EState.Idle;
        absorbTarget = null;
        arrivedCallback = null;
        Value = 0;
        bGroundFading = false;
        bFadeOnLand = false;
        RestoreGroundSorting();
    }

    private void Update()
    {
        switch (state)
        {
            case EState.Scattering:
                UpdateScatter();
                break;
            case EState.Grounded:
                UpdateGroundFade();
                break;
            case EState.Absorbing:
                UpdateAbsorb();
                break;
        }
    }

    private void UpdateScatter()
    {
        timer += Time.deltaTime;
        float t = scatterDuration > 0f ? Mathf.Clamp01(timer / scatterDuration) : 1f;

        Vector3 pos = Vector3.Lerp(scatterFrom, scatterTo, t);
        float height = Mathf.Sin(t * Mathf.PI) * scatterHeight;
        pos.y += height;
        transform.position = pos;

        // 튀어 오른 높이를 빼고 떨어질 자리 기준으로 정렬한다(본편 원목 낙하와 같은 방식). 내려앉은 뒤엔 움직이지 않으므로 다시 계산하지 않는다.
        if (t >= 1f)
        {
            state = EState.Grounded;
            height = 0f;
            groundTimer = 0f;
            bGroundFading = bFadeOnLand;
        }
        UpdateGroundSorting(height);
    }

    /// <summary>내려앉은 뒤 landedHoldSeconds 동안 불투명하게 머물다가 groundFadeSeconds에 걸쳐 전투 중 알파로 옅어진다.</summary>
    private void UpdateGroundFade()
    {
        if (false == bGroundFading) return;

        groundTimer += Time.deltaTime;
        float t = groundFadeSeconds > 0f ? Mathf.Clamp01((groundTimer - landedHoldSeconds) / groundFadeSeconds) : (groundTimer >= landedHoldSeconds ? 1f : 0f);
        ApplyAlpha(Mathf.Lerp(1f, groundAlpha, t));
        if (t >= 1f) bGroundFading = false;
    }

    private void UpdateGroundSorting(float _height)
    {
        if (null == sortable) return;
        sortable.SetHeight(_height);
        sortable.ManualLateUpdate();
    }

    private void SetAbsorbSorting()
    {
        if (null == renderers) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (null == renderers[i]) continue;
            renderers[i].sortingLayerName = absorbSortingLayer;
            renderers[i].sortingOrder = absorbSortingOrder;
        }
    }

    private void RestoreGroundSorting()
    {
        if (null == renderers || null == groundSortingLayerIds) return;
        for (int i = 0; i < renderers.Length && i < groundSortingLayerIds.Length; i++)
        {
            if (null != renderers[i]) renderers[i].sortingLayerID = groundSortingLayerIds[i];
        }
    }

    private void UpdateAbsorb()
    {
        if (null == absorbTarget)
        {
            Arrive();
            return;
        }

        absorbSpeed += absorbAcceleration * Time.deltaTime;

        Vector3 targetPos = absorbTarget.position;
        Vector3 pos = Vector3.MoveTowards(transform.position, targetPos, absorbSpeed * Time.deltaTime);
        transform.position = pos;

        if ((targetPos - pos).sqrMagnitude <= absorbArriveDistance * absorbArriveDistance)
        {
            Arrive();
        }
    }

    private void Arrive()
    {
        state = EState.Idle;
        Action<RunCoin> callback = arrivedCallback;
        arrivedCallback = null;
        callback?.Invoke(this);
    }
}
