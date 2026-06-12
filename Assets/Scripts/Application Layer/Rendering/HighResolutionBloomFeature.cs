using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

[Serializable]
public sealed class HighResolutionBloomFeature : ScriptableRendererFeature
{
    [Serializable]
    public sealed class Settings
    {
        [Tooltip("Run the layer bloom pass from this camera.")]
        public string targetCameraName = "PP UI Camera";

        [Tooltip("Objects on this layer are rendered into the high resolution bloom buffer.")]
        public LayerMask bloomLayerMask;

        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        public Material bloomMaterial;

        [Tooltip("Enable this only when the bloom layer is not already drawn by a camera.")]
        public bool compositeLayerColor;

        [Min(0f)] public float threshold = 1f;
        [Range(0f, 1f)] public float softKnee = 0.5f;
        [Min(0f)] public float intensity = 0.75f;
        [Range(0.25f, 6f)] public float radius = 1.5f;
        [Range(0, 4)] public int downsample = 1;
        [Range(1, 8)] public int blurIterations = 4;
        public bool runInSceneView;
    }

    [SerializeField] private Settings settings = new Settings();

    private HighResolutionBloomPass bloomPass;

    public override void Create()
    {
        if (settings.bloomLayerMask == 0)
        {
            int uiPPLayer = LayerMask.NameToLayer("UI_PP");
            if (uiPPLayer >= 0)
            {
                settings.bloomLayerMask = 1 << uiPPLayer;
            }
        }

        bloomPass = new HighResolutionBloomPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.bloomMaterial == null || settings.bloomMaterial.passCount < 4)
        {
            return;
        }

        if (settings.bloomLayerMask == 0)
        {
            return;
        }

        Camera camera = renderingData.cameraData.camera;
        if (camera == null)
        {
            return;
        }

        if (renderingData.cameraData.cameraType == CameraType.SceneView && !settings.runInSceneView)
        {
            return;
        }

