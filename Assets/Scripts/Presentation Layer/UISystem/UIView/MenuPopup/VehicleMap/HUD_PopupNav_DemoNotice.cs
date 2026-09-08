using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using PresentationLayer.UISystem;

public class HUD_PopupNav_DemoNotice : MonoBehaviour, IUIDepthCloseable
{
    [Header("External Links")]
    [Tooltip("Steam 상점 페이지 / 찜하기 URL")]
    [SerializeField] private string steamWishlistUrl = "https://store.steampowered.com/app/YOUR_APP_ID/";
    [Tooltip("공식 디스코드 커뮤니티 URL")]
    [SerializeField] private string discordCommunityUrl = "https://discord.gg/your_invite_link";

    [Header("Demo Notice UI References")]
    [Tooltip("데모 안내 오버레이 루트 오브젝트 (Dim 및 배너 포함)")]
    [SerializeField] private GameObject demoNoticeOverlay;
    [Tooltip("데모 안내 전체 화면 Dim 캔버스 그룹")]
    [SerializeField] private CanvasGroup demoDimCanvasGroup;
    [Tooltip("Title DimBG 스타일 좌우 펼쳐짐 띠 배너")]
    [SerializeField] private RectTransform demoBandTransform;
    [Tooltip("내부 콘텐츠(텍스트 및 버튼) 가시성/알파 제어용 캔버스 그룹")]
    [SerializeField] private CanvasGroup demoContentCanvasGroup;
    [Tooltip("데모 안내 타이틀 텍스트")]
    [SerializeField] private TextMeshProUGUI demoTitleText;
    [Tooltip("데모 안내 설명 텍스트 (통합 줄바꿈 텍스트)")]
    [SerializeField] private TextMeshProUGUI demoDescText;
    [Tooltip("데모 안내 타이틀 스타일 애니메이터")]
    [SerializeField] private TMPInlineStyleAnimator demoTitleAnimator;
    [Tooltip("데모 안내 설명 스타일 애니메이터")]
    [SerializeField] private TMPInlineStyleAnimator demoDescAnimator;
    [Tooltip("스팀 찜하기 외부 링크 버튼")]
    [SerializeField] private UI_ExternalLinkButton steamWishlistBtn;
    [Tooltip("디스코드 외부 링크 버튼")]
    [SerializeField] private UI_ExternalLinkButton discordBtn;

    [Header("Demo Notice Animation Settings - Show")]
    [Tooltip("DimBG 알파 페이드 인 연출 시간")]
    [SerializeField] private float dimFadeDuration = 0.2f;
    [Tooltip("DimBG 알파 페이드 인 이즈(Ease)")]
    [SerializeField] private Ease dimFadeEase = Ease.Linear;
    [Tooltip("상호작용 패널/밴드 쫀득한 펴짐 연출 시간")]
    [SerializeField] private float panelScaleDuration = 0.3f;
    [Tooltip("펼쳐지는 연출 이즈(Ease)")]
    [SerializeField] private Ease titleBandEase = Ease.OutBack;
    [Tooltip("바운스(반동) 강도 (낮을수록 약함, 기본 1.0 / DOTween 기본 1.7)")]
    [SerializeField] private float titleBandOvershoot = 1.0f;

    [Header("Demo Notice Animation Settings - Hide")]
    [Tooltip("콘텐츠 페이드 아웃 연출 시간")]
    [SerializeField] private float contentFadeOutDuration = 0.15f;
    [Tooltip("밴드 축소 닫힘 연출 시간")]
    [SerializeField] private float bandCloseDuration = 0.2f;
    [Tooltip("밴드 축소 이즈(Ease)")]
    [SerializeField] private Ease bandCloseEase = Ease.InBack;
    [Tooltip("DimBG 페이드 아웃 연출 시간")]
    [SerializeField] private float dimCloseDuration = 0.2f;

    // 내부 의존성 및 상태
    private HUD_PopupNav_Main mainController;
    private LocalizationManager localizationManager;
    private UIDepthController depthController;
    private ICursorBoxUI cursorBoxUI;
    private InputManager inputManager;
    private GameObject previousSelectedObject;
    private Action<EInputDeviceType> cachedOnInputDeviceChanged;

    private int focusedButtonIndex = 0; // 0: discordBtn (가장 왼쪽), 1: steamWishlistBtn
    private bool isDemoNoticeShowing = false;
    private bool isHiding = false;
    private Tween demoNoticeTween;

