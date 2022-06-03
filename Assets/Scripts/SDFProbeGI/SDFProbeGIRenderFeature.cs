using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace global_illumination
{
    public class SDFProbeGIRenderFeature : ScriptableRendererFeature
    {
        class Pass : ScriptableRenderPass
        {
            [SerializeField] int _occlusionID0;
            [SerializeField] int _occlusionID1;
            [SerializeField] int _gbuffer3ID;
            [SerializeField] private RTHandle _blackRT;
            [SerializeField] private RTHandle _occlusionRTping;
            [SerializeField] private RTHandle _occlusionRTpong;
            [SerializeField] private RTHandle _gbuffer3RT;
            [SerializeField] private Material _occlusionMaterial;
            [SerializeField] private SDFProbeGI _sdfProbeGI;

            public Pass()
            {
                this.renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights;
                this._occlusionID0 = Shader.PropertyToID("_Occlusion0");
                this._occlusionID1 = Shader.PropertyToID("_Occlusion1");
                this._gbuffer3ID = Shader.PropertyToID("_GBuffer3Temp");
                this._occlusionMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("hickv/SDFProbe"));
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (_sdfProbeGI == null)
                    _sdfProbeGI = SDFProbeGI.FindObjectOfType<SDFProbeGI>();

                RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                int width = cameraTargetDescriptor.width;
                int height = cameraTargetDescriptor.height;

                _occlusionRTping = RTHandles.Alloc(new RenderTargetIdentifier(_occlusionID0), name: "_Occlusion0");
                _occlusionRTpong = RTHandles.Alloc(new RenderTargetIdentifier(_occlusionID1), name: "_Occlusion1");
                _gbuffer3RT = RTHandles.Alloc(new RenderTargetIdentifier(_gbuffer3ID), name: "_GBuffer3Temp");

                cmd.GetTemporaryRT(_occlusionID0, width, height, 0, FilterMode.Point, UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm, 1);
                cmd.GetTemporaryRT(_occlusionID1, width, height, 0, FilterMode.Point, UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm, 1);
                cmd.GetTemporaryRT(_gbuffer3ID, cameraTargetDescriptor);
                cmd.SetGlobalTexture(_gbuffer3ID, _gbuffer3RT.nameID);

                ConfigureTarget(_occlusionRTping);
                ConfigureTarget(_occlusionRTpong);
                ConfigureTarget(_gbuffer3RT);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_sdfProbeGI == null)
                    return;

                if (_sdfProbeGI.LightProbesBuffer.CullingGroup == null || _sdfProbeGI.OcclusionProbesBuffer.CullingGroup == null)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.Clear();

                // occlusion
                RTHandle currentRT = _occlusionRTping;
                int renderedCount = 0;
                for (int i = 0; i < _sdfProbeGI.OcclusionProbesBuffer.Probes.Count; i++)
                {
                    bool isVisible = _sdfProbeGI.OcclusionProbesBuffer.CullingGroup.IsVisible(i);
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                        isVisible = true;
#endif

                    if (isVisible)
                    {
                        if (i == 0)
                        {
                            Blit(cmd, _occlusionRTping, _occlusionRTping, _sdfProbeGI.OcclusionProbesBuffer.Materials[i], 0);
                            currentRT = _occlusionRTping;
                        }
                        else if ((i + 1) % 2 == 0)
                        {
                            Blit(cmd, _occlusionRTping, _occlusionRTpong, _sdfProbeGI.OcclusionProbesBuffer.Materials[i], 0);
                            currentRT = _occlusionRTpong;
                        }
                        else
                        {
                            Blit(cmd, _occlusionRTpong, _occlusionRTping, _sdfProbeGI.OcclusionProbesBuffer.Materials[i], 0);
                            currentRT = _occlusionRTping;
                        }
                        renderedCount++;
                    }
                }

                if (renderedCount > 0)
                {
                    Blit(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, _gbuffer3RT);
                    Blit(cmd, currentRT, renderingData.cameraData.renderer.cameraColorTargetHandle, _occlusionMaterial, 1);
                }

                // light
                // for (int i = 0; i < _sdfProbeGI.LightProbesBuffer.Probes.Count; i++)
                // {
                //     if (_sdfProbeGI.LightProbesBuffer.CullingGroup.IsVisible(i))
                //         Blit(cmd, _occlusionRT0, renderingData.cameraData.renderer.cameraColorTargetHandle, _sdfProbeGI.LightProbesBuffer.Materials[i], 0);
                // }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            // Cleanup any allocated resources that were created during the execution of this render pass.
            public override void FrameCleanup(CommandBuffer cmd)
            {
                RTHandles.Release(_occlusionRTping);
                RTHandles.Release(_occlusionRTpong);
                RTHandles.Release(_gbuffer3RT);
                cmd.ReleaseTemporaryRT(_occlusionID0);
                cmd.ReleaseTemporaryRT(_occlusionID1);
                cmd.ReleaseTemporaryRT(_gbuffer3ID);
            }
        }

        Pass _pass;

        public override void Create()
        {
            _pass = new Pass();
            name = "SDF Probe Global Illumination";
        }

        // Here you can inject one or multiple render passes in the renderer.
        // This method is called when setting up the renderer once per-camera.
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            renderer.EnqueuePass(_pass);
        }
    }
}
