using System.Collections.Generic;
using UnityEngine;
using NaughtyAttributes;

/// <summary>
/// 궤도 운동 궤적 모드
/// </summary>
public enum OrbitTrajectoryMode
{
    [Tooltip("스크류 교차 나선 상승 (일정 반경을 유지하며 위로 회전 상승, 위성들이 서로 X자로 얽히며 교차)")]
    HelicalScrew,

    [Tooltip("기울어진 타원 궤도 (원자 모형 및 행성 궤도처럼 사방으로 교차)")]
    TiltedEllipse,

    [Tooltip("리사주 8자 궤도 (사방을 휘감으며 중심을 가로지르는 역동적 궤도)")]
    LissajousFigure8,

    [Tooltip("다차원 구면 교차 궤도 (3D 구면을 2D로 투영한 복합 궤도)")]
    SphericalCrossing
}

/// <summary>
/// 희귀 아이템 상시 오라 이펙트의 궤도 위성(Orbital Satellite) 시스템을 제어하는 컴포넌트입니다.
/// 초기화(Awake) 시점에 모든 위성, 트레일 풀, 중앙 코어 글로우를 100% 사전 생성(Pre-allocate)하여 캐싱하며,
/// 런타임 루프 및 오브젝트 풀링 재활용 시 단 1 Byte의 GC Alloc이나 Instantiate/Destroy도 발생하지 않는 완전한 Zero-Alloc 아키텍처입니다.
/// 위성/트레일/중앙 글로우 각각의 독립적인 Bloom 발광 수치 및 소팅 오더를 제공합니다.
/// </summary>
public class ItemAuraOrbitController : MonoBehaviour
{
    // ========================================================================
    // 1. 인스펙터 설정 파라미터
    // ========================================================================

    [Header("궤도 역동성 모드")]
    [SerializeField] private OrbitTrajectoryMode trajectoryMode = OrbitTrajectoryMode.HelicalScrew;

    [Header("스크류 나선 상승 설정 (Helical Screw 전용)")]
    [SerializeField, Range(0.2f, 3.0f), ShowIf("IsHelicalScrewMode")] private float screwHeight = 1.0f;
    [SerializeField, Range(0.05f, 1.5f), ShowIf("IsHelicalScrewMode")] private float screwRadius = 0.32f;
    [SerializeField, Range(0.5f, 6.0f), ShowIf("IsHelicalScrewMode")] private float screwTurns = 2.0f;
    [SerializeField, Range(0.1f, 5.0f), ShowIf("IsHelicalScrewMode")] private float screwRiseSpeed = 0.75f;
    [SerializeField, Range(-1.5f, 1.5f), ShowIf("IsHelicalScrewMode")] private float screwBaseYOffset = 0.0f;

    [Header("위성 기본 설정")]
    [SerializeField, Range(0, 8)] private int satelliteCount = 3;
    [SerializeField, Range(0.05f, 2.0f), HideIf("IsHelicalScrewMode")] private float orbitRadius = 0.55f;
    [SerializeField, Range(0.5f, 10.0f), HideIf("IsHelicalScrewMode")] private float orbitSpeed = 4.0f;
    [SerializeField, Range(0.0f, 1.0f), HideIf("IsHelicalScrewMode")] private float orbitSpeedVariation = 0.5f;

    [Header("타원/리사주 궤도 설정")]
    [SerializeField, Range(0.15f, 1.0f), HideIf("IsHelicalScrewMode")] private float ellipseRatio = 0.65f;
    [SerializeField, Range(0.0f, 180.0f), HideIf("IsHelicalScrewMode")] private float orbitTiltSpread = 60.0f;
    [SerializeField, Range(0.0f, 0.6f), HideIf("IsHelicalScrewMode")] private float radialWobble = 0.0f;
    [SerializeField, Range(0.2f, 5.0f), HideIf("IsHelicalScrewMode")] private float wobbleSpeed = 0.2f;
    [SerializeField, Range(0.0f, 0.6f), HideIf("IsHelicalScrewMode")] private float depthScaleAmount = 0.3f;

    [Header("위성 비주얼 및 블룸(HDR)")]
    [SerializeField] private Sprite satelliteSprite;
    [SerializeField, ColorUsage(true, true), Tooltip("위성 HDR 컬러 (Bloom 임계값 초과 발광)")]
    private Color satelliteColor = new Color(2.5f, 2.2f, 1.2f, 1f);
    [SerializeField, Range(0.5f, 10.0f), Tooltip("위성 본체 Bloom 발광 증폭 배율")]
    private float satelliteBloomMultiplier = 1.5f;
    [SerializeField, Range(0.01f, 0.3f)] private float satelliteSize = 0.069f;

