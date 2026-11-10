using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 카드 선택 화면. 기획서 v2.4 4-1·4-2 "선택 단계 규칙", 10-2 "카드 선택 화면 조작"·"카드 선택 화면 연출", 10-3 "카드 UI 연출".
///
/// [v2.4] 골드·가격·건너뛰기·감정이 없다. 카드는 모두 무료이고, 한 장 고르거나 선택 단계마다 1회 리롤한다.
///   OpenSelection/ReplaceOffers의 골드·리롤 비용·건너뛰기·첫 선택 인자는 시스템 코드가 정리될 때까지 시그니처에만 남아 있고 UI는 쓰지 않는다.
///
/// [시스템 → 뷰] RoguelikeUICoordinator가 아래 public 메서드를 부른다.
/// [뷰 → 시스템] 플레이어 입력은 이벤트로만 올린다. 뷰는 선택 판정을 하지 않는다(시스템이 거절하면 PlayRejected로 돌려준다).
///
/// [흐름]
///   OpenSelection → (RerollRequested → ReplaceOffers)* → BuyRequested(선택)
///   → 시스템이 결과를 판정 → PlayPurchase(선택 연출)
///   → 연출이 끝나면 뷰가 _onComplete를 부른다 → 시스템이 다음 단계(지형 전환/전투)로 넘어간다.
///   _onComplete를 부르지 않으면 시스템이 다음으로 넘어가지 못한다(대기 상한 뒤 강제 진행).
///
/// [구성] 어두운 딤 배경 위에 카드 줄(UI_RoguelikeCardSlot 최대 slots.Length칸) - 그 바로 아래 가운데 리롤 글자 버튼 - 설명 패널.
///   리롤 버튼은 플레이어 눈에 띄도록 카드 줄과 붙여 둔다(기획서의 좌하단 고정 배치 대신 - 구현 결정).
///   리롤 포커스 표시는 공용 커서 박스만 쓴다(별도 테두리 없음). 이름·설명은 커서가 올라가면 설명 패널이 보여 준다.
///   열리면 제일 왼쪽 카드가 호버된 상태로 시작한다. 마우스 호버·패드 선택은 같은 포커스로 모아 같은 연출(위로 6px + 커서 박스)을 준다.
///
/// [시간] 이 화면이 떠 있는 동안에도 보유 카드 창(Time.timeScale = 0)이나 ESC 메뉴로 일시정지가 걸릴 수 있다.
///   모든 연출은 unscaled 시간으로 돌린다(슬롯·이 클래스 Update). 구매 연출 대기 상한은 게임 시간이라 그동안 함께 멈춘다.
///
/// [폴리싱 - 시안 v4] 카드 줄 위 제목 명판(카드를 고르세요), 왼쪽 위 WAVE 배지(SetWave - 시스템이 부르기 전에는 숨김),
///   리롤은 카드 줄 바로 아래 가운데 글자 버튼(아이콘 + 리롤 + 남은 횟수, 폭은 18개 언어 중 가장 긴 문구 기준), 설명 패널은 리롤 버튼 아래(카드 선택 모드 - 폭 고정 · 높이는 카드마다).
///   키 안내는 패드 모드에서만 왼쪽 아래에 보인다(키보드 · 마우스 모드는 마우스 GUI 조작 - 키보드 단축키를 두지 않는다).
///
/// [아직 없는 것] 카드 뒷면·레어 프레임 리소스, 레어 등장 연출, 자물쇠 해제, 보관함 보관 연출, 카드 착지 효과음(호출 지점만 UI_RoguelikeCardSlot.StartLanding 주석).
/// </summary>
public class UIView_RoguelikeCardSelect : UIView_RoguelikeModalBase
{
    private enum EPhase
    {
        Idle,
        PurchaseFade,
        PurchaseMove,
        PurchaseHold,
        Closing,
    }

    private const float IntroIntervalSeconds = 0.13f;
    private const float FadeSeconds = 0.3f;
    private const float ExclusiveFadeSeconds = 0.15f;
    private const float MoveSeconds = 0.3f;
    private const float HoldSeconds = 0.2f;
    private const float CloseSeconds = 0.2f;
    private const float ShakeStepSeconds = 0.03f;
    private const float DisabledButtonAlpha = 0.4f;
    private const float RerollFocusSeconds = 0.1f;
    private static readonly Color RerollIdleColor = new Color(0.9f, 0.88f, 0.84f, 1f);
    private static readonly Color RerollFocusColor = Color.white;
    private const int CursorPadding = 4;
    // 18개 언어 중 가장 긴 리롤 문구(독일어 "Neu würfeln")로 잰 버튼 최소 폭 = 글자 + 아이콘 · 배지 · 여백.
    private const float SpecRerollWidth = 140f;
    private const float RerollPaddingWidth = 60f;
    // 18개 언어 중 가장 긴 제목 문구로 잰 명판 최소 폭.
    private const float SpecTitlePlateWidth = 200f;
    private const float TitleGap = 6f;
    private const float SelectDescWidth = 520f;
    private const float DescGap = 10f;
    private const float RerollGap = 8f;
    private const float ScreenMargin = 6f;
    private const float HoverHeadroom = 6f;
    private static readonly int[] ShakePattern = { -2, 2, -2, 2, -1, 1, 0 };

