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
    // 대시 게이지의 화면 왼쪽 아래 기준 위치(가방 오른쪽 끝 바로 옆, 코인 줄 바로 위; 스프라이트 하단 7px 여백 보정 포함)
    private static readonly Vector2 DashScreenPosition = new Vector2(90f, 23f);
    // 유성우(별빛 나침반) / 지진(흑요 부적) 구슬 HUD 규격 및 화면 왼쪽 아래 기준 위치(대시 게이지 바로 위 나란히)
    // 64x64 스프라이트, 1px = 1 UI 단위. 위치는 프리팹 왼쪽 아래(스프라이트 0,0) 기준.
    private const string MeteorBaseSpritePath = "Assets/Graphics/HUD/Bar/SeedBar/Stage03SeedHUD_Base.png";
    private const string MeteorFillSpritePath = "Assets/Graphics/HUD/Bar/SeedBar/Stage03SeedHUD_Fillbar.png";
    private const string MeteorFillMaterialPath = "Assets/Graphics/HUD/Buff/M_MeteorShowerFill.mat";
    private const string QuakeBaseSpritePath = "Assets/Graphics/HUD/Bar/SeedBar/Stage04SeedHUD_Base.png";
    private const string QuakeFillSpritePath = "Assets/Graphics/HUD/Bar/SeedBar/Stage04SeedHUD_Fillbar.png";
    private const float RelicGaugeSize = 64f;
    // 배치 기준(보이는 픽셀 기준): 유성우 구슬 왼쪽 끝 = 대시 왼쪽 끝 = 동전 아이콘 왼쪽 끝(x 90).
    // 간격은 모두 대시 아래 끝(y 30)과 동전 위 끝(y 23) 사이 빈 줄 수(6px)와 같게 - 대시 위 끝(y 40) ~ 유성우 구슬 아래 끝(y 47),
    // 유성우 구슬 오른쪽 끝(x 122) ~ 지진 구슬 왼쪽 끝(x 129). 두 구슬은 같은 높이(안쪽 원이 같은 줄)에 둔다.
    // 스프라이트 안 보이는 영역: 유성우 x 16~48 / y 17~48, 지진 x 16~47 / y 16~47, 대시 칸 x 0~22 / y 7~17, 동전 x 1~14 / y 1~14.
    private static readonly Vector2 MeteorShowerScreenPosition = new Vector2(74f, 30f);
    private static readonly Vector2 EarthquakeScreenPosition = new Vector2(113f, 30f);
    // 채움 색은 구슬 테두리의 강조색(유성우 별, 지진 보석)
    private static readonly Color32 MeteorFillColor = new Color32(154, 172, 227, 255);
    private static readonly Color32 QuakeFillColor = new Color32(225, 48, 31, 255);
    // 지진 쿨타임 음영 #000000, 알파 0.45
    private static readonly Color QuakeCooldownColor = new Color(0f, 0f, 0f, 0.45f);
    // HUD 그림자 규격(인벤토리 용량 게이지와 같다): 같은 스프라이트를 검정 반투명으로 왼쪽 아래에 깐다.
    // 칸이 위로 갈수록 오른쪽으로 기운 평행사변형이라 그림자가 오른쪽으로 치우쳐 보이므로 X는 2도트 더 왼쪽(-4)에 둔다.
    private static readonly Vector2 DashShadowOffset = new Vector2(-4f, -3f);
    private static readonly Color32 DashShadowColor = new Color32(0, 0, 0, 128);
    private const string DashBaseSpritePath = "Assets/Graphics/HUD/Dash/Dash_HUD_Base.png";
    private const string DashShadowTrimSpritePath = "Assets/Graphics/HUD/Dash/Dash_HUD_ShadowTrim.png";
    private const string DashFillSpritePath = "Assets/Graphics/HUD/Dash/Dash_HUD_Fill.png";
    private const string NumberSheetPath = "Assets/Graphics/Font/SmallCurrencyNum.png";
    private const string IconDir = "Assets/Graphics/HUD/Buff/BuffIcon";
    private const string IconTablePath = "Assets/Prefabs/UI/HUD/Buff/HUD_BuffIconTable.asset";

    // 버프 아이콘 테이블(id -> Buff_{enum 이름}.png)을 만들거나 같은 규칙으로 다시 채운다. 프리팹은 건드리지 않는다.
    [MenuItem("LumberBoy/HUD/Build Buff Icon Table")]
    public static HUD_BuffIconTable BuildIconTable()
    {
        EnsureFolder("Assets/Prefabs/UI/HUD", "Buff");

        HUD_BuffIconTable _table = AssetDatabase.LoadAssetAtPath<HUD_BuffIconTable>(IconTablePath);

        if (null == _table)
        {
            _table = ScriptableObject.CreateInstance<HUD_BuffIconTable>();
            AssetDatabase.CreateAsset(_table, IconTablePath);
        }

        EBuffHudId[] _ids = (EBuffHudId[])System.Enum.GetValues(typeof(EBuffHudId));
        System.Collections.Generic.List<HUD_BuffIconTable.Entry> _entries = new System.Collections.Generic.List<HUD_BuffIconTable.Entry>(_ids.Length);

        for (int i = 0; i < _ids.Length; i++)
        {
            if (EBuffHudId.None == _ids[i])
            {
                continue;
            }

            string _path = IconDir + "/Buff_" + _ids[i] + ".png";
            Sprite _sprite = AssetDatabase.LoadAssetAtPath<Sprite>(_path);

            if (null == _sprite)
            {
                Debug.LogWarning("[HUD_BuffPrefabBuilder] 버프 아이콘을 찾지 못했습니다: " + _path);
                continue;
            }

            HUD_BuffIconTable.Entry _entry = new HUD_BuffIconTable.Entry();
            _entry.id = _ids[i];
            _entry.icon = _sprite;
            _entries.Add(_entry);
        }

        _table.SetEntriesForEditor(_entries.ToArray());
        EditorUtility.SetDirty(_table);
        AssetDatabase.SaveAssets();
        return _table;
    }

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
        _bso.FindProperty("iconTable").objectReferenceValue = BuildIconTable();
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

        // ===== HUD_MeteorShowerGauge / HUD_EarthquakeGauge (대시 위 구슬 2개) =====
        BuildRelicGaugePrefabs(out GameObject _meteorPrefab, out GameObject _quakePrefab);

        // ===== HUD_StaminaGroup.prefab (스태미나 + 버프 + 대시 + 유성우/지진 조합) =====
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
        // 스태미나 바(가운데 기준 x -80, 폭 160) 안에서 8칸(18 + 20x7 = 158)이 좌우 1px씩 비도록 x -79,
        // 게이지 아래쪽이 바 위쪽에서 8px 위(바 y 10 + 높이 18 + 간격 8)
        _buffRt.anchoredPosition = new Vector2(-79f, 36f);

        GameObject _dashInst = (GameObject)PrefabUtility.InstantiatePrefab(_dashPrefab, _groupRt);
        RectTransform _dashRt = (RectTransform)_dashInst.transform;
        _dashRt.anchorMin = Vector2.zero;
        _dashRt.anchorMax = Vector2.zero;
        _dashRt.pivot = Vector2.zero;
        // 화면 왼쪽 아래 인벤토리(가방, UI_Inventory 팝업 뷰) 오른쪽에 고정. 인벤토리 열기/닫기 모션은 따라가지 않는다
        _dashRt.anchoredPosition = DashScreenPosition;

        GameObject _meteorInst = (GameObject)PrefabUtility.InstantiatePrefab(_meteorPrefab, _groupRt);
        PlaceRelicInstance((RectTransform)_meteorInst.transform, MeteorShowerScreenPosition);

        GameObject _quakeInst = (GameObject)PrefabUtility.InstantiatePrefab(_quakePrefab, _groupRt);
        PlaceRelicInstance((RectTransform)_quakeInst.transform, EarthquakeScreenPosition);

        SerializedObject _gso = new SerializedObject(_groupComp);
        _gso.FindProperty("dashGauge").objectReferenceValue = _dashInst.GetComponent<HUD_DashGauge>();
        _gso.FindProperty("meteorShowerGauge").objectReferenceValue = _meteorInst.GetComponent<HUD_RelicSkillGauge>();
        _gso.FindProperty("earthquakeGauge").objectReferenceValue = _quakeInst.GetComponent<HUD_RelicSkillGauge>();
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
        Sprite _shadowTrim = AssetDatabase.LoadAssetAtPath<Sprite>(DashShadowTrimSpritePath);
        Sprite _fill = AssetDatabase.LoadAssetAtPath<Sprite>(DashFillSpritePath);

        GameObject _go = new GameObject("HUD_DashGauge", typeof(RectTransform));
        RectTransform _rt = (RectTransform)_go.transform;
        SetRect(_rt, Vector2.zero, new Vector2(DashVisibleWidth, 24f), Vector2.zero);
        HUD_DashGauge _gauge = _go.AddComponent<HUD_DashGauge>();

        RectTransform _visual = NewRect("Visual", _rt, new Vector2(DashVisibleWidth * 0.5f, 12f), new Vector2(DashVisibleWidth, 24f), new Vector2(0.5f, 0.5f));
        RectTransform[] _cells = new RectTransform[HUD_DashGauge.MaxCells];
        Image[] _fills = new Image[HUD_DashGauge.MaxCells];
        Image[] _flashes = new Image[HUD_DashGauge.MaxCells];
        Image[] _shadows = new Image[HUD_DashGauge.MaxCells];

        // 그림자는 맞물린 칸 위에 겹쳐 그려지지 않도록 모든 칸보다 먼저(뒤에) 한 그룹으로 둔다
        RectTransform _shadowGroup = NewRect("Shadows", _visual, Vector2.zero, new Vector2(DashVisibleWidth, 24f), Vector2.zero);

        for (int i = 0; i < HUD_DashGauge.MaxCells; i++)
        {
            // 뒤 칸 그림자는 앞 칸 그림자와 겹치는 사선 줄을 뺀 스프라이트(반투명 이중 합성 방지)
            Sprite _shadowSprite = 0 == i ? _base : _shadowTrim;
            Image _shadowImg = NewImage("Shadow_" + i, _shadowGroup, _shadowSprite, new Vector2(DashCellPitch * i, 0f) + DashShadowOffset, new Vector2(24f, 24f));
            _shadowImg.color = DashShadowColor;
            _shadowImg.raycastTarget = false;
            _shadows[i] = _shadowImg;
        }

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
        SerializedProperty _sp = _so.FindProperty("shadowImages");
        _cp.arraySize = HUD_DashGauge.MaxCells;
        _fp.arraySize = HUD_DashGauge.MaxCells;
        _lp.arraySize = HUD_DashGauge.MaxCells;
        _sp.arraySize = HUD_DashGauge.MaxCells;

        for (int i = 0; i < HUD_DashGauge.MaxCells; i++)
        {
            _cp.GetArrayElementAtIndex(i).objectReferenceValue = _cells[i];
            _fp.GetArrayElementAtIndex(i).objectReferenceValue = _fills[i];
            _lp.GetArrayElementAtIndex(i).objectReferenceValue = _flashes[i];
            _sp.GetArrayElementAtIndex(i).objectReferenceValue = _shadows[i];
        }

        _so.ApplyModifiedPropertiesWithoutUndo();
        GameObject _prefab = PrefabUtility.SaveAsPrefabAsset(_go, PrefabDir + "/HUD_DashGauge.prefab");
        UnityEngine.Object.DestroyImmediate(_go);
        return _prefab;
    }

    // 유성우/지진 구슬 프리팹 두 개를 만든다. 대시 HUD 프리팹·스태미나 조합 프리팹은 건드리지 않는다.
    private static void BuildRelicGaugePrefabs(out GameObject _meteorPrefab, out GameObject _quakePrefab)
    {
        _meteorPrefab = BuildRelicGaugePrefab("HUD_MeteorShowerGauge",
            AssetDatabase.LoadAssetAtPath<Sprite>(MeteorBaseSpritePath),
            AssetDatabase.LoadAssetAtPath<Sprite>(MeteorFillSpritePath),
            MeteorFillColor, false, AssetDatabase.LoadAssetAtPath<Material>(MeteorFillMaterialPath));
        _quakePrefab = BuildRelicGaugePrefab("HUD_EarthquakeGauge",
            AssetDatabase.LoadAssetAtPath<Sprite>(QuakeBaseSpritePath),
            AssetDatabase.LoadAssetAtPath<Sprite>(QuakeFillSpritePath),
            QuakeFillColor, true);
    }

    private static void PlaceRelicInstance(RectTransform _rt, Vector2 _pos)
    {
        _rt.anchorMin = Vector2.zero;
        _rt.anchorMax = Vector2.zero;
        _rt.pivot = Vector2.zero;
        _rt.sizeDelta = new Vector2(RelicGaugeSize, RelicGaugeSize);
        _rt.anchoredPosition = _pos;
    }

    // 구슬 두 개만 다시 만들고, 스태미나 조합 프리팹 안의 두 인스턴스 위치만 맞춘다(다른 HUD 프리팹은 다시 만들지 않음).
    [MenuItem("LumberBoy/HUD/Build Relic Skill Gauges (유성우·지진 구슬만)")]
    public static void BuildRelicGaugesOnly()
    {
        BuildRelicGaugePrefabs(out GameObject _meteorPrefab, out GameObject _quakePrefab);
        string _groupPath = PrefabDir + "/HUD_StaminaGroup.prefab";
        GameObject _group = PrefabUtility.LoadPrefabContents(_groupPath);

        try
        {
            HUD_StaminaGroup _groupComp = _group.GetComponent<HUD_StaminaGroup>();
            SerializedObject _gso = new SerializedObject(_groupComp);
            HUD_RelicSkillGauge _meteor = _gso.FindProperty("meteorShowerGauge").objectReferenceValue as HUD_RelicSkillGauge;
            HUD_RelicSkillGauge _quake = _gso.FindProperty("earthquakeGauge").objectReferenceValue as HUD_RelicSkillGauge;

            if (null == _meteor || null == _quake)
            {
                Debug.LogError("[HUD_BuffPrefabBuilder] HUD_StaminaGroup에 유성우/지진 게이지 참조가 없습니다. 전체 빌드(Build Buff HUD Prefabs)를 쓰세요.");
                return;
            }

            PlaceRelicInstance((RectTransform)_meteor.transform, MeteorShowerScreenPosition);
            PlaceRelicInstance((RectTransform)_quake.transform, EarthquakeScreenPosition);
            PrefabUtility.SaveAsPrefabAsset(_group, _groupPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(_group);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[HUD_BuffPrefabBuilder] 완료: HUD_MeteorShowerGauge / HUD_EarthquakeGauge 재생성, HUD_StaminaGroup 배치 갱신");
    }

    // 구성(그리는 순서): 그림자(테두리 실루엣) -> 테두리 -> 채움(아래에서 위로) -> 쿨타임 음영 -> 섬광.
    // 그림자는 대시 게이지와 같은 오프셋·색. 테두리·채움·음영은 64x64 원본 그대로 겹친다.
    private static GameObject BuildRelicGaugePrefab(string _name, Sprite _base, Sprite _fill, Color32 _fillColor,
        bool _bCooldown, Material _meteorFillMaterial = null)
    {
        GameObject _go = new GameObject(_name, typeof(RectTransform));
        RectTransform _rt = (RectTransform)_go.transform;
        SetRect(_rt, Vector2.zero, new Vector2(RelicGaugeSize, RelicGaugeSize), Vector2.zero);
        HUD_RelicSkillGauge _gauge = _go.AddComponent<HUD_RelicSkillGauge>();

        float _half = RelicGaugeSize * 0.5f;
        Vector2 _size = new Vector2(RelicGaugeSize, RelicGaugeSize);
        RectTransform _visual = NewRect("Visual", _rt, new Vector2(_half, _half), _size, new Vector2(0.5f, 0.5f));

        Image _shadowImg = NewImage("Shadow", _visual, _base, DashShadowOffset, _size);
        _shadowImg.color = DashShadowColor;

        NewImage("Base", _visual, _base, Vector2.zero, _size);

        Image _fillImg = NewImage("Fill", _visual, _fill, Vector2.zero, _size);
        ConfigureVerticalFill(_fillImg);
        _fillImg.fillAmount = 0f;
        _fillImg.color = _fillColor;

        Image _cooldownImg = null;

        if (true == _bCooldown)
        {
            _cooldownImg = NewImage("CooldownOverlay", _visual, _fill, Vector2.zero, _size);
            ConfigureVerticalFill(_cooldownImg);
            _cooldownImg.fillAmount = 0f;
            _cooldownImg.color = QuakeCooldownColor;
            _cooldownImg.enabled = false;
        }

        Image _flashImg = NewImage("Flash", _visual, _fill, Vector2.zero, _size);
        _flashImg.color = Color.white;
        _flashImg.enabled = false;

        SerializedObject _so = new SerializedObject(_gauge);
        _so.FindProperty("visualRoot").objectReferenceValue = _visual;
        _so.FindProperty("fillImage").objectReferenceValue = _fillImg;
        _so.FindProperty("meteorFillMaterial").objectReferenceValue = _meteorFillMaterial;
        _so.FindProperty("cooldownImage").objectReferenceValue = _cooldownImg;
        _so.FindProperty("flashImage").objectReferenceValue = _flashImg;
        _so.FindProperty("chargingColor").colorValue = _fillColor;
        _so.FindProperty("readyColor").colorValue = _fillColor;
        _so.FindProperty("activeDrainColor").colorValue = _fillColor;
        _so.ApplyModifiedPropertiesWithoutUndo();

        GameObject _prefab = PrefabUtility.SaveAsPrefabAsset(_go, PrefabDir + "/" + _name + ".prefab");
        UnityEngine.Object.DestroyImmediate(_go);
        return _prefab;
    }

    private static void ConfigureVerticalFill(Image _image)
    {
        _image.type = Image.Type.Filled;
        _image.fillMethod = Image.FillMethod.Vertical;
        _image.fillOrigin = (int)Image.OriginVertical.Bottom;
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
