using System;
using System.Collections.Generic;
using PresentationLayer.UISystem;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 카드 선택 화면. 기획서 v2.4 4-1·4-2 "선택 단계 규칙", 10-2 "카드 선택 화면 조작"·"카드 선택 화면 연출", 10-3 "카드 UI 연출".
///
/// 골드·가격·건너뛰기·감정이 없다. 카드는 모두 무료이고, 한 장 고르거나 선택 단계마다 1회 리롤한다.
///
/// [시스템 → 뷰] RoguelikeUICoordinator가 아래 public 메서드를 부른다.
/// [뷰 → 시스템] 플레이어 입력은 이벤트로만 올린다. 뷰는 선택 판정을 하지 않는다(시스템이 거절하면 PlayRejected로 돌려준다).
///
/// [리롤 모드] 열 때 SlotRerollRequestedEvent 구독자가 있으면 칸별 모드(기획서 4-1: 카드 칸마다 1회), 없으면 예전 모드(공용 버튼 1개, 선택 단계마다 1회)다.
///   칸별 모드  카드마다 바로 아래에 20x20 리롤 아이콘 버튼(UI_RoguelikeCardSlot), 공용 리롤 버튼은 숨김. 버튼 활성은 RoguelikeCardViewData.bCanReroll.
///   예전 모드  공용 리롤 버튼만 보이고 카드별 버튼은 숨김. [호환] G-12 반영 후 삭제.
///
/// [흐름]
///   칸별 모드  OpenSelection → (SlotRerollRequested → ReplaceOffer(그 칸만 다시 뒤집힘))* → BuyRequested(선택)
///   예전 모드  OpenSelection → (RerollRequested → ReplaceOffers)* → BuyRequested(선택)
///   → 시스템이 결과를 판정 → PlayPurchase(선택 연출)
///   → 연출이 끝나면 뷰가 _onComplete를 부른다 → 시스템이 다음 단계(지형 전환/전투)로 넘어간다.
///   _onComplete를 부르지 않으면 시스템이 다음으로 넘어가지 못한다(대기 상한 뒤 강제 진행).
///
/// [구성] 어두운 딤 배경 위에 카드 줄(UI_RoguelikeCardSlot 최대 slots.Length칸) - 칸별 모드는 카드마다 바로 아래 아이콘 버튼, 예전 모드는 카드 줄 바로 아래 가운데 리롤 글자 버튼 - 설명 패널.
///   리롤 버튼은 플레이어 눈에 띄도록 카드 줄과 붙여 둔다(기획서의 좌하단 고정 배치 대신 - 구현 결정).
///   리롤 포커스 표시는 공용 커서 박스(버튼 둘레) + 버튼 배경 밝아짐이다. 칸별 버튼에 포커스가 오면 그 칸 카드는 올라가지 않고 나머지 카드가 어두워진다.
///   이름·설명은 커서가 올라가면 설명 패널이 보여 준다(칸별 버튼: "이 카드 바꾸기" + 설명, 이미 썼으면 사용 불가 문구).
///   열리면 제일 왼쪽 카드가 호버된 상태로 시작한다. 마우스 호버·패드 선택은 같은 포커스로 모아 같은 연출(위로 6px + 커서 박스)을 준다.
///
/// [시간] 이 화면이 떠 있는 동안에도 보유 카드 창(Time.timeScale = 0)이나 ESC 메뉴로 일시정지가 걸릴 수 있다.
///   모든 연출은 unscaled 시간으로 돌린다(슬롯·이 클래스 Update). 구매 연출 대기 상한은 게임 시간이라 그동안 함께 멈춘다.
///
/// [폴리싱 - 시안 v4] 카드 줄 위 제목 명판(카드를 고르세요), 왼쪽 위 WAVE 배지(SetWave - 시스템이 부르기 전에는 숨김),
///   예전 모드 리롤은 카드 줄 바로 아래 가운데 글자 버튼(아이콘 + 리롤 + 남은 횟수, 폭은 18개 언어 중 가장 긴 문구 기준), 설명 패널은 그 아래(카드 선택 모드 - 폭 고정 · 높이는 카드마다).
///   칸별 모드도 설명 패널 자리는 같다(공용 버튼 자리는 비워 둔다).
///   키 안내는 패드 모드에서만 왼쪽 아래에 보인다(키보드 · 마우스 모드는 마우스 GUI 조작 - 키보드 단축키를 두지 않는다).
///
/// [퇴장 단계] 연출이 끝나면 Complete 전에 퇴장 0.18초가 돈다: 화면 전체(딤 포함)가 흐려지고(screenGroup) Content가 4px 떠오른다. 끝나면 Complete.
///
/// [레어 카드 케이스] 레어(에메랄드 · 프리즘 · 루비)를 고르면 기존 이동 · 잠깐 머묾 대신 아크릴 케이스에 끼우는 연출(UI_RoguelikeAcrylicCase, 약 1.26초)이 돈다.
///   카드가 케이스 위쪽에 걸친 자리로 이동 → 쏙 끼움 → 딸깍(반동 · 번쩍 · 입자 · 효과음) → 광택 → 정지 → 보관 연출 → Complete.
///
/// [보관 연출] 이동 · 머묾(일반) 또는 케이스 연출(레어)이 끝나면 Exit(퇴장) 대신 보관 연출(UI_RoguelikeCardStoreDrop)이 돈다.
///   일반 카드는 슬리브가 씌워진 뒤(0.18초), 레어 카드는 케이스째, 카드가 위로 살짝 떴다 아래로 뚝 떨어진다(0.3초, 화면 밖). 낙하가 시작되는 순간 딤만 0.18초 동안 걷힌다(카드는 흐려지지 않는다).
///   낙하가 끝나면 HUD 보관함 트레이(RoguelikeHUDLink.OwnedTray)가 도착 반응을 하고(트레이가 꺼져 있으면 생략) 화면이 닫힌다(Complete). 합계 일반 약 1.18초, 레어 약 1.86초.
///   트레이 숫자는 시스템이 PlayPurchase보다 먼저 새 값으로 보낸다 - PlayPurchase가 직전 값으로 되돌려 잡아 두었다가(HoldPreviousCount) 도착 순간 푼다.
///   storeDrop · dimImage가 연결되어 있지 않으면 기존 퇴장(Exit)으로 닫는다.
///
/// [아직 없는 것] 보관함 쇼케이스 칸 보관(레어 케이스를 쇼케이스 칸으로 옮기고 접는 것), 카드 착지 · 보관 도착 효과음(호출 지점만 UI_RoguelikeCardSlot.StartLanding · UI_RoguelikeOwnedCardTray.ReleaseCountWithEffect 주석).
/// </summary>
public class UIView_RoguelikeCardSelect : UIView_RoguelikeModalBase
{
    private enum EPhase
    {
        Idle,
        PurchaseFade,
        PurchaseMove,
        PurchaseHold,
        PurchaseRareCase,
        PurchaseStore,
        Exit,
    }