    /// <summary>제시된 _offerIndex번 카드를 고르겠다.</summary>
    public event Action<int> BuyRequestedEvent;
    public event Action RerollRequestedEvent;

    protected readonly List<RoguelikeCardViewData> offers = new List<RoguelikeCardViewData>(8);

    //외부 의존성 (프리팹에서 연결)
    [Header("카드")]
    [SerializeField] private UI_RoguelikeCardSlot[] slots = new UI_RoguelikeCardSlot[0];
    [SerializeField] private RectTransform contentRoot;
    [Tooltip("카드 줄 아래쪽 y(Content 기준). 슬롯 피벗이 왼쪽 아래라 이 값이 카드 바닥이다.")]
    [SerializeField] private float cardRowY = 6f;
    [SerializeField] private float slotGap = 14f;
    [SerializeField] private UI_RoguelikeCardDescPanel descPanel;

    [Header("리롤 버튼")]
    [SerializeField] private Button rerollButton;
    [SerializeField] private CanvasGroup rerollGroup;
    [SerializeField] private UI_RoguelikeSelectRelay rerollRelay;
    [SerializeField] private RectTransform rerollIconRect;
    [SerializeField] private Image rerollBackground;
    [SerializeField] private TMP_Text rerollLabel;
    [SerializeField] private TMP_Text rerollCountText;

    [Header("머리 줄 · 키 안내")]
    [SerializeField] private RectTransform titlePlate;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private RectTransform waveBadge;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private RectTransform padGuideRow;
    [SerializeField] private TMP_Text hintMoveText;
    [SerializeField] private TMP_Text hintSelectText;
    [SerializeField] private UI_KeyboardImage[] padGuideImages = new UI_KeyboardImage[0];

    //상태 변수
    private EPhase phase;
    private float phaseTime;
    private int slotCount;
    private UI_RoguelikeSelectRelay focusedRelay;
    private UI_RoguelikeCardSlot chosenSlot;
    private Action pendingComplete;
    private bool bLocked;
    private LocalizationManager localizationManager;
    private Action languageChangedHandler;
    private float contentDefaultY;
    private bool bWaveSet;
    private Action<EInputDeviceType> deviceChangedHandler;
    private readonly System.Text.StringBuilder waveBuilder = new System.Text.StringBuilder(16);

    private const string PickKey = "Roguelike_CardSelect_Pick";
    private const string HintMoveKey = "Roguelike_Hint_Move";
    private const string HintSelectKey = "Roguelike_Hint_Select";
    private CursorMotionSettings cursorMotion;
    private float rerollFocusAmount;
    private float rerollIconBaseY;

    private RectTransform shakeRect;
    private Vector2 shakeBasePosition;
    private float shakeTime;

    public IReadOnlyList<RoguelikeCardViewData> Offers => offers;
    /// <summary>이번 선택 단계에서 리롤을 쓸 수 있는가(선택 단계마다 1회).</summary>
    public bool bCanReroll { get; private set; }

    /// <summary>
    /// [시스템 연결 지점] 왼쪽 위 WAVE 배지. 언리밋이면 Unlimit-N. 한 번도 부르지 않으면 배지를 숨긴다(LobbyRemoval_AppLayer_Request.md).
    /// </summary>
    public void SetWave(int _waveNumber, bool _bUnlimit, int _unlimitIndex)
    {
        bWaveSet = true;
        waveBuilder.Clear();
        if (true == _bUnlimit) waveBuilder.Append("Unlimit-").Append(_unlimitIndex);
        else waveBuilder.Append("WAVE ").Append(_waveNumber);
        if (null != waveText) waveText.text = waveBuilder.ToString();
        if (null != waveBadge) waveBadge.gameObject.SetActive(true);
    }

    /// <summary>
    /// 선택 화면을 연다. 화면이 켜지고 카드 등장 연출을 시작한다.
    /// </summary>
    public virtual void OpenSelection(IReadOnlyList<RoguelikeCardViewData> _offers, bool _bCanReroll)
    {
        CopyOffers(_offers);
        bCanReroll = _bCanReroll;

        Show();
        OnOffersPresented(false);
    }

