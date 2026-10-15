using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 대시 충전 게이지(최대 2칸). 평행사변형 칸마다 0~1 비율로 채움을 그린다.
/// 보임/숨김은 "대시를 쓸 수 있는가(원목 보험 증서 보유)"만으로 정하고, 칸 수가 0이어도 숨기지 않는다.
/// 칸 채움은 21칸 정수 도트로 스냅하고, 사용/충전 완료 연출은 DOTween 이징 곡선으로 평가한다(알파 페이드 없음).
/// </summary>
public class HUD_DashGauge : MonoBehaviour
{
    public const int MaxCells = 2;

    // 외부 의존성
    [Header("UI Ref")]
    [Tooltip("보임/숨김 대상. 이 컴포넌트가 있는 오브젝트는 켜 둔다.")]
    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private RectTransform[] cellRects = new RectTransform[MaxCells];
    [SerializeField] private Image[] fillImages = new Image[MaxCells];
    [Tooltip("번쩍임용. 채움 스프라이트를 단색(Replace)으로 칠해 겹친다.")]
    [SerializeField] private Image[] flashImages = new Image[MaxCells];

    [Header("Cell Shape")]
    [Tooltip("채움 스프라이트(24x24) 안에서 채움 픽셀이 시작하는 열과 폭")]
    [SerializeField] private int fillContentX = 1;
    [SerializeField] private int fillContentWidth = 21;
    [SerializeField] private int cellSize = 24;
    [Tooltip("칸 하나의 보이는 폭(스프라이트 캔버스 24 중 실루엣 폭). 보이는 전체 폭 계산에 쓴다.")]
    [SerializeField] private int cellVisibleWidth = 23;

    [Header("Motion")]
    [SerializeField] private float enterDuration = 0.35f;
    [Range(0f, 1f)][SerializeField] private float enterFromScale = 0.4f;
    [SerializeField] private Ease enterEase = Ease.OutBack;
    [SerializeField] private float enterOvershoot = 2.2f;
    [Tooltip("사용/충전 완료 때 칸이 튀는 높이(px)와 시간. 사용은 아래로, 충전 완료는 위로 튄다. 위치는 반올림하지 않고 연속값으로 움직인다.")]
    [SerializeField] private float bounceHeight = 4f;
    [SerializeField] private float bounceTime = 0.3f;
    [SerializeField] private Ease bounceEase = Ease.OutBack;
    [Tooltip("OutBack 등 오버슈트 곡선의 오버슈트 세기. 클수록 반대편으로 더 튕긴다.")]
    [SerializeField] private float bounceOvershoot = 2.5f;
    [SerializeField] private float flashTime = 0.12f;

    // 내부 의존성
    private readonly float[] ratios = new float[MaxCells];
    private readonly float[] bounceAges = new float[MaxCells];
    private readonly float[] bounceDirs = new float[MaxCells];
    private readonly float[] flashAges = new float[MaxCells];
    private readonly float[] flashAmounts = new float[MaxCells];
    private readonly Vector2[] cellBasePos = new Vector2[MaxCells];
    private readonly float[] cellShiftedX = new float[MaxCells];
    private Vector2 rootBasePos;

    // 상태 변수
    private int maxCharges = MaxCells;
    private bool bUnlocked = false;
    private bool bBaseCached = false;
    private float enterAge = -1f;

    public bool IsUnlocked
    {
        get { return bUnlocked; }
    }

    public int MaxCharges
    {
        get { return maxCharges; }
    }

    // 칸과 칸 사이 간격(프리팹의 칸 배치에서 읽는다)
    public float CellPitch
    {
        get
        {
            CacheBase();
            return cellBasePos[MaxCells - 1].x - cellBasePos[MaxCells - 2].x;
        }
    }

    // 현재 칸 수에서 화면에 보이는 가로폭
    public float VisibleWidth
    {
        get { return 0 < maxCharges ? cellVisibleWidth + CellPitch * (maxCharges - 1) : 0f; }
    }

    // 대시를 쓸 수 있을 때만 보인다. 새로 보이게 되는 순간 등장 연출이 나온다.
    public void SetUnlocked(bool _bUnlocked)
    {
        CacheBase();

        if (_bUnlocked == bUnlocked)
        {
            return;
        }

        bUnlocked = _bUnlocked;

        if (null != visualRoot)
        {
            visualRoot.gameObject.SetActive(_bUnlocked);
        }

        if (true == _bUnlocked)
        {
            enterAge = 0f;
            ApplyEnterScale(0f);
        }
        else
        {
            enterAge = -1f;
            ClearEffects();
        }
    }

    // 표시할 칸 수(0~2). 칸 수가 0이어도 게이지 전체를 숨기지 않는다(칸만 없는 상태).
    public void SetMaxCharges(int _count)
    {
        CacheBase();
        maxCharges = Mathf.Clamp(_count, 0, MaxCells);

        // 칸 수가 모자라면 남은 칸을 오른쪽 자리에 둔다(1칸이면 스태미나 바 오른쪽 끝)
        float _shift = (MaxCells - maxCharges) * CellPitch;

        for (int i = 0; i < MaxCells; i++)
        {
            if (null == cellRects[i])
            {
                continue;
            }

            cellRects[i].gameObject.SetActive(i < maxCharges);
            cellRects[i].anchoredPosition = new Vector2(cellBasePos[i].x + _shift, cellBasePos[i].y);
            cellShiftedX[i] = cellBasePos[i].x + _shift;
        }
    }

