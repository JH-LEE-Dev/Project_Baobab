using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 로컬라이즈 문구 속 키 토큰 {key:액션명}을 지금 장치·바인딩에 맞는 TMP 스프라이트 태그로 바꾼다.
///   예) "{key:Dash} 키를 눌러 대시" → "&lt;sprite="KeyIcons_kb_light_all" name="Space"&gt; 키를 눌러 대시"
/// 액션명은 ERebindableAction 이름 그대로다. 리바인딩·패드 전환·패드 아이콘 세트를 그대로 따른다.
/// 아이콘을 찾지 못하면 "[키 이름]" 텍스트로 대신 쓴다(아이콘이 통째로 사라지지 않게).
///
/// 스프라이트 에셋은 키 아이콘 시트 한 장당 하나이며 이름은 SpriteAssetPrefix + 텍스처 이름이다.
/// 만드는 쪽은 TMPSpriteAssetBuilder.BuildKeyIcons(메뉴 LumberBoy/UI/Build KeyIcons Sprite Assets).
/// </summary>
public static class TMPKeyIconFormatter
{
    public const string SpriteAssetPrefix = "KeyIcons_";

    private const string TokenOpen = "{key:";
    private const char TokenClose = '}';

    // 토큰 이름 대조용. Enum.TryParse는 부분 문자열을 새로 만들어야 해서 이름표를 한 번만 만들어 두고 위치로 비교한다.
    private static readonly string[] actionNames = Enum.GetNames(typeof(ERebindableAction));
    private static readonly ERebindableAction[] actionValues = (ERebindableAction[])Enum.GetValues(typeof(ERebindableAction));

    // 스프라이트 → 완성된 <sprite ...> 태그. Sprite.name/texture.name은 읽을 때마다 문자열을 새로 만든다.
    private static readonly Dictionary<Sprite, string> spriteTagCache = new Dictionary<Sprite, string>(16);

    /// <summary>문구에 키 토큰이 들어 있는지. 없으면 Format을 부를 필요가 없다.</summary>
    public static bool HasToken(string _src)
    {
        return false == string.IsNullOrEmpty(_src) && 0 <= _src.IndexOf(TokenOpen, StringComparison.Ordinal);
    }

    /// <summary>
    /// _src의 키 토큰을 모두 바꾼 문자열을 돌려준다. 토큰이 없으면 _src를 그대로 돌려준다.
    /// _sb는 호출하는 쪽이 들고 있는 것을 재사용한다(내부에서 Clear한다).
    /// </summary>
    public static string Format(string _src, InputManager _inputManager, KeyIconDatabase _db, StringBuilder _sb)
    {
        if (false == HasToken(_src) || null == _sb)
            return _src;

        _sb.Clear();
        int _cursor = 0;

        while (_cursor < _src.Length)
        {
            int _open = _src.IndexOf(TokenOpen, _cursor, StringComparison.Ordinal);

            if (0 > _open)
                break;

            int _nameStart = _open + TokenOpen.Length;
            int _close = _src.IndexOf(TokenClose, _nameStart);

            if (0 > _close)
                break;

            _sb.Append(_src, _cursor, _open - _cursor);

            if (true == TryFindAction(_src, _nameStart, _close - _nameStart, out ERebindableAction _action))
            {
                AppendKey(_sb, _action, _inputManager, _db);
            }
            else
            {
                // 모르는 액션명은 원문 그대로 남겨 문구 쪽 오타가 눈에 띄게 한다.
                _sb.Append(_src, _open, _close - _open + 1);
#if UNITY_EDITOR
                Debug.LogWarning("[TMPKeyIconFormatter] 알 수 없는 키 토큰: " + _src.Substring(_open, _close - _open + 1));
#endif
            }

            _cursor = _close + 1;
        }

        if (_cursor < _src.Length)
        {
            _sb.Append(_src, _cursor, _src.Length - _cursor);
        }

        return _sb.ToString();
    }

    private static bool TryFindAction(string _src, int _start, int _length, out ERebindableAction _action)
    {
        for (int i = 0; i < actionNames.Length; i++)
        {
            string _name = actionNames[i];

            if (_length == _name.Length && 0 == string.CompareOrdinal(_src, _start, _name, 0, _length))
            {
                _action = actionValues[i];
                return true;
            }
        }

        _action = default;
        return false;
    }

    private static void AppendKey(StringBuilder _sb, ERebindableAction _action, InputManager _inputManager, KeyIconDatabase _db)
    {
        if (null != _inputManager && null != _db)
        {
            string _path = _inputManager.GetBindingPath(_action, _inputManager.CurrentDevice);
            Sprite _icon = _db.GetIcon(_path, _inputManager.CurrentGamepadIconSet);

            if (null != _icon)
            {
                _sb.Append(GetSpriteTag(_icon));
                return;
            }
        }

        // 폴백: 바인딩 표시 이름. 그것도 없으면 액션 이름.
        string _display = null != _inputManager ? _inputManager.GetBindingDisplayStringForCurrentDevice(_action) : null;

        _sb.Append('[');
        _sb.Append(false == string.IsNullOrEmpty(_display) ? _display : _action.ToString());
        _sb.Append(']');
    }

    private static string GetSpriteTag(Sprite _icon)
    {
        if (false == spriteTagCache.TryGetValue(_icon, out string _tag))
        {
            _tag = "<sprite=\"" + SpriteAssetPrefix + _icon.texture.name + "\" name=\"" + _icon.name + "\">";
            spriteTagCache.Add(_icon, _tag);
        }

        return _tag;
    }
}
