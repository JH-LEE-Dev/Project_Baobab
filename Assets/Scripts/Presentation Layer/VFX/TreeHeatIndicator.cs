using System;
using UnityEngine;

/// <summary>
/// 나무가 열기를 방출하기 직전, 방출 범위(인접 8타일)를 바닥에 미리 보여주는 예고 인디케이터.
/// InDungeonVFXManager가 ObjectPool로 관리하며, 나무의 자식이 아니라 독립 오브젝트로 둔다
/// (나무 프리팹에는 SortingGroup이 있어 자식으로 넣으면 "Indicators" 정렬 레이어가 무시된다).
///
/// 나무가 열기 카운트다운을 시작하면 바로 붙지만, 처음 _hiddenDuration 동안은 보이지 않다가
/// 남은 _showDuration 동안 나타나 채움이 중심에서 테두리까지 차오른다. 가득 차는 순간이 방출 시점이다.
/// 나무가 먼저 죽거나 재사용되어 카운트다운이 끊기면(TreeObj.HeatSequence 변경) 즉시 사라진다.
/// </summary>
public class TreeHeatIndicator : MonoBehaviour
{
    private const float FadeInDuration = 0.12f;
    // 진행도가 이 값을 넘으면 방출이 임박했음을 알리도록 점멸한다.
    private const float BlinkStartProgress = 0.65f;
    private const float BlinkMinAlpha = 0.55f;
    private const float BlinkStartFrequency = 4f;
    private const float BlinkEndFrequency = 12f;
    // 픽셀 스냅으로 테두리가 쿼드 밖으로 잘리지 않도록 타원 바깥에 두는 여유.
    private const float QuadPadding = 0.125f;

    private static readonly int RadiusXID = Shader.PropertyToID("_RadiusX");
    private static readonly int QuadSizeID = Shader.PropertyToID("_QuadSize");
    private static readonly int ProgressID = Shader.PropertyToID("_Progress");
    private static readonly int AlphaID = Shader.PropertyToID("_Alpha");

    // 모든 인디케이터가 공유하는 1x1 유닛 FullRect 쿼드. 셰이더가 UV 0~1 전체를 쓰므로 Tight 메쉬면 안 된다.
    private static Sprite quadSprite;

    private SpriteRenderer spriteRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Action<TreeHeatIndicator> onFinished;

    private TreeObj tree;
    private int heatSequence;
    private float hiddenDuration;
    private float showDuration;
    private float elapsed;
    private bool bPlaying;

    public void Initialize(Material _material, int _sortingLayerID, int _sortingOrder, Action<TreeHeatIndicator> _onFinished)
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        spriteRenderer.sprite = GetQuadSprite();
        spriteRenderer.sharedMaterial = _material;
        spriteRenderer.sortingLayerID = _sortingLayerID;
        spriteRenderer.sortingOrder = _sortingOrder;
        spriteRenderer.enabled = false;

        propertyBlock = new MaterialPropertyBlock();
        onFinished = _onFinished;
    }

    public void Play(TreeObj _tree, float _hiddenDuration, float _showDuration, float _radiusX)
    {
        tree = _tree;
        heatSequence = _tree.HeatSequence;
        hiddenDuration = Mathf.Max(0f, _hiddenDuration);
        showDuration = Mathf.Max(0.01f, _showDuration);
        elapsed = 0f;
        bPlaying = true;

        float radiusX = Mathf.Max(0.01f, _radiusX);
        Vector2 quadSize = new Vector2(radiusX * 2f + QuadPadding * 2f, radiusX + QuadPadding * 2f);
        transform.position = _tree.transform.position;
        transform.rotation = Quaternion.identity;
        transform.localScale = new Vector3(quadSize.x, quadSize.y, 1f);

        propertyBlock.Clear();
        propertyBlock.SetFloat(RadiusXID, radiusX);
        propertyBlock.SetVector(QuadSizeID, quadSize);
        propertyBlock.SetFloat(ProgressID, 0f);
        propertyBlock.SetFloat(AlphaID, 0f);
        spriteRenderer.SetPropertyBlock(propertyBlock);
        spriteRenderer.enabled = false;
    }

    public void Stop()
    {
        if (!bPlaying) return;

        bPlaying = false;
        tree = null;
        spriteRenderer.enabled = false;

        onFinished?.Invoke(this);
    }

    private void Update()
    {
        if (!bPlaying) return;

        // 카운트다운 도중 나무가 죽거나 풀로 돌아가 재사용되면 예고도 함께 끝낸다.
        if (tree == null || !tree.isActiveAndEnabled || tree.HeatSequence != heatSequence)
        {
            Stop();
            return;
        }

        elapsed += Time.deltaTime;
        if (elapsed < hiddenDuration) return;

        float showElapsed = elapsed - hiddenDuration;
        if (showElapsed >= showDuration)
        {
            Stop();
            return;
        }

        float progress = showElapsed / showDuration;
        float alpha = Mathf.Clamp01(showElapsed / FadeInDuration);

        if (progress > BlinkStartProgress)
        {
            float blinkT = Mathf.InverseLerp(BlinkStartProgress, 1f, progress);
            float frequency = Mathf.Lerp(BlinkStartFrequency, BlinkEndFrequency, blinkT);
            bool bDim = Mathf.Repeat(showElapsed * frequency, 1f) > 0.5f;
            if (bDim) alpha *= BlinkMinAlpha;
        }

        propertyBlock.SetFloat(ProgressID, progress);
        propertyBlock.SetFloat(AlphaID, alpha);
        spriteRenderer.SetPropertyBlock(propertyBlock);
        spriteRenderer.enabled = true;
    }

    private static Sprite GetQuadSprite()
    {
        if (quadSprite != null) return quadSprite;

        Texture2D texture = Texture2D.whiteTexture;
        quadSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            texture.width,
            0,
            SpriteMeshType.FullRect);
        quadSprite.name = "TreeHeatIndicatorQuad";
        return quadSprite;
    }
}
