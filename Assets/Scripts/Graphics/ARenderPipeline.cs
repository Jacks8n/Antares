using System.Runtime.InteropServices;
using Antares.SDF;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;

namespace Antares.Graphics
{
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    struct SDFRayMarchingParameters
    {
        public Vector4 SceneBound0;
        public Vector2 SceneBound1;
        public Vector4 CameraU;
        public Vector4 CameraV;
        public Vector4 CameraPos;

        public void SetSceneBound(SDFScene scene)
        {
            scene.GetBound(out Vector3 min, out Vector3 max);
            SceneBound0 = new Vector4(min.x, min.y, min.z, max.x);
            SceneBound1 = new Vector2(max.y, max.z);
        }

        public void SetRayParams(float invW, float invH, Camera camera)
        {
            Debug.Assert(!camera.usePhysicalProperties, "not supported yet");
            Debug.Assert(!camera.orthographic, "not supported yet");

            float dydv = Mathf.Tan(camera.fieldOfView * .5f) * camera.nearClipPlane;
            float dxdu = dydv * camera.aspect;

            Transform cameraTrans = camera.transform;
            CameraU = camera.cameraToWorldMatrix.GetColumn(0) * (dxdu * invW * 2f);
            CameraV = camera.cameraToWorldMatrix.GetColumn(1) * (dydv * invH * 2f);
            CameraPos = cameraTrans.position;

            Vector3 lbn = cameraTrans.localToWorldMatrix.MultiplyPoint(new Vector3(-dxdu, -dydv, camera.nearClipPlane));
            CameraU.w = lbn.x;
            CameraV.w = lbn.y;
            CameraPos.w = lbn.z;
        }
    }

    public class ARenderPipeline : RenderPipeline
    {
        private readonly ComputeShader _rayMarchingCS;

        private readonly int _rayMarchingKernel;

        private readonly uint _rayMarchingKernelX;

        private readonly uint _rayMarchingKernelY;

        private readonly uint _rayMarchingKernelZ;

        private readonly Material _shadingMat;

        private ComputeBuffer _rayMarchingParamBuffer;

        public unsafe ARenderPipeline(ComputeShader rayMarchingCS, int rayMarchingKernel, Material shadingMat)
        {
            Debug.Assert(rayMarchingCS);
            Debug.Assert(shadingMat);

            _rayMarchingCS = rayMarchingCS;
            _rayMarchingKernel = rayMarchingKernel;
            rayMarchingCS.GetKernelThreadGroupSizes(rayMarchingKernel, out _rayMarchingKernelX, out _rayMarchingKernelY, out _rayMarchingKernelZ);

            _shadingMat = shadingMat;

            _rayMarchingParamBuffer = new ComputeBuffer(1, sizeof(SDFRayMarchingParameters));
        }

        ~ARenderPipeline()
        {
            _rayMarchingParamBuffer.Dispose();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            CommandBuffer cmd = CommandBufferPool.Get();
            CommandBuffer cmdCompute = CommandBufferPool.Get();
            cmdCompute.SetExecutionFlags(CommandBufferExecutionFlags.AsyncCompute);

            var attachments = new NativeArray<AttachmentDescriptor>(AttachmentCount, Allocator.Temp);
            attachments[AttachmentIndex_Depth] = new AttachmentDescriptor(RenderTextureFormat.Depth);
            attachments[AttachmentIndex_GBuffer0] = new AttachmentDescriptor(RenderTextureFormat.ARGBHalf);
            attachments[AttachmentIndex_Shading] = new AttachmentDescriptor(RenderTextureFormat.ARGBHalf) {
                loadStoreTarget = new RenderTargetIdentifier(ID_Shading),
                storeAction = RenderBufferStoreAction.Store
            };

            SDFScene scene = SDFScene.Instance;
            Debug.Assert(scene);

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];

