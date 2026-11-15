using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 모루 위에 뜨는 원판 도끼 내구도 HUD. 도끼를 맡긴 동안만 보인다. 그림은 화면 도끼 HUD의 원판 게이지 스프라이트를 그대로 쓴다
    /// (바탕 Gauge.png → 테두리 Gauge_Outline.png → 채움 Gauge_Inside.png 순서로 겹친다. 화면 HUD와 같은 순서).
    /// 채움은 AnvilRepairGauge 셰이더가 위에서부터 시계 방향으로 내구도만큼 칠하고, 모루를 두드릴 때마다 잠깐 밝아진다.
    /// 그림일 뿐이라 수리 판정과는 무관하다. RepairBox가 런타임에 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public class AnvilRepairGauge : MonoBehaviour
    {
        private const float PixelsPerUnit = 32f;
        private const string SortingLayerName = "FlyingItem";
        private const int BackgroundSortingOrder = 5;
        private const int OutlineSortingOrder = 6;
        private const int FillSortingOrder = 7;

        private const float FlashDuration = 0.1f; // 두드릴 때 밝아지는 시간

        private static readonly int IdMainRect = Shader.PropertyToID("_MainRect");
        private static readonly int IdSizePx = Shader.PropertyToID("_SizePx");
        private static readonly int IdMode = Shader.PropertyToID("_Mode");
        private static readonly int IdFill = Shader.PropertyToID("_Fill");
        private static readonly int IdFlash = Shader.PropertyToID("_Flash");

        private SpriteRenderer backgroundRenderer;
        private SpriteRenderer outlineRenderer;
        private SpriteRenderer fillRenderer;
        private Material spriteMaterial;
        private Material fillMaterial;
        private int widthPx;
        private int heightPx;

        private bool bVisible;
        private float fill;
        private float flashAge = float.MaxValue;

        /// <summary>_parent 아래에 원판을 만든다. 그림이나 셰이더가 없으면 null(원판 없이 진행).</summary>
        public static AnvilRepairGauge Create(Transform _parent, Sprite _backgroundSprite, Sprite _outlineSprite, Sprite _fillSprite, Shader _shader)
        {
            if (null == _backgroundSprite || null == _outlineSprite || null == _fillSprite || null == _shader || false == _shader.isSupported)
            {
                Debug.LogWarning("[AnvilRepairGauge] 원판 그림이나 셰이더가 없어 모루 도끼 내구도 HUD를 건너뜁니다.");
                return null;
            }

            GameObject go = new GameObject("AnvilRepairGauge");
            go.transform.SetParent(_parent, false);
            AnvilRepairGauge gauge = go.AddComponent<AnvilRepairGauge>();
            gauge.Initialize(_backgroundSprite, _outlineSprite, _fillSprite, _shader);
            return gauge;
        }

        /// <summary>_anchor(원판 바닥 가운데, 월드)에 원판을 보인다.</summary>
        public void Show(Vector3 _anchor, float _fill)
        {
            fill = Mathf.Clamp01(_fill);
            flashAge = float.MaxValue;
            PlaceAt(_anchor);
            bVisible = true;
            SetRenderersVisible(true);
            ApplyMaterial();
        }

        public void Hide()
        {
            bVisible = false;
            flashAge = float.MaxValue;
            SetRenderersVisible(false);
        }

        /// <summary>보이는 동안 매 프레임 진행도와 위치(원판 바닥 가운데, 월드)를 넘긴다.</summary>
        public void SetState(Vector3 _anchor, float _fill)
        {
            fill = Mathf.Clamp01(_fill);
            PlaceAt(_anchor);
        }

        /// <summary>모루가 한 번 두드릴 때 채움이 잠깐 밝아진다.</summary>
        public void PlayHit()
        {
            flashAge = 0f;
        }

        private void Initialize(Sprite _backgroundSprite, Sprite _outlineSprite, Sprite _fillSprite, Shader _shader)
        {
            spriteMaterial = new Material(_shader) { name = "M_AnvilRepairGauge_Sprite (Runtime)" };
            spriteMaterial.SetFloat(IdMode, 0f);

            fillMaterial = new Material(_shader) { name = "M_AnvilRepairGauge_Fill (Runtime)" };
            fillMaterial.SetFloat(IdMode, 1f);
            fillMaterial.SetVector(IdMainRect, UVRect(_fillSprite));
            fillMaterial.SetVector(IdSizePx, new Vector4(_fillSprite.rect.width, _fillSprite.rect.height, 0f, 0f));

            backgroundRenderer = CreateRenderer("Background", _backgroundSprite, spriteMaterial, BackgroundSortingOrder);
            outlineRenderer = CreateRenderer("Outline", _outlineSprite, spriteMaterial, OutlineSortingOrder);
            fillRenderer = CreateRenderer("Fill", _fillSprite, fillMaterial, FillSortingOrder);

            widthPx = Mathf.RoundToInt(_backgroundSprite.rect.width);
            heightPx = Mathf.RoundToInt(_backgroundSprite.rect.height);

            SetRenderersVisible(false);
        }

        private SpriteRenderer CreateRenderer(string _name, Sprite _sprite, Material _material, int _order)
        {
            GameObject go = new GameObject(_name);
            go.transform.SetParent(transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sharedMaterial = _material;
            renderer.sortingLayerName = SortingLayerName;
            renderer.sortingOrder = _order;

            // UI용 스프라이트라 PPU가 월드(32)와 다를 수 있다. 1텍셀 = 1/32 유닛이 되게 크기를 맞춘다.
            float scale = _sprite.pixelsPerUnit / PixelsPerUnit;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            return renderer;
        }

        // 원판 바닥 가운데를 _anchor에 맞춰 픽셀 격자에 놓는다.
        private void PlaceAt(Vector3 _anchor)
        {
            int left = Mathf.RoundToInt(_anchor.x * PixelsPerUnit) - widthPx / 2;
            int bottom = Mathf.RoundToInt(_anchor.y * PixelsPerUnit);

            SetBottomLeft(backgroundRenderer, left, bottom);
            SetBottomLeft(outlineRenderer, left, bottom);
            SetBottomLeft(fillRenderer, left, bottom);
        }

        private static void SetBottomLeft(SpriteRenderer _renderer, int _left, int _bottom)
        {
            Sprite sprite = _renderer.sprite;
            _renderer.transform.position = new Vector3(
                (_left + sprite.pivot.x) / PixelsPerUnit,
                (_bottom + sprite.pivot.y) / PixelsPerUnit,
                0f);
        }

        private void Update()
        {
            if (false == bVisible) return;

            if (flashAge < float.MaxValue) flashAge += Time.deltaTime;
            ApplyMaterial();
        }

        private void ApplyMaterial()
        {
            fillMaterial.SetFloat(IdFill, fill);
            fillMaterial.SetFloat(IdFlash, flashAge < FlashDuration ? 1f - flashAge / FlashDuration : 0f);
        }

        private void SetRenderersVisible(bool _bVisible)
        {
            if (null != backgroundRenderer) backgroundRenderer.enabled = _bVisible;
            if (null != outlineRenderer) outlineRenderer.enabled = _bVisible;
            if (null != fillRenderer) fillRenderer.enabled = _bVisible;
        }

        private static Vector4 UVRect(Sprite _sprite)
        {
            Rect r = _sprite.textureRect;
            float w = _sprite.texture.width;
            float h = _sprite.texture.height;
            return new Vector4(r.xMin / w, r.yMin / h, r.xMax / w, r.yMax / h);
        }

        private void OnDestroy()
        {
            if (null != spriteMaterial) Destroy(spriteMaterial);
            if (null != fillMaterial) Destroy(fillMaterial);
        }
    }
}