        if (renderingData.cameraData.cameraType != CameraType.Game &&
            renderingData.cameraData.cameraType != CameraType.SceneView)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(settings.targetCameraName) &&
            !string.Equals(camera.name, settings.targetCameraName, StringComparison.Ordinal))
        {
            return;
        }

        bloomPass.renderPassEvent = settings.injectionPoint;
        bloomPass.requiresIntermediateTexture = true;
        bloomPass.Setup(settings);
        renderer.EnqueuePass(bloomPass);
    }

    private sealed class HighResolutionBloomPass : ScriptableRenderPass
    {
        private const string DrawLayerPassName = "High Resolution Bloom Draw Layer";
        private const string ThresholdPassName = "High Resolution Bloom Threshold";
        private const string BlurHorizontalPassName = "High Resolution Bloom Blur Horizontal";
        private const string BlurVerticalPassName = "High Resolution Bloom Blur Vertical";
        private const string CompositePassName = "High Resolution Bloom Composite";

        private static readonly int BloomParamsId = Shader.PropertyToID("_BloomParams");
        private static readonly int BloomTextureId = Shader.PropertyToID("_BloomTexture");
        private static readonly int LayerTextureId = Shader.PropertyToID("_LayerTexture");
        private static readonly int CompositeLayerColorId = Shader.PropertyToID("_CompositeLayerColor");
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");

        private static readonly List<ShaderTagId> ShaderTags = new List<ShaderTagId>
        {
            new ShaderTagId("SRPDefaultUnlit"),
            new ShaderTagId("Universal2D"),
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly")
        };

        private static readonly MaterialPropertyBlock SharedPropertyBlock = new MaterialPropertyBlock();

        private Settings settings;

        public void Setup(Settings featureSettings)
        {
            settings = featureSettings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (settings == null || settings.bloomMaterial == null || settings.bloomLayerMask == 0)
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle source = resourceData.cameraColor;
            if (!source.IsValid())
            {
                return;
            }

            TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);
            if (sourceDesc.width <= 0 || sourceDesc.height <= 0)
            {
                return;
            }

            TextureHandle layerTexture = CreateLayerTexture(renderGraph, frameData, sourceDesc);
            TextureHandle bloomTexture = CreateBloomTexture(renderGraph, layerTexture);

            TextureDesc resultDesc = sourceDesc;
            resultDesc.name = "_HighResolutionLayerBloomResult";
            resultDesc.clearBuffer = false;
            TextureHandle result = renderGraph.CreateTexture(resultDesc);

            AddCompositePass(renderGraph, source, layerTexture, bloomTexture, result);
            resourceData.cameraColor = result;
        }

        private TextureHandle CreateLayerTexture(RenderGraph renderGraph, ContextContainer frameData, TextureDesc sourceDesc)
        {
            TextureDesc layerDesc = sourceDesc;
            layerDesc.name = "_HighResolutionLayerBloomSource";
            layerDesc.clearBuffer = true;
            layerDesc.clearColor = Color.clear;
            layerDesc.msaaSamples = MSAASamples.None;
            layerDesc.filterMode = FilterMode.Bilinear;
            layerDesc.depthBufferBits = DepthBits.None;

            TextureHandle layerTexture = renderGraph.CreateTexture(layerDesc);

            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            DrawingSettings drawingSettings = CreateDrawingSettings(
                ShaderTags,
                renderingData,
                cameraData,
                lightData,
                SortingCriteria.CommonTransparent);

            FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all, settings.bloomLayerMask);
            RendererListParams rendererListParams = new RendererListParams(
                renderingData.cullResults,
                drawingSettings,
                filteringSettings);

            using (var builder = renderGraph.AddRasterRenderPass<DrawLayerPassData>(
                       DrawLayerPassName,
                       out DrawLayerPassData passData,
                       profilingSampler))
            {
                passData.rendererList = renderGraph.CreateRendererList(rendererListParams);
                builder.UseRendererList(passData.rendererList);
                builder.SetRenderAttachment(layerTexture, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc(static (DrawLayerPassData data, RasterGraphContext context) =>
                {
                    context.cmd.DrawRendererList(data.rendererList);
                });
            }

            return layerTexture;
        }

        private TextureHandle CreateBloomTexture(RenderGraph renderGraph, TextureHandle layerTexture)
        {
            TextureDesc layerDesc = renderGraph.GetTextureDesc(layerTexture);

            Vector4 bloomParams = new Vector4(
                Mathf.Max(0f, settings.threshold),
                Mathf.Max(0f, settings.threshold * settings.softKnee),
                Mathf.Max(0f, settings.intensity),
                Mathf.Max(0.25f, settings.radius));

            settings.bloomMaterial.SetVector(BloomParamsId, bloomParams);
            settings.bloomMaterial.SetFloat(CompositeLayerColorId, settings.compositeLayerColor ? 1f : 0f);

            TextureDesc bloomDesc = layerDesc;
            int divisor = 1 << Mathf.Clamp(settings.downsample, 0, 4);
            bloomDesc.width = Mathf.Max(1, layerDesc.width / divisor);
            bloomDesc.height = Mathf.Max(1, layerDesc.height / divisor);
            bloomDesc.name = "_HighResolutionLayerBloom";
            bloomDesc.clearBuffer = false;
            bloomDesc.msaaSamples = MSAASamples.None;
            bloomDesc.filterMode = FilterMode.Bilinear;

            TextureHandle bloomA = renderGraph.CreateTexture(bloomDesc);
            renderGraph.AddBlitPass(
                new RenderGraphUtils.BlitMaterialParameters(layerTexture, bloomA, settings.bloomMaterial, 0),
                ThresholdPassName);

            TextureDesc blurDesc = bloomDesc;
            blurDesc.name = "_HighResolutionLayerBloomPingPong";
            TextureHandle bloomB = renderGraph.CreateTexture(blurDesc);

            int iterations = Mathf.Clamp(settings.blurIterations, 1, 8);
            for (int i = 0; i < iterations; i++)
            {
                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(bloomA, bloomB, settings.bloomMaterial, 1),
                    BlurHorizontalPassName);

                renderGraph.AddBlitPass(
                    new RenderGraphUtils.BlitMaterialParameters(bloomB, bloomA, settings.bloomMaterial, 2),
                    BlurVerticalPassName);
            }

            return bloomA;
        }

        private void AddCompositePass(
            RenderGraph renderGraph,
            TextureHandle source,
            TextureHandle layer,
            TextureHandle bloom,
            TextureHandle destination)
        {
            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                       CompositePassName,
                       out CompositePassData passData,
                       profilingSampler))
            {
                passData.source = source;
                passData.layer = layer;
                passData.bloom = bloom;
                passData.material = settings.bloomMaterial;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(layer, AccessFlags.Read);
                builder.UseTexture(bloom, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                {
                    RTHandle sourceHandle = data.source;
                    RTHandle layerHandle = data.layer;
                    RTHandle bloomHandle = data.bloom;

                    SharedPropertyBlock.Clear();
                    SharedPropertyBlock.SetTexture(BlitTextureId, sourceHandle);
                    SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
                    SharedPropertyBlock.SetTexture(LayerTextureId, layerHandle);
                    SharedPropertyBlock.SetTexture(BloomTextureId, bloomHandle);
                    context.cmd.DrawProcedural(
                        Matrix4x4.identity,
                        data.material,
                        3,
                        MeshTopology.Triangles,
                        3,
                        1,
                        SharedPropertyBlock);
                });
            }
        }

        private sealed class DrawLayerPassData
        {
            public RendererListHandle rendererList;
        }

        private sealed class CompositePassData
        {
            public TextureHandle source;
            public TextureHandle layer;
            public TextureHandle bloom;
            public Material material;
        }
    }
}
