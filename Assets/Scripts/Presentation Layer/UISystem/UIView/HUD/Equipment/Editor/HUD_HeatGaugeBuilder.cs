#if UNITY_EDITOR
using PresentationLayer.UISystem.UIView.HUD.Equipment;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 과열 게이지 HUD 구성기. HUD_Equipment.prefab 안(도끼 HUD 뒤, 왼쪽)에 게이지 계층을 만들고 참조를 연결한다.
/// 여러 번 실행해도 같은 결과로 맞춘다(기존 HUD_HeatGauge는 지우고 다시 만든다). 위치는 프리팹에서 바로 옮겨 맞춰도 된다.
///
///  HUD_HeatGauge (오른쪽 아래 앵커)
///   └ Visual                       숨김 동안 꺼짐
///      ├ Bar      (9x50, 아래 가운데 pivot)  슬라이드 · 바운스 대상
///      │  ├ HeatBar         HeatBar_Base + UI/HeatGaugeFill 머티리얼(테두리 · 달아오름 · 용암 채움 · 번쩍임)
///      │  ├ Sparks          21x80 불티(HUD_HeatSparks), 바 왼쪽 6px 밖부터
///      │  └ LevelNumber
///      ├ DamageRow (아이콘 + x배율)
///      └ SpeedRow  (아이콘 + x배율)
/// </summary>
public static class HUD_HeatGaugeBuilder
{
    private const string PrefabPath = "Assets/Prefabs/UI/HUD/Equipment/HUD_Equipment.prefab";
    private const string SpriteDir = "Assets/Graphics/HUD/Bar/HeatBar/";
    private const string NumberSheetPath = "Assets/Graphics/Font/SmallCurrencyNum.png";
    private const string RootName = "HUD_HeatGauge";
    private const string MaterialPath = SpriteDir + "M_HeatGaugeFill.mat";
    private const string ShaderName = "UI/HeatGaugeFill";

    // HUD_Equipment 오른쪽 아래 기준 px. 도끼 그림(HUD_Axe)은 x -76 ~ -12, y 7 ~ 77을 차지한다.
    private static readonly Vector2 BarPosition = new Vector2(-81.5f, 8f); // 바 아래 가운데(왼쪽 끝 -86)
    private static readonly Vector2 BarSize = new Vector2(9f, 50f);
    private static readonly Vector2 SparksPosition = new Vector2(-6f, 0f); // 바 왼쪽 아래 기준
    private static readonly Vector2 SparksSize = new Vector2(21f, 80f);
    private static readonly Vector2 LevelNumberPosition = new Vector2(4f, 56f); // 바 왼쪽 아래 기준, 글자 아래 끝이 바 위 2px
    private static readonly Vector2 RowSize = new Vector2(36f, 16f); // 아이콘(칠해진 영역 12px) · 2px · 배율(x1.00 21px)
    private static readonly Vector2 DamageRowPosition = new Vector2(-106f, 31f); // 줄 가운데(오른쪽 끝 -88)
    private static readonly Vector2 SpeedRowPosition = new Vector2(-106f, 17f);

