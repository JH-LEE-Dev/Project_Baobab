using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace LocalizationQA
{
    /// <summary>
    /// 게임이 제공하는 해상도와, 그 해상도에서 UI 캔버스가 실제로 갖는 크기.
    ///
    /// UI 캔버스는 PixelPerfectCanvasScaleApplier가 "화면 세로 / 360을 반올림한 정수 배율"로 그린다.
    /// 그래서 16:9 해상도는 모두 640×360, 16:10은 640×400 캔버스가 되고, 전체화면은 모니터에 따라
    /// 683×384(1366×768), 853×360(2560×1080)처럼 달라진다. 툴은 이 캔버스 크기로 UI를 띄워야 게임과 같다.
    /// </summary>
    internal static class LocQAResolution
    {
        internal struct Preset
        {
            public string label;
            public int width;
            public int height;

            public Vector2 CanvasSize
            {
                get
                {
                    int _scale = SettingsData.GetPixelScale(height);
                    return new Vector2((float)width / _scale, (float)height / _scale);
                }
            }
        }

        // 전체화면은 옵션의 해상도 목록을 쓰지 않고 모니터 해상도를 그대로 쓰므로, 흔한 모니터 해상도를 따로 둔다.
        private static readonly Vector2Int[] FullscreenMonitors =
        {
            new Vector2Int(1366, 768),
            new Vector2Int(1440, 900),
            new Vector2Int(1600, 900),
            new Vector2Int(1680, 1050),
            new Vector2Int(1280, 1024),
            new Vector2Int(2560, 1080),
            new Vector2Int(3440, 1440),
            new Vector2Int(3840, 1600),
            new Vector2Int(3840, 2160)
        };

        private static List<Preset> presets;
        private static GUIContent[] labels;

        public static List<Preset> All
        {
            get
            {
                if (null == presets) Build();
                return presets;
            }
        }

        public static GUIContent[] Labels
        {
            get
            {
                if (null == labels)
                {
                    labels = new GUIContent[All.Count];
                    for (int i = 0; i < labels.Length; i++)
                    {
                        Vector2 _canvas = All[i].CanvasSize;
                        labels[i] = new GUIContent($"{All[i].label}  (캔버스 {_canvas.x:0.#}×{_canvas.y:0.#})");
                    }
                }
                return labels;
            }
        }

        public static Preset Get(int _index)
        {
            List<Preset> _all = All;
            return _all[Mathf.Clamp(_index, 0, _all.Count - 1)];
        }

        /// <summary>기본값: 1920×1080 창모드 (가장 흔한 환경)</summary>
        public static int DefaultIndex
        {
            get
            {
                List<Preset> _all = All;
                for (int i = 0; i < _all.Count; i++)
                {
                    if (1920 == _all[i].width && 1080 == _all[i].height) return i;
                }
                return 0;
            }
        }

        private static void Build()
        {
            presets = new List<Preset>(24);

            Array _values = Enum.GetValues(typeof(EResolution));
            for (int i = 0; i < _values.Length; i++)
            {
                EResolution _res = (EResolution)_values.GetValue(i);
                SettingsData.GetResolutionSize(_res, out int _w, out int _h);
                presets.Add(new Preset { label = $"창모드 {_w}×{_h}", width = _w, height = _h });
            }

            for (int i = 0; i < FullscreenMonitors.Length; i++)
            {
                Vector2Int _m = FullscreenMonitors[i];
                presets.Add(new Preset { label = $"전체화면 · 모니터 {_m.x}×{_m.y}", width = _m.x, height = _m.y });
            }
        }
    }

    /// <summary>
    /// 문구에 따라 크기를 코드로 직접 맞추는 UI는, 툴에서도 게임 코드의 바로 그 메서드를 불러 똑같이 맞춘다.
    /// 레이아웃 그룹·ContentSizeFitter로 맞추는 UI는 레이아웃 재계산만으로 같아지므로 여기 없다.
    /// (새로 그런 UI가 생기면 여기에 한 줄 추가하면 된다. 메서드는 런타임 서비스에 의존하지 않아야 한다)
    /// </summary>
    internal static class LocQALayoutHooks
    {
        private static readonly (string type, string method)[] Hooks =
        {
            // 동의 토글 두 개의 폭·높이를 레이블 문구의 preferred 크기로 맞춘다.
            ("UI_InitialSetupPopup", "SnapConsentTogglesPixelPerfect"),
        };

        // 화면을 띄운 직후 한 번만 실행해, 등장 연출이 끝난 상태로 맞춘다. (순서대로 실행)
        // 프리팹에는 연출용 가림막이 켜진 채로 저장되어 있어서, 그대로 그리면 실제 화면과 다르다.
        private static readonly (string type, string method)[] SetupHooks =
        {
            // 특성 화면(TentUI): 원형으로 열리는 연출의 검은 가림막(CircleMask·CircleRevealDim)을 연출 종료 상태로.
            ("UI_TentAbilityComponent", "BeginCircleReveal"),
            ("UI_TentAbilityComponent", "EndCircleRevealImmediately"),
        };

        // 앞 창이 열려 있으면 게임이 감추는 뒤 창. 칸이 앞 창 안에 있을 때만 뒤 창을 감춘다.
        // 프리팹에는 둘 다 켜진 채 저장되어 있어서, 그대로 그리면 뒤 창이 비쳐 보인다.
        private static readonly (string front, string hidden)[] CoverRules =
        {
            // ESC 화면: 옵션 창을 열면 ESC 메뉴는 닫힘 연출로 사라진다. (UIView_ESC.OnOptionButtonClicked → UI_EscapeMenu.PlayCloseProduction)
            ("UI_Option", "UI_EscapeMenu"),
        };

        // 한 화면 안에서 단계별로 한 패널만 보이는 UI. (컴포넌트, 칸이 들어 있는 패널 필드, 그때 감춰지는 필드들)
        // 프리팹에는 모든 단계의 패널이 켜진 채 저장되어 있어 겹쳐 보인다.
        private static readonly (string type, string front, string[] hidden)[] PhaseRules =
        {
            // 첫 실행 팝업: 1단계 언어 선택 동안 동의 패널·동의 확인 버튼은 꺼져 있다. (UI_InitialSetupPopup.Show)
            ("UI_InitialSetupPopup", "languagePanel", new[] { "consentPanel", "confirmButton" }),
            // 2단계 동의로 넘어가면 언어 패널을 끈다. (UI_InitialSetupPopup.SetupConsentPanelOnTransition)
            ("UI_InitialSetupPopup", "consentPanel", new[] { "languagePanel", "languageConfirmButton" }),
        };

        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>
        /// 그릴 언어로 바뀐 게임 화면처럼 맞춘다. 언어를 넘길 때마다 앱 언어가 바로 바뀌고 그 언어의 이름 라벨만 켜지는 UI가 대상이다.
        ///  - 첫 실행 팝업: 선택 위치를 그 언어로 옮기고 게임의 UpdateLanguageDisplay를 실행 (그 언어 이름 라벨만 켜고 영어 부제를 바꿈)
        /// </summary>
        public static void RunLanguage(GameObject _root, Language _lang)
        {
            MonoBehaviour[] _behaviours = _root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < _behaviours.Length; i++)
            {
                MonoBehaviour _b = _behaviours[i];
                if (null == _b || "UI_InitialSetupPopup" != _b.GetType().Name) continue;

                try
                {
                    Type _type = _b.GetType();
                    Array _bindings = _type.GetField("languageBindings", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)?.GetValue(null) as Array;
                    FieldInfo _index = _type.GetField("languageIndex", FLAGS);
                    MethodInfo _update = _type.GetMethod("UpdateLanguageDisplay", FLAGS, null, Type.EmptyTypes, null);
                    if (null == _bindings || null == _index || null == _update) continue;

                    EOptionLanguage _option = LocQALanguages.ToOption(_lang);
                    for (int k = 0; k < _bindings.Length; k++)
                    {
                        object _binding = _bindings.GetValue(k);
                        if (false == Equals(_binding.GetType().GetField("Language").GetValue(_binding), _option)) continue;
                        _index.SetValue(_b, k);
                        _update.Invoke(_b, null);

                        // 점 커서도 현재 점 위로. (게임의 SnapLanguageDotCursor와 같다. 그 메서드는 DOTween을 건드려
                        // 에디터에서 DOTween 오브젝트가 생길 수 있으므로 위치 맞추기만 따라 한다)
                        RectTransform _cursor = _type.GetField("languageDotCursor", FLAGS)?.GetValue(_b) as RectTransform;
                        Array _dots = _type.GetField("languageDots", FLAGS)?.GetValue(_b) as Array;
                        if (null != _cursor && null != _dots && k < _dots.Length && _dots.GetValue(k) is Graphic _dot && null != _dot)
                        {
                            _cursor.SetParent(_dot.rectTransform, false);
                            _cursor.anchoredPosition = Vector2.zero;
                        }
                        break;
                    }
                }
                catch (Exception _e)
                {
                    Debug.LogWarning($"[LocalizationQA] UI_InitialSetupPopup 언어 표시 맞추기 실패: {_e.InnerException?.Message ?? _e.Message}");
                }
            }
        }

        /// <summary>칸(_target)이 들어 있는 창 때문에 게임에서 감춰지는 다른 창들.</summary>
        public static List<GameObject> CoveredWindows(Transform _target, Transform _root)
        {
            List<GameObject> _result = new List<GameObject>(2);
            for (int r = 0; r < CoverRules.Length; r++)
            {
                if (false == HasAncestorOfType(_target, _root, CoverRules[r].front)) continue;

                MonoBehaviour[] _behaviours = _root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < _behaviours.Length; i++)
                {
                    MonoBehaviour _b = _behaviours[i];
                    if (null == _b || _b.GetType().Name != CoverRules[r].hidden) continue;
                    if (true == _target.IsChildOf(_b.transform)) continue; // 칸을 품은 창은 감추지 않는다
                    _result.Add(_b.gameObject);
                }
            }

            for (int r = 0; r < PhaseRules.Length; r++)
            {
                MonoBehaviour[] _behaviours = _root.GetComponentsInChildren<MonoBehaviour>(true);
                for (int i = 0; i < _behaviours.Length; i++)
                {
                    MonoBehaviour _b = _behaviours[i];
                    if (null == _b || _b.GetType().Name != PhaseRules[r].type) continue;

                    GameObject _front = FieldObject(_b, PhaseRules[r].front);
                    if (null == _front || false == _target.IsChildOf(_front.transform)) continue;

                    for (int h = 0; h < PhaseRules[r].hidden.Length; h++)
                    {
                        GameObject _hidden = FieldObject(_b, PhaseRules[r].hidden[h]);
                        if (null != _hidden && false == _target.IsChildOf(_hidden.transform)) _result.Add(_hidden);
                    }
                }
            }
            return _result;
        }

        private static GameObject FieldObject(MonoBehaviour _owner, string _field)
        {
            object _value = _owner.GetType().GetField(_field, FLAGS)?.GetValue(_owner);
            if (_value is Component _component && null != _component) return _component.gameObject;
            if (_value is GameObject _go && null != _go) return _go;
            return null;
        }

        private static bool HasAncestorOfType(Transform _target, Transform _root, string _typeName)
        {
            for (Transform _t = _target; null != _t; _t = _t.parent)
            {
                MonoBehaviour[] _behaviours = _t.GetComponents<MonoBehaviour>();
                for (int i = 0; i < _behaviours.Length; i++)
                {
                    if (null != _behaviours[i] && _behaviours[i].GetType().Name == _typeName) return true;
                }
                if (_t == _root) break;
            }
            return false;
        }

        /// <summary>화면을 띄운 직후 한 번 실행한다. (등장 연출 종료 상태)</summary>
        public static void RunSetup(GameObject _root)
        {
            RunList(_root, SetupHooks);
        }

        /// <returns>훅을 하나라도 실행했으면 true (레이아웃을 한 번 더 계산해야 한다)</returns>
        public static bool Run(GameObject _root)
        {
            return RunList(_root, Hooks);
        }

        private static bool RunList(GameObject _root, (string type, string method)[] _hooks)
        {
            bool _ran = false;
            MonoBehaviour[] _behaviours = _root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < _behaviours.Length; i++)
            {
                MonoBehaviour _b = _behaviours[i];
                if (null == _b) continue;

                string _typeName = _b.GetType().Name;
                for (int h = 0; h < _hooks.Length; h++)
                {
                    if (_hooks[h].type != _typeName) continue;

                    MethodInfo _method = _b.GetType().GetMethod(_hooks[h].method, FLAGS, null, Type.EmptyTypes, null);
                    if (null == _method) continue;

                    try
                    {
                        _method.Invoke(_b, null);
                        _ran = true;
                    }
                    catch (Exception _e)
                    {
                        Debug.LogWarning($"[LocalizationQA] {_typeName}.{_hooks[h].method} 실행 실패: {_e.InnerException?.Message ?? _e.Message}");
                    }
                }
            }
            return _ran;
        }
    }
}