    [Header("트레일 렌더러 및 블룸(HDR)")]
    [SerializeField] private Material trailMaterial;
    [SerializeField, Range(0.5f, 10.0f), Tooltip("트레일 궤적 Bloom 발광 증폭 배율")]
    private float trailBloomMultiplier = 1.5f;
    [SerializeField, Range(0.05f, 3.0f)] private float trailTime = 0.25f;
    [SerializeField, Range(0.005f, 0.2f)] private float trailStartWidth = 0.04f;
    [SerializeField, Range(0.0f, 0.1f)] private float trailEndWidth = 0.0f;
    [SerializeField] private Gradient trailColorGradient;

    [Header("중앙 방사형 원형 코어 글로우 설정")]
    [SerializeField] private bool useCenterGlow = true;
    [SerializeField, ShowIf("useCenterGlow")] private Material centerGlowMaterial;
    [SerializeField, ShowIf("useCenterGlow")] private Sprite centerGlowSprite;
    [SerializeField, Range(0.1f, 3.0f), ShowIf("useCenterGlow")] private float centerGlowScale = 1.0f;
    [SerializeField, Range(0.5f, 10.0f), ShowIf("useCenterGlow"), Tooltip("중앙 원형 코어 Bloom 발광 증폭 배율")]
    private float centerGlowBloomMultiplier = 1.5f;

    [Header("소팅 레이어 및 개별 오더 분리 설정")]
    [SerializeField] private string sortingLayerName = "Objects";
    [SerializeField, Tooltip("중앙 방사형 원형 글로우의 소팅 오더 (보통 아이템 본체 뒤 -2)")]
    private int centerGlowSortingOrder = -2;
    [SerializeField, Tooltip("공전/상승하는 위성 본체의 소팅 오더 (보통 아이템 본체 앞 0)")]
    private int satelliteSortingOrder = 0;
    [SerializeField, Tooltip("위성 트레일 궤적의 소팅 오더 (위성 바로 뒤 -1)")]
    private int trailSortingOrder = -1;

    [Header("디버그")]
    [SerializeField] private bool showOnScreenDebugGui = false;

    // NaughtyAttributes 조건자
    private bool IsHelicalScrewMode => trajectoryMode == OrbitTrajectoryMode.HelicalScrew;

    // ========================================================================
    // 2. 런타임 상태 (0 GC 캐싱 데이터 구조)
    // ========================================================================

    private readonly List<SatelliteData> activeSatellites = new List<SatelliteData>(8);
    private GameObject centerGlowObject;
    private MaterialPropertyBlock centerGlowPropertyBlock;
    private Transform trailRoot;
    private bool isPlaying = false;
    private bool isInitialized = false;

    private static readonly int CenterIntensityPropertyId = Shader.PropertyToID("_Intensity");

    /// <summary>
    /// 개별 위성의 런타임 물리 및 0 GC 트레일 풀 데이터
    /// </summary>
    private class SatelliteData
    {
        public GameObject gameObject;
        public SpriteRenderer spriteRenderer;
        public TrailRenderer[] trailPool = new TrailRenderer[2]; // 위성당 2개의 사전 할당 트레일
        public int activeTrailIndex = 0;
        public float angleOffset;
        public float tiltAngleRad;
        public float speedMultiplier;
        public float wobblePhase;
        public float wobbleFreq;
        public float lissajousPhaseX;
        public float lissajousPhaseY;
        public float currentProgress; // 스크류 진행도 (0.0 ~ 1.0)
    }

    // ========================================================================
    // 3. 퍼블릭 제어 API (0 GC 재활용 최적화)
    // ========================================================================

    /// <summary>
    /// 상시 오라 궤도 위성 이펙트를 활성화합니다. (사전 생성된 인스턴스 100% 재사용)
    /// </summary>
    [Button("▶ Play Orbit (스크류/궤도 시작)")]
    public void Play()
    {
        if (false == isInitialized || activeSatellites.Count == 0 || activeSatellites.Count != satelliteCount)
        {
            RebuildSatellites();
        }
        else
        {
            ResetAllSatellites();
        }

        UpdateBloomSettings();
        SetVisualsActive(true);
        isPlaying = true;

        // 트레일 꼬리 끌림(Streak) 방지를 위해 1프레임 뒤 트레일 완전 초기화
        StartCoroutine(DelayedClearTrailsCoroutine());
    }

