using Antares.SDF;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;
using static Antares.Graphics.ARenderUtilities;
using static Antares.Graphics.AShaderSpecifications;
using UGraphics = UnityEngine.Graphics;

namespace Antares.Graphics
{
    public partial class ARenderPipeline : RenderPipeline
    {
        public static ARenderPipeline Instance { get; private set; }

        public bool IsSceneLoaded { get; private set; } = false;

        public AShaderSpecifications ShaderSpecs { get; private set; }

        public ComputeBuffer ConstantBuffer { get; private set; }

        private RenderTexture _sceneVolume;

        private RenderTexture _materialVolume;

        private SDFScene _scene;

        public ARenderPipeline(AShaderSpecifications shaderSpecs)
        {
            ShaderSpecs = shaderSpecs;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                return;

            UnloadScene();

            base.Dispose(disposing);
        }

        public void LoadScene(SDFScene scene)
        {
            Debug.Assert(!IsSceneLoaded);
            Debug.Assert(scene);

            if (scene.IsEmpty)
                return;

            _scene = scene;

            // resize textures
            Vector3Int sceneVolumeSize = _scene.Size;
            Vector3Int matVolumeSize = sceneVolumeSize / SDFGenerationCompute.MatVolumeScale;
            if (!_sceneVolume || !_materialVolume || !_sceneVolume.IsCreated() || !_materialVolume.IsCreated()
                || _sceneVolume.width != sceneVolumeSize.x || _sceneVolume.height != sceneVolumeSize.z || _sceneVolume.volumeDepth != sceneVolumeSize.y)
            {
                _sceneVolume = CreateRWVolumeRT(GraphicsFormat.R8_SNorm, sceneVolumeSize, SceneMipCount);
                _materialVolume = CreateRWVolumeRT(GraphicsFormat.R16_UInt, matVolumeSize, SceneMipCount);
            }

            if (ConstantBuffer == null || !ConstantBuffer.IsValid())
                ConstantBuffer = new ComputeBuffer(ShaderSpecs.ConstantBufferCount, ShaderSpecs.ConstantBufferStride, ComputeBufferType.Constant, ComputeBufferMode.SubUpdates);

            CommandBuffer cmd = CommandBufferPool.Get();

            // clear volumes
            {
                // 1.0 stands for maximum of snorm
                for (int j = 0; j < SceneMipCount; j++)
                    ShaderSpecs.TextureUtilCS.ClearVolume(cmd, _sceneVolume, 1f, j);
            }

            // upload brushes
            var brushCollection = _scene.BrusheCollection;
            ComputeBuffer brushBuffer, brushParameterBuffer;
            unsafe
            {
                var brushes = brushCollection.Brushes;
                var brushParams = brushCollection.BrushParameters;

                brushBuffer = new ComputeBuffer(brushes.Length, sizeof(SDFGenerationCompute.SDFBrush), ComputeBufferType.Structured, ComputeBufferMode.Immutable);
                var mappedBrushes = new NativeArray<SDFGenerationCompute.SDFBrush>(brushes.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < brushes.Length; i++)
                    mappedBrushes[i] = new SDFGenerationCompute.SDFBrush(brushes[i].Property, brushes[i].ParameterCount, brushes[i].ParameterOffset);
                brushBuffer.SetData(mappedBrushes);

                brushParameterBuffer = new ComputeBuffer(brushParams.Length, sizeof(float), ComputeBufferType.Structured, ComputeBufferMode.Immutable);
                brushParameterBuffer.SetData(brushParams);
            }

            // allocate indirect buffers
            ComputeBuffer dispatchCoordsBuffer, brushIndicesBuffer;
            ComputeBuffer[] mipDispatchesBuffers;
            {
                int matVolumeGridCount = matVolumeSize.x * matVolumeSize.y * matVolumeSize.z;
                dispatchCoordsBuffer = GetIndirectBuffer(matVolumeGridCount);
                brushIndicesBuffer = GetAppendBuffer(matVolumeGridCount * SDFGenerationCompute.MaxBrushCountFactor);
            }

            {
                SDFGenerationCompute sdfGeneration = ShaderSpecs.SDFGenerationCS;
                ComputeShader shader = sdfGeneration.Shader;

                // set global params
                {
                    sdfGeneration.SDFGenerationParamsCBSpan.SubUpdateCBuffer(ConstantBuffer, new SDFGenerationCompute.SDFGenerationParameters(_scene));
                    sdfGeneration.SDFGenerationParamsCBSpan.BindCBuffer(cmd, shader, Bindings.SDFGenerationParameters, ConstantBuffer);
                }

                // generate material volume mip 0
                int kernel = sdfGeneration.GenerateMatVolumeKernel;
                {
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.MaterialVolume, _materialVolume);
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.BrushAtlas, brushCollection.NumericalBrushAtlas);
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.SDFBrushes, brushBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.BrushParameters, brushParameterBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.DispatchCoords, dispatchCoordsBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.BrushIndices, brushIndicesBuffer);

