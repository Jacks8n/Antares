using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    public partial class AShaderSpecs
    {
        [Serializable]
        public class TextureUtilCompute : IComputeShaderSpec
        {
            public const int BlitMipGroupSizeX = 8;
            public const int BlitMipGroupSizeY = 8;
            public const int BlitMipGroupSizeZ = 8;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; private set; }

            public const int BlitKernelSize = 8;

            public const int ClearKernelSize = 8;

            public int BlitKernel { get; private set; }

            public int ClearKernel { get; private set; }

            public void ClearVolume(CommandBuffer cmd, RenderTexture volume, float value, int mip = 0)
            {
                cmd.SetComputeTextureParam(Shader, ClearKernel, ARenderLayouts.ID_Destination, volume, mip);
                cmd.SetComputeFloatParam(Shader, ARenderLayouts.ID_Value, value);

                int width = volume.width >> mip;
                int height = volume.height >> mip;
                int depth = volume.volumeDepth >> mip;
                cmd.DispatchCompute(Shader, ClearKernel, width / ClearKernelSize, height / ClearKernelSize, depth / ClearKernelSize);
            }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                BlitKernel = Shader.FindKernel("Blit");
                ClearKernel = Shader.FindKernel("Clear");
            }
        }
    }
}
