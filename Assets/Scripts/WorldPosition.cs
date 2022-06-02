using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace hickv.Engine.RenderingSystem
{
    public class WorldPosition : ScriptableRendererFeature
    {
        public class Pass : ScriptableRenderPass
        {
            public Material _material;
            RenderTargetIdentifier _rtID;
            int _nameID;

            public Pass()
            {
                this.renderPassEvent = RenderPassEvent.AfterRenderingGbuffer;
                _material = CoreUtils.CreateEngineMaterial(Shader.Find("hickv/WorldSpacePosition"));
            }

            public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
            {
                int width = cameraTextureDescriptor.width;
                int height = cameraTextureDescriptor.height;

                _nameID = Shader.PropertyToID("_WorldSpacePosition");
                _rtID = new RenderTargetIdentifier(_nameID);

                cmd.GetTemporaryRT(_nameID, width, height, 0, FilterMode.Point, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat);
                cmd.SetGlobalTexture(_nameID, _rtID);

                ConfigureTarget(_rtID);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.Clear();

                Blit(cmd, _rtID, _rtID, _material, 0);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            public override void FrameCleanup(CommandBuffer cmd)
            {
                cmd.ReleaseTemporaryRT(_nameID);
            }
        }

        Pass _pass;

        public override void Create()
        {
            _pass = new Pass();
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_pass); ;
        }
    }
}