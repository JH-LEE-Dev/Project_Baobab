using UnityEngine;
using DG.Tweening;
using System;
using UnityEngine.UI;

/// <summary>
/// 메인 메뉴 화면에서 로고 전용 애니메이션을 독립적으로 담당하는 스크립트입니다.
/// </summary>
public class UI_LogoAnim : MonoBehaviour
{
    [Header("Logo Target")]
    [SerializeField, Tooltip("애니메이션을 적용할 로고의 RectTransform")] 
    private RectTransform logoTransform;

    [Header("Fade Settings")]
    [SerializeField, Tooltip("로고 페이드아웃을 위한 CanvasGroup (없으면 자동 추가됨)")]
    private CanvasGroup canvasGroup;

    [Header("Animation Settings")]
    [SerializeField] private float moveDistanceY = 200f; // 위로 이동할 거리
    [SerializeField] private float moveDuration = 1f;
    [SerializeField] private Ease moveEase = Ease.OutBack; // 찰진 튕김 효과 (뽀잉)
    [SerializeField] private float fadeInDuration = 0.8f; // 로고 페이드인 기본 시간

    [Header("Demo Badge")]
    [SerializeField, Tooltip("데모 버전 표시 뱃지 오브젝트 (Title > Demo)")]
    private GameObject demoBadge;

    [Header("Roguelike Subtitle")]
    [SerializeField, Tooltip("로그라이크 하위 메뉴에서만 보이는 부제 (Title > Roguelike)")]
    private CanvasGroup roguelikeSubtitle;
    [SerializeField, Tooltip("부제 글자 9장 (Title > Roguelike > Letter0~8). 등장 때 0.05초 간격으로 하나씩 떨어진다.")]
    private Image[] roguelikeSubtitleLetters;
    [SerializeField] private float subtitleLetterDropDistance = 22f;
    [SerializeField] private float subtitleLetterStagger = 0.05f;
    [SerializeField] private float subtitleLetterDuration = 0.42f;
    [SerializeField] private float subtitleLetterOvershoot = 2.4f;
    [SerializeField, Tooltip("글자마다 낙하 시간 중 알파 0→1에 쓰는 비율")] private float subtitleLetterFadeRatio = 0.25f;

    private Vector2 initialPosition;

    private Action currentRevealComplete;
    private TweenCallback onRevealCompleteCallback;

    private Action currentFadeInComplete;
    private TweenCallback onFadeInCompleteCallback;

    private Action currentFadeOutComplete;
    private TweenCallback onFadeOutCompleteCallback;

    private TweenCallback onRoguelikeSubtitleHiddenCallback;

    // 부제 글자 9장의 제자리(Letter마다 anchoredPosition이 달라서 기억해 둔다)
    private Vector2[] roguelikeSubtitleLetterBasePositions;

    private bool isInitialPositionSet = false;

