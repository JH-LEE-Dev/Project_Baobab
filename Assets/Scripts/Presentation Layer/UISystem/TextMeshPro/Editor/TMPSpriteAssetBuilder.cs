#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>
/// 스프라이트 PNG에서 TMP Sprite Asset을 만드는 도구. 사용법은 Docs/TMPSpriteAsset_Guide.md 참고.
///  - LootGrowthIcons: 전리품 성장 문구용 "꽃" 아이콘 하나(개별 PNG).
///  - KeyIcons_*: 문구 속 키 아이콘. 키 아이콘 시트(슬라이스된 PNG) 한 장당 에셋 하나. 스프라이트 이름은 원본 그대로라
///    KeyIconDatabase가 돌려준 Sprite의 이름으로 바로 찾을 수 있다(TMPKeyIconFormatter 참고).
/// 같은 이름의 에셋이 이미 있으면 내용을 다시 채워 덮어쓴다(참조를 유지한다).
/// </summary>
public static class TMPSpriteAssetBuilder
{
    private const string OutputDir = "Assets/TextMesh Pro/Resources/Sprite Assets";
    private const string AssetName = "LootGrowthIcons";
    private const string FlowerTexturePath = "Assets/Graphics/Icons/Currency/Icon_Flower.png";
    private const string FlowerSpriteName = "flower";
    private const int IconSize = 16;

    // 키 아이콘 시트. 에셋 이름은 TMPKeyIconFormatter.SpriteAssetPrefix + 텍스처 이름이다(런타임이 같은 규칙으로 찾는다).
    private static readonly string[] KeyIconTexturePaths =
    {
        "Assets/Graphics/HUD/Keyboard/kb_light_all.png",
        "Assets/Graphics/HUD/Keyboard/mouse.png",
        "Assets/Graphics/HUD/GamePad/tilemap.png",
    };

    // 외곽선을 입힌 키 아이콘 시트 복사본을 저장하는 곳. 원본 시트는 건드리지 않는다.
    private const string KeyIconOutlinedDir = "Assets/Graphics/HUD/KeyIconsOutlined";
    private const int OutlineWidth = 1;
    private static readonly Color32 KeyIconOutlineColor = new Color32(255, 255, 255, 255);

    // 키 아이콘 표시 배율. TMP는 글리프를 (글자 크기 / IconSize)배로 그리므로, 설명 글자 크기 12에서 원본 픽셀 1:1이 되도록
    // 16/12를 곱한다. 아이콘이 쓰이는 글자 크기가 바뀌면 이 값도 맞춰 바꾼다.
    private const float KeyIconDisplayScale = 16f / 12f;
    // 기준선 아래로 내려가는 픽셀 수(글리프 단위). 원본 기준 2px + 아래 외곽선 1px.
    private const float KeyIconDescent = IconSize * 0.125f + OutlineWidth;

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
        TMP_SpriteAsset _asset = PrepareSpriteAsset(AssetName, _path, _tex, out bool _bNew);

        if (null == _asset)
            return;

        AddGlyph(_asset, 0, new Rect(0f, 0f, IconSize, IconSize), FlowerSpriteName);

        SaveSpriteAsset(_asset);

