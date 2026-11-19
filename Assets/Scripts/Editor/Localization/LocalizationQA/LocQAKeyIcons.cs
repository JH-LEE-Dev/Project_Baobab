using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LocalizationQA
{
    /// <summary>
    /// 문구 속 키 토큰 {key:액션명}을 게임처럼 키 아이콘 스프라이트 태그로 바꾼다. (TMPKeyIconFormatter.Format 그대로)
    ///
    /// 게임은 지금 장치 · 바인딩으로 아이콘을 고른다. 툴은 키보드 · 기본 바인딩(.inputactions)으로 고른다.
    /// 사용자가 바꾼 키 설정(LoadKeyBindings)은 읽지 않아, 누가 그려도 같은 그림이 나온다.
    /// 입력 장치 감시 · 입력 이벤트 구독은 만들지 않는다. (InputReader.Initialize를 부르지 않는다)
    /// </summary>
    [InitializeOnLoad]
    internal static class LocQAKeyIcons
    {
        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static GameObject host;
        private static InputManager input;
        private static InputActionSystem actions;
        private static KeyIconDatabase database;
        private static bool searchedDatabase;
        private static readonly StringBuilder builder = new StringBuilder(128);

        static LocQAKeyIcons()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            EditorApplication.quitting += Release;
        }

        public static bool HasToken(string _text) => TMPKeyIconFormatter.HasToken(_text);

        /// <summary>키 토큰이 있으면 아이콘 태그로 바꾼 문자열, 없으면 그대로</summary>
        public static string Format(string _text)
        {
            if (false == TMPKeyIconFormatter.HasToken(_text)) return _text;
            return TMPKeyIconFormatter.Format(_text, Input(), Database(), builder);
        }

        /// <summary>텍스트에 키 토큰이 남아 있으면 바꿔 넣는다.</summary>
        public static void Apply(TMP_Text _text)
        {
            if (null == _text || false == TMPKeyIconFormatter.HasToken(_text.text)) return;
            _text.text = Format(_text.text);
        }

        private static KeyIconDatabase Database()
        {
            if (null != database || true == searchedDatabase) return database;
            searchedDatabase = true;
            string[] _guids = AssetDatabase.FindAssets("t:KeyIconDatabase");
            for (int i = 0; i < _guids.Length && null == database; i++)
            {
                database = AssetDatabase.LoadAssetAtPath<KeyIconDatabase>(AssetDatabase.GUIDToAssetPath(_guids[i]));
            }
            return database;
        }

        // 키보드 · 기본 바인딩만 가진 입력 관리자. (장치 추적기가 없으면 InputReader는 키보드 · 마우스로 본다)
        private static InputManager Input()
        {
            if (null != input) return input;

            try
            {
                InputReader _reader = (InputReader)FormatterServices.GetUninitializedObject(typeof(InputReader));
                actions = new InputActionSystem();
                typeof(InputReader).GetField("actions", FLAGS)?.SetValue(_reader, actions);

                host = EditorUtility.CreateGameObjectWithHideFlags("LocQA_KeyboardInput", HideFlags.HideAndDontSave);
                input = host.AddComponent<InputManager>();
                typeof(InputManager).GetField("<inputReader>k__BackingField", FLAGS)?.SetValue(input, _reader);
            }
            catch (System.Exception _e)
            {
                Debug.LogWarning($"[LocalizationQA] 키 아이콘용 입력 정보를 만들지 못해 키 이름으로 대신 씁니다: {_e.Message}");
                Release();
            }
            return input;
        }

        private static void Release()
        {
            if (null != host) Object.DestroyImmediate(host);
            actions?.Dispose();
            host = null;
            input = null;
            actions = null;
            database = null;
            searchedDatabase = false;
        }
    }
}
