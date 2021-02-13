using System;
using System.Runtime.InteropServices;
using Antares.SDF;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    public partial class AShaderSpecs
    {
        [Serializable]
        public partial class SDFGenerationCompute : IShaderSpec
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SDFBrush
            {
                private readonly Vector3 WorldToLocalCol0;
                private readonly Vector3 WorldToLocalCol1;
                private readonly Vector3 WorldToLocalCol2;
                private readonly Vector3 WorldToLocalCol3;

                private readonly uint BrushType;

                private readonly uint MaterialID;

                private readonly uint ParameterOffset;

                private readonly float Scale;

                public SDFBrush(SDFBrushProperty brushProperty, uint parameterOffset)
                {
                    Matrix4x4 worldToLocal = brushProperty.Transform.WorldToLocal;
                    WorldToLocalCol0 = worldToLocal.GetColumn(0);
                    WorldToLocalCol1 = worldToLocal.GetColumn(1);
                    WorldToLocalCol2 = worldToLocal.GetColumn(2);
                    WorldToLocalCol3 = worldToLocal.GetColumn(3);

                    BrushType = (uint)brushProperty.BrushType;
                    MaterialID = brushProperty.MaterialID;
                    ParameterOffset = parameterOffset;
                    Scale = brushProperty.Transform.Scale;
                }
            }

            public const int MatVolumeScale = 4;

            /// <summary>
            /// thread number of material volume generation kernel
            /// </summary>
            public const int MatVolumeTileSize = 4;

            /// <summary>
            /// maximum number of brushes that can be applied to each grid of material volume
            /// </summary>
            public const int MaxBrushPerMatVolumeGrid = 16;

            /// <summary>
            /// maximum ratio between total brush indiex count and total grid count
            /// </summary>
            public const int MaxBrushCountFactor = MaxBrushPerMatVolumeGrid / 8;

            public const int GenerateMatVolumeKernelSize = 4;

            public const int GenerateMipDispatchKernelSize = 4;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; }

            public int GenerateMatVolumeKernel { get; private set; }

            public int GenerateMipDispatchKernel { get; private set; }

            public int GenerateSceneVolumeKernel { get; private set; }

            public int GenerateMipMapKernel { get; private set; }

            void IShaderSpec.OnAfterDeserialize()
            {
                GenerateMatVolumeKernel = Shader.FindKernel("GenerateMatVolume");
                GenerateMipDispatchKernel = Shader.FindKernel("GenerateMipDispatch");
                GenerateSceneVolumeKernel = Shader.FindKernel("GenerateSceneVolume");
                GenerateMipMapKernel = Shader.FindKernel("GenerateMipMap");
            }
        }
    }
}
