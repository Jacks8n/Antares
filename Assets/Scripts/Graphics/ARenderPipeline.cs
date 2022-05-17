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
    public partial class ARenderPipeline : RenderPipeline
    {
        public static ARenderPipeline Instance { get; private set; }

        public bool IsSceneLoaded { get; private set; } = false;

        private readonly AShaderSpecs _shaderSpecs;

        private ComputeBuffer _constantBuffer;

        private RenderTexture _sceneVolume;

        private RenderTexture _materialVolume;

        private SDFScene _scene;

        public ARenderPipeline(AShaderSpecs shaderSpecs)
        {
            _shaderSpecs = shaderSpecs;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                return;

            UnloadScene();
            UnloadPhysicsScene();

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
            Vector3Int sceneSize = _scene.Size;
            Vector3Int matVolumeSize = sceneSize / SDFGenerationCompute.MatVolumeScale;
            if (!_sceneVolume || !_materialVolume || !_sceneVolume.IsCreated() || !_materialVolume.IsCreated()
                || _sceneVolume.width != sceneSize.x || _sceneVolume.height != sceneSize.z || _sceneVolume.volumeDepth != sceneSize.y)
            {
                _sceneVolume = CreateRWVolumeRT(GraphicsFormat.R8_SNorm, sceneSize, SceneMipCount);
                _materialVolume = CreateRWVolumeRT(GraphicsFormat.R16_UInt, matVolumeSize, SceneMipCount);
            }

            if (_constantBuffer == null || !_constantBuffer.IsValid())
                _constantBuffer = new ComputeBuffer(_shaderSpecs.ConstantBufferCount, _shaderSpecs.ConstantBufferStride, ComputeBufferType.Constant, ComputeBufferMode.SubUpdates);

            CommandBuffer cmd = CommandBufferPool.Get();

            // clear volumes
            {
                // 1.0 stands for maximum of snorm
                for (int j = 0; j < SceneMipCount; j++)
                    _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _sceneVolume, 1f, j);
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

                mipDispatchesBuffers = new ComputeBuffer[SceneMipCount - 1];
                int mipDispatchCount = matVolumeGridCount;
                for (int i = 0; i < SceneMipCount - 1; i++)
                    mipDispatchesBuffers[i] = GetIndirectBuffer(mipDispatchCount /= 8);
            }

            {
                SDFGenerationCompute sdfGeneration = _shaderSpecs.SDFGenerationCS;
                ComputeShader shader = sdfGeneration.Shader;

                // set global params
                {
                    sdfGeneration.SDFGenerationParamsCBSegment.SubUpdateCBuffer(_constantBuffer, new SDFGenerationCompute.SDFGenerationParameters(_scene));
                    sdfGeneration.SDFGenerationParamsCBSegment.BindCBuffer(cmd, shader, ID_SDFGenerationParameters, _constantBuffer);
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

                // generate scene volume mip 0
                kernel = sdfGeneration.GenerateSceneVolumeKernel;
                {
                    var parameters = new SDFGenerationCompute.MipGenerationParameters(_scene, 0);
                    sdfGeneration.MipGenerationParamsCBSegment[0].SubUpdateCBuffer(_constantBuffer, parameters);
                    sdfGeneration.MipGenerationParamsCBSegment[0].BindCBuffer(cmd, shader, ID_MipGenerationParameters, _constantBuffer);

                    SetMaterialVolume(cmd, shader, kernel);
                    SetSceneVolume(cmd, shader, kernel);
                    cmd.SetComputeBufferParam(shader, kernel, ID_SDFBrushes, brushBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, ID_SDFBrushParameters, brushParameterBuffer);
                    cmd.SetComputeTextureParam(shader, kernel, ID_BrushAtlas, brushCollection.NumericalBrushAtlas);
                    cmd.SetComputeBufferParam(shader, kernel, ID_BrushIndices, brushIndicesBuffer);
                    DispatchIndirect(cmd, shader, kernel, ID_DispatchCoordsBuffer, dispatchCoordsBuffer);
                }

                {
                    Vector3Int dispatchSize = matVolumeSize / SDFGenerationCompute.GenerateMipDispatchKernelSize;
                    for (int i = 0; i < SceneMipCount - 1; i++)
                    {
                        var parameters = new SDFGenerationCompute.MipGenerationParameters(_scene, i + 1);
                        sdfGeneration.MipGenerationParamsCBSegment[i + 1].SubUpdateCBuffer(_constantBuffer, parameters);
                        sdfGeneration.MipGenerationParamsCBSegment[i + 1].BindCBuffer(cmd, shader, ID_MipGenerationParameters, _constantBuffer);

                        // generate material volume non-zero mips
                        kernel = sdfGeneration.GenerateMipDispatchKernel;
                        SetMaterialVolume(cmd, shader, kernel, i);
                        cmd.SetComputeTextureParam(shader, kernel, ID_MaterialVolumeMip, _materialVolume, i + 1);
                        cmd.SetComputeBufferParam(shader, kernel, ID_MipDispatchesBuffer, mipDispatchesBuffers[i]);

                        dispatchSize /= 2;
                        cmd.DispatchCompute(shader, kernel, dispatchSize.x, dispatchSize.y, dispatchSize.z);

                        // generate scene volume non-zero mips
                        kernel = sdfGeneration.GenerateMipMapKernel;
                        SetSceneVolume(cmd, shader, kernel, i);
                        cmd.SetComputeTextureParam(shader, kernel, ID_SceneVolumeMip, _sceneVolume, i + 1);

                        DispatchIndirect(cmd, shader, sdfGeneration.GenerateMipMapKernel, ID_MipDispatchesBuffer, mipDispatchesBuffers[i]);
                    }
                }
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

        public void UnloadScene()
        {
            if (!IsSceneLoaded)
            {
                Debug.Log("scene is double unloaded");
                return;
            }

            _sceneVolume.Release();
            _materialVolume.Release();
            _constantBuffer.Release();

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
                    RayMarchingCompute rayMarching = _shaderSpecs.RayMarchingCS;
                    ComputeShader shader = rayMarching.Shader;

                    // set cbuffer
                    int tileCountX = width / RayMarchingCompute.MarchingTileSize, tileCountY = height / RayMarchingCompute.MarchingTileSize;
                    {
                        var parameters = new RayMarchingCompute.RayMarchingParameters(camera, _scene, invWidth, invHeight);
                        rayMarching.RayMarchingParamsCBSegment.SubUpdateCBuffer(_constantBuffer, parameters);
                        rayMarching.RayMarchingParamsCBSegment.BindCBuffer(cmdCompute, shader, ID_RayMarchingParameters, _constantBuffer);
                    }

                    // tiled marching
                    int kernel = rayMarching.TiledMarchingKernel;
                    {
                        cmdCompute.GetTemporaryRT(ID_TiledRM, new RenderTextureDescriptor(tileCountX, tileCountY, GraphicsFormat.R32G32_SFloat, depthBufferBits: 0) { enableRandomWrite = true });
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneVolume, _sceneVolume);
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_TiledRM, new RenderTargetIdentifier(ID_TiledRM));
                        cmdCompute.DispatchCompute(shader, kernel, tileCountX / RayMarchingCompute.TiledMarchingGroupSize, tileCountY / RayMarchingCompute.TiledMarchingGroupSize, 1);
                    }

                    // per pixel marching
                    kernel = rayMarching.RayMarchingKernel;
                    {
                        RenderTextureDescriptor rmRTDesc = new RenderTextureDescriptor(width, height, GraphicsFormat.R16G16B16A16_SFloat, depthBufferBits: 0, mipCount: 0) { enableRandomWrite = true };
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneVolume, _sceneVolume);
                        cmdCompute.GetTemporaryRT(ID_SceneRM0, rmRTDesc);
                        cmdCompute.GetTemporaryRT(ID_SceneRM1, rmRTDesc);
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_TiledRM, new RenderTargetIdentifier(ID_TiledRM));
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneRM0, new RenderTargetIdentifier(ID_SceneRM0));
                        cmdCompute.SetComputeTextureParam(shader, kernel, ID_SceneRM1, new RenderTargetIdentifier(ID_SceneRM1));
                        cmdCompute.DispatchCompute(shader, kernel, width / RayMarchingCompute.RayMarchingGroupSize, height / RayMarchingCompute.RayMarchingGroupSize, 1);
                    }

                    rmFence = cmdCompute.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.ComputeProcessing);

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

        private void SetSceneVolume(CommandBuffer cmd, ComputeShader shader, int kernel, int mipLevel = 0) => cmd.SetComputeTextureParam(shader, kernel, ID_SceneVolume, _sceneVolume, mipLevel);

        private void SetMaterialVolume(CommandBuffer cmd, ComputeShader shader, int kernel, int mipLevel = 0) => cmd.SetComputeTextureParam(shader, kernel, ID_MaterialVolume, _materialVolume, mipLevel);

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
