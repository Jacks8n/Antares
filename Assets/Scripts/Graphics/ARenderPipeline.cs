using Antares.SDF;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;
using static Antares.Graphics.AShaderSpecs;

namespace Antares.Graphics
{
    public class ARenderPipeline : RenderPipeline
    {
        private readonly ComputeShader _rayMarchingCS;

        private readonly int _tiledRayMarchingKernel;

        private readonly int _rayMarchingKernel;

        private readonly Material _shadingMat;

        private readonly ComputeBuffer _rayMarchingParamBuffer;

        private SDFScene _loadedScene;

        public unsafe ARenderPipeline(ComputeShader rayMarchingCS, Material shadingMat)
        {
            Debug.Assert(rayMarchingCS);
            Debug.Assert(shadingMat);

            _rayMarchingCS = rayMarchingCS;
            _tiledRayMarchingKernel = _rayMarchingCS.FindKernel("TiledMarching");
            _rayMarchingKernel = _rayMarchingCS.FindKernel("RayMarching");

#if UNITY_EDITOR
            _rayMarchingCS.GetKernelThreadGroupSizes(_tiledRayMarchingKernel, out uint kernelX, out uint kernelY, out uint kernelZ);
            Debug.Assert(kernelX == TiledMarchingGroupSizeX && kernelY == TiledMarchingGroupSizeY && kernelZ == TiledMarchingGroupSizeZ);

            _rayMarchingCS.GetKernelThreadGroupSizes(_rayMarchingKernel, out kernelX, out kernelY, out kernelZ);
            Debug.Assert(kernelX == RayMarchingGroupSizeX && kernelY == RayMarchingGroupSizeY && kernelZ == RayMarchingGroupSizeZ);
#endif

            _shadingMat = shadingMat;

            _rayMarchingParamBuffer = new ComputeBuffer(1, sizeof(SDFRayMarchingParameters));
        }

        public void LoadScene(SDFScene scene)
        {
            _loadedScene = scene;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                return;

            _rayMarchingParamBuffer.Dispose();

            base.Dispose(disposing);
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            if (!_loadedScene)
                return;

            CommandBuffer cmd = CommandBufferPool.Get();
            CommandBuffer cmdCompute = CommandBufferPool.Get();
            cmdCompute.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];

                if (!camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
                    continue;

                int width = camera.pixelWidth, height = camera.pixelHeight;
                float invWidth = 1f / width, invHeight = 1f / height;

#if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView)
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif

                context.SetupCameraProperties(camera);

                // dispatch ray marching
                GraphicsFence rmFence = default;
                {
                    {
                        var rmParamData = new NativeArray<SDFRayMarchingParameters>(1, Allocator.Temp);
                        rmParamData[0] = new SDFRayMarchingParameters(camera, _loadedScene, invWidth, invHeight);
                        _rayMarchingParamBuffer.SetData(rmParamData);
                        rmParamData.Dispose();
                    }

                    {
                        int tiledCountX = width / TiledMarchingGroupSizeX, tiledCountY = height / TiledMarchingGroupSizeY;
                        cmdCompute.GetTemporaryRT(ID_TiledRM, new RenderTextureDescriptor(tiledCountX, tiledCountY, RenderTextureFormat.RFloat, depthBufferBits: 0) { enableRandomWrite = true });
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _tiledRayMarchingKernel, ID_SceneVolume, _loadedScene.Volume);
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _tiledRayMarchingKernel, ID_TiledRM, new RenderTargetIdentifier(ID_TiledRM));
                        cmdCompute.SetComputeBufferParam(_rayMarchingCS, _tiledRayMarchingKernel, ID_RMParams, _rayMarchingParamBuffer);

                        cmdCompute.DispatchCompute(_rayMarchingCS, _tiledRayMarchingKernel, tiledCountX, tiledCountY, TiledMarchingGroupSizeZ);
                    }

