using UnityEngine;
using TMPro;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public sealed class TMPPixelOutlineMeshPadding : MonoBehaviour
{
    [SerializeField, Min(0f)] private float outlineTexelPadding = 1f;
    [SerializeField] private bool useMaterialOutlineWidth = true;

    private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
    private TMP_Text text;

    public float OutlineTexelPadding
    {
        get => outlineTexelPadding;
        set
        {
            outlineTexelPadding = Mathf.Max(0f, value);
            Text.SetVerticesDirty();
        }
    }

    public bool UseMaterialOutlineWidth
    {
        get => useMaterialOutlineWidth;
        set
        {
            useMaterialOutlineWidth = value;
            Text.SetVerticesDirty();
        }
    }

    private TMP_Text Text
    {
        get
        {
            if (text == null)
            {
                text = GetComponent<TMP_Text>();
            }

            return text;
        }
    }

    private void OnEnable()
    {
        Text.OnPreRenderText += ExpandTextMesh;
        Text.SetVerticesDirty();
    }

    private void OnDisable()
    {
        if (text != null)
        {
            text.OnPreRenderText -= ExpandTextMesh;
            text.SetVerticesDirty();
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        outlineTexelPadding = Mathf.Max(0f, outlineTexelPadding);
        Text.SetVerticesDirty();
    }
#endif

    private void ExpandTextMesh(TMP_TextInfo textInfo)
    {
        if (!isActiveAndEnabled || textInfo == null || textInfo.characterCount == 0)
        {
            return;
        }

        Texture texture = Text.font != null ? Text.font.atlasTexture : Text.mainTexture;
        if (texture == null || texture.width <= 0 || texture.height <= 0)
        {
            return;
        }

        float padding = ResolvePadding();
        if (padding <= 0f)
        {
            return;
        }

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo character = textInfo.characterInfo[i];
            if (!character.isVisible)
            {
                continue;
            }

            TMP_MeshInfo meshInfo = textInfo.meshInfo[character.materialReferenceIndex];
            ExpandCharacterQuad(
                meshInfo.vertices,
                meshInfo.uvs0,
                meshInfo.uvs2,
                character.vertexIndex,
                texture.width,
                texture.height,
                padding);
        }
    }

    private float ResolvePadding()
    {
        if (!useMaterialOutlineWidth)
        {
            return outlineTexelPadding;
        }

        Material material = Text.fontSharedMaterial;
        if (material == null || !material.HasProperty(OutlineWidthId))
        {
            return outlineTexelPadding;
        }

        return Mathf.Max(outlineTexelPadding, material.GetFloat(OutlineWidthId));
    }

    private static void ExpandCharacterQuad(
        Vector3[] vertices,
        Vector4[] uvs,
        Vector2[] uvBoundsMax,
        int vertexIndex,
        int textureWidth,
        int textureHeight,
        float padding)
    {
        float minX = vertices[vertexIndex].x;
        float maxX = minX;
        float minY = vertices[vertexIndex].y;
        float maxY = minY;
        float minU = uvs[vertexIndex].x;
        float maxU = minU;
        float minV = uvs[vertexIndex].y;
        float maxV = minV;

        for (int i = 1; i < 4; i++)
        {
            Vector3 position = vertices[vertexIndex + i];
            Vector4 uv = uvs[vertexIndex + i];

            minX = Mathf.Min(minX, position.x);
            maxX = Mathf.Max(maxX, position.x);
            minY = Mathf.Min(minY, position.y);
            maxY = Mathf.Max(maxY, position.y);
            minU = Mathf.Min(minU, uv.x);
            maxU = Mathf.Max(maxU, uv.x);
            minV = Mathf.Min(minV, uv.y);
            maxV = Mathf.Max(maxV, uv.y);
        }

        float uvWidth = maxU - minU;
        float uvHeight = maxV - minV;

        if (uvWidth <= 0f || uvHeight <= 0f)
        {
            return;
        }

        if (uvBoundsMax == null || uvBoundsMax.Length < vertexIndex + 4)
        {
            return;
        }

        float localPerTexelX = (maxX - minX) / (uvWidth * textureWidth);
        float localPerTexelY = (maxY - minY) / (uvHeight * textureHeight);
        float positionPaddingX = localPerTexelX * padding;
        float positionPaddingY = localPerTexelY * padding;
        float uvPaddingX = padding / textureWidth;
        float uvPaddingY = padding / textureHeight;

        for (int i = 0; i < 4; i++)
        {
            Vector3 position = vertices[vertexIndex + i];
            Vector4 uv = uvs[vertexIndex + i];

            if (Mathf.Approximately(position.x, minX))
            {
                position.x -= positionPaddingX;
            }
            else if (Mathf.Approximately(position.x, maxX))
            {
                position.x += positionPaddingX;
            }

            if (Mathf.Approximately(position.y, minY))
            {
                position.y -= positionPaddingY;
            }
            else if (Mathf.Approximately(position.y, maxY))
            {
                position.y += positionPaddingY;
            }

            if (Mathf.Approximately(uv.x, minU))
            {
                uv.x -= uvPaddingX;
            }
            else if (Mathf.Approximately(uv.x, maxU))
            {
                uv.x += uvPaddingX;
            }

            if (Mathf.Approximately(uv.y, minV))
            {
                uv.y -= uvPaddingY;
            }
            else if (Mathf.Approximately(uv.y, maxV))
            {
                uv.y += uvPaddingY;
            }

            vertices[vertexIndex + i] = position;
            uv.z = minU;
            uv.w = minV;
            uvs[vertexIndex + i] = uv;
            uvBoundsMax[vertexIndex + i] = new Vector2(maxU, maxV);
        }
    }
}
