#if UNITY_EDITOR
using System.IO;
using Coffee.UIEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버프 HUD 임시 에셋/프리팹 생성기. 프레임/게이지 임시 스프라이트 PNG를 만들고
/// HUD_BuffIcon/HUD_BuffBar 프리팹과 스태미나+버프 조합 프리팹(HUD_StaminaGroup)을 만들고 HUD_UI의 참조를 교체한다. 실제 스프라이트가 준비되면
/// 프리팹의 Image/RawImage 스프라이트만 교체하면 된다(여러 번 실행해도 같은 결과로 덮어쓴다).
/// </summary>
public static class HUD_BuffPrefabBuilder
{
    private const string ArtDir = "Assets/Graphics/HUD/Buff";
    private const string PrefabDir = "Assets/Prefabs/UI/HUD/Buff";
    private const string StaminaPrefabPath = "Assets/Prefabs/UI/HUD/SliderBar/HUD_Stamina.prefab";
    private const string HudUiPrefabPath = "Assets/Prefabs/UI/HUD/HUD_UI.prefab";
    // 평행사변형 두 칸의 사선 외곽선(2px)이 같은 픽셀에 정확히 겹쳐 맞물리는 칸 간격(스프라이트 폭 24가 아니라 이 값)
    private const float DashCellPitch = 15f;
    private const float DashVisibleWidth = 38f;
    private const float DashReservedWidth = 46f;
    private const string DashBaseSpritePath = "Assets/Graphics/HUD/Dash/Dash_HUD_Base.png";
    private const string DashFillSpritePath = "Assets/Graphics/HUD/Dash/Dash_HUD_Fill.png";
    private const string NumberSheetPath = "Assets/Graphics/Font/SmallCurrencyNum.png";

