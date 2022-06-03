using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace global_illumination
{
    public class LightProbeGIRenderFeature : ScriptableRendererFeature
    {
        class Pass : ScriptableRenderPass
        {
            private RTHandle _giColorTarget;
            private LightProbeGI _lightProbeGI;

            public Pass()
            {
                this.renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights;
            }

            // This method is called before executing the render pass.
            // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
            // When empty this render pass will render to the active camera render target.
            // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
            // The render pipeline will ensure target setup and clearing happens in a performant manner.
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                _giColorTarget = RTHandles.Alloc(renderingData.cameraData.cameraTargetDescriptor);
                if (_lightProbeGI == null)
                    _lightProbeGI = LightProbeGI.FindObjectOfType<LightProbeGI>();
            }

            // Here you can implement the rendering logic.
            // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
            // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
            // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    return;
#endif

                if (_lightProbeGI == null)
                    return;

                CullingGroup tetraCulling = _lightProbeGI.TetrahedraCullingGroup;
                if (tetraCulling == null)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.Clear();

                int tetraCount = _lightProbeGI.TetrahedraCount;
                Material[] tetraMats = _lightProbeGI.TetrahedraMaterials;
                for (int i = 0; i < tetraCount; i++)
                {
                    if (tetraCulling.IsVisible(i))
                        Blit(cmd, _giColorTarget, renderingData.cameraData.renderer.cameraColorTargetHandle, tetraMats[i], 0);
                }

                context.ExecuteCommandBuffer(cmd);

                CommandBufferPool.Release(cmd);
            }

            // Cleanup any allocated resources that were created during the execution of this render pass.
            public override void FrameCleanup(CommandBuffer cmd)
            {
                RTHandles.Release(_giColorTarget);
            }
        }

        Pass _pass;

        public override void Create()
        {
            _pass = new Pass();
            name = "Light Probe Global Illumination";
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_pass);
        }
    }
}
