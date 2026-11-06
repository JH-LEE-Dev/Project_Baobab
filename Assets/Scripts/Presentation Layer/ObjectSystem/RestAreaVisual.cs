using System.Collections;
using PresentationLayer.VFX;
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
    [Tooltip("튀어나오는 연출로 크기를 바꾸는 건 이 그림 묶음(뒤·앞·그림자)뿐이다. 발밑 충돌 타일맵(Rigidbody2D)이 들어 있는 " +
             "campfireObj 자체를 0 크기로 줄이면 물리 쪽이 그 순간의 위치를 기억해 타일맵이 옆으로 밀려난다.")]
    [SerializeField] private Transform campfireVisual;
    [Tooltip("모닥불 뒤(Back)·앞(Front) 조각. 둘 다 campfireObj 위치로 정렬하고 각자의 offset으로 앞뒤를 가른다 " +
             "(Back 0 / Front +2, 사이의 +1은 불꽃 이펙트 자리).")]
    [SerializeField] private CustomSortable[] campfireSortables;

    [Header("Range Ellipse")]
    [SerializeField] private Renderer ellipseRenderer;

    [Header("Plus Particle")]
    [Tooltip("범위 안에서 올라오는 + 입자. 모닥불 CustomSortable로 지면 Y 정렬해 사물 사이에 끼어든다.")]
    [SerializeField] private VFX_RestAreaPlus plusVfx;

    [Header("Flame")]
    [Tooltip("모닥불 불꽃 연출. 열리면 campfireRevealDelay 뒤 점화하고, 닫히면 즉시 멈춘다. 모닥불 Back 조각 기준으로 앞뒤를 정렬한다.")]
    [SerializeField] private VFX_CampfireFlame flameVfx;

    [Header("Reveal")]
    [Tooltip("모닥불이 튀어나오는 시간 (EaseOutBack)")]
    [SerializeField] private float revealDuration = 0.6f;
    [Tooltip("닫힐 때 타원이 크기 변화 없이 사라지는 시간")]
    [SerializeField] private float hideDuration = 0.3f;
    [Tooltip("열릴 때 모닥불이 목표 크기를 살짝 넘었다가 돌아오는 정도 (EaseOutBack 계수)")]
    [SerializeField] private float revealOvershoot = 1.8f;
    [Tooltip("타원보다 모닥불이 늦게 튀어나오는 시간")]
    [SerializeField] private float campfireRevealDelay = 0.12f;
    [Tooltip("열릴 때 타원이 완성된 모양 그대로 서서히 드러나는 시간. 크기는 바뀌지 않는다.")]
    [SerializeField] private float ellipseRevealDuration = 0.8f;

    // 첫 파동은 드러나는 시간보다 조금 길게 지나가 외곽에 닿을 때쯤 다 드러나 있게 한다.
    private const float FirstWaveSpanRatio = 1.2f;
    // 다 드러난 뒤 평소 파동이 켜지는 시간. 첫 파동과 평소 파동이 겹쳐 보이지 않게 한다.
    private const float WaveGateDuration = 0.8f;

    private static readonly int FadeID = Shader.PropertyToID("_Fade");
    private static readonly int RevealID = Shader.PropertyToID("_Reveal");
    private static readonly int WaveGateID = Shader.PropertyToID("_WaveGate");

    private MaterialPropertyBlock ellipseMPB;
    private TilemapFootprintCollider footprintCollider;
    private Vector3 campfireBaseScale = Vector3.one;

    private bool bOpen = false;
    private float elapsed = 0f;

    private void Awake()
    {
        if (campfireVisual != null) campfireBaseScale = campfireVisual.localScale;

        if (campfireObj != null)
        {
            footprintCollider = new TilemapFootprintCollider(campfireObj.GetComponentInChildren<Tilemap>(true));
        }

        // 렌더러를 따로 넘기지 않으면 각 조각이 자기 아래 SpriteRenderer만 모은다.
        if (campfireSortables != null && campfireObj != null)
        {
            for (int i = 0; i < campfireSortables.Length; i++)
            {
                if (campfireSortables[i] != null) campfireSortables[i].Initialize(campfireObj.transform);
            }
        }

        // + 입자는 offset 0인 뒤(Back) 조각 기준으로 정렬한다.
        if (null != plusVfx) plusVfx.Initialize(campfireSortables != null && campfireSortables.Length > 0 ? campfireSortables[0] : null);

        // 불꽃도 offset 0인 뒤(Back) 조각 기준으로 정렬한다. (Back 0 / 불꽃 +1 / Front +2)
        if (null != flameVfx) flameVfx.Initialize(campfireSortables != null && campfireSortables.Length > 0 ? campfireSortables[0] : null);

        ellipseMPB = new MaterialPropertyBlock();
        ApplyClosedState();
    }

    public void Initialize(IEnvironmentProvider _environmentProvider, float _radiusX, float _radiusY)
    {
        footprintCollider?.SetEnvironmentProvider(_environmentProvider);

        if (null != plusVfx) plusVfx.SetRadius(_radiusX, _radiusY);

        // 타원 크기는 여기서 한 번 정하고 열림/닫힘 연출에서 바꾸지 않는다. (오브젝트 스케일 = 타원 지름)
        if (ellipseRenderer != null)
        {
            ellipseRenderer.transform.localScale = new Vector3(_radiusX * 2f, _radiusY * 2f, 1f);
        }
    }

    /// <summary>매 프레임 불러도 된다. 상태가 바뀔 때만 동작한다.</summary>
    public void SetOpen(bool _bOpen)
    {
        if (bOpen == _bOpen) return;

        bOpen = _bOpen;
        elapsed = 0f;

        if (null != plusVfx) plusVfx.SetOpen(bOpen);
        if (null != flameVfx) flameVfx.SetOpen(bOpen, campfireRevealDelay);

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
            // 닫힘: 크기 변화 없이 페이드만 한다. 파동은 평소 상태 그대로 둔다.
            float hideT = hideDuration > 0f ? Mathf.Clamp01(elapsed / hideDuration) : 1f;
            ApplyEllipse(1f - hideT, 1f, 1f);

            if (hideT >= 1f) ApplyClosedState();
        }
    }

    private void LateUpdate()
    {
        if (campfireSortables == null || campfireObj == null || !campfireObj.activeSelf) return;

        for (int i = 0; i < campfireSortables.Length; i++)
        {
            if (campfireSortables[i] == null) continue;
            campfireSortables[i].SetHeight(0f);
            campfireSortables[i].ManualLateUpdate();
        }
    }

    // 열림 연출에서 지금(elapsed) 보여야 할 타원 알파·첫 파동과 모닥불 크기를 입힌다.
    // 타원은 완성된 모양 그대로 서서히 드러나고(smoothstep), 그동안 아주 옅은 첫 파동이 한 번 지나간다.
    private void ApplyRevealPose()
    {
        float ellipseT = ellipseRevealDuration > 0f ? Mathf.Clamp01(elapsed / ellipseRevealDuration) : 1f;
        float firstWaveT = ellipseRevealDuration > 0f ? Mathf.Clamp01(elapsed / (ellipseRevealDuration * FirstWaveSpanRatio)) : 1f;
        float waveGateT = Mathf.Clamp01((elapsed - ellipseRevealDuration) / WaveGateDuration);
        float campfireT = revealDuration > 0f ? Mathf.Clamp01((elapsed - campfireRevealDelay) / revealDuration) : 1f;

        ApplyEllipse(Mathf.SmoothStep(0f, 1f, ellipseT), firstWaveT, waveGateT);
        if (campfireVisual != null)
        {
            campfireVisual.localScale = campfireBaseScale * EaseOutBack(campfireT);
        }
    }

    private void ApplyEllipse(float _fade, float _reveal, float _waveGate)
    {
        if (ellipseRenderer == null) return;

        ellipseRenderer.GetPropertyBlock(ellipseMPB);
        ellipseMPB.SetFloat(FadeID, _fade);
        ellipseMPB.SetFloat(RevealID, _reveal);
        ellipseMPB.SetFloat(WaveGateID, _waveGate);
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
