#if UNITY_EDITOR
using System.Collections.Generic;
using PresentationLayer.Environment;
using PresentationLayer.UISystem;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 전리품 성장 UI 에셋 구성기. 여러 번 실행해도 같은 결과로 맞춘다.
///  1) LootGrowthVisualData.asset 생성/갱신(단계별 아우라 색 + 임시 스프라이트 9개)
///  2) HUD_LootReveal.prefab에 성장 줄(TMP_Growth) 추가 + 참조 연결
///  3) LootPillar.prefab에 LootGrowthVisualData 연결
/// </summary>
public static class LootGrowthUiBuilder
{
    private const string DataPath = "Assets/Scriptable Obj/LootData/LootGrowthVisualData.asset";
    private const string DbPath = "Assets/Scriptable Obj/LootData/LootItemTypeDataBase.asset";
    private const string RevealPrefabPath = "Assets/Prefabs/UI/LootInfo/HUD_LootReveal.prefab";
    private const string PillarPrefabPath = "Assets/Prefabs/Objects/LootPillar/LootPillar.prefab";
    private const string SpriteAssetPath = "Assets/TextMesh Pro/Resources/Sprite Assets/LootGrowthIcons.asset";

    // 시안 A: 1단계 초록 5BE070, 2단계 하늘 58D7F2, 3단계 금 FFC94A (HDR 강도 2.0)
    private static readonly Color[] TierColorsSrgb =
    {
        new Color(0x5B / 255f, 0xE0 / 255f, 0x70 / 255f),
        new Color(0x58 / 255f, 0xD7 / 255f, 0xF2 / 255f),
        new Color(0xFF / 255f, 0xC9 / 255f, 0x4A / 255f),
    };

    private const float AuraIntensity = 2f;

    private static readonly LootType[] GrowthLoots = { LootType.LostAndFoundBox, LootType.SporePotion, LootType.StarCompass };

    [MenuItem("LumberBoy/UI/Build Loot Growth UI")]
    public static void Build()
    {
        LootGrowthVisualData _data = BuildVisualData();
        BuildRevealPrefab(_data);
        BuildPillarPrefab(_data);

        AssetDatabase.SaveAssets();
        Debug.Log("[LootGrowthUiBuilder] 완료: LootGrowthVisualData / HUD_LootReveal(성장 줄) / LootPillar 연결. 임시 스프라이트 "
            + _data.PlaceholderCount + "개");
    }

    private static LootGrowthVisualData BuildVisualData()
    {
        LootGrowthVisualData _data = AssetDatabase.LoadAssetAtPath<LootGrowthVisualData>(DataPath);

        if (null == _data)
        {
            _data = ScriptableObject.CreateInstance<LootGrowthVisualData>();
            AssetDatabase.CreateAsset(_data, DataPath);
        }

        Color[] _colors = new Color[LootGrowthVisualData.MaxTier];

        for (int i = 0; i < _colors.Length; i++)
        {
            Color _c = TierColorsSrgb[i] * AuraIntensity;
            _c.a = 1f;
            _colors[i] = _c;
        }

        LootItemTypeDataBase _db = AssetDatabase.LoadAssetAtPath<LootItemTypeDataBase>(DbPath);
        List<LootGrowthVisualData.TierSprite> _sprites = new List<LootGrowthVisualData.TierSprite>(9);

        for (int l = 0; l < GrowthLoots.Length; l++)
        {
            LootItemTypeData _item = null != _db ? _db.Get(GrowthLoots[l]) : null;
            Sprite _base = null != _item ? _item.sprite : null;

            for (int t = 1; t <= LootGrowthVisualData.MaxTier; t++)
            {
                LootGrowthVisualData.TierSprite _entry = new LootGrowthVisualData.TierSprite();
                _entry.lootType = GrowthLoots[l];
                _entry.tier = t;
                _entry.sprite = _base;
                _entry.isPlaceholder = true; // 씨앗/꽃/나무 정식 아트 전까지 임시
                _sprites.Add(_entry);
            }
        }

        _data.SetEditorData(_colors, _sprites);
        EditorUtility.SetDirty(_data);
        return _data;
    }

