using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections.Generic;

namespace global_illumination
{
    public class SDFProbeGIRenderFeature : ScriptableRendererFeature
    {
        class Pass : ScriptableRenderPass
        {
            [SerializeField] int _lightID0;
            [SerializeField] int _lightID1;
            [SerializeField] int _occlusionID0;
            [SerializeField] int _occlusionID1;
            [SerializeField] int _gbuffer3ID;
            [SerializeField] private RTHandle _lightRTping;
            [SerializeField] private RTHandle _lightRTpong;
            [SerializeField] private RTHandle _occlusionRTping;
            [SerializeField] private RTHandle _occlusionRTpong;
            [SerializeField] private RTHandle _gbuffer3RT;
            [SerializeField] private Material _sdfProbeMaterial;
            [SerializeField] private SDFProbeGI _sdfProbeGI;

            public Pass()
            {
                this.renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights;
                this._lightID0 = Shader.PropertyToID("_Light0");
                this._lightID1 = Shader.PropertyToID("_Light1");
                this._occlusionID0 = Shader.PropertyToID("_Occlusion0");
                this._occlusionID1 = Shader.PropertyToID("_Occlusion1");
                this._gbuffer3ID = Shader.PropertyToID("_GBuffer3Temp");
                this._sdfProbeMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("hickv/SDFProbe"));
            }

            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                if (_sdfProbeGI == null)
                    _sdfProbeGI = SDFProbeGI.FindObjectOfType<SDFProbeGI>();

                RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                int width = cameraTargetDescriptor.width;
                int height = cameraTargetDescriptor.height;

                _lightRTping = RTHandles.Alloc(new RenderTargetIdentifier(_lightID0), name: "_Light0");
                _lightRTpong = RTHandles.Alloc(new RenderTargetIdentifier(_lightID1), name: "_Light1");
                _occlusionRTping = RTHandles.Alloc(new RenderTargetIdentifier(_occlusionID0), name: "_Occlusion0");
                _occlusionRTpong = RTHandles.Alloc(new RenderTargetIdentifier(_occlusionID1), name: "_Occlusion1");
                _gbuffer3RT = RTHandles.Alloc(new RenderTargetIdentifier(_gbuffer3ID), name: "_GBuffer3Temp");

                cmd.GetTemporaryRT(_lightID0, width, height, 0, FilterMode.Point, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat, 1);
                cmd.GetTemporaryRT(_lightID1, width, height, 0, FilterMode.Point, UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat, 1);
                cmd.GetTemporaryRT(_occlusionID0, width, height, 0, FilterMode.Point, UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm, 1);
                cmd.GetTemporaryRT(_occlusionID1, width, height, 0, FilterMode.Point, UnityEngine.Experimental.Rendering.GraphicsFormat.R8_UNorm, 1);
                cmd.GetTemporaryRT(_gbuffer3ID, cameraTargetDescriptor);
                cmd.SetGlobalTexture(_gbuffer3ID, _gbuffer3RT.nameID);

                ConfigureTarget(_lightRTping);
                ConfigureTarget(_lightRTpong);
                ConfigureTarget(_occlusionRTping);
                ConfigureTarget(_occlusionRTpong);
                ConfigureTarget(_gbuffer3RT);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                if (_sdfProbeGI == null)
                    return;

                SDFProbeGI.ProbesBuffer lightProbesBuffer = _sdfProbeGI.LightProbesBuffer;
                CullingGroup lightProbesCullingGroup = lightProbesBuffer.CullingGroup;
                SDFProbeGI.ProbesBuffer occlusionProbesBuffer = _sdfProbeGI.OcclusionProbesBuffer;
                CullingGroup occlusionProbesCullingGroup = occlusionProbesBuffer.CullingGroup;

                if (lightProbesCullingGroup == null || occlusionProbesCullingGroup == null)
                    return;

                CommandBuffer cmd = CommandBufferPool.Get();
                cmd.Clear();

                int renderedCount = 0;

                // occlusion
                RTHandle currentOcclusionRT = _occlusionRTping;
                {
                    int probesCount = occlusionProbesBuffer.Probes.Count;
                    List<Material> materials = occlusionProbesBuffer.Materials;
                    for (int i = 0; i < probesCount; i++)
                    {
                        bool isVisible = occlusionProbesCullingGroup.IsVisible(i);
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                            isVisible = true;
#endif
                        if (isVisible)
                        {
                            if (i == 0)
                            {
                                Blit(cmd, _occlusionRTping, _occlusionRTping, materials[i], 0);
                                currentOcclusionRT = _occlusionRTping;
                            }
                            else if ((i + 1) % 2 == 0)
                            {
                                Blit(cmd, _occlusionRTping, _occlusionRTpong, materials[i], 0);
                                currentOcclusionRT = _occlusionRTpong;
                            }
                            else
                            {
                                Blit(cmd, _occlusionRTpong, _occlusionRTping, materials[i], 0);
                                currentOcclusionRT = _occlusionRTping;
                            }
                            renderedCount++;
                        }
                    }
                }

                // light
                RTHandle currentLightRT = _lightRTping;
                {
                    int probesCount = lightProbesBuffer.Probes.Count;
                    List<Material> materials = lightProbesBuffer.Materials;
                    for (int i = 0; i < probesCount; i++)
                    {
                        bool isVisible = lightProbesCullingGroup.IsVisible(i);
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                            isVisible = true;
#endif
                        if (isVisible)
                        {
                            if (i == 0)
                            {
                                Blit(cmd, _lightRTping, _lightRTping, materials[i], 2);
                                currentLightRT = _lightRTping;
                            }
                            else if ((i + 1) % 2 == 0)
                            {
                                Blit(cmd, _lightRTping, _lightRTpong, materials[i], 2);
                                currentLightRT = _lightRTpong;
                            }
                            else
                            {
                                Blit(cmd, _lightRTpong, _lightRTping, materials[i], 2);
                                currentLightRT = _lightRTping;
                            }
                            renderedCount++;
                        }
                    }
                }

                // merge occlusion
                if (renderedCount > 0)
                {
                    Blit(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, _gbuffer3RT);
                    Blit(cmd, currentOcclusionRT, renderingData.cameraData.renderer.cameraColorTargetHandle, _sdfProbeMaterial, 1);
                    Blit(cmd, currentLightRT, renderingData.cameraData.renderer.cameraColorTargetHandle, _sdfProbeMaterial, 3);
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
                RTHandles.Release(_lightRTping);
                RTHandles.Release(_lightRTpong);
                RTHandles.Release(_occlusionRTping);
                RTHandles.Release(_occlusionRTpong);
                RTHandles.Release(_gbuffer3RT);
                cmd.ReleaseTemporaryRT(_lightID0);
                cmd.ReleaseTemporaryRT(_lightID1);
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
