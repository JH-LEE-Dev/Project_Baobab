using System;
using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 범용 경고/확인 팝업 UI 스크립트입니다.
/// 다양한 시스템 텍스트 출력과 콜백 실행을 담당하며 DOTween을 사용한 슬라이드 및 페이드 연출이 포함되어 있습니다.
/// </summary>
public class UI_WarningPopup : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text messageText;
    [SerializeField] private UI_WarningPopupButton confirmButton;
    [SerializeField] private UI_WarningPopupButton cancelButton;
    
    [Header("Animation Settings")]
    [SerializeField] private CanvasGroup popupCanvasGroup;
    [SerializeField] private RectTransform popupWindowRoot;
    [SerializeField] private float animationDuration = 0.25f;
    [SerializeField] private float slideOffset = 50f;
    [SerializeField] private Ease openEase = Ease.OutCubic;
    [SerializeField] private Ease closeEase = Ease.InCubic;

    [Header("Dim Background Settings")]
    [SerializeField] private CanvasGroup dimCanvasGroup;
    [SerializeField, Range(0f, 1f)] private float dimTargetAlpha = 0.8f;
    [SerializeField] private float dimAnimationDuration = 0.25f;

    private Action onConfirmAction;
    private Action onCancelAction;
    private SoundID openSoundId = SoundID.None;
    private SoundID closeSoundId = SoundID.None;
    private SoundID hoverSoundId = SoundID.None;
    private bool hasPlayedCloseSound;
    
    private Sequence productionSequence;
    private Vector2 originalRootAnchoredPosition;
    
    private void Awake()
    {
        if (null != popupWindowRoot)
        {
            originalRootAnchoredPosition = popupWindowRoot.anchoredPosition;
        }
    }

    private void OnDestroy()
    {
        KillSequence();
        onConfirmAction = null;
        onCancelAction = null;
    }

    /// <summary>
    /// 경고 팝업을 띄우고 콜백을 등록합니다.
    /// </summary>
    public void ShowWarning(
        string _message,
        Action _onConfirm,
        Action _onCancel = null,
        SoundID _openSoundId = SoundID.None,
        SoundID _closeSoundId = SoundID.None,
        SoundID _hoverSoundId = SoundID.None)
    {
        if (null != messageText) messageText.text = _message;

        onConfirmAction = _onConfirm;
        onCancelAction = _onCancel;
        openSoundId = _openSoundId;
        closeSoundId = _closeSoundId;
        hoverSoundId = _hoverSoundId;
        hasPlayedCloseSound = false;

        if (null != confirmButton)
            confirmButton.Initialize(OnConfirmButtonClicked, PlayHoverSound);
            
        if (null != cancelButton)
        {
            cancelButton.gameObject.SetActive(null != _onCancel);
            cancelButton.Initialize(OnCancelButtonClicked, PlayHoverSound);
        }

        PlayConfiguredSound(openSoundId);
        PlayOpenProduction();
    }

    private void OnConfirmButtonClicked()
    {
        if (null != onConfirmAction)
            onConfirmAction();
            
        PlayCloseProduction();
    }

    private void OnCancelButtonClicked()
    {
        if (null != onCancelAction)
            onCancelAction();
            
        PlayCloseProduction();
    }

    private void PlayOpenProduction()
    {
        gameObject.SetActive(true);
        KillSequence();

        if (null == popupCanvasGroup || null == popupWindowRoot) return;

        // 초기 상태 세팅 (투명, 아래로 내려간 상태)
        popupCanvasGroup.alpha = 0f;
        popupWindowRoot.anchoredPosition = originalRootAnchoredPosition + new Vector2(0f, -slideOffset);

        if (null != dimCanvasGroup)
        {
            dimCanvasGroup.alpha = 0f;
        }

        productionSequence = DOTween.Sequence().SetUpdate(true);
        
        productionSequence.Join(popupCanvasGroup.DOFade(1f, animationDuration).SetEase(openEase));
        productionSequence.Join(popupWindowRoot.DOAnchorPosY(originalRootAnchoredPosition.y, animationDuration).SetEase(openEase));

        if (null != dimCanvasGroup)
        {
            productionSequence.Join(dimCanvasGroup.DOFade(dimTargetAlpha, dimAnimationDuration));
        }
    }

    private void PlayCloseProduction()
    {
        if (false == hasPlayedCloseSound)
        {
            hasPlayedCloseSound = true;
            PlayConfiguredSound(closeSoundId);
        }

        KillSequence();

        if (null == popupCanvasGroup || null == popupWindowRoot)
        {
            gameObject.SetActive(false);
            return;
        }

        productionSequence = DOTween.Sequence().SetUpdate(true);
        
        float targetY = originalRootAnchoredPosition.y - slideOffset;
        
        productionSequence.Join(popupCanvasGroup.DOFade(0f, animationDuration).SetEase(closeEase));
        productionSequence.Join(popupWindowRoot.DOAnchorPosY(targetY, animationDuration).SetEase(closeEase));
        
        if (null != dimCanvasGroup)
        {
            productionSequence.Join(dimCanvasGroup.DOFade(0f, dimAnimationDuration));
        }

        productionSequence.OnComplete(OnCloseProductionComplete);
    }

    private void OnCloseProductionComplete()
    {
        gameObject.SetActive(false);
    }

    private void KillSequence()
    {
        if (null != productionSequence)
        {
            productionSequence.Kill();
            productionSequence = null;
        }
    }

    private void PlayHoverSound()
    {
        PlayConfiguredSound(hoverSoundId);
    }

    private static void PlayConfiguredSound(SoundID _soundId)
    {
        if (SoundID.None != _soundId)
        {
            Sound.PlayUI(_soundId);
        }
    }
}
