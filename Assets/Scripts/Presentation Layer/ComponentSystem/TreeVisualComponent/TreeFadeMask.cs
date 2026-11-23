using UnityEngine;

/// <summary>
/// 반투명 나무의 밑동 가림 마스크. TreeVisualComponent가 반투명해질 때 수관 렌더러 아래에 만들고 켜고 끈다.
///
/// 수관·밑동을 따로 반투명하게 그리면 수관 뒤로 올라온 밑동이 수관을 뚫고 비쳐 보인다.
/// 이 렌더러는 밑동보다 먼저(외곽선 스텐실 쓰기보다도 먼저) 수관과 같은 그림을 색 없이 그려 스텐실 64번 비트를 찍는다.
/// 밑동 재질(Comp Equal, ReadMask 64)은 찍힌 칸을 건너뛰고, 바로 뒤에 그려지는 수관(WriteMask 96)이 같은 칸에서 비트를 지운다.
/// 그래서 수관이 덮는 칸에는 수관만, 나머지 칸에는 밑동이 예전과 같은 투명도로 그려진다.
///
/// 켜져 있는 동안 매 프레임 끝에 수관의 그림·뒤집기·색(알파)·프로퍼티 블록(바람 흔들림 값 등)·켜짐을 그대로 따라 해서
/// 수관과 같은 칸에 찍히게 한다(같은 셰이더·같은 변환이라 흔들림·픽셀 맞춤도 같다).
/// </summary>
public sealed class TreeFadeMask : MonoBehaviour
{
    // 밑동 정렬 순서에서 뺀 값. 외곽선 스텐실 쓰기(밑동 -2, -1)보다 먼저 그린다.
    private const int SortingOrderOffset = -3;

    private SpriteRenderer maskRenderer;
    private SpriteRenderer source;
    private SpriteRenderer bottom;
    private MaterialPropertyBlock block;
    // 수관의 블록을 베끼기 전에, 이 나무가 그리기 직전으로 미뤄 둔 값(열기·점멸)을 먼저 써넣게 한다.
    private TreeVisualComponent owner;

    public void Bind(SpriteRenderer _maskRenderer, SpriteRenderer _source, SpriteRenderer _bottom, TreeVisualComponent _owner)
    {
        owner = _owner;
        maskRenderer = _maskRenderer;
        source = _source;
        bottom = _bottom;
        block = new MaterialPropertyBlock();
    }

    private void LateUpdate()
    {
        Sync();
    }

    public void Sync()
    {
        if (maskRenderer == null || source == null || bottom == null) return;

        bool bVisible = source.enabled && source.gameObject.activeInHierarchy && source.sprite != null
            && bottom.enabled && bottom.gameObject.activeInHierarchy;
        if (maskRenderer.enabled != bVisible) maskRenderer.enabled = bVisible;
        if (false == bVisible) return;

        if (maskRenderer.sprite != source.sprite) maskRenderer.sprite = source.sprite;
        maskRenderer.flipX = source.flipX;
        maskRenderer.flipY = source.flipY;
        // 알파까지 같아야 셰이더의 clip(알파 0.01 미만 버림)이 수관과 같은 칸에서 갈린다.
        maskRenderer.color = source.color;
        maskRenderer.sortingLayerID = bottom.sortingLayerID;
        maskRenderer.sortingOrder = bottom.sortingOrder + SortingOrderOffset;

        if (owner is object) owner.FlushDeferredMaterial();

        if (source.HasPropertyBlock())
        {
            source.GetPropertyBlock(block);
            maskRenderer.SetPropertyBlock(block);
        }
        else if (maskRenderer.HasPropertyBlock())
        {
            maskRenderer.SetPropertyBlock(null);
        }
    }
}
