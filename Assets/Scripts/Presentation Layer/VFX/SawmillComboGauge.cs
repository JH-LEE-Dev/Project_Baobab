using UnityEngine;

namespace PresentationLayer.VFX
{
    /// <summary>
    /// 제재소 가공 콤보 게이지(원목 상자 위). 그림은 기존 스프라이트만 쓴다.
    /// - 판: 마을 UI 판(Boarder.png)을 9-슬라이스로 작게.
    /// - 막대: 커터 위 진행 막대(CutterGauge_background/fill.png)를 SawmillComboGauge 셰이더로 가로로 늘리고 칸으로 나눈다.
    /// 쌓일 때 새 칸이 잠깐 밝아지고, 가득 차는 순간 전체가 밝아졌다가 반짝임이 칸을 따라 돌고, 풀리면 위 칸부터 꺼진다.
    /// 그림일 뿐이라 가공·벨트·판정과는 무관하다. SawmillComboPresenter가 런타임에 만든다.
    /// </summary>
    [DisallowMultipleComponent]
    public class SawmillComboGauge : MonoBehaviour
    {
        private const float PixelsPerUnit = 32f;
        private const string SortingLayerName = "FlyingItem";
        private const int PanelSortingOrder = 5;
        private const int BarSortingOrder = 6;

        private const int MaxSegments = 10;
        private const int SegWidth = 3;
        private const int SegGap = 1;
        private const int PanelPadX = 4;        // 판 가장자리에서 막대까지(테두리·그늘 포함)
        private const int PanelHeight = 12;     // 판 높이(px). Boarder 9-슬라이스 최소 10
        private const int BarBottomInPanel = 5; // 판 바닥에서 막대 바닥까지(아래 그늘 줄 위)

        private const float PopDuration = 0.1f;     // 새 칸이 밝은 시간
        private const float FlashDuration = 0.12f;  // 가득 찬 순간 전체가 밝은 시간
        private const float DrainDuration = 0.3f;   // 풀릴 때 위 칸부터 꺼지는 시간

        private static readonly int IdMainRect = Shader.PropertyToID("_MainRect");
        private static readonly int IdFillTex = Shader.PropertyToID("_FillTex");
        private static readonly int IdFillRect = Shader.PropertyToID("_FillRect");
        private static readonly int IdMode = Shader.PropertyToID("_Mode");
        private static readonly int IdSizePx = Shader.PropertyToID("_SizePx");
        private static readonly int IdSrcSizePx = Shader.PropertyToID("_SrcSizePx");
        private static readonly int IdSegments = Shader.PropertyToID("_Segments");
        private static readonly int IdSegWidth = Shader.PropertyToID("_SegWidth");
        private static readonly int IdSegGap = Shader.PropertyToID("_SegGap");
        private static readonly int IdLit = Shader.PropertyToID("_Lit");
        private static readonly int IdPopIndex = Shader.PropertyToID("_PopIndex");
        private static readonly int IdFlash = Shader.PropertyToID("_Flash");
        private static readonly int IdShine = Shader.PropertyToID("_Shine");
        private static readonly int IdCoolIndex = Shader.PropertyToID("_CoolIndex");

        private SpriteRenderer panelRenderer;
        private SpriteRenderer barRenderer;
        private Material panelMaterial;
        private Material barMaterial;
        private int barWidthPx;
        private int barHeightPx;

        private bool bVisible;
        private int segments = MaxSegments;
        private int stack;
        private int popIndex = -1;
        private float popAge = float.MaxValue;
        private float flashAge = float.MaxValue;
        private float drainAge = float.MaxValue;
        private int drainFrom;

        /// <summary>_parent 아래에 게이지를 만든다. 그림이나 셰이더가 없으면 null(게이지 없이 진행).</summary>
        public static SawmillComboGauge Create(Transform _parent, Sprite _panelSprite, Sprite _trackSprite, Sprite _fillSprite, Shader _shader)
        {
            if (null == _panelSprite || null == _trackSprite || null == _fillSprite || null == _shader || false == _shader.isSupported)
            {
                Debug.LogWarning("[SawmillComboGauge] 게이지 그림이나 셰이더가 없어 가공 콤보 게이지를 건너뜁니다.");
                return null;
            }

            GameObject go = new GameObject("SawmillComboGauge");
            go.transform.SetParent(_parent, false);
            SawmillComboGauge gauge = go.AddComponent<SawmillComboGauge>();
            gauge.Initialize(_panelSprite, _trackSprite, _fillSprite, _shader);
            return gauge;
        }

        /// <summary>
        /// 매 프레임 상태를 넘긴다. _anchor는 판 바닥 가운데(월드), _stack/_max는 콤보 수.
        /// 다시 보이기 시작할 때는 지금 상태 그대로 보여 준다(그동안 바뀐 것을 한꺼번에 터뜨리지 않는다).
        /// </summary>
        public void SetState(bool _bVisible, Vector3 _anchor, int _stack, int _max)
        {
            int max = Mathf.Clamp(_max, 1, MaxSegments);
            int next = Mathf.Clamp(_stack, 0, max);

            if (false == _bVisible)
            {
                if (bVisible) SetRenderersVisible(false);
                bVisible = false;
                stack = next;
                ResetAnimation();
                return;
            }

            if (false == bVisible || max != segments)
            {
                segments = max;
                stack = next;
                ResetAnimation();
                ApplyLayout();
                SetRenderersVisible(true);
                bVisible = true;
            }

            if (next > stack)
            {
                popIndex = next - 1;
                popAge = 0f;
                if (next == segments) flashAge = 0f;
                drainAge = float.MaxValue;
            }
            else if (next < stack)
            {
                drainFrom = stack;
                drainAge = 0f;
                popAge = float.MaxValue;
                flashAge = float.MaxValue;
            }
            stack = next;

            PlaceAt(_anchor);
        }

