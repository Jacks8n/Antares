using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;
using static Antares.Graphics.ARenderUtilities;
using static Antares.Graphics.AShaderSpecifications;

namespace Antares.Utility
{
    public static class Texture3DAtlas
    {
        private struct SizedValue<T> : IComparable<SizedValue<T>>
        {
            public readonly T Value;

            private readonly int Size;

            public SizedValue(int size)
            {
                Value = default;
                Size = size;
            }

            public SizedValue(T value, int size)
            {
                Value = value;
                Size = size;
            }

            public int CompareTo(SizedValue<T> other)
            {
                return Size.CompareTo(other.Size);
            }

            public int CompareTo<U>(SizedValue<U> other)
            {
                return Size.CompareTo(other.Size);
            }
        }

        /// <summary>
        /// This constraint is to match the dispatch group size, which is defined as such for efficiency.
        /// </summary>
        public const int TextureSizeAlignment = 8;

        public const int MaxTextureSize = 2048;

        /// <param name="textures">Size of each texture must be multiple of <see cref="TextureSizeAlignment"/></param>
        /// <param name="cmd">Copy operations will be recorded into it.</param>
        /// <param name="offsets">Offsets of packed textures.</param>
        /// <returns>Packed atlas</returns>
        public static RenderTexture GetAtlas<TTextures, TOffsets>(CommandBuffer cmd, TTextures textures, TOffsets offsets) where TTextures : IList<Texture3D> where TOffsets : IList<Vector3Int>
        {
            if (textures.Count == 0)
                return null;

            var sortedTextures = GetPooledList<SizedValue<(Texture3D Texture, int Index)>>(textures.Count);
            for (int i = 0; i < textures.Count; i++)
                sortedTextures.Add(GetSized((textures[i], i), GetMaxEdge(textures[i])));
            sortedTextures.Sort();

            // chunks are ordered by their length of shortest edge
            var chunks = GetPooledList<SizedValue<(Vector3Int Offset, Vector3Int Size)>>((textures.Count + 1) / 2).ToPriorityQueue();
            chunks.Enqueue(GetSized((Vector3Int.zero, new Vector3Int(MaxTextureSize, MaxTextureSize, MaxTextureSize)), MaxTextureSize));

            void Ascend(Vector3Int vector, ref int l, ref int r)
            {
                if (vector[r] < vector[l])
                {
                    int temp = l;
                    l = r;
                    r = temp;
                }
            }

            void EnqueueChunk(Vector3Int offset, Vector3Int size) => chunks.Enqueue(GetSized((offset, size), GetMinDimension(size)));

            for (int i = textures.Count - 1; i >= 0; i--)
            {
                var sizedTexture = sortedTextures[i];
                Vector3Int textureSize = new Vector3Int(sizedTexture.Value.Texture.width, sizedTexture.Value.Texture.depth, sizedTexture.Value.Texture.height);

                var chunk = chunks.Dequeue();
                Vector3Int offset = chunk.Value.Offset;
                Vector3Int size = chunk.Value.Size;

                Vector3Int freeSpace = size - textureSize;

                Debug.Assert(freeSpace.x >= 0 && freeSpace.y >= 0 && freeSpace.z >= 0);

                int min = 0, mid = 1, max = 2;
                Ascend(freeSpace, ref min, ref mid);
                Ascend(freeSpace, ref min, ref max);
                Ascend(freeSpace, ref mid, ref max);

                if (freeSpace[max] == 0)
                    continue;
                offset[max] = textureSize[max];
                size[max] = freeSpace[max];
                EnqueueChunk(offset, size);

                if (freeSpace[mid] == 0)
                    continue;
                offset = chunk.Value.Offset;
                offset[mid] = textureSize[mid];
                size[mid] = freeSpace[mid];
                size[max] = textureSize[max];
                EnqueueChunk(offset, size);

                if (freeSpace[min] == 0)
                    continue;
                offset = chunk.Value.Offset;
                size = textureSize;
                offset[min] = textureSize[min];
                size[min] = freeSpace[min];
                EnqueueChunk(offset, size);

                offsets[sizedTexture.Value.Index] = offset;
            }

            Vector3Int packSize = new Vector3Int(MaxTextureSize, MaxTextureSize, MaxTextureSize);
            for (int i = 0; i < chunks.Count; i++)
                packSize = Vector3Int.Max(packSize, chunks[i].Value.Offset);

            ReleasePooledList(chunks.UnderlyingList);
            ReleasePooledList(sortedTextures);

            GraphicsFormat format = textures[0].graphicsFormat;
            RenderTexture atlasRT = CreateRWVolumeRT(format, packSize);
            ComputeShader blitCS = ShaderSpecsInstance.TextureUtilCS.Shader;
            int kernel = ShaderSpecsInstance.TextureUtilCS.BlitKernel;

            cmd.SetComputeTextureParam(blitCS, kernel, Bindings.Destination, atlasRT);
            for (int i = 0; i < textures.Count; i++)
            {
                Texture3D texture = textures[i];
                cmd.SetComputeVectorParam(blitCS, Bindings.Offset, (Vector3)offsets[i]);
                cmd.SetComputeTextureParam(blitCS, kernel, Bindings.Source, texture);
                cmd.DispatchCompute(blitCS, kernel, texture.width / TextureUtilCompute.BlitMipGroupSizeX, texture.height / TextureUtilCompute.BlitMipGroupSizeY, texture.depth / TextureUtilCompute.BlitMipGroupSizeZ);
            }

            return atlasRT;
        }

        private static int GetMaxEdge(Texture3D texture) => Mathf.Max(Mathf.Max(texture.width, texture.height), texture.depth);

        private static int GetMinDimension(Vector3Int vector) => Mathf.Min(Mathf.Min(vector.x, vector.y), vector.z);

        private static SizedValue<T> GetSized<T>(T value, int size) => new SizedValue<T>(value, size);

        private static List<T> GetPooledList<T>(int capacity)
        {
            List<T> list = ListPool<T>.Get();
            list.Capacity = capacity;
            return list;
        }

        private static void ReleasePooledList<T>(List<T> list) => ListPool<T>.Release(list);
    }
}