    [MenuItem("LumberBoy/HUD/Build Heat Gauge HUD")]
    public static void Build()
    {
        Sprite _base = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "HeatBar_Base.png");
        Sprite _damageIcon = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "HeatBar_DamageIcon.png");
        Sprite _speedIcon = AssetDatabase.LoadAssetAtPath<Sprite>(SpriteDir + "HeatBar_AttackSpeedIcon.png");
        Sprite[] _glyphs = LoadGlyphSprites();

        if (null == _base || null == _damageIcon || null == _speedIcon || null == _glyphs)
        {
            Debug.LogError("[HUD_HeatGaugeBuilder] 스프라이트를 찾지 못했습니다: " + SpriteDir);
            return;
        }

        Material _material = LoadOrCreateMaterial();

        if (null == _material)
            return;

        GameObject _prefab = PrefabUtility.LoadPrefabContents(PrefabPath);

        try
        {
            HUD_Equipment _equipment = _prefab.GetComponent<HUD_Equipment>();

            if (null == _equipment)
            {
                Debug.LogError("[HUD_HeatGaugeBuilder] HUD_Equipment 컴포넌트를 찾지 못했습니다.");
                return;
            }

            Transform _old = _prefab.transform.Find(RootName);
            if (null != _old)
                UnityEngine.Object.DestroyImmediate(_old.gameObject);

            HUD_HeatGauge _gauge = BuildGauge(_prefab.transform, _base, _material, _damageIcon, _speedIcon, _glyphs);

            SerializedObject _eso = new SerializedObject(_equipment);
            _eso.FindProperty("heatGauge").objectReferenceValue = _gauge;
            _eso.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(_prefab, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(_prefab);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[HUD_HeatGaugeBuilder] 완료: " + PrefabPath);
    }

    // 머티리얼은 처음 한 번만 만든다(이후 인스펙터에서 조정한 팔레트 · 틱 값을 덮어쓰지 않는다).
    private static Material LoadOrCreateMaterial()
    {
        Material _material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (null != _material)
            return _material;

        Shader _shader = Shader.Find(ShaderName);

        if (null == _shader)
        {
            Debug.LogError("[HUD_HeatGaugeBuilder] 셰이더를 찾지 못했습니다: " + ShaderName);
            return null;
        }

        _material = new Material(_shader);
        AssetDatabase.CreateAsset(_material, MaterialPath);
        return _material;
    }

    // 시트 순서가 1~9, 0, '.', 'x' 이므로 _0~_8이 1~9, _9가 0, _10이 점, _11이 x
    private static Sprite[] LoadGlyphSprites()
    {
        UnityEngine.Object[] _all = AssetDatabase.LoadAllAssetsAtPath(NumberSheetPath);
        Sprite[] _glyphs = new Sprite[HUD_HeatNumber.GlyphCount];

        for (int i = 0; i < _all.Length; i++)
        {
            Sprite _sp = _all[i] as Sprite;

            if (null == _sp)
                continue;

            string _n = _sp.name;
            int _k = int.Parse(_n.Substring(_n.LastIndexOf('_') + 1));

            if (_k <= 8)
                _glyphs[_k + 1] = _sp;
            else if (9 == _k)
                _glyphs[0] = _sp;
            else if (10 == _k)
                _glyphs[HUD_HeatNumber.GlyphDot] = _sp;
            else if (11 == _k)
                _glyphs[HUD_HeatNumber.GlyphX] = _sp;
        }

        for (int i = 0; i < _glyphs.Length; i++)
        {
            if (null == _glyphs[i])
            {
                Debug.LogError("[HUD_HeatGaugeBuilder] 숫자 글리프를 찾지 못했습니다: " + i);
                return null;
            }
        }

        return _glyphs;
    }

    private static HUD_HeatGauge BuildGauge(Transform _parent, Sprite _base, Material _material, Sprite _damageIcon, Sprite _speedIcon, Sprite[] _glyphs)
    {
        RectTransform _root = NewRect(RootName, _parent, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);
        _root.SetAsFirstSibling(); // 도끼 HUD(과열 아우라 포함)보다 뒤에 그린다
        HUD_HeatGauge _gauge = _root.gameObject.AddComponent<HUD_HeatGauge>();

        RectTransform _visual = NewRect("Visual", _root, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, Vector2.zero);

        // 바
        RectTransform _bar = NewRect("Bar", _visual, new Vector2(0.5f, 0f), new Vector2(1f, 0f), BarPosition, BarSize);
        Image _fillImage = NewImage("HeatBar", _bar, _base, Vector2.zero, BarSize);
        _fillImage.material = _material;

        RectTransform _sparksRt = NewRect("Sparks", _bar, Vector2.zero, Vector2.zero, SparksPosition, SparksSize);
        RawImage _sparksRaw = _sparksRt.gameObject.AddComponent<RawImage>();
        _sparksRaw.raycastTarget = false;
        HUD_HeatSparks _sparks = _sparksRt.gameObject.AddComponent<HUD_HeatSparks>();
        SerializedObject _sparksSo = new SerializedObject(_sparks);
        _sparksSo.FindProperty("image").objectReferenceValue = _sparksRaw;
        _sparksSo.ApplyModifiedPropertiesWithoutUndo();

        HUD_HeatNumber _level = NewNumber("LevelNumber", _bar, new Vector2(0f, 0f), LevelNumberPosition, HUD_HeatNumber.EAlign.Center, _glyphs);

        // 배율 줄
        HUD_HeatNumber _damage;
        HUD_HeatNumber _speed;
        RectTransform _damageRow = NewRow("DamageRow", _visual, DamageRowPosition, _damageIcon, _glyphs, out _damage);
        RectTransform _speedRow = NewRow("SpeedRow", _visual, SpeedRowPosition, _speedIcon, _glyphs, out _speed);

        SerializedObject _so = new SerializedObject(_gauge);
        _so.FindProperty("visualRoot").objectReferenceValue = _visual.gameObject;
        _so.FindProperty("barGroup").objectReferenceValue = _bar;
        _so.FindProperty("damageRow").objectReferenceValue = _damageRow;
        _so.FindProperty("speedRow").objectReferenceValue = _speedRow;
        _so.FindProperty("fillImage").objectReferenceValue = _fillImage;
        _so.FindProperty("heatMaterial").objectReferenceValue = _material;
        _so.FindProperty("sparks").objectReferenceValue = _sparks;
        _so.FindProperty("levelNumber").objectReferenceValue = _level;
        _so.FindProperty("damageNumber").objectReferenceValue = _damage;
        _so.FindProperty("speedNumber").objectReferenceValue = _speed;
        _so.ApplyModifiedPropertiesWithoutUndo();

        return _gauge;
    }

    private static RectTransform NewRow(string _name, Transform _parent, Vector2 _pos, Sprite _icon, Sprite[] _glyphs, out HUD_HeatNumber _number)
    {
        RectTransform _row = NewRect(_name, _parent, new Vector2(0.5f, 0.5f), new Vector2(1f, 0f), _pos, RowSize);

        // 아이콘은 줄 왼쪽 끝, 배율은 줄 오른쪽 끝에 붙인다(자릿수가 늘면 왼쪽으로 자란다).
        float _half = RowSize.x * 0.5f;
        RectTransform _iconRt = NewRect("Icon", _row, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-_half, 0f), new Vector2(16f, 16f));
        Image _iconImage = _iconRt.gameObject.AddComponent<Image>();
        _iconImage.sprite = _icon;
        _iconImage.raycastTarget = false;

        _number = NewNumber("Value", _row, new Vector2(0.5f, 0.5f), new Vector2(_half, 0f), HUD_HeatNumber.EAlign.Right, _glyphs);
        return _row;
    }

    private static HUD_HeatNumber NewNumber(string _name, Transform _parent, Vector2 _anchor, Vector2 _pos, HUD_HeatNumber.EAlign _align, Sprite[] _glyphs)
    {
        RectTransform _rt = NewRect(_name, _parent, new Vector2(0.5f, 0.5f), _anchor, _pos, Vector2.zero);
        HUD_HeatNumber _number = _rt.gameObject.AddComponent<HUD_HeatNumber>();

        RectTransform _glyphRt = NewRect("Glyphs", _rt, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1f, 8f));
        RawImage _raw = _glyphRt.gameObject.AddComponent<RawImage>();
        _raw.raycastTarget = false;
        _raw.enabled = false;

        SerializedObject _so = new SerializedObject(_number);
        _so.FindProperty("image").objectReferenceValue = _raw;
        _so.FindProperty("align").enumValueIndex = (int)_align;
        SerializedProperty _gp = _so.FindProperty("glyphSprites");
        _gp.arraySize = _glyphs.Length;

        for (int i = 0; i < _glyphs.Length; i++)
        {
            _gp.GetArrayElementAtIndex(i).objectReferenceValue = _glyphs[i];
        }

        _so.ApplyModifiedPropertiesWithoutUndo();
        return _number;
    }

    private static RectTransform NewRect(string _name, Transform _parent, Vector2 _pivot, Vector2 _anchor, Vector2 _pos, Vector2 _size)
    {
        GameObject _go = new GameObject(_name, typeof(RectTransform));
        RectTransform _rt = (RectTransform)_go.transform;
        _rt.SetParent(_parent, false);
        _rt.anchorMin = _anchor;
        _rt.anchorMax = _anchor;
        _rt.pivot = _pivot;
        _rt.sizeDelta = _size;
        _rt.anchoredPosition = _pos;
        _go.layer = _parent.gameObject.layer;
        return _rt;
    }

    private static Image NewImage(string _name, Transform _parent, Sprite _sprite, Vector2 _pos, Vector2 _size)
    {
        RectTransform _rt = NewRect(_name, _parent, Vector2.zero, Vector2.zero, _pos, _size);
        Image _image = _rt.gameObject.AddComponent<Image>();
        _image.sprite = _sprite;
        _image.raycastTarget = false;
        return _image;
    }
}
#endif
