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

        private readonly ComputeBuffer _constantBuffer;

        private RenderTexture _sceneVolume;

        private RenderTexture _materialVolume;

        private SDFScene _loadedScene;

        public ARenderPipeline(AShaderSpecs shaderSpecs)
        {
            _shaderSpecs = shaderSpecs;

            _sceneVolume = null;
            _materialVolume = null;
            _loadedScene = null;

            _constantBuffer = new ComputeBuffer(_shaderSpecs.ConstantBufferCount, _shaderSpecs.ConstantBufferStride, ComputeBufferType.Constant, ComputeBufferMode.SubUpdates);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                return;

            LoadScene(null);
            _constantBuffer.Release();

            base.Dispose(disposing);
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

            if (scene == null || scene.IsEmpty)
            {
                ReleaseVolumes();
                return;
            }

            // resize textures
            Vector3Int sceneSize = _loadedScene.Size;
            Vector3Int matVolumeSize = sceneSize / SDFGenerationCompute.MatVolumeScale;
            if (_sceneVolume == null || _sceneVolume.width != sceneSize.x || _sceneVolume.height != sceneSize.z || _sceneVolume.volumeDepth != sceneSize.y)
            {
                ReleaseVolumes();

                _sceneVolume = CreateRWVolumeRT(GraphicsFormat.R8_SNorm, sceneSize, SceneMipCount);
                _materialVolume = CreateRWVolumeRT(GraphicsFormat.R16_UInt, matVolumeSize, SceneMipCount);
            }

            CommandBuffer cmd = CommandBufferPool.Get();

            // upload brushes
            ComputeBuffer brushBuffer, brushParameterBuffer;
            var brushCollection = _loadedScene.BrusheCollection;
            unsafe
            {
                var brushes = brushCollection.Brushes;
                var brushParams = brushCollection.BrushParameters;

                brushBuffer = new ComputeBuffer(brushes.Length, sizeof(SDFGenerationCompute.SDFBrush), ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);
                var mappedBrushes = brushBuffer.BeginWrite<SDFGenerationCompute.SDFBrush>(0, brushes.Length);
                for (int i = 0; i < brushes.Length; i++)
                    mappedBrushes[i] = new SDFGenerationCompute.SDFBrush(brushes[i].Property, brushes[i].ParameterCount, brushes[i].ParameterOffset);
                brushBuffer.EndWrite<SDFGenerationCompute.SDFBrush>(brushes.Length);

                brushParameterBuffer = new ComputeBuffer(brushParams.Length, sizeof(float), ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);
                var mappedBrushParams = brushParameterBuffer.BeginWrite<float>(0, brushParams.Length);
                mappedBrushParams.CopyFrom(brushParams);
                brushParameterBuffer.EndWrite<float>(brushParams.Length);
            }

            // allocate indirect buffers
            ComputeBuffer dispatchCoordsBuffer, brushIndicesBuffer, mipDispatchesBuffer;
            {
                int matVolumeGridCount = matVolumeSize.x * matVolumeSize.y * matVolumeSize.z;
                dispatchCoordsBuffer = GetIndirectBuffer(matVolumeGridCount);
                brushIndicesBuffer = GetUShortAppendBuffer(matVolumeGridCount * SDFGenerationCompute.MaxBrushCountFactor);
                mipDispatchesBuffer = GetIndirectBuffer(matVolumeGridCount / 8);
            }

            {
                SDFGenerationCompute sdfGeneration = _shaderSpecs.SDFGenerationCS;
                ComputeShader shader = sdfGeneration.Shader;

                // set global params
                {
                    int parameterOffset = sdfGeneration.SDFGenerationParametersOffset;

                    SetCBufferSegment(cmd, shader, ID_SDFGenerationParameters, parameterOffset, new SDFGenerationCompute.SDFGenerationParameters(_loadedScene));
                }

                // generate material volume mip 0
                int kernel = sdfGeneration.GenerateMatVolumeKernel;
                {
                    SetMaterialVolume(cmd, shader, kernel);
                    cmd.SetComputeTextureParam(shader, kernel, ID_BrushAtlas, brushCollection.NumericalBrushAtlas);
                    cmd.SetComputeBufferParam(shader, kernel, ID_SDFBrushes, brushBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, ID_SDFBrushParameters, brushParameterBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, ID_DispatchCoordsBuffer, dispatchCoordsBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, ID_BrushIndices, brushIndicesBuffer);

                    Vector3Int dispatchSize = matVolumeSize / SDFGenerationCompute.GenerateMatVolumeKernelSize;
                    cmd.DispatchCompute(shader, kernel, dispatchSize.x, dispatchSize.y, dispatchSize.z);
                }

                // generate scene volume mip 0(async)
                kernel = sdfGeneration.GenerateSceneVolumeKernel;
                {
                    SetMaterialVolume(cmd, shader, kernel);
                    SetSceneVolume(cmd, shader, kernel);
                    cmd.SetComputeBufferParam(shader, kernel, ID_SDFBrushes, brushBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, ID_SDFBrushParameters, brushParameterBuffer);
                    cmd.SetComputeTextureParam(shader, kernel, ID_BrushAtlas, brushCollection.NumericalBrushAtlas);
                    cmd.SetComputeBufferParam(shader, kernel, ID_BrushIndices, brushIndicesBuffer);
                    DispatchIndirect(cmd, shader, kernel, ID_DispatchCoordsBuffer, dispatchCoordsBuffer);
                }

                // generate material volume non-zero mips
                // generate scene volume non-zero mips(async)
                {
                    SetMaterialVolume(cmd, shader, sdfGeneration.GenerateMipDispatchKernel);
                    SetSceneVolume(cmd, shader, sdfGeneration.GenerateMipMapKernel);

                    Vector3Int dispatchSize = matVolumeSize / SDFGenerationCompute.GenerateMipDispatchKernelSize;
                    for (int i = 0; i < SceneMipCount - 1; i++)
                    {
                        kernel = sdfGeneration.GenerateMipDispatchKernel;
                        cmd.SetComputeIntParam(shader, ID_VolumeMipLevel, i);
                        cmd.SetComputeTextureParam(shader, kernel, ID_MipVolume, _materialVolume, i + 1);
                        cmd.SetComputeBufferParam(shader, kernel, ID_MipDispatchesBuffer, mipDispatchesBuffer);

                        dispatchSize /= 2;
                        cmd.DispatchCompute(shader, kernel, dispatchSize.x, dispatchSize.y, dispatchSize.z);

                        kernel = sdfGeneration.GenerateMipMapKernel;
                        cmd.SetComputeTextureParam(shader, kernel, ID_MipVolume, _sceneVolume, i + 1);
                        DispatchIndirect(cmd, shader, kernel, ID_MipDispatchesBuffer, mipDispatchesBuffer);

                        if (i < SceneMipCount - 2)
                            ResetIndirectBuffer(cmd, mipDispatchesBuffer);
                    }
                }
            }

            UGraphics.ExecuteCommandBuffer(cmd);

            brushBuffer.Release();
            brushParameterBuffer.Release();
            brushIndicesBuffer.Release();
            dispatchCoordsBuffer.Release();
            mipDispatchesBuffer.Release();
            CommandBufferPool.Release(cmd);
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

#if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView)
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif

                context.SetupCameraProperties(camera);

                int width = camera.pixelWidth, height = camera.pixelHeight;
                float invWidth = 1f / width, invHeight = 1f / height;

                // dispatch ray marching
                // todo: don't know why cbuffer is not set
                GraphicsFence rmFence;
                {
                    RayMarchingCompute rayMarching = _shaderSpecs.RayMarchingCS;
                    ComputeShader shader = rayMarching.Shader;

                    // set cbuffer
                    // todo: don't know why this doesn't set data though there's no error
                    // answer: f**king unity didn't mark computebuffer.setdata as deprecated, it does literally noting
                    int tiledCountX = width / RayMarchingCompute.TiledMarchingGroupSizeX, tiledCountY = height / RayMarchingCompute.TiledMarchingGroupSizeY;
                    {
                        int parameterOffset = rayMarching.RayMarchingParametersOffset;
                        var parameters = new RayMarchingCompute.SDFRayMarchingParameters(camera, _loadedScene, width, height, invWidth, invHeight);

                        SetCBufferSegment(cmdCompute, shader, ID_RayMarchingParameters, parameterOffset, parameters);
                    }

                    // tiled marching
                    int kernel = rayMarching.TiledMarchingKernel;
                    {
                        cmdCompute.GetTemporaryRT(ID_TiledRM, new RenderTextureDescriptor(tiledCountX, tiledCountY, RenderTextureFormat.RGFloat, depthBufferBits: 0) { enableRandomWrite = true });
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneVolume, _sceneVolume);
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_TiledRM, new RenderTargetIdentifier(ID_TiledRM));
                        cmdCompute.DispatchCompute(shader, kernel, tiledCountX, tiledCountY, RayMarchingCompute.RayMarchingGroupSizeZ);
                    }

                    // per pixel marching
                    kernel = rayMarching.RayMarchingKernel;
                    {
                        RenderTextureDescriptor rmRTDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, depthBufferBits: 0, mipCount: 0) { enableRandomWrite = true };
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneVolume, _sceneVolume);
                        cmdCompute.GetTemporaryRT(ID_SceneRM0, rmRTDesc);
                        cmdCompute.GetTemporaryRT(ID_SceneRM1, rmRTDesc);
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_TiledRM, new RenderTargetIdentifier(ID_TiledRM));
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneRM0, new RenderTargetIdentifier(ID_SceneRM0));
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneRM1, new RenderTargetIdentifier(ID_SceneRM1));
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
                        using var colors = new NativeArray<int>(new int[] { AttachmentIndex_Shading }, Allocator.Temp);
                        using var inputs = new NativeArray<int>(new int[] { AttachmentIndex_GBuffer0, AttachmentIndex_Depth }, Allocator.Temp);
                        context.BeginSubPass(colors, inputs, isDepthStencilReadOnly: true);
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
                // todo: tone mapping, post-processings
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

        private unsafe void SetCBufferSegment<T>(CommandBuffer cmd, ComputeShader shader, int cbufferID, int offsetInBytes, T data) where T : unmanaged
        {
            int size = sizeof(T);

            var mapped = _constantBuffer.BeginWrite<byte>(offsetInBytes, size);
            mapped.ReinterpretStore(0, data);
            _constantBuffer.EndWrite<byte>(size);

            cmd.SetComputeConstantBufferParam(shader, cbufferID, _constantBuffer, offsetInBytes, size);
        }

        private void SetSceneVolume(CommandBuffer cmd, ComputeShader shader, int kernel, int mipLevel = 0) => cmd.SetComputeTextureParam(shader, kernel, ID_SceneVolume, _sceneVolume, mipLevel);

        private void SetMaterialVolume(CommandBuffer cmd, ComputeShader shader, int kernel, int mipLevel = 0) => cmd.SetComputeTextureParam(shader, kernel, ID_MaterialVolume, _materialVolume, mipLevel);

        private ComputeBuffer GetIndirectBuffer(int count)
        {
            ComputeBuffer buffer = new ComputeBuffer(count + 4, sizeof(uint), ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.SubUpdates);

            var mappedBuffer = buffer.BeginWrite<uint>(0, 4);
            mappedBuffer[0] = 4;
            mappedBuffer[1] = 8192;
            mappedBuffer[2] = 0;
            mappedBuffer[3] = 1;
            buffer.EndWrite<uint>(4);

            return buffer;
        }

        private void ResetIndirectBuffer(CommandBuffer cmd, ComputeBuffer buffer)
        {
            using var header = new NativeArray<uint>(new uint[] { 4, 8192, 0, 1 }, Allocator.Temp);
            cmd.SetComputeBufferData(buffer, header);
        }

        private ComputeBuffer GetUShortAppendBuffer(int count)
        {
            ComputeBuffer buffer = new ComputeBuffer(count / 2 + 1, sizeof(uint), ComputeBufferType.Structured, ComputeBufferMode.SubUpdates);

            var mappedBuffer = buffer.BeginWrite<ushort>(0, 1);
            mappedBuffer[0] = 1;
            buffer.EndWrite<ushort>(1);

            return buffer;
        }

        private void DispatchIndirect(CommandBuffer cmd, ComputeShader shader, int kernel, int bufferID, ComputeBuffer indirectBuffer)
        {
            cmd.SetComputeBufferParam(shader, kernel, bufferID, indirectBuffer);
            cmd.DispatchCompute(shader, kernel, indirectBuffer, 1);
        }
    }
}
