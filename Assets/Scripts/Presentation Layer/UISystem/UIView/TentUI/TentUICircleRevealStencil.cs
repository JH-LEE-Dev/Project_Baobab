using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Graphic))]
public sealed class TentUICircleRevealStencil : UIBehaviour, IMaterialModifier
{
    private const int StencilId = 8;
    private const int StencilMask = 8;

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

        return modifiedMaterial;
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
