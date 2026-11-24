using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 HUD 우상단의 보관함 트레이. 기획서 10-2 "보유 카드 보관함 - 기본 상태(접힘)": 겹친 카드 묶음 아이콘 + 보유 카드 총량 숫자가 든 숫자판 하나.
/// 표시 전용이다 - 마우스를 받지 않고(레이캐스트 그래픽 없음) 누르거나 올려도 반응하지 않는다. 보유 카드 창은 키(인벤토리 키)로만 연다.
///
/// [숫자] 레어 · 일반 장수는 SetCounts로 받아 합계(총량) 하나로 보여 준다. 레어 · 일반 구분은 보유 카드 창이 보여 준다. 값을 받기 전(HUD가 켜질 때, ResetTray)에는 0이다. 카드가 0장이어도 0으로 표기한다.
///   숫자는 버프 HUD와 같은 작은 숫자 글리프(UI_RoguelikeIconNumber)다.
///
/// [창 위로 올림] 보유 카드 창은 HUD보다 위 캔버스(Overlay, sort 100)에 뜨고 딤이 HUD를 덮는다. 트레이 연출이 가려지지 않도록 창이 열려 있는 동안
///   창 캔버스의 맨 위 레이어(AttachTo)로 트레이를 옮기고, 닫기 도착 직후 원래 자리(Detach)로 돌려보낸다. 앵커 · 위치 값은 그대로라 화면 위치가 변하지 않는다(두 캔버스의 스케일러 설정이 같다).
///
/// [연출] 보유 카드 창의 열기 · 닫기와 짝을 이룬다(시간표는 RoguelikeOwnedCardsMotion, 창과 같은 시작 시각을 쓴다). 반투명 · 입자 · 탄성 없이 정수 픽셀과 프레임 단위 자세 교체만 쓴다. unscaled 시간이다(창이 열려 있는 동안 게임이 멈춘다).
///   보간 대신 손으로 그린 프레임 표다: 시뮬레이션에서 확정한 ms 표(연출 속도 2.2배 기준)를 MsPerUnit으로 연출 단위에 맞춘다. 키 아이콘은 단독으로 움직이지 않는다(판이 가라앉을 때 같이 내려갈 뿐이다). 보관 도착 반응(Store)에서만 앞 카드 아이콘 · 숫자가 따로 튄다.
///   열 때 (PlayOpenEffect) · 닫을 때 (PlayAbsorbEffect)  키를 누른 즉시 같은 반응이 나온다: 판 1칸 가라앉음 + 테두리 흰색 점멸 → 판 복귀 · 테두리 살구색 → 평소. 창이 트레이 칸에 닿는 도착 순간에는 반응하지 않는다.
///   테두리 점멸은 판 스프라이트 교체(plateSprites)다.
///
/// [보관 도착 반응 - 카드 선택 화면의 보관 연출(UI_RoguelikeCardStoreDrop)이 끝나는 순간] 30fps 틱 6개(0.2초, 연출 속도 배율 없는 unscaled 시간).
///   판이 가라앉았다 올라오고(강: 2 · 2 · 1 · -1 · -1 · 0px, 약: 1 · 1 · 0px), 앞 카드 아이콘이 위로 튀고(0 · 2 · 3 · 2 · 1 · 0px), 숫자가 위로 튀며(강만 0 · 3 · 3 · 2 · 1 · 0px), 테두리가 흰색(강) 또는 살구색(약) → 살구색 → 평소로 돌아온다.
///   강 = 보유 카드 총량이 늘었다. 약 = 레벨 업이라 숫자가 그대로다.
///   숫자 잡기: 시스템은 보유 카드 갱신(SetCounts)을 구매 신호(카드 선택 화면의 PlayPurchase)보다 같은 프레임에 먼저 보낸다. 새 값이 연출보다 먼저 보이지 않도록
///   HoldPreviousCount가 숫자를 직전 값으로 되돌려 잡고, 반응이 시작되는 순간 ReleaseCountWithEffect가 새 값을 그린다. 강제 닫힘 · 초기화 · 트레이가 다시 켜질 때는 반응 없이 풀린다(ReleaseCount).
/// </summary>
public class UI_RoguelikeOwnedCardTray : MonoBehaviour
{
    /// <summary>
    /// 숫자판 크기. 구성기(RoguelikeHUDBuilder)가 그림을 만들 때도 쓴다. 판 그림은 34x19이고 스프라이트는 가로 세로가 짝수가 되도록 위쪽에 투명 1행이 붙은 34x20이다
    /// (PlateHeight = 스프라이트 높이, PlateArtHeight = 그림 높이). 스프라이트 왼쪽 아래가 기준이라 그림은 예전과 같은 자리에 선다.
    /// </summary>
    public const int PlateWidth = 34;
    public const int PlateArtHeight = 19;
    public const int PlateHeight = PlateArtHeight + 1;

