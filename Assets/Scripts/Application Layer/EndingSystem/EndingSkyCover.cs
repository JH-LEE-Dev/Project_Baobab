using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 엔딩 출발 하늘 연출(구름이 화면을 덮었다가 걷힘)과 엔딩 내내 쓰는 검은 페이드.
/// 마을에서 덮인 채로 씬이 바뀌어도 남아(DontDestroyOnLoad) 엔딩 씬에서 걷히고, 엔딩이 끝나 메인메뉴로 나갈 때
/// 다시 덮은 뒤 씬이 바뀌는 순간 사라진다(EndingDirector.ReturnToMainMenu).
///
/// 그림과 배치는 본편 SkyProduction(UIView_SkyProduction)과 같고, 출발·도착 시간은 하늘 연출이 두 번 빨라지기 전
/// 초기 버전(cf38c73f: 이동 1.8초, 구름 흔들림 4초)이다. 엔딩 뒤 메인메뉴 복귀는 본편 마을 → 메인메뉴와 같이
/// 메인메뉴 모드(SetMainMenuMode)로 지금 본편 시간에 맞춰 덮는다(PlayCover에 시간을 넘긴다). 본편 SkyProduction은 건드리지 않는다.
/// </summary>
public class EndingSkyCover : MonoBehaviour
{
    [SerializeField] private RectTransform cloudImage;
    [SerializeField] private RectTransform skyImage;
    [SerializeField] private RectTransform skyImage2;
    [SerializeField] private RectTransform cloudTargetTransform;
    [SerializeField] private RectTransform skyTargetTransform;
    [SerializeField] private Image blackImage;
    [SerializeField] private float moveDuration = 1.8f;
    [SerializeField] private Ease moveEase = Ease.InOutCubic;
    [SerializeField] private float floatingAmplitude = 8f;
    [SerializeField] private float floatingDuration = 4f;
    [Tooltip("메인메뉴 모드 구름 그림(본편 SkyProduction의 loadingCloudSprite_MainMenu).")]
    [SerializeField] private Sprite loadingCloudSprite_MainMenu;
    [Tooltip("평소 구름 그림(본편 SkyProduction의 loadingCloudSprite).")]
    [SerializeField] private Sprite loadingCloudSprite;

    public static EndingSkyCover Current { get; private set; }

    public float MoveDuration => moveDuration;

    private Vector2 cloudStartPos;
    private Vector2 skyStartPos;
    private Vector2 skyStartPos2;
    private Sequence moveSequence;
    private Tween skyTween1;
    private Tween skyTween2;
    private Tween cloudTween;
    private Tween blackTween;

    public static EndingSkyCover Create(EndingSkyCover _prefab)
    {
        if (null != Current) return Current;
        if (null == _prefab) return null;

        EndingSkyCover cover = Instantiate(_prefab);
        DontDestroyOnLoad(cover.gameObject);
        return cover;
    }

    private void Awake()
    {
        Current = this;

        if (null != cloudImage) cloudStartPos = cloudImage.anchoredPosition;
        if (null != skyImage) skyStartPos = skyImage.anchoredPosition;
        if (null != skyImage2) skyStartPos2 = skyImage2.anchoredPosition;

        SetBlackAlpha(0f);
    }

    /// <summary>구름과 하늘이 아래에서 올라와 화면을 덮는다(카메라 상승과 같은 시간). 덮인 뒤에는 천천히 흔들린다.</summary>
    public void PlayCover()
    {
        PlayMove(true, moveDuration, StartFloating);
    }

    /// <summary>PlayCover와 같고 이동 시간만 _duration을 쓴다(메인메뉴 복귀는 본편 지금 시간에 맞춘다).</summary>
    public void PlayCover(float _duration)
    {
        PlayMove(true, _duration, StartFloating);
    }

    /// <summary>
    /// 본편 UIView_SkyProduction.SetMainMenuMode와 같다. 메인메뉴로 나갈 때는 메인메뉴 화면이 하늘 자리를 대신하므로
    /// 하늘 그림을 끄고 구름만 메인메뉴용 그림으로 바꾼다.
    /// </summary>
    public void SetMainMenuMode(bool _bMainMenu)
    {
        if (null != skyImage) skyImage.gameObject.SetActive(false == _bMainMenu);
        if (null != skyImage2) skyImage2.gameObject.SetActive(false == _bMainMenu);

        if (null != cloudImage)
        {
            Image image = cloudImage.GetComponent<Image>();
            Sprite sprite = _bMainMenu ? loadingCloudSprite_MainMenu : loadingCloudSprite;
            if (null != image && null != sprite) image.sprite = sprite;
        }
    }

