using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    public partial class AShaderSpecs
    {
        [Serializable]
        public class AtlasBlitCompute : IShaderSpec
        {
            public const int BlitMipGroupSizeX = 8;
            public const int BlitMipGroupSizeY = 8;
            public const int BlitMipGroupSizeZ = 8;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; }

            public int BlitKernel { get; private set; }

            void IShaderSpec.OnAfterDeserialize() => BlitKernel = Shader.FindKernel("Blit");
        }
    }
}