    /// <summary>
    /// 이 빌드에 상점 버튼이 있는지. <b>STOVE 데모에는 상점 버튼 자체가 없습니다</b>(ApplyStoreBranding 참고).
    ///
    /// 버튼을 끄는 쪽과 패드가 버튼을 잡는 쪽이 반드시 같은 값을 봐야 해서 판정을 여기 한 곳에만 둡니다.
    /// 둘이 어긋나면 화면에 없는 버튼을 패드로만 누를 수 있는, 눈으로는 못 찾는 상태가 됩니다.
    /// </summary>
    private bool HasStoreButton => null != steamWishlistBtn && false == BuildInfo.IsStove;

    /// <summary>패드로 갈 수 있는 마지막 칸입니다. 상점 버튼이 없으면 디스코드 한 칸뿐입니다.</summary>
    private int MaxFocusIndex => HasStoreButton ? 1 : 0;

    public bool IsDemoNoticeShowing => isDemoNoticeShowing;
    public bool IsHiding => isHiding;
    public bool IsDemoNoticeActive => (true == isDemoNoticeShowing || true == isHiding);

    // IUIDepthCloseable 구현: ESC로 뎁스 스택에서 닫힐 때 호출됩니다.
    public bool IsActive => isDemoNoticeShowing;
    public void Hide() => HideDemoNoticeOverlay();

    public void FocusDemoButton(int _index, bool _playSound = true)
    {
        if (null == inputManager || false == inputManager.IsGamepadMode) return;

        focusedButtonIndex = Mathf.Clamp(_index, 0, MaxFocusIndex);

        if (0 == focusedButtonIndex)
        {
            if (null != steamWishlistBtn)
            {
                steamWishlistBtn.UnfocusButtonImmediate();
            }
            if (null != discordBtn)
            {
                discordBtn.FocusButton(_playSound);
            }
        }
        else
        {
            if (null != discordBtn)
            {
                discordBtn.UnfocusButtonImmediate();
            }
            if (null != steamWishlistBtn)
            {
                steamWishlistBtn.FocusButton(_playSound);
            }
        }
    }

    public bool HandleDirectionalInput(Vector2 _input)
    {
        if (false == isDemoNoticeShowing || true == isHiding) return false;
        if (null == inputManager || false == inputManager.IsGamepadMode) return false;

        if (_input.x <= -0.5f)
        {
            if (1 == focusedButtonIndex)
            {
                FocusDemoButton(0, true);
                return true;
            }
        }
        else if (_input.x >= 0.5f)
        {
            // 상점 버튼이 없는 빌드(STOVE 데모)에서는 오른쪽 칸이 아예 없다.
            if (0 == focusedButtonIndex && 1 <= MaxFocusIndex)
            {
                FocusDemoButton(1, true);
                return true;
            }
        }

        return false;
    }

    public void HandleInteractionKey()
    {
        if (false == isDemoNoticeShowing || true == isHiding) return;

        if (0 == focusedButtonIndex)
        {
            if (null != discordBtn)
            {
                discordBtn.ExecuteClick();
            }
        }
        else
        {
            if (null != steamWishlistBtn)
            {
                steamWishlistBtn.ExecuteClick();
            }
        }
    }

    /// <summary>DemoNoticeUI.json 의 본문 엔트리 id 입니다. 스토어마다 문구가 다릅니다.</summary>
    private const int DESC_ENTRY_STEAM = 2;
    private const int DESC_ENTRY_STOVE = 3;

