using System.Runtime.InteropServices;
using Antares.SDF;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    struct APipelineCBuffer
    {
        Vector3 BoundMax;
        Vector3 BoundMin;
    }

    public class ARenderPipeline : RenderPipeline
    {
        private Material _rayMarchingMat;

        // Albedo
        private Texture2D[] _GBuffers;

        private Texture2D _depthBuffer;

        private RenderTargetBinding _GBufferDepthBindings;

        private APipelineCBuffer _CBufferParams;

        // Constant buffer for ray marching
        private ComputeBuffer _CBuffer;

        private int _CBufferId;

        private int _sceneVolumeId;

        public ARenderPipeline(int width, int height, Material rayMarchingMat)
        {
            Debug.Assert(rayMarchingMat);

            _rayMarchingMat = rayMarchingMat;

            _GBuffers = new Texture2D[] {
            new Texture2D(width, height, TextureFormat.RGBAFloat, false, true)
            };

            int ngbuffer = _GBuffers.Length;

            var gbufferIdentifiers = new RenderTargetIdentifier[ngbuffer];
            RenderBufferLoadAction[] gbufferLoadActions = new RenderBufferLoadAction[ngbuffer];
            RenderBufferStoreAction[] gbufferStoreActions = new RenderBufferStoreAction[ngbuffer];

            for (int i = 0; i < ngbuffer; i++)
            {
                gbufferIdentifiers[i] = new RenderTargetIdentifier(_GBuffers[i]);
                gbufferLoadActions[i] = RenderBufferLoadAction.DontCare;
                gbufferStoreActions[i] = RenderBufferStoreAction.Store;
            }

            _GBufferDepthBindings = new RenderTargetBinding(gbufferIdentifiers, gbufferLoadActions, gbufferStoreActions,
                _depthBuffer, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);

            _CBuffer = new ComputeBuffer(1, Marshal.SizeOf<APipelineCBuffer>(), ComputeBufferType.Constant, ComputeBufferMode.Immutable);
            _CBufferId = Shader.PropertyToID("SceneParams");
            _sceneVolumeId = Shader.PropertyToID("SceneVolume");
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras)
        {
            SDFScene scene = SDFScene.Instance;

            CommandBuffer cmd = CommandBufferPool.Get();

            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];

#if UNITY_EDITOR
                if (camera.cameraType == CameraType.SceneView)
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif

                CameraClearFlags clearFlags = camera.clearFlags;
                if (clearFlags == CameraClearFlags.Skybox)
                    context.DrawSkybox(camera);
                else if (clearFlags != CameraClearFlags.Nothing)
                    cmd.ClearRenderTarget(
                        clearDepth: clearFlags == CameraClearFlags.Depth,
                        clearColor: clearFlags == CameraClearFlags.SolidColor,
                        camera.backgroundColor);

                context.SetupCameraProperties(camera);

                if (camera.TryGetCullingParameters(out ScriptableCullingParameters cullingParameters))
                {
                    CullingResults cullingResults = context.Cull(ref cullingParameters);

                    SortingSettings sortingSettings = new SortingSettings(camera) {
                        criteria = SortingCriteria.CommonOpaque
                    };

                    cmd.SetRenderTarget(_GBufferDepthBindings);
                    cmd.SetGlobalConstantBuffer(_CBuffer, _CBufferId, 0, _CBuffer.stride);
                    cmd.SetGlobalTexture(_sceneVolumeId, scene.SDF);
                    cmd.DrawProcedural(Matrix4x4.identity, _rayMarchingMat, -1, MeshTopology.Triangles, 3);

                    context.ExecuteCommandBuffer(cmd);

                    DrawingSettings drawingSettings = new DrawingSettings(new ShaderTagId("ForwardBase"), sortingSettings);
                    FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.all);
                    context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSettings);
                }
                else
                    context.ExecuteCommandBuffer(cmd);

                cmd.Clear();
            }

            CommandBufferPool.Release(cmd);
        }
    }
}