    /// <summary>덮었던 구름과 하늘이 다시 아래로 내려가며 걷힌다(카메라 하강과 같은 타이밍).</summary>
    public void PlayReveal(Action _onComplete)
    {
        PlayMove(false, moveDuration, () => _onComplete?.Invoke());
    }

    public Tween FadeBlack(float _alpha, float _duration)
    {
        KillTween(ref blackTween);
        if (null == blackImage) return null;

        blackImage.enabled = true;
        blackTween = blackImage.DOFade(_alpha, _duration).SetEase(Ease.InOutSine).SetUpdate(true)
            .OnComplete(() => SetBlackAlpha(_alpha));
        return blackTween;
    }

    /// <summary>검은 화면을 바로 덮거나 걷는다(엔딩 씬을 에디터에서 바로 켰을 때 구름 대신 쓴다).</summary>
    public void SetBlack(float _alpha)
    {
        KillTween(ref blackTween);
        SetBlackAlpha(_alpha);
    }

    public void Dispose()
    {
        Destroy(gameObject);
    }

    private void PlayMove(bool _bCover, float _duration, Action _onComplete)
    {
        KillTween(ref skyTween1);
        KillTween(ref skyTween2);
        KillTween(ref cloudTween);
        if (null != moveSequence && moveSequence.IsActive()) moveSequence.Kill();

        Vector2 cloudDest = _bCover && null != cloudTargetTransform ? cloudTargetTransform.anchoredPosition : cloudStartPos;
        Vector2 skyDest = _bCover && null != skyTargetTransform ? skyTargetTransform.anchoredPosition : skyStartPos;
        Vector2 skyDest2 = _bCover && null != skyTargetTransform ? skyTargetTransform.anchoredPosition : skyStartPos2;

        moveSequence = DOTween.Sequence();
        if (null != cloudImage) moveSequence.Join(cloudImage.DOAnchorPos(cloudDest, _duration, true).SetEase(moveEase));
        if (null != skyImage) moveSequence.Join(skyImage.DOAnchorPos(skyDest, _duration, true).SetEase(moveEase));
        if (null != skyImage2) moveSequence.Join(skyImage2.DOAnchorPos(skyDest2, _duration, true).SetEase(moveEase));
        moveSequence.OnComplete(() => _onComplete?.Invoke());
    }

    // 본편 UIView_SkyProduction.StartFloatingSequence와 같은 흔들림.
    private void StartFloating()
    {
        if (null != skyImage)
        {
            skyTween1 = skyImage.DOAnchorPosY(skyImage.anchoredPosition.y + floatingAmplitude * 0.4f, floatingDuration)
                .SetOptions(true).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        if (null != skyImage2)
        {
            skyTween2 = skyImage2.DOAnchorPosY(skyImage2.anchoredPosition.y - floatingAmplitude * 0.4f, floatingDuration)
                .SetOptions(true).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }

        if (null != cloudImage)
        {
            cloudTween = cloudImage.DOAnchorPosY(cloudImage.anchoredPosition.y + floatingAmplitude, floatingDuration * 0.85f)
                .SetOptions(true).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
        }
    }

    private void SetBlackAlpha(float _alpha)
    {
        if (null == blackImage) return;

        Color color = blackImage.color;
        color.a = _alpha;
        blackImage.color = color;
        blackImage.enabled = _alpha > 0f;
    }

    private static void KillTween(ref Tween _tween)
    {
        if (null != _tween && _tween.IsActive()) _tween.Kill();
        _tween = null;
    }

    private void OnDestroy()
    {
        KillTween(ref skyTween1);
        KillTween(ref skyTween2);
        KillTween(ref cloudTween);
        KillTween(ref blackTween);
        if (null != moveSequence && moveSequence.IsActive()) moveSequence.Kill();

        if (Current == this) Current = null;
    }
}
