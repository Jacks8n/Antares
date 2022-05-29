﻿using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;

namespace Antares.Graphics
{
    public static class ARenderUtilities
    {
        public static readonly bool IsUVFlipped = false;

        public static readonly Vector2 ScreenBlitScale = new Vector2(1f, IsUVFlipped ? -1f : 1f);
        public static readonly Vector2 ScreenBlitOffset = new Vector2(0f, IsUVFlipped ? 1f : 0f);

        private static Mesh _fullScreenMesh = null;

        public static Mesh GetFullScreenMesh()
        {
            if (!_fullScreenMesh)
                _fullScreenMesh = new Mesh()
                {
                    vertices = new Vector3[] { new Vector3(-1f, -1f), new Vector3(-1f, 3f), new Vector3(3f, -1f) },
                    triangles = new int[] { 0, 1, 2 },
                    uv = IsUVFlipped
                    ? new Vector2[] { new Vector2(0f, 0f), new Vector2(0f, 2f), new Vector2(2f, 0f) }
                    : new Vector2[] { new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(2f, 1f) }
                };
            return _fullScreenMesh;
        }

#if UNITY_EDITOR
        private static Mesh _fullScreenSceneViewMesh = null;

        public static Mesh GetFullScreenSceneViewMesh()
        {
            if (!_fullScreenSceneViewMesh)
                _fullScreenSceneViewMesh = new Mesh()
                {
                    vertices = new Vector3[] { new Vector3(-1f, -1f), new Vector3(-1f, 3f), new Vector3(3f, -1f) },
                    triangles = new int[] { 0, 1, 2 },
                    uv = new Vector2[] { new Vector2(0f, 1f), new Vector2(0f, -1f), new Vector2(2f, 1f) }
                };
            return _fullScreenSceneViewMesh;
        }
#endif

        /// <summary>
        /// maps the dimension of volume to physical size
        /// </summary>
        public static Vector3Int GetVolumeRTSize(RenderTexture volume)
        {
            return new Vector3Int(volume.width, volume.volumeDepth, volume.height);
        }

        /// <summary>
        /// creates random-acess-enabled 3d texture
        /// </summary>
        public static RenderTexture CreateRWVolumeRT(GraphicsFormat format, Vector3Int size, int mipCount = 1)
        {
            Debug.Assert(mipCount > 0);

            RenderTextureDescriptor volumeDesc = new RenderTextureDescriptor()
            {
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
                enableRandomWrite = true,
            };
            RenderTexture texture = new RenderTexture(volumeDesc);
            texture.Create();
            return texture;
        }

        /// <summary>
        /// checks capcity and allocates a bigger one if needed
        /// </summary>
        /// <returns>whether a new compute buffer is allocated</returns>
        public static bool ReserveComputeBuffer(ref ComputeBuffer buffer, int capacity, int stride, ComputeBufferType type, ComputeBufferMode mode)
        {
            if (buffer == null)
            {
                buffer = new ComputeBuffer(capacity, stride, type, mode);
                return true;
            }

            if (buffer.count >= capacity && buffer.stride == stride && buffer.IsValid())
                return false;

            buffer.Release();
            buffer = new ComputeBuffer(capacity, stride, type, mode);
            return true;
        }
    }
}