    // 이름/설명(TextField)은 기존 값 그대로 두고, 성장 줄만 DimBG 아래쪽에 별도 마스크(GrowthMask)로 둔다.
    // TextField의 위치/크기/이름/설명 위치는 원래 값(80,-5 / 360x100 / 35 / -15)이며, 여기서도 그 값으로 맞춘다.
    private static readonly Vector2 TextFieldPos = new Vector2(80f, -5f + TextShiftUp);
    private static readonly Vector2 TextFieldSize = new Vector2(360f, 100f);
    private const float NameY = 35f;
    private const float DescY = -15f;

    // 띠(DimBG/DissolveBG)는 120 -> 132(중심 0, 66 ~ -66)로 두고, 이름/설명/성장 줄 묶음(TextField, GrowthMask)을
    // TextShiftUp만큼 위로 올려 위 여백을 줄이고 성장 줄 아래 여백을 확보한다. 기둥(IMG_Pillar)과 코드가 쓰는
    // 절대 좌표(pillarStartY/pillarTargetY)는 원래 값 그대로다.
    private const float BandHeight = 132f;
    private const float TextShiftUp = 10f;
    private const float GrowthMaskY = -58f + TextShiftUp;
    private const float GrowthLineHeight = 16f;
    private const float PillarRestY = -5f;
    private const float PillarStartY = -110f;
    private const float PillarTargetY = -5f;

