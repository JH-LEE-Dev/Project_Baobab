using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로그라이크 전투 HUD의 대쉬 칸(RoguelikeHUD_Polish_Plan.md 5-3). 스태미너 바 오른쪽의 대쉬 아이콘 + 14 x 9 캡슐 칸 최대 2개다.
/// 본편 대쉬 게이지(HUD_DashGauge)는 로그라이크에서 화면에서만 숨고(UIView_HUD가 CanvasGroup alpha 0) 동작은 그대로 이어지며,
/// UIView_HUD가 같은 입력을 이 컴포넌트에도 전달한다(RoguelikeHUDLink.DashCells). 메서드 모양은 HUD_DashGauge와 같다.
///   잠겨 있으면(SetUnlocked false) 아이콘 · 칸 모두 숨는다. 해금되면 등장 연출 없이 바로 나타난다.
///   최대 칸 수가 1이면 두 번째 칸이 숨는다. 칸마다 안쪽 12 x 7을 CPU로 그린 RawImage(RoguelikeBarPainter)가 홈 + 채움을 맡는다. 채움은 정수 px다.
///   충전 완료(SetCharge가 1에 도달): 그 칸 채움이 2틱(24Hz) 동안 WHITE.
/// </summary>
public class UI_RoguelikeDashCells : MonoBehaviour
{
    public const int MaxCells = 2;

    private const int InnerWidth = 12;
    private const int InnerHeight = 7;
    private const int NoTick = -1000000;
    private const int FlashTicks = 2;

    //내부 의존성 (프리팹에서 연결)
    [SerializeField, Tooltip("아이콘 + 칸 전체. 해금되지 않으면 꺼진다.")]
    private GameObject visualRoot;
    [SerializeField, Tooltip("칸 오브젝트(윤곽 스프라이트 + 안쪽 RawImage를 자식으로 가진다). 최대 칸 수에 맞춰 켜고 끈다.")]
    private GameObject[] cellObjects = new GameObject[MaxCells];
    [SerializeField, Tooltip("칸마다 안쪽 12x7을 그리는 RawImage.")]
    private RawImage[] fillSurfaces = new RawImage[MaxCells];

    //상태 변수
    private readonly RoguelikeBarPainter[] painters = new RoguelikeBarPainter[MaxCells];
    private readonly float[] ratios = new float[MaxCells];
    private readonly int[] flashStartTicks = new int[MaxCells];
    private readonly int[] paintedPixels = new int[MaxCells];
    private readonly bool[] bPaintedFlash = new bool[MaxCells];
    private int maxCharges = MaxCells;
    private bool bUnlocked;
    private bool bInitialized;
    private float clock;

    public bool IsUnlocked => bUnlocked;

    /// <summary>대시를 쓸 수 있을 때만 보인다. 등장 연출은 없다.</summary>
    public void SetUnlocked(bool _bUnlocked)
    {
        EnsureInitialized();

        bUnlocked = _bUnlocked;
        if (null != visualRoot) visualRoot.SetActive(_bUnlocked);
    }

    /// <summary>표시할 칸 수(0~2). 1이면 두 번째 칸이 숨는다.</summary>
    public void SetMaxCharges(int _count)
    {
        EnsureInitialized();

        maxCharges = Mathf.Clamp(_count, 0, MaxCells);

        for (int i = 0; i < MaxCells; i++)
        {
            if (null != cellObjects[i]) cellObjects[i].SetActive(i < maxCharges);
        }
    }

    /// <summary>칸 하나의 충전 비율(0~1). 가득 차는 순간 그 칸이 2틱 동안 WHITE로 번쩍인다.</summary>
    public void SetCharge(int _index, float _ratio)
    {
        if (_index < 0 || MaxCells <= _index) return;

        EnsureInitialized();

        float next = Mathf.Clamp01(_ratio);
        float previous = ratios[_index];
        if (Mathf.Approximately(next, previous)) return;

        if (previous < 1f && 1f <= next) flashStartTicks[_index] = CurrentTick();

        ratios[_index] = next;
        Repaint(_index);
    }

    /// <summary>연출 없이 칸 채움만 바꾼다(처음 보이게 될 때의 초기 상태 맞춤용).</summary>
    public void SetChargeSilently(int _index, float _ratio)
    {
        if (_index < 0 || MaxCells <= _index) return;

        EnsureInitialized();

        ratios[_index] = Mathf.Clamp01(_ratio);
        flashStartTicks[_index] = NoTick;
        Repaint(_index);
    }

    /// <summary>모든 칸을 같은 비율로 되돌린다(씬 진입/초기화용).</summary>
    public void ResetCharges(float _ratio)
    {
        for (int i = 0; i < MaxCells; i++) SetChargeSilently(i, _ratio);
    }

    /// <summary>켜고 끈다(퇴장 모션 뒤 숨김). 값과 해금 상태는 그대로 남는다.</summary>
    public void SetVisible(bool _bVisible)
    {
        gameObject.SetActive(_bVisible);
    }

    // // 내부

    private int CurrentTick()
    {
        return (int)(clock * RoguelikeBarPainter.TicksPerSecond);
    }

    /// <summary>처음 쓸 때 한 번: 칸 비율은 가득, 해금 전에는 아이콘 · 칸이 보이지 않는다(HUD_DashGauge와 같은 초기 상태).</summary>
    private void EnsureInitialized()
    {
        if (true == bInitialized) return;

        bInitialized = true;

        for (int i = 0; i < MaxCells; i++)
        {
            ratios[i] = 1f;
            flashStartTicks[i] = NoTick;
            paintedPixels[i] = -1;
        }

        if (null != visualRoot) visualRoot.SetActive(false);
    }

    /// <summary>채움 px와 번쩍임 상태가 바뀌었을 때만 칸을 다시 그린다.</summary>
    private void Repaint(int _index)
    {
        if (null == fillSurfaces[_index]) return;

        int pixels = Mathf.RoundToInt(ratios[_index] * InnerWidth);
        bool bFlash = CurrentTick() - flashStartTicks[_index] < FlashTicks;
        if (pixels == paintedPixels[_index] && bFlash == bPaintedFlash[_index]) return;

        paintedPixels[_index] = pixels;
        bPaintedFlash[_index] = bFlash;

        if (null == painters[_index]) painters[_index] = new RoguelikeBarPainter(fillSurfaces[_index], InnerWidth, InnerHeight, "UI_RoguelikeDashFill" + _index);

        RoguelikeBarPainter painter = painters[_index];
        painter.Clear();
        painter.Groove(-1);
        painter.Run(0, pixels, true == bFlash ? RoguelikeHudColors.Flash : RoguelikeHudColors.Dash, true);
        painter.Apply();
    }

    private void Awake()
    {
        // 첫 프레임에 텍스처 없는 RawImage(흰 네모)가 보이지 않도록 칸을 미리 한 번 그린다.
        EnsureInitialized();

        for (int i = 0; i < MaxCells; i++) Repaint(i);
    }

    private void Update()
    {
        clock += Mathf.Min(Time.unscaledDeltaTime, 1f / 20f);

        // 번쩍임이 끝난 칸은 원래 색으로 되돌린다.
        for (int i = 0; i < MaxCells; i++)
        {
            if (true == bPaintedFlash[i] && CurrentTick() - flashStartTicks[i] >= FlashTicks) Repaint(i);
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < MaxCells; i++)
        {
            if (null != painters[i]) painters[i].Dispose();
        }
    }
}