    /// <summary>리롤로 카드가 바뀌었다. 리롤은 선택 단계마다 1회라 쓰고 나면 _bCanReroll이 false로 온다.</summary>
    public virtual void ReplaceOffers(IReadOnlyList<RoguelikeCardViewData> _offers, bool _bCanReroll)
    {
        CopyOffers(_offers);
        bCanReroll = _bCanReroll;

        OnOffersPresented(true);
    }

    /// <summary>
    /// 구매가 확정됐다. 고르지 않은 카드는 흐려지며 사라지고(배타 카드는 더 빠르게) → 고른 카드가 화면 중앙으로 → 잠깐 머문 뒤
    /// 화면을 닫고 _onComplete를 부른다. 보관함 보관 연출(기획서 10-2)은 아직 없다 - 여기서 이어 붙인다.
    /// </summary>
    public virtual void PlayPurchase(in RoguelikeCardPurchaseViewData _purchase, IReadOnlyList<int> _discardedIndices, Action _onComplete)
    {
        if (false == IsVisible)
        {
            _onComplete?.Invoke();
            return;
        }

        pendingComplete = _onComplete;
        bLocked = true;
        chosenSlot = FindSlotByOffer(_purchase.offerIndex);

        ApplyDimming(null);
        chosenSlot?.PlayFlash();

        for (int i = 0; i < slotCount; i++)
        {
            UI_RoguelikeCardSlot slot = slots[i];
            if (slot == chosenSlot) continue;

            slot.PlayFade(ContainsIndex(_discardedIndices, slot.OfferIndex) ? ExclusiveFadeSeconds : FadeSeconds);
        }

        SetControlsVisible(false);
        HideCursor();
        phase = EPhase.PurchaseFade;
        phaseTime = 0f;
        Sound.PlayUI(SoundID.GetItem);
    }

    /// <summary>건너뛰기 등으로 구매 없이 닫는다. 카드가 흐려지며 닫힌 뒤 _onComplete를 부른다.</summary>
    public virtual void CloseSelection(Action _onComplete)
    {
        if (false == IsVisible)
        {
            _onComplete?.Invoke();
            return;
        }

        pendingComplete = _onComplete;
        bLocked = true;
        chosenSlot = null;

        for (int i = 0; i < slotCount; i++) slots[i].PlayFade(CloseSeconds);

        SetControlsVisible(false);
        HideCursor();
        phase = EPhase.Closing;
        phaseTime = 0f;
    }

    /// <summary>선택·리롤이 거절됐다. 해당 카드나 리롤 버튼이 정수 픽셀로 흔들린다.</summary>
    public virtual void PlayRejected(ECardShopResult _reason, int _offerIndex)
    {
        Sound.PlayUI(SoundID.AbilityUpgradeFailed);
        UI_RoguelikeCardSlot slot = FindSlotByOffer(_offerIndex);
        if (null != slot)
        {
            slot.PlayReject();
            return;
        }

        StartButtonShake(rerollButton);
    }

    /// <summary>카드가 처음 제시되거나(_bReroll=false) 리롤로 바뀌었을 때(_bReroll=true) 카드를 다시 놓고 순차로 뒤집는다.</summary>
    protected virtual void OnOffersPresented(bool _bReroll)
    {
        int count = Mathf.Min(offers.Count, slots.Length);
        if (offers.Count > slots.Length)
        {
            Debug.LogWarning("[UIView_RoguelikeCardSelect] 제시 카드(" + offers.Count + "장)가 슬롯(" + slots.Length + "칸)보다 많아 앞의 " + slots.Length + "장만 그립니다.");
        }

        slotCount = count;
        chosenSlot = null;
        bLocked = false;
        phase = EPhase.Idle;
        SetControlsVisible(true);

        PlaceSlots(count);
        UpdateControls();
        LayoutControls();
        RebuildNavigation();

        UI_RoguelikeSelectRelay first = count > 0 ? slots[0].Relay : rerollRelay;
        SetFocus(first, true);
        SelectInEventSystem(first);

        // 처음 포커스한 카드의 설명 높이로 바로 맞춘다(열리자마자 높이가 바뀌는 연출이 보이지 않게).
        descPanel.SnapHeight();
    }

    // // 입력 전달

    protected void RequestBuy(int _offerIndex) => BuyRequestedEvent?.Invoke(_offerIndex);
    protected void RequestReroll() => RerollRequestedEvent?.Invoke();

    private void CopyOffers(IReadOnlyList<RoguelikeCardViewData> _offers)
    {
        offers.Clear();
        if (null == _offers) return;

        for (int i = 0; i < _offers.Count; i++) offers.Add(_offers[i]);
    }

    // // 배치

