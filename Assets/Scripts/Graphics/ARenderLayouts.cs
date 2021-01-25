using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    [ExecuteAlways]
    static class ARenderLayouts
    {
        public static readonly bool IsUVFlipped =
            SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D11
            || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12;

        public static readonly Vector2 ScreenBlitScale = new Vector2(1f, IsUVFlipped ? -1f : 1f);
        public static readonly Vector2 ScreenBlitOffset = new Vector2(0f, IsUVFlipped ? 1f : 0f);

        public const string Binding_SceneVolume = "SceneVolume";
        public const string Binding_SceneVolumeMip = "SceneVolumeMip";
        public const string Binding_SceneTexel = "SceneTexel";
        public const string Binding_TiledRM = "TiledRM";
        public const string Binding_Depth = "Depth";
        public const string Binding_Shading = "Shading";
        public const string Binding_SceneRM0 = "SceneRM0";
        public const string Binding_SceneRM1 = "SceneRM1";
        public const string Binding_RMParams = "RMParams";

        public static readonly int ID_SceneVolume = Shader.PropertyToID(Binding_SceneVolume);
        public static readonly int ID_SceneVolumeMip = Shader.PropertyToID(Binding_SceneVolumeMip);
        public static readonly int ID_SceneTexel = Shader.PropertyToID(Binding_SceneTexel);
        public static readonly int ID_TiledRM = Shader.PropertyToID(Binding_TiledRM);
        public static readonly int ID_Depth = Shader.PropertyToID(Binding_Depth);
        public static readonly int ID_Shading = Shader.PropertyToID(Binding_Shading);
        public static readonly int ID_SceneRM0 = Shader.PropertyToID(Binding_SceneRM0);
        public static readonly int ID_SceneRM1 = Shader.PropertyToID(Binding_SceneRM1);
        public static readonly int ID_RMParams = Shader.PropertyToID(Binding_RMParams);

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
    }
}
