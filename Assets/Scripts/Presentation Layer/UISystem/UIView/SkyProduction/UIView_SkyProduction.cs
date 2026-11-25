using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using DG.Tweening;

public class UIView_SkyProduction : UIView
{
    // 맵별 하늘 그림. 목록에 없는 맵(마을·1스테이지)은 프리팹에 들어 있는 기본 그림을 쓴다.
    [System.Serializable]
    private struct SkyMapTheme
    {
        public MapType mapType;
        public Sprite sky;
        public Sprite skyCloud;
        public Sprite cloud;
    }

    // //외부 의존성

    // //내부 의존성
    [SerializeField] private RectTransform cloudImage;
    [SerializeField] private RectTransform skyImage;
    [SerializeField] private RectTransform skyImage2;
    [SerializeField] private RectTransform cloudTargetTransform;
    [SerializeField] private RectTransform skyTargetTransform;
    [SerializeField] private float moveDuration = 1.0f;
    [SerializeField] private bool useCustomCurve = false;
    [SerializeField] private AnimationCurve moveCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
    [SerializeField] private Ease moveEase = Ease.OutCubic;
    [SerializeField] private float floatingAmplitude = 7.0f;
    [SerializeField] private float floatingDuration = 4.0f;
    [SerializeField] private Sprite loadingCloudSprite_MainMenu;
    [SerializeField] private Sprite loadingCloudSprite;
    [SerializeField] private SkyMapTheme[] mapThemes;
    // 출발 맵 그림 → 도착 맵 그림을 덮는 동안 색만 서서히 넘기는 셰이더(UI/SkyThemeBlend).
    [SerializeField] private Shader themeBlendShader;

    private Sequence moveSequence;
    private Tween skyTween1;
    private Tween skyTween2;
    private Tween cloudTween;
    private Vector2 cloudStartPos;
    private Vector2 skyStartPos;
    private Vector2 skyStartPos2;
    private bool isMoved = false;
    private bool hasStartPos = false;

    private Image cloudImg;
    private Image skyImg;
    private Image skyImg2;
    private Sprite defaultSkySprite;
    private Sprite defaultSkyCloudSprite;
    private bool hasDefaultSprites = false;
    private bool bMainMenuMode = false;
    private MapType displayedTheme = MapType.Town;
    private MapType pendingTheme = MapType.Town;
    private bool bThemeBlendPending = false;
    private Material cloudBlendMat;
    private Material skyBlendMat;
    private Material skyBlendMat2;
    private Tween themeBlendTween;
    private static readonly int BlendTexId = Shader.PropertyToID("_BlendTex");
    private static readonly int BlendId = Shader.PropertyToID("_Blend");

    #region Public Methods

    public override void Initialize(UIViewContext _ctx)
    {
        base.Initialize(_ctx);
        CacheStartPositions();
    }

    public override void SetupUI()
    {
        base.SetupUI();
    }

    public override void Refresh()
    {
        base.Refresh();
    }

    public override void Release()
    {
        KillMoveSequence();
        KillFloatingSequence();
        base.Release();
    }

    #endregion

    #region Protected Methods

    protected override void OnShow()
    {
        base.OnShow();
    }

    protected override void OnHide()
    {
        base.OnHide();
    }

    #endregion

    #region Unity Events

    protected override void Awake()
    {
        base.Awake();
        CacheStartPositions();
    }

    public override void OnDestroy()
    {
        KillMoveSequence();
        KillFloatingSequence();
        DestroyBlendMaterials();
        base.OnDestroy();
    }

    #endregion

    #region Private Methods

    private void CacheStartPositions()
    {
        if (hasStartPos) return;

        if (cloudImage != null)
            cloudStartPos = cloudImage.anchoredPosition;
        if (skyImage != null)
            skyStartPos = skyImage.anchoredPosition;
        if (skyImage2 != null)
            skyStartPos2 = skyImage2.anchoredPosition;

        hasStartPos = true;
    }