    private void PlaceSlots(int _count)
    {
        float slotWidth = 0 < slots.Length && null != slots[0] ? ((RectTransform)slots[0].transform).rect.width : 0f;
        float totalWidth = 0 < _count ? _count * slotWidth + (_count - 1) * slotGap : 0f;
        float left = Mathf.Floor(-totalWidth * 0.5f);

        for (int i = 0; i < slots.Length; i++)
        {
            UI_RoguelikeCardSlot slot = slots[i];
            if (i >= _count)
            {
                slot.Unbind();
                continue;
            }

            slot.Bind(i, offers[i]);
            slot.SetPosition(new Vector2(left + Mathf.Round(i * (slotWidth + slotGap)), cardRowY));
            slot.PlayIntro(i * IntroIntervalSeconds);
        }
    }

    private string Localize(int _key)
    {
        return null != localizationManager ? localizationManager.GetText(_key) : string.Empty;
    }

    /// <summary>리롤 버튼의 이름·설명. 쓸 수 없으면 설명 자리에 이유(사용 불가)를 보여 준다.</summary>
    private void GetRerollInfo(out string _name, out string _description)
    {
        _name = Localize(LocKeys.RoguelikeUI.roguelikeCardSelectReroll);
        _description = Localize(bCanReroll ? LocKeys.RoguelikeUI.roguelikeCardSelectRerollDesc : LocKeys.RoguelikeUI.roguelikeCardSelectUnavailable);
    }

    /// <summary>
    /// 제목 명판 · 리롤 버튼 · 설명 패널 배치.
    ///   제목: 카드 줄(호버로 떠오른 높이 포함) 바로 위 가운데. 폭은 18개 언어 최소 폭과 현재 문구 폭 중 큰 값.
    ///   리롤: 카드 줄 바로 아래 가운데. 폭은 18개 언어 최소 폭과 현재 문구 폭 중 큰 값.
    ///   설명 패널: 리롤 버튼 바로 아래(아래로 자라므로 리롤 버튼 위치는 설명 길이와 상관없이 고정). 그 뒤 화면 안에 들어오도록 Content 위치를 조정한다.
    /// </summary>
    private void LayoutControls()
    {
        RectTransform rerollRect = (RectTransform)rerollButton.transform;
        float rerollWidth = SpecRerollWidth;
        if (null != rerollLabel) rerollWidth = Mathf.Max(rerollWidth, Mathf.Ceil(rerollLabel.GetPreferredValues(rerollLabel.text, 10000f, 10000f).x) + RerollPaddingWidth);
        // 가운데 피벗이라 폭을 짝수로 맞춰 양 끝이 정수 픽셀에 오게 한다.
        rerollRect.sizeDelta = new Vector2(Mathf.Ceil(rerollWidth * 0.5f) * 2f, rerollRect.sizeDelta.y);
        rerollIconBaseY = rerollIconRect.anchoredPosition.y - Mathf.Round(rerollFocusAmount);

        if (null != titlePlate && null != titleText)
        {
            float cardHeight = 0 < slots.Length ? ((RectTransform)slots[0].transform).rect.height : 0f;
            float plateWidth = Mathf.Max(SpecTitlePlateWidth, Mathf.Ceil(titleText.GetPreferredValues(titleText.text, 10000f, 10000f).x) + 32f);
            titlePlate.sizeDelta = new Vector2(Mathf.Ceil(plateWidth * 0.5f) * 2f, titlePlate.sizeDelta.y);
            titlePlate.anchoredPosition = new Vector2(0f, Mathf.Round(cardRowY + cardHeight + UI_RoguelikeCard.HoverLiftPixels + TitleGap));
        }

        float rerollHeight = rerollRect.sizeDelta.y;
        float rerollTop = cardRowY - RerollGap;
        rerollRect.anchoredPosition = new Vector2(0f, Mathf.Round(rerollTop - rerollHeight * 0.5f));

        float y = rerollTop - rerollHeight - DescGap;
        ((RectTransform)descPanel.transform).anchoredPosition = new Vector2(0f, y);
        FitContentVertically(y - PrepareDescriptionPanel());
    }

    /// <summary>
    /// 설명 패널이 보여 줄 수 있는 모든 내용(제시된 카드, 리롤 설명)을 한 번씩 재서 ① 가장 넓은 폭으로 폭을 고정하고
    /// ② 그 폭에서 가장 큰 높이를 돌려준다. 호버를 옮겨도 패널 크기와 화면 위치가 바뀌지 않는다.
    /// 끝나면 패널을 비운다(호출한 쪽이 현재 포커스의 설명을 다시 채운다).
    /// </summary>
    private float PrepareDescriptionPanel()
    {
        RectTransform screen = (RectTransform)contentRoot.parent;
        descPanel.SetMaxWidth(screen.rect.width - ScreenMargin * 2f);

        GetRerollInfo(out string rerollName, out string rerollDescription);

        // 카드 선택 모드: 폭은 고정(화면이 좁으면 화면 폭 안). 높이는 카드마다 바뀌지만 화면 배치는 가장 큰 높이 기준으로 잡는다.
        descPanel.SetFixedWidth(Mathf.Min(SelectDescWidth, screen.rect.width - ScreenMargin * 2f));

        descPanel.Clear();
        float height = descPanel.TargetHeight;
        for (int i = 0; i < slotCount; i++) height = Mathf.Max(height, descPanel.MeasureCardHeight(slots[i].Data));
        height = Mathf.Max(height, descPanel.MeasureInfoHeight(rerollName, rerollDescription));

        descPanel.Clear();
        return height;
    }