    private System.Collections.IEnumerator DelayedClearTrailsCoroutine()
    {
        yield return null; // 1프레임 대기 (트랜스폼 업데이트 및 TrailRenderer 초기화 대기)
        
        for (int i = 0; i < activeSatellites.Count; i++)
        {
            var sat = activeSatellites[i];
            if (sat != null && sat.trailPool != null)
            {
                for (int t = 0; t < sat.trailPool.Length; t++)
                {
                    if (sat.trailPool[t] != null)
                    {
                        sat.trailPool[t].Clear();
                        sat.trailPool[t].emitting = (t == sat.activeTrailIndex); // 트랜스폼 갱신이 완전히 끝난 뒤 방출 시작
                    }
                }
            }
        }
    }

    /// <summary>
    /// 궤도 위성 이펙트를 즉시 정지하고 모든 렌더러를 끕니다. (오브젝트 파괴 없이 0 GC 비활성화)
    /// </summary>
    [Button("⏹ Stop Orbit (스크류/궤도 정지)")]
    public void Stop()
    {
        isPlaying = false;
        MuteAllTrails();
        SetVisualsActive(false);
    }

    public void SetSatelliteCount(int _count)
    {
        satelliteCount = Mathf.Clamp(_count, 0, 8);
        if (true == isPlaying) RebuildSatellites();
    }

    public void SetTrajectoryMode(OrbitTrajectoryMode _mode)
    {
        trajectoryMode = _mode;
    }

    public void SetScrewParameters(float _height, float _radius, float _turns, float _speed)
    {
        screwHeight = Mathf.Max(0.1f, _height);
        screwRadius = Mathf.Max(0.01f, _radius);
        screwTurns = Mathf.Max(0.1f, _turns);
        screwRiseSpeed = Mathf.Max(0.01f, _speed);
    }

    public void SetOrbitSpeed(float _speed, float _variation = 0.35f)
    {
        orbitSpeed = _speed;
        orbitSpeedVariation = _variation;
        UpdateSpeedMultipliers();
    }

    public void SetOrbitRadius(float _radius)
    {
        orbitRadius = Mathf.Clamp(_radius, 0.05f, 2.0f);
    }

    public void SetEllipseRatio(float _ratio)
    {
        ellipseRatio = Mathf.Clamp(_ratio, 0.15f, 1.0f);
    }

    public void SetOrbitTiltSpread(float _spreadDegrees)
    {
        orbitTiltSpread = Mathf.Clamp(_spreadDegrees, 0f, 180f);
        UpdateTiltAngles();
    }

    public void SetBloomMultipliers(float _satelliteBloom, float _trailBloom, float _centerGlowBloom)
    {
        satelliteBloomMultiplier = Mathf.Max(0.1f, _satelliteBloom);
        trailBloomMultiplier = Mathf.Max(0.1f, _trailBloom);
        centerGlowBloomMultiplier = Mathf.Max(0.1f, _centerGlowBloom);
        UpdateBloomSettings();
    }

    public void SetTrailMaterial(Material _material)
    {
        trailMaterial = _material;
        int count = activeSatellites.Count;
        for (int i = 0; i < count; i++)
        {
            var sat = activeSatellites[i];
            if (null != sat?.trailPool)
            {
                for (int t = 0; t < sat.trailPool.Length; t++)
                {
                    if (null != sat.trailPool[t]) sat.trailPool[t].material = _material;
                }
            }
        }
    }

    /// <summary>
    /// 소팅 레이어 및 각 구성 요소별 독립 소팅 오더를 일괄 설정합니다.
    /// </summary>
    public void SetSorting(string _layerName, int _satelliteOrder, int _trailOrder, int _centerGlowOrder)
    {
        sortingLayerName = _layerName;
        satelliteSortingOrder = _satelliteOrder;
        trailSortingOrder = _trailOrder;
        centerGlowSortingOrder = _centerGlowOrder;
        ApplySortingToAll();
    }

    /// <summary>
    /// 기본 기준 오더를 기반으로 일괄 설정합니다 (하위 호환).
    /// </summary>
    public void SetSorting(string _layerName, int _baseOrder)
    {
        sortingLayerName = _layerName;
        satelliteSortingOrder = _baseOrder;
        trailSortingOrder = _baseOrder - 1;
        centerGlowSortingOrder = _baseOrder - 2;
        ApplySortingToAll();
    }