    private void PlayMoveSequence()
    {
        CacheStartPositions();
        KillMoveSequence();
        KillFloatingSequence();

        moveSequence = DOTween.Sequence();
        isMoved = !isMoved;

        // 덮는 동안 출발 맵 그림 → 도착 맵 그림으로 색을 넘긴다. 다 덮였을 때는 도착 맵 그림 그대로라
        // 씬이 바뀐 뒤 걷히는 연출은 도착 맵 그림만으로 이어진다.
        // 섞기 트윈은 시퀀스에 넣지 않고 같은 시간으로 따로 돌린다(중첩 트윈은 따로 끊을 수 없어서).
        if (bThemeBlendPending)
        {
            if (false == (isMoved && false == bMainMenuMode && moveDuration > 0f && BeginThemeBlend()))
                CompleteThemeBlend();
        }

        Vector2 cloudDest = isMoved ? (cloudTargetTransform != null ? cloudTargetTransform.anchoredPosition : cloudStartPos) : cloudStartPos;
        Vector2 skyDest = isMoved ? (skyTargetTransform != null ? skyTargetTransform.anchoredPosition : skyStartPos) : skyStartPos;
        Vector2 skyDest2 = isMoved ? (skyTargetTransform != null ? skyTargetTransform.anchoredPosition : skyStartPos2) : skyStartPos2;

        if (null != cloudImage)
        {
            var tween = cloudImage.DOAnchorPos(cloudDest, moveDuration, true);
            if (useCustomCurve)
                tween.SetEase(moveCurve);
            else
                tween.SetEase(moveEase);
            moveSequence.Join(tween);
        }

        if (null != skyImage)
        {
            var tween = skyImage.DOAnchorPos(skyDest, moveDuration, true);
            if (useCustomCurve)
                tween.SetEase(moveCurve);
            else
                tween.SetEase(moveEase);
            moveSequence.Join(tween);
        }

        if (null != skyImage2)
        {
            var tween = skyImage2.DOAnchorPos(skyDest2, moveDuration, true);
            if (useCustomCurve)
                tween.SetEase(moveCurve);
            else
                tween.SetEase(moveEase);
            moveSequence.Join(tween);
        }

        moveSequence.OnComplete(StartFloatingSequence);
    }