    [MenuItem("LumberBoy/HUD/Build Buff HUD Prefabs")]
    public static void Build()
    {
        EnsureFolder("Assets/Graphics/HUD", "Buff");
        EnsureFolder("Assets/Prefabs/UI/HUD", "Buff");

        Color32 _white = new Color32(254, 254, 254, 255);
        Color32 _outline = new Color32(37, 33, 30, 255);
        Color32 _well = new Color32(40, 30, 22, 255);
        Color32 _emptyTop = new Color32(51, 38, 28, 255);
        Color32 _emptyBottom = new Color32(46, 35, 26, 255);
        Color32 _clear = new Color32(0, 0, 0, 0);

        // 프레임 16x16: 흰 스티커 1 + 어두운 외곽 1 + 12x12 아이콘 우물
        Texture2D _frame = new Texture2D(16, 16, TextureFormat.RGBA32, false);

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bool _bEdge = 0 == x || 15 == x || 0 == y || 15 == y;
                bool _bCorner = (0 == x || 15 == x) && (0 == y || 15 == y);
                bool _bRing = 1 == x || 14 == x || 1 == y || 14 == y;
                Color32 _c = true == _bCorner ? _clear : (true == _bEdge ? _white : (true == _bRing ? _outline : _well));
                _frame.SetPixel(x, y, _c);
            }
        }

        SavePng(_frame, ArtDir + "/Buff_Frame.png");

        // 게이지 뒷판 16x4(안쪽 14x2). 텍스처 y는 아래에서 위.
        Texture2D _back = new Texture2D(16, 4, TextureFormat.RGBA32, false);

        for (int y = 0; y < 4; y++)
        {
            for (int x = 0; x < 16; x++)
            {
                bool _bCorner = (0 == x || 15 == x) && (0 == y || 3 == y);
                Color32 _c;

                if (true == _bCorner)
                {
                    _c = _clear;
                }
                else if (0 == x || 15 == x || 0 == y || 3 == y)
                {
                    _c = _outline;
                }
                else
                {
                    _c = 2 == y ? _emptyTop : _emptyBottom;
                }

                _back.SetPixel(x, y, _c);
            }
        }

        SavePng(_back, ArtDir + "/Buff_GaugeBack.png");

        Texture2D _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _whiteTex.SetPixel(0, 0, new Color32(255, 255, 255, 255));
        SavePng(_whiteTex, ArtDir + "/Buff_White.png");

        AssetDatabase.Refresh();
        ImportAsSprite(ArtDir + "/Buff_Frame.png");
        ImportAsSprite(ArtDir + "/Buff_GaugeBack.png");
        ImportAsSprite(ArtDir + "/Buff_White.png");

        Sprite _sFrame = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/Buff_Frame.png");
        Sprite _sBack = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/Buff_GaugeBack.png");
        Sprite _sWhite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + "/Buff_White.png");

        // 숫자 스프라이트: 시트 순서가 1~9,0 이므로 _0~_8이 1~9, _9가 0
        UnityEngine.Object[] _all = AssetDatabase.LoadAllAssetsAtPath(NumberSheetPath);
        Sprite[] _digits = new Sprite[10];

        for (int i = 0; i < _all.Length; i++)
        {
            Sprite _sp = _all[i] as Sprite;

            if (null == _sp)
            {
                continue;
            }

            string _n = _sp.name;
            int _k = int.Parse(_n.Substring(_n.LastIndexOf('_') + 1));

            if (_k <= 8)
            {
                _digits[_k + 1] = _sp;
            }
            else if (9 == _k)
            {
                _digits[0] = _sp;
            }
        }

        for (int i = 0; i < 10; i++)
        {
            if (null == _digits[i])
            {
                Debug.LogError("[HUD_BuffPrefabBuilder] 숫자 스프라이트를 찾지 못했습니다: " + i);
                return;
            }
        }

        // ===== HUD_BuffIcon.prefab (18x21 칸) =====
        GameObject _root = new GameObject("HUD_BuffIcon", typeof(RectTransform));
        RectTransform _rootRt = (RectTransform)_root.transform;
        SetRect(_rootRt, Vector2.zero, new Vector2(18f, 21f), Vector2.zero);
        HUD_BuffIcon _icon = _root.AddComponent<HUD_BuffIcon>();

        RectTransform _visual = NewRect("Visual", _rootRt, new Vector2(9f, 10.5f), new Vector2(18f, 21f), new Vector2(0.5f, 0.5f));
        CanvasGroup _group = _visual.gameObject.AddComponent<CanvasGroup>();
        _group.blocksRaycasts = false;
        _group.interactable = false;

        Image _frameImg = NewImage("Frame", _visual, _sFrame, new Vector2(1f, 4f), new Vector2(16f, 16f));
        UIEffect _outlineFx = _frameImg.gameObject.AddComponent<UIEffect>();
        ConfigureOutline(_outlineFx);
        // 프레임 스프라이트에 외곽선을 직접 그리므로 꺼 둔다(필요하면 다시 켜서 쓸 수 있게 컴포넌트는 남김)
        _outlineFx.enabled = false;
        Image _accent = NewImage("Accent", _visual, _sWhite, new Vector2(3f, 18f), new Vector2(12f, 1f));
        _accent.color = new Color32(170, 212, 88, 255);
        RawImage _iconRaw = NewRaw("Icon", _visual, new Vector2(3f, 6f), new Vector2(12f, 12f));
        NewImage("GaugeBack", _visual, _sBack, new Vector2(1f, 0f), new Vector2(16f, 4f));
        Image _fillBottom = NewImage("GaugeFillBottom", _visual, _sWhite, new Vector2(2f, 1f), new Vector2(14f, 1f));
        Image _fillTop = NewImage("GaugeFillTop", _visual, _sWhite, new Vector2(2f, 2f), new Vector2(14f, 1f));
        ConfigureFilled(_fillBottom, new Color32(116, 165, 48, 255));
        ConfigureFilled(_fillTop, new Color32(169, 211, 86, 255));
        RawImage _numberRaw = NewRaw("Number", _visual, new Vector2(9f, 4f), new Vector2(8f, 8f));

        SerializedObject _so = new SerializedObject(_icon);
        _so.FindProperty("visualRoot").objectReferenceValue = _visual;
        _so.FindProperty("visualGroup").objectReferenceValue = _group;
        _so.FindProperty("frameImage").objectReferenceValue = _frameImg;
        _so.FindProperty("outlineEffect").objectReferenceValue = _outlineFx;
        _so.FindProperty("accentImage").objectReferenceValue = _accent;
        _so.FindProperty("iconImage").objectReferenceValue = _iconRaw;
        _so.FindProperty("gaugeFillTopImage").objectReferenceValue = _fillTop;
        _so.FindProperty("gaugeFillBottomImage").objectReferenceValue = _fillBottom;
        _so.FindProperty("numberImage").objectReferenceValue = _numberRaw;
        _so.ApplyModifiedPropertiesWithoutUndo();

        string _iconPrefabPath = PrefabDir + "/HUD_BuffIcon.prefab";
        GameObject _iconPrefab = PrefabUtility.SaveAsPrefabAsset(_root, _iconPrefabPath);
        UnityEngine.Object.DestroyImmediate(_root);

        // ===== HUD_BuffBar.prefab (스태미나와 무관한 독립 프리팹) =====
        GameObject _barGo = new GameObject("HUD_BuffBar", typeof(RectTransform));
        RectTransform _barRt = (RectTransform)_barGo.transform;
        SetRect(_barRt, Vector2.zero, new Vector2(160f, 21f), Vector2.zero);
        HUD_BuffBar _bar = _barGo.AddComponent<HUD_BuffBar>();

        RectTransform _icons = NewRect("Icons", _barRt, Vector2.zero, new Vector2(160f, 21f), Vector2.zero);
        RectTransform _chip = NewRect("Chip", _barRt, Vector2.zero, new Vector2(18f, 21f), Vector2.zero);
        Image _chipFrame = NewImage("ChipFrame", _chip, _sFrame, new Vector2(1f, 4f), new Vector2(16f, 16f));
        UIEffect _chipFx = _chipFrame.gameObject.AddComponent<UIEffect>();
        ConfigureOutline(_chipFx);
        _chipFx.enabled = false;
        RawImage _chipNumber = NewRaw("ChipNumber", _chip, new Vector2(5f, 8f), new Vector2(8f, 8f));
        _chip.gameObject.SetActive(false);
        RawImage _fxRaw = NewRaw("Fx", _barRt, new Vector2(-24f, -24f), new Vector2(200f, 112f));

        SerializedObject _bso = new SerializedObject(_bar);
        _bso.FindProperty("iconPrefab").objectReferenceValue = _iconPrefab.GetComponent<HUD_BuffIcon>();
        _bso.FindProperty("iconRoot").objectReferenceValue = _icons;
        _bso.FindProperty("fxOverlay").objectReferenceValue = _fxRaw;
        _bso.FindProperty("chipRoot").objectReferenceValue = _chip;
        _bso.FindProperty("chipNumber").objectReferenceValue = _chipNumber;
        SerializedProperty _dp = _bso.FindProperty("digitSprites");
        _dp.arraySize = 10;

        for (int i = 0; i < 10; i++)
        {
            _dp.GetArrayElementAtIndex(i).objectReferenceValue = _digits[i];
        }

        _bso.ApplyModifiedPropertiesWithoutUndo();
        GameObject _barPrefab = PrefabUtility.SaveAsPrefabAsset(_barGo, PrefabDir + "/HUD_BuffBar.prefab");
        UnityEngine.Object.DestroyImmediate(_barGo);

        // ===== HUD_DashGauge.prefab (대시 2칸) =====
        GameObject _dashPrefab = BuildDashPrefab();

        // ===== HUD_StaminaGroup.prefab (스태미나 + 버프 + 대시 조합) =====
        GameObject _staminaPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StaminaPrefabPath);
        GameObject _groupGo = new GameObject("HUD_StaminaGroup", typeof(RectTransform));
        RectTransform _groupRt = (RectTransform)_groupGo.transform;
        _groupRt.anchorMin = Vector2.zero;
        _groupRt.anchorMax = Vector2.one;
        _groupRt.offsetMin = Vector2.zero;
        _groupRt.offsetMax = Vector2.zero;
        HUD_StaminaGroup _groupComp = _groupGo.AddComponent<HUD_StaminaGroup>();

        GameObject _staminaInst = (GameObject)PrefabUtility.InstantiatePrefab(_staminaPrefab, _groupRt);
        GameObject _buffInst = (GameObject)PrefabUtility.InstantiatePrefab(_barPrefab, _groupRt);
        RectTransform _buffRt = (RectTransform)_buffInst.transform;
        _buffRt.anchorMin = new Vector2(0.5f, 0f);
        _buffRt.anchorMax = new Vector2(0.5f, 0f);
        _buffRt.pivot = Vector2.zero;
        // 스태미나 바(가운데 기준 x -80)의 왼쪽 끝, 게이지 아래쪽이 바 위쪽에서 8px 위(바 y 10 + 높이 18 + 간격 8)
        _buffRt.anchoredPosition = new Vector2(-80f, 36f);

        GameObject _dashInst = (GameObject)PrefabUtility.InstantiatePrefab(_dashPrefab, _groupRt);
        RectTransform _dashRt = (RectTransform)_dashInst.transform;
        _dashRt.anchorMin = new Vector2(0.5f, 0f);
        _dashRt.anchorMax = new Vector2(0.5f, 0f);
        _dashRt.pivot = Vector2.zero;
        // 바 오른쪽 끝에 붙인다: 보이는 폭 38(맞물린 2칸), 보이는 아래쪽은 칸 안 7px 위 -> 게이지 아래쪽이 바 위 8px
        _dashRt.anchoredPosition = new Vector2(80f - DashVisibleWidth, 29f);

        SerializedObject _gso = new SerializedObject(_groupComp);
        _gso.FindProperty("dashGauge").objectReferenceValue = _dashInst.GetComponent<HUD_DashGauge>();
        _gso.FindProperty("stamina").objectReferenceValue = _staminaInst.GetComponent<HUD_Stemina>();
        _gso.FindProperty("buffBar").objectReferenceValue = _buffInst.GetComponent<HUD_BuffBar>();
        _gso.ApplyModifiedPropertiesWithoutUndo();
        GameObject _groupPrefab = PrefabUtility.SaveAsPrefabAsset(_groupGo, PrefabDir + "/HUD_StaminaGroup.prefab");
        UnityEngine.Object.DestroyImmediate(_groupGo);

        // ===== HUD_UI.prefab: 스태미나 프리팹 참조를 조합 프리팹으로 교체 =====
        GameObject _hudUi = PrefabUtility.LoadPrefabContents(HudUiPrefabPath);

        try
        {
            UIView_HUD _view = _hudUi.GetComponentInChildren<UIView_HUD>(true);
            SerializedObject _vso = new SerializedObject(_view);
            _vso.FindProperty("hudSteminaBarPrefab").objectReferenceValue = _groupPrefab;
            _vso.FindProperty("dashReservedWidth").floatValue = DashReservedWidth;
            _vso.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(_hudUi, HudUiPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(_hudUi);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[HUD_BuffPrefabBuilder] 완료: HUD_BuffIcon / HUD_BuffBar / HUD_StaminaGroup 프리팹 생성, HUD_UI 참조 교체");
    }

    private static GameObject BuildDashPrefab()
    {
        Sprite _base = AssetDatabase.LoadAssetAtPath<Sprite>(DashBaseSpritePath);
        Sprite _fill = AssetDatabase.LoadAssetAtPath<Sprite>(DashFillSpritePath);

        GameObject _go = new GameObject("HUD_DashGauge", typeof(RectTransform));
        RectTransform _rt = (RectTransform)_go.transform;
        SetRect(_rt, Vector2.zero, new Vector2(DashVisibleWidth, 24f), Vector2.zero);
        HUD_DashGauge _gauge = _go.AddComponent<HUD_DashGauge>();

        RectTransform _visual = NewRect("Visual", _rt, new Vector2(DashVisibleWidth * 0.5f, 12f), new Vector2(DashVisibleWidth, 24f), new Vector2(0.5f, 0.5f));
        RectTransform[] _cells = new RectTransform[HUD_DashGauge.MaxCells];
        Image[] _fills = new Image[HUD_DashGauge.MaxCells];
        Image[] _flashes = new Image[HUD_DashGauge.MaxCells];

        for (int i = 0; i < HUD_DashGauge.MaxCells; i++)
        {
            RectTransform _cell = NewRect("Cell_" + i, _visual, new Vector2(DashCellPitch * i, 0f), new Vector2(24f, 24f), Vector2.zero);
            Image _baseImg = NewImage("Base", _cell, _base, Vector2.zero, new Vector2(24f, 24f));
            Image _fillImg = NewImage("Fill", _cell, _fill, Vector2.zero, new Vector2(24f, 24f));
            _fillImg.type = Image.Type.Filled;
            _fillImg.fillMethod = Image.FillMethod.Horizontal;
            _fillImg.fillOrigin = 0;
            _fillImg.fillAmount = 1f;
            Image _flashImg = NewImage("Flash", _cell, _fill, Vector2.zero, new Vector2(24f, 24f));
            _flashImg.type = Image.Type.Filled;
            _flashImg.fillMethod = Image.FillMethod.Horizontal;
            _flashImg.fillOrigin = 0;
            _flashImg.fillAmount = 1f;
            UIEffect _flashFx = _flashImg.gameObject.AddComponent<UIEffect>();
            _flashFx.colorFilter = ColorFilter.Replace;
            _flashFx.color = Color.white;
            _flashImg.enabled = false;
            _baseImg.raycastTarget = false;
            _cells[i] = _cell;
            _fills[i] = _fillImg;
            _flashes[i] = _flashImg;
        }

        SerializedObject _so = new SerializedObject(_gauge);
        _so.FindProperty("visualRoot").objectReferenceValue = _visual;
        SerializedProperty _cp = _so.FindProperty("cellRects");
        SerializedProperty _fp = _so.FindProperty("fillImages");
        SerializedProperty _lp = _so.FindProperty("flashImages");
        _cp.arraySize = HUD_DashGauge.MaxCells;
        _fp.arraySize = HUD_DashGauge.MaxCells;
        _lp.arraySize = HUD_DashGauge.MaxCells;

        for (int i = 0; i < HUD_DashGauge.MaxCells; i++)
        {
            _cp.GetArrayElementAtIndex(i).objectReferenceValue = _cells[i];
            _fp.GetArrayElementAtIndex(i).objectReferenceValue = _fills[i];
            _lp.GetArrayElementAtIndex(i).objectReferenceValue = _flashes[i];
        }

        _so.ApplyModifiedPropertiesWithoutUndo();
        GameObject _prefab = PrefabUtility.SaveAsPrefabAsset(_go, PrefabDir + "/HUD_DashGauge.prefab");
        UnityEngine.Object.DestroyImmediate(_go);
        return _prefab;
    }

    private static void ConfigureOutline(UIEffect _fx)
    {
        _fx.shadowMode = ShadowMode.Outline8;
        _fx.shadowDistance = new Vector2(1f, -1f);
        _fx.shadowIteration = 1;
        _fx.shadowFade = 1f;
        _fx.shadowColorFilter = ColorFilter.Replace;
        _fx.shadowColor = Color.white;
    }

    private static void ConfigureFilled(Image _image, Color32 _color)
    {
        _image.type = Image.Type.Filled;
        _image.fillMethod = Image.FillMethod.Horizontal;
        _image.fillOrigin = 0;
        _image.fillAmount = 1f;
        _image.color = _color;
    }

    private static void EnsureFolder(string _parent, string _name)
    {
        if (false == AssetDatabase.IsValidFolder(_parent + "/" + _name))
        {
            AssetDatabase.CreateFolder(_parent, _name);
        }
    }

    private static void SavePng(Texture2D _tex, string _path)
    {
        _tex.Apply();
        File.WriteAllBytes(_path, _tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(_tex);
    }

    private static void ImportAsSprite(string _path)
    {
        TextureImporter _imp = (TextureImporter)AssetImporter.GetAtPath(_path);
        _imp.textureType = TextureImporterType.Sprite;
        _imp.spriteImportMode = SpriteImportMode.Single;
        _imp.spritePixelsPerUnit = 32f;
        _imp.filterMode = FilterMode.Point;
        _imp.textureCompression = TextureImporterCompression.Uncompressed;
        _imp.mipmapEnabled = false;
        _imp.alphaIsTransparency = true;
        TextureImporterSettings _ts = new TextureImporterSettings();
        _imp.ReadTextureSettings(_ts);
        _ts.spriteMeshType = SpriteMeshType.FullRect;
        _ts.spriteAlignment = (int)SpriteAlignment.Center;
        _imp.SetTextureSettings(_ts);
        _imp.SaveAndReimport();
    }

    private static void SetRect(RectTransform _rt, Vector2 _pos, Vector2 _size, Vector2 _pivot)
    {
        _rt.anchorMin = Vector2.zero;
        _rt.anchorMax = Vector2.zero;
        _rt.pivot = _pivot;
        _rt.sizeDelta = _size;
        _rt.anchoredPosition = _pos;
    }

    private static RectTransform NewRect(string _name, Transform _parent, Vector2 _pos, Vector2 _size, Vector2 _pivot)
    {
        GameObject _go = new GameObject(_name, typeof(RectTransform));
        RectTransform _rt = (RectTransform)_go.transform;
        _rt.SetParent(_parent, false);
        SetRect(_rt, _pos, _size, _pivot);
        return _rt;
    }

    private static Image NewImage(string _name, Transform _parent, Sprite _sprite, Vector2 _pos, Vector2 _size)
    {
        RectTransform _rt = NewRect(_name, _parent, _pos, _size, Vector2.zero);
        Image _im = _rt.gameObject.AddComponent<Image>();
        _im.sprite = _sprite;
        _im.raycastTarget = false;
        return _im;
    }

    private static RawImage NewRaw(string _name, Transform _parent, Vector2 _pos, Vector2 _size)
    {
        RectTransform _rt = NewRect(_name, _parent, _pos, _size, Vector2.zero);
        RawImage _im = _rt.gameObject.AddComponent<RawImage>();
        _im.raycastTarget = false;
        return _im;
    }
}
#endif