    private const float IntroIntervalSeconds = 0.13f;
    // 희귀 카드 등장 연출의 순서(⓪): 일반 카드를 먼저 뒤집고, 마지막 일반 카드 시작 + 0.3초에 희귀 카드를 시작한다. 희귀가 여럿이면 앞 희귀 시작 + 0.9초씩.
    private const float RareAfterNormalSeconds = 0.3f;
    private const float RareIntervalSeconds = 0.9f;
    private const float FadeSeconds = 0.3f;
    private const float ExclusiveFadeSeconds = 0.15f;
    private const float MoveSeconds = 0.3f;
    private const float HoldSeconds = 0.1f;
    private const float CloseSeconds = 0.2f;
    private const float ExitSeconds = 0.18f;
    private const float DimFadeSeconds = 0.18f;
    private const int ExitLiftPixels = 4;
    // 보유 카드 창이 열리는 동안 이 화면이 가라앉으며 사라지는(Out) / 창이 닫히면 떠오르며 돌아오는(In) 연출.
    private const float CoverOutSeconds = 0.15f;
    private const float CoverInSeconds = 0.18f;
    private const int CoverSinkPixels = 6;
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
    // [호환] G-12 반영 후 삭제(예전 모드 전용: 공용 리롤 버튼).
    public event Action RerollRequestedEvent;
    /// <summary>칸별 모드: 제시된 _offerIndex번 칸의 카드를 다른 카드로 바꾸겠다. 코디네이터가 구독하면 칸별 모드가 된다(G-12).</summary>
    public event Action<int> SlotRerollRequestedEvent;

    protected readonly List<RoguelikeCardViewData> offers = new List<RoguelikeCardViewData>(8);

    //외부 의존성 (프리팹에서 연결)
    [Header("카드")]
    [SerializeField] private UI_RoguelikeCardSlot[] slots = new UI_RoguelikeCardSlot[0];
    [SerializeField] private RectTransform contentRoot;
    [Tooltip("화면 전체(딤 포함)를 흐리게 하는 루트 CanvasGroup. 퇴장 연출에 쓴다.")]
    [SerializeField] private CanvasGroup screenGroup;
    [Tooltip("카드 줄 아래쪽 y(Content 기준). 슬롯 피벗이 왼쪽 아래라 이 값이 카드 바닥이다.")]
    [SerializeField] private float cardRowY = 6f;
    [SerializeField] private float slotGap = 14f;
    [SerializeField] private UI_RoguelikeCardDescPanel descPanel;

    [Header("레어 카드 케이스")]
    [SerializeField] private UI_RoguelikeAcrylicCase acrylicCase;

    [Header("보관 연출")]
    [SerializeField] private UI_RoguelikeCardStoreDrop storeDrop;
    [Tooltip("화면 전체를 어둡게 덮는 딤 이미지. 보관 낙하가 시작되면 이것만 걷힌다(카드 · 화면 전체는 흐려지지 않는다).")]
    [SerializeField] private Image dimImage;

    [Header("리롤 버튼 [호환] G-12 반영 후 삭제(예전 모드 전용 공용 버튼)")]
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
    [SerializeField] private TMPInlineStyleAnimator titleAnimator;
    [SerializeField] private TMPInlineStyleAnimator titleShadowAnimator;
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
    // 열 때 SlotRerollRequestedEvent 구독자가 있으면 칸별 리롤 모드, 없으면 예전 공용 버튼 모드.
    private bool bSlotRerollMode;
    private bool bRarePurchase;
    // 희귀 카드의 두근 ~ 유지 시작 사이(그 카드를 뺀 나머지를 어둡게). 없으면 null.
    private UI_RoguelikeCardSlot rareSpotlightSlot;
    private float[] introDelays = new float[0];
    private LocalizationManager localizationManager;
    private Action languageChangedHandler;
    private Action rareCaseDoneHandler;
    private Action storeDropStartHandler;
    private Action storeDropDoneHandler;
    private bool bDimFading;
    private float dimFadeTime;
    private float dimBaseAlpha = 1f;
    private float contentDefaultY;
    private float exitDuration;
    private float exitBaseY;
    // 보유 카드 창에 덮이는 상태. coverAmount 0 = 보임, 1 = 완전히 사라짐. coverBaseY = 덮이기 직전 Content 위치.
    private bool bCovered;
    private float coverAmount;
    private float coverBaseY;
    private bool bWaveSet;
    private Action<EInputDeviceType> deviceChangedHandler;
    private readonly System.Text.StringBuilder waveBuilder = new System.Text.StringBuilder(16);

