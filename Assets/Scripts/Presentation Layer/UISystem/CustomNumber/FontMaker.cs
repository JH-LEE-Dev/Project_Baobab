using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace PresentationLayer.UISystem.CustomNumber
{
    [ExecuteAlways]
    public class FontMaker : MonoBehaviour, ILayoutElement
    {
        private const int GlyphPixelSize = 12;
        private const string GlyphPrefix = "FontGlyph_";

        [Header("Resources")]
        [SerializeField] private Sprite currency0;
        [SerializeField] private Sprite currency1;
        [SerializeField] private Sprite currency2;
        [SerializeField] private Sprite currency3;
        [SerializeField] private Sprite currency4;
        [SerializeField] private Sprite currency5;
        [SerializeField] private Sprite currency6;
        [SerializeField] private Sprite currency7;
        [SerializeField] private Sprite currency8;
        [SerializeField] private Sprite currency9;
        [SerializeField] private Sprite currencySlash;

        [Header("Settings")]
        [SerializeField] private float pixelScale = 1.0f;
        [SerializeField] private float characterSpacing = 0.0f;
        [SerializeField] private float outlineOverlap = 1.0f;
        [SerializeField] private Vector2 glyphOffset = Vector2.zero;

        [Header("Editor Preview")]
        [SerializeField] private bool previewInEditor = true;
        [SerializeField] private string previewText = "22/22";

        private readonly List<RawImage> glyphPool = new List<RawImage>(8);
        private readonly char[] textBuffer = new char[32];
        private readonly Sprite[] sourceSprites = new Sprite[10];

        private RectTransform rectTransform;
        private string lastText = string.Empty;
        private bool initialized;
        private float layoutWidth;
        private float layoutHeight = GlyphPixelSize;
        private Color glyphColor = Color.white;

        public float minWidth => layoutWidth;
        public float preferredWidth => layoutWidth;
        public float flexibleWidth => -1.0f;
        public float minHeight => layoutHeight;
        public float preferredHeight => layoutHeight;
        public float flexibleHeight => -1.0f;
        public int layoutPriority => 1;

        public void Initialize()
        {
            if (initialized)
                return;

            initialized = true;
            rectTransform = GetComponent<RectTransform>();
            layoutWidth = Mathf.Max(0.0f, rectTransform.sizeDelta.x);
            layoutHeight = Mathf.Max(0.0f, rectTransform.sizeDelta.y);
            CacheSprites();
            CollectPool();
            HideRemainingGlyphs(0);
        }

        private void Awake()
        {
            if (Application.isPlaying)
                Initialize();
        }

        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                Initialize();
                return;
            }

#if UNITY_EDITOR
            if (previewInEditor)
                ScheduleEditorPreview();
#endif
        }

        private void OnValidate()
        {
            pixelScale = Mathf.Max(0.0f, pixelScale);
            outlineOverlap = Mathf.Max(0.0f, outlineOverlap);

            if (Application.isPlaying || false == previewInEditor)
                return;

#if UNITY_EDITOR
            ScheduleEditorPreview();
#endif
        }

#if UNITY_EDITOR
        private void ScheduleEditorPreview()
        {
            if (false == CanUpdateEditorPreview())
                return;

            EditorApplication.delayCall -= RefreshEditorPreview;
            EditorApplication.delayCall += RefreshEditorPreview;
        }

        private void RefreshEditorPreview()
        {
            EditorApplication.delayCall -= RefreshEditorPreview;

            if (null == this || Application.isPlaying || false == previewInEditor)
                return;

            if (false == CanUpdateEditorPreview())
                return;

            initialized = false;
            lastText = string.Empty;
            Initialize();
            SetText(previewText);
        }

        private bool CanUpdateEditorPreview()
        {
            if (null == gameObject || EditorUtility.IsPersistent(gameObject))
                return false;

            if (false == gameObject.scene.IsValid())
                return false;

            PrefabStage _prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            if (null != _prefabStage)
                return _prefabStage.IsPartOfPrefabContents(gameObject);

            return gameObject.scene.isLoaded;
        }