                    {
                        RenderTextureDescriptor rmRTDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, depthBufferBits: 0, mipCount: 0) { enableRandomWrite = true };
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_SceneVolume, _loadedScene.Volume);
                        cmdCompute.GetTemporaryRT(ID_SceneRM0, rmRTDesc);
                        cmdCompute.GetTemporaryRT(ID_SceneRM1, rmRTDesc);
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_TiledRM, new RenderTargetIdentifier(ID_TiledRM));
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_SceneRM0, new RenderTargetIdentifier(ID_SceneRM0));
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_SceneRM1, new RenderTargetIdentifier(ID_SceneRM1));
                        cmdCompute.SetComputeBufferParam(_rayMarchingCS, _rayMarchingKernel, ID_RMParams, _rayMarchingParamBuffer);

                        cmdCompute.DispatchCompute(_rayMarchingCS, _rayMarchingKernel, width / RayMarchingGroupSizeX, height / RayMarchingGroupSizeY, 1);
                    }

                    rmFence = cmdCompute.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.PixelProcessing);

                    context.ExecuteCommandBufferAsync(cmdCompute, ComputeQueueType.Default);
                    cmdCompute.Clear();
                }

                // allocate textures
                {
                    cmd.GetTemporaryRT(ID_Shading, new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, depthBufferBits: 0, mipCount: 0));
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }

                using (var attachments = GetAttachments(camera.backgroundColor))
                    context.BeginRenderPass(width, height, samples: 1, attachments, AttachmentIndex_Depth);

                // draw skybox
                if (camera.clearFlags.HasFlag(CameraClearFlags.Skybox))
                {
                    using (var colors = new NativeArray<int>(new int[] { AttachmentIndex_Shading }, Allocator.Temp))
                        context.BeginSubPass(colors, isDepthReadOnly: false);

                    context.DrawSkybox(camera);

                    context.EndSubPass();
                }

                // cull
                CullingResults cullingResults = context.Cull(ref cullingParameters);

                SortingSettings sortingSettings = new SortingSettings(camera) {
                    criteria = SortingCriteria.CommonOpaque
                };
                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

                // draw opaque mesh
                {
                    using (var colors = new NativeArray<int>(new int[] { AttachmentIndex_GBuffer0 }, Allocator.Temp))
                        context.BeginSubPass(colors, isDepthReadOnly: false);

                    DrawingSettings drawingSettings = new DrawingSettings(new ShaderTagId("Deferred"), sortingSettings);

                    context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

                    context.EndSubPass();
                }

                // combine rasterization and ray marching
                {
                    {
                        var colors = new NativeArray<int>(new int[] { AttachmentIndex_Shading }, Allocator.Temp);
                        var inputs = new NativeArray<int>(new int[] { AttachmentIndex_GBuffer0, AttachmentIndex_Depth }, Allocator.Temp);
                        context.BeginSubPass(colors, inputs, isDepthReadOnly: true);
                        colors.Dispose();
                        inputs.Dispose();
                    }

                    cmd.WaitOnAsyncGraphicsFence(rmFence, SynchronisationStage.PixelProcessing);
#if UNITY_EDITOR
                    Mesh fullscreen = camera.cameraType == CameraType.SceneView ? GetFullScreenSceneViewMesh() : GetFullScreenMesh();
#else
                    Mesh fullscreen = GetFullScreenMesh();
#endif

                    cmd.DrawMesh(fullscreen, Matrix4x4.identity, _shadingMat);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    context.EndSubPass();
                }

                context.EndRenderPass();

                // blit to target
                // todo: tonemap
                {
                    bool renderToRT = camera.targetTexture;
                    RenderTargetIdentifier target = renderToRT
                        ? new RenderTargetIdentifier(camera.targetTexture)
                        : new RenderTargetIdentifier(Display.displays[camera.targetDisplay].colorBuffer);

                    if (renderToRT)
                        cmd.Blit(new RenderTargetIdentifier(ID_Shading), target, new Vector2(1f, 1f), Vector2.zero);
                    else
                        cmd.Blit(new RenderTargetIdentifier(ID_Shading), target, ScreenBlitScale, ScreenBlitOffset);
                }

                for (int j = 0; j < ID_NonAttachmentRTs.Length; j++)
                    cmd.ReleaseTemporaryRT(ID_NonAttachmentRTs[j]);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.Submit();

            CommandBufferPool.Release(cmd);
            CommandBufferPool.Release(cmdCompute);
        }
    }
}
