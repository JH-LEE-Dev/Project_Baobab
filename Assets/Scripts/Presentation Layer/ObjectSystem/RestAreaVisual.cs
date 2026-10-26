using System.Collections;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// "휴식" 특성의 그림 쪽: 휴식 구역 중앙의 모닥불과 휴식 범위 타원.
/// 휴식 판정은 StaminaRecoverCircle이 하고, 여기서는 열림/닫힘에 맞춰 보여주기만 한다.
/// 이 오브젝트의 위치가 곧 휴식 구역 중심이다. (StaminaRecoverCircle.Initialize가 맞춰 둔다)
/// </summary>
public class RestAreaVisual : MonoBehaviour
{
    [Header("Campfire")]
    [SerializeField] private GameObject campfireObj;
    [Tooltip("튀어나오는 연출로 크기를 바꾸는 건 이 스프라이트뿐이다. 발밑 충돌 타일맵(Rigidbody2D)이 들어 있는 " +
             "campfireObj 자체를 0 크기로 줄이면 물리 쪽이 그 순간의 위치를 기억해 타일맵이 옆으로 밀려난다.")]
    [SerializeField] private SpriteRenderer campfireSR;
    [SerializeField] private CustomSortable campfireSortable;

    [Header("Range Ellipse")]
    [SerializeField] private Renderer ellipseRenderer;

    [Header("Reveal")]
    [SerializeField] private float revealDuration = 0.6f;
    [SerializeField] private float hideDuration = 0.25f;
    [Tooltip("열릴 때 타원·모닥불이 목표 크기를 살짝 넘었다가 돌아오는 정도 (EaseOutBack 계수)")]
    [SerializeField] private float revealOvershoot = 1.8f;
    [Tooltip("타원보다 모닥불이 늦게 튀어나오는 시간")]
    [SerializeField] private float campfireRevealDelay = 0.12f;

    private static readonly int FadeID = Shader.PropertyToID("_Fade");

    private MaterialPropertyBlock ellipseMPB;
    private TilemapFootprintCollider footprintCollider;
    private Vector3 ellipseBaseScale = Vector3.one;
    private Vector3 campfireBaseScale = Vector3.one;

    private bool bOpen = false;
    private float elapsed = 0f;

    private void Awake()
    {
        if (campfireSR != null) campfireBaseScale = campfireSR.transform.localScale;

        if (campfireObj != null)
        {
            footprintCollider = new TilemapFootprintCollider(campfireObj.GetComponentInChildren<Tilemap>(true));
        }

        if (campfireSortable != null && campfireSR != null)
        {
            campfireSortable.Initialize(campfireObj.transform);
            campfireSortable.AddSpriteRenderer(campfireSR);
        }

        // Initialize 전에도 프리팹에 맞춰 둔 크기로 그려지도록 기본값을 잡아 둔다.
        if (ellipseRenderer != null) ellipseBaseScale = ellipseRenderer.transform.localScale;

        ellipseMPB = new MaterialPropertyBlock();
        ApplyClosedState();
    }

    public void Initialize(IEnvironmentProvider _environmentProvider, float _radiusX, float _radiusY)
    {
        footprintCollider?.SetEnvironmentProvider(_environmentProvider);

        ellipseBaseScale = new Vector3(_radiusX * 2f, _radiusY * 2f, 1f);
        if (ellipseRenderer != null)
        {
            ellipseRenderer.transform.localScale = ellipseBaseScale;
        }
    }

    /// <summary>매 프레임 불러도 된다. 상태가 바뀔 때만 동작한다.</summary>
    public void SetOpen(bool _bOpen)
    {
        if (bOpen == _bOpen) return;

        bOpen = _bOpen;
        elapsed = 0f;

        if (bOpen)
        {
            if (campfireObj != null) campfireObj.SetActive(true);
            if (ellipseRenderer != null) ellipseRenderer.enabled = true;
            // 이번 프레임 Update가 이미 지나갔을 수 있으므로 시작 크기(0)를 바로 입혀 둔다.
            ApplyRevealPose();
            RefreshFootprint();
        }
        else
        {
            footprintCollider?.Clear();
            if (campfireObj != null) campfireObj.SetActive(false);
        }
    }

