using Antares.SDF;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    struct ARenderLoop
    {
        enum RPAttachments { Depth, GBuffer0, Shading, Max };

        private const int _rayMarchingKernelWidth = 32;

        private const int _rayMarchingKernelHeight = 32;

        private static readonly int _sceneVolumeID;

        private static readonly int _depthTextureID;

        private static readonly int _gbufferID;

        private static readonly int _shadingTextureID;

        private static readonly Mesh _fullscreenMesh;

        private int _width, _height;

        private Vector4[] _cameraToWorldMatrix;

        static ARenderLoop()
        {
            _sceneVolumeID = Shader.PropertyToID("SceneVolume");
            _depthTextureID = Shader.PropertyToID("DepthTexture");
            _gbufferID = Shader.PropertyToID("GBuffer0");
            _shadingTextureID = Shader.PropertyToID("ShadingTexture");

            _fullscreenMesh = new Mesh() {
                vertices = new Vector3[] { Vector3.zero, new Vector3(2f, 0f), new Vector3(0f, 2f) },
                uv = new Vector2[] { Vector2.zero, new Vector2(2f, 0f), new Vector2(0f, 2f) },
                triangles = new int[] { 0, 1, 2 },
            };
        }

        public ARenderLoop(int width, int height)
        {
            _width = width;
            _height = height;
            _cameraToWorldMatrix = new Vector4[3];
        }

        public void BeginRenderPass(ref ScriptableRenderContext context, CommandBuffer cmd)
        {
            cmd.GetTemporaryRT(_depthTextureID, new RenderTextureDescriptor(_width, _height, RenderTextureFormat.Depth));
            cmd.GetTemporaryRT(_gbufferID, new RenderTextureDescriptor(_width, _height, RenderTextureFormat.ARGB32) { enableRandomWrite = true });

            var attachments = new NativeArray<AttachmentDescriptor>((int)RPAttachments.Max, Allocator.Temp);

            var descriptor = new AttachmentDescriptor(RenderTextureFormat.Depth, new RenderTargetIdentifier(_depthTextureID));
            descriptor.ConfigureClear(Color.clear, 1f);
            attachments[(int)RPAttachments.Depth] = descriptor;

            descriptor = new AttachmentDescriptor(RenderTextureFormat.ARGB32, new RenderTargetIdentifier(_gbufferID));
            attachments[(int)RPAttachments.GBuffer0] = descriptor;

            context.BeginRenderPass(_width, _height, 1, attachments, 0);
            attachments.Dispose();
        }

        public void DispatchRayMarching(CommandBuffer cmd, Camera camera, SDFScene scene, ComputeShader rayMarchingCS, int rayMarchingKernel)
        {
            Transform cameraTransform = camera.transform;
            Vector3 cameraRight = cameraTransform.right;
            Vector3 cameraUp = cameraTransform.up;
            Vector3 cameraPos = cameraTransform.position - cameraRight * .5f - cameraUp * .5f;
            cameraRight /= _width;
            cameraUp /= _height;

            _cameraToWorldMatrix[0] = new Vector4(cameraRight.x, cameraUp.x, cameraPos.x);
            _cameraToWorldMatrix[1] = new Vector4(cameraRight.y, cameraUp.y, cameraPos.y);
            _cameraToWorldMatrix[2] = new Vector4(cameraRight.z, cameraUp.z, cameraPos.z);
            cmd.SetComputeVectorArrayParam(rayMarchingCS, rayMarchingKernel, _cameraToWorldMatrix);

            cmd.SetComputeTextureParam(rayMarchingCS, rayMarchingKernel, _sceneVolumeID, scene.Volume);
            cmd.SetComputeTextureParam(rayMarchingCS, rayMarchingKernel, _gbufferID, new RenderTargetIdentifier(_gbufferID));
            cmd.DispatchCompute(rayMarchingCS, rayMarchingKernel, _width / _rayMarchingKernelWidth, _height / _rayMarchingKernelHeight, 1);

            cmd.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.PixelProcessing);
        }

        public void Shading(ref ScriptableRenderContext context, CommandBuffer cmd, Material shadingMat)
        {
            var outputs = new NativeArray<int>(1, Allocator.Temp);
            outputs[0] = (int)RPAttachments.GBuffer0;

            var inputs = new NativeArray<int>((int)RPAttachments.Max, Allocator.Temp);
            inputs[0] = (int)RPAttachments.Depth;
            inputs[1] = (int)RPAttachments.GBuffer0;

            context.BeginSubPass(outputs, inputs, isDepthReadOnly: true);
            cmd.DrawMesh(_fullscreenMesh, Matrix4x4.identity, shadingMat);
            context.EndSubPass();
        }

        public void EndRenderPass(ref ScriptableRenderContext context)
        {
            context.EndRenderPass();
        }
    }
}