    private const string PickKey = "Roguelike_CardSelect_Pick";
    private const string SlotRerollNameKey = "Roguelike_CardSelect_RerollCard";
    private const string SlotRerollDescKey = "Roguelike_CardSelect_RerollCardDesc";
    // 제목 연출 태그: 파도 + 민트(#7EEDBE) · 금(#EDD87E) 2색 순환.
    private const string TitleTagOpen = "<wave><color=#7EEDBE,#EDD87E>";
    private const string TitleTagClose = "</color></wave>";
    private string titlePlainText = string.Empty;
    private const string HintMoveKey = "Roguelike_Hint_Move";
    private const string HintSelectKey = "Roguelike_Hint_Select";
    private CursorMotionSettings cursorMotion;
    private float rerollFocusAmount;
    private float rerollIconBaseY;

    private RectTransform shakeRect;
    private Vector2 shakeBasePosition;
    private float shakeTime;

    /// <summary>[호환] G-12 반영 후 삭제. 예전 모드: 이번 선택 단계에서 리롤을 쓸 수 있는가(선택 단계마다 1회). 칸별 모드는 RoguelikeCardViewData.bCanReroll을 쓴다.</summary>
    public bool bCanReroll { get; private set; }

    /// <summary>보유 카드 창 · ESC 도전과제 창으로 덮을 수 있는 상태인가(고를 수 있는 상태: 화면이 떠 있고 Idle · 잠금 아님 · 구매/퇴장 연출 아님). 코디네이터의 인벤토리 키 통과 판정에 쓴다.</summary>
    public bool CanBeCoveredByOwnedCards => true == IsVisible && EPhase.Idle == phase && false == bLocked;

    // 덮이는 중이거나 덮여 있는가(복귀 연출 중 포함).
    private bool IsCoverActive => true == bCovered || 0f < coverAmount;

    /// <summary>
    /// [시스템 연결 지점] 왼쪽 위 WAVE 배지. 언리밋이면 Unlimit-N. 한 번도 부르지 않으면 배지를 숨긴다(기획서 v3.2 C-2).
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
        // 코디네이터는 Awake/Initialize에서 이벤트를 구독하므로 열 때 판정하면 된다(Show 전에 정해야 레이아웃이 모드를 안다).
        bSlotRerollMode = null != SlotRerollRequestedEvent;
        CopyOffers(_offers);
        bCanReroll = _bCanReroll;

