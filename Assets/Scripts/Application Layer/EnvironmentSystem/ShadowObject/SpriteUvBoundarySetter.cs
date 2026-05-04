using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteUvBoundarySetter : MonoBehaviour
{
    private SpriteRenderer sr;
    private MaterialPropertyBlock mpb;
    private static readonly int UvRectId = Shader.PropertyToID("_UvRect");

    void OnEnable()
    {
        sr = GetComponent<SpriteRenderer>();
        mpb = new MaterialPropertyBlock();
        UpdateUvRect();
    }

    // 애니메이션 등으로 스프라이트가 변할 수 있으므로 LateUpdate에서 갱신
    void LateUpdate()
    {
        UpdateUvRect();
    }

    void UpdateUvRect()
    {
        if (sr == null || sr.sprite == null) return;

        // 스프라이트의 텍스처 내 UV 좌표(0~1 범위)를 가져옵니다.
        Rect uv = sr.sprite.textureRect;
        float texWidth = sr.sprite.texture.width;
        float texHeight = sr.sprite.texture.height;

        // x, y, z, w -> minX, minY, maxX, maxY
        Vector4 uvRect = new Vector4(
            uv.x / texWidth,
            uv.y / texHeight,
            (uv.x + uv.width) / texWidth,
            (uv.y + uv.height) / texHeight
        );

        sr.GetPropertyBlock(mpb);
        mpb.SetVector(UvRectId, uvRect);
        sr.SetPropertyBlock(mpb);
    }
}