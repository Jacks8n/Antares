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

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SDFGenerationParameters
            {
                private readonly Vector4 SceneToWorldRow0;
                private readonly Vector4 SceneToWorldRow1;
                private readonly Vector4 SceneToWorldRow2;

                private readonly Vector3 BrushCullRadius;

                private readonly int SDFBrushCount;

                private readonly Vector3 SceneGridSize;

                public SDFGenerationParameters(SDFScene scene)
                {
                    Matrix4x4 sceneToWorld = scene.SceneToWorld;
                    SceneToWorldRow0 = sceneToWorld.GetRow(0);
                    SceneToWorldRow1 = sceneToWorld.GetRow(1);
                    SceneToWorldRow2 = sceneToWorld.GetRow(2);

                    float gridSize = scene.GridWorldSize;
                    float sdfBand = gridSize * 4f;
                    const float cubert3Half = 0.7211247851537f;
                    const int tileSizeInt = SDFGenerationCompute.MatVolumeScale * SDFGenerationCompute.MatVolumeTileSize;
                    const float tileRadiusFactor = tileSizeInt * cubert3Half;
                    const float gridRadiusFactor = SDFGenerationCompute.MatVolumeScale * cubert3Half;
                    BrushCullRadius = new Vector3(
                        gridSize * tileRadiusFactor + sdfBand,
                        gridSize * gridRadiusFactor + sdfBand,
                        gridSize * cubert3Half + sdfBand);

                    SDFBrushCount = scene.BrusheCollection.Brushes.Length;

                    SceneGridSize = new Vector3(sdfBand, 1f / sdfBand, gridSize * gridSize);
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

            public unsafe int SDFGenerationParametersSize => sizeof(SDFGenerationParameters);

            public int SDFGenerationParametersOffset { get; private set; }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                GenerateMatVolumeKernel = Shader.FindKernel("GenerateMatVolume");
                GenerateMipDispatchKernel = Shader.FindKernel("GenerateMipDispatch");
                GenerateSceneVolumeKernel = Shader.FindKernel("GenerateSceneVolume");
                GenerateMipMapKernel = Shader.FindKernel("GenerateMipMap");

                SDFGenerationParametersOffset = specs.RegisterConstantBuffer<SDFGenerationParameters>();
            }
        }
    }
}
