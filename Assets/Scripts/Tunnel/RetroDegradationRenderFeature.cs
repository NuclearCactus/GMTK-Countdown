using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace GMTKCountdown.Tunnel
{
    public class RetroDegradationRenderFeature : ScriptableRendererFeature
    {
        [System.Serializable]
        public class Settings
        {
            public Material material;
            public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public Settings settings = new Settings();

        class RetroPass : ScriptableRenderPass
        {
            private Settings passSettings;

            public RetroPass(Settings settings)
            {
                passSettings = settings;
                renderPassEvent = settings.renderPassEvent;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                Material mat = passSettings.material;
                if (mat == null)
                {
                    var controller = Object.FindAnyObjectByType<TunnelRetroDegradationController>();
                    if (controller != null)
                        mat = controller.GetMaterial();
                }

                if (mat == null)
                    return;

                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                if (cameraData.cameraType != CameraType.Game && cameraData.cameraType != CameraType.SceneView)
                    return;

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                TextureHandle src = resourceData.activeColorTexture;
                if (!src.IsValid())
                    return;

                RenderTextureDescriptor desc = cameraData.cameraTargetDescriptor;
                desc.depthBufferBits = 0;

                TextureHandle dst = UniversalRenderer.CreateRenderGraphTexture(renderGraph, desc, "_RetroDegradationTexture", false);

                // Pass 1: Apply retro shader filter from activeColorTexture -> dst
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("PSX_CRT_Degradation_Pass", out var passData))
                {
                    passData.src = src;
                    passData.material = mat;

                    builder.UseTexture(src, AccessFlags.Read);
                    builder.SetRenderAttachment(dst, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        context.cmd.SetGlobalTexture("_MainTex", data.src);
                        context.cmd.SetGlobalTexture("_BlitTexture", data.src);
                        Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), data.material, 0);
                    });
                }

                // Pass 2: Blit filtered texture back to activeColorTexture
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("PSX_CRT_CopyBack_Pass", out var passData))
                {
                    passData.src = dst;

                    builder.UseTexture(dst, AccessFlags.Read);
                    builder.SetRenderAttachment(src, 0, AccessFlags.Write);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Blitter.BlitTexture(context.cmd, data.src, new Vector4(1, 1, 0, 0), 0, false);
                    });
                }
            }

            private class PassData
            {
                public TextureHandle src;
                public Material material;
            }
        }

        private RetroPass retroPass;

        public override void Create()
        {
            if (settings.material == null)
            {
                Shader shader = Shader.Find("Custom/PSX_CRT_Degradation");
                if (shader != null)
                    settings.material = new Material(shader);
            }

            retroPass = new RetroPass(settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(retroPass);
        }
    }
}
