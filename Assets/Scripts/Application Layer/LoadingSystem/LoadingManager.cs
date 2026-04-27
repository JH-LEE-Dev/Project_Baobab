using UnityEngine;
using DG.Tweening;
using System;

public class LoadingManager : MonoBehaviour
{
    private static LoadingManager instance;
    public static LoadingManager Instance => instance;

    [SerializeField] private CanvasGroup loadingCanvasGroup;
    [SerializeField] private float fadeDuration = 0.5f;

    private void Awake()
    {
        if (instance != null)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        // Bootstrap이 이미 DontDestroyOnLoad라면 자식 객체일 경우 자동으로 유지되지만, 
        // 독립적인 싱글턴 보장을 위해 추가합니다.
        if (transform.parent == null)
            DontDestroyOnLoad(gameObject);
            
        Initialize();
    }

    public void Initialize()
    {
        if (loadingCanvasGroup != null)
        {
            loadingCanvasGroup.alpha = 0f;
            loadingCanvasGroup.gameObject.SetActive(false);
        }
    }

    public void Show(Action onComplete = null)
    {
        if (loadingCanvasGroup == null)
        {
            if (onComplete != null) onComplete();
            return;
        }

        loadingCanvasGroup.gameObject.SetActive(true);
        loadingCanvasGroup.DOKill();
        
        Tweener tween = loadingCanvasGroup.DOFade(1f, fadeDuration);
        if (onComplete != null)
        {
            tween.OnComplete(new TweenCallback(onComplete));
        }
    }

    public void Hide(Action onComplete = null)
    {
        if (loadingCanvasGroup == null)
        {
            if (onComplete != null) onComplete();
            return;
        }

        loadingCanvasGroup.DOKill();
        Tweener tween = loadingCanvasGroup.DOFade(0f, fadeDuration);
        
        if (onComplete != null)
            tween.OnComplete(new TweenCallback(onComplete));
            
        tween.OnComplete(DisableCanvasGroup);
    }

    private void DisableCanvasGroup()
    {
        if (loadingCanvasGroup != null)
            loadingCanvasGroup.gameObject.SetActive(false);
    }

    // 유저 직관에 맞춘 에일리어스
    public void FadeOut(Action onComplete = null) => Show(onComplete); // 화면 가리기 (로딩 시작)
    public void FadeIn(Action onComplete = null) => Hide(onComplete);  // 화면 걷어내기 (로딩 종료)
}
