using Unity.Collections;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    public static class ARenderLayouts
    {
        public static readonly bool IsUVFlipped =
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11
            || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12;

        public static readonly Vector2 ScreenBlitScale = new Vector2(1f, IsUVFlipped ? -1f : 1f);
        public static readonly Vector2 ScreenBlitOffset = new Vector2(0f, IsUVFlipped ? 1f : 0f);

        public const string Binding_MaterialVolume = "MaterialVolume";
        public static int ID_MaterialVolume { get; } = Shader.PropertyToID(Binding_MaterialVolume);

        public const string Binding_SceneVolume = "SceneVolume";
        public static int ID_SceneVolume { get; } = Shader.PropertyToID(Binding_SceneVolume);

        public const string Binding_MipVolume = "MipVolume";
        public static int ID_MipVolume { get; } = Shader.PropertyToID(Binding_MipVolume);

        public const string Binding_VolumeMipLevel = "VolumeMipLevel";
        public static int ID_VolumeMipLevel { get; } = Shader.PropertyToID(Binding_VolumeMipLevel);

        public const string Binding_TiledRM = "TiledRM";
        public static int ID_TiledRM { get; } = Shader.PropertyToID(Binding_TiledRM);

        public const string Binding_Depth = "Depth";
        public static int ID_Depth { get; } = Shader.PropertyToID(Binding_Depth);

        public const string Binding_Shading = "Shading";
        public static int ID_Shading { get; } = Shader.PropertyToID(Binding_Shading);

        public const string Binding_SceneRM0 = "SceneRM0";
        public static int ID_SceneRM0 { get; } = Shader.PropertyToID(Binding_SceneRM0);

        public const string Binding_SceneRM1 = "SceneRM1";
        public static int ID_SceneRM1 { get; } = Shader.PropertyToID(Binding_SceneRM1);

        public const string Binding_RayMarchingParameters = "RayMarchingParameters";
        public static int ID_RayMarchingParameters { get; } = Shader.PropertyToID(Binding_RayMarchingParameters);

        public const string Binding_BlitSource = "BlitSrc";
        public static int ID_BlitSource { get; } = Shader.PropertyToID(Binding_BlitSource);

        public const string Binding_BlitDestination = "BlitDst";
        public static int ID_BlitDestination { get; } = Shader.PropertyToID(Binding_BlitDestination);

        public const string Binding_BlitOffset = "BlitOffset";
        public static int ID_BlitOffset { get; } = Shader.PropertyToID(Binding_BlitOffset);

        public const string Binding_SDFBrushes = "SDFBrushes";
        public static int ID_SDFBrushes { get; } = Shader.PropertyToID(Binding_SDFBrushes);

        public const string Binding_SDFBrushParameters = "BrushParameters";
        public static int ID_SDFBrushParameters { get; } = Shader.PropertyToID(Binding_SDFBrushParameters);

        public const string Binding_DispatchCoordsBuffer = "DispatchCoords";
        public static int ID_DispatchCoordsBuffer { get; } = Shader.PropertyToID(Binding_DispatchCoordsBuffer);

        public const string Binding_BrushIndices = "BrushIndices";
        public static int ID_BrushIndices { get; } = Shader.PropertyToID(Binding_BrushIndices);

        public const string Binding_MipDispatchesBuffer = "MipDispatches";
        public static int ID_MipDispatchesBuffer { get; } = Shader.PropertyToID(Binding_MipDispatchesBuffer);

        public const string Binding_SDFGenerationParameters = "SDFGenerationParameters";
        public static int ID_SDFGenerationParameters { get; } = Shader.PropertyToID(Binding_SDFGenerationParameters);

        public const string Binding_BrushAtlas = "BrushAtlas";
        public static int ID_BrushAtlas { get; } = Shader.PropertyToID(Binding_BrushAtlas);

        public static readonly int[] ID_NonAttachmentRTs = new int[] {
            ID_TiledRM,
            ID_Shading,
            ID_SceneRM0,
            ID_SceneRM1
        };

        public enum Attachments { Depth, GBuffer0, Shading, Max };

        public const int AttachmentCount = (int)Attachments.Max;
        public const int AttachmentIndex_Depth = (int)Attachments.Depth;
        public const int AttachmentIndex_GBuffer0 = (int)Attachments.GBuffer0;
        public const int AttachmentIndex_Shading = (int)Attachments.Shading;

        private static AttachmentDescriptor Attachment_Depth = new AttachmentDescriptor(RenderTextureFormat.Depth) {
            loadAction = RenderBufferLoadAction.Clear,
            clearDepth = 1f
        };
        private static AttachmentDescriptor Attachment_GBuffer0 = new AttachmentDescriptor(RenderTextureFormat.ARGBHalf) {
            loadAction = RenderBufferLoadAction.Clear,
            clearColor = Color.clear
        };
        private static AttachmentDescriptor Attachment_Shading = new AttachmentDescriptor(RenderTextureFormat.ARGBHalf) {
            loadStoreTarget = new RenderTargetIdentifier(ID_Shading),
            loadAction = RenderBufferLoadAction.Clear,
            storeAction = RenderBufferStoreAction.Store,
            clearColor = Color.clear
        };

        private static Mesh _fullScreenMesh = null;

#if UNITY_EDITOR
        private static Mesh _fullScreenSceneViewMesh = null;
#endif

        public static NativeArray<AttachmentDescriptor> GetAttachments(Color clearColor)
        {
            Attachment_Shading.clearColor = clearColor;

            var attachments = new NativeArray<AttachmentDescriptor>(AttachmentCount, Allocator.Temp);
            attachments[AttachmentIndex_Depth] = Attachment_Depth;
            attachments[AttachmentIndex_GBuffer0] = Attachment_GBuffer0;
            attachments[AttachmentIndex_Shading] = Attachment_Shading;
            return attachments;
        }

        public static Mesh GetFullScreenMesh()
        {
            if (!_fullScreenMesh)
                _fullScreenMesh = new Mesh() {
                    vertices = new Vector3[] { new Vector3(-1f, -1f), new Vector3(-1f, 3f), new Vector3(3f, -1f) },
                    triangles = new int[] { 0, 1, 2 },
                    uv = IsUVFlipped
                    ? new Vector2[] { new Vector2(0f, 0f), new Vector2(0f, 2f), new Vector2(2f, 0f) }
                    : new Vector2[] { new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(2f, 1f) }
                };
            return _fullScreenMesh;
        }

#if UNITY_EDITOR
        public static Mesh GetFullScreenSceneViewMesh()
        {
            if (!_fullScreenSceneViewMesh)
                _fullScreenSceneViewMesh = new Mesh() {
                    vertices = new Vector3[] { new Vector3(-1f, -1f), new Vector3(-1f, 3f), new Vector3(3f, -1f) },
                    triangles = new int[] { 0, 1, 2 },
                    uv = new Vector2[] { new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(2f, 1f) }
                };
            return _fullScreenSceneViewMesh;
        }
#endif

        public static RenderTexture CreateRWVolumeRT(GraphicsFormat format, Vector3Int size, int mipCount = 1)
        {
            Debug.Assert(mipCount > 0);

            RenderTextureDescriptor volumeDesc = new RenderTextureDescriptor() {
                dimension = TextureDimension.Tex3D,
                width = size.x,
                height = size.z,
                volumeDepth = size.y,
                depthBufferBits = 0,
                graphicsFormat = format,
                msaaSamples = 1,
                useMipMap = mipCount > 1,
                mipCount = mipCount,
                autoGenerateMips = false,
                enableRandomWrite = true
            };
            RenderTexture texture = new RenderTexture(volumeDesc);
            texture.Create();
            return texture;
        }
    }
}
