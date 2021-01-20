using System.Runtime.InteropServices;
using Antares.SDF;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;

namespace Antares.Graphics
{
    [StructLayout(LayoutKind.Sequential)]
    struct SDFRayMarchingParameters
    {
        public Vector3 UVToSceneColumn0;
        public Vector3 UVToSceneColumn1;
        public Vector3 UVToSceneColumn2;
        public Vector3 UVToSceneColumn3;

        public Vector4 SceneTexel;

        public Vector4 SceneSize;

        public Vector3 WorldToSceneTColumn0;
        public Vector3 WorldToSceneTColumn1;
        public Vector3 WorldToSceneTColumn2;
        public Vector3 WorldToSceneTColumn3;

        public SDFRayMarchingParameters(Camera camera, SDFScene scene, float invW, float invH)
        {
            float near = camera.nearClipPlane;
            float dydvHalf = Mathf.Tan((Mathf.Deg2Rad * .5f) * camera.fieldOfView) * near;
            float dxduHalf = dydvHalf * camera.aspect;

            Transform cameraTrans = camera.transform;
            Vector3 right = cameraTrans.localToWorldMatrix.GetColumn(0) * (2f * invW * dxduHalf);
            UVToSceneColumn0 = scene.WorldToSceneVector(right);

            Vector3 up = cameraTrans.localToWorldMatrix.GetColumn(1) * (2f * invH * dydvHalf);
            UVToSceneColumn1 = scene.WorldToSceneVector(up);

            Vector3 lbn = cameraTrans.localToWorldMatrix.MultiplyPoint(new Vector3(-dxduHalf, -dydvHalf, near));
            UVToSceneColumn2 = scene.WorldToScenePoint(lbn);

            Vector3 camPos = cameraTrans.position;
            UVToSceneColumn3 = scene.WorldToScenePoint(camPos);

            Vector3 texel = scene.SizeInv;
            SceneTexel = new Vector4(texel.x, texel.y, texel.z, SDFGenerator.MaxDistance);

            Vector3 size = scene.Size;
            SceneSize = new Vector4(size.x, size.y, size.z, 0f);

            Transform sceneTrans = scene.transform;
            Matrix4x4 worldToScene = sceneTrans.worldToLocalMatrix;
            WorldToSceneTColumn0 = worldToScene.GetColumn(0);
            WorldToSceneTColumn1 = worldToScene.GetColumn(1);
            WorldToSceneTColumn2 = worldToScene.GetColumn(2);
            WorldToSceneTColumn3 = worldToScene.GetColumn(3);
        }
    }

    public class ARenderPipeline : RenderPipeline
    {
        private readonly ComputeShader _rayMarchingCS;

        private readonly int _tiledRayMarchingKernel;

        private readonly int _rayMarchingKernel;

        private const int _tiledMarchingGroupSizeX = 1;
        private const int _tiledMarchingGroupSizeY = 1;
        private const int _tiledMarchingGroupSizeZ = 1;

        private const int _rayMarchingGroupSizeX = 8;
        private const int _rayMarchingGroupSizeY = 8;
        private const int _rayMarchingGroupSizeZ = 1;

        private readonly Material _shadingMat;

        private readonly ComputeBuffer _rayMarchingParamBuffer;

        public unsafe ARenderPipeline(ComputeShader rayMarchingCS, Material shadingMat)
        {
            Debug.Assert(rayMarchingCS);
            Debug.Assert(shadingMat);

            _rayMarchingCS = rayMarchingCS;
            _tiledRayMarchingKernel = _rayMarchingCS.FindKernel("TiledMarching");
            _rayMarchingKernel = _rayMarchingCS.FindKernel("RayMarching");

#if UNITY_EDITOR
            _rayMarchingCS.GetKernelThreadGroupSizes(_tiledRayMarchingKernel, out uint kernelX, out uint kernelY, out uint kernelZ);
            Debug.Assert(kernelX == _tiledMarchingGroupSizeX && kernelY == _tiledMarchingGroupSizeY && kernelZ == _tiledMarchingGroupSizeZ);

            _rayMarchingCS.GetKernelThreadGroupSizes(_rayMarchingKernel, out kernelX, out kernelY, out kernelZ);
            Debug.Assert(kernelX == _rayMarchingGroupSizeX && kernelY == _rayMarchingGroupSizeY && kernelZ == _rayMarchingGroupSizeZ);
#endif

            _shadingMat = shadingMat;

            _rayMarchingParamBuffer = new ComputeBuffer(1, sizeof(SDFRayMarchingParameters));
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
            CommandBuffer cmd = CommandBufferPool.Get();
            CommandBuffer cmdCompute = CommandBufferPool.Get();
            cmdCompute.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

            SDFScene scene = SDFScene.Instance;
            Debug.Assert(scene);

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
                        rmParamData[0] = new SDFRayMarchingParameters(camera, scene, invWidth, invHeight);
                        _rayMarchingParamBuffer.SetData(rmParamData);
                        rmParamData.Dispose();
                    }

                    {
                        int tiledCountX = width / _tiledMarchingGroupSizeX, tiledCountY = height / _tiledMarchingGroupSizeY;
                        cmdCompute.GetTemporaryRT(ID_TiledRM, new RenderTextureDescriptor(tiledCountX, tiledCountY, RenderTextureFormat.RFloat, depthBufferBits: 0) { enableRandomWrite = true });
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _tiledRayMarchingKernel, ID_SceneVolume, scene.Scene);
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _tiledRayMarchingKernel, ID_TiledRM, new RenderTargetIdentifier(ID_TiledRM));
                        cmdCompute.SetComputeBufferParam(_rayMarchingCS, _tiledRayMarchingKernel, ID_RMParams, _rayMarchingParamBuffer);

                        cmdCompute.DispatchCompute(_rayMarchingCS, _tiledRayMarchingKernel, tiledCountX, tiledCountY, _tiledMarchingGroupSizeZ);
                    }

                    {
                        RenderTextureDescriptor rmRTDesc = new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, depthBufferBits: 0, mipCount: 0) { enableRandomWrite = true };
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_SceneVolume, scene.Scene);
                        cmdCompute.GetTemporaryRT(ID_SceneRM0, rmRTDesc);
                        cmdCompute.GetTemporaryRT(ID_SceneRM1, rmRTDesc);
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_TiledRM, new RenderTargetIdentifier(ID_TiledRM));
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_SceneRM0, new RenderTargetIdentifier(ID_SceneRM0));
                        cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_SceneRM1, new RenderTargetIdentifier(ID_SceneRM1));
                        cmdCompute.SetComputeBufferParam(_rayMarchingCS, _rayMarchingKernel, ID_RMParams, _rayMarchingParamBuffer);

                        cmdCompute.DispatchCompute(_rayMarchingCS, _rayMarchingKernel, width / _rayMarchingGroupSizeX, height / _rayMarchingGroupSizeY, 1);
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