    // 칸 하나의 충전 비율(0~1). 가득 찼다가 비면 "사용", 가득 차면 "충전 완료" 연출이 자동으로 나온다.
    public void SetCharge(int _index, float _ratio)
    {
        if (_index < 0 || MaxCells <= _index)
        {
            return;
        }

        float _next = Mathf.Clamp01(_ratio);
        float _prev = ratios[_index];

        if (Mathf.Approximately(_next, _prev))
        {
            return;
        }

        if (1f <= _prev && _next < 0.5f)
        {
            // 사용: 비어 버리기 직전의 채움 모양으로 번쩍이고 아래로 눌린다
            PlayFlash(_index, _prev, -1f);
        }
        else if (_prev < 1f && 1f <= _next)
        {
            PlayFlash(_index, 1f, 1f);
        }

        ratios[_index] = _next;
        ApplyFill(_index, _next);
    }

    // 연출 없이 칸 채움만 바꾼다(처음 보이게 될 때의 초기 상태 맞춤용).
    public void SetChargeSilently(int _index, float _ratio)
    {
        if (_index < 0 || MaxCells <= _index)
        {
            return;
        }

        ratios[_index] = Mathf.Clamp01(_ratio);
        ApplyFill(_index, ratios[_index]);
    }

    // 모든 칸을 같은 비율로 되돌린다(씬 진입/초기화용).
    public void ResetCharges(float _ratio)
    {
        for (int i = 0; i < MaxCells; i++)
        {
            ratios[i] = Mathf.Clamp01(_ratio);
            ApplyFill(i, ratios[i]);
        }

        ClearEffects();
    }

    private void CacheBase()
    {
        if (true == bBaseCached)
        {
            return;
        }

        bBaseCached = true;

        if (null != visualRoot)
        {
            rootBasePos = visualRoot.anchoredPosition;
        }

        for (int i = 0; i < MaxCells; i++)
        {
            if (null != cellRects[i])
            {
                cellBasePos[i] = cellRects[i].anchoredPosition;
                cellShiftedX[i] = cellBasePos[i].x;
            }
        }
    }

    private void ApplyFill(int _index, float _ratio)
    {
        if (null == fillImages[_index])
        {
            return;
        }

        float _px = Mathf.Round(_ratio * fillContentWidth);
        fillImages[_index].fillAmount = (fillContentX + _px) / cellSize;
    }

    private void PlayFlash(int _index, float _amountRatio, float _dir)
    {
        flashAges[_index] = 0f;
        bounceAges[_index] = 0f;
        bounceDirs[_index] = _dir;
        flashAmounts[_index] = (fillContentX + Mathf.Round(_amountRatio * fillContentWidth)) / cellSize;

        if (null != flashImages[_index])
        {
            flashImages[_index].fillAmount = flashAmounts[_index];
            flashImages[_index].enabled = true;
        }
    }

    private void ClearEffects()
    {
        for (int i = 0; i < MaxCells; i++)
        {
            flashAges[i] = -1f;
            bounceAges[i] = -1f;

            if (null != flashImages[i])
            {
                flashImages[i].enabled = false;
            }

            if (null != cellRects[i])
            {
                cellRects[i].anchoredPosition = new Vector2(cellShiftedX[i], cellBasePos[i].y);
            }
        }

        if (null != visualRoot)
        {
            visualRoot.localScale = Vector3.one;
        }
    }

    // 칸 폭이 정수 도트가 되도록 배율을 스냅한다.
    private float SnapScale(float _scale)
    {
        float _px = Mathf.Max(1f, Mathf.Round(_scale * cellSize));
        return _px / cellSize;
    }

    private void ApplyEnterScale(float _t)
    {
        if (null == visualRoot)
        {
            return;
        }

        float _s = SnapScale(DOVirtual.EasedValue(enterFromScale, 1f, Mathf.Clamp01(_t), enterEase, enterOvershoot));
        visualRoot.localScale = new Vector3(_s, _s, 1f);
    }

    private void Awake()
    {
        CacheBase();

        for (int i = 0; i < MaxCells; i++)
        {
            ratios[i] = 1f;
            flashAges[i] = -1f;
            bounceAges[i] = -1f;
            ApplyFill(i, 1f);

            if (null != flashImages[i])
            {
                flashImages[i].enabled = false;
            }
        }

        // 해금되기 전에는 보이지 않는다
        if (null != visualRoot)
        {
            visualRoot.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        float _dt = Time.deltaTime;

        if (0f <= enterAge)
        {
            enterAge += _dt;
            ApplyEnterScale(enterAge / enterDuration);

            if (enterDuration <= enterAge)
            {
                enterAge = -1f;
                visualRoot.localScale = Vector3.one;
            }
        }

        for (int i = 0; i < MaxCells; i++)
        {
            if (0f <= flashAges[i])
            {
                flashAges[i] += _dt;

                if (flashTime <= flashAges[i])
                {
                    flashAges[i] = -1f;

                    if (null != flashImages[i])
                    {
                        flashImages[i].enabled = false;
                    }
                }
            }

            if (0f <= bounceAges[i])
            {
                bounceAges[i] += _dt;
                float _t = Mathf.Clamp01(bounceAges[i] / bounceTime);
                float _dy = DOVirtual.EasedValue(bounceHeight * bounceDirs[i], 0f, _t, bounceEase, bounceOvershoot);

                if (null != cellRects[i])
                {
                    cellRects[i].anchoredPosition = new Vector2(cellShiftedX[i], cellBasePos[i].y + _dy);
                }

                if (1f <= _t)
                {
                    bounceAges[i] = -1f;
                }
            }
        }
    }
}