                    Vector3Int dispatchSize = matVolumeSize / SDFGenerationCompute.GenerateMatVolumeKernelSize;
                    cmd.DispatchCompute(shader, kernel, dispatchSize.x, dispatchSize.y, dispatchSize.z);
                }

                // generate scene volume mip 0
                kernel = sdfGeneration.GenerateSceneVolumeKernel;
                {
                    var parameters = new SDFGenerationCompute.MipGenerationParameters(sceneVolumeSize, 0);
                    sdfGeneration.MipGenerationParamsCBSpan[0].SubUpdateCBuffer(ConstantBuffer, parameters);
                    sdfGeneration.MipGenerationParamsCBSpan[0].BindCBuffer(cmd, shader, Bindings.MipGenerationParameters, ConstantBuffer);

                    cmd.SetComputeTextureParam(shader, kernel, Bindings.MaterialVolume, _materialVolume);
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, _sceneVolume);
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.SDFBrushes, brushBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.BrushParameters, brushParameterBuffer);
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.BrushAtlas, brushCollection.NumericalBrushAtlas);
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.BrushIndices, brushIndicesBuffer);
                    DispatchIndirect(cmd, shader, kernel, Bindings.DispatchCoords, dispatchCoordsBuffer);
                }

                // generate non-zero mips
                GenerateVolumeMips(cmd, _sceneVolume, _materialVolume, out mipDispatchesBuffers);
            }

            UGraphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            brushBuffer.Release();
            brushParameterBuffer.Release();
            brushIndicesBuffer.Release();
            dispatchCoordsBuffer.Release();
            for (int i = 0; i < mipDispatchesBuffers.Length; i++)
                mipDispatchesBuffers[i].Release();

            IsSceneLoaded = true;
        }

        public void GenerateVolumeMips(CommandBuffer cmd, RenderTexture sceneVolume, RenderTexture materialVolume, out ComputeBuffer[] mipDispatchesBuffers)
        {
            Debug.Assert(GetVolumeRTSize(sceneVolume) == GetVolumeRTSize(materialVolume) * SDFGenerationCompute.MatVolumeScale,
                "the size of scene volume doesn't match the one of material volume");
            Debug.Assert(sceneVolume.mipmapCount == materialVolume.mipmapCount,
                "the mip level of scene volume doesn't match the one of material volume");

            int sceneMipCount = sceneVolume.mipmapCount;
            Vector3Int sceneVolumeSize = GetVolumeRTSize(sceneVolume);
            Vector3Int matVolumeSize = GetVolumeRTSize(materialVolume);

            // allocate indirect buffers
            {
                mipDispatchesBuffers = new ComputeBuffer[sceneMipCount - 1];

                int matVolumeGridCount = matVolumeSize.x * matVolumeSize.y * matVolumeSize.z;
                for (int i = 0; i < sceneMipCount - 1; i++)
                {
                    matVolumeGridCount /= 8;
                    mipDispatchesBuffers[i] = GetIndirectBuffer(matVolumeGridCount);
                }
            }

            SDFGenerationCompute sdfGeneration = ShaderSpecs.SDFGenerationCS;
            ComputeShader shader = sdfGeneration.Shader;

            Vector3Int threadGroups = matVolumeSize / SDFGenerationCompute.GenerateMipDispatchKernelSize;
            for (int i = 0; i < sceneMipCount - 1; i++)
            {
                var parameters = new SDFGenerationCompute.MipGenerationParameters(sceneVolumeSize, i + 1);
                sdfGeneration.MipGenerationParamsCBSpan[i + 1].SubUpdateCBuffer(ConstantBuffer, parameters);
                sdfGeneration.MipGenerationParamsCBSpan[i + 1].BindCBuffer(cmd, shader, Bindings.MipGenerationParameters, ConstantBuffer);

                // generate material volume non-zero mips
                int kernel = sdfGeneration.GenerateMipDispatchKernel;
                cmd.SetComputeTextureParam(shader, kernel, Bindings.MaterialVolume, materialVolume, i);
                cmd.SetComputeTextureParam(shader, kernel, Bindings.MaterialVolumeMip, materialVolume, i + 1);
                cmd.SetComputeBufferParam(shader, kernel, Bindings.MipDispatches, mipDispatchesBuffers[i]);

                threadGroups /= 2;
                cmd.DispatchCompute(shader, kernel, threadGroups.x, threadGroups.y, threadGroups.z);

                // generate scene volume non-zero mips
                kernel = sdfGeneration.GenerateMipMapKernel;
                cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, sceneVolume, i);
                cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolumeMip, sceneVolume, i + 1);

                DispatchIndirect(cmd, shader, sdfGeneration.GenerateMipMapKernel, Bindings.MipDispatches, mipDispatchesBuffers[i]);
            }
        }

        public void UnloadScene()
        {
            if (!IsSceneLoaded)
            {
                Debug.Log("scene is double unloaded");
                return;
            }

            _sceneVolume.Release();
            _materialVolume.Release();
            ConstantBuffer.Release();

            IsSceneLoaded = false;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            if (!IsSceneLoaded || !_scene.enabled)
            {
                UnloadScene();
                return;
            }

            //var window = UnityEditor.EditorWindow.GetWindow<UnityEditor.SceneView>();
            //UnityEditorInternal.RenderDoc.BeginCaptureRenderDoc(window);
            //LoadScene(_loadedScene);
            //UnityEditorInternal.RenderDoc.EndCaptureRenderDoc(window);
            //_loadedScene = null;
            //return;

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
                GraphicsFence rmFence;
                {
                    RayMarchingCompute rayMarching = ShaderSpecs.RayMarchingCS;
                    ComputeShader shader = rayMarching.Shader;

                    // set cbuffer
                    int tileCountX = width / RayMarchingCompute.MarchingTileSize, tileCountY = height / RayMarchingCompute.MarchingTileSize;
                    {
                        var parameters = new RayMarchingCompute.RayMarchingParameters(camera, _scene, invWidth, invHeight);
                        rayMarching.RayMarchingParamsCBSpan.SubUpdateCBuffer(ConstantBuffer, parameters);
                        rayMarching.RayMarchingParamsCBSpan.BindCBuffer(cmdCompute, shader, Bindings.RayMarchingParameters, ConstantBuffer);
                    }

                    // tiled marching
                    int kernel = rayMarching.TiledMarchingKernel;
                    {
                        cmdCompute.GetTemporaryRT(Bindings.TiledRM, new RenderTextureDescriptor(tileCountX, tileCountY, GraphicsFormat.R32G32_SFloat, depthBufferBits: 0) { enableRandomWrite = true });
                        cmdCompute.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, _sceneVolume);
                        cmdCompute.SetComputeTextureParam(shader, kernel, Bindings.TiledRM, new RenderTargetIdentifier(Bindings.TiledRM));
                        cmdCompute.DispatchCompute(shader, kernel, tileCountX / RayMarchingCompute.TiledMarchingGroupSize, tileCountY / RayMarchingCompute.TiledMarchingGroupSize, 1);
                    }

                    // per pixel marching
                    kernel = rayMarching.RayMarchingKernel;
                    {
                        RenderTextureDescriptor rmRTDesc = new RenderTextureDescriptor(width, height, GraphicsFormat.R16G16B16A16_SFloat, depthBufferBits: 0, mipCount: 0) { enableRandomWrite = true };
                        cmdCompute.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, _sceneVolume);
                        cmdCompute.GetTemporaryRT(Bindings.SceneRM0, rmRTDesc);
                        cmdCompute.GetTemporaryRT(Bindings.SceneRM1, rmRTDesc);
                        cmdCompute.SetComputeTextureParam(shader, kernel, Bindings.TiledRM, new RenderTargetIdentifier(Bindings.TiledRM));
                        cmdCompute.SetComputeTextureParam(shader, kernel, Bindings.SceneRM0, new RenderTargetIdentifier(Bindings.SceneRM0));
                        cmdCompute.SetComputeTextureParam(shader, kernel, Bindings.SceneRM1, new RenderTargetIdentifier(Bindings.SceneRM1));
                        cmdCompute.DispatchCompute(shader, kernel, width / RayMarchingCompute.RayMarchingGroupSize, height / RayMarchingCompute.RayMarchingGroupSize, 1);
                    }

                    rmFence = cmdCompute.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);

                    context.ExecuteCommandBufferAsync(cmdCompute, ComputeQueueType.Default);
                    cmdCompute.Clear();
                }

                // allocate textures
                {
                    cmd.GetTemporaryRT(Bindings.Shading, new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, depthBufferBits: 0, mipCount: 0));
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();
                }

                using (var attachments = RenderPass0.GetAttachments(camera.backgroundColor))
                    context.BeginRenderPass(width, height, samples: 1, attachments, RenderPass0.Index_Depth);

                // draw skybox
                if (camera.clearFlags.HasFlag(CameraClearFlags.Skybox))
                {
                    using (var colors = new NativeArray<int>(new int[] { RenderPass0.Index_Shading }, Allocator.Temp))
                        context.BeginSubPass(colors, isDepthStencilReadOnly: false);

                    context.DrawSkybox(camera);

                    context.EndSubPass();
                }

                // cull
                CullingResults cullingResults = context.Cull(ref cullingParameters);

                SortingSettings sortingSettings = new SortingSettings(camera)
                {
                    criteria = SortingCriteria.CommonOpaque
                };
                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);

                // draw opaque mesh
                {
                    using (var colors = new NativeArray<int>(new int[] { RenderPass0.Index_GBuffer0 }, Allocator.Temp))
                        context.BeginSubPass(colors, isDepthStencilReadOnly: false);

                    DrawingSettings drawingSettings = new DrawingSettings(new ShaderTagId("Deferred"), sortingSettings);

                    context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);

                    context.EndSubPass();
                }

                // combine rasterization and ray marching
                {
                    {
                        using var colors = new NativeArray<int>(new int[] { RenderPass0.Index_Shading }, Allocator.Temp);
                        using var inputs = new NativeArray<int>(new int[] { RenderPass0.Index_GBuffer0, RenderPass0.Index_Depth }, Allocator.Temp);
                        context.BeginSubPass(colors, inputs, isDepthStencilReadOnly: true);
                    }

                    cmd.WaitOnAsyncGraphicsFence(rmFence, SynchronisationStage.PixelProcessing);
#if UNITY_EDITOR
                    Mesh fullscreen = camera.cameraType == CameraType.SceneView ? GetFullScreenSceneViewMesh() : GetFullScreenMesh();
#else
                    Mesh fullscreen = GetFullScreenMesh();
#endif

                    cmd.DrawMesh(fullscreen, Matrix4x4.identity, ShaderSpecs.Deferred.Material);
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
                        cmd.Blit(new RenderTargetIdentifier(Bindings.Shading), target, new Vector2(1f, 1f), Vector2.zero);
                    else
                        cmd.Blit(new RenderTargetIdentifier(Bindings.Shading), target, ScreenBlitScale, ScreenBlitOffset);
                }

                for (int j = 0; j < Bindings.NonAttachmentRTs.Length; j++)
                    cmd.ReleaseTemporaryRT(Bindings.NonAttachmentRTs[j]);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.Submit();

            CommandBufferPool.Release(cmd);
            CommandBufferPool.Release(cmdCompute);
        }

        private ComputeBuffer GetIndirectBuffer(int count)
        {
            ComputeBuffer buffer = new ComputeBuffer(count + 4, sizeof(uint), ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Immutable);

            buffer.SetData(new uint[] { 4, 8192, 0, 1 }, 0, 0, 4);

            return buffer;
        }

        private ComputeBuffer GetAppendBuffer(int count)
        {
            ComputeBuffer buffer = new ComputeBuffer(count + 1, sizeof(uint), ComputeBufferType.Structured, ComputeBufferMode.Immutable);

            buffer.SetData(new uint[] { 1 }, 0, 0, 1);

            return buffer;
        }

        private void DispatchIndirect(CommandBuffer cmd, ComputeShader shader, int kernel, int bufferID, ComputeBuffer indirectBuffer)
        {
            cmd.SetComputeBufferParam(shader, kernel, bufferID, indirectBuffer);
            cmd.DispatchCompute(shader, kernel, indirectBuffer, sizeof(int));
        }
    }
}
