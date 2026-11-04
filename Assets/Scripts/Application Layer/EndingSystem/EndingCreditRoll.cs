using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 엔딩 크레딧. 검은 패널이 오른쪽에서 부드럽게 들어오고, 패널 안에서 글자 크레딧이 아래에서 위로 흐른 뒤,
/// 다 흐르면 패널이 다시 오른쪽으로 빠진다.
///
/// 건너뛸 수 없다(ESC 무시). 아무 키나 누르고 있는 동안만 빨라진다 - 판정은 메인메뉴 크레딧(UI_Credit)과 같다.
///
/// 패널은 CroppedAreaFitter가 맞춘 "카메라가 실제로 그리는 영역" 안에서 오른쪽 일정 비율을 차지한다. 비율로 잡으므로
/// 화면비·배율이 달라도(16:9, 16:10, 4:3, 울트라와이드 필러박스) 차가 달리는 왼쪽 영역과 크레딧 영역의 비례가 같다.
/// 카메라는 EndingDirector가 같은 비율(LeftAreaCenter)로 맞춰 차를 왼쪽 영역 가운데에 둔다.
/// </summary>
public class EndingCreditRoll : MonoBehaviour
{
    [SerializeField] private RectTransform panel;
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform contentRoot;
    [Tooltip("패널이 차지하는 오른쪽 비율(카메라 영역 기준).")]
    [SerializeField, Range(0.3f, 0.6f)] private float panelWidthRatio = 0.44f;
    [SerializeField] private float slideDuration = 1.1f;
    [SerializeField] private Ease slideInEase = Ease.OutCubic;
    [SerializeField] private Ease slideOutEase = Ease.InOutCubic;
    [Tooltip("크레딧이 흐르는 속도(캔버스 단위/초).")]
    [SerializeField] private float scrollSpeed = 32f;
    [SerializeField] private float speedMultiplier = 3f;

    [Header("엔딩 문구 (소풍 장면)")]
    [SerializeField] private CanvasGroup messageGroup;
    [SerializeField] private TMPro.TMP_Text thanksText;
    [SerializeField] private TMPro.TMP_Text unlockText;
    [Tooltip("숲 위에서도 읽히도록 글자 뒤에 까는 1픽셀 검은 그림자(본문 글자의 부모).")]
    [SerializeField] private TMPro.TMP_Text thanksShadowText;
    [SerializeField] private TMPro.TMP_Text unlockShadowText;
    [Tooltip("그림자에 더해 글자 둘레 8방향에 1픽셀 테두리를 두른다(나무·풀 위에서도 글자 모양이 보이게).")]
    [SerializeField] private bool bOutline = true;

    public float PanelWidthRatio => panelWidthRatio;
    public float SlideDuration => slideDuration;

    private InputManager inputManager;
    private Tween panelTween;
    private bool bScrolling;
    private float scrollY;
    private float scrollEndY;
    private Action scrollComplete;
    private readonly Dictionary<TMPro.TMP_Text, List<TMPro.TMP_Text>> outlines = new Dictionary<TMPro.TMP_Text, List<TMPro.TMP_Text>>();

    // 본문 글자 기준 테두리 방향. 그림자(부모)가 이미 (+1, -1)에 있으므로 나머지 7방향만 만든다.
    private static readonly Vector2[] OutlineOffsets =
    {
        new Vector2(-1f, 1f), new Vector2(0f, 1f), new Vector2(1f, 1f),
        new Vector2(-1f, 0f), new Vector2(1f, 0f),
        new Vector2(-1f, -1f), new Vector2(0f, -1f),
    };

    private void Awake()
    {
        if (null != panel)
        {
            panel.anchorMin = new Vector2(1f - panelWidthRatio, 0f);
            panel.anchorMax = new Vector2(1f, 1f);
            panel.gameObject.SetActive(false);
        }

        if (null != messageGroup)
        {
            messageGroup.alpha = 0f;
            messageGroup.gameObject.SetActive(false);
        }

        if (bOutline)
        {
            BuildOutline(thanksText, thanksShadowText);
            BuildOutline(unlockText, unlockShadowText);
        }
    }

    // 본문 글자를 복제해 그림자 색으로 칠하고 1픽셀씩 비켜 본문 뒤에 깐다.
    private void BuildOutline(TMPro.TMP_Text _text, TMPro.TMP_Text _shadow)
    {
        if (null == _text || null == _shadow || _text.transform.parent != _shadow.transform) return;

        RectTransform source = _text.rectTransform;
        List<TMPro.TMP_Text> list = new List<TMPro.TMP_Text>(OutlineOffsets.Length);
        for (int i = 0; i < OutlineOffsets.Length; i++)
        {
            TMPro.TMP_Text clone = Instantiate(_text, _shadow.transform);
            clone.name = "Outline";
            clone.color = _shadow.color;
            clone.raycastTarget = false;
            clone.rectTransform.anchoredPosition = source.anchoredPosition + OutlineOffsets[i];
            clone.transform.SetSiblingIndex(0);
            list.Add(clone);
        }
        outlines[_text] = list;
    }

    private void Start()
    {
        // 입력 관리자는 BootStrap과 함께 씬을 넘어 살아 있다(에디터에서 엔딩 씬을 바로 켜면 없을 수 있다).
        inputManager = FindAnyObjectByType<InputManager>();
    }