        Show();
        OnOffersPresented(false);
    }

    /// <summary>[호환] G-12 반영 후 삭제. 예전 모드: 리롤로 카드가 바뀌었다. 리롤은 선택 단계마다 1회라 쓰고 나면 _bCanReroll이 false로 온다.</summary>
    public virtual void ReplaceOffers(IReadOnlyList<RoguelikeCardViewData> _offers, bool _bCanReroll)
    {
        CopyOffers(_offers);
        bCanReroll = _bCanReroll;

        OnOffersPresented(true);
    }

    /// <summary>
    /// 칸별 모드: _index번 칸의 카드만 _offer로 바꾸고 그 칸만 다시 뒤집는다. 다른 칸 · 제목 연출 · 포커스는 그대로다.
    /// 포커스가 그 칸의 버튼이면 설명 패널만 갱신된다(다 썼으면 사용 불가 문구). 포커스가 그 카드면 뒤집히는 동안 커서를 숨기고 착지 때 다시 띄운다.
    /// 칸별 모드가 아니거나 칸이 없으면 경고 후 무시한다. 구매 연출 중(잠금)에도 무시한다.
    /// </summary>
    public virtual void ReplaceOffer(int _index, RoguelikeCardViewData _offer)
    {
        UI_RoguelikeCardSlot slot = FindSlotByOffer(_index);
        if (false == bSlotRerollMode || 0 > _index || offers.Count <= _index || null == slot)
        {
            Debug.LogWarning("[UIView_RoguelikeCardSelect] ReplaceOffer(" + _index + ")를 무시합니다. 칸별 리롤 모드가 아니거나 해당 칸이 없습니다.");
            return;
        }

        if (true == bLocked) return;

        offers[_index] = _offer;
        slot.Bind(_index, _offer);
        slot.PlayIntro(0f, CardRarity.IsRare(_offer.displayGrade));
        slot.SetRerollAvailable(_offer.bCanReroll);

        // Bind가 카드 호버 · 어둡게를 초기화하므로 현재 포커스에 맞게 다시 건다.
        if (null != focusedRelay && slot.Relay == focusedRelay)
        {
            slot.SetFocused(true);
            HideCursor();
        }

        ApplyDimming(GetFocusedSlot());
        // 새 카드로 설명 최대 높이가 달라질 수 있어 배치를 다시 잡는다(내비게이션은 바뀌지 않는다).
        LayoutControls();
        RefreshDescription();
    }

    /// <summary>
    /// 구매가 확정됐다. 고르지 않은 카드는 흐려지며 사라지고(배타 카드는 더 빠르게) → 고른 카드가 화면 중앙으로 → 잠깐 머문 뒤
    /// 보관 연출(UI_RoguelikeCardStoreDrop: 일반은 슬리브 0.18초 + 아래로 뚝 0.3초, 레어는 케이스째 0.3초, 낙하 시작과 함께 딤만 걷힘, 일반 약 1.18초 / 레어 약 1.86초)
    /// → HUD 트레이 도착 반응 → 화면을 닫고 _onComplete를 부른다.
    /// 레어 카드(_purchase.displayGrade)는 이동 · 잠깐 머묾 대신 아크릴 케이스에 끼우는 연출(UI_RoguelikeAcrylicCase)이 돈다.
    /// 트레이가 켜져 있으면 숫자를 직전 값으로 되돌려 잡아 둔다(시스템이 보유 카드 갱신을 이 호출보다 먼저 보낸다).
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
        bRarePurchase = null != acrylicCase && null != chosenSlot && true == CardRarity.IsRare(_purchase.displayGrade);

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
        // 일반 카드는 전용 선택음(기획서 v3.1 10-2), 레어 카드(MAX · 프리즘 · 계약)는 기존 공통 소리 그대로.
        Sound.PlayUI(CardRarity.IsRare(_purchase.displayGrade) ? SoundID.GetItem : SoundID.SFXCardSelectNormal);

        UI_RoguelikeOwnedCardTray tray = GetActiveTray();
        if (null != tray) tray.HoldPreviousCount();
    }

    /// <summary>런 강제 종료 등으로 구매 없이 닫는다. 화면 전체가 흐려지며 닫힌 뒤 _onComplete를 부른다.</summary>
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
        bRarePurchase = false;

        SetControlsVisible(false);
        HideCursor();
        StartExit(CloseSeconds);
    }

    /// <summary>선택·리롤이 거절됐다. 해당 카드나 리롤 버튼이 정수 픽셀로 흔들린다.</summary>
    public virtual void PlayRejected(ECardShopResult _reason, int _offerIndex)
    {
        Sound.PlayUI(SoundID.AbilityUpgradeFailed);
        UI_RoguelikeCardSlot slot = FindSlotByOffer(_offerIndex);
        if (true == bSlotRerollMode && ECardShopResult.RerollNotAllowed == _reason && null != slot && null != slot.RerollButton)
        {
            StartButtonShake(slot.RerollButton);
            return;
        }

        if (null != slot)
        {
            slot.PlayReject();
            return;
        }

        StartButtonShake(rerollButton);
    }

    /// <summary>
    /// 보유 카드 창 · ESC 도전과제 창이 열리고 닫히는 동안 이 화면을 가라앉혀 숨기거나(true) 떠올려 되돌린다(false). 코디네이터가 두 창의 열기/닫힘에 맞춰 부른다.
    /// 덮여 있는 동안에는 입력을 받지 않는다. 복귀가 끝나면 이전 포커스 카드에 커서를 다시 띄우고, 패드 모드이며 이 화면 위에 뜬 UI(ESC 메뉴 등)가 선택을 쥐고 있지 않으면 그 카드를 다시 선택한다(마우스 모드는 선택하지 않는다).
    /// 고를 수 있는 상태(Idle)가 아니면 덮지 않는다(퇴장 · 구매 연출의 화면 투명도와 겹치지 않게).
    /// </summary>
    public void SetCoveredByOwnedCards(bool _bCovered)
    {
        if (_bCovered == bCovered) return;

        if (true == _bCovered)
        {
            if (false == CanBeCoveredByOwnedCards) return;

            if (0f >= coverAmount) coverBaseY = contentRoot.anchoredPosition.y;
            bCovered = true;
            HideCursor();
            SetCoverInputBlocked(true);
            return;
        }

        bCovered = false;
    }

    /// <summary>덮임 입력 차단. 덮여 있는 동안 화면 안의 카드 · 리롤이 마우스 · 제출을 받지 않게 한다.</summary>
    private void SetCoverInputBlocked(bool _bBlocked)
    {
        if (null == screenGroup) return;

        screenGroup.blocksRaycasts = false == _bBlocked;
        screenGroup.interactable = false == _bBlocked;
    }

    /// <summary>덮임 연출 진행(unscaled). Out은 EaseIn(t²)로 가라앉고, In은 같은 곡선을 거꾸로 되짚어 EaseOut으로 떠오른다. Content는 정수 픽셀로만 움직인다.</summary>
    private void UpdateCover()
    {
        if (false == IsCoverActive) return;

        float target = true == bCovered ? 1f : 0f;
        float duration = true == bCovered ? CoverOutSeconds : CoverInSeconds;
        coverAmount = Mathf.MoveTowards(coverAmount, target, Time.unscaledDeltaTime / duration);

        float eased = coverAmount * coverAmount;
        if (null != screenGroup) screenGroup.alpha = 1f - eased;
        contentRoot.anchoredPosition = new Vector2(contentRoot.anchoredPosition.x, coverBaseY - Mathf.Round(CoverSinkPixels * eased));

        if (true == bCovered || 0f < coverAmount) return;

        // 복귀 완료: 입력을 되살리고 이전 포커스 카드에 커서를 다시 띄운다.
        // 투명도 · Content 위치는 위에서 이미 원래 값(eased = 0)이 됐다. 이 시점은 bCovered = false, coverAmount = 0이라
        // ResetCoverState의 가드(IsCoverActive)에 걸리므로 입력 차단만 직접 푼다.
        SetCoverInputBlocked(false);
        if (null == focusedRelay) return;

        ShowCursor(focusedRelay);
        if (null != viewCtx && null != viewCtx.inputManager && true == viewCtx.inputManager.IsGamepadMode && true == CanRestoreSelection(EventSystem.current)) SelectInEventSystem(focusedRelay);
    }

    /// <summary>덮임 상태를 처음으로 돌린다(투명도 · Content 위치 · 입력). 덮인 채 강제로 닫혀도 다음 열기에 남지 않게 열기/닫기/복귀 완료에서 부른다.</summary>
    private void ResetCoverState()
    {
        if (false == IsCoverActive) return;

        bCovered = false;
        coverAmount = 0f;
        if (null != screenGroup) screenGroup.alpha = 1f;
        contentRoot.anchoredPosition = new Vector2(contentRoot.anchoredPosition.x, coverBaseY);
        SetCoverInputBlocked(false);
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
        bRarePurchase = false;
        bLocked = false;
        phase = EPhase.Idle;
        if (null != acrylicCase) acrylicCase.ResetCase();
        SetControlsVisible(true);
        // 뷰가 처음 열릴 때만 제목 글자가 차례로 튀어나온다(리롤 때는 다시 하지 않는다). 이후 wave는 계속된다.
        if (false == _bReroll)
        {
            if (null != titleAnimator) titleAnimator.PlayRevealBounce();
            if (null != titleShadowAnimator) titleShadowAnimator.PlayRevealBounce();
        }

        PlaceSlots(count);
        UpdateControls();
        LayoutControls();
        RebuildNavigation();

        UI_RoguelikeSelectRelay first = count > 0 ? slots[0].Relay : (false == bSlotRerollMode ? rerollRelay : null);
        SetFocus(first, true);
        SelectInEventSystem(first);

        // 처음 포커스한 카드의 설명 높이로 바로 맞춘다(열리자마자 높이가 바뀌는 연출이 보이지 않게).
        descPanel.SnapHeight();
    }

    // // 입력 전달

    protected void RequestBuy(int _offerIndex) => BuyRequestedEvent?.Invoke(_offerIndex);
    // [호환] G-12 반영 후 삭제.
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
        ComputeIntroDelays(_count);

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
            slot.PlayIntro(introDelays[i], CardRarity.IsRare(offers[i].displayGrade));
            slot.SetRerollVisible(bSlotRerollMode);
            if (true == bSlotRerollMode) slot.SetRerollAvailable(offers[i].bCanReroll);
        }
    }

    /// <summary>
    /// 카드별 뒤집기 시작 지연(초). 희귀 카드가 없으면 예전과 같은 i * 0.13초다.
    /// 희귀 카드가 있으면 일반 카드를 제시 순서대로 0, 0.13, 0.26... 초에 먼저 시작하고(희귀 칸은 건너뜀), 희귀 카드는 마지막 일반 카드 시작 + 0.3초에 시작한다(일반 카드가 없으면 0).
    /// 희귀가 여러 장이면 두 번째부터 앞 희귀 시작 + 0.9초.
    /// </summary>
    private void ComputeIntroDelays(int _count)
    {
        if (introDelays.Length < _count) introDelays = new float[_count];

        bool bAnyRare = false;
        for (int i = 0; i < _count; i++)
        {
            if (true == CardRarity.IsRare(offers[i].displayGrade))
            {
                bAnyRare = true;
                break;
            }
        }

        if (false == bAnyRare)
        {
            for (int i = 0; i < _count; i++) introDelays[i] = i * IntroIntervalSeconds;
            return;
        }

        int normalCount = 0;
        float lastNormal = 0f;
        for (int i = 0; i < _count; i++)
        {
            if (true == CardRarity.IsRare(offers[i].displayGrade)) continue;

            introDelays[i] = normalCount * IntroIntervalSeconds;
            lastNormal = introDelays[i];
            normalCount++;
        }

        float rareStart = 0 < normalCount ? lastNormal + RareAfterNormalSeconds : 0f;
        for (int i = 0; i < _count; i++)
        {
            if (false == CardRarity.IsRare(offers[i].displayGrade)) continue;

            introDelays[i] = rareStart;
            rareStart += RareIntervalSeconds;
        }
    }

    private string Localize(int _key)
    {
        return null != localizationManager ? localizationManager.GetText(_key) : string.Empty;
    }

    private string Localize(string _key)
    {
        return null != localizationManager ? localizationManager.GetText(_key) : string.Empty;
    }

    /// <summary>칸별 리롤 버튼의 이름·설명. 이미 썼으면 설명 자리에 이유(사용 불가)를 보여 준다.</summary>
    private void GetSlotRerollInfo(bool _bAvailable, out string _name, out string _description)
    {
        _name = Localize(SlotRerollNameKey);
        _description = true == _bAvailable ? Localize(SlotRerollDescKey) : Localize(LocKeys.RoguelikeUI.roguelikeCardSelectUnavailable);
    }

    /// <summary>[호환] G-12 반영 후 삭제. 예전 모드 공용 리롤 버튼의 이름·설명. 쓸 수 없으면 설명 자리에 이유(사용 불가)를 보여 준다.</summary>
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
            float plateWidth = Mathf.Max(SpecTitlePlateWidth, Mathf.Ceil(titleText.GetPreferredValues(titlePlainText, 10000f, 10000f).x) + 32f);
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

        // 카드 선택 모드: 폭은 고정(화면이 좁으면 화면 폭 안). 높이는 카드마다 바뀌지만 화면 배치는 가장 큰 높이 기준으로 잡는다.
        descPanel.SetFixedWidth(Mathf.Min(SelectDescWidth, screen.rect.width - ScreenMargin * 2f));

        descPanel.Clear();
        float height = descPanel.TargetHeight;
        for (int i = 0; i < slotCount; i++) height = Mathf.Max(height, descPanel.MeasureCardHeight(slots[i].Data));
        if (true == bSlotRerollMode)
        {
            GetSlotRerollInfo(true, out string slotName, out string slotAvailableDescription);
            height = Mathf.Max(height, descPanel.MeasureInfoHeight(slotName, slotAvailableDescription));
            GetSlotRerollInfo(false, out slotName, out string slotUnavailableDescription);
            height = Mathf.Max(height, descPanel.MeasureInfoHeight(slotName, slotUnavailableDescription));
        }
        else
        {
            GetRerollInfo(out string rerollName, out string rerollDescription);
            height = Mathf.Max(height, descPanel.MeasureInfoHeight(rerollName, rerollDescription));
        }

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

    /// <summary>[호환] G-12 반영 후 삭제. 예전 모드: 리롤 가능 여부를 공용 버튼 상태(활성/어둡게)에 반영한다. 칸별 버튼은 PlaceSlots · ReplaceOffer가 칸마다 반영한다.</summary>
    private void UpdateControls()
    {
        rerollButton.interactable = bCanReroll;
        rerollGroup.alpha = bCanReroll ? 1f : DisabledButtonAlpha;
        if (null != rerollCountText) rerollCountText.text = bCanReroll ? "1" : "0";
    }

    private void SetControlsVisible(bool _bVisible)
    {
        // 칸별 모드는 공용 버튼을 항상 숨기고 카드별 버튼을 _bVisible에 맞춘다.
        rerollButton.gameObject.SetActive(_bVisible && false == bSlotRerollMode);
        if (true == bSlotRerollMode)
        {
            for (int i = 0; i < slotCount; i++) slots[i].SetRerollVisible(_bVisible);
        }

        descPanel.gameObject.SetActive(_bVisible);
        if (null != titlePlate) titlePlate.gameObject.SetActive(_bVisible);
        if (null != waveBadge) waveBadge.gameObject.SetActive(_bVisible && bWaveSet);
        RefreshPadGuide(_bVisible);
    }

    /// <summary>
    /// 패드·키보드 이동. Automatic은 픽셀 아트 배치에서 엉뚱한 곳으로 가므로 Explicit으로 직접 잇는다.
    /// 예전 모드: 카드끼리는 좌우, 카드에서 아래는 공용 리롤 버튼, 버튼에서 위는 가운데 카드.
    /// 칸별 모드: 카드 i는 좌우 옆 카드, 아래는 카드 i의 리롤 버튼. 리롤 버튼 i는 위가 카드 i, 좌우는 옆 칸 리롤 버튼, 아래는 없음.
    /// 리롤을 이미 써서 못 쓰는 상태여도 선택은 된다(설명 패널에서 사용 불가 사유를 읽을 수 있게).
    /// </summary>
    private void RebuildNavigation()
    {
        if (true == bSlotRerollMode)
        {
            for (int i = 0; i < slotCount; i++)
            {
                SetExplicitNavigation(slots[i].SelectButton, i > 0 ? slots[i - 1].SelectButton : null,
                    i < slotCount - 1 ? slots[i + 1].SelectButton : null, null, slots[i].RerollButton);
                SetExplicitNavigation(slots[i].RerollButton, i > 0 ? slots[i - 1].RerollButton : null,
                    i < slotCount - 1 ? slots[i + 1].RerollButton : null, slots[i].SelectButton, null);
            }

            return;
        }

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
        FindSlotByRerollRelay(focusedRelay)?.SetRerollFocused(false);
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

        // 칸별 리롤 버튼에 포커스가 오면 그 칸 카드는 올라가지 않고 나머지 카드만 어둡게 해 어느 카드를 바꾸는지 보이게 한다.
        UI_RoguelikeCardSlot rerollSlot = FindSlotByRerollRelay(_relay);
        if (null != rerollSlot) rerollSlot.SetRerollFocused(true);

        ApplyDimming(null != slot ? slot : rerollSlot);
        RefreshDescription();
        ShowCursor(_relay);
    }

    /// <summary>카드가 선택 중이면 나머지 카드를 어둡게 해 밝기로도 선택 중인 카드를 구분한다. 예전 모드 공용 리롤 버튼에 포커스가 있거나 포커스가 없으면 모두 원래 밝기. 칸별 리롤 버튼이면 그 칸 외의 카드를 어둡게 한다(_focusedSlot = 그 칸).</summary>
    private void ApplyDimming(UI_RoguelikeCardSlot _focusedSlot)
    {
        // 희귀 카드의 두근 ~ 유지 시작 사이에는 그 카드를 뺀 나머지를 어둡게 한다(희귀 등장 연출의 스포트라이트).
        if (null != rareSpotlightSlot)
        {
            for (int i = 0; i < slotCount; i++) slots[i].SetDimmed(slots[i] != rareSpotlightSlot);
            return;
        }

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

        UI_RoguelikeCardSlot rerollSlot = FindSlotByRerollRelay(focusedRelay);
        if (null != rerollSlot)
        {
            GetSlotRerollInfo(rerollSlot.Data.bCanReroll, out string slotName, out string slotDescription);
            descPanel.SetInfo(slotName, slotDescription);
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

    /// <summary>그 relay가 칸별 리롤 버튼인 칸. 카드 relay · 공용 리롤 relay면 null.</summary>
    private UI_RoguelikeCardSlot FindSlotByRerollRelay(UI_RoguelikeSelectRelay _relay)
    {
        if (null == _relay) return null;

        for (int i = 0; i < slotCount; i++)
        {
            if (slots[i].RerollRelay == _relay) return slots[i];
        }

        return null;
    }

    /// <summary>포커스가 카드 또는 칸별 리롤 버튼에 있으면 그 칸, 아니면 null.</summary>
    private UI_RoguelikeCardSlot GetFocusedSlot()
    {
        UI_RoguelikeCardSlot slot = FindSlotByRelay(focusedRelay);
        return null != slot ? slot : FindSlotByRerollRelay(focusedRelay);
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

    private void OnSlotRareSpotlight(UI_RoguelikeCardSlot _slot, bool _bOn)
    {
        if (true == _bOn) rareSpotlightSlot = _slot;
        else if (_slot == rareSpotlightSlot) rareSpotlightSlot = null;

        ApplyDimming(GetFocusedSlot());
    }

    private void OnSlotRerollClicked(UI_RoguelikeCardSlot _slot)
    {
        if (bLocked || false == bSlotRerollMode) return;
        // 그 칸이 뒤집히는 중이면 무시한다(새 카드가 드러나기 전에 또 바꾸지 못하게).
        if (true == _slot.IsIntroPlaying) return;

        Sound.PlayUI(SoundID.MainClick);
        SlotRerollRequestedEvent?.Invoke(_slot.OfferIndex);
    }

    // [호환] G-12 반영 후 삭제.
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

    /// <summary>[호환] G-12 반영 후 삭제(칸별 버튼은 UI_RoguelikeCardSlot이 같은 연출을 한다). 공용 리롤 버튼에 포커스가 오면 배경이 밝아지고 아이콘이 1px 뜬다(카드 호버와 같은 결의 약한 반응).</summary>
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
                if (true == bRarePurchase)
                {
                    StartRareCase();
                    break;
                }

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
                StartStore();
                break;

            case EPhase.PurchaseRareCase:
                // 끝은 아크릴 케이스가 알려 준다(OnRareCaseDone).
                break;

            case EPhase.PurchaseStore:
                // 끝은 보관 연출이 알려 준다(OnStoreDropDone). 딤은 낙하가 시작된 뒤 따로 걷힌다.
                UpdateDimFade();
                break;

            case EPhase.Exit:
                float t = Mathf.Clamp01(phaseTime / exitDuration);
                float eased = t * t;
                if (null != screenGroup) screenGroup.alpha = 1f - eased;
                contentRoot.anchoredPosition = new Vector2(contentRoot.anchoredPosition.x, exitBaseY + Mathf.Round(ExitLiftPixels * eased));
                if (1f <= t) Complete();
                break;
        }
    }

    /// <summary>퇴장 단계 시작. 화면 전체가 흐려지며 Content가 ExitLiftPixels만큼 떠오른다.</summary>
    private void StartExit(float _duration)
    {
        exitDuration = Mathf.Max(0.01f, _duration);
        exitBaseY = contentRoot.anchoredPosition.y;
        phase = EPhase.Exit;
        phaseTime = 0f;
    }

    /// <summary>퇴장 연출로 바뀐 Content 위치 · 화면 투명도를 원래대로 돌린다. phase를 바꾸기 전에 불러야 한다.</summary>
    private void RestoreExitState()
    {
        ResetCoverState();
        if (EPhase.Exit == phase) contentRoot.anchoredPosition = new Vector2(contentRoot.anchoredPosition.x, exitBaseY);
        if (null != screenGroup) screenGroup.alpha = 1f;
        RestoreDim();
    }

    /// <summary>보관 연출이 걷은 딤 알파를 원래대로 돌린다.</summary>
    private void RestoreDim()
    {
        bDimFading = false;
        dimFadeTime = 0f;
        if (null == dimImage) return;

        Color color = dimImage.color;
        color.a = dimBaseAlpha;
        dimImage.color = color;
    }

    /// <summary>딤 이미지만 1 - t^2로 걷는다(카드 · 화면 전체는 그대로). 낙하가 시작된 뒤(OnStoreDropStart)부터 DimFadeSeconds 동안.</summary>
    private void UpdateDimFade()
    {
        if (false == bDimFading || null == dimImage) return;

        dimFadeTime += Time.unscaledDeltaTime;
        float t = Mathf.Clamp01(dimFadeTime / DimFadeSeconds);
        Color color = dimImage.color;
        color.a = dimBaseAlpha * (1f - t * t);
        dimImage.color = color;
    }

    /// <summary>HUD 보관함 트레이. 연결되어 있지 않거나 꺼져 있으면(첫 선택) null - 트레이 관련 호출을 모두 건너뛴다.</summary>
    private static UI_RoguelikeOwnedCardTray GetActiveTray()
    {
        UI_RoguelikeOwnedCardTray tray = RoguelikeHUDLink.OwnedTray;
        return null != tray && true == tray.gameObject.activeInHierarchy ? tray : null;
    }

    /// <summary>
    /// 레어 카드: 카드를 케이스 위에 걸친 자리(최종 위치 + InsertDepthPixels)로 보내면서 케이스가 떠오르게 한다. 그 뒤 끼움 · 딸깍 · 광택은 케이스가 카드(슬롯) 위치까지 직접 움직여 진행한다.
    /// 케이스에 끼울 카드라 호버로 떠 있던 것을 풀고(최종 위치에서 케이스 안쪽 4px 정렬이 어긋나지 않게) 밑 그림자를 끈다.
    /// </summary>
    private void StartRareCase()
    {
        Vector2 finalPosition = GetCenterPosition();

        chosenSlot.SetFocused(false);
        chosenSlot.Card.SetShadowVisible(false);
        chosenSlot.PlayMove(finalPosition + new Vector2(0f, UI_RoguelikeAcrylicCase.InsertDepthPixels), MoveSeconds);
        acrylicCase.Play((RectTransform)chosenSlot.transform, finalPosition, rareCaseDoneHandler);

        phase = EPhase.PurchaseRareCase;
        phaseTime = 0f;
    }

    private void OnRareCaseDone()
    {
        if (EPhase.PurchaseRareCase != phase) return;

        StartStore();
    }

    /// <summary>
    /// 보관 연출 시작(일반: 이동 · 머묾 뒤, 레어: 케이스 연출 뒤). 고른 카드(레어는 케이스째)가 아래로 뚝 떨어진다.
    /// 호버로 떠 있던 것을 풀고 카드 밑 그림자를 끈다(레어는 StartRareCase가 이미 했다). storeDrop · dimImage · 고른 슬롯 중 하나라도 없으면 기존 퇴장으로 닫는다
    /// (이때 PlayPurchase가 잡아 둔 트레이 숫자는 화면이 닫힐 때 OnHideContent가 반응 없이 푼다).
    /// </summary>
    private void StartStore()
    {
        if (null == storeDrop || null == dimImage || null == chosenSlot)
        {
            StartExit(ExitSeconds);
            return;
        }

        chosenSlot.SetFocused(false);
        chosenSlot.Card.SetShadowVisible(false);

        // Play 안에서 낙하 시작 콜백이 바로 불릴 수 있으므로(슬리브 없음) 단계를 먼저 바꾼다.
        phase = EPhase.PurchaseStore;
        phaseTime = 0f;
        storeDrop.Play((RectTransform)chosenSlot.transform, true == bRarePurchase ? acrylicCase : null, false == bRarePurchase, storeDropStartHandler, storeDropDoneHandler);
    }

    private void OnStoreDropStart()
    {
        if (EPhase.PurchaseStore != phase) return;

        bDimFading = true;
        dimFadeTime = 0f;
    }

    private void OnStoreDropDone()
    {
        if (EPhase.PurchaseStore != phase) return;

        UI_RoguelikeOwnedCardTray tray = GetActiveTray();
        if (null != tray) tray.ReleaseCountWithEffect();

        Complete();
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
        RestoreExitState();
        phase = EPhase.Idle;

        Hide();
        callback?.Invoke();
    }

    /// <summary>제목 · 리롤 글자 · 키 안내 문구. 언어가 바뀌면 다시 채운다.</summary>
    private void ApplyStaticTexts()
    {
        titlePlainText = null != localizationManager ? localizationManager.GetText(PickKey) : string.Empty;
        // 파도 + 색 순환 태그로 감싼다. 태그는 TMPInlineStyleAnimator가 제거하므로 판 폭은 titlePlainText 기준으로 잰다.
        if (null != titleText) titleText.text = TitleTagOpen + titlePlainText + TitleTagClose;
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

        ResetCoverState();
        if (null != screenGroup) screenGroup.alpha = 1f;
        RestoreDim();
        LayoutControls();
        pendingComplete = null;
        phase = EPhase.Idle;
        bLocked = false;
        bRarePurchase = false;
        focusedRelay = null;
        if (null != storeDrop) storeDrop.ResetDrop();
        if (null != acrylicCase) acrylicCase.ResetCase();
    }

    /// <summary>
    /// 화면이 닫힌다. 연출 중 밖에서 닫혔다면(ForceCloseCardSelection) 대기 중이던 완료 콜백은 부르지 않고 버린다 -
    /// 시스템은 이미 다음 단계로 넘어간 뒤라 늦은 완료 신호가 다음 단계를 건드릴 수 있다.
    /// </summary>
    protected override void OnHideContent()
    {
        RestoreExitState();
        pendingComplete = null;
        phase = EPhase.Idle;
        bLocked = false;
        bRarePurchase = false;
        chosenSlot = null;
        // 연출 중 밖에서 닫혀도(ForceCloseCardSelection) 슬리브를 숨기고 카드 · 케이스를 원래 자리로 돌린다(완료 콜백은 부르지 않는다).
        // 보관 낙하는 케이스를 같이 옮기므로 낙하를 먼저 되돌린 뒤 케이스를 되돌린다.
        if (null != storeDrop) storeDrop.ResetDrop();
        if (null != acrylicCase) acrylicCase.ResetCase();
        // 보관 연출이 잡아 둔 트레이 숫자가 강제 닫힘으로 묶이지 않게 반응 없이 푼다(정상 종료는 OnStoreDropDone이 이미 풀었다).
        UI_RoguelikeOwnedCardTray tray = RoguelikeHUDLink.OwnedTray;
        if (null != tray) tray.ReleaseCount();
        FindSlotByRelay(focusedRelay)?.SetFocused(false);
        FindSlotByRerollRelay(focusedRelay)?.SetRerollFocused(false);
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
        rareCaseDoneHandler = OnRareCaseDone;
        storeDropStartHandler = OnStoreDropStart;
        storeDropDoneHandler = OnStoreDropDone;
        cursorMotion = CursorMotionSettings.Card;
        if (null != acrylicCase) acrylicCase.Initialize();
        if (null != storeDrop) storeDrop.Initialize();
        if (null != dimImage) dimBaseAlpha = dimImage.color.a;

        // 프리팹에 잡아 둔 Content 기본 위치. 이후 레이아웃은 이 값에서 다시 계산한다.
        contentDefaultY = contentRoot.anchoredPosition.y;
        deviceChangedHandler = OnDeviceChanged;
        if (null != waveBadge) waveBadge.gameObject.SetActive(false);

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].ClickedEvent += OnSlotClicked;
            slots[i].RevealedEvent += OnSlotRevealed;
            slots[i].LandedEvent += OnSlotLanded;
            slots[i].RareSpotlightEvent += OnSlotRareSpotlight;
            slots[i].Relay.FocusedEvent += OnRelayFocused;
            slots[i].RerollClickedEvent += OnSlotRerollClicked;
            if (null != slots[i].RerollRelay) slots[i].RerollRelay.FocusedEvent += OnRelayFocused;
        }

        rerollRelay.FocusedEvent += OnRelayFocused;
        rerollButton.onClick.AddListener(OnRerollClicked);
    }

    public override void Update()
    {
        base.Update();

        UpdatePhase();
        UpdateCover();
        UpdateButtonShake();

        // 보유 카드 창에 덮여 있는 동안에는 리롤 포커스 갱신과 선택 복구를 하지 않는다(그 창이 선택을 쥐고 있다).
        if (true == IsCoverActive) return;

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
        SlotRerollRequestedEvent = null;
        pendingComplete = null;
        UnsubscribeLanguage();
        base.OnDestroy();
    }
}
