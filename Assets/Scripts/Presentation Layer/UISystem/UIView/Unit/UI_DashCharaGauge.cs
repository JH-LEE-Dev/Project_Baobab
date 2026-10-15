using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 발 밑 중앙에 뜨는 작은 대시 충전 게이지(최대 2칸). 데이터는 IDashState(해금/찬 횟수/최대 횟수/진행도)를 그대로 읽는다.
///
/// 보임 규칙
///  - 해금(bUnlocked)은 "보일 수 있는 조건"일 뿐이다. 해금 직후 모두 차 있으면 보이지 않는다.
///  - 대시를 쓰면 알파 1로 즉시 켜지고 사용 연출(번쩍임)이 나온다. 충전 중에는 계속 보인다.
///  - 충전이 모두 차면 완료 연출(번쩍임)과 함께 곧바로 fadeDuration 동안 알파가 떨어져 사라진다(유지 시간 없음).
///  - 감소 중에 다시 쓰면 감소를 멈추고 알파 1로 즉시 돌아온다.
/// 타이머는 일시정지(timeScale 0)에도 멈추지 않도록 unscaledDeltaTime을 쓴다.
/// </summary>
public class UI_DashCharaGauge : MonoBehaviour
{
    public const int MaxCells = 2;

    private enum VisState
    {
        Hidden = 0,
        Showing,
        Fading,
    }

    // 내부 의존성
    [Header("UI Ref")]
    [Tooltip("알파를 내릴 그룹. 켜짐은 1, 사라짐은 0이다.")]
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform[] cellRects = new RectTransform[MaxCells];
    [SerializeField] private Image[] fillImages = new Image[MaxCells];
    [Tooltip("번쩍임용. 채움 스프라이트를 단색(Replace)으로 칠해 겹친다.")]
    [SerializeField] private Image[] flashImages = new Image[MaxCells];

    [Header("Cell Shape")]
    [Tooltip("스프라이트 캔버스 한 변(px)")]
    [SerializeField] private int cellSize = 24;
    [Tooltip("채움 스프라이트 안에서 채움 픽셀이 시작하는 열과 폭")]
    [SerializeField] private int fillContentX = 9;
    [SerializeField] private int fillContentWidth = 7;
    [Tooltip("두 칸이 빗변으로 맞물리는 간격(px). 5면 바깥 선이 한 줄로 겹쳐 밀착해 보인다.")]
    [SerializeField] private float cellPitch = 5f;
    [Tooltip("보이는 영역의 중심이 캔버스 가운데에서 벗어난 양(px). 가운데 정렬 보정.")]
    [SerializeField] private Vector2 contentCenterShift = new Vector2(0.5f, -0.5f);

    [Header("Follow")]
    [Tooltip("월드 좌표에서 캐릭터 위치 기준 오프셋(발 밑 중앙). 월드 1칸 = 32px.")]
    [SerializeField] private Vector2 followOffset = new Vector2(0f, -0.2f);

    [Header("Visibility")]
    [Tooltip("모두 찬 즉시 알파가 서서히 떨어져 사라지는 시간(초). 0.3초 이내 권장.")]
    [SerializeField] private float fadeDuration = 0.25f;
    [SerializeField] private Ease fadeEase = Ease.Linear;

    [Header("Motion")]
    [Tooltip("사용/충전 완료 때 칸이 번쩍이는 시간(초)")]
    [SerializeField] private float flashTime = 0.12f;

    private readonly float[] flashAges = new float[MaxCells];

    // 외부 의존성
    private IDashState dashState;
    private Transform followTarget;
    private RectTransform rootRect;

    // 상태 변수
    private VisState visState = VisState.Hidden;
    private float stateTimer = 0f;
    private bool bUnlocked = false;
    private int shownCharges = 0;
    private int shownMax = 0;

    // 읽기 전용 상태(검증/디버그용)
    public bool IsVisible
    {
        get { return VisState.Hidden != visState; }
    }

    public float CurrentAlpha
    {
        get { return null != canvasGroup ? canvasGroup.alpha : 0f; }
    }

    // 캐릭터 발 밑을 따라간다. 대상이 없으면 제자리에 둔다.
    public void SetTarget(Transform _target, Vector2 _offset)
    {
        followTarget = _target;
        followOffset = _offset;
    }