    private static void BuildRevealPrefab(LootGrowthVisualData _data)
    {
        GameObject _root = PrefabUtility.LoadPrefabContents(RevealPrefabPath);

        try
        {
            Transform _dim = _root.transform.Find("DimBG");
            Transform _field = null != _dim ? _dim.Find("TextField") : null;
            Transform _desc = null != _field ? _field.Find("TMP_Description") : null;
            Transform _name = null != _field ? _field.Find("TMP_Name") : null;

            if (null == _dim || null == _field || null == _desc || null == _name)
            {
                Debug.LogError("[LootGrowthUiBuilder] HUD_LootReveal 계층(DimBG/TextField/TMP_Name, TMP_Description)을 찾지 못했습니다.");
                return;
            }

            // 0) 띠 두께: DimBG(마스크)와 DissolveBG를 같은 높이로 맞춘다(중심은 그대로)
            RectTransform _dimRt = (RectTransform)_dim;
            _dimRt.sizeDelta = new Vector2(_dimRt.sizeDelta.x, BandHeight);
            _dimRt.anchoredPosition = new Vector2(_dimRt.anchoredPosition.x, 0f);

            Transform _pillar = _dim.Find("IMG_Pillar");

            if (null != _pillar)
            {
                RectTransform _pillarRt = (RectTransform)_pillar;
                _pillarRt.anchoredPosition = new Vector2(_pillarRt.anchoredPosition.x, PillarRestY);
            }

            Transform _dissolve = _root.transform.Find("DissolveBG");

            if (null != _dissolve)
            {
                RectTransform _dissolveRt = (RectTransform)_dissolve;
                _dissolveRt.sizeDelta = new Vector2(_dissolveRt.sizeDelta.x, BandHeight);
                _dissolveRt.anchoredPosition = new Vector2(_dissolveRt.anchoredPosition.x, 0f);
            }

            // 1) 이름/설명 영역을 원래 값으로 복원
            RectTransform _fieldRt = (RectTransform)_field;
            _fieldRt.anchoredPosition = TextFieldPos;
            _fieldRt.sizeDelta = TextFieldSize;
            ((RectTransform)_name).anchoredPosition = new Vector2(0f, NameY);
            ((RectTransform)_desc).anchoredPosition = new Vector2(0f, DescY);

            // 2) 성장 줄 마스크(TextField와 같은 가로 위치/폭, DimBG 아래쪽)
            Transform _maskTf = _dim.Find("GrowthMask");

            if (null == _maskTf)
            {
                GameObject _maskGo = new GameObject("GrowthMask", typeof(RectTransform), typeof(UnityEngine.UI.RectMask2D));
                _maskGo.layer = _dim.gameObject.layer;
                _maskTf = _maskGo.transform;
                _maskTf.SetParent(_dim, false);
                _maskTf.SetSiblingIndex(_field.GetSiblingIndex() + 1);
            }

            RectTransform _maskRt = (RectTransform)_maskTf;
            _maskRt.anchorMin = new Vector2(0.5f, 0.5f);
            _maskRt.anchorMax = new Vector2(0.5f, 0.5f);
            _maskRt.pivot = new Vector2(0.5f, 0.5f);
            _maskRt.sizeDelta = new Vector2(TextFieldSize.x, GrowthLineHeight);
            _maskRt.anchoredPosition = new Vector2(TextFieldPos.x, GrowthMaskY);

            // 3) 성장 텍스트: 예전에 TextField 아래에 만들어 둔 것이 있으면 옮기고, 없으면 설명 복제
            Transform _growth = _maskTf.Find("TMP_Growth");

            if (null == _growth)
            {
                Transform _old = _field.Find("TMP_Growth");

                if (null != _old)
                {
                    _old.SetParent(_maskTf, false);
                    _growth = _old;
                }
                else
                {
                    GameObject _go = Object.Instantiate(_desc.gameObject, _maskTf);
                    _go.name = "TMP_Growth";
                    _growth = _go.transform;
                }
            }

            RectTransform _growthRt = (RectTransform)_growth;
            _growthRt.anchorMin = new Vector2(0f, 0.5f);
            _growthRt.anchorMax = new Vector2(0f, 0.5f);
            _growthRt.pivot = new Vector2(0f, 0.5f);
            _growthRt.anchoredPosition = Vector2.zero;
            _growthRt.sizeDelta = new Vector2(TextFieldSize.x, GrowthLineHeight);

            TMP_Text _text = _growth.GetComponent<TMP_Text>();
            _text.text = string.Empty;
            _text.spriteAsset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(SpriteAssetPath);

            // 복제된 애니메이터의 tmpText 참조가 원본(설명)을 가리키지 않게 자기 자신으로 다시 건다.
            TMPInlineStyleAnimator _anim = _growth.GetComponent<TMPInlineStyleAnimator>();

            if (null != _anim)
            {
                SerializedObject _aso = new SerializedObject(_anim);
                _aso.FindProperty("tmpText").objectReferenceValue = _text;
                _aso.ApplyModifiedPropertiesWithoutUndo();
            }

            HUD_LootReveal _reveal = _root.GetComponent<HUD_LootReveal>();
            SerializedObject _so = new SerializedObject(_reveal);
            _so.FindProperty("lootGrowthText").objectReferenceValue = _text;
            _so.FindProperty("growthVisualData").objectReferenceValue = _data;
            _so.FindProperty("pillarStartY").floatValue = PillarStartY;
            _so.FindProperty("pillarTargetY").floatValue = PillarTargetY;
            _so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(_root, RevealPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(_root);
        }
    }

    private static void BuildPillarPrefab(LootGrowthVisualData _data)
    {
        GameObject _root = PrefabUtility.LoadPrefabContents(PillarPrefabPath);

        try
        {
            LootDisplayObject _display = _root.GetComponentInChildren<LootDisplayObject>(true);

            if (null == _display)
            {
                Debug.LogError("[LootGrowthUiBuilder] LootPillar.prefab에서 LootDisplayObject를 찾지 못했습니다.");
                return;
            }

            SerializedObject _so = new SerializedObject(_display);
            SerializedProperty _prop = _so.FindProperty("growthVisualData");

            // 이미 연결돼 있으면 다시 저장하지 않는다. 프리팹을 다시 저장하면 정렬 레이어 인덱스(m_SortingLayer)가
            // 정규화되어 이번 작업과 무관한 diff가 생긴다.
            if (_data == _prop.objectReferenceValue)
            {
                return;
            }

            _prop.objectReferenceValue = _data;
            _so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(_root, PillarPrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(_root);
        }
    }
}
#endif