    /// <summary>패널이 오른쪽 밖에서 들어온다.</summary>
    public Tween SlideIn()
    {
        if (null == panel) return null;

        panel.gameObject.SetActive(true);
        ResetContent();
        KillPanelTween();
        panel.anchoredPosition = new Vector2(GetPanelWidth(), 0f);
        panelTween = panel.DOAnchorPosX(0f, slideDuration).SetEase(slideInEase);
        return panelTween;
    }

    /// <summary>패널이 오른쪽 밖으로 빠진다.</summary>
    public Tween SlideOut()
    {
        if (null == panel) return null;

        KillPanelTween();
        panelTween = panel.DOAnchorPosX(GetPanelWidth(), slideDuration).SetEase(slideOutEase)
            .OnComplete(() => panel.gameObject.SetActive(false));
        return panelTween;
    }

    /// <summary>크레딧을 아래에서 위로 흘린다. 마지막 줄이 패널 위로 빠지면 _onComplete.</summary>
    public void PlayScroll(Action _onComplete)
    {
        scrollComplete = _onComplete;

        if (null == contentRoot || null == viewport)
        {
            bScrolling = false;
            _onComplete?.Invoke();
            return;
        }

        ResetContent();
        bScrolling = true;
    }

    /// <summary>소풍 장면 문구. 감사 문구는 항상, 로그라이크 해제 문구는 최초 해제 때만 보인다.</summary>
    public Tween ShowMessage(string _thanks, string _unlock, float _fadeSeconds)
    {
        if (null == messageGroup) return null;

        bool bUnlock = false == string.IsNullOrEmpty(_unlock);
        SetMessageText(thanksText, thanksShadowText, _thanks, true);
        SetMessageText(unlockText, unlockShadowText, _unlock, bUnlock);

        messageGroup.gameObject.SetActive(true);
        messageGroup.DOKill();
        messageGroup.alpha = 0f;
        return messageGroup.DOFade(1f, _fadeSeconds).SetEase(Ease.InOutSine);
    }

    private void SetMessageText(TMPro.TMP_Text _text, TMPro.TMP_Text _shadow, string _value, bool _bVisible)
    {
        string value = _value ?? string.Empty;
        if (null != _text) _text.text = value;
        if (null != _text && outlines.TryGetValue(_text, out List<TMPro.TMP_Text> list))
        {
            for (int i = 0; i < list.Count; i++) list[i].text = value;
        }
        if (null != _shadow)
        {
            _shadow.text = value;
            _shadow.gameObject.SetActive(_bVisible);
        }
        else if (null != _text)
        {
            _text.gameObject.SetActive(_bVisible);
        }
    }

    public Tween HideMessage(float _fadeSeconds)
    {
        if (null == messageGroup) return null;

        messageGroup.DOKill();
        return messageGroup.DOFade(0f, _fadeSeconds).SetEase(Ease.InOutSine);
    }

    private void Update()
    {
        if (false == bScrolling) return;

        float multiplier = IsSpeedUpHeld() ? speedMultiplier : 1f;
        scrollY += scrollSpeed * multiplier * Time.deltaTime;

        if (scrollY >= scrollEndY)
        {
            scrollY = scrollEndY;
            bScrolling = false;
            ApplyScroll();

            Action complete = scrollComplete;
            scrollComplete = null;
            complete?.Invoke();
            return;
        }

        ApplyScroll();
    }

    // 메인메뉴 크레딧(UI_Credit)과 같은 판정: 취소 키를 뺀 모든 "눌림". 마우스 이동·휠은 치지 않는다.
    private bool IsSpeedUpHeld()
    {
        return (null != inputManager && inputManager.AnyButtonHeldThisFrame && false == inputManager.WasGamepadUICancelPressedThisFrame)
            || (null != Keyboard.current && Keyboard.current.anyKey.isPressed && false == Keyboard.current.escapeKey.isPressed)
            || (null != Mouse.current && (Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed || Mouse.current.middleButton.isPressed));
    }

    // 내용의 위쪽 끝을 뷰포트 아래쪽 끝에 맞춰 두고(scrollY = 0), 내용의 아래쪽 끝이 뷰포트 위로 빠질 때까지 올린다.
    private void ResetContent()
    {
        if (null == contentRoot || null == viewport) return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = new Vector2(1f, 1f);
        contentRoot.pivot = new Vector2(0.5f, 1f);

        scrollY = 0f;
        scrollEndY = viewport.rect.height + contentRoot.rect.height;
        ApplyScroll();
    }

    private void ApplyScroll()
    {
        if (null == contentRoot || null == viewport) return;

        contentRoot.anchoredPosition = new Vector2(0f, -viewport.rect.height + scrollY);
    }

    private float GetPanelWidth()
    {
        float width = null != panel ? panel.rect.width : 0f;
        return width > 0f ? width : 400f;
    }

    private void KillPanelTween()
    {
        if (null != panelTween && panelTween.IsActive()) panelTween.Kill();
        panelTween = null;
    }

    private void OnDestroy()
    {
        KillPanelTween();
        if (null != messageGroup) messageGroup.DOKill();
    }
}