    // 대시 상태 구독. null이면 구독만 해제하고 숨긴다.
    public void Bind(IDashState _dashState)
    {
        Unbind();

        dashState = _dashState;

        if (null == dashState)
        {
            HideImmediate();
            return;
        }

        dashState.DashStateChangedEvent += OnDashStateChanged;

        // 첫 반영: 연출 없이 현재 상태에 맞춘다. 해금 + 모두 참이면 숨김, 충전 중이면 보임.
        bUnlocked = dashState.bUnlocked;
        shownMax = dashState.MaxCharges;
        shownCharges = dashState.CurrentCharges;
        LayoutCells();
        ApplyFills();

        if (false == bUnlocked || shownMax <= 0 || shownMax <= shownCharges)
            HideImmediate();
        else
            ShowImmediate(VisState.Showing);
    }

    public void Unbind()
    {
        if (null != dashState)
        {
            dashState.DashStateChangedEvent -= OnDashStateChanged;
            dashState = null;
        }
    }

    // ===== 상태 전이 =====

    private void OnDashStateChanged()
    {
        if (null == dashState)
            return;

        bool _bPrevUnlocked = bUnlocked;
        int _prevCharges = shownCharges;
        int _prevMax = shownMax;

        bUnlocked = dashState.bUnlocked;
        shownMax = dashState.MaxCharges;
        shownCharges = dashState.CurrentCharges;

        if (_prevMax != shownMax)
            LayoutCells();

        // 대시를 쓸 수 없는 상태(전리품 미보유, 마을, 로그라이크)에서는 보이지 않는다.
        if (false == bUnlocked || shownMax <= 0)
        {
            ApplyFills();
            HideImmediate();
            return;
        }

        bool _bUsed = _bPrevUnlocked && shownCharges < _prevCharges;
        bool _bCompleted = _bPrevUnlocked && _prevCharges < shownCharges;

        if (true == _bUsed)
        {
            // 사용: 알파 1로 즉시 켜지고, 비워진 칸에 사용 연출
            PlayCellFlash(Mathf.Clamp(shownCharges, 0, MaxCells - 1));
            ShowImmediate(VisState.Showing);
        }
        else if (true == _bCompleted)
        {
            PlayCellFlash(Mathf.Clamp(shownCharges - 1, 0, MaxCells - 1));

            if (VisState.Hidden == visState)
                ShowImmediate(VisState.Showing);
        }
        else if (false == _bPrevUnlocked && shownCharges < shownMax)
        {
            // 막 해금되었는데 이미 충전 중이면 보인다(모두 차 있으면 보이지 않는다)
            ShowImmediate(VisState.Showing);
        }

        // 모두 차면 곧바로 알파가 떨어져 사라진다. 충전 중이면 계속 보인다.
        if (shownMax <= shownCharges)
        {
            if (VisState.Showing == visState)
                BeginFade();
        }
        else if (VisState.Fading == visState)
        {
            ShowImmediate(VisState.Showing);
        }

        ApplyFills();
    }

    private void ShowImmediate(VisState _state)
    {
        visState = _state;
        stateTimer = 0f;
        SetAlpha(1f);
    }

    private void BeginFade()
    {
        visState = VisState.Fading;
        stateTimer = 0f;
        SetAlpha(1f);
    }

    private void HideImmediate()
    {
        visState = VisState.Hidden;
        stateTimer = 0f;
        SetAlpha(0f);
        ClearEffects();
    }

    private void SetAlpha(float _alpha)
    {
        if (null != canvasGroup)
            canvasGroup.alpha = _alpha;
    }

    // ===== 배치와 채움 =====