    /// <summary>
    /// 상점 버튼을 현재 스토어에 맞춥니다.
    ///
    /// 이 팝업은 데모 빌드에서만 뜨므로(BuildInfo.IsDemo), 데모를 내는 스토어의 유저는 이 화면을
    /// 반드시 봅니다. 그래서 링크와 로고가 스토어와 어긋나면 유저가 바로 알아챕니다.
    ///
    /// [STOVE는 버튼을 통째로 끕니다]
    /// STOVE 데모에서는 <b>본편 상점으로 보내지 않기로 했습니다.</b> URL도 로고도 넣지 않으므로,
    /// 눌러도 아무 데도 가지 않는 버튼이나 Steam 로고가 붙은 버튼을 남기는 대신 버튼 자체를 끕니다.
    /// 부모(ButtonField)가 HorizontalLayoutGroup + ContentSizeFitter라, 끄면 남은 디스코드 버튼이
    /// 알아서 가운데로 옵니다. 프리팹을 STOVE용으로 따로 만들 필요가 없습니다.
    ///
    /// 문구도 같이 갈립니다. DemoNoticeUI.json entry 3(STOVE 본문)에는 찜하기 안내가 없습니다.
    /// 버튼만 끄고 문구를 그대로 두면 <b>없는 버튼을 누르라고 말하는 화면</b>이 됩니다.
    ///
    /// [itch는 분기하지 않습니다 - 일부러 그렇습니다]
    /// 스토어가 셋인데 여기가 STOVE/그 외 이분법인 것이 빠뜨린 것처럼 보일 수 있습니다.
    /// itch 데모의 상점 버튼은 <b>Steam 위시리스트로 보내기로 했으므로</b>, itch가 아래쪽으로
    /// 떨어져 Steam URL·Steam 로고·Steam 문구를 그대로 쓰는 것이 곧 정답입니다.
    ///
    /// 로고는 "이 빌드가 어느 스토어에서 왔는가"가 아니라 <b>"이 버튼이 어디를 여는가"</b>를
    /// 나타냅니다. 버튼이 Steam을 열면 어느 스토어의 빌드든 Steam 로고가 맞습니다.
    /// itchStoreUrl이나 itch 로고를 새로 만들면 <b>지금 맞는 화면이 오히려 틀어집니다.</b>
    ///
    /// itch를 자기 상점으로 안내하기로 방침이 바뀌면 그때 세 갈래로 넓히고,
    /// PlatformConsistencyGuard의 브랜딩 검사도 itch까지 함께 넓히십시오.
    /// </summary>
    private void ApplyStoreBranding()
    {
        if (null == steamWishlistBtn) return;

        // 버튼의 존재 여부는 HasStoreButton 한 곳에서만 판정한다. 패드 이동 범위도 같은 값을 본다.
        if (false == HasStoreButton)
        {
            steamWishlistBtn.gameObject.SetActive(false);
            return;
        }

        steamWishlistBtn.gameObject.SetActive(true);

        if (false == string.IsNullOrEmpty(steamWishlistUrl))
        {
            steamWishlistBtn.SetUrl(steamWishlistUrl);
        }
    }

    public void Initialize(HUD_PopupNav_Main _mainController, LocalizationManager _localizationManager, UIDepthController _depthController = null, ICursorBoxUI _cursorBoxUI = null, InputManager _inputManager = null)
    {
        mainController = _mainController;
        localizationManager = _localizationManager;
        depthController = _depthController;
        cursorBoxUI = _cursorBoxUI;
        inputManager = _inputManager;

        if (null != steamWishlistBtn)
        {
            steamWishlistBtn.SetCursorBoxUI(cursorBoxUI, inputManager);
        }
        if (null != discordBtn)
        {
            discordBtn.SetCursorBoxUI(cursorBoxUI, inputManager);
        }

        if (null != discordBtn && null != steamWishlistBtn)
        {
            Navigation _navNone = new Navigation();
            _navNone.mode = Navigation.Mode.None;
            discordBtn.navigation = _navNone;
            steamWishlistBtn.navigation = _navNone;
        }

        if (null == cachedOnInputDeviceChanged) cachedOnInputDeviceChanged = OnInputDeviceChanged;
        if (null != inputManager && null != inputManager.inputReader)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
            inputManager.inputReader.InputDeviceChangedEvent += cachedOnInputDeviceChanged;
        }

