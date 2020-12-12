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
        // public Vector3 UVToSceneRow0;
        // public Vector3 UVToSceneRow1;
        // public Vector3 UVToSceneRow2;

        public Vector3 UVToSceneColumn0;
        public Vector3 UVToSceneColumn1;
        public Vector3 UVToSceneColumn2;
        public Vector3 UVToSceneColumn3;

        public Vector4 SceneTexel;

        // public Vector4 WorldToSceneTRow0;
        // public Vector4 WorldToSceneTRow1;
        // public Vector4 WorldToSceneTRow2;

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
            Vector3 up = cameraTrans.localToWorldMatrix.GetColumn(1) * (2f * invH * dydvHalf);
            right = scene.WorldToSceneVector(right);
            up = scene.WorldToSceneVector(up);

            Vector3 lbn = cameraTrans.localToWorldMatrix.MultiplyPoint(new Vector3(-dxduHalf, -dydvHalf, near));
            Vector3 camPos = cameraTrans.position;
            lbn = scene.WorldToScenePoint(lbn);
            camPos = scene.WorldToScenePoint(camPos);

            // UVToSceneRow0 = new Vector4(right.x, up.x, lbn.x, camPos.x);
            // UVToSceneRow1 = new Vector4(right.y, up.y, lbn.y, camPos.y);
            // UVToSceneRow2 = new Vector4(right.z, up.z, lbn.z, camPos.z);
            UVToSceneColumn0 = right;
            UVToSceneColumn1 = up;
            UVToSceneColumn2 = lbn;
            UVToSceneColumn3 = camPos;

            Transform sceneTrans = scene.transform;
            Matrix4x4 worldToScene = sceneTrans.worldToLocalMatrix;
            // WorldToSceneTRow0 = new Vector4(worldToScene.m11, worldToScene.m21, worldToScene.m31);
            // WorldToSceneTRow1 = new Vector4(worldToScene.m12, worldToScene.m22, worldToScene.m32);
            // WorldToSceneTRow2 = new Vector4(worldToScene.m13, worldToScene.m23, worldToScene.m33);
            WorldToSceneTColumn0 = worldToScene.GetColumn(0);
            WorldToSceneTColumn1 = worldToScene.GetColumn(1);
            WorldToSceneTColumn2 = worldToScene.GetColumn(2);
            WorldToSceneTColumn3 = worldToScene.GetColumn(3);

            Vector3 texel = scene.SizeInv;
            SceneTexel = new Vector4(texel.x, texel.y, texel.z, SDFGenerator.MaxDistance);
        }
    }

    public class ARenderPipeline : RenderPipeline
    {
        private const int RayMarchingKernelX = 8;
        private const int RayMarchingKernelY = 8;
        private const int RayMarchingKernelZ = 1;

        private readonly ComputeShader _rayMarchingCS;

        private readonly int _rayMarchingKernel;

        private readonly Material _shadingMat;

        private readonly ComputeBuffer _rayMarchingParamBuffer;

        public unsafe ARenderPipeline(ComputeShader rayMarchingCS, int rayMarchingKernel, Material shadingMat)
        {
            Debug.Assert(rayMarchingCS);
            Debug.Assert(shadingMat);

            _rayMarchingCS = rayMarchingCS;
            _rayMarchingKernel = rayMarchingKernel;

#if UNITY_EDITOR
            rayMarchingCS.GetKernelThreadGroupSizes(rayMarchingKernel, out uint kernelX, out uint kernelY, out uint kernelZ);
            Debug.Assert(kernelX == RayMarchingKernelX && kernelY == RayMarchingKernelY && kernelZ == RayMarchingKernelZ);
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
                    cmdCompute.GetTemporaryRT(ID_SceneRM0, new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, depthBufferBits: 0, mipCount: 0) { enableRandomWrite = true });
                    cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_SceneRM0, new RenderTargetIdentifier(ID_SceneRM0));
                    cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_SceneSDF, scene.Scene);

                    {
                        SDFRayMarchingParameters rmParams = new SDFRayMarchingParameters(camera, scene, invWidth, invHeight);

                        var rmParamData = new NativeArray<SDFRayMarchingParameters>(1, Allocator.Temp);
                        rmParamData[0] = rmParams;
                        _rayMarchingParamBuffer.SetData(rmParamData);
                        rmParamData.Dispose();
                    }
                    cmdCompute.SetComputeBufferParam(_rayMarchingCS, _rayMarchingKernel, ID_RMParams, _rayMarchingParamBuffer);

                    cmdCompute.DispatchCompute(_rayMarchingCS, _rayMarchingKernel, width / RayMarchingKernelX, height / RayMarchingKernelY, 1);
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

                {
                    NativeArray<AttachmentDescriptor> attachments = GetAttachments(camera.backgroundColor);
                    context.BeginRenderPass(width, height, samples: 1, attachments, AttachmentIndex_Depth);
                    attachments.Dispose();
                }

                // draw skybox
                if (camera.clearFlags.HasFlag(CameraClearFlags.Skybox))
                {
                    {
                        var colors = new NativeArray<int>(new int[] { AttachmentIndex_Shading }, Allocator.Temp);
                        context.BeginSubPass(colors, isDepthReadOnly: false);
                        colors.Dispose();
                    }

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
                    {
                        var colors = new NativeArray<int>(new int[] { AttachmentIndex_GBuffer0 }, Allocator.Temp);
                        context.BeginSubPass(colors, isDepthReadOnly: false);
                        colors.Dispose();
                    }

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
                    cmd.DrawMesh(GetFullScreenMesh(), Matrix4x4.identity, _shadingMat);
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

                cmd.ReleaseTemporaryRT(ID_Shading);
                cmd.ReleaseTemporaryRT(ID_SceneRM0);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.Submit();

            CommandBufferPool.Release(cmd);
            CommandBufferPool.Release(cmdCompute);
        }
    }
}