    /// <summary>
    /// 카드 줄부터 설명 패널 맨 아래(패널이 가장 크게 자랐을 때)까지가 화면 안에 들어오도록 Content를 위아래로 옮긴다.
    /// 화면 비율에 따라 UI 높이가 달라지고(폭 기준 스케일), 언어마다 패널 높이가 달라지기 때문이다.
    /// 패널은 호버한 카드마다 높이가 바뀌므로 현재 높이가 아니라 최대 높이를 기준으로 해서 호버 중에 화면이 움직이지 않게 한다.
    /// </summary>
    private void FitContentVertically(float _bottomLocal)
    {
        RectTransform screen = (RectTransform)contentRoot.parent;
        float halfHeight = screen.rect.height * 0.5f;
        if (0f >= halfHeight) return;

        float cardHeight = 0 < slots.Length ? ((RectTransform)slots[0].transform).rect.height : 0f;
        float titleHeight = null != titlePlate ? titlePlate.sizeDelta.y + TitleGap : 0f;
        float topLocal = cardRowY + cardHeight + UI_RoguelikeCard.HoverLiftPixels + HoverHeadroom + titleHeight;

        float minY = -halfHeight + ScreenMargin - _bottomLocal;
        float maxY = halfHeight - ScreenMargin - topLocal;

        float y = contentDefaultY;
        if (y < minY) y = minY;
        if (y > maxY) y = maxY;

        contentRoot.anchoredPosition = new Vector2(contentRoot.anchoredPosition.x, Mathf.Round(y));
    }

    /// <summary>리롤 가능 여부를 버튼 상태(활성/어둡게)에 반영한다.</summary>
    private void UpdateControls()
    {
        rerollButton.interactable = bCanReroll;
        rerollGroup.alpha = bCanReroll ? 1f : DisabledButtonAlpha;
        if (null != rerollCountText) rerollCountText.text = bCanReroll ? "1" : "0";
    }

    private void SetControlsVisible(bool _bVisible)
    {
        rerollButton.gameObject.SetActive(_bVisible);
        descPanel.gameObject.SetActive(_bVisible);
        if (null != titlePlate) titlePlate.gameObject.SetActive(_bVisible);
        if (null != waveBadge) waveBadge.gameObject.SetActive(_bVisible && bWaveSet);
        RefreshPadGuide(_bVisible);
    }

    /// <summary>
    /// 패드·키보드 이동. Automatic은 픽셀 아트 배치에서 엉뚱한 곳으로 가므로 Explicit으로 직접 잇는다.
    /// 카드끼리는 좌우, 카드에서 아래는 리롤 버튼, 버튼에서 위는 가운데 카드.
    /// 리롤을 이미 써서 못 쓰는 상태여도 선택은 된다(설명 패널에서 사용 불가 사유를 읽을 수 있게).
    /// </summary>
    private void RebuildNavigation()
    {
        for (int i = 0; i < slotCount; i++)
        {
            SetExplicitNavigation(slots[i].SelectButton, i > 0 ? slots[i - 1].SelectButton : null,
                i < slotCount - 1 ? slots[i + 1].SelectButton : null, null, rerollButton);
        }

        Button middleCard = 0 < slotCount ? slots[(slotCount - 1) / 2].SelectButton : null;
        SetExplicitNavigation(rerollButton, null, null, middleCard, null);
    }

    private static void SetExplicitNavigation(Button _button, Selectable _left, Selectable _right, Selectable _up, Selectable _down)
    {
        Navigation navigation = new Navigation
        {
            mode = Navigation.Mode.Explicit,
            selectOnLeft = _left,
            selectOnRight = _right,
            selectOnUp = _up,
            selectOnDown = _down,
        };
        _button.navigation = navigation;
    }

    // // 포커스(호버·선택)

