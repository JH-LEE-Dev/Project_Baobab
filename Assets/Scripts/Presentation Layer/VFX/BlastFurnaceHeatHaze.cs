// ─────────────────────────────────────────────────────────────────────────────
// [죽은 코드] 원석 / 용광로 계열.
//
// SYSTEM_VAR.GEM_ORE_SYSTEM_ENABLED 가 false 라서 이 파일의 코드는 한 줄도 실행되지
// 않는다. 원석이 게임에 들어오는 입구(InDungeonObjectManager.OnTreeDead)가 막혀 있어
// 원석 아이템이 생성되지 않고, 용광로도 열리지 않는다.
//
// 버그 / 회귀 검토 대상에서 제외한다. 동작하지 않는 코드라 여기서 나오는 지적은 의미가 없다.
// 되살릴 때는 Assets/Scripts/Global/System.cs 의 그 스위치를 true 로 바꾸면 되고,
// 그 시점에 이 헤더들도 같이 지워야 한다.
// ─────────────────────────────────────────────────────────────────────────────

using UnityEngine;
using NaughtyAttributes;

/// <summary>
/// 마을 용광로 상단에 부착되어 제련 가동 상태(BlastFurnace.Running)에 따라
/// 열기 아지랑이(Heat Haze) 디스토션 효과를 서서히 켜고(Fade In) 끄는(Fade Out) 비주얼 컴포넌트입니다.
/// 외부 스크립트에서 Play(), Stop(), StopImmediate() 퍼블릭 API를 통해 직접 제어할 수도 있습니다.
/// MaterialPropertyBlock을 사용하여 머티리얼 인스턴싱 없이 Zero GC Alloc으로 _Intensity를 제어합니다.
/// </summary>
public class BlastFurnaceHeatHaze : MonoBehaviour
{
    // //외부 의존성
    [Header("Target Furnace")]
    [Tooltip("상태를 감지할 용광로. 비워두면 부모에서 자동으로 탐색합니다.")]
    [SerializeField] private BlastFurnace blastFurnace;

    [Header("Renderer")]
    [Tooltip("아지랑이 셰이더가 적용된 렌더러. 비워두면 자기 자신에서 탐색합니다.")]
    [SerializeField] private Renderer hazeRenderer;

    [Header("Fade Settings")]
    [Tooltip("제련 시작 시 아지랑이가 완전히 켜질 때까지 걸리는 시간(초)")]
    [SerializeField] private float fadeInDuration = 1.5f;

    [Tooltip("제련 종료 시 아지랑이가 완전히 식어 사라질 때까지 걸리는 시간(초)")]
    [SerializeField] private float fadeOutDuration = 2.5f;

    [Tooltip("최대 왜곡 세기 (0~1)")]
    [Range(0.0f, 1.0f)]
    [SerializeField] private float maxIntensity = 1.0f;

    [Header("Test & Manual Control")]
    [Tooltip("체크 시 실제 용광로 상태를 무시하고 인스펙터/스크립트 수동 제어 상태를 적용합니다.")]
    [SerializeField] private bool isTestMode = false;

    [Tooltip("수동 제어 모드에서의 가동 여부 (Fade In / Out)")]
    [SerializeField] private bool isTestRunning = false;

    [Tooltip("체크 시 게임 화면(Game View) 좌측 상단에 [🔥 아지랑이 토글] 버튼을 띄웁니다.")]
    [SerializeField] private bool showScreenTestButton = false;

    // //내부 의존성
    private static readonly int intensityPropertyId = Shader.PropertyToID("_Intensity");
    private MaterialPropertyBlock propertyBlock;
    private float currentIntensity = 0.0f;
    private bool isInitialized = false;

    // //퍼블릭 초기화 및 제어 메서드

    public void Initialize()
    {
        if (true == isInitialized)
            return;

        isInitialized = true;

        if (null == blastFurnace)
            blastFurnace = GetComponentInParent<BlastFurnace>();

        if (null == hazeRenderer)
            hazeRenderer = GetComponent<Renderer>();

        if (null == propertyBlock)
            propertyBlock = new MaterialPropertyBlock();

        currentIntensity = 0.0f;
        ApplyIntensity(0.0f);
    }

    // //퍼블릭 제어 메서드 (외부 스크립트 호출용 표준 API)

