using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 단일 슬롯 형태의 포션 퀵 인터페이스(Active Potion Slot)를 제공하는 HUD 컴포넌트입니다.
/// </summary>
public class HUD_Loot : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup slotCanvasGroup; // 사용 불가 상태 등의 페이드 처리용
    [SerializeField] private RectTransform motionTarget;  // 스케일 모션 연출이 적용될 대상 객체
    [SerializeField] private Image potionIcon;          // 포션 아이콘 (필요 시 활용)
    [SerializeField] private Image flashOverlay;        // 반짝임 연출용 하얀색 오버레이 (선택사항)
    [SerializeField] private UI_KeyboardImage keyBindImage; // 키 바인딩 표기용
    
    [Header("Motion Settings")]
    [SerializeField] private float motionDuration = 0.6f;
    [SerializeField] private Vector3 maxScale = new Vector3(1.4f, 1.4f, 1f);
    
    private Tween transitionTween;
    private Sequence motionSequence;
    private bool hasAcquired = false;

    public void Initialize(InputManager _inputManager = null)
    {
        if (null != keyBindImage && null != _inputManager)
        {
            keyBindImage.Initialize(_inputManager);
        }
        if (null == slotCanvasGroup)
        {
            slotCanvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (null == slotCanvasGroup)
            {
                slotCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (null != slotCanvasGroup)
        {
            slotCanvasGroup.alpha = 0f;
        }

        if (null != flashOverlay)
        {
            Color _clearColor = flashOverlay.color;
            _clearColor.a = 0f;
            flashOverlay.color = _clearColor;
        }
        
        hasAcquired = false;
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 포션 충전(획득) 신호가 들어왔을 때 호출되어 충전 피드백 모션을 재생합니다.
    /// 최초 획득 시에는 비활성 상태에서 활성화되며 등장 연출을 재생합니다.
    /// 인자로 들어오는 _acquiredType은 무시하고 고정된 포션 아이콘에 대해 작동합니다.
    /// </summary>
    public void AcquireLoot(LootType _acquiredType, bool _playAnimation = true)
    {
        if (false == _playAnimation || null == motionTarget)
        {
            return;
        }

        if (false == hasAcquired)
        {
            hasAcquired = true;
            gameObject.SetActive(true);
            PlayAppearanceMotion();
        }
        else
        {
            PlayFeedbackMotion(Vector3.one * 1.2f, motionDuration * 0.8f);
        }
    }

    private void PlayAppearanceMotion()
    {
        if (null != motionSequence && true == motionSequence.IsActive())
        {
            motionSequence.Kill();
        }

        motionTarget.localScale = Vector3.zero;
        if (null != slotCanvasGroup)
        {
            slotCanvasGroup.alpha = 0f;
            slotCanvasGroup.DOFade(1f, motionDuration).SetEase(Ease.OutQuad);
        }

        motionSequence = DOTween.Sequence();
        motionSequence.Insert(0f, motionTarget.DOScale(maxScale, motionDuration * 0.6f).SetEase(Ease.OutBack));
        motionSequence.Insert(motionDuration * 0.6f, motionTarget.DOScale(Vector3.one, motionDuration * 0.4f).SetEase(Ease.OutQuad));
        motionSequence.Play();
    }

    /// <summary>
    /// 특수 키를 눌러 포션을 일괄 소비할 때 강렬한 피드백 모션을 재생합니다.
    /// </summary>
    public void PlayUsePotionMotion()
    {
        if (null == motionTarget)
        {
            return;
        }
        
        PlayFeedbackMotion(maxScale, motionDuration);
    }

    /// <summary>
    /// 현재 포션의 사용 가능 여부에 따라 슬롯의 투명도를 조절합니다.
    /// </summary>
    public void SetSlotActive(bool _isActive)
    {
        if (null != slotCanvasGroup)
        {
            slotCanvasGroup.DOKill();
            slotCanvasGroup.DOFade(true == _isActive ? 1f : 0.4f, 0.3f);
        }
    }

    private void PlayFeedbackMotion(Vector3 _targetScale, float _duration)
    {
        if (null != motionSequence && true == motionSequence.IsActive())
        {
            motionSequence.Kill();
        }

        motionTarget.localScale = Vector3.one;
        if (null != flashOverlay)
        {
            Color _clearColor = flashOverlay.color;
            _clearColor.a = 0f;
            flashOverlay.color = _clearColor; 
        }

        motionSequence = DOTween.Sequence();
        
        // 쫀득한 뽀잉 스케일 연출
        motionSequence.Insert(0f, motionTarget.DOScale(_targetScale, _duration * 0.3f).SetEase(Ease.OutQuad));
        motionSequence.Insert(_duration * 0.3f, motionTarget.DOScale(Vector3.one, _duration * 0.7f).SetEase(Ease.OutElastic));
        
        // 오버레이가 설정되어 있다면 반짝임 연출 추가
        if (null != flashOverlay)
        {
            motionSequence.Insert(0f, flashOverlay.DOFade(1f, _duration * 0.2f).SetEase(Ease.OutFlash));
            motionSequence.Insert(_duration * 0.2f, flashOverlay.DOFade(0f, _duration * 0.8f).SetEase(Ease.InQuad));
        }
        
        motionSequence.Play();
    }

    /// <summary>
    /// UIView_HUD의 HUDGoDown에 대응하는 함수입니다.
    /// </summary>
    public void OnHUDGoDown()
    {
        if (null == slotCanvasGroup)
        {
            return;
        }
        
        if (null != transitionTween && true == transitionTween.IsActive())
        {
            transitionTween.Kill();
        }
        
        transitionTween = slotCanvasGroup.DOFade(0f, 0.3f);
    }

    /// <summary>
    /// UIView_HUD의 HUDGoUp에 대응하는 함수입니다.
    /// </summary>
    public void OnHUDGoUp()
    {
        if (null == slotCanvasGroup)
        {
            return;
        }
        
        if (null != transitionTween && true == transitionTween.IsActive())
        {
            transitionTween.Kill();
        }
        
        transitionTween = slotCanvasGroup.DOFade(1f, 0.3f);
    }

    #region Editor Test Logic
    [NaughtyAttributes.Button("Test Fill Motion")]
    private void TestFillMotion()
    {
        AcquireLoot(LootType.SporePotion, true);
    }

    [NaughtyAttributes.Button("Test Use Motion")]
    private void TestUseMotion()
    {
        PlayUsePotionMotion();
    }
    #endregion
}