        ResetNotice();
    }

    private void OnInputDeviceChanged(EInputDeviceType _device)
    {
        if (false == isDemoNoticeShowing) return;

        if (EInputDeviceType.Gamepad == _device)
        {
            FocusDemoButton(focusedButtonIndex, false);
        }
        else
        {
            if (null != discordBtn) discordBtn.UnfocusButtonImmediate();
            if (null != steamWishlistBtn) steamWishlistBtn.UnfocusButtonImmediate();
            if (null != EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }

    public void ShowDemoNoticeOverlay(MapType _restrictedMapType = MapType.None)
    {
        if (null == demoNoticeOverlay || true == isDemoNoticeShowing || true == isHiding)
        {
            return;
        }

        if (null != EventSystem.current)
        {
            previousSelectedObject = EventSystem.current.currentSelectedGameObject;
        }

        isDemoNoticeShowing = true;
        isHiding = false;
        demoNoticeOverlay.SetActive(true);
        Sound.PlayUI(SoundID.DemoEnd);

        depthController?.RegisterView(this);

        // 배너 확장 연출 전 콘텐츠는 투명(Alpha 0) 상태로 대기 (레이아웃 크기는 정상 유지)
        SetContentAlpha(0f);

        // 텍스트 로컬라이징 적용 (JSON ID 14)
        if (null != localizationManager)
        {
            const int demoJsonId = 14;
            if (null != demoTitleText)
            {
                string _title = localizationManager.GetText(demoJsonId, 1);
                if (false == string.IsNullOrEmpty(_title))
                {
                    demoTitleText.text = _title;
                }
            }

            if (null != demoDescText)
            {
                // 본문은 스토어 이름을 직접 말합니다("Steam 찜하기"). 단어만 바꿔서 될 일이 아니라
                // (찜하기라는 개념이 스토어마다 없을 수 있어) 아예 다른 엔트리를 씁니다.
                // STOVE 본문에는 본편 상점 안내가 통째로 빠져 있습니다 - 상점 버튼도 없기 때문입니다.
                //   1 = 제목 / 2 = 본문(Steam) / 3 = 본문(STOVE)
                string _desc = localizationManager.GetText(demoJsonId, BuildInfo.IsStove ? DESC_ENTRY_STOVE : DESC_ENTRY_STEAM);

                if (true == BuildInfo.IsStove && true == string.IsNullOrEmpty(_desc))
                {
                    // 여기까지 왔다는 것은 번역문이 아직 없다는 뜻이다. 빈 팝업을 띄우느니
                    // Steam 문구라도 보여주되, 이 상태로 출시되지 않도록 크게 남긴다.
                    // (PlatformConsistencyGuard가 STOVE 데모 빌드에서 이 상태를 막는다)
                    Debug.LogError("[DemoNotice] STOVE 본문(DemoNoticeUI.json entry 3)이 없어 Steam 문구로 대체합니다. " +
                                   "이 빌드를 STOVE에 올리면 상점 버튼은 없는데 화면은 찜하기를 누르라고 말하게 됩니다.");
                    _desc = localizationManager.GetText(demoJsonId, DESC_ENTRY_STEAM);
                }

                if (false == string.IsNullOrEmpty(_desc))
                {
                    demoDescText.text = _desc;
                }
            }
        }

        // 상점 링크·아이콘을 스토어에 맞춘다.
        // 프리팹 값이라 디파인을 자동으로 따라오지 않으므로 런타임에 갈아끼운다.
        ApplyStoreBranding();
        if (null != discordBtn && false == string.IsNullOrEmpty(discordCommunityUrl))
        {
            discordBtn.SetUrl(discordCommunityUrl);
        }

        // DOTween 애니메이션: Title DimBG 연출 동일 적용
        if (null != demoNoticeTween && true == demoNoticeTween.IsActive())
        {
            demoNoticeTween.Kill();
            demoNoticeTween = null;
        }

        Sequence _seq = DOTween.Sequence();

        if (null != demoDimCanvasGroup)
        {
            demoDimCanvasGroup.alpha = 0f;
            demoDimCanvasGroup.blocksRaycasts = true;
            _seq.Join(demoDimCanvasGroup.DOFade(1f, dimFadeDuration).SetEase(dimFadeEase));
        }

        if (null != demoBandTransform)
        {
            demoBandTransform.localScale = new Vector3(0f, 1f, 1f);
            _seq.Join(demoBandTransform.DOScaleX(1f, panelScaleDuration).SetEase(titleBandEase, titleBandOvershoot));
        }

        _seq.OnComplete(HandleRevealAnimationComplete);

        demoNoticeTween = _seq;
    }

    private void HandleRevealAnimationComplete()
    {
        // 배너 확장이 완료된 시점에 콘텐츠 알파 활성화 및 텍스트 바운스 연출 실행
        SetContentAlpha(1f);

        if (null != demoTitleAnimator)
        {
            demoTitleAnimator.PlayRevealBounce();
        }
        if (null != demoDescAnimator)
        {
            demoDescAnimator.PlayRevealBounce();
        }

        if (null != inputManager && true == inputManager.IsGamepadMode)
        {
            FocusDemoButton(0, true);
        }
    }

    public void HideDemoNoticeOverlay()
    {
        if (null == demoNoticeOverlay || false == demoNoticeOverlay.activeSelf || false == isDemoNoticeShowing || true == isHiding)
        {
            return;
        }

        isHiding = true;

        Sound.PlayUI(SoundID.ResultUIClose);

        if (null != mainController)
        {
            mainController.HandleDemoNoticeClosing();
        }

        if (null != demoContentCanvasGroup)
        {
            demoContentCanvasGroup.blocksRaycasts = false;
        }

        if (null != demoDimCanvasGroup)
        {
            demoDimCanvasGroup.blocksRaycasts = true;
        }

        if (null != demoNoticeTween && true == demoNoticeTween.IsActive())
        {
            demoNoticeTween.Kill();
            demoNoticeTween = null;
        }

        Sequence _seq = DOTween.Sequence();

        if (null != demoContentCanvasGroup)
        {
            _seq.Join(demoContentCanvasGroup.DOFade(0f, contentFadeOutDuration).SetEase(Ease.OutQuad));
        }

        if (null != demoBandTransform)
        {
            _seq.Join(demoBandTransform.DOScaleX(0f, bandCloseDuration).SetEase(bandCloseEase));
        }

        if (null != demoDimCanvasGroup)
        {
            _seq.Join(demoDimCanvasGroup.DOFade(0f, dimCloseDuration).SetEase(Ease.Linear));
        }

        _seq.OnComplete(HandleHideAnimationComplete);

        demoNoticeTween = _seq;
    }

    private void HandleHideAnimationComplete()
    {
        if (null != steamWishlistBtn)
        {
            steamWishlistBtn.UnfocusButtonImmediate();
        }
        if (null != discordBtn)
        {
            discordBtn.UnfocusButtonImmediate();
        }

        if (null != demoDimCanvasGroup)
        {
            demoDimCanvasGroup.blocksRaycasts = false;
        }
        if (null != demoNoticeOverlay)
        {
            demoNoticeOverlay.SetActive(false);
        }
        isDemoNoticeShowing = false;
        isHiding = false;
        focusedButtonIndex = 0;

        depthController?.UnregisterView(this);

        if (null != previousSelectedObject && true == previousSelectedObject.activeInHierarchy && null != EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(previousSelectedObject);
            previousSelectedObject = null;
        }

        // 메인 컨트롤러에 닫힘 알림 전달
        if (null != mainController)
        {
            mainController.HandleDemoNoticeClosed();
        }
    }

    private void SetContentAlpha(float _alpha)
    {
        if (null != demoContentCanvasGroup)
        {
            demoContentCanvasGroup.alpha = _alpha;
            demoContentCanvasGroup.blocksRaycasts = 0.99f <= _alpha;
        }
    }

    public void ResetNotice()
    {
        KillTweens();
        SetContentAlpha(0f);
        focusedButtonIndex = 0;

        if (null != steamWishlistBtn)
        {
            steamWishlistBtn.UnfocusButtonImmediate();
        }
        if (null != discordBtn)
        {
            discordBtn.UnfocusButtonImmediate();
        }

        if (null != demoNoticeOverlay)
        {
            demoNoticeOverlay.SetActive(false);
        }
        if (null != demoDimCanvasGroup)
        {
            demoDimCanvasGroup.blocksRaycasts = false;
        }
        isDemoNoticeShowing = false;
        isHiding = false;
        previousSelectedObject = null;

        // 네비게이션 팝업이 애니메이션 없이(다른 경로로) 강제로 닫힐 때도 호출되므로, 뎁스 스택에
        // 좀비 항목으로 남지 않도록 여기서도 해제한다.
        depthController?.UnregisterView(this);
    }

    public void KillTweens()
    {
        if (null != demoNoticeTween && true == demoNoticeTween.IsActive())
        {
            demoNoticeTween.Kill();
            demoNoticeTween = null;
        }
    }

    private void OnDestroy()
    {
        if (null != inputManager && null != inputManager.inputReader && null != cachedOnInputDeviceChanged)
        {
            inputManager.inputReader.InputDeviceChangedEvent -= cachedOnInputDeviceChanged;
        }

        depthController?.UnregisterView(this);
        KillTweens();
    }
}