        /// <summary>게이지를 숨긴다(맵을 떠날 때). 다음 SetState에서 다시 보인다.</summary>
        public void Clear()
        {
            bVisible = false;
            ResetAnimation();
            SetRenderersVisible(false);
        }

        private void Initialize(Sprite _panelSprite, Sprite _trackSprite, Sprite _fillSprite, Shader _shader)
        {
            panelMaterial = new Material(_shader) { name = "M_SawmillComboGauge_Panel (Runtime)" };
            panelMaterial.SetFloat(IdMode, 0f);

            barMaterial = new Material(_shader) { name = "M_SawmillComboGauge_Bar (Runtime)" };
            barMaterial.SetFloat(IdMode, 1f);
            barMaterial.SetTexture(IdFillTex, _fillSprite.texture);
            barMaterial.SetVector(IdMainRect, UVRect(_trackSprite));
            barMaterial.SetVector(IdFillRect, UVRect(_fillSprite));
            barMaterial.SetVector(IdSrcSizePx, new Vector4(_trackSprite.rect.width, _trackSprite.rect.height, 0f, 0f));
            barMaterial.SetFloat(IdSegWidth, SegWidth);
            barMaterial.SetFloat(IdSegGap, SegGap);

            panelRenderer = CreateRenderer("Panel", _panelSprite, panelMaterial, PanelSortingOrder);
            barRenderer = CreateRenderer("Bar", _trackSprite, barMaterial, BarSortingOrder);
            barHeightPx = Mathf.RoundToInt(_trackSprite.rect.height);

            ResetAnimation();
            ApplyLayout();
            SetRenderersVisible(false);
        }

        private SpriteRenderer CreateRenderer(string _name, Sprite _sprite, Material _material, int _order)
        {
            GameObject go = new GameObject(_name);
            go.transform.SetParent(transform, false);
            SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = _sprite;
            renderer.sharedMaterial = _material;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.sortingLayerName = SortingLayerName;
            renderer.sortingOrder = _order;
            return renderer;
        }

        private void ApplyLayout()
        {
            barWidthPx = segments * (SegWidth + SegGap) - SegGap + 2;
            barMaterial.SetFloat(IdSegments, segments);
            barMaterial.SetVector(IdSizePx, new Vector4(barWidthPx, barHeightPx, 0f, 0f));

            barRenderer.size = new Vector2(barWidthPx / PixelsPerUnit, barHeightPx / PixelsPerUnit);
            panelRenderer.size = new Vector2((barWidthPx + PanelPadX * 2) / PixelsPerUnit, PanelHeight / PixelsPerUnit);
        }

        // 판 바닥 가운데를 _anchor에 맞춰 픽셀 격자에 놓는다.
        private void PlaceAt(Vector3 _anchor)
        {
            int panelWidthPx = barWidthPx + PanelPadX * 2;
            int left = Mathf.RoundToInt(_anchor.x * PixelsPerUnit) - panelWidthPx / 2;
            int bottom = Mathf.RoundToInt(_anchor.y * PixelsPerUnit);

            SetBottomLeft(panelRenderer, left, bottom, panelWidthPx, PanelHeight);
            SetBottomLeft(barRenderer, left + PanelPadX, bottom + BarBottomInPanel, barWidthPx, barHeightPx);
        }

        private static void SetBottomLeft(SpriteRenderer _renderer, int _left, int _bottom, int _widthPx, int _heightPx)
        {
            Sprite sprite = _renderer.sprite;
            Vector2 pivot = new Vector2(sprite.pivot.x / sprite.rect.width, sprite.pivot.y / sprite.rect.height);
            _renderer.transform.position = new Vector3(
                (_left + pivot.x * _widthPx) / PixelsPerUnit,
                (_bottom + pivot.y * _heightPx) / PixelsPerUnit,
                0f);
        }

        private void Update()
        {
            if (false == bVisible) return;

            float dt = Time.deltaTime;
            if (popAge < float.MaxValue) popAge += dt;
            if (flashAge < float.MaxValue) flashAge += dt;
            if (drainAge < float.MaxValue) drainAge += dt;

            int lit = stack;
            int cooling = -1;
            if (drainAge < DrainDuration && drainFrom > 0)
            {
                lit = Mathf.CeilToInt(drainFrom * (1f - drainAge / DrainDuration));
                cooling = lit;
            }

            barMaterial.SetFloat(IdLit, lit);
            barMaterial.SetFloat(IdCoolIndex, cooling);
            barMaterial.SetFloat(IdPopIndex, popAge < PopDuration ? popIndex : -1);
            barMaterial.SetFloat(IdFlash, flashAge < FlashDuration ? 1f : 0f);
            barMaterial.SetFloat(IdShine, stack >= segments && flashAge >= FlashDuration ? 1f : 0f);
        }

        private void ResetAnimation()
        {
            popIndex = -1;
            popAge = float.MaxValue;
            flashAge = float.MaxValue;
            drainAge = float.MaxValue;
        }

        private void SetRenderersVisible(bool _bVisible)
        {
            if (null != panelRenderer) panelRenderer.enabled = _bVisible;
            if (null != barRenderer) barRenderer.enabled = _bVisible;
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
            if (null != panelMaterial) Destroy(panelMaterial);
            if (null != barMaterial) Destroy(barMaterial);
        }
    }
}
