using System;
using Antares.Physics;
using Antares.SDF;
using Unity.Collections;
using UnityEditorInternal;
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

        public RenderTargetIdentifier SceneVolume => _sceneVolume;

#if UNITY_EDITOR
        /// <summary>
        /// If not empty, it will be invoked on the next <see cref="Render(ScriptableRenderContext, Camera[])"/>
        /// call to perform RenderDoc capture. Then the <see cref="Scene"/> will be immediately unloaded.
        /// </summary>
        public event Action HookRenderDocCaptureEvents;
#endif

        public SDFScene Scene { get; private set; }

        private RenderTexture _sceneVolume;

        private RenderTexture _materialVolume;

        private MaterialPropertyBlock _shadingPropertyBlock;

        private APhysicsPipeline _physicsPipeline;

        public ARenderPipeline(AShaderSpecifications shaderSpecs)
        {
            ShaderSpecs = shaderSpecs;

            Instance = this;
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposing)
            {
                UnloadScene();
            }

            base.Dispose(disposing);
        }

        public APhysicsPipeline GetPhysicsPipeline()
        {
            if (_physicsPipeline == null)
                _physicsPipeline = new APhysicsPipeline();

            return _physicsPipeline;
        }

        public void LoadScene(SDFScene scene)
        {
            Debug.Assert(!IsSceneLoaded);
            Debug.Assert(scene);

            if (scene.IsEmpty)
                return;

            Scene = scene;

            // resize textures
            Vector3Int sceneVolumeSize = Scene.Size;
            Vector3Int matVolumeSize = sceneVolumeSize / SDFGenerationCompute.MatVolumeScale;
            if (!_sceneVolume || !_materialVolume || !_sceneVolume.IsCreated() || !_materialVolume.IsCreated()
                || _sceneVolume.width != sceneVolumeSize.x || _sceneVolume.height != sceneVolumeSize.z || _sceneVolume.volumeDepth != sceneVolumeSize.y)
            {
                _sceneVolume = CreateRWVolumeRT(GraphicsFormat.R8_SNorm, sceneVolumeSize, SceneMipCount);
                _materialVolume = CreateRWVolumeRT(GraphicsFormat.R16_UInt, matVolumeSize, SceneMipCount);
            }

            CommandBuffer cmd = CommandBufferPool.Get();

            // clear volumes
            {
                // 1.0 stands for maximum of snorm
                for (int j = 0; j < SceneMipCount; j++)
                    ShaderSpecs.TextureUtilCS.ClearVolume(cmd, _sceneVolume, 1f, j);
            }

            // upload brushes
            var brushCollection = Scene.BrushCollection;
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
            ComputeBuffer dispatchCoordsBuffer;
            ComputeBuffer brushIndicesBuffer;
            ComputeBuffer mipDispatchesBuffer;
            {
                int matVolumeGridCount = matVolumeSize.x * matVolumeSize.y * matVolumeSize.z;
                dispatchCoordsBuffer = GetIndirectBuffer(cmd, matVolumeGridCount);
                brushIndicesBuffer = GetAppendBuffer(cmd, matVolumeGridCount * SDFGenerationCompute.MaxBrushCountFactor);
            }

            {
                SDFGenerationCompute sdfGeneration = ShaderSpecs.SDFGenerationCS;
                ComputeShader shader = sdfGeneration.Shader;

                // bind cbuffers
                ConstantBuffer.Push(cmd, new SDFGenerationCompute.SDFGenerationParameters(Scene), shader, Bindings.SDFGenerationParameters);

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

                kernel = sdfGeneration.GenerateIndirectArgsKernel;
                {
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.IndirectArgs, dispatchCoordsBuffer);
                    cmd.SetComputeIntParam(shader, Bindings.IndirectArgsOffset, 0);
                    cmd.DispatchCompute(shader, kernel, 1, 1, 1);
                }

                // generate scene volume mip 0
                kernel = sdfGeneration.GenerateSceneVolumeKernel;
                {
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.MaterialVolume, _materialVolume);
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, _sceneVolume);
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.BrushAtlas, brushCollection.NumericalBrushAtlas);
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.SDFBrushes, brushBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.BrushParameters, brushParameterBuffer);
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.BrushIndices, brushIndicesBuffer);
                    DispatchIndirect(cmd, shader, kernel, Bindings.DispatchCoords, dispatchCoordsBuffer);
                }

                // generate non-zero mips
                GenerateVolumeMips(cmd, _sceneVolume, _materialVolume, out mipDispatchesBuffer);
            }

            UGraphics.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);

            brushBuffer.Release();
            brushParameterBuffer.Release();
            brushIndicesBuffer.Release();
            dispatchCoordsBuffer.Release();
            mipDispatchesBuffer.Release();

            IsSceneLoaded = true;
        }

        /// <summary>
        /// generate mips of sdf volume with mip 0 of given scene volume and material volume
        /// </summary>
        /// <param name="indirectBuffer">allocated buffer for indirect dispatch. it should be released after the <paramref name="cmd"/> is executed</param>
        public void GenerateVolumeMips(CommandBuffer cmd, RenderTexture sceneVolume, RenderTexture materialVolume, out ComputeBuffer indirectBuffer)
        {
            Debug.Assert(GetVolumeRTSize(sceneVolume) == GetVolumeRTSize(materialVolume) * SDFGenerationCompute.MatVolumeScale,
                "the size of scene volume doesn't match the one of material volume");
            Debug.Assert(sceneVolume.mipmapCount == materialVolume.mipmapCount,
                "the mip level of scene volume doesn't match the one of material volume");
            Debug.Assert(sceneVolume.mipmapCount > 1, "no need to generate mips");

            int mipCount = sceneVolume.mipmapCount;
            Vector3Int matVolumeSize = GetVolumeRTSize(materialVolume);

            SDFGenerationCompute sdfGeneration = ShaderSpecs.SDFGenerationCS;

            // allocate indirect buffers
            int matVolumeGridCount = matVolumeSize.x * matVolumeSize.y * matVolumeSize.z;
            int dispatchArgumentsOffset = 0;
            {
                long a = 1 << (3 * (mipCount - 1));
                int maxDispatchCount = (int)((a - 1) * matVolumeGridCount / (7 * a));

                indirectBuffer = new ComputeBuffer(maxDispatchCount + 4 * (mipCount - 1), 4, ComputeBufferType.IndirectArguments | ComputeBufferType.Default, ComputeBufferMode.Immutable);
                for (int i = 0; i < mipCount - 1; i++)
                {
                    InitializeIndirectBuffer(cmd, indirectBuffer, dispatchArgumentsOffset);

                    matVolumeGridCount /= 8;
                    dispatchArgumentsOffset += GetIndirectBufferSize(matVolumeGridCount);
                }
            }

            ComputeShader shader = sdfGeneration.Shader;

            ConstantBuffer.Set<SDFGenerationCompute.MipGenerationParameters>(cmd, shader, Bindings.MipGenerationParameters);

            matVolumeGridCount = matVolumeSize.x * matVolumeSize.y * matVolumeSize.z;
            dispatchArgumentsOffset = 0;

            Vector3Int threadGroups = matVolumeSize / SDFGenerationCompute.GenerateMipDispatchKernelSize;
            for (int i = 0; i < mipCount - 1; i++)
            {
                var parameters = new SDFGenerationCompute.MipGenerationParameters(matVolumeSize, (uint)dispatchArgumentsOffset);
                ConstantBuffer.UpdateData(cmd, parameters);

                // generate material volume non-zero mips
                int kernel = sdfGeneration.GenerateMipDispatchKernel;
                {
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.MipDispatches, indirectBuffer);
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.MaterialVolume, materialVolume, i);
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.MaterialVolumeMip, materialVolume, i + 1);

                    threadGroups /= 2;
                    cmd.DispatchCompute(shader, kernel, threadGroups.x, threadGroups.y, threadGroups.z);
                }

                kernel = sdfGeneration.GenerateIndirectArgsKernel;
                {
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.IndirectArgs, indirectBuffer);
                    cmd.SetComputeIntParam(shader, Bindings.IndirectArgsOffset, dispatchArgumentsOffset);
                    cmd.DispatchCompute(shader, kernel, 1, 1, 1);
                }

                // generate scene volume non-zero mips
                kernel = sdfGeneration.GenerateMipMapKernel;
                {
                    cmd.SetComputeBufferParam(shader, kernel, Bindings.MipDispatches, indirectBuffer);
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, sceneVolume, i);
                    cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolumeMip, sceneVolume, i + 1);
                    DispatchIndirect(cmd, shader, sdfGeneration.GenerateMipMapKernel, Bindings.MipDispatches, indirectBuffer, (uint)dispatchArgumentsOffset);
                }

                matVolumeSize /= 2;
                matVolumeGridCount /= 8;
                dispatchArgumentsOffset += GetIndirectBufferSize(matVolumeGridCount);
            }

            _shadingPropertyBlock = new MaterialPropertyBlock();
        }

        public void UnloadScene()
        {
            if (!IsSceneLoaded)
                return;

            Scene.enabled = false;
            Scene = null;

            _sceneVolume.Release();
            _materialVolume.Release();

            IsSceneLoaded = false;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
#if UNITY_EDITOR
            if (HookRenderDocCaptureEvents != null)
            {
                if (RenderDoc.IsLoaded())
                {
                    BeginCaptureSceneView();
                    HookRenderDocCaptureEvents();
                    EndCaptureSceneView();
                }
                else
                    Debug.LogWarning("Enable RenderDoc Manually to Enable Hooks.");

                HookRenderDocCaptureEvents = null;
                return;
            }
#endif

            if (!IsSceneLoaded)
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
                GraphicsFence rmFence;
                {
                    RayMarchingCompute rayMarching = ShaderSpecs.RayMarchingCS;
                    ComputeShader shader = rayMarching.Shader;

                    // set cbuffer
                    int tileCountX = width / RayMarchingCompute.MarchingTileSize, tileCountY = height / RayMarchingCompute.MarchingTileSize;
                    {
                        var parameters = new RayMarchingCompute.RayMarchingParameters(camera, Scene, invWidth, invHeight);
                        ConstantBuffer.Push(cmdCompute, parameters, shader, Bindings.RayMarchingParameters);
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

                // draw fluid
                if (_physicsPipeline != null && _physicsPipeline.IsSceneLoaded)
                {
                    using (var colors = new NativeArray<int>(new int[] { RenderPass0.Index_GBuffer0 }, Allocator.Temp))
                        context.BeginSubPass(colors, isDepthStencilReadOnly: false);

                    _physicsPipeline.RenderDebugParticles(cmd, camera);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

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
                    Mesh fullscreen = camera.cameraType == CameraType.SceneView ? FullScreenSceneViewMesh : FullScreenMesh;
#else
                    Mesh fullscreen = GetFullScreenMesh();
#endif

                    _shadingPropertyBlock.SetVector(Bindings.FrameBufferSize, new Vector4(width, height, 0f, 0f));
                    cmd.DrawMesh(fullscreen, Matrix4x4.identity, ShaderSpecs.Deferred.Material, 0, 0, _shadingPropertyBlock);
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
                        cmd.Blit(new RenderTargetIdentifier(Bindings.Shading), target, Vector2.one, Vector2.zero);
                    else
                        cmd.Blit(new RenderTargetIdentifier(Bindings.Shading), target, Vector2.one, Vector2.zero);
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

        private int GetIndirectBufferSize(int count)
        {
            return count + 4;
        }

        private void InitializeIndirectBuffer(CommandBuffer cmd, ComputeBuffer buffer, int offset)
        {
            cmd.SetBufferData(buffer, new uint[] { 0, 1024, 0, 1 }, 0, offset, 4);
        }

        private ComputeBuffer GetIndirectBuffer(CommandBuffer cmd, int count)
        {
            ComputeBuffer buffer = new ComputeBuffer(GetIndirectBufferSize(count), sizeof(uint), ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Immutable);

            InitializeIndirectBuffer(cmd, buffer, 0);

            return buffer;
        }

        private ComputeBuffer GetAppendBuffer(CommandBuffer cmd, int count)
        {
            ComputeBuffer buffer = new ComputeBuffer(count + 1, sizeof(uint), ComputeBufferType.Structured, ComputeBufferMode.Immutable);

            cmd.SetBufferData(buffer, new uint[] { 1 });

            return buffer;
        }

        private void DispatchIndirect(CommandBuffer cmd, ComputeShader shader, int kernel, int bufferID, ComputeBuffer indirectBuffer, uint offset = 0)
        {
            cmd.SetComputeBufferParam(shader, kernel, bufferID, indirectBuffer);
            cmd.DispatchCompute(shader, kernel, indirectBuffer, (offset + 1) * sizeof(uint));
        }
    }
}