    private void StartFloatingSequence()
    {
        KillFloatingSequence();

        // Sequence를 쓰지 않고 개별 Tween으로 처리하여 경고를 방지합니다.
        if (null != skyImage)
        {
            Vector2 currentPos = skyImage.anchoredPosition;
            skyTween1 = skyImage.DOAnchorPosY(currentPos.y + (floatingAmplitude * 0.4f), floatingDuration)
                .SetOptions(true)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (null != skyImage2)
        {
            Vector2 currentPos = skyImage2.anchoredPosition;
            skyTween2 = skyImage2.DOAnchorPosY(currentPos.y - (floatingAmplitude * 0.4f), floatingDuration)
                .SetOptions(true)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }

        if (null != cloudImage)
        {
            Vector2 currentPos = cloudImage.anchoredPosition;
            cloudTween = cloudImage.DOAnchorPosY(currentPos.y + floatingAmplitude, floatingDuration * 0.85f)
                .SetOptions(true)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
    }

    private void KillMoveSequence()
    {
        if (null != moveSequence && true == moveSequence.IsActive())
        {
            moveSequence.Kill();
        }

        // 덮는 도중에 끊겼으면 도착 맵 그림으로 확정한다(섞던 재질이 남지 않게).
        // 아직 시작 전인 준비(PrepareMapTransition)는 그대로 둔다.
        if (null != themeBlendTween)
            CompleteThemeBlend();
    }

    private void CacheDefaultSprites()
    {
        if (hasDefaultSprites) return;

        if (cloudImage != null) cloudImg = cloudImage.GetComponent<Image>();
        if (skyImage != null) skyImg = skyImage.GetComponent<Image>();
        if (skyImage2 != null) skyImg2 = skyImage2.GetComponent<Image>();

        defaultSkySprite = skyImg != null ? skyImg.sprite : null;
        defaultSkyCloudSprite = skyImg2 != null ? skyImg2.sprite : null;

        hasDefaultSprites = true;
    }

    private void GetThemeSprites(MapType _mapType, out Sprite _sky, out Sprite _skyCloud, out Sprite _cloud)
    {
        CacheDefaultSprites();

        _sky = defaultSkySprite;
        _skyCloud = defaultSkyCloudSprite;
        _cloud = loadingCloudSprite;

        if (mapThemes == null) return;

        for (int i = 0; i < mapThemes.Length; i++)
        {
            if (mapThemes[i].mapType != _mapType) continue;

            if (mapThemes[i].sky != null) _sky = mapThemes[i].sky;
            if (mapThemes[i].skyCloud != null) _skyCloud = mapThemes[i].skyCloud;
            if (mapThemes[i].cloud != null) _cloud = mapThemes[i].cloud;
            return;
        }
    }

    private void ApplyThemeSprites(MapType _mapType)
    {
        GetThemeSprites(_mapType, out Sprite sky, out Sprite skyCloud, out Sprite cloud);

        if (skyImg != null) skyImg.sprite = sky;
        if (skyImg2 != null) skyImg2.sprite = skyCloud;
        if (cloudImg != null) cloudImg.sprite = bMainMenuMode ? loadingCloudSprite_MainMenu : cloud;
    }

    private bool BeginThemeBlend()
    {
        if (themeBlendShader == null) return false;

        GetThemeSprites(pendingTheme, out Sprite sky, out Sprite skyCloud, out Sprite cloud);

        SetBlendMaterial(skyImg, ref skyBlendMat, sky);
        SetBlendMaterial(skyImg2, ref skyBlendMat2, skyCloud);
        SetBlendMaterial(cloudImg, ref cloudBlendMat, cloud);
        SetBlendAmount(0f);

        // 구름 이동과 같은 곡선으로 섞는다. 색이 구름이 크게 지나가는 동안 대부분 바뀌고
        // 구름이 멈출 때 함께 멈춰, 멈춘 화면에서 색만 물드는 구간이 생기지 않는다.
        themeBlendTween = DOVirtual.Float(0f, 1f, moveDuration, SetBlendAmount)
            .OnComplete(CompleteThemeBlend);
        if (useCustomCurve)
            themeBlendTween.SetEase(moveCurve);
        else
            themeBlendTween.SetEase(moveEase);
        return true;
    }

    private void SetBlendMaterial(Image _img, ref Material _mat, Sprite _to)
    {
        if (_img == null) return;

        // 같은 그림이면 섞을 것이 없으니 기본 재질 그대로 둔다.
        if (_to == null || _to == _img.sprite)
        {
            _img.material = null;
            return;
        }

        if (_mat == null)
        {
            _mat = new Material(themeBlendShader);
            _mat.hideFlags = HideFlags.DontSave;
        }

        _mat.SetTexture(BlendTexId, _to.texture);
        _img.material = _mat;
    }

    private void SetBlendAmount(float _value)
    {
        if (skyBlendMat != null) skyBlendMat.SetFloat(BlendId, _value);
        if (skyBlendMat2 != null) skyBlendMat2.SetFloat(BlendId, _value);
        if (cloudBlendMat != null) cloudBlendMat.SetFloat(BlendId, _value);
    }

    // 섞기를 끝내고 도착 맵 그림을 기본 재질로 올린다. 섞기 마지막 값(1)은 도착 그림과 같은 픽셀이라 바꿔도 티가 나지 않는다.
    private void CompleteThemeBlend()
    {
        if (null != themeBlendTween && true == themeBlendTween.IsActive())
        {
            themeBlendTween.Kill();
        }
        themeBlendTween = null;

        if (false == bThemeBlendPending) return;

        bThemeBlendPending = false;
        displayedTheme = pendingTheme;

        if (skyImg != null) skyImg.material = null;
        if (skyImg2 != null) skyImg2.material = null;
        if (cloudImg != null) cloudImg.material = null;

        ApplyThemeSprites(displayedTheme);
    }

    private void DestroyBlendMaterials()
    {
        if (null != themeBlendTween && true == themeBlendTween.IsActive())
        {
            themeBlendTween.Kill();
        }
        themeBlendTween = null;

        if (skyBlendMat != null) Destroy(skyBlendMat);
        if (skyBlendMat2 != null) Destroy(skyBlendMat2);
        if (cloudBlendMat != null) Destroy(cloudBlendMat);
        skyBlendMat = null;
        skyBlendMat2 = null;
        cloudBlendMat = null;
    }

    private void KillFloatingSequence()
    {
        if (null != skyTween1 && true == skyTween1.IsActive())
        {
            skyTween1.Kill();
        }
        skyTween1 = null;

        if (null != skyTween2 && true == skyTween2.IsActive())
        {
            skyTween2.Kill();
        }
        skyTween2 = null;

        if (null != cloudTween && true == cloudTween.IsActive())
        {
            cloudTween.Kill();
        }
        cloudTween = null;
    }

    #endregion

    public void StartSkyProduction()
    {
        PlayMoveSequence();
    }

    /// <summary>
    /// 애니메이션 없이 즉시 "구름이 덮인" 상태로 스냅한다. MainMenu → Town 최초 진입 시,
    /// 화면이 메인 메뉴에 가려져 있는 동안 미리 덮인 상태로 세팅해두고, 이후 StartSkyProduction()
    /// 한 번으로 카메라 하강과 같은 타이밍에 "걷히는" 연출만 재생하기 위한 용도.
    /// </summary>
    public void SnapToCoveredState()
    {
        CacheStartPositions();
        KillMoveSequence();
        KillFloatingSequence();

        SetMainMenuMode(true);

        isMoved = true;

        if (cloudImage != null)
            cloudImage.anchoredPosition = cloudTargetTransform != null ? cloudTargetTransform.anchoredPosition : cloudStartPos;

        if (skyImage != null)
            skyImage.anchoredPosition = skyTargetTransform != null ? skyTargetTransform.anchoredPosition : skyStartPos;

        if (skyImage2 != null)
            skyImage2.anchoredPosition = skyTargetTransform != null ? skyTargetTransform.anchoredPosition : skyStartPos2;
    }

    /// <summary>
    /// 메인 메뉴 연출 등 특정 상황에서 배경 하늘(Sky)과 보조 구름(LoadingSkyCloud) 객체를 끄고 켤 수 있으며, 구름 스프라이트를 변경합니다.
    /// </summary>
    public void SetMainMenuMode(bool isMainMenu)
    {
        CompleteThemeBlend();
        CacheDefaultSprites();
        bMainMenuMode = isMainMenu;

        if (null != skyImage)
        {
            skyImage.gameObject.SetActive(!isMainMenu);
        }
        
        if (null != skyImage2)
        {
            skyImage2.gameObject.SetActive(!isMainMenu);
        }

        // 구름 그림: 메인메뉴 모드는 메인메뉴 전용 그림, 아니면 지금 보이는 맵의 그림.
        ApplyThemeSprites(displayedTheme);
    }

    /// <summary>
    /// 다음 덮기 연출을 _from 맵 그림으로 시작해 _to 맵 그림으로 끝나게 준비한다. 덮는 동안 셰이더로 색만 서서히 넘기고,
    /// 다 덮인 뒤 씬이 바뀌어 걷힐 때는 _to 그림 그대로다. 두 맵 그림이 같으면(마을↔1스테이지 등) 섞지 않는다.
    /// SetMainMenuMode(false) 다음, StartSkyProduction() 전에 부른다.
    /// </summary>
    public void PrepareMapTransition(MapType _from, MapType _to)
    {
        CompleteThemeBlend();

        displayedTheme = _from;
        ApplyThemeSprites(_from);

        if (_to == _from) return;

        GetThemeSprites(_from, out Sprite fromSky, out Sprite fromSkyCloud, out Sprite fromCloud);
        GetThemeSprites(_to, out Sprite toSky, out Sprite toSkyCloud, out Sprite toCloud);

        if (fromSky == toSky && fromSkyCloud == toSkyCloud && fromCloud == toCloud)
        {
            displayedTheme = _to;
            return;
        }

        pendingTheme = _to;
        bThemeBlendPending = true;
    }
}