    /// <summary>
    /// 아지랑이를 서서히 켭니다 (Fade In).
    /// 수동 제어 모드로 전환되어 fadeInDuration에 걸쳐 최대 세기로 증가합니다.
    /// </summary>
    [Button("🔥 가동 시작 (Fade In)")]
    public void Play()
    {
        Initialize();
        isTestMode = true;
        isTestRunning = true;
    }

    /// <summary>
    /// 아지랑이를 서서히 끕니다 (Fade Out).
    /// fadeOutDuration에 걸쳐 잔열이 식어가며 완전히 꺼지고 렌더러가 비활성화됩니다.
    /// </summary>
    [Button("❄️ 가동 종료 (Fade Out)")]
    public void Stop()
    {
        Initialize();
        isTestMode = true;
        isTestRunning = false;
    }

    /// <summary>
    /// 페이드 시간 없이 즉시 아지랑이를 끄고 렌더러를 비활성화합니다.
    /// </summary>
    public void StopImmediate()
    {
        Initialize();
        isTestMode = true;
        isTestRunning = false;
        currentIntensity = 0.0f;
        ApplyIntensity(0.0f);
    }

    /// <summary>
    /// 가동 상태(true: 켜기, false: 끄기)를 지정하여 제어합니다.
    /// </summary>
    public void SetRunning(bool _isRunning)
    {
        if (true == _isRunning)
        {
            Play();
        }
        else
        {
            Stop();
        }
    }

    /// <summary>
    /// 외부 수동 제어를 해제하고 다시 용광로(BlastFurnace.Running) 상태를 따르도록 복귀합니다.
    /// </summary>
    [Button("🔄 테스트 모드 해제 (실제 용광로 연동)")]
    public void ResetToFurnace()
    {
        isTestMode = false;
    }

    // //기존 테스트 메서드 호환용 별칭 (인스펙터 우클릭 메뉴)

    [ContextMenu("Test - Start Smelting (Fade In)")]
    public void TestStartSmelting()
    {
        Play();
    }

    [ContextMenu("Test - Stop Smelting (Fade Out)")]
    public void TestStopSmelting()
    {
        Stop();
    }

    [ContextMenu("Test - Reset to Furnace State")]
    public void ResetTestMode()
    {
        ResetToFurnace();
    }

    // //프라이빗 메서드

    private void ApplyIntensity(float _intensity)
    {
        if (null == hazeRenderer)
            return;

        bool _shouldEnable = 0.0001f < _intensity;
        if (_shouldEnable != hazeRenderer.enabled)
        {
            hazeRenderer.enabled = _shouldEnable;
        }

        if (true == _shouldEnable)
        {
            hazeRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(intensityPropertyId, _intensity * maxIntensity);
            hazeRenderer.SetPropertyBlock(propertyBlock);
        }
    }

    // //유니티 이벤트 함수 (Awake, Start, OnDestroy 등 최하단 배치)

    private void Awake()
    {
        Initialize();
    }

    private void Update()
    {
        bool _isRunning = true == isTestMode ? isTestRunning : (null != blastFurnace && blastFurnace.Running);
        float _targetIntensity = true == _isRunning ? 1.0f : 0.0f;

        float _duration = true == _isRunning ? fadeInDuration : fadeOutDuration;
        float _speed = 0.0001f < _duration ? 1.0f / _duration : 100.0f;

        float _nextIntensity = Mathf.MoveTowards(currentIntensity, _targetIntensity, Time.deltaTime * _speed);

        if (0.00001f < Mathf.Abs(currentIntensity - _nextIntensity))
        {
            currentIntensity = _nextIntensity;
            ApplyIntensity(currentIntensity);
        }
    }

#if UNITY_EDITOR
    private void OnGUI()
    {
        if (false == showScreenTestButton)
            return;

        bool _activeState = (true == isTestMode && true == isTestRunning) || (false == isTestMode && null != blastFurnace && blastFurnace.Running);
        string _btnText = true == _activeState ? "[용광로 아지랑이] 🔥 가동 중 (클릭: 정지)" : "[용광로 아지랑이] ❄️ 정지 중 (클릭: 가동)";

        if (GUI.Button(new Rect(10, 10, 240, 36), _btnText))
        {
            if (true == _activeState)
            {
                Stop();
            }
            else
            {
                Play();
            }
        }
    }
#endif
}