    private void SetFocus(UI_RoguelikeSelectRelay _relay, bool _bForce)
    {
        if (false == _bForce && _relay == focusedRelay) return;

        // 열릴 때의 첫 포커스(_bForce)와 포커스 해제에는 소리를 내지 않는다.
        if (false == _bForce && null != _relay) Sound.PlayUI(SoundID.ResultUIHover);

        FindSlotByRelay(focusedRelay)?.SetFocused(false);
        focusedRelay = _relay;

        if (null == _relay)
        {
            ApplyDimming(null);
            descPanel.Clear();
            HideCursor();
            return;
        }

        UI_RoguelikeCardSlot slot = FindSlotByRelay(_relay);
        if (null != slot) slot.SetFocused(true);

        ApplyDimming(slot);
        RefreshDescription();
        ShowCursor(_relay);
    }

    /// <summary>카드가 선택 중이면 나머지 카드를 어둡게 해 밝기로도 선택 중인 카드를 구분한다. 리롤 버튼에 포커스가 있거나 포커스가 없으면 모두 원래 밝기.</summary>
    private void ApplyDimming(UI_RoguelikeCardSlot _focusedSlot)
    {
        for (int i = 0; i < slotCount; i++) slots[i].SetDimmed(null != _focusedSlot && slots[i] != _focusedSlot);
    }

    /// <summary>포커스가 있는 대상(카드·리롤)의 이름·설명을 패널에 채운다. 아직 뒤집히는 중인 카드는 비운다.</summary>
    private void RefreshDescription()
    {
        UI_RoguelikeCardSlot slot = FindSlotByRelay(focusedRelay);
        if (null != slot)
        {
            if (slot.IsRevealed) descPanel.SetCard(slot.Data);
            else descPanel.Clear();
            return;
        }

        if (null != focusedRelay && focusedRelay == rerollRelay)
        {
            GetRerollInfo(out string rerollName, out string rerollDescription);
            descPanel.SetInfo(rerollName, rerollDescription);
            return;
        }

        descPanel.Clear();
    }

    private void ShowCursor(UI_RoguelikeSelectRelay _relay)
    {
        ICursorBoxUI cursor = viewCtx?.cursorBoxUI;
        if (null == cursor) return;

        // 카드는 뒤집히는 동안 커서 박스를 띄우지 않는다(착지하는 순간 OnSlotLanded에서 나타난다).
        // 이전 대상(리롤 직후의 리롤 버튼 등)에 커서 박스가 남지 않게 착지 전까지 감춘다.
        UI_RoguelikeCardSlot slot = FindSlotByRelay(_relay);
        if (null != slot && slot.IsIntroPlaying)
        {
            HideCursor();
            return;
        }

        RectTransform target = _relay.CursorTarget;
        cursor.Show(target, target.rect.size + new Vector2(CursorPadding, CursorPadding), Vector2.zero, cursorMotion);
    }

    private void HideCursor()
    {
        viewCtx?.cursorBoxUI?.Hide();
    }

    private void SelectInEventSystem(UI_RoguelikeSelectRelay _relay)
    {
        EventSystem eventSystem = EventSystem.current;
        if (null == eventSystem || null == _relay) return;
        if (eventSystem.currentSelectedGameObject == _relay.gameObject) return;

        eventSystem.SetSelectedGameObject(_relay.gameObject);
    }

