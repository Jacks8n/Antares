using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Utility
{
    public interface I3DAtlasElement
    {
        public void SetAtlasOffset(Vector3Int offset);
    }

    public static class Texture3DAtlas
    {
        /// <summary>
        /// This constraint is to match the dispatch group size, which is defined as such for efficiency.
        /// </summary>
        public const int TextureSizeAlignment = 8;

        /// <param name="textures">Size of each texture must be both power of 2 and multiple of <see cref="TextureSizeAlignment"/></param>
        /// <param name="cmd">Copy operations will be recorded into it.</param>
        /// <param name="offsets">Offsets of packed textures.</param>
        /// <returns>Packed atlas</returns>
        public static RenderTexture GetAtlas<TTextures, TIndices, TElement>(TTextures textures, CommandBuffer cmd, TIndices offsets) where TTextures : IList<Texture3D> where TIndices : IList<TElement> where TElement : I3DAtlasElement
        {

            return null;
        }
    }
}
