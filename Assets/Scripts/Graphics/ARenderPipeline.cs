using System;
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

        private SDFScene _loadedScene;

        private RenderTexture _sceneVolume;

        private RenderTexture _materialVolume;

        public ARenderPipeline()
        {
            _shaderSpecs = ShaderSpecsInstance;

            _loadedScene = null;
            _sceneVolume = null;
            _materialVolume = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                return;

            if (_sceneVolume)
                _sceneVolume.Release();
            if (_materialVolume)
                _materialVolume.Release();

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

            if (scene == null)
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
                _materialVolume = CreateRWVolumeRT(GraphicsFormat.R16_UInt, matVolumeSize, 1);
            }

            CommandBuffer cmd = CommandBufferPool.Get();

            // upload brushes
            ComputeBuffer brushBuffer, brushParameterBuffer;
            unsafe
            {
                var brushes = _loadedScene.BrusheCollection.Brushes;
                var brushParams = _loadedScene.BrusheCollection.BrushParameters;

                brushBuffer = new ComputeBuffer(brushes.Length, sizeof(SDFGenerationCompute.SDFBrush), ComputeBufferType.Structured);
                var mappedBrushes = brushBuffer.BeginWrite<SDFGenerationCompute.SDFBrush>(0, brushes.Length);
                for (int i = 0; i < brushes.Length; i++)
                    mappedBrushes[i] = new SDFGenerationCompute.SDFBrush(brushes[i].Brush, (uint)brushes[i].Offset);
                brushBuffer.EndWrite<SDFGenerationCompute.SDFBrush>(brushes.Length);

                brushParameterBuffer = new ComputeBuffer(brushParams.Length, sizeof(float), ComputeBufferType.Structured);
                var mappedBrushParams = brushParameterBuffer.BeginWrite<float>(0, brushParams.Length);
                brushParams.CopyTo(mappedBrushParams);
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

            // apply brushes
            {
                SDFGenerationCompute sdfGeneration = ShaderSpecsInstance.SDFGenerationCS;
                ComputeShader shader = sdfGeneration.Shader;

                // set global params
                {
                    float gridSize = _loadedScene.GridWorldSize;
                    float sdfBand = gridSize * 4f;
                    const int tileSizeInScene = SDFGenerationCompute.MatVolumeScale * SDFGenerationCompute.MatVolumeTileSize;
                    Vector4 brushCullRadius = new Vector4(
                        gridSize * tileSizeInScene + sdfBand,
                        gridSize * SDFGenerationCompute.MatVolumeScale + sdfBand,
                        gridSize + sdfBand);
                    cmd.SetComputeVectorParam(shader, ID_BrushCullRadius, brushCullRadius);
                    cmd.SetComputeMatrixParam(shader, ID_SceneToWorld, _loadedScene.SceneToWorld);
                }

                // generate material volume mip 0
                int kernel = sdfGeneration.GenerateMatVolumeKernel;
                {
                    SetMaterialVolume(cmd, shader, kernel);
                    cmd.SetComputeBufferParam(shader, kernel, ID_SDFBrushes, brushBuffer);
                    cmd.SetComputeIntParam(shader, ID_SDFBrushCount, brushBuffer.count);
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
                    cmd.SetComputeBufferParam(shader, kernel, ID_BrushIndices, brushIndicesBuffer);
                    DispatchIndirect(cmd, shader, kernel, ID_DispatchCoordsBuffer, dispatchCoordsBuffer);
                }

                // generate material volume non-zero mips
                // generate scene volume non-zero mips(async)
                {
                    SetMaterialVolume(cmd, shader, sdfGeneration.GenerateMipDispatchKernel);

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
                            ResetIndirectBuffer(mipDispatchesBuffer);
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
            // todo: refactor setting ray marching parameter

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

        private void SetSceneVolume(CommandBuffer cmd, ComputeShader shader, int kernel) => cmd.SetComputeTextureParam(shader, kernel, ID_SceneVolume, _sceneVolume);

        private void SetMaterialVolume(CommandBuffer cmd, ComputeShader shader, int kernel) => cmd.SetComputeTextureParam(shader, kernel, ID_MaterialVolume, _materialVolume);

        private ComputeBuffer GetIndirectBuffer(int count)
        {
            ComputeBuffer buffer = new ComputeBuffer(count + 4, sizeof(uint), ComputeBufferType.Structured | ComputeBufferType.IndirectArguments);
            ResetIndirectBuffer(buffer);
            return buffer;
        }

        private void ResetIndirectBuffer(ComputeBuffer buffer)
        {
            var mappedBuffer = buffer.BeginWrite<uint>(0, 4);
            mappedBuffer[0] = 4;
            mappedBuffer[1] = 8192;
            mappedBuffer[2] = 0;
            mappedBuffer[3] = 1;
            buffer.EndWrite<uint>(4);
        }

        private ComputeBuffer GetUShortAppendBuffer(int count)
        {
            ComputeBuffer buffer = new ComputeBuffer(count + 1, sizeof(ushort), ComputeBufferType.Structured);

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
