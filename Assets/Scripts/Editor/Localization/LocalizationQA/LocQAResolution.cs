using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

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

        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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
