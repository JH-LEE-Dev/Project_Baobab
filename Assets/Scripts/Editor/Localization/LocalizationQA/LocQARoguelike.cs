using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace LocalizationQA
{
    /// <summary>
    /// 로그라이크 UI를 게임과 같은 내용 · 배치로 그린다.
    ///
    /// 로그라이크 화면은 문구를 코드가 채우고(프리팹 글자는 비어 있다), 문구 길이로 명판 · 판 · 설명 패널 크기를 코드가 직접 정한다.
    /// 카드 설명은 색 표시({+} {-})를 리치 텍스트로 바꾸고 줄을 다시 잇는 등 가공해서 넣는다.
    /// 그래서 툴이 문구만 넣으면 실제 화면과 달라지므로, 게임 코드의 바로 그 채우기 · 배치 메서드를 부른다.
    ///  - 문구는 툴 전용 LocalizationManager(게임 번역 파일을 그 언어로 읽은 것)로 게임 코드가 직접 넣는다.
    ///  - 검수 중인 문구가 보이는 상태로 데이터를 고른다. (카드 이름 · 설명이면 그 카드를 고른 상태, 기록 탭이면 그 탭 등)
    ///  - 카드는 런타임이 쓰는 카드 DB(RoguelikeSystem 프리팹의 cardDatabase)에서 가져온다.
    ///  - 패드에서만 보이는 키 안내 줄은 그 줄의 문구를 검수할 때만 패드 모드로 그린다.
    /// 게임 코드 · 에셋은 바꾸지 않는다. 기록 파일은 게임과 같이 읽기만 한다.
    /// </summary>
    [InitializeOnLoad]
    internal static class LocQARoguelike
    {
        private const string FILE = "RoguelikeUI/";
        private const string SYSTEM_PREFAB = "Assets/Prefabs/Roguelike/RoguelikeSystem.prefab";
        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        private static readonly string[] RecordTabKeys = { "Roguelike_Records_TabRecent", "Roguelike_Records_TabClear", "Roguelike_Records_TabUnlimit" };
        private static readonly string[] RerollKeys = { "Roguelike_CardSelect_Reroll", "Roguelike_CardSelect_RerollDesc", "Roguelike_CardSelect_Unavailable" };
        private static readonly string[] MainMenuButtonFields = { "loadGameButton", "newGameButton", "roguelikeButton", "optionButton", "creditButton", "exitButton" };
        private const string MaxLevelKey = "Roguelike_CardLevel_Max";

        private static GameObject localizationHost;
        private static LocalizationManager localization;
        private static Language localizationLanguage;
        private static GameObject padHost;
        private static InputManager padInput;

        static LocQARoguelike()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Release;
            EditorApplication.quitting += Release;
        }

        /// <summary>칸이 로그라이크 화면 안이면 그 화면을 게임처럼 채우는 계획. 아니면 null</summary>
        public static LocQAHostPlan Plan(GameObject _instance, Transform _target, LocQAEntry _entry)
        {
            if (null == _instance || null == _target) return null;
            string _key = null != _entry && true == _entry.id.StartsWith(FILE, StringComparison.Ordinal) ? _entry.id.Substring(FILE.Length) : null;

            try
            {
                MonoBehaviour _view;
                if (null != (_view = Owner(_target, "UIView_RoguelikeResult"))) return Result(_view, _target, _key);
                if (null != (_view = Owner(_target, "UIView_RoguelikeCardSelect"))) return CardSelect(_view, _target, _key);
                if (null != (_view = Owner(_target, "UIView_RoguelikeOwnedCards"))) return OwnedCards(_view, _target, _key);
                if (null != (_view = Owner(_target, "UIView_RoguelikeRecords"))) return Records(_view, _key);
                if (null != (_view = Owner(_target, "UIView_RoguelikeChallenges"))) return Challenges(_view, _target, _key);
                if (null != (_view = Owner(_target, "UIView_RoguelikeCollection"))) return Collection(_view);
                if (null != (_view = Owner(_target, "UI_MainMenu"))) return MainMenu(_view, _target, _key);
            }
            catch (Exception _e)
            {
                Debug.LogWarning($"[LocalizationQA] 로그라이크 화면 채우기 준비 실패: {Unwrap(_e).Message}");
            }
            return null;
        }

        // //화면별

        // UIView_RoguelikeResult.OnResultPresented: ApplyTexts → Layout
        private static LocQAHostPlan Result(MonoBehaviour _view, Transform _target, string _key)
        {
            // 키 안내 줄은 패드 모드에서만 보인다. (Layout의 bGuide)
            RectTransform _guide = Get(_view, "guideRow") as RectTransform;
            bool _pad = null != _guide && true == _target.IsChildOf(_guide);
            object _oldInput = Get(_view, "inputManager");
            if (true == _pad)
            {
                Set(_view, "inputManager", PadInput());
                PadIcons(_view, "keyGuideImages", true);
            }

            // 보유 카드 칸 템플릿은 Initialize에서 꺼 둔다. (에디터에서 띄운 화면은 Initialize가 돌지 않는다)
            GameObject _slotTemplate = Get(_view, "slotTemplate") is Component _template && null != _template ? _template.gameObject : null;
            bool _templateWasActive = null != _slotTemplate && _slotTemplate.activeSelf;
            if (null != _slotTemplate) _slotTemplate.SetActive(false);

            // 새 기록 / 최고 웨이브 줄은 등수로 갈린다. (bNewRecord = 0 == clearRank || 0 == unlimitRank)
            bool _best = "Roguelike_Result_BestWave" == _key;
            RoguelikeResultViewData _data = new RoguelikeResultViewData
            {
                reachedWave = 17,
                bAbandoned = "Roguelike_Result_Abandoned" == _key,
                playTimeSeconds = 1517f,
                treesFelled = 2846,
                cards = OwnedSample(null, 0, 6),
                clearRank = true == _best ? 3 : 0,
                unlimitRank = -1,
            };
            SetProperty(_view, "Data", _data);

            return new LocQAHostPlan
            {
                ownsText = true,
                onLanguage = _lang =>
                {
                    Set(_view, "localizationManager", Localization(_lang));
                    Call(_view, "ApplyTexts");
                    Call(_view, "Layout");
                },
                onAfterLayout = () => Call(_view, "Layout"),
                onRestore = () =>
                {
                    Set(_view, "inputManager", _oldInput);
                    if (true == _pad) PadIcons(_view, "keyGuideImages", false);
                    if (null != _slotTemplate) _slotTemplate.SetActive(_templateWasActive);
                },
            };
        }

        // UIView_RoguelikeCardSelect.OnOffersPresented(PlaceSlots → UpdateControls → LayoutControls → SetFocus) + OnLanguageChanged
        // 카드 등장(뒤집기) 연출은 끝난 상태로 둔다. (Bind가 앞면으로 맞춘다)
        private static LocQAHostPlan CardSelect(MonoBehaviour _view, Transform _target, string _key)
        {
            CardDatabaseSO _db = CardDatabase();
            bool _max = MaxLevelKey == _key;
            CardDefinitionSO _focus = true == _max ? FirstLevelCard(_db) : FindCard(_db, _key);

            // 제시 장수는 게임과 같게 (CardOfferGenerator: RoguelikeBalanceSO.offerCount)
            int _offerCount = OfferCount();
            List<RoguelikeCardViewData> _offers = new List<RoguelikeCardViewData>(_offerCount);
            if (null != _focus) _offers.Add(Offer(_focus, true == _max ? _focus.EffectiveMaxLevel : 1));
            if (null != _db)
            {
                // 종류가 다른 카드를 먼저, 모자라면 남은 카드로 채운다.
                for (int _pass = 0; _pass < 2; _pass++)
                {
                    for (int i = 0; i < _db.Count && _offers.Count < _offerCount; i++)
                    {
                        CardDefinitionSO _card = _db.Get(i);
                        if (null == _card || _card == _focus || ECardKind.Ruby == _card.kind || ECardKind.Prism == _card.kind) continue;
                        if (_offers.Exists(o => o.cardId == _card.cardId)) continue;
                        if (0 == _pass && _offers.Exists(o => o.kind == _card.kind)) continue;
                        _offers.Add(Offer(_card, 1));
                    }
                }
            }

            bool _reroll = Array.IndexOf(RerollKeys, _key) >= 0;
            Call(_view, "CopyOffers", _offers);
            SetProperty(_view, "bCanReroll", "Roguelike_CardSelect_Unavailable" != _key);
            _view.GetType().GetMethod("SetWave")?.Invoke(_view, new object[] { 12, false, 0 });

            Array _slots = Get(_view, "slots") as Array;
            int _count = Mathf.Min(_offers.Count, null != _slots ? _slots.Length : 0);
            Set(_view, "slotCount", _count);
            PlaceSlots(_view, _slots, _offers, _count);
            Call(_view, "SetControlsVisible", true);

            MonoBehaviour _desc = Get(_view, "descPanel") as MonoBehaviour;
            object _relay = true == _reroll || 0 == _count ? Get(_view, "rerollRelay") : ((UI_RoguelikeCardSlot)_slots.GetValue(0)).Relay;

            // 키 안내 줄은 패드 모드에서만 보인다. (RefreshPadGuide) 그 줄의 문구면 아이콘도 패드로.
            RectTransform _padRow = Get(_view, "padGuideRow") as RectTransform;
            bool _pad = null != _padRow && true == _target.IsChildOf(_padRow);
            if (true == _pad) PadIcons(_view, "padGuideImages", true);

            return new LocQAHostPlan
            {
                ownsText = true,
                onLanguage = _lang =>
                {
                    LocalizationManager _loc = Localization(_lang);
                    Set(_view, "localizationManager", _loc);
                    if (null != _desc) ((UI_RoguelikeCardDescPanel)_desc).Initialize(_loc);
                    Call(_view, "ApplyStaticTexts");
                    Call(_view, "UpdateControls");
                    Call(_view, "LayoutControls");
                    Set(_view, "focusedRelay", null);
                    Call(_view, "SetFocus", _relay, true);
                    if (null != _desc) ((UI_RoguelikeCardDescPanel)_desc).SnapHeight();
                },
                onRestore = () => { if (true == _pad) PadIcons(_view, "padGuideImages", false); },
            };
        }

        // PlaceSlots에서 등장 연출(PlayIntro)만 뺀 것
        private static void PlaceSlots(MonoBehaviour _view, Array _slots, List<RoguelikeCardViewData> _offers, int _count)
        {
            if (null == _slots) return;
            float _gap = Convert.ToSingle(Get(_view, "slotGap"));
            float _rowY = Convert.ToSingle(Get(_view, "cardRowY"));
            UI_RoguelikeCardSlot _first = _slots.Length > 0 ? (UI_RoguelikeCardSlot)_slots.GetValue(0) : null;
            float _width = null != _first ? ((RectTransform)_first.transform).rect.width : 0f;
            float _total = _count > 0 ? _count * _width + (_count - 1) * _gap : 0f;
            float _left = Mathf.Floor(-_total * 0.5f);

            for (int i = 0; i < _slots.Length; i++)
            {
                UI_RoguelikeCardSlot _slot = (UI_RoguelikeCardSlot)_slots.GetValue(i);
                if (null == _slot) continue;
                if (i >= _count)
                {
                    _slot.Unbind();
                    continue;
                }
                _slot.Bind(i, _offers[i]);
                _slot.SetPosition(new Vector2(_left + Mathf.Round(i * (_width + _gap)), _rowY));
            }
        }

        // UIView_RoguelikeOwnedCards.Rebuild: BindCells → LayoutAll(문구 · 배치 · 설명) / OnLanguageChanged: LayoutAll
        private static LocQAHostPlan OwnedCards(MonoBehaviour _view, Transform _target, string _key)
        {
            CardDatabaseSO _db = CardDatabase();
            bool _max = MaxLevelKey == _key;
            CardDefinitionSO _focus = true == _max ? FirstLevelCard(_db) : FindCard(_db, _key);

            IList _cards = Get(_view, "cards") as IList;
            if (null != _cards)
            {
                _cards.Clear();
                // "아직 고른 카드 없음" 문구는 카드가 한 장도 없을 때만 보인다.
                List<RoguelikeOwnedCardViewData> _sample = "Roguelike_OwnedCards_Empty" == _key ? new List<RoguelikeOwnedCardViewData>() : OwnedSample(_focus, true == _max ? -1 : 0, 8);
                for (int i = 0; i < _sample.Count; i++) _cards.Add(_sample[i]);
            }

            // 이동 안내 줄은 패드 모드에서만 보인다. (OnShowContent: moveGuideRow.SetActive(bGamepadMode))
            RectTransform _moveGuide = Get(_view, "moveGuideRow") as RectTransform;
            bool _guideWasActive = null != _moveGuide && _moveGuide.gameObject.activeSelf;
            bool _pad = null != _moveGuide && true == _target.IsChildOf(_moveGuide);
            if (null != _moveGuide) _moveGuide.gameObject.SetActive(_pad);
            if (true == _pad) PadIcons(_view, "keyGuideImages", true);

            MonoBehaviour _desc = Get(_view, "descPanel") as MonoBehaviour;
            bool _bound = false;
            string _focusId = null != _focus ? _focus.cardId : null;

            return new LocQAHostPlan
            {
                ownsText = true,
                onLanguage = _lang =>
                {
                    LocalizationManager _loc = Localization(_lang);
                    Set(_view, "localizationManager", _loc);
                    if (null != _desc) ((UI_RoguelikeCardDescPanel)_desc).Initialize(_loc);
                    if (false == _bound)
                    {
                        Call(_view, "BindCells");
                        Set(_view, "focusedCell", FocusCell(_view, _focusId));
                        _bound = true;
                    }
                    Call(_view, "LayoutAll");
                },
                onRestore = () =>
                {
                    if (null != _moveGuide) _moveGuide.gameObject.SetActive(_guideWasActive);
                    if (true == _pad) PadIcons(_view, "keyGuideImages", false);
                },
            };
        }

        private static object FocusCell(MonoBehaviour _view, string _cardId)
        {
            IList _cells = Get(_view, "cells") as IList;
            if (null == _cells || 0 == _cells.Count) return null;
            for (int i = 0; i < _cells.Count; i++)
            {
                UI_RoguelikeOwnedCardCell _cell = _cells[i] as UI_RoguelikeOwnedCardCell;
                if (null != _cell && true == _cell.gameObject.activeSelf && _cell.Data.cardId == _cardId) return _cell;
            }
            return _cells[0];   // FocusFirst
        }

        // UIView_RoguelikeRecords.OnRecordsPresented / OnLanguageChanged: ApplyStaticTexts → RefreshTab
        private static LocQAHostPlan Records(MonoBehaviour _view, string _key)
        {
            Set(_view, "currentTab", Mathf.Max(0, Array.IndexOf(RecordTabKeys, _key)));

            // 행 · 카드 띠 칸 견본은 Initialize에서 꺼 둔다. (에디터에서 띄운 화면은 Initialize가 돌지 않는다)
            // 카드 띠(stripRoot)는 나이테 기록판에서 스포트라이트 안에 늘 있고 RefreshTab이 켜고 끄므로 여기서 숨기지 않는다.
            List<GameObject> _templates = new List<GameObject>(2);
            string[] _templateFields = { "rowTemplate", "stripSlotTemplate" };
            for (int i = 0; i < _templateFields.Length; i++)
            {
                if (Get(_view, _templateFields[i]) is Component _template && null != _template && true == _template.gameObject.activeSelf)
                {
                    _template.gameObject.SetActive(false);
                    _templates.Add(_template.gameObject);
                }
            }

            return new LocQAHostPlan
            {
                ownsText = true,
                onLanguage = _lang =>
                {
                    Set(_view, "localizationManager", Localization(_lang));
                    Call(_view, "ApplyStaticTexts");
                    Call(_view, "RefreshTab");
                },
                onRestore = () =>
                {
                    for (int i = 0; i < _templates.Count; i++)
                    {
                        if (null != _templates[i]) _templates[i].SetActive(true);
                    }
                },
            };
        }

        // UIView_MainMenu.OpenRoguelikeChallenges: RoguelikeChallenges.FillViewData(기록, 언어) → OpenChallenges(ApplyStaticTexts · ApplyHeaderValues · RefreshRows)
        // 제목 · 팁은 목록을 만드는 쪽이 번역해 넘기므로 언어마다 목록을 다시 만든다.
        private static LocQAHostPlan Challenges(MonoBehaviour _view, Transform _target, string _key)
        {
            Action<Language> _fill = _lang =>
            {
                LocalizationManager _loc = Localization(_lang);
                Set(_view, "localizationManager", _loc);
                if (Get(_view, "challenges") is List<RoguelikeChallengeViewData> _list)
                {
                    _list.Clear();
                    RoguelikeChallenges.FillViewData(new RoguelikeRecordData(), _loc, _list);
                }
                Call(_view, "ApplyStaticTexts");
                Call(_view, "ApplyHeaderValues");
                Call(_view, "RefreshInkMetrics");
                Call(_view, "RefreshRows");
                Call(_view, "LayoutHeader");
            };

            // 칸 템플릿(RowTemplate)은 게임에서 꺼진 채로 있고, 복제한 행에 도전과제가 하나씩 들어간다.
            // 템플릿 칸의 문구면 그 문구가 들어간 행의 칸으로 옮겨 그린다.
            Transform _template = Get(_view, "rowTemplate") is Component _row && null != _row ? _row.transform : null;
            Func<Language, TMP_Text> _retarget = null;
            RectTransform _found = null;
            if (null != _template && true == _target.IsChildOf(_template) && false == string.IsNullOrEmpty(_key))
            {
                bool _tip = _target.name.IndexOf("Tip", StringComparison.Ordinal) >= 0;
                _retarget = _lang =>
                {
                    _fill(_lang);
                    string _format = Localization(_lang).GetText(_key);
                    IList _rows = Get(_view, "rows") as IList;
                    List<RoguelikeChallengeViewData> _list = Get(_view, "challenges") as List<RoguelikeChallengeViewData>;
                    if (null == _rows || null == _list) return null;
                    for (int i = 0; i < _list.Count && i < _rows.Count; i++)
                    {
                        if (false == MatchesFormat(true == _tip ? _list[i].tip : _list[i].title, _format)) continue;
                        _found = (_rows[i] as Component)?.transform as RectTransform;
                        return Get(_rows[i], true == _tip ? "tipText" : "titleText") as TMP_Text;
                    }
                    return null;
                };
            }

            // 그 행이 본문(340x220) 아래에 있으면 게임의 패드 이동(ScrollBodyTo)처럼 행이 보이게 본문을 내린다. (미끄러짐 없이 바로)
            Action _reveal = () =>
            {
                if (null == _found) return;
                RectTransform _viewport = Get(_view, "bodyViewport") as RectTransform;
                if (null == _viewport || false == Get(_view, "bodyContentHeight") is float _content) return;
                float _top = -_found.anchoredPosition.y;
                float _bottom = _top + _found.rect.height;
                float _height = _viewport.rect.height;
                float _scroll = _bottom > _height ? _bottom - _height : 0f;
                if (_top < _scroll) _scroll = _top;
                Call(_view, "SetBodyScroll", Mathf.Round(Mathf.Clamp(_scroll, 0f, Mathf.Max(0f, _content - _height))));
            };

            return new LocQAHostPlan
            {
                ownsText = true,
                onLanguage = _lang =>
                {
                    _fill(_lang);
                    _reveal();
                },
                onAfterLayout = _reveal,
                onRestore = () => { if (null != _found) Call(_view, "SetBodyScroll", 0f); },
                retarget = _retarget,
            };
        }

        // UIView_RoguelikeCollection.OnLanguageChanged: ApplyStaticTexts → LayoutAll (칸 데이터가 없는 에디터 화면이라 분류 머리줄 · 진행도 글자 자리만 새 언어로 다시 잡는다. 제목판 글자는 열 때 RenderPlate가 채운다)
        private static LocQAHostPlan Collection(MonoBehaviour _view)
        {
            return new LocQAHostPlan
            {
                ownsText = true,
                onLanguage = _lang =>
                {
                    Set(_view, "localizationManager", Localization(_lang));
                    Call(_view, "ApplyStaticTexts");
                    Call(_view, "LayoutAll");
                },
            };
        }

        // 형식 문구("{0} 그루 베기")로 만든 문자열인지. {0} 앞뒤가 같으면 같은 문구로 본다.
        private static bool MatchesFormat(string _text, string _format)
        {
            if (string.IsNullOrEmpty(_text) || string.IsNullOrEmpty(_format)) return false;
            int _slot = _format.IndexOf("{0}", StringComparison.Ordinal);
            if (_slot < 0) return _text == _format;
            string _prefix = _format.Substring(0, _slot);
            string _suffix = _format.Substring(_slot + 3);
            return _text.Length >= _prefix.Length + _suffix.Length && true == _text.StartsWith(_prefix, StringComparison.Ordinal) && true == _text.EndsWith(_suffix, StringComparison.Ordinal);
        }

        // UI_MainMenu.ShowRoguelikeButtons: 메인 버튼을 끄고 로그라이크 하위 메뉴를 켠다.
        private static LocQAHostPlan MainMenu(MonoBehaviour _menu, Transform _target, string _key)
        {
            Transform _root = Get(_menu, "roguelikeMenuRoot") as Transform;
            bool _inMenu = null != _root && true == _target.IsChildOf(_root);
            if (false == _inMenu) return null;

            List<GameObject> _hidden = new List<GameObject>(MainMenuButtonFields.Length);
            for (int i = 0; i < MainMenuButtonFields.Length; i++)
            {
                if (Get(_menu, MainMenuButtonFields[i]) is Component _button && null != _button && true == _button.gameObject.activeSelf)
                {
                    _button.gameObject.SetActive(false);
                    _hidden.Add(_button.gameObject);
                }
            }

            // 디스코드 아이콘은 로그라이크 메뉴에서 화면 밖으로 빠진다. (SlideDiscordButton(false))
            // 버튼은 실행 중에 UIView_MainMenu가 넘겨준다(SetDiscordButton). 프리팹 참조가 비어 있으면 같은 화면의 디스코드 링크 버튼을 찾는다.
            Component _discord = Get(_menu, "discordButton") as Component;
            if (null == _discord)
            {
                UI_ExternalLinkButton[] _links = _menu.transform.root.GetComponentsInChildren<UI_ExternalLinkButton>(true);
                for (int i = 0; i < _links.Length && null == _discord; i++)
                {
                    if (null != _links[i] && _links[i].name.IndexOf("Discord", StringComparison.OrdinalIgnoreCase) >= 0) _discord = _links[i];
                }
            }
            if (null != _discord && true == _discord.gameObject.activeSelf)
            {
                _discord.gameObject.SetActive(false);
                _hidden.Add(_discord.gameObject);
            }

            List<GameObject> _shown = new List<GameObject>(1);
            Show(_root, _shown);

            // 하위 메뉴 버튼은 코드가 세로로 늘어놓는다. (ShowRoguelikeButtons: roguelikeFirstButtonY - i * buttonSpacingY, 형제 순서 i)
            List<RectTransform> _buttons = new List<RectTransform>(RoguelikeButtonFields.Length);
            List<Vector2> _buttonPositions = new List<Vector2>(RoguelikeButtonFields.Length);
            List<int> _buttonOrders = new List<int>(RoguelikeButtonFields.Length);
            float _firstY = Convert.ToSingle(Get(_menu, "roguelikeFirstButtonY"));
            float _spacing = Convert.ToSingle(Get(_menu, "buttonSpacingY"));
            for (int i = 0; i < RoguelikeButtonFields.Length; i++)
            {
                if (false == (Get(_menu, RoguelikeButtonFields[i]) is Component _button) || null == _button || false == _button.transform is RectTransform _rect) continue;
                _buttons.Add(_rect);
                _buttonPositions.Add(_rect.anchoredPosition);
                _buttonOrders.Add(_rect.GetSiblingIndex());
            }
            for (int i = 0; i < RoguelikeButtonFields.Length; i++)
            {
                if (false == (Get(_menu, RoguelikeButtonFields[i]) is Component _button) || null == _button || false == _button.transform is RectTransform _rect) continue;
                _rect.anchoredPosition = new Vector2(_rect.anchoredPosition.x, _firstY - i * _spacing);
                _rect.SetSiblingIndex(i);
            }

            return new LocQAHostPlan
            {
                // 하위 메뉴 버튼 글자는 연결된 문구를 툴이 넣는다.
                ownsText = false,
                onRestore = () =>
                {
                    for (int i = 0; i < _hidden.Count; i++)
                    {
                        if (null != _hidden[i]) _hidden[i].SetActive(true);
                    }
                    for (int i = 0; i < _shown.Count; i++)
                    {
                        if (null != _shown[i]) _shown[i].SetActive(false);
                    }
                    for (int i = _buttons.Count - 1; i >= 0; i--)
                    {
                        if (null == _buttons[i]) continue;
                        _buttons[i].anchoredPosition = _buttonPositions[i];
                        _buttons[i].SetSiblingIndex(_buttonOrders[i]);
                    }
                },
            };
        }

        // UI_MainMenu.InitializeRoguelikeMenu의 roguelikeButtons 순서
        private static readonly string[] RoguelikeButtonFields = { "roguelikeStartButton", "roguelikeRecordsButton", "roguelikeChallengesButton", "roguelikeCollectionButton", "roguelikeOptionButton", "roguelikeBackButton" };

        private static void Show(Transform _t, List<GameObject> _shown)
        {
            if (null == _t || true == _t.gameObject.activeSelf) return;
            _t.gameObject.SetActive(true);
            _shown.Add(_t.gameObject);
        }

        // //카드 데이터 (런타임이 쓰는 DB)

        private static CardDatabaseSO CardDatabase()
        {
            GameObject _prefab = AssetDatabase.LoadAssetAtPath<GameObject>(SYSTEM_PREFAB);
            MonoBehaviour _system = LocQAHosting.FindBehaviour(_prefab, "RoguelikeSystem");
            return null != _system ? Get(_system, "cardDatabase") as CardDatabaseSO : null;
        }

        private static CardDefinitionSO FindCard(CardDatabaseSO _db, string _key)
        {
            if (null == _db || string.IsNullOrEmpty(_key)) return null;
            for (int i = 0; i < _db.Count; i++)
            {
                CardDefinitionSO _card = _db.Get(i);
                if (null != _card && (_card.nameKey == _key || _card.descriptionKey == _key)) return _card;
            }
            return null;
        }

        // "MAX" 표기가 나오는 카드: 레벨이 있는(루비 · 프리즘이 아닌) 첫 카드
        private static CardDefinitionSO FirstLevelCard(CardDatabaseSO _db)
        {
            if (null == _db) return null;
            for (int i = 0; i < _db.Count; i++)
            {
                CardDefinitionSO _card = _db.Get(i);
                if (null != _card && ECardKind.Ruby != _card.kind && ECardKind.Prism != _card.kind && _card.EffectiveMaxLevel > 1) return _card;
            }
            return null;
        }

        // RoguelikeUICoordinator.BuildCardViewData와 같은 값
        private static RoguelikeCardViewData Offer(CardDefinitionSO _card, int _levelAfter)
        {
            ECardDisplayGrade _grade = CardRarity.GetDisplayGrade(_card, _levelAfter);
            return new RoguelikeCardViewData
            {
                cardId = _card.cardId,
                nameKey = _card.nameKey,
                descriptionKey = _card.descriptionKey,
                card = _card,
                icon = _card.icon,
                kind = _card.kind,
                displayGrade = _grade,
                levelAfterPurchase = _levelAfter,
                maxLevel = _card.EffectiveMaxLevel,
                bRuby = ECardDisplayGrade.Ruby == _grade,
                bNewCard = _levelAfter <= 1,
                bFirstSeen = false,
                bRare = CardRarity.IsRare(_grade),
            };
        }

        /// <summary>보유 카드 견본. _focus를 맨 앞에(_focusLevel이 -1이면 최대 레벨), 나머지는 DB 순서로 _count장까지.</summary>
        private static List<RoguelikeOwnedCardViewData> OwnedSample(CardDefinitionSO _focus, int _focusLevel, int _count)
        {
            List<RoguelikeOwnedCardViewData> _result = new List<RoguelikeOwnedCardViewData>(_count);
            CardDatabaseSO _db = CardDatabase();
            if (null != _focus) _result.Add(Owned(_focus, _focusLevel < 0 ? _focus.EffectiveMaxLevel : Mathf.Max(1, _focusLevel)));
            if (null == _db) return _result;

            for (int i = 0; i < _db.Count && _result.Count < _count; i++)
            {
                CardDefinitionSO _card = _db.Get(i);
                if (null == _card || _card == _focus) continue;
                _result.Add(Owned(_card, Mathf.Clamp(_card.EffectiveMaxLevel - 1, 1, Mathf.Max(1, _card.EffectiveMaxLevel))));
            }
            return _result;
        }

        // RoguelikeUICoordinator.ToOwnedView와 같은 값
        private static RoguelikeOwnedCardViewData Owned(CardDefinitionSO _card, int _level)
        {
            return new RoguelikeOwnedCardViewData
            {
                cardId = _card.cardId,
                nameKey = _card.nameKey,
                descriptionKey = _card.descriptionKey,
                card = _card,
                icon = _card.icon,
                kind = _card.kind,
                displayGrade = CardRarity.GetDisplayGrade(_card, _level),
                level = _level,
                maxLevel = _card.EffectiveMaxLevel,
                bRuby = ECardKind.Ruby == _card.kind,
            };
        }

        // //툴 전용 LocalizationManager · 패드 입력

        /// <summary>
        /// 게임 번역 파일(Resources/Localization, Bootstrap.LoadLocalizationData와 같은 목록)을 _lang으로 읽은 LocalizationManager.
        /// Initialize · SetLanguage는 폰트 교체(FontLocalizer) 전역 상태를 건드리므로 부르지 않고, 내부 표만 채운다.
        /// 폰트는 툴이 칸마다 그 언어 폰트로 바꾼다.
        /// </summary>
        private static LocalizationManager Localization(Language _lang)
        {
            if (null == localization)
            {
                localizationHost = EditorUtility.CreateGameObjectWithHideFlags("LocQA_Localization", HideFlags.HideAndDontSave);
                localization = localizationHost.AddComponent<LocalizationManager>();
                Set(localization, "masterTable", new Dictionary<int, string>(4096));
                Set(localization, "enumToKeyMap", new Dictionary<long, int>(128));
                Set(localization, "stringToKeyMap", new Dictionary<string, int>(1024, StringComparer.OrdinalIgnoreCase));
                Set(localization, "loadedJsons", new List<string>(32));
                Set(localization, "stringBuilder", new StringBuilder(128));
                localizationLanguage = (Language)(-1);
            }

            if (localizationLanguage != _lang)
            {
                Set(localization, "currentLanguage", _lang);
                (Get(localization, "masterTable") as IDictionary)?.Clear();
                TextAsset[] _files = Resources.LoadAll<TextAsset>("Localization");
                for (int i = 0; i < _files.Length; i++)
                {
                    if (null != _files[i]) localization.LoadLocalizationJson(_files[i].text);
                }
                localizationLanguage = _lang;
            }
            return localization;
        }

        /// <summary>IsGamepadMode가 true인 입력 관리자. 장치 감시(입력 시스템 구독)는 만들지 않고 현재 장치 값만 패드로 둔다.</summary>
        private static InputManager PadInput()
        {
            if (null != padInput) return padInput;

            padHost = EditorUtility.CreateGameObjectWithHideFlags("LocQA_PadInput", HideFlags.HideAndDontSave);
            padInput = padHost.AddComponent<InputManager>();

            InputReader _reader = (InputReader)FormatterServices.GetUninitializedObject(typeof(InputReader));
            FieldInfo _trackerField = typeof(InputReader).GetField("deviceTracker", FLAGS);
            object _tracker = FormatterServices.GetUninitializedObject(_trackerField.FieldType);
            FieldInfo _device = _tracker.GetType().GetField("currentDevice", FLAGS);
            _device.SetValue(_tracker, Enum.Parse(_device.FieldType, "Gamepad"));
            _trackerField.SetValue(_reader, _tracker);
            Set(padInput, "<inputReader>k__BackingField", _reader);
            return padInput;
        }

        /// <summary>화면의 키 아이콘(UI_KeyboardImage 배열 필드)을 패드 아이콘으로 바꾸거나(_on) 키보드로 되돌린다.</summary>
        private static void PadIcons(MonoBehaviour _view, string _field, bool _on)
        {
            if (false == Get(_view, _field) is UI_KeyboardImage[] _images) return;
            for (int i = 0; i < _images.Length; i++)
            {
                if (null == _images[i]) continue;
                _images[i].Initialize(true == _on ? PadInput() : null);
                _images[i].SetGamepadMode(_on);
            }
        }

        // 카드 선택 창에 한 번에 제시하는 장수 (RoguelikeBalanceSO.offerCount, 기본 4)
        private static int OfferCount()
        {
            string[] _guids = AssetDatabase.FindAssets("t:RoguelikeBalanceSO");
            for (int i = 0; i < _guids.Length; i++)
            {
                RoguelikeBalanceSO _balance = AssetDatabase.LoadAssetAtPath<RoguelikeBalanceSO>(AssetDatabase.GUIDToAssetPath(_guids[i]));
                if (null != _balance) return Mathf.Max(1, _balance.offerCount);
            }
            return 4;
        }

        private static void Release()
        {
            if (null != localizationHost) UnityEngine.Object.DestroyImmediate(localizationHost);
            if (null != padHost) UnityEngine.Object.DestroyImmediate(padHost);
            localizationHost = null;
            localization = null;
            padHost = null;
            padInput = null;
        }

        // //리플렉션

        private static MonoBehaviour Owner(Transform _target, string _type)
        {
            for (Transform _t = _target; null != _t; _t = _t.parent)
            {
                MonoBehaviour[] _behaviours = _t.GetComponents<MonoBehaviour>();
                for (int i = 0; i < _behaviours.Length; i++)
                {
                    if (null != _behaviours[i] && _type == _behaviours[i].GetType().Name) return _behaviours[i];
                }
            }
            return null;
        }

        private static FieldInfo Field(object _owner, string _field)
        {
            for (Type _t = _owner.GetType(); null != _t; _t = _t.BaseType)
            {
                FieldInfo _info = _t.GetField(_field, FLAGS);
                if (null != _info) return _info;
            }
            throw new MissingFieldException(_owner.GetType().Name, _field);
        }

        private static object Get(object _owner, string _field) => Field(_owner, _field).GetValue(_owner);

        private static void Set(object _owner, string _field, object _value) => Field(_owner, _field).SetValue(_owner, _value);

        private static void SetProperty(object _owner, string _property, object _value)
        {
            for (Type _t = _owner.GetType(); null != _t; _t = _t.BaseType)
            {
                PropertyInfo _info = _t.GetProperty(_property, FLAGS);
                if (null == _info) continue;
                _info.SetValue(_owner, _value);
                return;
            }
            throw new MissingMemberException(_owner.GetType().Name, _property);
        }

        private static object Call(object _owner, string _method, params object[] _args)
        {
            for (Type _t = _owner.GetType(); null != _t; _t = _t.BaseType)
            {
                MethodInfo[] _methods = _t.GetMethods(FLAGS);
                for (int i = 0; i < _methods.Length; i++)
                {
                    if (_methods[i].Name != _method || _methods[i].GetParameters().Length != _args.Length) continue;
                    try { return _methods[i].Invoke(_owner, _args); }
                    catch (TargetInvocationException _e) { throw _e.InnerException ?? _e; }
                }
            }
            throw new MissingMethodException(_owner.GetType().Name, _method);
        }

        public static Exception Unwrap(Exception _e) => _e is TargetInvocationException && null != _e.InnerException ? _e.InnerException : _e;
    }
}