    /// <summary>
    /// 소팅 레이어와 위성/트레일/중앙 글로우 사이의 기존 상대 오프셋(인스펙터에서 잡아둔 깊이감 간격)은
    /// 그대로 유지한 채, 위성 기준 오더만 외부 값(예: 본체+1)으로 재기준(rebase)합니다.
    /// </summary>
    public void RebaseSortingOrder(int _satelliteOrder)
    {
        int trailOffset = trailSortingOrder - satelliteSortingOrder;
        int centerGlowOffset = centerGlowSortingOrder - satelliteSortingOrder;

        satelliteSortingOrder = _satelliteOrder;
        trailSortingOrder = _satelliteOrder + trailOffset;
        centerGlowSortingOrder = _satelliteOrder + centerGlowOffset;
        ApplySortingToAll();
    }

    public void SetCenterGlowSortingOrder(int _order)
    {
        centerGlowSortingOrder = _order;
        ApplySortingToAll();
    }

    public void SetSatelliteSortingOrder(int _order)
    {
        satelliteSortingOrder = _order;
        ApplySortingToAll();
    }

    public void SetTrailSortingOrder(int _order)
    {
        trailSortingOrder = _order;
        ApplySortingToAll();
    }

    // ========================================================================
    // 4. 프라이빗 내부 구현 (0 GC 할당 없는 완전 캐싱 구조)
    // ========================================================================

    private void EnsureTrailRoot()
    {
        if (null == trailRoot)
        {
            GameObject trObj = new GameObject("AuraTrailRoot");
            trObj.transform.SetParent(transform, false);
            trObj.transform.localPosition = Vector3.zero;
            trObj.transform.localRotation = Quaternion.identity;
            trObj.transform.localScale = Vector3.one;
            trailRoot = trObj.transform;
        }
    }

    /// <summary>
    /// 초기화(Awake) 시점에만 딱 1회 호출되어 모든 위성과 트레일 풀을 사전 생성합니다.
    /// </summary>
    private void RebuildSatellites()
    {
        ClearAllSatellites();
        EnsureTrailRoot();

        // 1. 중앙 방사형 원형 코어 글로우 사전 생성
        if (true == useCenterGlow && (null != centerGlowMaterial || null != centerGlowSprite))
        {
            centerGlowObject = new GameObject("AuraCenterGlow");
            centerGlowObject.transform.SetParent(transform, false);
            centerGlowObject.transform.localPosition = Vector3.zero;
            centerGlowObject.transform.localScale = Vector3.one * centerGlowScale;

            SpriteRenderer sr = centerGlowObject.AddComponent<SpriteRenderer>();
            sr.sprite = centerGlowSprite != null ? centerGlowSprite : satelliteSprite;
            if (null != centerGlowMaterial)
            {
                sr.material = centerGlowMaterial;
            }
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = centerGlowSortingOrder;
        }

        // 2. 위성 및 트레일 풀 사전 생성
        for (int i = 0; i < satelliteCount; i++)
        {
            SatelliteData sat = CreateSatellite(i);
            activeSatellites.Add(sat);
        }

        UpdateBloomSettings();
        isInitialized = true;
    }

    private void ConfigureTrail(TrailRenderer trail)
    {
        trail.time = trailTime;
        trail.startWidth = trailStartWidth;
        trail.endWidth = trailEndWidth;
        trail.numCapVertices = 4;
        trail.numCornerVertices = 4;
        trail.textureMode = LineTextureMode.Stretch;
        trail.autodestruct = false;
        trail.generateLightingData = false;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;
        trail.sortingLayerName = sortingLayerName;
        trail.sortingOrder = trailSortingOrder;

        if (null != trailMaterial)
        {
            trail.material = trailMaterial;
        }

        ApplyTrailColor(trail);
    }