    // 칸 수에 맞춰 가운데 정렬로 배치한다. 보이지 않는 칸은 끈다.
    private void LayoutCells()
    {
        float _span = (Mathf.Max(1, shownMax) - 1) * cellPitch;

        for (int i = 0; i < MaxCells; i++)
        {
            if (null == cellRects[i])
                continue;

            cellRects[i].gameObject.SetActive(i < shownMax);

            // 보이는 영역 전체의 중심이 루트 중심에 오도록: 칸 i의 중심 = i*pitch - span/2, 캔버스 안 내용 중심 보정을 뺀다.
            // 위치는 항상 정수 아트px로 맞춘다. 반 픽셀 어긋나면 게이지 도트가 월드 도트 격자와 엇갈려 굵고 얇은 도트가 생긴다.
            // 칸 실루엣 폭이 홀수(9px)라 한 칸은 정확한 중앙이 정수 격자에 없다. 이상 위치가 .5일 때는 내림(Floor)해 왼쪽으로 붙인다
            // (Round는 .5를 0으로 올려 항상 오른쪽으로 치우쳤다). 2칸은 이상 위치가 정수라 그대로 중앙이다.
            float _x = Mathf.Floor(i * cellPitch - _span * 0.5f - contentCenterShift.x);
            float _y = Mathf.Round(-contentCenterShift.y);
            cellRects[i].anchoredPosition = new Vector2(_x, _y);
        }

    }

    // 찬 칸은 1, 다음에 차오르는 칸은 진행도, 그 뒤 칸은 0
    private void ApplyFills()
    {
        float _progress = null != dashState ? Mathf.Clamp01(dashState.RechargeProgress) : 0f;

        for (int i = 0; i < MaxCells; i++)
        {
            float _ratio = 0f;

            if (i < shownCharges)
                _ratio = 1f;
            else if (i == shownCharges && shownCharges < shownMax)
                _ratio = _progress;

            SetFill(i, _ratio);
        }
    }

    private void SetFill(int _index, float _ratio)
    {
        if (null == fillImages[_index])
            return;

        float _px = Mathf.Round(Mathf.Clamp01(_ratio) * fillContentWidth);
        fillImages[_index].fillAmount = (fillContentX + _px) / cellSize;
    }

    private void PlayCellFlash(int _index)
    {
        flashAges[_index] = 0f;

        if (null != flashImages[_index])
        {
            // 사용이면 비기 전의 채움 모양(가득), 완료면 가득 찬 모양으로 번쩍인다
            flashImages[_index].fillAmount = (fillContentX + fillContentWidth) / (float)cellSize;
            flashImages[_index].enabled = true;
        }
    }

    private void ClearEffects()
    {
        for (int i = 0; i < MaxCells; i++)
        {
            flashAges[i] = -1f;

            if (null != flashImages[i])
                flashImages[i].enabled = false;
        }
    }

    private void UpdateEffects(float _dt)
    {
        for (int i = 0; i < MaxCells; i++)
        {
            if (0f <= flashAges[i])
            {
                flashAges[i] += _dt;

                if (flashTime <= flashAges[i])
                {
                    flashAges[i] = -1f;

                    if (null != flashImages[i])
                        flashImages[i].enabled = false;
                }
            }
        }
    }

    private void Awake()
    {
        rootRect = (RectTransform)transform;

        for (int i = 0; i < MaxCells; i++)
        {
            flashAges[i] = -1f;

            if (null != flashImages[i])
                flashImages[i].enabled = false;
        }

        SetAlpha(0f);
    }

    private void Update()
    {
        if (VisState.Hidden == visState)
            return;

        float _dt = Time.unscaledDeltaTime;

        // 충전 진행도는 매 프레임 바뀐다
        ApplyFills();
        UpdateEffects(_dt);

        if (VisState.Fading == visState)
        {
            stateTimer += _dt;
            float _t = Mathf.Clamp01(stateTimer / Mathf.Max(0.01f, fadeDuration));
            SetAlpha(DOVirtual.EasedValue(1f, 0f, _t, fadeEase));

            if (1f <= _t)
                HideImmediate();
        }
    }

    private void LateUpdate()
    {
        if (null == followTarget || null == rootRect)
            return;

        // 발 밑 중앙. 1/32 월드 격자에 맞춰 캐릭터가 움직여도 도트가 흔들리지 않게 한다.
        Vector2 _p = (Vector2)followTarget.position + followOffset;
        float _x = Mathf.Round(_p.x * 32f) / 32f;
        float _y = Mathf.Round(_p.y * 32f) / 32f;
        rootRect.position = new Vector3(_x, _y, 0f);
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
