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

    private bool isDemoNoticeShowing = false;
    private bool isHiding = false;
    private Tween demoNoticeTween;

    public bool IsDemoNoticeShowing => isDemoNoticeShowing;
    public bool IsHiding => isHiding;
    public bool IsDemoNoticeActive => (true == isDemoNoticeShowing || true == isHiding);

    // IUIDepthCloseable 구현: ESC로 뎁스 스택에서 닫힐 때 호출됩니다.
    public bool IsActive => isDemoNoticeShowing;
    public void Hide() => HideDemoNoticeOverlay();

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
            Navigation _discordNav = new Navigation();
            _discordNav.mode = Navigation.Mode.Explicit;
            _discordNav.selectOnRight = steamWishlistBtn;
            _discordNav.selectOnLeft = steamWishlistBtn;
            discordBtn.navigation = _discordNav;

            Navigation _steamNav = new Navigation();
            _steamNav.mode = Navigation.Mode.Explicit;
            _steamNav.selectOnLeft = discordBtn;
            _steamNav.selectOnRight = discordBtn;
            steamWishlistBtn.navigation = _steamNav;
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
            if (null != discordBtn && null != EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(discordBtn.gameObject);
            }
        }
        else
        {
            if (null != cursorBoxUI)
            {
                cursorBoxUI.HideImmediately();
            }
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
                string _desc = localizationManager.GetText(demoJsonId, 2);
                if (false == string.IsNullOrEmpty(_desc))
                {
                    demoDescText.text = _desc;
                }
            }
        }

        // URL 동적 주입
        if (null != steamWishlistBtn && false == string.IsNullOrEmpty(steamWishlistUrl))
        {
            steamWishlistBtn.SetUrl(steamWishlistUrl);
        }
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
            if (null != discordBtn && null != EventSystem.current)
            {
                EventSystem.current.SetSelectedGameObject(discordBtn.gameObject);
            }
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
            steamWishlistBtn.HideCursor();
        }
        if (null != discordBtn)
        {
            discordBtn.HideCursor();
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

        if (null != steamWishlistBtn)
        {
            steamWishlistBtn.HideCursor();
        }
        if (null != discordBtn)
        {
            discordBtn.HideCursor();
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
