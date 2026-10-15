#if UNITY_EDITOR
using Coffee.UIEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 발 밑 대시 게이지 프리팹 생성기. Chara 스프라이트(Base/Fill)로 UI_DashCharaGauge.prefab을 만들고
/// UnitUI.prefab의 UIView_Unit.dashGaugePrefab에 연결한다. 여러 번 실행해도 같은 결과로 덮어쓴다.
/// </summary>
public static class UI_DashCharaGaugeBuilder
{
    private const string BaseSpritePath = "Assets/Graphics/HUD/Dash/Dash_HUD_BaseChara.png";
    private const string FillSpritePath = "Assets/Graphics/HUD/Dash/Dash_HUD_FillChara.png";
    private const string PrefabDir = "Assets/Prefabs/UI/Unit/DashCharaGauge";
    private const string PrefabPath = PrefabDir + "/UI_DashCharaGauge.prefab";
    private const string UnitUiPrefabPath = "Assets/Prefabs/UI/Unit/UnitUI.prefab";

    [MenuItem("LumberBoy/HUD/Build Dash Chara Gauge Prefab")]
    public static void Build()
    {
        Sprite _base = AssetDatabase.LoadAssetAtPath<Sprite>(BaseSpritePath);
        Sprite _fill = AssetDatabase.LoadAssetAtPath<Sprite>(FillSpritePath);

        if (null == _base || null == _fill)
        {
            Debug.LogError("[UI_DashCharaGaugeBuilder] Chara 스프라이트를 찾지 못했습니다.");
            return;
        }

        if (false == AssetDatabase.IsValidFolder(PrefabDir))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs/UI/Unit", "DashCharaGauge");
        }

        GameObject _root = new GameObject("UI_DashCharaGauge", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform _rootRt = (RectTransform)_root.transform;
        _rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        _rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        _rootRt.pivot = new Vector2(0.5f, 0.5f);
        _rootRt.sizeDelta = new Vector2(24f, 24f);
        CanvasGroup _group = _root.GetComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;
        UI_DashCharaGauge _gauge = _root.AddComponent<UI_DashCharaGauge>();

        RectTransform[] _cells = new RectTransform[UI_DashCharaGauge.MaxCells];
        Image[] _fills = new Image[UI_DashCharaGauge.MaxCells];
        Image[] _flashes = new Image[UI_DashCharaGauge.MaxCells];

        for (int i = 0; i < UI_DashCharaGauge.MaxCells; i++)
        {
            RectTransform _cell = NewRect("Cell_" + i, _rootRt, Vector2.zero, new Vector2(24f, 24f));
            NewImage("Base", _cell, _base);
            Image _fillImg = NewImage("Fill", _cell, _fill);
            ConfigureFilled(_fillImg);
            Image _flashImg = NewImage("Flash", _cell, _fill);
            ConfigureFilled(_flashImg);
            UIEffect _fx = _flashImg.gameObject.AddComponent<UIEffect>();
            _fx.colorFilter = ColorFilter.Replace;
            _fx.color = Color.white;
            _flashImg.enabled = false;

            _cells[i] = _cell;
            _fills[i] = _fillImg;
            _flashes[i] = _flashImg;
        }

        SerializedObject _so = new SerializedObject(_gauge);
        _so.FindProperty("canvasGroup").objectReferenceValue = _group;
        SerializedProperty _cp = _so.FindProperty("cellRects");
        SerializedProperty _fp = _so.FindProperty("fillImages");
        SerializedProperty _lp = _so.FindProperty("flashImages");
        _cp.arraySize = UI_DashCharaGauge.MaxCells;
        _fp.arraySize = UI_DashCharaGauge.MaxCells;
        _lp.arraySize = UI_DashCharaGauge.MaxCells;

        for (int i = 0; i < UI_DashCharaGauge.MaxCells; i++)
        {
            _cp.GetArrayElementAtIndex(i).objectReferenceValue = _cells[i];
            _fp.GetArrayElementAtIndex(i).objectReferenceValue = _fills[i];
            _lp.GetArrayElementAtIndex(i).objectReferenceValue = _flashes[i];
        }

        _so.ApplyModifiedPropertiesWithoutUndo();
        GameObject _prefab = PrefabUtility.SaveAsPrefabAsset(_root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(_root);

        // UnitUI.prefab의 UIView_Unit에 연결
        GameObject _unitUi = PrefabUtility.LoadPrefabContents(UnitUiPrefabPath);

        try
        {
            UIView_Unit _view = _unitUi.GetComponentInChildren<UIView_Unit>(true);

            if (null != _view)
            {
                SerializedObject _vso = new SerializedObject(_view);
                _vso.FindProperty("dashGaugePrefab").objectReferenceValue = _prefab;
                _vso.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(_unitUi, UnitUiPrefabPath);
            }
            else
            {
                Debug.LogError("[UI_DashCharaGaugeBuilder] UnitUI.prefab에서 UIView_Unit을 찾지 못했습니다.");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(_unitUi);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[UI_DashCharaGaugeBuilder] 완료: " + PrefabPath + " 생성, UnitUI.prefab에 연결");
    }

    private static void ConfigureFilled(Image _image)
    {
        _image.type = Image.Type.Filled;
        _image.fillMethod = Image.FillMethod.Horizontal;
        _image.fillOrigin = 0;
        _image.fillAmount = 1f;
    }

    private static RectTransform NewRect(string _name, Transform _parent, Vector2 _pos, Vector2 _size)
    {
        GameObject _go = new GameObject(_name, typeof(RectTransform));
        RectTransform _rt = (RectTransform)_go.transform;
        _rt.SetParent(_parent, false);
        _rt.anchorMin = new Vector2(0.5f, 0.5f);
        _rt.anchorMax = new Vector2(0.5f, 0.5f);
        _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.sizeDelta = _size;
        _rt.anchoredPosition = _pos;
        return _rt;
    }

    private static Image NewImage(string _name, Transform _parent, Sprite _sprite)
    {
        RectTransform _rt = NewRect(_name, _parent, Vector2.zero, new Vector2(24f, 24f));
        Image _im = _rt.gameObject.AddComponent<Image>();
        _im.sprite = _sprite;
        _im.raycastTarget = false;
        return _im;
    }
}
#endif
