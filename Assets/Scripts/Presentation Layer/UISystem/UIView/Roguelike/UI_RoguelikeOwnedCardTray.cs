using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 HUD 우하단의 보관함 트레이. 기획서 10-2 "보유 카드 보관함 - 기본 상태(접힘)": 겹친 카드 묶음 아이콘 + 보유 카드 총량 숫자가 든 숫자판 하나.
/// 표시 전용이다 - 마우스를 받지 않고(레이캐스트 그래픽 없음) 누르거나 올려도 반응하지 않는다. 보유 카드 창은 키(인벤토리 키)로만 연다.
///
/// [숫자] 레어 · 일반 장수는 SetCounts로 받아 합계(총량) 하나로 보여 준다. 레어 · 일반 구분은 보유 카드 창이 보여 준다. 값을 받기 전(HUD가 켜질 때, ResetTray)에는 0이다. 카드가 0장이어도 0으로 표기한다.
///   숫자는 버프 HUD와 같은 작은 숫자 글리프(UI_RoguelikeIconNumber)다.
///
/// [창 위로 올림] 보유 카드 창은 HUD보다 위 캔버스(Overlay, sort 100)에 뜨고 딤이 HUD를 덮는다. 트레이 연출이 가려지지 않도록 창이 열려 있는 동안
///   창 캔버스의 맨 위 레이어(AttachTo)로 트레이를 옮기고, 닫기 도착 직후 원래 자리(Detach)로 돌려보낸다. 앵커 · 위치 값은 그대로라 화면 위치가 변하지 않는다(두 캔버스의 스케일러 설정이 같다).
///
/// [연출] 보유 카드 창의 열기 · 닫기와 짝을 이룬다(시간표는 RoguelikeOwnedCardsMotion, 창과 같은 시작 시각을 쓴다). 반투명 · 입자 · 탄성 없이 정수 픽셀과 프레임 단위 자세 교체만 쓴다. unscaled 시간이다(창이 열려 있는 동안 게임이 멈춘다).
///   보간 대신 손으로 그린 프레임 표다: 시뮬레이션에서 확정한 ms 표(연출 속도 2.2배 기준)를 MsPerUnit으로 연출 단위에 맞춘다. 카드 · 숫자 · 키 아이콘은 단독으로 움직이지 않는다(판이 가라앉을 때 같이 내려갈 뿐이다).
///   열 때 (PlayOpenEffect) · 닫을 때 (PlayAbsorbEffect)  키를 누른 즉시 같은 반응이 나온다: 판 1칸 가라앉음 + 테두리 흰색 점멸 → 판 복귀 · 테두리 살구색 → 평소. 창이 트레이 칸에 닿는 도착 순간에는 반응하지 않는다.
///   테두리 점멸은 판 스프라이트 교체(plateSprites)다.
/// </summary>
public class UI_RoguelikeOwnedCardTray : MonoBehaviour
{
    /// <summary>숫자판 크기. 구성기(RoguelikeHUDBuilder)가 그림을 만들 때도 쓴다.</summary>
    public const int PlateWidth = 34;
    public const int PlateHeight = 19;

    /// <summary>연출 단위 1이 몇 ms인지(연출 속도 2.2배에서 확정한 프레임 표 기준). 표의 ms 값을 연출 단위 시각에 맞춘다.</summary>
    private const float MsPerUnit = 1000f / 2.2f;

    // 반응 프레임 경계(키 입력부터 ms). 열기 · 닫기가 같은 표를 쓴다.
    private const float ReactPressEnd = 60f;
    private const float ReactFlashEnd = 110f;

    private enum EMotion
    {
        None,
        React,
    }

    //내부 의존성 (프리팹에서 연결)
    [SerializeField] private UI_RoguelikeIconNumber totalNumber;
    [SerializeField] private Image plateImage;
    [SerializeField, Tooltip("0 = 기본, 1 = 약한 점멸(살구색 테두리), 2 = 강한 점멸(흰색 테두리)")] private Sprite[] plateSprites = new Sprite[0];
    [SerializeField] private RectTransform keyIcon;

    //상태 변수
    private RectTransform plateRect;
    private Vector2 plateBase;
    private Vector2 keyBase;
    private Transform homeParent;
    private int homeSiblingIndex;
    private EMotion motion;
    private float motionClock;
    private bool bAttached;
    private bool bReady;
    private int poseDrop;
    private int poseOutline;

    /// <summary>트레이 칸의 사각형. 보유 카드 창이 닫힐 때 이 칸으로 줄어든다.</summary>
    public RectTransform TrayRect => (RectTransform)transform;

    /// <summary>레어(케이스) · 일반(슬리브) 장수. 합계를 숫자판에 그린다. 값이 바뀔 때만 부르면 된다.</summary>
    public void SetCounts(int _rareCount, int _normalCount)
    {
        if (null != totalNumber) totalNumber.SetSingle(_rareCount + _normalCount);
    }

    /// <summary>런 시작·종료 초기화. 0 · 0으로 되돌리고 연출도 멈춘다.</summary>
    public void ResetTray()
    {
        SetCounts(0, 0);
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

    /// <summary>연출을 즉시 멈추고 평소 자세로 되돌린다(창이 연출 없이 닫힐 때, 런 초기화).</summary>
    public void ResetEffect()
    {
        motion = EMotion.None;
        if (true == bReady) ApplyRest();
    }

    // // 연출 계산

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
    }

    // // 그리기

    /// <summary>계산된 자세를 화면에 반영한다: 판 스프라이트 · 판 높이 · 키 아이콘 위치.</summary>
    private void ApplyPose()
    {
        if (null != plateImage && 0 < plateSprites.Length) plateImage.sprite = plateSprites[Mathf.Clamp(poseOutline, 0, plateSprites.Length - 1)];

        // 카드 · 숫자는 판의 자식이라 판이 가라앉으면 같이 내려간다. 키 아이콘은 판 밖이라 따로 내린다(판과 같은 칸수).
        plateRect.anchoredPosition = plateBase + new Vector2(0f, -poseDrop);
        keyIcon.anchoredPosition = keyBase + new Vector2(0f, -poseDrop);
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

        bReady = true;
        return true;
    }

    // // 유니티 라이프사이클

    private void OnEnable()
    {
        // HUD가 다시 켜지면 연출 없이 평소 자세로 선다.
        motion = EMotion.None;
        if (true == EnsureReady()) ApplyRest();
    }

    private void Update()
    {
        if (EMotion.None == motion) return;

        motionClock += Mathf.Min(Time.unscaledDeltaTime, RoguelikeOwnedCardsMotion.MaxFrameSeconds);
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