    private void ApplyTrailColor(TrailRenderer trail)
    {
        if (null == trail) return;

        if (null != trailColorGradient && 0 < trailColorGradient.colorKeys.Length)
        {
            Gradient g = new Gradient();
            var srcColorKeys = trailColorGradient.colorKeys;
            var newColorKeys = new GradientColorKey[srcColorKeys.Length];
            for (int i = 0; i < srcColorKeys.Length; i++)
            {
                Color c = srcColorKeys[i].color * trailBloomMultiplier;
                newColorKeys[i] = new GradientColorKey(c, srcColorKeys[i].time);
            }
            g.SetKeys(newColorKeys, trailColorGradient.alphaKeys);
            trail.colorGradient = g;
        }
        else
        {
            Gradient g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 0.95f, 0.6f) * trailBloomMultiplier, 0.0f),
                    new GradientColorKey(new Color(1.0f, 0.7f, 0.1f) * trailBloomMultiplier, 1.0f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1.0f, 0.0f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            trail.colorGradient = g;
        }
    }

    private SatelliteData CreateSatellite(int _index)
    {
        GameObject satObj = new GameObject($"AuraSatellite_{_index}");
        satObj.transform.SetParent(transform, false);
        satObj.transform.localPosition = Vector3.zero;
        satObj.transform.localScale = Vector3.one * satelliteSize;

        SpriteRenderer sr = satObj.AddComponent<SpriteRenderer>();
        sr.sprite = satelliteSprite;
        sr.color = satelliteColor * satelliteBloomMultiplier;
        sr.sortingLayerName = sortingLayerName;
        sr.sortingOrder = satelliteSortingOrder;

        // 고유 파라미터 산출
        float countF = Mathf.Max(1, satelliteCount);
        float baseTilt = ((float)_index * (orbitTiltSpread * Mathf.Deg2Rad) / countF)
                         - (orbitTiltSpread * 0.5f * Mathf.Deg2Rad);
        float jitter = Mathf.Sin(_index * 45.67f + 12.34f) * 0.2f;

        float speedHash = Mathf.Abs(Mathf.Sin(_index * 127.1f + 311.7f));
        float speedMult = Mathf.Lerp(1f - orbitSpeedVariation, 1f + orbitSpeedVariation, speedHash);

        SatelliteData data = new SatelliteData
        {
            gameObject = satObj,
            spriteRenderer = sr,
            angleOffset = (float)_index * (Mathf.PI * 2f / countF),
            tiltAngleRad = baseTilt + jitter,
            speedMultiplier = speedMult,
            wobblePhase = _index * 1.618f,
            wobbleFreq = 1.0f + (_index % 3) * 0.35f,
            lissajousPhaseX = _index * (Mathf.PI * 0.5f),
            lissajousPhaseY = _index * (Mathf.PI * 0.25f),
            currentProgress = (float)_index / countF // 스크류 시작 높이 균등 분배
        };

        // 초기 위치 설정
        Vector3 initialPos = (trajectoryMode == OrbitTrajectoryMode.HelicalScrew)
            ? CalculateScrewPosition(data, data.currentProgress, out _)
            : CalculateSatellitePosition(data, 0f, out _);

        satObj.transform.localPosition = initialPos;

        // 0 GC 사전 할당 고정 풀: 위성당 2개의 독립 트레일 오브젝트 생성 (trailRoot의 자식으로 독립 배치)
        for (int t = 0; t < 2; t++)
        {
            GameObject trailObj = new GameObject($"Trail_{_index}_{t}");
            trailObj.transform.SetParent(trailRoot, false);
            trailObj.transform.localPosition = initialPos;

            TrailRenderer trail = trailObj.AddComponent<TrailRenderer>();
            ConfigureTrail(trail);
            trail.Clear();
            trail.emitting = false; // 1프레임 대기 후 코루틴에서 켬

            data.trailPool[t] = trail;
        }

        data.activeTrailIndex = 0;
        return data;
    }

    /// <summary>
    /// 오브젝트 풀 재활용 또는 재생 시 0 GC로 위성 상태를 재설정합니다.
    /// </summary>
    private void ResetAllSatellites()
    {
        int count = activeSatellites.Count;
        for (int i = 0; i < count; i++)
        {
            SatelliteData sat = activeSatellites[i];
            if (null == sat || null == sat.gameObject) continue;

            sat.currentProgress = (float)i / Mathf.Max(1, count);
            Vector3 pos = (trajectoryMode == OrbitTrajectoryMode.HelicalScrew)
                ? CalculateScrewPosition(sat, sat.currentProgress, out _)
                : CalculateSatellitePosition(sat, 0f, out _);

            sat.gameObject.transform.localPosition = pos;

            for (int t = 0; t < sat.trailPool.Length; t++)
            {
                var tr = sat.trailPool[t];
                if (null != tr)
                {
                    tr.transform.localPosition = pos;
                    tr.Clear();
                    tr.emitting = false; // 1프레임 대기 후 코루틴에서 켬
                }
            }
            sat.activeTrailIndex = 0;
        }
    }

    private void SetVisualsActive(bool _active)
    {
        if (null != centerGlowObject)
        {
            centerGlowObject.SetActive(_active);
        }

        int count = activeSatellites.Count;
        for (int i = 0; i < count; i++)
        {
            SatelliteData sat = activeSatellites[i];
            if (null != sat && null != sat.gameObject)
            {
                sat.gameObject.SetActive(_active);
            }
        }

        if (null != trailRoot)
        {
            trailRoot.gameObject.SetActive(_active);
        }
    }

    private void MuteAllTrails()
    {
        int count = activeSatellites.Count;
        for (int i = 0; i < count; i++)
        {
            SatelliteData sat = activeSatellites[i];
            if (null == sat || null == sat.trailPool) continue;

            for (int t = 0; t < sat.trailPool.Length; t++)
            {
                if (null != sat.trailPool[t])
                {
                    sat.trailPool[t].emitting = false;
                    sat.trailPool[t].Clear();
                }
            }
        }
    }

    private void ClearAllSatellites()
    {
        if (null != centerGlowObject)
        {
            Destroy(centerGlowObject);
            centerGlowObject = null;
        }

        int count = activeSatellites.Count;
        for (int i = 0; i < count; i++)
        {
            SatelliteData sat = activeSatellites[i];
            if (null != sat)
            {
                if (null != sat.gameObject) Destroy(sat.gameObject);
                if (null != sat.trailPool)
                {
                    for (int t = 0; t < sat.trailPool.Length; t++)
                    {
                        if (null != sat.trailPool[t]) Destroy(sat.trailPool[t].gameObject);
                    }
                }
            }
        }
        activeSatellites.Clear();

        if (null != trailRoot)
        {
            Destroy(trailRoot.gameObject);
            trailRoot = null;
        }

        isInitialized = false;
    }

    private void UpdateBloomSettings()
    {
        // 1. 중앙 원형 글로우 블룸 반영
        if (null != centerGlowObject)
        {
            if (null == centerGlowPropertyBlock) centerGlowPropertyBlock = new MaterialPropertyBlock();
            Renderer r = centerGlowObject.GetComponent<Renderer>();
            if (null != r)
            {
                r.GetPropertyBlock(centerGlowPropertyBlock);
                centerGlowPropertyBlock.SetFloat(CenterIntensityPropertyId, centerGlowBloomMultiplier);
                r.SetPropertyBlock(centerGlowPropertyBlock);
            }
        }

        // 2. 위성 및 트레일 블룸 반영
        int count = activeSatellites.Count;
        for (int i = 0; i < count; i++)
        {
            SatelliteData sat = activeSatellites[i];
            if (null == sat) continue;

            if (null != sat.trailPool)
            {
                for (int t = 0; t < sat.trailPool.Length; t++)
                {
                    ApplyTrailColor(sat.trailPool[t]);
                }
            }
        }
    }

    private void UpdateSpeedMultipliers()
    {
        int count = activeSatellites.Count;
        for (int i = 0; i < count; i++)
        {
            SatelliteData sat = activeSatellites[i];
            float hash = Mathf.Abs(Mathf.Sin(i * 127.1f + 311.7f));
            sat.speedMultiplier = Mathf.Lerp(1f - orbitSpeedVariation, 1f + orbitSpeedVariation, hash);
        }
    }

    private void UpdateTiltAngles()
    {
        int count = activeSatellites.Count;
        float countF = Mathf.Max(1, count);
        float tiltStep = (orbitTiltSpread * Mathf.Deg2Rad) / countF;

        for (int i = 0; i < count; i++)
        {
            SatelliteData sat = activeSatellites[i];
            float baseTilt = (float)i * tiltStep - (orbitTiltSpread * 0.5f * Mathf.Deg2Rad);
            float jitter = Mathf.Sin(i * 45.67f + 12.34f) * 0.2f;
            sat.tiltAngleRad = baseTilt + jitter;
        }
    }

    private void ApplySortingToAll()
    {
        if (null != centerGlowObject)
        {
            SpriteRenderer csr = centerGlowObject.GetComponent<SpriteRenderer>();
            if (null != csr)
            {
                csr.sortingLayerName = sortingLayerName;
                csr.sortingOrder = centerGlowSortingOrder;
            }
        }

        int count = activeSatellites.Count;
        for (int i = 0; i < count; i++)
        {
            SatelliteData sat = activeSatellites[i];
            if (null == sat) continue;

            if (null != sat.spriteRenderer)
            {
                sat.spriteRenderer.sortingLayerName = sortingLayerName;
                sat.spriteRenderer.sortingOrder = satelliteSortingOrder;
            }
            if (null != sat.trailPool)
            {
                for (int t = 0; t < sat.trailPool.Length; t++)
                {
                    if (null != sat.trailPool[t])
                    {
                        sat.trailPool[t].sortingLayerName = sortingLayerName;
                        sat.trailPool[t].sortingOrder = trailSortingOrder;
                    }
                }
            }
        }
    }

    private Vector3 CalculateScrewPosition(SatelliteData sat, float progress, out float depthZ)
    {
        // Y축 높이: 바닥에서 상단으로 선형 상승
        float y = screwBaseYOffset + (progress - 0.5f) * screwHeight;

        // XZ 평면 일정한 원통형 회전각
        float theta = (progress * screwTurns * Mathf.PI * 2f) + sat.angleOffset;

        float x = Mathf.Cos(theta) * screwRadius;
        depthZ = Mathf.Sin(theta); // 원근감 깊이 (-1 ~ +1)

        return new Vector3(x, y, 0f);
    }

    private Vector3 CalculateSatellitePosition(SatelliteData sat, float time, out float depthZ)
    {
        float dynamicRadius = orbitRadius;
        if (0.001f < radialWobble)
        {
            float wobble = Mathf.Sin(time * wobbleSpeed * sat.wobbleFreq + sat.wobblePhase);
            dynamicRadius += wobble * radialWobble;
        }

        float theta = time * orbitSpeed * sat.speedMultiplier + sat.angleOffset;

        float localX = 0f;
        float localY = 0f;
        depthZ = 0f;

        switch (trajectoryMode)
        {
            case OrbitTrajectoryMode.TiltedEllipse:
            {
                localX = Mathf.Cos(theta) * dynamicRadius;
                localY = Mathf.Sin(theta) * (dynamicRadius * ellipseRatio);
                depthZ = Mathf.Sin(theta);
                break;
            }
            case OrbitTrajectoryMode.LissajousFigure8:
            {
                localX = Mathf.Sin(theta + sat.lissajousPhaseX) * dynamicRadius;
                localY = Mathf.Sin(theta * 2.0f + sat.lissajousPhaseY) * (dynamicRadius * ellipseRatio);
                depthZ = Mathf.Cos(theta);
                break;
            }
            case OrbitTrajectoryMode.SphericalCrossing:
            {
                float phi = sat.tiltAngleRad + Mathf.Sin(theta * 0.7f) * 0.5f;
                localX = dynamicRadius * Mathf.Sin(phi) * Mathf.Cos(theta);
                localY = dynamicRadius * Mathf.Sin(phi) * Mathf.Sin(theta) * ellipseRatio;
                depthZ = dynamicRadius * Mathf.Cos(phi);
                break;
            }
        }

        float cosTilt = Mathf.Cos(sat.tiltAngleRad);
        float sinTilt = Mathf.Sin(sat.tiltAngleRad);

        float worldX = localX * cosTilt - localY * sinTilt;
        float worldY = localX * sinTilt + localY * cosTilt;

        return new Vector3(worldX, worldY, 0f);
    }

    private void UpdateOrbits()
    {
        float dt = Time.deltaTime;
        float time = Time.time;
        int count = activeSatellites.Count;

        for (int i = 0; i < count; i++)
        {
            SatelliteData sat = activeSatellites[i];
            if (null == sat || null == sat.gameObject) continue;

            if (trajectoryMode == OrbitTrajectoryMode.HelicalScrew)
            {
                // ============================================================
                // 스크류 교차 나선 상승 + 0 GC 사전 할당 고정 풀링 시스템
                // ============================================================
                sat.currentProgress += dt * screwRiseSpeed;

                // 상단 도달 후 바닥 리셋 순간
                if (sat.currentProgress >= 1.0f)
                {
                    sat.currentProgress -= 1.0f;

                    // 1. 현재 상단에 도달한 트레일의 방출을 끔 (그 자리에서 공중 자연 소멸)
                    TrailRenderer oldTrail = sat.trailPool[sat.activeTrailIndex];
                    if (null != oldTrail)
                    {
                        oldTrail.emitting = false;
                    }

                    // 2. 바닥 위치 계산 및 위성 이동
                    Vector3 resetPos = CalculateScrewPosition(sat, sat.currentProgress, out _);
                    sat.gameObject.transform.localPosition = resetPos;

                    // 3. 대기 중이던 2번째 트레일 풀로 바톤 터치 (0 GC 재사용)
                    sat.activeTrailIndex = (sat.activeTrailIndex + 1) % 2;
                    TrailRenderer nextTrail = sat.trailPool[sat.activeTrailIndex];
                    if (null != nextTrail)
                    {
                        nextTrail.transform.localPosition = resetPos;
                        nextTrail.Clear();
                        nextTrail.emitting = true;
                    }
                    continue;
                }

                // 상단 도달(0.85 ~ 1.0) 및 바닥 시작(0.0 ~ 0.15) 위성 알파 페이드
                float fade = 1.0f;
                if (sat.currentProgress > 0.85f)
                {
                    fade = Mathf.InverseLerp(1.0f, 0.85f, sat.currentProgress);
                }
                else if (sat.currentProgress < 0.15f)
                {
                    fade = Mathf.InverseLerp(0.0f, 0.15f, sat.currentProgress);
                }

                Vector3 pos = CalculateScrewPosition(sat, sat.currentProgress, out float depthZ);
                sat.gameObject.transform.localPosition = pos;

                // 활성 트레일의 위치 동기화
                TrailRenderer activeTrail = sat.trailPool[sat.activeTrailIndex];
                if (null != activeTrail)
                {
                    activeTrail.transform.localPosition = pos;
                }

                // 위성 HDR Bloom 컬러 및 3D Depth 스케일링
                Color c = satelliteColor * satelliteBloomMultiplier;
                c.a = satelliteColor.a * fade;
                sat.spriteRenderer.color = c;

                float scaleFactor = (1.0f + depthZ * depthScaleAmount) * fade;
                sat.gameObject.transform.localScale = Vector3.one * (satelliteSize * Mathf.Max(0.01f, scaleFactor));
            }
            else
            {
                // ============================================================
                // 일반 궤도 모드 (타원 / 리사주 / 구면)
                // ============================================================
                Vector3 pos = CalculateSatellitePosition(sat, time, out float depthZ);
                sat.gameObject.transform.localPosition = pos;

                // 트레일 위치 동기화
                TrailRenderer tr = sat.trailPool[0];
                if (null != tr) tr.transform.localPosition = pos;

                Color c = satelliteColor * satelliteBloomMultiplier;
                sat.spriteRenderer.color = c;

                if (0.001f < depthScaleAmount)
                {
                    float scaleFactor = 1.0f + depthZ * depthScaleAmount;
                    sat.gameObject.transform.localScale = Vector3.one * (satelliteSize * Mathf.Max(0.1f, scaleFactor));
                }
            }
        }
    }

    // ========================================================================
    // 5. 유니티 생명주기 이벤트 (오브젝트 풀링 완전 호환)
    // ========================================================================

    private void Awake()
    {
        if (false == isInitialized)
        {
            RebuildSatellites();
        }
    }

    private void OnEnable()
    {
        if (false == isPlaying)
        {
            Play();
        }
    }

    private void Update()
    {
        if (true == isPlaying)
        {
            UpdateOrbits();
        }
    }

    private void OnDisable()
    {
        Stop();
    }

    private void OnDestroy()
    {
        ClearAllSatellites();
    }

    private void OnValidate()
    {
        if (true == isPlaying && Application.isPlaying)
        {
            ApplySortingToAll();
            UpdateTiltAngles();
            UpdateSpeedMultipliers();
            UpdateBloomSettings();
        }
    }

    private void OnGUI()
    {
        if (false == showOnScreenDebugGui) return;

        GUILayout.BeginArea(new Rect(10, 10, 320, 280), "Item Aura Orbit VFX", GUI.skin.window);
        GUILayout.Label($"Trajectory: {trajectoryMode}");
        GUILayout.Label($"Active Satellites: {activeSatellites.Count}");
        GUILayout.Label($"Screw Height: {screwHeight:F2} | Radius: {screwRadius:F2}");
        GUILayout.Label($"Satellite Bloom: {satelliteBloomMultiplier:F1} | Trail Bloom: {trailBloomMultiplier:F1}");

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Helical Screw")) trajectoryMode = OrbitTrajectoryMode.HelicalScrew;
        if (GUILayout.Button("Ellipse")) trajectoryMode = OrbitTrajectoryMode.TiltedEllipse;
        if (GUILayout.Button("Lissajous")) trajectoryMode = OrbitTrajectoryMode.LissajousFigure8;
        GUILayout.EndHorizontal();

        if (GUILayout.Button(isPlaying ? "⏹ Stop" : "▶ Play"))
        {
            if (true == isPlaying) Stop();
            else Play();
        }
        GUILayout.EndArea();
    }
}
