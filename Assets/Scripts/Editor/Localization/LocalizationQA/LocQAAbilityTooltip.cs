using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LocalizationQA
{
    /// <summary>
    /// 특성 툴팁을 게임과 같은 내용·위치로 만든다. (UI_TentAbilityComponent.ShowToolTip / ApplyToolTipContent)
    ///
    /// 툴팁은 폭 상한 없이 가장 긴 줄만큼 넓어지므로, 검수 중인 문구 말고 같이 들어가는 제목·레벨·설명·값·비용이
    /// 툴팁 크기(화면 밖으로 나가는지)를 정한다. 그래서 그 문구가 나오는 노드의 툴팁을 게임 공식 그대로 채운다.
    ///
    /// 값은 진행도에 따라 달라지므로, 게임에서 나올 수 있는 가장 긴 상태로 만든다.
    ///  - 보통: 그 노드가 마지막 레벨업 직전이고 같은 효과의 다른 노드는 모두 최대 (누적값이 가장 큼)
    ///  - "최대 레벨"·"해금 완료" 문구: 그 노드가 최대 레벨인 상태
    /// 문자열 조합은 게임 메서드(FormatToolTipValue, ShouldAppendPercentUnit, GetToolTipDisplayBaseValue 등)를 그대로 부른다.
    /// 위치는 화면 가운데 노드 옆 (가로 위치는 게임도 화면 안으로 보정하지 않으므로, 넓은 툴팁은 게임처럼 잘려 보인다)
    /// </summary>
    internal static class LocQAAbilityTooltip
    {
        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        private sealed class Node
        {
            public SkillType type;
            public Skill skill;
            public LocQAEntry name;
            public List<Skill> skills;  // 이 노드가 속한 데이터 세트(데모 / 정식)의 스킬 전체. 누적값 계산용
        }

        public const string NodeMissingReason = "특성 노드 데이터(데모·정식)에 없는 노드라 툴팁에 나오지 않음";
        public const string CommandMissingReason = "이 명령(SkillCommandType)을 쓰는 스킬이 데이터에 없음";
        private const string NamePrefix = "AbilityUI/";
        private const string DescriptionPrefix = "AbilityUI/AbilityDescription_";

        public static LocQAHostPlan Plan(MonoBehaviour _ability, MonoBehaviour _tooltip, string _entryId, LocQAStringTable _table)
        {
            if (null == _ability || null == _tooltip) return null;
            List<Node> _nodes = LoadNodes(_table);
            if (0 == _nodes.Count) return null;

            bool _maxed = false;
            bool _forceFree = false;
            Node _node = null;

            if (null != _entryId && true == _entryId.EndsWith("_Name", StringComparison.Ordinal))
            {
                _node = _nodes.Find(n => null != n.name && n.name.id == _entryId);
            }
            else if (null != _entryId && true == _entryId.StartsWith("AbilityUI/AbilityDescription_", StringComparison.Ordinal))
            {
                string _command = _entryId.Substring("AbilityUI/AbilityDescription_".Length);
                _node = _nodes.Find(n => LastCommand(n.skill).ToString() == _command);
            }
            else if ("AbilityUI/Common_MaxLevel" == _entryId)
            {
                _maxed = true;
            }
            else if ("AbilityUI/Common_UnlockComplete" == _entryId || "AbilityUI/Common_ToUnlock" == _entryId)
            {
                _maxed = "AbilityUI/Common_UnlockComplete" == _entryId;
                _node = _nodes.Find(n => true == Call<bool>(_ability, "ShouldUseUnlockToolTipValueText", LastCommand(n.skill)));
            }
            else if ("AbilityUI/Common_Free" == _entryId)
            {
                _forceFree = true;
                _node = _nodes.Find(n => n.skill.maxLevel > 0 && Cost(n.skill.cost.moneyCurve, n.skill.maxLevel) <= 0 && Cost(n.skill.cost.carrotCurve, n.skill.maxLevel) <= 0);
            }
            // 공용 문구("레벨", "최대 레벨" 등)는 트리의 첫 노드(누구나 보는 노드)의 툴팁에서 본다.
            // 다른 노드의 긴 설명 때문에 생기는 문제는 그 설명 문구를 검수할 때 드러난다.
            if (null == _node) _node = _nodes[0];
            if (null == _node) return null;

            Node _picked = _node;
            return new LocQAHostPlan
            {
                tooltip = _tooltip,
                ability = _ability,
                tooltipBackground = true == _maxed ? (Color?)null : StaticField<Color>(_ability, "ToolTipBackgroundAvailableColor"),
                tooltipContent = _lang => Content(_ability, _picked, _picked.skills, _maxed, _forceFree, _lang, _table),
            };
        }

        /// <summary>AbilityToolTip.SetContent(제목, 레벨, 설명, 값, 비용, 재화) 인자</summary>
        private static object[] Content(MonoBehaviour _ability, Node _node, List<Skill> _skills, bool _maxed, bool _forceFree, Language _lang, LocQAStringTable _table)
        {
            int _max = Mathf.Max(_node.skill.maxLevel, 0);
            int _current = true == _maxed ? _max : Mathf.Max(0, _max - 1);
            int _targetLevel = Mathf.Min(_current + 1, _max);     // SkillManager.RequestSkillValuePreviewData

            string _maxColor = StaticField<string>(_ability, "ToolTipCostMaxLevelColor");
            string _valueColor = StaticField<string>(_ability, "ToolTipValueColor");

            string _title = Text(_table, null != _node.name ? _node.name.id : null, _lang);
            string _level = $"{Text(_table, "AbilityUI/Common_Level", _lang)} : {_current} / {_max}";

            // SkillDispatcher.DispatchCommandWithChange: Y = 다음 레벨 증가량, X = 지금까지 누적, Z = X + Y (누적은 레벨업마다 그 레벨 값을 더한 것)
            SkillCommandInfo? _command = LastCommandInfo(_node.skill);
            string _description = string.Empty;
            string _value = string.Empty;
            if (true == _command.HasValue && SkillCommandType.None != _command.Value.skillCommandType)
            {
                SkillCommandType _type = _command.Value.skillCommandType;
                float _y = _command.Value.amountCurve.Evaluate(_targetLevel);
                float _accumulated = Accumulated(_skills, _type, _node.type, _current);
                float _base = Call<float>(_ability, "GetToolTipDisplayBaseValue", _type);   // ConvertToToolTipDisplayValue
                float _x = _accumulated + _base;
                float _z = _accumulated + _y + _base;

                string _format = Text(_table, "AbilityUI/AbilityDescription_" + _type, _lang);
                if (false == string.IsNullOrEmpty(_format))
                {
                    try { _description = string.Format(_format, Format(_ability, _y, false), Format(_ability, _x, false), Format(_ability, _z, false)); }
                    catch (FormatException) { _description = _format; }
                }

                bool _percent = Call<bool>(_ability, "ShouldAppendPercentUnit", _format);
                if (true == Call<bool>(_ability, "ShouldUseUnlockToolTipValueText", _type))
                {
                    _value = true == _maxed
                        ? Colored(_ability, Text(_table, "AbilityUI/Common_UnlockComplete", _lang), _maxColor)
                        : Colored(_ability, Text(_table, "AbilityUI/Common_ToUnlock", _lang), _valueColor);
                }
                else
                {
                    _value = true == _maxed
                        ? Colored(_ability, Format(_ability, _x, _percent), _maxColor)
                        : $"{Format(_ability, _x, _percent)} -> {Colored(_ability, Format(_ability, _z, _percent), _valueColor)}";
                }
            }

            // BuildToolTipCostText
            string _cost;
            MoneyType _money = MoneyType.None;
            if (true == _maxed)
            {
                _cost = Colored(_ability, $"<WAVE>{Text(_table, "AbilityUI/Common_MaxLevel", _lang)}</WAVE>", _maxColor);
            }
            else
            {
                long _coin = Cost(_node.skill.cost.moneyCurve, _targetLevel);
                long _carrot = Cost(_node.skill.cost.carrotCurve, _targetLevel);
                long _next = _coin > 0 ? _coin : _carrot;
                if (true == _forceFree || _next <= 0)
                {
                    _cost = Text(_table, "AbilityUI/Common_Free", _lang);
                }
                else
                {
                    _money = _coin > 0 ? MoneyType.Coin : MoneyType.Carrot;
                    _cost = Colored(_ability, AbilityNumberFormatter.FormatCompact(_next), Call<string>(_ability, "GetToolTipCostColor", AbilityLevelUpRejectReason.Pass));
                }
            }

            return new object[] { _title, _level, _description, _value, _cost, _money };
        }

        /// <summary>
        /// 그 효과의 누적값. 이 노드는 _currentLevel까지, 같은 효과를 가진 다른 노드는 모두 최대 레벨까지 올린 상태.
        /// </summary>
        private static float Accumulated(List<Skill> _skills, SkillCommandType _type, SkillType _self, int _currentLevel)
        {
            float _sum = 0f;
            for (int s = 0; s < _skills.Count; s++)
            {
                Skill _skill = _skills[s];
                if (null == _skill.skillTypes) continue;
                int _levels = _skill.skillType == _self ? _currentLevel : _skill.maxLevel;
                for (int c = 0; c < _skill.skillTypes.Count; c++)
                {
                    if (_skill.skillTypes[c].skillCommandType != _type) continue;
                    for (int l = 1; l <= _levels; l++) _sum += _skill.skillTypes[c].amountCurve.Evaluate(l);
                }
            }
            return _sum;
        }

        /// <summary>
        /// 툴팁 위치. UI_TentAbilityComponent.ShowToolTip과 같은 공식으로, 노드는 화면 가운데(줌 1, 노드 폭 1)로 둔다.
        /// </summary>
        public static void Place(MonoBehaviour _ability, MonoBehaviour _tooltip)
        {
            if (null == _ability || null == _tooltip) return;
            MethodInfo _getSize = _tooltip.GetType().GetMethod("GetSize", FLAGS, null, Type.EmptyTypes, null);
            MethodInfo _setPosition = _tooltip.GetType().GetMethod("SetAnchoredPosition", FLAGS, null, new[] { typeof(Vector2) }, null);
            if (null == _getSize || null == _setPosition) return;

            Vector2 _size = (Vector2)_getSize.Invoke(_tooltip, null);
            const float _nodeWidth = 1f;
            float _spacing = StaticField<float>(_ability, "ToolTipSpacing");
            float _x = (_nodeWidth * 0.5f) + _spacing + (_size.x * 0.5f);   // 처음 열 때는 오른쪽 배치
            float _y = Call<float>(_ability, "ClampToolTipYToScreen", 0f, _size.y);
            _setPosition.Invoke(_tooltip, new object[] { new Vector2(_x, _y) });
        }

        // //데이터

        /// <summary>
        /// 데모 · 정식 두 데이터 세트의 노드. 지금 빌드 쪽 세트를 먼저 넣는다. (같은 문구를 쓰는 노드는 지금 빌드 쪽으로 그린다)
        /// 다른 쪽 세트에만 있는 노드도 그 세트의 스킬 값으로 그린다.
        /// </summary>
        private static List<Node> LoadNodes(LocQAStringTable _table)
        {
            List<Node> _nodes = new List<Node>(256);

            string[] _guids = AssetDatabase.FindAssets("t:AbilityBuildVariantData");
            if (0 == _guids.Length) return _nodes;
            AbilityBuildVariantData _variant = AssetDatabase.LoadAssetAtPath<AbilityBuildVariantData>(AssetDatabase.GUIDToAssetPath(_guids[0]));
            if (null == _variant) return _nodes;

            bool _demoFirst = BuildInfo.IsDemo;
            AddNodes(_nodes, _table, _demoFirst ? _variant.DemoAbilityNodeDatabase : _variant.FullAbilityNodeDatabase, _demoFirst ? _variant.DemoSkillDataBase : _variant.FullSkillDataBase);
            AddNodes(_nodes, _table, _demoFirst ? _variant.FullAbilityNodeDatabase : _variant.DemoAbilityNodeDatabase, _demoFirst ? _variant.FullSkillDataBase : _variant.DemoSkillDataBase);
            return _nodes;
        }

        private static void AddNodes(List<Node> _nodes, LocQAStringTable _table, TextAsset _nodeDatabase, SkillDataBase _skillDatabase)
        {
            if (null == _nodeDatabase || null == _skillDatabase) return;

            List<Skill> _skills = _skillDatabase.skills ?? new List<Skill>();
            Dictionary<SkillType, Skill> _skillMap = new Dictionary<SkillType, Skill>();
            for (int i = 0; i < _skills.Count; i++) _skillMap[_skills[i].skillType] = _skills[i];

            AbilityNodeDatabaseJson _json = JsonUtility.FromJson<AbilityNodeDatabaseJson>(_nodeDatabase.text);
            if (null == _json || null == _json.nodes) return;

            for (int i = 0; i < _json.nodes.Length; i++)
            {
                AbilityNodeDefinitionJson _definition = _json.nodes[i];
                if (false == Enum.TryParse(_definition.skillType, out SkillType _type)) continue;
                if (false == _skillMap.TryGetValue(_type, out Skill _skill)) continue;
                _nodes.Add(new Node { type = _type, skill = _skill, name = FindAbilityEntry(_table, _definition.nameLocId), skills = _skills });
            }
        }

        /// <summary>특성 DB 동기화 결과 (목록 갱신 때)</summary>
        internal sealed class SyncReport
        {
            public int nodes;           // 데모 · 정식 노드 수 (같은 노드는 한 번)
            public int added;           // 새로 연결한 문구
            public int markedUnused;    // 어느 노드도 쓰지 않아 미사용으로 표시한 문구
            public int revived;         // 미사용이었다가 다시 쓰이게 된 문구
            public readonly List<string> missingNames = new List<string>();   // 이름 문구(nameLocId)가 AbilityUI에 없는 노드
        }

        /// <summary>
        /// 특성 툴팁 칸에 데모 · 정식 노드의 이름 · 설명 문구를 모두 연결한다. 노드를 추가 · 삭제해도 목록 갱신만 누르면 따라간다.
        ///  - 이름: 툴팁 제목 칸 (nameLocId)
        ///  - 설명: 툴팁 설명 칸 (그 노드 스킬의 마지막 명령 AbilityDescription_명령 — 미리보기 값이 명령마다 오고 마지막 것이 남는다)
        /// 어느 노드도 쓰지 않는 이름 · 설명 문구는, 어느 칸에도 연결되지 않았을 때만 미사용으로 표시한다. (사람이 적은 사유는 건드리지 않는다)
        /// </summary>
        public static SyncReport Sync(LocQAReviewData _data, LocQAStringTable _table, GameObject _tooltipPrefab, string _tooltipGuid)
        {
            SyncReport _report = new SyncReport();
            AbilityToolTip _tooltip = null != _tooltipPrefab ? _tooltipPrefab.GetComponentInChildren<AbilityToolTip>(true) : null;
            if (null == _tooltip) return _report;

            TMP_Text _titleText = typeof(AbilityToolTip).GetField("titleText", FLAGS)?.GetValue(_tooltip) as TMP_Text;
            TMP_Text _descriptionText = typeof(AbilityToolTip).GetField("descriptionText", FLAGS)?.GetValue(_tooltip) as TMP_Text;

            List<Node> _nodes = LoadNodes(_table);
            HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> _descriptions = new HashSet<string>(StringComparer.Ordinal);
            HashSet<SkillType> _seen = new HashSet<SkillType>();
            for (int i = 0; i < _nodes.Count; i++)
            {
                Node _node = _nodes[i];
                if (true == _seen.Add(_node.type)) _report.nodes++;
                if (null != _node.name) _names.Add(_node.name.id);
                else if (false == _report.missingNames.Contains(_node.type.ToString())) _report.missingNames.Add(_node.type.ToString());

                SkillCommandType _command = LastCommand(_node.skill);
                if (SkillCommandType.None == _command) continue;
                string _descriptionId = DescriptionPrefix + _command;
                if (null != _table.Find(_descriptionId)) _descriptions.Add(_descriptionId);
            }

            _report.added += Bind(_data, _tooltipPrefab, _tooltipGuid, _titleText, _names);
            _report.added += Bind(_data, _tooltipPrefab, _tooltipGuid, _descriptionText, _descriptions);

            HashSet<string> _connected = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _data.Items.Count; i++) _connected.Add(_data.Items[i].entryId);

            for (int i = 0; i < _table.Entries.Count; i++)
            {
                string _id = _table.Entries[i].id;
                bool _isDescription = _id.StartsWith(DescriptionPrefix, StringComparison.Ordinal);
                bool _isName = false == _isDescription && _id.StartsWith(NamePrefix, StringComparison.Ordinal) && _id.EndsWith("_Name", StringComparison.Ordinal);
                if (false == _isDescription && false == _isName) continue;

                string _reason = true == _isDescription ? CommandMissingReason : NodeMissingReason;
                bool _used = true == _isDescription ? _descriptions.Contains(_id) : _names.Contains(_id);
                string _current = _data.UnusedReason(_id);

                if (true == _used || true == _connected.Contains(_id))
                {
                    // 동기화가 적은 사유만 걷는다.
                    if (_reason == _current)
                    {
                        _data.SetUnused(_id, null);
                        _report.revived++;
                    }
                }
                else if (null == _current)
                {
                    _data.SetUnused(_id, _reason);
                    _report.markedUnused++;
                }
            }
            return _report;
        }

        private static int Bind(LocQAReviewData _data, GameObject _prefab, string _guid, TMP_Text _text, HashSet<string> _entryIds)
        {
            if (null == _text || 0 == _entryIds.Count) return 0;
            if (false == LocQAReviewCollector.GetSlot(_text, out string _slotGuid, out string _slotPath)) return 0;
            string _contextPath = LocQAPaths.GetPath(_text.transform, _prefab.transform);
            int _depth = LocQAReviewCollector.ContextScore(_prefab, _text.transform);

            int _added = 0;
            foreach (string _id in _entryIds)
            {
                bool _isNew = null == _data.Find(_slotGuid + "|" + _slotPath + "|" + _id);
                if (true == _data.AddOrImprove(_slotGuid, _slotPath, _guid, _contextPath, _depth, _id, "코드분석") && true == _isNew) _added++;
            }
            return _added;
        }

        // 노드 이름은 AbilityUI 파일의 항목 id로 찾는다. (ResolveLocalizedEntryText(nameLocId))
        private static LocQAEntry FindAbilityEntry(LocQAStringTable _table, int _entryId)
        {
            for (int i = 0; i < _table.Entries.Count; i++)
            {
                LocQAEntry _entry = _table.Entries[i];
                if ("AbilityUI" == _entry.file && _entryId == _entry.data.id) return _entry;
            }
            return null;
        }

        // 미리보기 값은 명령마다 오고 마지막 것이 남는다. (SkillAccumulatedValuePreviewProvided가 덮어씀)
        private static SkillCommandInfo? LastCommandInfo(Skill _skill)
        {
            if (null == _skill.skillTypes || 0 == _skill.skillTypes.Count) return null;
            return _skill.skillTypes[_skill.skillTypes.Count - 1];
        }

        private static SkillCommandType LastCommand(Skill _skill)
        {
            SkillCommandInfo? _info = LastCommandInfo(_skill);
            return true == _info.HasValue ? _info.Value.skillCommandType : SkillCommandType.None;
        }

        // SkillNode.EvaluateCost
        private static long Cost(ProgressionCurve _curve, int _level)
        {
            return MoneyMath.RoundToLong(_curve.EvaluateDecimal(_level), MidpointRounding.AwayFromZero);
        }

        private static string Text(LocQAStringTable _table, string _id, Language _lang)
        {
            LocQAEntry _entry = null != _id ? _table.Find(_id) : null;
            return null != _entry ? LocQALanguages.Resolve(_entry.data, _lang) : string.Empty;
        }

        private static string Format(MonoBehaviour _ability, float _value, bool _percent) => Call<string>(_ability, "FormatToolTipValue", _value, _percent);

        private static string Colored(MonoBehaviour _ability, string _text, string _color) => Call<string>(_ability, "BuildToolTipColorText", _text, _color);

        private static T Call<T>(MonoBehaviour _target, string _method, params object[] _args)
        {
            Type[] _types = new Type[_args.Length];
            for (int i = 0; i < _args.Length; i++) _types[i] = null != _args[i] ? _args[i].GetType() : typeof(string);
            MethodInfo _info = _target.GetType().GetMethod(_method, FLAGS, null, _types, null);
            if (null == _info) throw new MissingMethodException(_target.GetType().Name, _method);
            return (T)_info.Invoke(_target, _args);
        }

        private static T StaticField<T>(MonoBehaviour _target, string _field)
        {
            FieldInfo _info = _target.GetType().GetField(_field, FLAGS);
            if (null == _info) throw new MissingFieldException(_target.GetType().Name, _field);
            return (T)(true == _info.IsLiteral ? _info.GetRawConstantValue() : _info.GetValue(null));
        }
    }
}