    /// <summary>연출 단위 1이 몇 ms인지(연출 속도 2.2배에서 확정한 프레임 표 기준). 표의 ms 값을 연출 단위 시각에 맞춘다.</summary>
    private const float MsPerUnit = 1000f / 2.2f;

    // 반응 프레임 경계(키 입력부터 ms). 열기 · 닫기가 같은 표를 쓴다.
    private const float ReactPressEnd = 60f;
    private const float ReactFlashEnd = 110f;

    // 보관 도착 반응(Store): 30fps 틱 표. 연출 속도 배율(RoguelikeOwnedCardsMotion.Speed)을 쓰지 않는 실제 시간이다.
    private const float StoreTicksPerSecond = 30f;
    private const int StoreTicks = 6;
    // 판 내림(px, 아래가 +). 강 = 총량이 늘었다, 약 = 레벨 업(숫자 그대로).
    private static readonly int[] StoreDropStrong = { 2, 2, 1, -1, -1, 0 };
    private static readonly int[] StoreDropWeak = { 1, 1, 0, 0, 0, 0 };
    // 앞 카드 아이콘 · 숫자가 위로 튀는 높이(px). 숫자는 강일 때만 튄다.
    private static readonly int[] StoreIconHop = { 0, 2, 3, 2, 1, 0 };
    private static readonly int[] StoreNumberJump = { 0, 3, 3, 2, 1, 0 };
    // 테두리(plateSprites 인덱스: 0 = 기본, 1 = 살구색, 2 = 흰색).
    private static readonly int[] StoreOutlineStrong = { 2, 2, 1, 1, 0, 0 };
    private static readonly int[] StoreOutlineWeak = { 1, 1, 1, 1, 0, 0 };

    private enum EMotion
    {
        None,
        React,
        Store,
    }

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private UI_RoguelikeIconNumber totalNumber;
    [SerializeField] private Image plateImage;
    [SerializeField, Tooltip("0 = 기본, 1 = 약한 점멸(살구색 테두리), 2 = 강한 점멸(흰색 테두리)")] private Sprite[] plateSprites = new Sprite[0];
    [SerializeField] private RectTransform keyIcon;
    [SerializeField, Tooltip("판 위의 앞 카드 아이콘. 보관 도착 반응에서 위로 튄다.")] private RectTransform frontCardIcon;

    //상태 변수
    private RectTransform plateRect;
    private RectTransform numberRect;
    private Vector2 plateBase;
    private Vector2 keyBase;
    private Vector2 iconBase;
    private Vector2 numberBase;
    private Transform homeParent;
    private int homeSiblingIndex;
    private EMotion motion;
    private float motionClock;
    private bool bAttached;
    private bool bReady;
    private int poseDrop;
    private int poseOutline;
    private int poseIconHop;
    private int poseNumberJump;
    private int shownTotal;
    private int previousTotal;
    private int latestTotal;
    private int countFrame = -1;
    private bool bCountHeld;
    private bool bStoreGain;

    /// <summary>트레이 칸의 사각형. 보유 카드 창이 닫힐 때 이 칸으로 줄어든다.</summary>
    public RectTransform TrayRect => (RectTransform)transform;

    /// <summary>레어(케이스) · 일반(슬리브) 장수. 합계를 숫자판에 그린다. 값이 바뀔 때만 부르면 된다. 숫자를 잡아 둔 동안에는 값만 기억하고 그리지 않는다.</summary>
    public void SetCounts(int _rareCount, int _normalCount)
    {
        previousTotal = latestTotal;
        latestTotal = _rareCount + _normalCount;
        countFrame = Time.frameCount;

        if (true == bCountHeld) return;

        DrawTotal(latestTotal);
    }