    /// <summary>
    /// 차량이 배치/재배치될 때 모닥불 발밑 타일을 길찾기 불가 타일로 다시 등록한다.
    /// 차량 발밑 등록과 같은 이유로 몇 프레임 뒤 한 번 더 등록해 던전 타일맵 재초기화에 걷어차이지 않게 한다.
    /// </summary>
    public void RefreshFootprint()
    {
        if (!bOpen || footprintCollider == null || !gameObject.activeInHierarchy) return;

        footprintCollider.Register();
        StopCoroutine(nameof(ReregisterFootprintDelayed));
        StartCoroutine(nameof(ReregisterFootprintDelayed));
    }

    private IEnumerator ReregisterFootprintDelayed()
    {
        yield return null;
        yield return null;
        if (bOpen) footprintCollider?.Register();

        yield return new WaitForSeconds(0.2f);
        if (bOpen) footprintCollider?.Register();
    }

    private void OnEnable()
    {
        // 던전에 다시 들어올 때마다 열리는 연출을 처음부터 보여준다.
        // 크기는 다음 Update에서야 바뀌므로, 지금 바로 시작 크기를 입히지 않으면 켜지는 프레임에
        // 지난 원정의 다 큰 모습이 한 번 그려졌다가 0으로 줄어든다.
        elapsed = 0f;
        if (bOpen) ApplyRevealPose();
        RefreshFootprint();
    }

    private void OnDisable()
    {
        footprintCollider?.Clear();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        if (bOpen)
        {
            ApplyRevealPose();
        }
        else if (ellipseRenderer != null && ellipseRenderer.enabled)
        {
            float hideT = hideDuration > 0f ? Mathf.Clamp01(elapsed / hideDuration) : 1f;
            float k = 1f - hideT;
            ApplyEllipse(Mathf.Lerp(0.85f, 1f, k), k);

            if (hideT >= 1f) ApplyClosedState();
        }
    }

    private void LateUpdate()
    {
        if (campfireSortable != null && campfireObj != null && campfireObj.activeSelf)
        {
            campfireSortable.SetHeight(0f);
            campfireSortable.ManualLateUpdate();
        }
    }

    // 열림 연출에서 지금(elapsed) 보여야 할 타원·모닥불 크기를 입힌다.
    private void ApplyRevealPose()
    {
        float ellipseT = revealDuration > 0f ? Mathf.Clamp01(elapsed / revealDuration) : 1f;
        float campfireT = revealDuration > 0f ? Mathf.Clamp01((elapsed - campfireRevealDelay) / revealDuration) : 1f;

        ApplyEllipse(EaseOutBack(ellipseT), Mathf.Clamp01(ellipseT * 2f));
        if (campfireSR != null)
        {
            campfireSR.transform.localScale = campfireBaseScale * EaseOutBack(campfireT);
        }
    }

    private void ApplyEllipse(float _scale, float _fade)
    {
        if (ellipseRenderer == null) return;

        ellipseRenderer.transform.localScale = new Vector3(ellipseBaseScale.x * _scale, ellipseBaseScale.y * _scale, 1f);

        ellipseRenderer.GetPropertyBlock(ellipseMPB);
        ellipseMPB.SetFloat(FadeID, _fade);
        ellipseRenderer.SetPropertyBlock(ellipseMPB);
    }

    private void ApplyClosedState()
    {
        if (ellipseRenderer != null) ellipseRenderer.enabled = false;
        if (campfireObj != null && !bOpen) campfireObj.SetActive(false);
    }

    private float EaseOutBack(float _t)
    {
        float c1 = revealOvershoot;
        float c3 = c1 + 1f;
        float u = _t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