        Debug.Log("[TMPSpriteAssetBuilder] " + (_bNew ? "생성" : "갱신") + ": " + _path + " (sprite name=\"" + FlowerSpriteName + "\")");
    }

    [MenuItem("LumberBoy/UI/Build KeyIcons Sprite Assets")]
    public static void BuildKeyIcons()
    {
        List<Sprite> _sprites = new List<Sprite>(128);
        List<Rect> _rects = new List<Rect>(128);
        HashSet<string> _names = new HashSet<string>();

        for (int i = 0; i < KeyIconTexturePaths.Length; i++)
        {
            string _texPath = KeyIconTexturePaths[i];
            Texture2D _tex = AssetDatabase.LoadAssetAtPath<Texture2D>(_texPath);

            if (null == _tex)
            {
                Debug.LogError("[TMPSpriteAssetBuilder] 텍스처를 찾지 못했습니다: " + _texPath);
                continue;
            }

            // 시트에서 잘린 스프라이트 전부. 서브 에셋 순서는 보장되지 않지만 이름으로 찾으므로 상관없다.
            _sprites.Clear();
            _names.Clear();
            UnityEngine.Object[] _subAssets = AssetDatabase.LoadAllAssetsAtPath(_texPath);

            for (int j = 0; j < _subAssets.Length; j++)
            {
                if (_subAssets[j] is Sprite _sprite)
                {
                    // 이름이 겹치면 TMP가 먼저 등록된 쪽만 찾는다. 조용히 엉뚱한 키가 뜨지 않도록 알리고 건너뛴다.
                    if (false == _names.Add(_sprite.name))
                    {
                        Debug.LogWarning("[TMPSpriteAssetBuilder] 같은 이름의 스프라이트가 있어 건너뜁니다: " + _texPath + " / " + _sprite.name);
                        continue;
                    }

                    _sprites.Add(_sprite);
                }
            }

            // 원본 시트는 그대로 두고, 흰 외곽선을 입힌 복사본 시트를 따로 만들어 쓴다(옵션 화면 등 기존 키 아이콘에는 영향 없음).
            Texture2D _outlinedTex = BuildOutlinedSheet(_texPath, _tex.name, _sprites, _rects);

            if (null == _outlinedTex)
                continue;

            // 에셋 이름은 원본 텍스처 이름 기준이다. TMPKeyIconFormatter가 KeyIconDatabase의 (원본) Sprite로 에셋을 찾는다.
            string _assetName = TMPKeyIconFormatter.SpriteAssetPrefix + _tex.name;
            string _path = OutputDir + "/" + _assetName + ".asset";
            TMP_SpriteAsset _asset = PrepareSpriteAsset(_assetName, _path, _outlinedTex, out bool _bNew);

            if (null == _asset)
                continue;

            for (int j = 0; j < _sprites.Count; j++)
            {
                AddGlyph(_asset, (uint)j, _rects[j], _sprites[j].name, KeyIconDescent, KeyIconDisplayScale);
            }

            SaveSpriteAsset(_asset);

            Debug.Log("[TMPSpriteAssetBuilder] " + (_bNew ? "생성" : "갱신") + ": " + _path + " (스프라이트 " + _sprites.Count + "개)");
        }
    }

    // 원본 시트의 스프라이트를 사방 OutlineWidth만큼 여백을 둔 칸으로 다시 늘어놓고, 불투명 픽셀 바깥 8방향에
    // KeyIconOutlineColor 외곽선을 칠한 시트를 KeyIconOutlinedDir에 PNG로 저장한다. _rects에는 새 시트에서의 각 칸 영역이 담긴다.
    private static Texture2D BuildOutlinedSheet(string _srcPath, string _texName, List<Sprite> _sprites, List<Rect> _rects)
    {
        _rects.Clear();

        // 원본은 읽기 불가 설정이라 파일에서 직접 읽는다. LoadImage도 아래쪽이 y=0이라 Sprite.rect와 좌표가 같다.
        Texture2D _src = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (false == _src.LoadImage(File.ReadAllBytes(_srcPath)))
        {
            Debug.LogError("[TMPSpriteAssetBuilder] 원본 PNG를 읽지 못했습니다: " + _srcPath);
            UnityEngine.Object.DestroyImmediate(_src);
            return null;
        }

        // 칸 배치: 한 줄에 원본 시트 폭만큼(+여백) 채우고 넘치면 다음 줄로(선반식). 아래에서 위로 쌓는다.
        int _pad = OutlineWidth;
        int _sheetWidth = _src.width + _src.width / IconSize * _pad * 2 + _pad * 2;
        int _x = 0;
        int _y = 0;
        int _rowHeight = 0;

        for (int i = 0; i < _sprites.Count; i++)
        {
            Rect _r = _sprites[i].rect;
            int _w = Mathf.RoundToInt(_r.width) + _pad * 2;
            int _h = Mathf.RoundToInt(_r.height) + _pad * 2;

            if (_x + _w > _sheetWidth)
            {
                _x = 0;
                _y += _rowHeight;
                _rowHeight = 0;
            }

            _rects.Add(new Rect(_x, _y, _w, _h));
            _x += _w;
            _rowHeight = Mathf.Max(_rowHeight, _h);
        }

        int _sheetHeight = _y + _rowHeight;
        Color32[] _srcPixels = _src.GetPixels32();
        Color32[] _dstPixels = new Color32[_sheetWidth * _sheetHeight];
        Color32 _outline = KeyIconOutlineColor;

        for (int i = 0; i < _sprites.Count; i++)
        {
            Rect _r = _sprites[i].rect;
            int _sx = Mathf.RoundToInt(_r.x);
            int _sy = Mathf.RoundToInt(_r.y);
            int _sw = Mathf.RoundToInt(_r.width);
            int _sh = Mathf.RoundToInt(_r.height);
            int _cx = Mathf.RoundToInt(_rects[i].x);
            int _cy = Mathf.RoundToInt(_rects[i].y);
            int _cw = _sw + _pad * 2;
            int _ch = _sh + _pad * 2;

            for (int py = 0; py < _ch; py++)
            {
                for (int px = 0; px < _cw; px++)
                {
                    int _ox = px - _pad;
                    int _oy = py - _pad;
                    Color32 _c = GetSpritePixel(_srcPixels, _src.width, _sx, _sy, _sw, _sh, _ox, _oy);

                    // 투명 칸이면 주변 8방향(외곽선 두께만큼)에 원본 픽셀이 있을 때 외곽선을 칠한다. 안티앨리어싱 없음(픽셀 아트).
                    if (0 == _c.a && true == HasOpaqueNeighbor(_srcPixels, _src.width, _sx, _sy, _sw, _sh, _ox, _oy, _pad))
                    {
                        _c = _outline;
                    }

                    _dstPixels[(_cy + py) * _sheetWidth + (_cx + px)] = _c;
                }
            }
        }

        UnityEngine.Object.DestroyImmediate(_src);

        Texture2D _dst = new Texture2D(_sheetWidth, _sheetHeight, TextureFormat.RGBA32, false);
        _dst.SetPixels32(_dstPixels);
        _dst.Apply();

        if (false == Directory.Exists(KeyIconOutlinedDir))
        {
            Directory.CreateDirectory(KeyIconOutlinedDir);
        }

        string _dstPath = KeyIconOutlinedDir + "/" + _texName + "_Outlined.png";
        File.WriteAllBytes(_dstPath, _dst.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(_dst);

        AssetDatabase.ImportAsset(_dstPath, ImportAssetOptions.ForceSynchronousImport);

        // 픽셀 아트 그대로: Point 필터, 무압축, 밉맵 없음, 크기 그대로.
        TextureImporter _importer = (TextureImporter)AssetImporter.GetAtPath(_dstPath);
        _importer.textureType = TextureImporterType.Default;
        _importer.alphaIsTransparency = true;
        _importer.mipmapEnabled = false;
        _importer.filterMode = FilterMode.Point;
        _importer.textureCompression = TextureImporterCompression.Uncompressed;
        _importer.npotScale = TextureImporterNPOTScale.None;
        _importer.wrapMode = TextureWrapMode.Clamp;
        _importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(_dstPath);
    }

    // 스프라이트 영역 밖(여백)은 투명으로 본다.
    private static Color32 GetSpritePixel(Color32[] _pixels, int _texWidth, int _sx, int _sy, int _sw, int _sh, int _ox, int _oy)
    {
        if (0 > _ox || 0 > _oy || _sw <= _ox || _sh <= _oy)
            return new Color32(0, 0, 0, 0);

        return _pixels[(_sy + _oy) * _texWidth + (_sx + _ox)];
    }

    private static bool HasOpaqueNeighbor(Color32[] _pixels, int _texWidth, int _sx, int _sy, int _sw, int _sh, int _ox, int _oy, int _range)
    {
        for (int dy = -_range; dy <= _range; dy++)
        {
            for (int dx = -_range; dx <= _range; dx++)
            {
                if (0 == dx && 0 == dy)
                    continue;

                if (0 < GetSpritePixel(_pixels, _texWidth, _sx, _sy, _sw, _sh, _ox + dx, _oy + dy).a)
                    return true;
            }
        }

        return false;
    }

    // 에셋을 불러오거나 새로 만들고, 시트·머티리얼·글꼴 정보를 채운 뒤 글리프/문자 테이블을 비운 상태로 돌려준다.
    private static TMP_SpriteAsset PrepareSpriteAsset(string _assetName, string _path, Texture2D _tex, out bool _bNew)
    {
        TMP_SpriteAsset _asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(_path);
        _bNew = null == _asset;

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

        _asset.name = _assetName;
        _asset.spriteSheet = _tex;
        _asset.hashCode = TMP_TextUtilities.GetSimpleHashCode(_assetName);

        // 머티리얼: TMP 기본 스프라이트 셰이더. 에셋의 서브 오브젝트로 둔다.
        if (null == _asset.material)
        {
            Shader _shader = Shader.Find("TextMeshPro/Sprite");

            if (null == _shader)
            {
                Debug.LogError("[TMPSpriteAssetBuilder] 'TextMeshPro/Sprite' 셰이더를 찾지 못했습니다.");
                return null;
            }

            Material _mat = new Material(_shader);
            _mat.name = _assetName + " Material";
            AssetDatabase.AddObjectToAsset(_mat, _asset);
            _asset.material = _mat;
        }

        _asset.material.SetTexture(ShaderUtilities.ID_MainTex, _tex);

        // 글꼴 정보: pointSize = 아이콘 한 변. 텍스트 크기가 12면 아이콘이 12px로 보인다(크기는 <size>나 scale로 조절).
        FaceInfo _face = new FaceInfo();
        _face.familyName = _assetName;
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

        return _asset;
    }

    // 시트의 _rect 영역을 글리프 하나로 등록한다. 아래쪽 1/8이 기준선 아래로 내려가 글자 높이와 맞는다.
    private static void AddGlyph(TMP_SpriteAsset _asset, uint _index, Rect _rect, string _spriteName)
    {
        AddGlyph(_asset, _index, _rect, _spriteName, IconSize * 0.125f, 1f);
    }

    // _descent: 기준선 아래로 내려가는 픽셀 수, _scale: 표시 배율(TMP_SpriteGlyph.scale)
    private static void AddGlyph(TMP_SpriteAsset _asset, uint _index, Rect _rect, string _spriteName, float _descent, float _scale)
    {
        int _w = Mathf.RoundToInt(_rect.width);
        int _h = Mathf.RoundToInt(_rect.height);

        TMP_SpriteGlyph _glyph = new TMP_SpriteGlyph();
        _glyph.index = _index;
        _glyph.metrics = new GlyphMetrics(_w, _h, 0f, _h - _descent, _w);
        _glyph.glyphRect = new GlyphRect(Mathf.RoundToInt(_rect.x), Mathf.RoundToInt(_rect.y), _w, _h);
        _glyph.scale = _scale;
        _glyph.atlasIndex = 0;
        _asset.spriteGlyphTable.Add(_glyph);

        TMP_SpriteCharacter _char = new TMP_SpriteCharacter(0xFFFE, _glyph);
        _char.name = _spriteName;
        _char.scale = 1f;
        _char.glyphIndex = _index;
        _asset.spriteCharacterTable.Add(_char);
    }

    private static void SaveSpriteAsset(TMP_SpriteAsset _asset)
    {
        _asset.UpdateLookupTables();

        EditorUtility.SetDirty(_asset);
        EditorUtility.SetDirty(_asset.material);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }
}
#endif