    /// <summary>
    /// 숫자를 직전 값으로 되돌려 그리고 잡아 둔다(카드 선택 화면의 PlayPurchase가 부른다). 시스템이 SetCounts를 같은 프레임에 먼저 보내므로 새 값이 연출보다 먼저 보이지 않게 한다.
    /// 이번 프레임에 SetCounts가 오지 않았다면(레벨 업처럼 보유 카드 구성이 그대로) 지금 값을 그대로 잡는다. ReleaseCountWithEffect · ReleaseCount가 풀 때까지 SetCounts는 값만 기억한다.
    /// </summary>
    public void HoldPreviousCount()
    {
        bCountHeld = true;
        DrawTotal(countFrame == Time.frameCount ? previousTotal : latestTotal);
    }

    /// <summary>잡아 둔 숫자를 풀고 새 값을 그린 뒤 도착 반응을 시작한다. 숫자가 늘었으면 강한 반응, 같으면 약한 반응이다. 잡혀 있지 않으면 아무 일도 없다.</summary>
    public void ReleaseCountWithEffect()
    {
        if (false == bCountHeld) return;

        bCountHeld = false;
        bStoreGain = shownTotal < latestTotal;
        DrawTotal(latestTotal);

        if (false == isActiveAndEnabled || false == EnsureReady()) return;

        // [효과음 지점] 카드 보관 도착 - 전용 SoundID 필요(Application Layer)
        motion = EMotion.Store;
        motionClock = 0f;
        EvaluateStore(0);
    }

    /// <summary>반응 없이 잡아 둔 숫자를 풀고 최신 값을 그린다(강제 닫힘). 잡혀 있지 않으면 아무 일도 없다.</summary>
    public void ReleaseCount()
    {
        if (false == bCountHeld) return;

        bCountHeld = false;
        DrawTotal(latestTotal);
    }

    /// <summary>런 시작·종료 초기화. 0 · 0으로 되돌리고(직전 · 최신 값도 0) 연출도 멈춘다.</summary>
    public void ResetTray()
    {
        bCountHeld = false;
        SetCounts(0, 0);
        previousTotal = 0;
        latestTotal = 0;
        ResetEffect();
    }

    public void SetVisible(bool _bVisible)
    {
        gameObject.SetActive(_bVisible);
    }

    /// <summary>트레이를 _layer(보유 카드 창 캔버스의 맨 위 레이어)로 옮긴다. 이미 옮겨졌으면 아무 일도 없다. 화면 위치는 변하지 않는다.</summary>
    public void AttachTo(RectTransform _layer)
    {
        if (true == bAttached || null == _layer) return;

        homeParent = transform.parent;
        homeSiblingIndex = transform.GetSiblingIndex();
        transform.SetParent(_layer, false);
        bAttached = true;
    }

    /// <summary>AttachTo로 옮긴 트레이를 원래 자리(HUD)로 되돌린다. 옮겨진 적이 없으면 아무 일도 없다.</summary>
    public void Detach()
    {
        if (false == bAttached) return;

        bAttached = false;
        Transform parent = homeParent;
        homeParent = null;
        if (null == parent) return;

        transform.SetParent(parent, false);
        transform.SetSiblingIndex(homeSiblingIndex);
    }

    /// <summary>창이 열리기 시작할 때 부른다.</summary>
    public void PlayOpenEffect()
    {
        if (false == isActiveAndEnabled || false == EnsureReady()) return;

        motion = EMotion.React;
        motionClock = 0f;
        EvaluateReact(0f);
    }

    /// <summary>창이 닫히기 시작할 때 부른다. 열 때와 같은 반응이 키 입력 즉시 나온다(창이 트레이 칸에 닿는 도착 순간에는 반응하지 않는다).</summary>
    public void PlayAbsorbEffect()
    {
        PlayOpenEffect();
    }

    /// <summary>연출을 즉시 멈추고 평소 자세로 되돌린다(창이 연출 없이 닫힐 때, 런 초기화). 잡아 둔 숫자도 반응 없이 푼다.</summary>
    public void ResetEffect()
    {
        ReleaseCount();
        motion = EMotion.None;
        if (true == bReady) ApplyRest();
    }

    // // 연출 계산

    private void DrawTotal(int _total)
    {
        shownTotal = _total;
        if (null != totalNumber) totalNumber.SetSingle(_total);
    }

