#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>
/// 스프라이트 PNG 한 장(또는 여러 장 가로 배열 시트가 아닌 개별 파일 목록)에서 TMP Sprite Asset을 만드는 도구.
/// 지금은 전리품 성장 문구용 "꽃" 아이콘 하나(LootGrowthIcons)만 만든다. 사용법은 Docs/TMPSpriteAsset_Guide.md 참고.
/// 같은 이름의 에셋이 이미 있으면 내용을 다시 채워 덮어쓴다(참조를 유지한다).
/// </summary>
public static class TMPSpriteAssetBuilder
{
    private const string OutputDir = "Assets/TextMesh Pro/Resources/Sprite Assets";
    private const string AssetName = "LootGrowthIcons";
    private const string FlowerTexturePath = "Assets/Graphics/Icons/Currency/Icon_Flower.png";
    private const string FlowerSpriteName = "flower";
    private const int IconSize = 16;

    [MenuItem("LumberBoy/UI/Build LootGrowthIcons Sprite Asset")]
    public static void BuildLootGrowthIcons()
    {
        Texture2D _tex = AssetDatabase.LoadAssetAtPath<Texture2D>(FlowerTexturePath);

        if (null == _tex)
        {
            Debug.LogError("[TMPSpriteAssetBuilder] 텍스처를 찾지 못했습니다: " + FlowerTexturePath);
            return;
        }

        string _path = OutputDir + "/" + AssetName + ".asset";
        TMP_SpriteAsset _asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(_path);
        bool _bNew = null == _asset;

        if (true == _bNew)
        {
            _asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            AssetDatabase.CreateAsset(_asset, _path);
        }

        // 버전 문자열이 비어 있고 머티리얼이 있으면 TMP가 구버전 에셋으로 보고 업그레이드를 시도하다 NRE가 난다.
        // 읽기 전용 프로퍼티라 직렬화 필드로 먼저 채운다(테이블에 손대기 전에).
        SerializedObject _so = new SerializedObject(_asset);
        SerializedProperty _version = _so.FindProperty("m_Version");

        if (string.IsNullOrEmpty(_version.stringValue))
        {
            _version.stringValue = "1.1.0";
            _so.ApplyModifiedPropertiesWithoutUndo();
        }

        _asset.name = AssetName;
        _asset.spriteSheet = _tex;
        _asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(AssetName);

        // 머티리얼: TMP 기본 스프라이트 셰이더. 에셋의 서브 오브젝트로 둔다.
        if (null == _asset.material)
        {
            Shader _shader = Shader.Find("TextMeshPro/Sprite");

            if (null == _shader)
            {
                Debug.LogError("[TMPSpriteAssetBuilder] 'TextMeshPro/Sprite' 셰이더를 찾지 못했습니다.");
                return;
            }

            Material _mat = new Material(_shader);
            _mat.name = AssetName + " Material";
            AssetDatabase.AddObjectToAsset(_mat, _asset);
            _asset.material = _mat;
        }

        _asset.material.SetTexture(ShaderUtilities.ID_MainTex, _tex);

        // 글꼴 정보: pointSize = 아이콘 한 변. 텍스트 크기가 12면 아이콘이 12px로 보인다(크기는 <size>나 scale로 조절).
        FaceInfo _face = new FaceInfo();
        _face.familyName = AssetName;
        _face.styleName = "Regular";
        _face.pointSize = IconSize;
        _face.scale = 1f;
        _face.lineHeight = IconSize;
        _face.ascentLine = IconSize * 0.875f;
        _face.capLine = IconSize * 0.875f;
        _face.meanLine = IconSize * 0.5f;
        _face.baseline = 0f;
        _face.descentLine = -IconSize * 0.125f;
        _asset.faceInfo = _face;

        _asset.spriteGlyphTable.Clear();
        _asset.spriteCharacterTable.Clear();

        TMP_SpriteGlyph _glyph = new TMP_SpriteGlyph();
        _glyph.index = 0;
        _glyph.metrics = new GlyphMetrics(IconSize, IconSize, 0f, IconSize * 0.875f, IconSize);
        _glyph.glyphRect = new GlyphRect(0, 0, IconSize, IconSize);
        _glyph.scale = 1f;
        _glyph.atlasIndex = 0;
        _asset.spriteGlyphTable.Add(_glyph);

        TMP_SpriteCharacter _char = new TMP_SpriteCharacter(0xFFFE, _glyph);
        _char.name = FlowerSpriteName;
        _char.scale = 1f;
        _char.glyphIndex = 0;
        _asset.spriteCharacterTable.Add(_char);

        _asset.UpdateLookupTables();

        EditorUtility.SetDirty(_asset);
        EditorUtility.SetDirty(_asset.material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[TMPSpriteAssetBuilder] " + (_bNew ? "생성" : "갱신") + ": " + _path + " (sprite name=\"" + FlowerSpriteName + "\")");
    }
}
#endif