    private UI_RoguelikeCardSlot FindSlotByRelay(UI_RoguelikeSelectRelay _relay)
    {
        if (null == _relay) return null;

        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i].Relay == _relay) return slots[i];
        }

        return null;
    }

    private UI_RoguelikeCardSlot FindSlotByOffer(int _offerIndex)
    {
        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i].OfferIndex == _offerIndex) return slots[i];
        }

        return null;
    }

    private static bool ContainsIndex(IReadOnlyList<int> _indices, int _index)
    {
        if (null == _indices) return false;

        for (int i = 0; i < _indices.Count; i++)
        {
            if (_indices[i] == _index) return true;
        }

        return false;
    }

    // // 이벤트 핸들러

    private void OnLanguageChanged()
    {
        ApplyStaticTexts();
        LayoutControls();
        RefreshDescription();
    }

    private void OnRelayFocused(UI_RoguelikeSelectRelay _relay)
    {
        if (bLocked) return;

        SetFocus(_relay, false);
        SelectInEventSystem(_relay);
    }

    private void OnSlotClicked(UI_RoguelikeCardSlot _slot)
    {
        if (bLocked) return;

        RequestBuy(_slot.OfferIndex);
    }

    private void OnSlotRevealed(UI_RoguelikeCardSlot _slot)
    {
        if (null != focusedRelay && _slot.Relay == focusedRelay) RefreshDescription();
    }

    private void OnSlotLanded(UI_RoguelikeCardSlot _slot)
    {
        if (null != focusedRelay && _slot.Relay == focusedRelay) ShowCursor(focusedRelay);
    }

    private void OnRerollClicked()
    {
        if (bLocked) return;

        Sound.PlayUI(SoundID.MainClick);
        RequestReroll();
    }

    // // 연출 진행

    private void StartButtonShake(Button _button)
    {
        if (null != shakeRect) shakeRect.anchoredPosition = shakeBasePosition;

        shakeRect = (RectTransform)_button.transform;
        shakeBasePosition = shakeRect.anchoredPosition;
        shakeTime = 0f;
    }

    private void UpdateButtonShake()
    {
        if (null == shakeRect) return;

        shakeTime += Time.unscaledDeltaTime;
        int step = (int)(shakeTime / ShakeStepSeconds);

        if (step >= ShakePattern.Length)
        {
            shakeRect.anchoredPosition = shakeBasePosition;
            shakeRect = null;
            return;
        }

        shakeRect.anchoredPosition = shakeBasePosition + new Vector2(ShakePattern[step], 0f);
    }

    /// <summary>리롤 버튼에 포커스가 오면 배경이 밝아지고 아이콘이 1px 뜬다(카드 호버와 같은 결의 약한 반응).</summary>
    private void UpdateRerollFocus()
    {
        float target = null != focusedRelay && focusedRelay == rerollRelay ? 1f : 0f;
        if (rerollFocusAmount == target) return;

        rerollFocusAmount = Mathf.MoveTowards(rerollFocusAmount, target, Time.unscaledDeltaTime / RerollFocusSeconds);
        rerollBackground.color = Color.Lerp(RerollIdleColor, RerollFocusColor, rerollFocusAmount);

        Vector2 iconPosition = rerollIconRect.anchoredPosition;
        rerollIconRect.anchoredPosition = new Vector2(iconPosition.x, rerollIconBaseY + Mathf.Round(rerollFocusAmount));
    }

    private void UpdatePhase()
    {
        if (EPhase.Idle == phase) return;

        phaseTime += Time.unscaledDeltaTime;

        switch (phase)
        {
            case EPhase.PurchaseFade:
                if (phaseTime < FadeSeconds) break;
                chosenSlot?.PlayMove(GetCenterPosition(), MoveSeconds);
                phase = EPhase.PurchaseMove;
                phaseTime = 0f;
                break;

            case EPhase.PurchaseMove:
                if (phaseTime < MoveSeconds) break;
                phase = EPhase.PurchaseHold;
                phaseTime = 0f;
                break;

            case EPhase.PurchaseHold:
                if (phaseTime < HoldSeconds) break;
                Complete();
                break;

            case EPhase.Closing:
                if (phaseTime < CloseSeconds) break;
                Complete();
                break;
        }
    }

    /// <summary>고른 카드가 멈출 곳. 카드 한 장의 중심이 화면 중앙에 오는 Content 기준 정수 좌표.</summary>
    private Vector2 GetCenterPosition()
    {
        RectTransform slotRect = (RectTransform)chosenSlot.transform;
        Vector2 contentOffset = null != contentRoot ? contentRoot.anchoredPosition : Vector2.zero;
        return new Vector2(Mathf.Floor(-slotRect.rect.width * 0.5f - contentOffset.x), Mathf.Floor(-slotRect.rect.height * 0.5f - contentOffset.y));
    }

    private void UnsubscribeLanguage()
    {
        if (null != localizationManager) localizationManager.OnLanguageChanged -= languageChangedHandler;
    }

    /// <summary>연출이 끝났다. 화면을 닫고 시스템에 알린다(닫은 뒤 콜백 - 시스템이 다음 단계로 넘어간다).</summary>
    private void Complete()
    {
        Action callback = pendingComplete;
        pendingComplete = null;
        phase = EPhase.Idle;

        Hide();
        callback?.Invoke();
    }

    /// <summary>제목 · 리롤 글자 · 키 안내 문구. 언어가 바뀌면 다시 채운다.</summary>
    private void ApplyStaticTexts()
    {
        if (null != titleText) titleText.text = null != localizationManager ? localizationManager.GetText(PickKey) : string.Empty;
        if (null != rerollLabel) rerollLabel.text = Localize(LocKeys.RoguelikeUI.roguelikeCardSelectReroll);
        if (null != hintMoveText) hintMoveText.text = null != localizationManager ? localizationManager.GetText(HintMoveKey) : string.Empty;
        if (null != hintSelectText) hintSelectText.text = null != localizationManager ? localizationManager.GetText(HintSelectKey) : string.Empty;
    }

    /// <summary>키 안내는 패드 모드에서만 보인다(키보드 · 마우스 모드는 마우스 GUI 조작).</summary>
    private void RefreshPadGuide(bool _bVisible)
    {
        if (null == padGuideRow) return;
        bool bPad = null != viewCtx && null != viewCtx.inputManager && true == viewCtx.inputManager.IsGamepadMode;
        padGuideRow.gameObject.SetActive(_bVisible && bPad);
    }

    private void OnDeviceChanged(EInputDeviceType _device)
    {
        RefreshPadGuide(null != titlePlate && titlePlate.gameObject.activeSelf);
    }

    protected override void OnShowContent()
    {
        UnsubscribeLanguage();
        localizationManager = viewCtx?.localizationManager;
        if (null != localizationManager) localizationManager.OnLanguageChanged += languageChangedHandler;

        descPanel.Initialize(localizationManager);
        rerollFocusAmount = 0f;
        rerollBackground.color = RerollIdleColor;
        ApplyStaticTexts();

        InputManager input = viewCtx?.inputManager;
        if (null != input && null != input.inputReader)
        {
            input.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
            input.inputReader.InputDeviceChangedEvent += deviceChangedHandler;
        }
        for (int i = 0; padGuideImages.Length > i; i++)
        {
            if (null != padGuideImages[i]) padGuideImages[i].Initialize(input);
        }

        LayoutControls();
        pendingComplete = null;
        phase = EPhase.Idle;
        bLocked = false;
        focusedRelay = null;
    }

    /// <summary>
    /// 화면이 닫힌다. 연출 중 밖에서 닫혔다면(ForceCloseCardSelection) 대기 중이던 완료 콜백은 부르지 않고 버린다 -
    /// 시스템은 이미 다음 단계로 넘어간 뒤라 늦은 완료 신호가 다음 단계를 건드릴 수 있다.
    /// </summary>
    protected override void OnHideContent()
    {
        pendingComplete = null;
        phase = EPhase.Idle;
        bLocked = false;
        chosenSlot = null;
        FindSlotByRelay(focusedRelay)?.SetFocused(false);
        focusedRelay = null;
        UnsubscribeLanguage();
        if (null != viewCtx && null != viewCtx.inputManager && null != viewCtx.inputManager.inputReader)
        {
            viewCtx.inputManager.inputReader.InputDeviceChangedEvent -= deviceChangedHandler;
        }

        // 닫힌 화면 안의 오브젝트가 EventSystem 선택으로 남지 않게 정리한다(비활성 오브젝트가 선택된 채로 남으면 다른 UI의 패드 조작이 먹통이 될 수 있다).
        EventSystem eventSystem = EventSystem.current;
        if (null != eventSystem && null != eventSystem.currentSelectedGameObject && eventSystem.currentSelectedGameObject.transform.IsChildOf(transform))
        {
            eventSystem.SetSelectedGameObject(null);
        }

        if (null != shakeRect)
        {
            shakeRect.anchoredPosition = shakeBasePosition;
            shakeRect = null;
        }

        HideCursor();
    }

    protected override void Awake()
    {
        base.Awake();

        languageChangedHandler = OnLanguageChanged;
        cursorMotion = CursorMotionSettings.Card;

        // 프리팹에 잡아 둔 Content 기본 위치. 이후 레이아웃은 이 값에서 다시 계산한다.
        contentDefaultY = contentRoot.anchoredPosition.y;
        deviceChangedHandler = OnDeviceChanged;
        if (null != waveBadge) waveBadge.gameObject.SetActive(false);

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].ClickedEvent += OnSlotClicked;
            slots[i].RevealedEvent += OnSlotRevealed;
            slots[i].LandedEvent += OnSlotLanded;
            slots[i].Relay.FocusedEvent += OnRelayFocused;
        }

        rerollRelay.FocusedEvent += OnRelayFocused;
        rerollButton.onClick.AddListener(OnRerollClicked);
    }

    public override void Update()
    {
        base.Update();

        UpdatePhase();
        UpdateButtonShake();
        UpdateRerollFocus();

        // 선택 중이던 것이 비활성화되면 EventSystem 선택이 null이 되어 패드 조작이 먹통이 된다. 포커스가 있으면 되돌려 준다.
        // 패드 모드에서만 되돌리고, ESC 메뉴 · 옵션 창처럼 이 화면 위에 뜬 UI가 선택을 쥐고 있는 동안에는 빼앗지 않는다(CanRestoreSelection).
        EventSystem eventSystem = EventSystem.current;
        bool bCanRestore = CanRestoreSelection(eventSystem);
        if (true == bCanRestore && EPhase.Idle == phase && false == bLocked && null != focusedRelay && focusedRelay.gameObject.activeInHierarchy
            && null != viewCtx && null != viewCtx.inputManager && true == viewCtx.inputManager.IsGamepadMode)
        {
            eventSystem.SetSelectedGameObject(focusedRelay.gameObject);
        }
    }

    public override void OnDestroy()
    {
        BuyRequestedEvent = null;
        RerollRequestedEvent = null;
        pendingComplete = null;
        UnsubscribeLanguage();
        base.OnDestroy();
    }
}
