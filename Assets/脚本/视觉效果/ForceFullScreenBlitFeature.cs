using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class ForceFullScreenBlitFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        public Material material;
        public int materialPass = 0;
        public RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;

        public bool skipNonGameCamera = true;
        public bool skipOverlayCamera = true;

        [Tooltip("兼容旧 shader：把 _MainTex/_BaseMap 绑定到相机颜色")]
        public bool bindMainTexAlias = true;
    }

    public Settings settings = new Settings();

    class Pass : ScriptableRenderPass
    {
        readonly Settings s;
        RTHandle temp;

        static readonly int MainTexID = Shader.PropertyToID("_MainTex");
        static readonly int BaseMapID = Shader.PropertyToID("_BaseMap");

        public Pass(Settings settings)
        {
            s = settings;
            renderPassEvent = s.passEvent;
            ConfigureInput(ScriptableRenderPassInput.Color);
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;

            RenderingUtils.ReAllocateIfNeeded(
                ref temp,
                desc,
                FilterMode.Bilinear,
                TextureWrapMode.Clamp,
                name: "_ForceFullScreenBlitTemp"
            );
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (s.material == null) return;

            var camData = renderingData.cameraData;

            if (s.skipNonGameCamera && camData.cameraType != CameraType.Game) return;
#if UNITY_2022_2_OR_NEWER
            if (s.skipOverlayCamera && camData.renderType == CameraRenderType.Overlay) return;
#endif

            var renderer = camData.renderer;
            RenderTargetIdentifier source = renderer.cameraColorTarget;

            var cmd = CommandBufferPool.Get("ForceFullScreenBlit");

            // 关键：旧 shader 往往采样 _MainTex/_BaseMap，而 URP Blit 供的是 _BlitTexture
            // 这里把别名强行绑定到相机颜色，避免采样空纹理直接黑屏
            if (s.bindMainTexAlias)
            {
                cmd.SetGlobalTexture(MainTexID, source);
                cmd.SetGlobalTexture(BaseMapID, source);
            }

            // source -> temp (effect) -> source
            cmd.Blit(source, temp, s.material, s.materialPass);
            cmd.Blit(temp, source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            temp?.Release();
            temp = null;
        }
    }

    Pass pass;

    public override void Create()
    {
        pass = new Pass(settings);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (settings.material == null) return;
        renderer.EnqueuePass(pass);
    }

    protected override void Dispose(bool disposing)
    {
        pass?.Dispose();
        pass = null;
    }
}
