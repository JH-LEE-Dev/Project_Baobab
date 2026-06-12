using System;
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
        [Tooltip("Run only on this camera. Leave empty to run on every game camera that uses this renderer.")]
        public string targetCameraName = "PP Main Camera";

        public RenderPassEvent injectionPoint = RenderPassEvent.AfterRenderingPostProcessing;
        public Material bloomMaterial;

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
        bloomPass = new HighResolutionBloomPass();
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.bloomMaterial == null || settings.bloomMaterial.passCount < 4)
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
        bloomPass.Setup(settings);
        renderer.EnqueuePass(bloomPass);
    }

    private sealed class HighResolutionBloomPass : ScriptableRenderPass
    {
        private const string ThresholdPassName = "High Resolution Bloom Threshold";
        private const string BlurHorizontalPassName = "High Resolution Bloom Blur Horizontal";
        private const string BlurVerticalPassName = "High Resolution Bloom Blur Vertical";
        private const string CompositePassName = "High Resolution Bloom Composite";

        private static readonly int BloomParamsId = Shader.PropertyToID("_BloomParams");
        private static readonly int BloomTextureId = Shader.PropertyToID("_BloomTexture");
        private static readonly int BlitTextureId = Shader.PropertyToID("_BlitTexture");
        private static readonly int BlitScaleBiasId = Shader.PropertyToID("_BlitScaleBias");
        private static readonly MaterialPropertyBlock SharedPropertyBlock = new MaterialPropertyBlock();

        private Settings settings;

        public void Setup(Settings featureSettings)
        {
            settings = featureSettings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (settings == null || settings.bloomMaterial == null)
            {
                return;
            }

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            TextureHandle source = resourceData.activeColorTexture;
            if (!source.IsValid())
            {
                return;
            }

            TextureDesc sourceDesc = renderGraph.GetTextureDesc(source);
            if (sourceDesc.width <= 0 || sourceDesc.height <= 0)
            {
                return;
            }

            Vector4 bloomParams = new Vector4(
                Mathf.Max(0f, settings.threshold),
                Mathf.Max(0f, settings.threshold * settings.softKnee),
                Mathf.Max(0f, settings.intensity),
                Mathf.Max(0.25f, settings.radius));

            settings.bloomMaterial.SetVector(BloomParamsId, bloomParams);

            TextureDesc bloomDesc = sourceDesc;
            int divisor = 1 << Mathf.Clamp(settings.downsample, 0, 4);
            bloomDesc.width = Mathf.Max(1, sourceDesc.width / divisor);
            bloomDesc.height = Mathf.Max(1, sourceDesc.height / divisor);
            bloomDesc.name = "_HighResolutionBloom";
            bloomDesc.clearBuffer = false;
            bloomDesc.msaaSamples = MSAASamples.None;
            bloomDesc.filterMode = FilterMode.Bilinear;

            TextureHandle bloomA = renderGraph.CreateTexture(bloomDesc);
            renderGraph.AddBlitPass(
                new RenderGraphUtils.BlitMaterialParameters(source, bloomA, settings.bloomMaterial, 0),
                ThresholdPassName);

            TextureDesc blurDesc = bloomDesc;
            blurDesc.name = "_HighResolutionBloomPingPong";
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

            if (resourceData.isActiveTargetBackBuffer)
            {
                TextureDesc copyDesc = sourceDesc;
                copyDesc.name = "_HighResolutionBloomBackBufferCopy";
                copyDesc.clearBuffer = false;
                TextureHandle sourceCopy = renderGraph.CreateTexture(copyDesc);
                renderGraph.AddBlitPass(source, sourceCopy, Vector2.one, Vector2.zero, passName: "Copy Backbuffer Before Bloom");
                AddCompositePass(renderGraph, sourceCopy, bloomA, source);
                return;
            }

            TextureDesc resultDesc = sourceDesc;
            resultDesc.name = "_HighResolutionBloomResult";
            resultDesc.clearBuffer = false;
            TextureHandle result = renderGraph.CreateTexture(resultDesc);
            AddCompositePass(renderGraph, source, bloomA, result);
            resourceData.cameraColor = result;
        }

        private void AddCompositePass(RenderGraph renderGraph, TextureHandle source, TextureHandle bloom, TextureHandle destination)
        {
            using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                       CompositePassName,
                       out CompositePassData passData,
                       profilingSampler))
            {
                passData.source = source;
                passData.bloom = bloom;
                passData.material = settings.bloomMaterial;

                builder.UseTexture(source, AccessFlags.Read);
                builder.UseTexture(bloom, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                {
                    RTHandle sourceHandle = data.source;
                    RTHandle bloomHandle = data.bloom;

                    SharedPropertyBlock.Clear();
                    SharedPropertyBlock.SetTexture(BlitTextureId, sourceHandle);
                    SharedPropertyBlock.SetVector(BlitScaleBiasId, new Vector4(1f, 1f, 0f, 0f));
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

        private sealed class CompositePassData
        {
            public TextureHandle source;
            public TextureHandle bloom;
            public Material material;
        }
    }
}