#endif

        public void SetNumber(int _value)
        {
            SetText(Mathf.Max(0, _value).ToString());
        }

        public void SetFraction(int _currentValue, int _maxValue)
        {
            SetText($"{Mathf.Max(0, _currentValue)}/{Mathf.Max(0, _maxValue)}");
        }

        public void SetText(string _text)
        {
            Initialize();

            int _length = WriteSupportedText(_text);
            string _validatedText = new string(textBuffer, 0, _length);

            lastText = _validatedText;
            UpdateGlyphs(_length);
        }

        public void SetGlyphColor(Color _color)
        {
            Initialize();
            glyphColor = _color;
            ApplyGlyphColor();
        }

        public void ResetGlyphColor()
        {
            SetGlyphColor(Color.white);
        }

        public void Clear()
        {
            Initialize();
            lastText = string.Empty;
            HideRemainingGlyphs(0);

            if (null != rectTransform)
                rectTransform.sizeDelta = new Vector2(0.0f, GlyphPixelSize * pixelScale);

            layoutWidth = 0.0f;
            layoutHeight = GlyphPixelSize * pixelScale;
            MarkLayoutDirty();
        }

        public void CalculateLayoutInputHorizontal()
        {
        }

        public void CalculateLayoutInputVertical()
        {
        }

        private int WriteSupportedText(string _text)
        {
            if (string.IsNullOrEmpty(_text))
                return 0;

            int _length = 0;
            bool _hasSlash = false;
            for (int i = 0; i < _text.Length && _length < textBuffer.Length; i++)
            {
                char _char = _text[i];
                if (false == IsSupported(_char))
                    continue;

                if ('/' == _char)
                {
                    if (_hasSlash)
                        continue;

                    _hasSlash = true;
                }

                textBuffer[_length++] = _char;
            }

            return _length;
        }

        private bool IsSupported(char _char)
        {
            return ('0' <= _char && _char <= '9') || '/' == _char;
        }

        private void UpdateGlyphs(int _length)
        {
            EnsurePoolSize(_length);

            float _cursor = 0.0f;
            float _scaledGlyphSize = GlyphPixelSize * pixelScale;
            int _visibleLength = Mathf.Min(_length, glyphPool.Count);
            float _layoutWidth = CalculateLayoutWidth(_visibleLength);
            float _leftEdge = _layoutWidth * -0.5f;

            layoutWidth = Mathf.Max(0.0f, _layoutWidth);
            layoutHeight = _scaledGlyphSize;

            if (null != rectTransform)
                rectTransform.sizeDelta = new Vector2(layoutWidth, layoutHeight);

            for (int i = 0; i < _visibleLength; i++)
            {
                char _char = textBuffer[i];
                GlyphMetrics _metrics = GetMetrics(_char);
                RawImage _image = glyphPool[i];
                RectTransform _imageRect = (RectTransform)_image.transform;

                _image.texture = GetTexture(_char);
                _image.raycastTarget = false;
                _image.color = glyphColor;
                _imageRect.sizeDelta = new Vector2(_scaledGlyphSize, _scaledGlyphSize);
                _imageRect.anchorMin = new Vector2(0.5f, 0.5f);
                _imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                _imageRect.pivot = new Vector2(0.5f, 0.5f);
                _imageRect.anchoredPosition = new Vector2(
                    _leftEdge + _cursor - (_metrics.LeftPadding * pixelScale) + (_scaledGlyphSize * 0.5f) + glyphOffset.x,
                    glyphOffset.y);

                if (false == _image.gameObject.activeSelf)
                    _image.gameObject.SetActive(true);

                _cursor += _metrics.InkWidth * pixelScale;
                if (i < _visibleLength - 1)
                    _cursor += (characterSpacing - outlineOverlap) * pixelScale;
            }

            HideRemainingGlyphs(_visibleLength);
            MarkLayoutDirty();
        }

        private void MarkLayoutDirty()
        {
            if (null == rectTransform)
                return;

            LayoutRebuilder.MarkLayoutForRebuild(rectTransform);

            RectTransform _parentRect = rectTransform.parent as RectTransform;
            if (null != _parentRect)
                LayoutRebuilder.MarkLayoutForRebuild(_parentRect);
        }

        private float CalculateLayoutWidth(int _length)
        {
            if (_length <= 0)
                return 0.0f;

            float _width = 0.0f;
            for (int i = 0; i < _length; i++)
            {
                _width += GetMetrics(textBuffer[i]).InkWidth * pixelScale;
                if (i < _length - 1)
                    _width += (characterSpacing - outlineOverlap) * pixelScale;
            }

            return _width;
        }

        private void CacheSprites()
        {
            sourceSprites[0] = currency0;
            sourceSprites[1] = currency1;
            sourceSprites[2] = currency2;
            sourceSprites[3] = currency3;
            sourceSprites[4] = currency4;
            sourceSprites[5] = currency5;
            sourceSprites[6] = currency6;
            sourceSprites[7] = currency7;
            sourceSprites[8] = currency8;
            sourceSprites[9] = currency9;
        }

        private void CollectPool()
        {
            glyphPool.Clear();

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform _child = transform.GetChild(i);
                if (false == _child.name.StartsWith(GlyphPrefix))
                    continue;

                RawImage _image = _child.GetComponent<RawImage>();
                if (null != _image)
                    glyphPool.Add(_image);
            }

            glyphPool.Sort(CompareGlyphIndex);
        }

        private int CompareGlyphIndex(RawImage _left, RawImage _right)
        {
            int _leftIndex = GetGlyphIndex(_left);
            int _rightIndex = GetGlyphIndex(_right);
            return _leftIndex.CompareTo(_rightIndex);
        }

        private int GetGlyphIndex(RawImage _image)
        {
            if (null == _image)
                return int.MaxValue;

            string _name = _image.name;
            if (false == _name.StartsWith(GlyphPrefix))
                return int.MaxValue;

            string _indexText = _name.Substring(GlyphPrefix.Length);
            return int.TryParse(_indexText, out int _index) ? _index : int.MaxValue;
        }

        private void EnsurePoolSize(int _count)
        {
            while (glyphPool.Count < _count)
            {
                RawImage _image = CreateGlyphImage(glyphPool.Count);
                if (null == _image)
                    return;

                glyphPool.Add(_image);
            }
        }

        private RawImage CreateGlyphImage(int _index)
        {
#if UNITY_EDITOR
            if (false == Application.isPlaying && false == CanUpdateEditorPreview())
                return null;
#endif

            GameObject _glyphObject = new GameObject($"{GlyphPrefix}{_index}", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            _glyphObject.layer = gameObject.layer;
            _glyphObject.transform.SetParent(transform, false);

            RectTransform _glyphRect = (RectTransform)_glyphObject.transform;
            _glyphRect.anchorMin = new Vector2(0.5f, 0.5f);
            _glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
            _glyphRect.pivot = new Vector2(0.5f, 0.5f);

            RawImage _image = _glyphObject.GetComponent<RawImage>();
            _image.raycastTarget = false;
            return _image;
        }

        private void HideRemainingGlyphs(int _activeCount)
        {
            for (int i = _activeCount; i < glyphPool.Count; i++)
            {
                if (null != glyphPool[i])
                    glyphPool[i].gameObject.SetActive(false);
            }
        }

        private void ApplyGlyphColor()
        {
            for (int i = 0; i < glyphPool.Count; i++)
            {
                if (null != glyphPool[i])
                    glyphPool[i].color = glyphColor;
            }
        }

        private Texture GetTexture(char _char)
        {
            if ('0' <= _char && _char <= '9')
                return GetTexture(sourceSprites[_char - '0']);

            if ('/' == _char)
                return GetTexture(currencySlash);

            return null;
        }

        private Texture GetTexture(Sprite _sprite)
        {
            return null == _sprite ? null : _sprite.texture;
        }

        private GlyphMetrics GetMetrics(char _char)
        {
            switch (_char)
            {
                case '1':
                    return new GlyphMetrics(4, 4);
                case '4':
                    return new GlyphMetrics(2, 8);
                case '/':
                    return new GlyphMetrics(4, 5);
                default:
                    return new GlyphMetrics(3, 7);
            }
        }

        private readonly struct GlyphMetrics
        {
            public readonly int LeftPadding;
            public readonly int InkWidth;

            public GlyphMetrics(int _leftPadding, int _inkWidth)
            {
                LeftPadding = _leftPadding;
                InkWidth = _inkWidth;
            }
        }
    }
}
