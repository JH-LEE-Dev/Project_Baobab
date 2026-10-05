using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace LocalizationQA
{
    /// <summary>
    /// 게임이 실행 중에 만들어 화면에 붙이는 부품을, 게임과 같은 화면·같은 자리·같은 내용으로 띄운다.
    /// 부품 프리팹만 단독으로 그리면 부모가 정하는 폭, 이웃 행, 스크롤, 위치가 게임과 달라진다.
    ///
    ///  - 특성 툴팁: TentUI의 abilityBackground 아래에 만들고(UI_TentAbilityComponent.EnsureToolTipInstance),
    ///    게임과 같은 공식으로 만든 제목·레벨·설명·값·비용을 넣고, 화면 가운데 노드 옆에 게임 공식대로 놓는다.
    ///  - 키 설정 행(키보드·패드): ESC 화면 옵션 창의 컨트롤 탭 목록에 ERebindableAction 순서로 9개씩 (UI_Option.InitializeControlTab)
    ///  - 데모 안내: 지도 팝업(HUD_PopupNav_Main) 안에 중첩된 그 자리
    ///  - 말풍선: 캐릭터 머리 위 (화면 가운데 + 16)
    /// 던전 배너·지도 팝업·인벤토리는 단독으로 띄워도 게임과 자리·크기가 같다. (부모가 화면 전체 또는 화면 구석의 점)
    /// </summary>
    internal static class LocQAHosting
    {
        private const string TENT_UI = "Assets/Prefabs/UI/TentUI/TentUI.prefab";
        private const string TOOLTIP = "Assets/Prefabs/UI/TentUI/ToolTip.prefab";
        private const string ESC_MENU = "Assets/Prefabs/UI/ESC/ESCMenu.prefab";
        private const string KEY_ROW = "Assets/Prefabs/UI/Option/OPT_KeyBinder.prefab";
        private const string PAD_ROW = "Assets/Prefabs/UI/Option/OPT_PadKeyBinder.prefab";
        private const string NAV_MAIN = "Assets/Prefabs/UI/MenuPopup/Map/NewNav/HUD_PopupNav_Main.prefab";
        private const string DEMO_NOTICE = "Assets/Prefabs/UI/MenuPopup/Map/NewNav/HUD_PopupNav_DemoNotice.prefab";

        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // //다른 화면 안에서 그릴 부품

        /// <summary>
        /// 부품 프리팹의 칸이면, 그 부품이 게임에서 붙는 화면과 그 화면 안의 경로를 돌려준다.
        /// </summary>
        public static bool TryRedirect(string _contextGuid, string _contextPath, string _entryId, out string _hostGuid, out string _hostPath)
        {
            _hostGuid = null;
            _hostPath = null;
            string _part = AssetDatabase.GUIDToAssetPath(_contextGuid);

            if (TOOLTIP == _part)
            {
                _hostGuid = AssetDatabase.AssetPathToGUID(TENT_UI);
                string _parent = FieldPath(TENT_UI, "UI_TentAbilityComponent", "abilityBackground");
                if (null == _parent) return false;
                _hostPath = Join(_parent, TooltipName, _contextPath);
                return true;
            }

            if (KEY_ROW == _part || PAD_ROW == _part)
            {
                _hostGuid = AssetDatabase.AssetPathToGUID(ESC_MENU);
                string _parent = FieldPath(ESC_MENU, "UI_Option", KEY_ROW == _part ? "keyBindRowContainer" : "gamepadKeyBindRowContainer");
                if (null == _parent) return false;
                int _row = Mathf.Max(0, RowEntries().IndexOf(_entryId));
                _hostPath = Join(_parent, RowName(_part, _row), _contextPath);
                return true;
            }

            if (DEMO_NOTICE == _part)
            {
                string _nested = NestedPath(NAV_MAIN, DEMO_NOTICE);
                if (null == _nested) return false;
                _hostGuid = AssetDatabase.AssetPathToGUID(NAV_MAIN);
                _hostPath = Join(_nested, _contextPath);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 화면을 띄운 직후(텍스트를 모으기 전) 그 화면에 붙는 부품을 게임처럼 만들어 붙인다.
        /// _fills에는 만든 부품 칸에 들어갈 문구(경로 → 키)를 넣는다. (키 설정 행 제목 등)
        /// </summary>
        public static void Attach(GameObject _instance, string _hostGuid, Dictionary<string, string> _fills)
        {
            string _host = AssetDatabase.GUIDToAssetPath(_hostGuid);
            try
            {
                if (TENT_UI == _host) AttachTooltip(_instance);
                if (ESC_MENU == _host)
                {
                    AttachRows(_instance, "keyBindRowContainer", KEY_ROW, "Visuals/TMP_Title", false, _fills);
                    AttachRows(_instance, "gamepadKeyBindRowContainer", PAD_ROW, "Visuals/NameGroup/TMP_Title", true, _fills);
                }
            }
            catch (Exception _e)
            {
                Debug.LogWarning($"[LocalizationQA] 부품 붙이기 실패 ({_host}): {_e.Message}");
            }
        }

        private const string TooltipName = "ToolTip";

        // UI_TentAbilityComponent.EnsureToolTipInstance: Instantiate(toolTipPrefab, toolTipParent ?? abilityBackground). 처음엔 숨겨져 있다.
        private static void AttachTooltip(GameObject _instance)
        {
            MonoBehaviour _owner = FindBehaviour(_instance, "UI_TentAbilityComponent");
            Transform _parent = FieldObject(_owner, "toolTipParent") ?? FieldObject(_owner, "abilityBackground");
            GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TOOLTIP);
            if (null == _parent || null == _prefab) return;

            GameObject _tip = UnityEngine.Object.Instantiate(_prefab, _parent, false);
            _tip.name = TooltipName;
            _tip.SetActive(false);
        }

        // UI_Option.InitializeControlTab: ERebindableAction 순서로 행을 하나씩 Instantiate(행프리팹, 컨테이너)
        private static void AttachRows(GameObject _instance, string _containerField, string _rowPrefab, string _titlePath, bool _gamepad, Dictionary<string, string> _fills)
        {
            MonoBehaviour _option = FindBehaviour(_instance, "UI_Option");
            Transform _container = FieldObject(_option, _containerField);
            GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_rowPrefab);
            if (null == _container || null == _prefab) return;

            List<string> _entries = RowEntries();
            for (int i = 0; i < _entries.Count; i++)
            {
                GameObject _row = UnityEngine.Object.Instantiate(_prefab, _container, false);
                _row.name = RowName(_rowPrefab, i);

                Transform _title = LocQAPaths.Find(_row.transform, _titlePath);
                if (null != _title) _fills[LocQAPaths.GetPath(_title, _instance.transform)] = _entries[i];

                // 패드에서 리바인드할 수 없는 이동 4개는 자물쇠가 켜지고 제목이 옆으로 밀린다. (GamepadDefaultBindings.IsRebindableOnGamepad)
                if (true == _gamepad)
                {
                    MonoBehaviour _rowComponent = FindBehaviour(_row, "UI_OptionGamepadKeyBindRow");
                    Transform _lock = FieldObject(_rowComponent, "lockIndicator");
                    if (null != _lock) _lock.gameObject.SetActive(false == IsRebindableOnGamepad(i));
                }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)_container);
        }

        private static bool IsRebindableOnGamepad(int _index)
        {
            Array _actions = Enum.GetValues(typeof(ERebindableAction));
            if (_index >= _actions.Length) return true;
            return GamepadDefaultBindings.IsRebindableOnGamepad((ERebindableAction)_actions.GetValue(_index));
        }

        /// <summary>키 설정 행 제목 키. ERebindableAction 순서 (UI_Option.GetActionLabel: MoveUp → OptionUI/moveUp …)</summary>
        private static List<string> RowEntries()
        {
            Array _actions = Enum.GetValues(typeof(ERebindableAction));
            List<string> _result = new List<string>(_actions.Length);
            for (int i = 0; i < _actions.Length; i++)
            {
                string _name = _actions.GetValue(i).ToString();
                _result.Add("OptionUI/" + char.ToLowerInvariant(_name[0]) + _name.Substring(1));
            }
            return _result;
        }

        private static string RowName(string _rowPrefab, int _index) => System.IO.Path.GetFileNameWithoutExtension(_rowPrefab) + "_" + _index;

        // //문구마다 화면 상태 맞추기

        /// <summary>
        /// 이 칸을 그리기 전에 게임에서 그 칸이 보일 때의 화면 상태로 맞춘다. 돌려준 계획은 언어마다 Apply에서 쓴다.
        ///  - 옵션 탭: 칸이 들어 있는 탭을 고른 상태 (UI_OptionTabGroup.SelectTab — 효과음 없이)
        ///  - 특성 툴팁: 그 문구가 나오는 노드의 툴팁 내용
        ///  - 데모 안내: 등장 연출이 끝난 상태
        ///  - 말풍선: 캐릭터 머리 위
        /// </summary>
        public static LocQAHostPlan Begin(GameObject _instance, Transform _target, LocQAEntry _entry, LocQAStringTable _table)
        {
            SelectOptionTabs(_instance, _target);
            FinishDemoNotice(_instance, _target);

            MonoBehaviour _bubble = FindBehaviour(_instance, "UI_SpeechBubble");
            if (null != _bubble && true == _target.IsChildOf(_bubble.transform))
            {
                // UI_SpeechBubble.LateUpdate: rootRect.position = 캐릭터 위치 + (0, 0.5) — 화면 UI로 16. 카메라가 캐릭터를 따라가므로 화면 가운데.
                ((RectTransform)_bubble.transform).anchoredPosition = new Vector2(0f, 16f);
                return new LocQAHostPlan { bubble = _bubble };
            }

            MonoBehaviour _tipComponent = FindBehaviour(_instance, "AbilityToolTip");
            if (null != _tipComponent && true == _target.IsChildOf(_tipComponent.transform))
            {
                MonoBehaviour _ability = FindBehaviour(_instance, "UI_TentAbilityComponent");
                return LocQAAbilityTooltip.Plan(_ability, _tipComponent, null != _entry ? _entry.id : null, _table);
            }
            return null;
        }

        // 옵션 창은 고른 탭의 패널만 켜져 있다. 프리팹에는 여러 패널이 켜진 채 저장되어 있다.
        private static void SelectOptionTabs(GameObject _instance, Transform _target)
        {
            MonoBehaviour[] _behaviours = _instance.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < _behaviours.Length; i++)
            {
                MonoBehaviour _group = _behaviours[i];
                if (null == _group || "UI_OptionTabGroup" != _group.GetType().Name) continue;

                Array _tabs = _group.GetType().GetField("tabs", FLAGS)?.GetValue(_group) as Array;
                MethodInfo _select = _group.GetType().GetMethod("SelectTab", FLAGS, null, new[] { typeof(int), typeof(bool) }, null);
                if (null == _tabs || null == _select) continue;

                int _index = 0;
                for (int t = 0; t < _tabs.Length; t++)
                {
                    object _pair = _tabs.GetValue(t);
                    GameObject _panel = _pair.GetType().GetField("tabPanel")?.GetValue(_pair) as GameObject;
                    if (null != _panel && true == _target.IsChildOf(_panel.transform)) _index = t;
                }
                _select.Invoke(_group, new object[] { _index, false });
            }
        }

        // HUD_PopupNav_DemoNotice가 다 나타난 상태: 띠 스케일 1, 내용·어둡게 하는 배경 투명도 1 (ShowDemoNoticeOverlay의 끝 상태)
        private static void FinishDemoNotice(GameObject _instance, Transform _target)
        {
            MonoBehaviour _notice = FindBehaviour(_instance, "HUD_PopupNav_DemoNotice");
            if (null == _notice || false == _target.IsChildOf(_notice.transform)) return;

            Transform _groups = LocQAPaths.Find(_notice.transform, "Groups");
            if (null != _groups) _groups.localScale = Vector3.one;
            CanvasGroup[] _canvasGroups = _notice.GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < _canvasGroups.Length; i++) _canvasGroups[i].alpha = 1f;
        }

        // //공용

        private static string Join(params string[] _parts)
        {
            List<string> _list = new List<string>(_parts.Length);
            for (int i = 0; i < _parts.Length; i++)
            {
                if (false == string.IsNullOrEmpty(_parts[i])) _list.Add(_parts[i]);
            }
            return string.Join("/", _list);
        }

        /// <summary>프리팹 에셋에서 컴포넌트 필드가 가리키는 오브젝트의 경로 (루트 기준)</summary>
        private static string FieldPath(string _prefabPath, string _type, string _field)
        {
            GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(_prefabPath);
            if (null == _prefab) return null;
            Transform _target = FieldObject(FindBehaviour(_prefab, _type), _field);
            return null != _target ? LocQAPaths.GetPath(_target, _prefab.transform) : null;
        }

        /// <summary>호스트 프리팹 안에 중첩된 부품 프리팹 인스턴스의 경로</summary>
        private static string NestedPath(string _hostPath, string _partPath)
        {
            GameObject _host = AssetDatabase.LoadAssetAtPath<GameObject>(_hostPath);
            if (null == _host) return null;
            Transform[] _all = _host.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < _all.Length; i++)
            {
                if (false == PrefabUtility.IsAnyPrefabInstanceRoot(_all[i].gameObject)) continue;
                if (_partPath == PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(_all[i].gameObject)) return LocQAPaths.GetPath(_all[i], _host.transform);
            }
            return null;
        }

        public static MonoBehaviour FindBehaviour(GameObject _root, string _type)
        {
            if (null == _root) return null;
            MonoBehaviour[] _behaviours = _root.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < _behaviours.Length; i++)
            {
                if (null != _behaviours[i] && _type == _behaviours[i].GetType().Name) return _behaviours[i];
            }
            return null;
        }

        public static Transform FieldObject(MonoBehaviour _owner, string _field)
        {
            if (null == _owner) return null;
            object _value = _owner.GetType().GetField(_field, FLAGS)?.GetValue(_owner);
            if (_value is Component _component && null != _component) return _component.transform;
            if (_value is GameObject _go && null != _go) return _go.transform;
            return null;
        }
    }

    /// <summary>한 칸을 그리는 동안 언어마다 부품 내용·위치를 게임처럼 맞추는 계획</summary>
    internal sealed class LocQAHostPlan
    {
        public MonoBehaviour bubble;

        public MonoBehaviour tooltip;
        public MonoBehaviour ability;
        public Func<Language, object[]> tooltipContent;   // AbilityToolTip.SetContent 인자 (언어별)
        public Color? tooltipBackground;

        /// <summary>그 언어의 문구가 다 들어간 뒤, 레이아웃 계산 전</summary>
        public void ApplyLanguage(Language _lang)
        {
            if (null != tooltip && null != tooltipContent)
            {
                if (true == tooltipBackground.HasValue) Invoke(tooltip, "SetBackgroundColor", new[] { typeof(Color) }, tooltipBackground.Value);
                Invoke(tooltip, "SetContent", new[] { typeof(string), typeof(string), typeof(string), typeof(string), typeof(string), typeof(MoneyType) }, tooltipContent(_lang));
            }
        }

        /// <summary>레이아웃 계산 뒤 — 크기에 따라 정해지는 위치</summary>
        public void AfterLayout()
        {
            if (null != bubble) Invoke(bubble, "UpdateLayout", Type.EmptyTypes);
            if (null != tooltip) LocQAAbilityTooltip.Place(ability, tooltip);
        }

        private static void Invoke(MonoBehaviour _target, string _method, Type[] _types, params object[] _args)
        {
            MethodInfo _info = _target.GetType().GetMethod(_method, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, _types, null);
            _info?.Invoke(_target, _args);
        }
    }
}