    /// <summary>보관 도착 반응의 _tick(0 ~ 5)번째 틱 자세. 강 · 약은 bStoreGain이 정한다.</summary>
    private void EvaluateStore(int _tick)
    {
        int tick = Mathf.Clamp(_tick, 0, StoreTicks - 1);

        ClearPose();

        poseDrop = true == bStoreGain ? StoreDropStrong[tick] : StoreDropWeak[tick];
        poseOutline = true == bStoreGain ? StoreOutlineStrong[tick] : StoreOutlineWeak[tick];
        poseIconHop = StoreIconHop[tick];
        poseNumberJump = true == bStoreGain ? StoreNumberJump[tick] : 0;

        ApplyPose();
    }

    /// <summary>키를 누른 순간부터의 반응: 판이 1칸 가라앉고 테두리가 흰색으로 번쩍이고, 판이 돌아오며 살구색을 거쳐 평소로 돌아온다. 창이 열릴 때도 닫힐 때도 같다.</summary>
    private void EvaluateReact(float _t)
    {
        float ms = _t * MsPerUnit;

        ClearPose();

        if (ReactPressEnd > ms)
        {
            poseDrop = 1;
            poseOutline = 2;
        }
        else if (ReactFlashEnd > ms)
        {
            poseOutline = 1;
        }

        ApplyPose();
    }

    private void ClearPose()
    {
        poseDrop = 0;
        poseOutline = 0;
        poseIconHop = 0;
        poseNumberJump = 0;
    }

    // // 그리기

    /// <summary>계산된 자세를 화면에 반영한다: 판 스프라이트 · 판 높이 · 키 아이콘 위치 · 앞 카드 아이콘 · 숫자 높이.</summary>
    private void ApplyPose()
    {
        if (null != plateImage && 0 < plateSprites.Length) plateImage.sprite = plateSprites[Mathf.Clamp(poseOutline, 0, plateSprites.Length - 1)];

        // 카드 · 숫자는 판의 자식이라 판이 가라앉으면 같이 내려간다. 키 아이콘은 판 밖이라 따로 내린다(판과 같은 칸수).
        plateRect.anchoredPosition = plateBase + new Vector2(0f, -poseDrop);
        keyIcon.anchoredPosition = keyBase + new Vector2(0f, -poseDrop);

        // 보관 도착 반응에서만 앞 카드 아이콘 · 숫자가 판 위에서 따로 위로 튄다(그 밖에는 0이라 제자리).
        if (null != frontCardIcon) frontCardIcon.anchoredPosition = iconBase + new Vector2(0f, poseIconHop);
        if (null != numberRect) numberRect.anchoredPosition = numberBase + new Vector2(0f, poseNumberJump);
    }

    /// <summary>연출이 없는 평소 자세.</summary>
    private void ApplyRest()
    {
        ClearPose();
        ApplyPose();
    }

    private bool EnsureReady()
    {
        if (true == bReady) return true;
        if (null == plateImage || null == keyIcon) return false;

        plateRect = plateImage.rectTransform;
        plateBase = plateRect.anchoredPosition;
        keyBase = keyIcon.anchoredPosition;

        if (null != frontCardIcon) iconBase = frontCardIcon.anchoredPosition;

        // 숫자 이미지(Number)는 UI_RoguelikeIconNumber가 폭에 맞춰 직접 놓으므로, 숫자 묶음의 위치를 잡는 부모(NumberAnchor)를 움직인다.
        numberRect = null != totalNumber ? totalNumber.transform.parent as RectTransform : null;
        if (null != numberRect) numberBase = numberRect.anchoredPosition;

        bReady = true;
        return true;
    }

    // // 유니티 라이프사이클

    private void OnEnable()
    {
        // HUD가 다시 켜지면 연출 없이 평소 자세로 선다. 잡아 둔 숫자가 남아 있으면 반응 없이 푼다.
        ReleaseCount();
        motion = EMotion.None;
        if (true == EnsureReady()) ApplyRest();
    }

    private void Update()
    {
        if (EMotion.None == motion) return;

        motionClock += Mathf.Min(Time.unscaledDeltaTime, RoguelikeOwnedCardsMotion.MaxFrameSeconds);

        if (EMotion.Store == motion)
        {
            int tick = (int)(motionClock * StoreTicksPerSecond);
            if (StoreTicks <= tick)
            {
                motion = EMotion.None;
                ApplyRest();
                return;
            }

            EvaluateStore(tick);
            return;
        }

        float t = motionClock * RoguelikeOwnedCardsMotion.Speed;

        if (ReactFlashEnd <= t * MsPerUnit)
        {
            motion = EMotion.None;
            ApplyRest();
            return;
        }

        EvaluateReact(t);
    }
}
