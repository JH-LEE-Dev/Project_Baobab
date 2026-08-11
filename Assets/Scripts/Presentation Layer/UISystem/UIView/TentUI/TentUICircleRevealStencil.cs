using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public sealed class TentUICircleRevealStencil : UIBehaviour, IMaterialModifier
{
    private const int StencilId = 64;
    private const int StencilMask = 64;
    private static readonly int StencilPropertyId = Shader.PropertyToID("_Stencil");
    private static readonly int StencilOpPropertyId = Shader.PropertyToID("_StencilOp");
    private static readonly int StencilCompPropertyId = Shader.PropertyToID("_StencilComp");
    private static readonly int StencilReadMaskPropertyId = Shader.PropertyToID("_StencilReadMask");
    private static readonly int StencilWriteMaskPropertyId = Shader.PropertyToID("_StencilWriteMask");
    private static readonly int ColorMaskPropertyId = Shader.PropertyToID("_ColorMask");
    private static readonly int UseUIAlphaClipPropertyId = Shader.PropertyToID("_UseUIAlphaClip");

    private enum StencilRole
    {
        MaskWriter,
        MaskReader
    }

    [SerializeField] private StencilRole role = StencilRole.MaskReader;

    private Graphic graphic;
    private Material modifiedMaterial;
    private Material popMaterial;

    public void ConfigureAsMaskWriter()
    {
        if (role == StencilRole.MaskWriter)
            return;

        role = StencilRole.MaskWriter;
        SetMaterialDirty();
    }

    public void ConfigureAsMaskReader()
    {
        if (role == StencilRole.MaskReader)
            return;

        role = StencilRole.MaskReader;
        SetMaterialDirty();
    }

    public Material GetModifiedMaterial(Material baseMaterial)
    {
        if (!isActiveAndEnabled || baseMaterial == null)
            return baseMaterial;

        RemoveCachedMaterials();

        if (role == StencilRole.MaskWriter)
        {
            modifiedMaterial = StencilMaterial.Add(
                baseMaterial,
                StencilId,
                StencilOp.Replace,
                CompareFunction.Always,
                0,
                255,
                StencilMask);

            popMaterial = StencilMaterial.Add(
                baseMaterial,
                0,
                StencilOp.Zero,
                CompareFunction.Always,
                0,
                255,
                StencilMask);

            PreserveRuntimePropertiesAndStencilState(
                baseMaterial,
                modifiedMaterial,
                StencilId,
                StencilOp.Replace,
                CompareFunction.Always,
                0,
                255,
                StencilMask);

            PreserveRuntimePropertiesAndStencilState(
                baseMaterial,
                popMaterial,
                0,
                StencilOp.Zero,
                CompareFunction.Always,
                0,
                255,
                StencilMask);

            Graphic targetGraphic = GetGraphic();
            if (targetGraphic != null)
            {
                targetGraphic.canvasRenderer.popMaterialCount = 1;
                targetGraphic.canvasRenderer.SetPopMaterial(popMaterial, 0);
            }

            return modifiedMaterial;
        }

        modifiedMaterial = StencilMaterial.Add(
            baseMaterial,
            StencilId,
            StencilOp.Keep,
            CompareFunction.Equal,
            ColorWriteMask.All,
            StencilMask,
            0);

        PreserveRuntimePropertiesAndStencilState(
            baseMaterial,
            modifiedMaterial,
            StencilId,
            StencilOp.Keep,
            CompareFunction.Equal,
            ColorWriteMask.All,
            StencilMask,
            0);

        return modifiedMaterial;
    }

    private static void PreserveRuntimePropertiesAndStencilState(
        Material source,
        Material destination,
        int stencilId,
        StencilOp operation,
        CompareFunction compareFunction,
        ColorWriteMask colorWriteMask,
        int readMask,
        int writeMask)
    {
        if (source == null || destination == null || source == destination)
            return;

        // Some UI shaders provide effect values only at runtime. Preserve every
        // material property, then restore the stencil state owned by this component.
        destination.CopyPropertiesFromMaterial(source);

        bool useAlphaClip = operation != StencilOp.Keep && writeMask > 0;
        destination.SetInt(StencilPropertyId, stencilId);
        destination.SetInt(StencilOpPropertyId, (int)operation);
        destination.SetInt(StencilCompPropertyId, (int)compareFunction);
        destination.SetInt(StencilReadMaskPropertyId, readMask);
        destination.SetInt(StencilWriteMaskPropertyId, writeMask);
        destination.SetInt(ColorMaskPropertyId, (int)colorWriteMask);
        destination.SetInt(UseUIAlphaClipPropertyId, useAlphaClip ? 1 : 0);

        if (useAlphaClip)
            destination.EnableKeyword("UNITY_UI_ALPHACLIP");
        else
            destination.DisableKeyword("UNITY_UI_ALPHACLIP");
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetMaterialDirty();
    }

    protected override void OnDisable()
    {
        RemoveCachedMaterials();
        SetMaterialDirty();
        base.OnDisable();
    }

    protected override void OnDestroy()
    {
        RemoveCachedMaterials();
        base.OnDestroy();
    }

    private Graphic GetGraphic()
    {
        if (graphic == null)
            graphic = GetComponent<Graphic>();

        return graphic;
    }

    private void SetMaterialDirty()
    {
        Graphic targetGraphic = GetGraphic();
        if (targetGraphic != null)
            targetGraphic.SetMaterialDirty();
    }

    private void RemoveCachedMaterials()
    {
        if (modifiedMaterial != null)
        {
            StencilMaterial.Remove(modifiedMaterial);
            modifiedMaterial = null;
        }

        if (popMaterial != null)
        {
            StencilMaterial.Remove(popMaterial);
            popMaterial = null;
        }
    }
}
