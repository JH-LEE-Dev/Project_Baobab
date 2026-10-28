#if UNITY_EDITOR
using Coffee.UIEffects;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 수리대(모루) 사용 횟수 월드 HUD 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) ToolKit.png를 픽셀 스프라이트(PPU 32, Point)로 임포트
///  2) HUD_RepairKitCount.prefab: 월드 캔버스(1/32 배율) + 아이콘(UIEffect Outline8 흰색) + 숫자(RawImage)
///  3) OffRoadVehicle.prefab: RepairBox 아래에 HUD를 붙이고 RepairBox.countHud에 연결(이미 붙어 있으면 위치는 그대로 둔다)
/// </summary>
public static class HUD_RepairKitCountBuilder
{
    private const string IconPath = "Assets/Graphics/HUD/Anvil/ToolKit.png";
    private const string NumberSheetPath = "Assets/Graphics/Font/SmallCurrencyNum.png";
    private const string PrefabDir = "Assets/Prefabs/UI/HUD/Anvil";
    private const string PrefabPath = PrefabDir + "/HUD_RepairKitCount.prefab";
    private const string VehiclePrefabPath = "Assets/Prefabs/Objects/OffRoadVehicle/OffRoadVehicle.prefab";

    private const float IconSize = 16f;
    private const float PixelsPerUnit = 32f;

    // RepairBox 로컬 기준 HUD 중심 위치. 모루 그림(중심 0, 0.4375, 폭 1) 오른쪽 위 옆. 프리팹에서 바로 옮겨 맞춰도 된다.
    private static readonly Vector3 HudLocalPosition = new Vector3(0.95f, 0.75f, 0f);

    [MenuItem("LumberBoy/HUD/Build Repair Kit Count HUD")]
    public static void Build()
    {
        if (null == AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath))
        {
            Debug.LogError("[HUD_RepairKitCountBuilder] 스프라이트를 찾지 못했습니다: " + IconPath);
            return;
        }

        ImportAsSprite(IconPath);
        Sprite _icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        Sprite[] _digits = LoadDigitSprites();

        if (null == _icon || null == _digits)
        {
            return;
        }

        GameObject _prefab = BuildPrefab(_icon, _digits);
        WireVehicle(_prefab);
        AssetDatabase.SaveAssets();
        Debug.Log("[HUD_RepairKitCountBuilder] 완료: " + PrefabPath + " / " + VehiclePrefabPath);
    }

    // 숫자 시트 순서가 1~9,0 이므로 _0~_8이 1~9, _9가 0 (HUD_BuffPrefabBuilder와 같은 규칙)
    private static Sprite[] LoadDigitSprites()
    {
        UnityEngine.Object[] _all =AssetDatabase.LoadAllAssetsAtPath(NumberSheetPath);
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
                Debug.LogError("[HUD_RepairKitCountBuilder] 숫자 스프라이트를 찾지 못했습니다: " + i);
                return null;
            }
        }

        return _digits;
    }

    private static GameObject BuildPrefab(Sprite _icon, Sprite[] _digits)
    {
        if (false == AssetDatabase.IsValidFolder(PrefabDir))
        {
            AssetDatabase.CreateFolder("Assets/Prefabs/UI/HUD", "Anvil");
        }

        // 루트: 월드 캔버스. 1px = 1/32 유닛이 되도록 배율을 맞춘다(WorldCanvas 프리팹과 같은 값).
        GameObject _root = new GameObject("HUD_RepairKitCount", typeof(RectTransform));
        RectTransform _rootRt = (RectTransform)_root.transform;
        SetRect(_rootRt, Vector2.zero, new Vector2(IconSize, IconSize), new Vector2(0.5f, 0.5f));
        _rootRt.localScale = new Vector3(1f / PixelsPerUnit, 1f / PixelsPerUnit, 1f);

        Canvas _canvas = _root.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.WorldSpace;
        WorldCanvasEnabler _enabler = _root.AddComponent<WorldCanvasEnabler>();
        HUD_RepairKitCount _hud = _root.AddComponent<HUD_RepairKitCount>();

        RectTransform _visual = NewRect("Visual", _rootRt, Vector2.zero, new Vector2(IconSize, IconSize), new Vector2(0.5f, 0.5f));
        _visual.anchorMin = new Vector2(0.5f, 0.5f);
        _visual.anchorMax = new Vector2(0.5f, 0.5f);
        _visual.anchoredPosition = Vector2.zero;

        Image _iconImg = NewImage("Icon", _visual, _icon, Vector2.zero, new Vector2(IconSize, IconSize));
        UIEffect _outlineFx = _iconImg.gameObject.AddComponent<UIEffect>();
        ConfigureOutline(_outlineFx);

        RawImage _numberRaw = NewRaw("Number", _visual, new Vector2(11f, -2f), new Vector2(8f, 8f));

        SerializedObject _so = new SerializedObject(_hud);
        _so.FindProperty("canvasEnabler").objectReferenceValue = _enabler;
        _so.FindProperty("visualRoot").objectReferenceValue = _visual;
        _so.FindProperty("numberImage").objectReferenceValue = _numberRaw;
        SerializedProperty _dp = _so.FindProperty("digitSprites");
        _dp.arraySize = 10;

        for (int i = 0; i < 10; i++)
        {
            _dp.GetArrayElementAtIndex(i).objectReferenceValue = _digits[i];
        }

        _so.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject _eso = new SerializedObject(_enabler);
        _eso.FindProperty("canvas").objectReferenceValue = _canvas;
        _eso.ApplyModifiedPropertiesWithoutUndo();

        GameObject _prefab = PrefabUtility.SaveAsPrefabAsset(_root, PrefabPath);
        UnityEngine.Object.DestroyImmediate(_root);
        return _prefab;
    }

    private static void WireVehicle(GameObject _hudPrefab)
    {
        GameObject _vehicle = PrefabUtility.LoadPrefabContents(VehiclePrefabPath);

        try
        {
            RepairBox _repairBox = _vehicle.GetComponentInChildren<RepairBox>(true);

            if (null == _repairBox)
            {
                Debug.LogError("[HUD_RepairKitCountBuilder] OffRoadVehicle에서 RepairBox를 찾지 못했습니다.");
                return;
            }

            SerializedObject _boxSo = new SerializedObject(_repairBox);
            SerializedProperty _hudProp = _boxSo.FindProperty("countHud");

            if (null == _hudProp.objectReferenceValue)
            {
                GameObject _inst = (GameObject)PrefabUtility.InstantiatePrefab(_hudPrefab, _repairBox.transform);
                _inst.transform.localPosition = HudLocalPosition;
                _hudProp.objectReferenceValue = _inst.GetComponent<HUD_RepairKitCount>();
                _boxSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(_vehicle, VehiclePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(_vehicle);
        }
    }

    // 버프 HUD 프레임과 같은 외곽선 설정(Outline8, 거리 1, 흰색)
    private static void ConfigureOutline(UIEffect _fx)
    {
        _fx.shadowMode = ShadowMode.Outline8;
        _fx.shadowDistance = new Vector2(1f, -1f);
        _fx.shadowIteration = 1;
        _fx.shadowFade = 1f;
        _fx.shadowColorFilter = ColorFilter.Replace;
        _fx.shadowColor = Color.white;
    }

    private static void ImportAsSprite(string _path)
    {
        TextureImporter _imp = (TextureImporter)AssetImporter.GetAtPath(_path);
        _imp.textureType = TextureImporterType.Sprite;
        _imp.spriteImportMode = SpriteImportMode.Single;
        _imp.spritePixelsPerUnit = PixelsPerUnit;
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
