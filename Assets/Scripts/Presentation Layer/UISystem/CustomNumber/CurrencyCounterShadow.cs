using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace PresentationLayer.UISystem.CustomNumber
{
    /// <summary>
    /// 재화 카운터(CurrencyCounterHUD)의 아이콘과 숫자 글자 뒤에 같은 모양의 그림자를 그린다(대시 게이지 그림자와 같은 방식).
    /// 그림자를 아이콘·글자마다 붙이면 뒤 요소의 그림자가 앞 요소 위에 겹쳐 그려지므로, 모든 그림자를 카운터의 첫 자식 한 층에 모아
    /// 아이콘·숫자보다 먼저(뒤에) 그린다. 매 LateUpdate에 원본의 켜짐·그림·위치·크기·배율·투명도를 따라가고, 값이 같으면 건드리지 않는다.
    /// 플레이 중에만 그림자 층을 만든다(프리팹에 저장되지 않음).
    /// </summary>
    public class CurrencyCounterShadow : MonoBehaviour
    {
        private const string ShadowRootName = "__CounterShadow__";

        [Header("UI Ref")]
        [Tooltip("비우면 자식 CurrencyIcon을 찾는다.")]
        [SerializeField] private Image iconImage;
        [Tooltip("비우면 자식 CurrencyFontHUD를 찾는다.")]
        [SerializeField] private CurrencyFontHUD fontHUD;

        [Header("Shadow")]
        [SerializeField] private Vector2 shadowOffset = new Vector2(-4f, -3f);
        [SerializeField] private Color shadowColor = new Color32(0, 0, 0, 128);

        private RectTransform shadowRoot;
        private Image iconShadow;
        private RectTransform fontShadowRoot;
        private RectTransform fontRect;
        private readonly List<RawImage> glyphSources = new List<RawImage>(16);
        private readonly List<RawImage> glyphShadows = new List<RawImage>(16);
        private int scannedChildCount = -1;

        private void Awake()
        {
            if (null == iconImage)
            {
                Transform _icon = transform.Find("CurrencyIcon");
                iconImage = null != _icon ? _icon.GetComponent<Image>() : null;
            }

            if (null == fontHUD)
            {
                fontHUD = GetComponentInChildren<CurrencyFontHUD>(true);
            }

            fontRect = null != fontHUD ? fontHUD.transform as RectTransform : null;
            BuildShadowRoot();
        }

        private void LateUpdate()
        {
            if (null == shadowRoot)
            {
                return;
            }

            // 다른 자식이 앞으로 끼어들어도 그림자 층은 항상 맨 뒤
            if (0 != shadowRoot.GetSiblingIndex())
            {
                shadowRoot.SetAsFirstSibling();
            }

            SyncIcon();
            SyncFont();
        }

        private void BuildShadowRoot()
        {
            GameObject _root = new GameObject(ShadowRootName, typeof(RectTransform));
            _root.layer = gameObject.layer;
            shadowRoot = (RectTransform)_root.transform;
            shadowRoot.SetParent(transform, false);
            shadowRoot.anchorMin = Vector2.zero;
            shadowRoot.anchorMax = Vector2.one;
            shadowRoot.pivot = new Vector2(0.5f, 0.5f);
            shadowRoot.offsetMin = Vector2.zero;
            shadowRoot.offsetMax = Vector2.zero;
            shadowRoot.SetAsFirstSibling();

            if (null != iconImage)
            {
                GameObject _icon = new GameObject("IconShadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                _icon.layer = gameObject.layer;
                _icon.transform.SetParent(shadowRoot, false);
                iconShadow = _icon.GetComponent<Image>();
                iconShadow.raycastTarget = false;
                iconShadow.color = shadowColor;
            }

            if (null != fontRect)
            {
                GameObject _font = new GameObject("FontShadow", typeof(RectTransform));
                _font.layer = gameObject.layer;
                fontShadowRoot = (RectTransform)_font.transform;
                fontShadowRoot.SetParent(shadowRoot, false);
            }
        }

        private void SyncIcon()
        {
            if (null == iconShadow)
            {
                return;
            }

            bool _bVisible = null != iconImage && iconImage.isActiveAndEnabled && null != iconImage.sprite;
            SetActive(iconShadow.gameObject, _bVisible);

            if (false == _bVisible)
            {
                return;
            }

            if (iconShadow.sprite != iconImage.sprite)
            {
                iconShadow.sprite = iconImage.sprite;
            }

            iconShadow.preserveAspect = iconImage.preserveAspect;
            iconShadow.color = MultiplyAlpha(shadowColor, iconImage.color.a);
            // 그림자 층은 카운터 전체를 덮으므로 아이콘의 카운터 기준 배치를 그대로 옮긴다
            CopyRect((RectTransform)iconImage.transform, (RectTransform)iconShadow.transform, shadowOffset);
        }

        private void SyncFont()
        {
            if (null == fontShadowRoot)
            {
                return;
            }

            bool _bVisible = null != fontRect && fontRect.gameObject.activeInHierarchy;
            SetActive(fontShadowRoot.gameObject, _bVisible);

            if (false == _bVisible)
            {
                return;
            }

            CopyRect(fontRect, fontShadowRoot, Vector2.zero);

            // 글자 풀은 필요할 때 늘어나므로(증감 숫자 등) 자식 수가 바뀌면 다시 모은다
            if (scannedChildCount != fontRect.childCount)
            {
                ScanGlyphs();
            }

            for (int i = 0; i < glyphSources.Count; i++)
            {
                RawImage _src = glyphSources[i];
                RawImage _dst = glyphShadows[i];

                if (null == _src || null == _dst)
                {
                    continue;
                }

                bool _bGlyphVisible = _src.gameObject.activeSelf && _src.enabled && null != _src.texture;
                SetActive(_dst.gameObject, _bGlyphVisible);

                if (false == _bGlyphVisible)
                {
                    continue;
                }

                if (_dst.texture != _src.texture)
                {
                    _dst.texture = _src.texture;
                }

                _dst.uvRect = _src.uvRect;
                _dst.color = MultiplyAlpha(shadowColor, _src.color.a);
                CopyRect((RectTransform)_src.transform, (RectTransform)_dst.transform, shadowOffset);
            }
        }

        private void ScanGlyphs()
        {
            scannedChildCount = fontRect.childCount;

            for (int i = 0; i < fontRect.childCount; i++)
            {
                RawImage _src = fontRect.GetChild(i).GetComponent<RawImage>();

                if (null == _src || true == glyphSources.Contains(_src))
                {
                    continue;
                }

                GameObject _go = new GameObject(_src.name + "_Shadow", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
                _go.layer = gameObject.layer;
                _go.transform.SetParent(fontShadowRoot, false);
                RawImage _dst = _go.GetComponent<RawImage>();
                _dst.raycastTarget = false;
                glyphSources.Add(_src);
                glyphShadows.Add(_dst);
            }
        }

        private static void CopyRect(RectTransform _src, RectTransform _dst, Vector2 _offset)
        {
            if (_dst.anchorMin != _src.anchorMin) _dst.anchorMin = _src.anchorMin;
            if (_dst.anchorMax != _src.anchorMax) _dst.anchorMax = _src.anchorMax;
            if (_dst.pivot != _src.pivot) _dst.pivot = _src.pivot;
            if (_dst.sizeDelta != _src.sizeDelta) _dst.sizeDelta = _src.sizeDelta;

            Vector2 _pos = _src.anchoredPosition + _offset;
            if (_dst.anchoredPosition != _pos) _dst.anchoredPosition = _pos;
            if (_dst.localScale != _src.localScale) _dst.localScale = _src.localScale;
            if (_dst.localRotation != _src.localRotation) _dst.localRotation = _src.localRotation;
        }

        private static Color MultiplyAlpha(Color _color, float _alpha)
        {
            _color.a *= _alpha;
            return _color;
        }

        private static void SetActive(GameObject _go, bool _bActive)
        {
            if (_go.activeSelf != _bActive)
            {
                _go.SetActive(_bActive);
            }
        }
    }
}
