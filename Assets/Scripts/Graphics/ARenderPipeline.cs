using Antares.SDF;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;
using static Antares.Graphics.AShaderSpecs;

using UGraphics = UnityEngine.Graphics;

namespace Antares.Graphics
{
    public class ARenderPipeline : RenderPipeline
    {
        private readonly AShaderSpecs _shaderSpecs;

        private readonly ComputeBuffer _rayMarchingParamBuffer;

        private SDFScene _loadedScene;

        private RenderTexture _sceneVolume;

        private RenderTexture _materialVolume;

        private ComputeBuffer _sdfGenerationBuffer;

        public unsafe ARenderPipeline()
        {
            _shaderSpecs = ShaderSpecsInstance;

            _rayMarchingParamBuffer = new ComputeBuffer(1, sizeof(RayMarchingCompute.SDFRayMarchingParameters));

            _loadedScene = null;
            _sceneVolume = null;
            _materialVolume = null;
            _sdfGenerationBuffer = null;
        }

        public void LoadScene(SDFScene scene)
        {
            void ReleaseVolumes()
            {
                if (_sceneVolume)
                    _sceneVolume.Release();
                if (_materialVolume)
                    _materialVolume.Release();
            }

            _loadedScene = scene;

            if (scene == null)
            {
                ReleaseVolumes();
                return;
            }

            // resize textures
            Vector3Int sceneSize = scene.Size;
            if (_sceneVolume == null || _sceneVolume.width != sceneSize.x || _sceneVolume.height != sceneSize.z || _sceneVolume.volumeDepth != sceneSize.y)
            {
                ReleaseVolumes();

                _sceneVolume = CreateRWVolumeRT(GraphicsFormat.R8_SNorm, sceneSize, SceneMipCount);
                _materialVolume = CreateRWVolumeRT(GraphicsFormat.R16G16_UInt, sceneSize / MaterialVolumeScale, 1);
            }

            CommandBuffer cmd = CommandBufferPool.Get();

            // TODO: apply brushes
            {
                SDFBrushCollection brushes = _loadedScene.Brushes;

                var analyticalBrushes = brushes.AnalyticalBrushes;
                var numericalBrushes = brushes.NumericalBrushes;

                int brushSize;
                unsafe
                {
                    brushSize = sizeof(SDFBrushNumerical);
                }

                ComputeBuffer brushBuffer = new ComputeBuffer(numericalBrushes.Count, brushSize);


                brushBuffer.Release();
            }

            // TODO: generate mipmaps
            {
                Vector3Int mipSize = sceneSize;

                UGraphics.ExecuteCommandBuffer(cmd);
            }

            CommandBufferPool.Release(cmd);
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
                        var rmParamData = new NativeArray<RayMarchingCompute.SDFRayMarchingParameters>(1, Allocator.Temp);
                        rmParamData[0] = new RayMarchingCompute.SDFRayMarchingParameters(camera, _loadedScene, invWidth, invHeight);
                        _rayMarchingParamBuffer.SetData(rmParamData);
                        rmParamData.Dispose();
                    }

                    {
                        ComputeShader shader = _shaderSpecs.RayMarchingCS.Shader;
                        int kernel = _shaderSpecs.RayMarchingCS.TiledMarchingKernel;
                        int tiledCountX = width / RayMarchingCompute.TiledMarchingGroupSizeX, tiledCountY = height / RayMarchingCompute.TiledMarchingGroupSizeY;
                        cmdCompute.GetTemporaryRT(ID_TiledRM, new RenderTextureDescriptor(tiledCountX, tiledCountY, RenderTextureFormat.RFloat, depthBufferBits: 0) { enableRandomWrite = true });
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneVolume, _sceneVolume);
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_TiledRM, new RenderTargetIdentifier(ID_TiledRM));
                        cmdCompute.SetComputeBufferParam(shader, kernel, ID_RMParams, _rayMarchingParamBuffer);

                        cmdCompute.DispatchCompute(shader, kernel, tiledCountX, tiledCountY, RayMarchingCompute.RayMarchingGroupSizeZ);
                    }

                    {
                        ComputeShader shader = _shaderSpecs.RayMarchingCS.Shader;
                        int kernel = _shaderSpecs.RayMarchingCS.RayMarchingKernel;
                        RenderTextureDescriptor rmRTDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, depthBufferBits: 0, mipCount: 0) { enableRandomWrite = true };
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneVolume, _sceneVolume);
                        cmdCompute.GetTemporaryRT(ID_SceneRM0, rmRTDesc);
                        cmdCompute.GetTemporaryRT(ID_SceneRM1, rmRTDesc);
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_TiledRM, new RenderTargetIdentifier(ID_TiledRM));
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneRM0, new RenderTargetIdentifier(ID_SceneRM0));
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneRM1, new RenderTargetIdentifier(ID_SceneRM1));
                        cmdCompute.SetComputeBufferParam(shader, kernel, ID_RMParams, _rayMarchingParamBuffer);

                        cmdCompute.DispatchCompute(shader, kernel, width / RayMarchingCompute.RayMarchingGroupSizeX, height / RayMarchingCompute.RayMarchingGroupSizeY, 1);
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
                        context.BeginSubPass(colors, isDepthStencilReadOnly: false);

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
                        context.BeginSubPass(colors, isDepthStencilReadOnly: false);

                    DrawingSettings drawingSettings = new DrawingSettings(new ShaderTagId("Deferred"), sortingSettings);

                    context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

                    context.EndSubPass();
                }

                // combine rasterization and ray marching
                {
                    {
                        var colors = new NativeArray<int>(new int[] { AttachmentIndex_Shading }, Allocator.Temp);
                        var inputs = new NativeArray<int>(new int[] { AttachmentIndex_GBuffer0, AttachmentIndex_Depth }, Allocator.Temp);
                        context.BeginSubPass(colors, inputs, isDepthStencilReadOnly: true);
                        colors.Dispose();
                        inputs.Dispose();
                    }

                    cmd.WaitOnAsyncGraphicsFence(rmFence, SynchronisationStage.PixelProcessing);
#if UNITY_EDITOR
                    Mesh fullscreen = camera.cameraType == CameraType.SceneView ? GetFullScreenSceneViewMesh() : GetFullScreenMesh();
#else
                    Mesh fullscreen = GetFullScreenMesh();
#endif

                    cmd.DrawMesh(fullscreen, Matrix4x4.identity, _shaderSpecs.Deferred.Material);
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
