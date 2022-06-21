using System;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;

namespace Antares.Graphics
{
    public partial class AShaderSpecifications
    {
        [Serializable]
        public class TextureUtilCompute : IComputeShaderSpec
        {
            // todo
            // refactoring required

            public const int BlitMipGroupSizeX = 8;
            public const int BlitMipGroupSizeY = 8;
            public const int BlitMipGroupSizeZ = 8;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; private set; }

            public const int BlitKernelSize = 8;

            public const int ClearKernelSize = 8;

            public const int ClearBufferKernelSize = 64;

            public int BlitKernel { get; private set; }

            public int ClearKernel { get; private set; }

            public int ClearIntKernel { get; private set; }

            public int ClearBufferKernel { get; private set; }

            public void ClearVolume(CommandBuffer cmd, RenderTexture volume, float value, int mip = 0)
            {
                cmd.SetComputeTextureParam(Shader, ClearKernel, Bindings.Destination, volume, mip);
                cmd.SetComputeFloatParam(Shader, Bindings.Value, value);

                int width = volume.width >> mip;
                int height = volume.height >> mip;
                int depth = volume.volumeDepth >> mip;
                cmd.SetComputeIntParams(Shader, Bindings.Bound, width, height, depth);

                int groupX = (width + ClearKernelSize - 1) / ClearKernelSize;
                int groupY = (height + ClearKernelSize - 1) / ClearKernelSize;
                int groupZ = (depth + ClearKernelSize - 1) / ClearKernelSize;
                cmd.DispatchCompute(Shader, ClearKernel, groupX, groupY, groupZ);
            }

            public void ClearVolume(CommandBuffer cmd, RenderTexture volume, int value, int mip = 0)
            {
                cmd.SetComputeTextureParam(Shader, ClearIntKernel, Bindings.DestinationInt, volume, mip);
                cmd.SetComputeIntParam(Shader, Bindings.ValueInt, value);

                int width = volume.width >> mip;
                int height = volume.height >> mip;
                int depth = volume.volumeDepth >> mip;
                cmd.SetComputeIntParams(Shader, Bindings.Bound, width, height, depth);

                int groupX = (width + ClearKernelSize - 1) / ClearKernelSize;
                int groupY = (height + ClearKernelSize - 1) / ClearKernelSize;
                int groupZ = (depth + ClearKernelSize - 1) / ClearKernelSize;
                cmd.DispatchCompute(Shader, ClearIntKernel, groupX, groupY, groupZ);
            }

            public void ClearBuffer(CommandBuffer cmd, ComputeBuffer buffer, int value)
            {
                cmd.SetComputeBufferParam(Shader, ClearBufferKernel, Bindings.DestinationBuffer, buffer);
                cmd.SetComputeIntParam(Shader, Bindings.ValueInt, value);
                cmd.SetComputeIntParams(Shader, Bindings.Bound, buffer.count, 0, 0);
                
                int group = (buffer.count + ClearBufferKernelSize - 1) / ClearBufferKernelSize;
                cmd.DispatchCompute(Shader, ClearBufferKernel, group, 1, 1);
            }

            void IShaderSpec.Initialize()
            {
                BlitKernel = Shader.FindKernel("Blit");
                ClearKernel = Shader.FindKernel("Clear");
                ClearIntKernel = Shader.FindKernel("ClearInt");
                ClearBufferKernel = Shader.FindKernel("ClearBuffer");
            }
        }
    }
}