                if (!camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
                    continue;

                int width = camera.pixelWidth, height = camera.pixelHeight;

#if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView)
                {
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
                    width = (int)SceneView.currentDrawingSceneView.position.width;
                    height = (int)SceneView.currentDrawingSceneView.position.height;
                }
#endif

                context.SetupCameraProperties(camera);

                // dispatch ray marching
                GraphicsFence rmFence;
                {
                    cmdCompute.GetTemporaryRT(ID_SceneRM0, new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGB32, depthBufferBits: 0, mipCount: 0) { enableRandomWrite = true });
                    cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_SceneRM0, new RenderTargetIdentifier(ID_SceneRM0));
                    cmdCompute.SetComputeTextureParam(_rayMarchingCS, _rayMarchingKernel, ID_SceneSDF, scene.Scene);

                    {
                        SDFRayMarchingParameters rmParams = new SDFRayMarchingParameters();
                        rmParams.SetSceneBound(scene);

                        var rmParamData = new NativeArray<SDFRayMarchingParameters>(new SDFRayMarchingParameters[] { rmParams }, Allocator.Temp);
                        _rayMarchingParamBuffer.SetData(rmParamData);
                        rmParamData.Dispose();
                    }
                    cmdCompute.SetComputeBufferParam(_rayMarchingCS, _rayMarchingKernel, ID_RMParams, _rayMarchingParamBuffer);

                    Vector3Int dispatchGroups = GetRMDispatchGroups(width, height, _rayMarchingKernelX, _rayMarchingKernelY, _rayMarchingKernelZ);
                    cmdCompute.DispatchCompute(_rayMarchingCS, _rayMarchingKernel, dispatchGroups.x, dispatchGroups.y, dispatchGroups.z);
                    rmFence = cmdCompute.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.PixelProcessing);

                    context.ExecuteCommandBufferAsync(cmdCompute, ComputeQueueType.Default);
                    cmdCompute.Clear();
                }

                cmd.GetTemporaryRT(ID_Shading, new RenderTextureDescriptor(width, height, RenderTextureFormat.ARGBHalf, 0, 0));
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                context.BeginRenderPass(width, height, samples: 1, attachments, AttachmentIndex_Depth);

                // clear
                {
                    CameraClearFlags clearFlags = camera.clearFlags;
                    if (clearFlags == CameraClearFlags.Skybox)
                    {
                        {
                            var colors = new NativeArray<int>(new int[] { AttachmentIndex_Shading }, Allocator.Temp);
                            context.BeginSubPass(colors, isDepthReadOnly: false);
                            colors.Dispose();
                        }
                        context.DrawSkybox(camera);
                        context.EndSubPass();
                    }
                }

                // cull
                CullingResults cullingResults = context.Cull(ref cullingParameters);

                // draw opaque mesh
                {
                    {
                        var colors = new NativeArray<int>(new int[] { AttachmentIndex_GBuffer0 }, Allocator.Temp);
                        context.BeginSubPass(colors, isDepthReadOnly: false);
                        colors.Dispose();
                    }

                    SortingSettings sortingSettings = new SortingSettings(camera) {
                        criteria = SortingCriteria.CommonOpaque
                    };
                    FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque);
                    DrawingSettings drawingSettings = new DrawingSettings(new ShaderTagId("ForwardBase"), sortingSettings);

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
                    cmd.DrawMesh(FullScreenMesh, Matrix4x4.identity, _shadingMat);
                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    context.EndSubPass();
                }

                context.EndRenderPass();

                RenderBuffer rtPresent = camera.targetTexture
                    ? camera.targetTexture.colorBuffer
                    : Display.displays[camera.targetDisplay].colorBuffer;
                cmd.Blit(new RenderTargetIdentifier(ID_Shading), rtPresent);

                cmd.ReleaseTemporaryRT(ID_Shading);
                cmd.ReleaseTemporaryRT(ID_SceneRM0);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
            }

            context.Submit();

            attachments.Dispose();

            CommandBufferPool.Release(cmd);
            CommandBufferPool.Release(cmdCompute);
        }
    }
}