    public void Initialize()
    {
        if (null == onRevealCompleteCallback) onRevealCompleteCallback = OnRevealComplete;
        if (null == onFadeInCompleteCallback) onFadeInCompleteCallback = OnFadeInComplete;
        if (null == onFadeOutCompleteCallback) onFadeOutCompleteCallback = OnFadeOutComplete;
        if (null == onRoguelikeSubtitleHiddenCallback) onRoguelikeSubtitleHiddenCallback = OnRoguelikeSubtitleHidden;
        CaptureRoguelikeSubtitleLetterBases();

        if (null != logoTransform && false == isInitialPositionSet)
        {
            initialPosition = logoTransform.anchoredPosition;
            isInitialPositionSet = true;
        }

        if (null == demoBadge && null != logoTransform)
        {
            Transform _demoTransform = logoTransform.Find("Demo");
            if (null != _demoTransform)
            {
                demoBadge = _demoTransform.gameObject;
            }
        }

        if (null == canvasGroup)
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (null == canvasGroup)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }
    }

    /// <summary>
    /// 데모 버전 표시 뱃지(Title > Demo)의 활성화 상태를 설정합니다.
    /// 정식 버전 빌드 시 비활성화하여 데모 표기를 숨깁니다.
    /// </summary>
    public void SetDemoBadgeActive(bool _isActive)
    {
        if (null == demoBadge && null != logoTransform)
        {
            Transform _demoTransform = logoTransform.Find("Demo");
            if (null != _demoTransform)
            {
                demoBadge = _demoTransform.gameObject;
            }
        }

        if (null != demoBadge)
        {
            demoBadge.SetActive(_isActive);
        }
    }

    /// <summary>
    /// 로그라이크 부제(Title > Roguelike)를 보이거나 숨깁니다.
    /// 글자는 프리팹에 놓인 제자리(Letter마다 다름)를 기준으로 움직인다.
    /// 보이기 + 애니메이션: 글자 9장이 위에서 0.05초 간격으로 하나씩 떨어집니다(_duration은 사용하지 않음).
    /// 숨기기 + 애니메이션: _duration초 동안 컨테이너 알파로 페이드합니다(unscaled).
    /// </summary>
    public void SetRoguelikeSubtitleVisible(bool _isVisible, bool _isAnimated, float _duration)
    {
        if (null == roguelikeSubtitle) return;

        CaptureRoguelikeSubtitleLetterBases();
        KillRoguelikeSubtitleTweens();

        if (true == _isVisible)
        {
            roguelikeSubtitle.alpha = 1f;
            roguelikeSubtitle.gameObject.SetActive(true);

            if (false == _isAnimated || 0f >= _duration)
            {
                ResetRoguelikeSubtitleLetters();
                return;
            }

            if (null == roguelikeSubtitleLetters) return;
            for (int i = 0; roguelikeSubtitleLetters.Length > i; i++)
            {
                Image _letter = roguelikeSubtitleLetters[i];
                if (null == _letter) continue;

                float _delay = i * subtitleLetterStagger;
                RectTransform _rect = _letter.rectTransform;
                Vector2 _basePosition = roguelikeSubtitleLetterBasePositions[i];
                _rect.anchoredPosition = _basePosition + new Vector2(0f, subtitleLetterDropDistance);
                Color _color = _letter.color;
                _color.a = 0f;
                _letter.color = _color;

                _rect.DOAnchorPos(_basePosition, subtitleLetterDuration, true)
                    .SetEase(Ease.OutBack, subtitleLetterOvershoot)
                    .SetDelay(_delay)
                    .SetUpdate(true);
                _letter.DOFade(1f, subtitleLetterDuration * subtitleLetterFadeRatio)
                    .SetDelay(_delay)
                    .SetUpdate(true);
            }
            return;
        }

        if (false == _isAnimated || 0f >= _duration)
        {
            roguelikeSubtitle.alpha = 0f;
            roguelikeSubtitle.gameObject.SetActive(false);
            ResetRoguelikeSubtitleLetters();
            return;
        }

        if (false == roguelikeSubtitle.gameObject.activeSelf) return;
        if (null == onRoguelikeSubtitleHiddenCallback) onRoguelikeSubtitleHiddenCallback = OnRoguelikeSubtitleHidden;
        roguelikeSubtitle.DOFade(0f, _duration).SetUpdate(true).OnComplete(onRoguelikeSubtitleHiddenCallback);
    }

    private void OnRoguelikeSubtitleHidden()
    {
        if (null == roguelikeSubtitle) return;

        roguelikeSubtitle.gameObject.SetActive(false);
        ResetRoguelikeSubtitleLetters();
    }

    /// <summary>
    /// 부제 컨테이너와 글자 9장의 진행 중인 트윈을 모두 중단합니다.
    /// </summary>
    private void KillRoguelikeSubtitleTweens()
    {
        if (null != roguelikeSubtitle) roguelikeSubtitle.DOKill();
        if (null == roguelikeSubtitleLetters) return;

        for (int i = 0; roguelikeSubtitleLetters.Length > i; i++)
        {
            Image _letter = roguelikeSubtitleLetters[i];
            if (null == _letter) continue;

            _letter.rectTransform.DOKill();
            _letter.DOKill();
        }
    }

    /// <summary>
    /// 부제 글자 9장의 제자리를 한 번만 기억합니다(배열이 없거나 글자 수가 달라졌을 때만 다시 만듦).
    /// 등장 · 되돌리기보다 먼저 불러야 하며, 트윈이 위치를 옮기기 전의 값이 저장됩니다.
    /// </summary>
    private void CaptureRoguelikeSubtitleLetterBases()
    {
        if (null == roguelikeSubtitleLetters) return;
        if (null != roguelikeSubtitleLetterBasePositions && roguelikeSubtitleLetters.Length == roguelikeSubtitleLetterBasePositions.Length) return;

        roguelikeSubtitleLetterBasePositions = new Vector2[roguelikeSubtitleLetters.Length];
        for (int i = 0; roguelikeSubtitleLetters.Length > i; i++)
        {
            Image _letter = roguelikeSubtitleLetters[i];
            if (null == _letter) continue;

            roguelikeSubtitleLetterBasePositions[i] = _letter.rectTransform.anchoredPosition;
        }
    }

    /// <summary>
    /// 부제 글자를 제자리(알파 1)로 되돌립니다.
    /// </summary>
    private void ResetRoguelikeSubtitleLetters()
    {
        if (null == roguelikeSubtitleLetters) return;

        CaptureRoguelikeSubtitleLetterBases();

        for (int i = 0; roguelikeSubtitleLetters.Length > i; i++)
        {
            Image _letter = roguelikeSubtitleLetters[i];
            if (null == _letter) continue;

            _letter.rectTransform.anchoredPosition = roguelikeSubtitleLetterBasePositions[i];
            Color _color = _letter.color;
            _color.a = 1f;
            _letter.color = _color;
        }
    }

    private void OnRevealComplete()
    {
        currentRevealComplete?.Invoke();
        currentRevealComplete = null;
    }

    /// <summary>
    /// 로고를 원래 시작 위치로 즉시 되돌립니다.
    /// </summary>
    public void ResetToInitialState()
    {
        if (null != logoTransform)
        {
            logoTransform.DOKill();
            if (true == isInitialPositionSet)
            {
                logoTransform.anchoredPosition = initialPosition;
            }
        }
    }

    /// <summary>
    /// 로고가 위로 튕겨 올라가는 모션을 실행합니다.
    /// </summary>
    /// <param name="onComplete">모션이 완전히 끝난 뒤 호출될 콜백</param>
    public void PlayRevealSequence(Action _onComplete = null)
    {
        currentRevealComplete = _onComplete;

        if (null != logoTransform)
        {
            logoTransform.DOKill();
            
            if (false == isInitialPositionSet)
            {
                initialPosition = logoTransform.anchoredPosition;
                isInitialPositionSet = true;
            }
            else
            {
                logoTransform.anchoredPosition = initialPosition;
            }

            // 목표 위치 설정
            Vector2 targetPos = initialPosition + new Vector2(0f, moveDistanceY);

            // 부드럽고 찰지게 이동 후 콜백 실행
            Sound.PlayUI(SoundID.MainMenuStart00);
            Sound.PlayUI(SoundID.MainMenuStart01);
            logoTransform.DOAnchorPos(targetPos, moveDuration)
                .SetEase(moveEase)
                .OnComplete(onRevealCompleteCallback);
        }
        else
        {
            // 로고가 없다면 즉시 콜백 실행
            _onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 로고의 알파 값을 즉시 설정합니다.
    /// </summary>
    public void SetAlpha(float _alpha)
    {
        if (null != canvasGroup)
        {
            canvasGroup.DOKill();
            canvasGroup.alpha = _alpha;
        }
    }

    private void OnFadeInComplete()
    {
        currentFadeInComplete?.Invoke();
        currentFadeInComplete = null;
    }

    /// <summary>
    /// 로고를 서서히 선명하게 만듭니다.
    /// </summary>
    public void PlayFadeIn(float _duration = -1f, Action _onComplete = null)
    {
        currentFadeInComplete = _onComplete;
        float _dur = (0f <= _duration) ? _duration : fadeInDuration;

        if (null != canvasGroup)
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(1f, _dur).OnComplete(onFadeInCompleteCallback);
        }
        else
        {
            _onComplete?.Invoke();
        }
    }

    /// <summary>
    /// 로고를 서서히 투명하게 만듭니다.
    /// </summary>
    private void OnFadeOutComplete()
    {
        currentFadeOutComplete?.Invoke();
        currentFadeOutComplete = null;
    }

    public void PlayFadeOut(float _duration, Action _onComplete = null)
    {
        currentFadeOutComplete = _onComplete;
        if (null != canvasGroup)
        {
            canvasGroup.DOKill();
            canvasGroup.DOFade(0f, _duration).OnComplete(onFadeOutCompleteCallback);
        }
        else
        {
            _onComplete?.Invoke();
        }
    }

    private void OnDestroy()
    {
        if (null != logoTransform)
        {
            logoTransform.DOKill();
        }
        if (null != canvasGroup)
        {
            canvasGroup.DOKill();
        }
        KillRoguelikeSubtitleTweens();
    }
}
