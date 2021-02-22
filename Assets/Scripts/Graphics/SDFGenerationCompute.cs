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
        public partial class SDFGenerationCompute : IComputeShaderSpec
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SDFBrush
            {
                private readonly Vector4 WorldToLocalRow0;
                private readonly Vector4 WorldToLocalRow1;
                private readonly Vector4 WorldToLocalRow2;

                private readonly uint BrushType;

                private readonly uint MaterialID;

                private readonly uint ParameterCountAndOffset;

                private readonly float Scale;

                public SDFBrush(SDFBrushProperty brushProperty, int parameterCount, int parameterOffset)
                {
                    Matrix4x4 worldToLocal = brushProperty.Transform.WorldToLocal;
                    WorldToLocalRow0 = worldToLocal.GetRow(0);
                    WorldToLocalRow1 = worldToLocal.GetRow(1);
                    WorldToLocalRow2 = worldToLocal.GetRow(2);

                    BrushType = (uint)brushProperty.BrushType;

                    MaterialID = brushProperty.MaterialID + 1;

                    ParameterCountAndOffset = (uint)(parameterCount << 24 | parameterOffset);

                    Scale = brushProperty.Transform.Scale;
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SDFGenerationParameters
            {
                private readonly Vector4 SceneToWorldRow0;
                private readonly Vector4 SceneToWorldRow1;
                private readonly Vector4 SceneToWorldRow2;

                private readonly Vector2 BrushCullRadius;

                private readonly Vector2 SceneGridSize;

                private readonly int SDFBrushCount;

                public SDFGenerationParameters(SDFScene scene)
                {
                    Matrix4x4 sceneToWorld = scene.SceneToWorld;
                    SceneToWorldRow0 = sceneToWorld.GetRow(0);
                    SceneToWorldRow1 = sceneToWorld.GetRow(1);
                    SceneToWorldRow2 = sceneToWorld.GetRow(2);

                    float gridSize = scene.GridWorldSize;
                    float sdfBand = gridSize * SDFSupremum;
                    const float cubert3Half = 0.7211247851537f;
                    const int tileSizeInt = MatVolumeScale * MatVolumeTileSize;
                    const float tileRadiusFactor = tileSizeInt * cubert3Half;
                    const float gridRadiusFactor = MatVolumeScale * cubert3Half;
                    BrushCullRadius = new Vector2(
                        gridSize * tileRadiusFactor + sdfBand,
                        gridSize * gridRadiusFactor + sdfBand);

                    SDFBrushCount = scene.BrusheCollection.Brushes.Length;

                    SceneGridSize = new Vector2(gridSize, gridSize * gridSize);
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct MipGenerationParameters
            {
                private readonly Vector2 SDFSupremum;

                private readonly int VolumeMipLevel;

                public MipGenerationParameters(SDFScene scene, int mip)
                {
                    float supremum = (1 << mip) * AShaderSpecs.SDFSupremum * scene.GridWorldSize;
                    SDFSupremum = new Vector2(supremum, 1f / supremum);

                    VolumeMipLevel = mip;
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
            public ComputeShader Shader { get; private set; }

            public int GenerateMatVolumeKernel { get; private set; }

            public int GenerateMipDispatchKernel { get; private set; }

            public int GenerateSceneVolumeKernel { get; private set; }

            public int GenerateMipMapKernel { get; private set; }

            public ConstantBufferSegment<SDFGenerationParameters> SDFGenerationCBuffer { get; private set; }

            public ConstantBufferSegment<MipGenerationParameters> MipGenerationCBuffer { get; private set; }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                GenerateMatVolumeKernel = Shader.FindKernel("GenerateMatVolume");
                GenerateMipDispatchKernel = Shader.FindKernel("GenerateMipDispatch");
                GenerateSceneVolumeKernel = Shader.FindKernel("GenerateSceneVolume");
                GenerateMipMapKernel = Shader.FindKernel("GenerateMipMap");

                SDFGenerationCBuffer = specs.RegisterConstantBuffer<SDFGenerationParameters>();
                MipGenerationCBuffer = specs.RegisterConstantBuffer<MipGenerationParameters>();
            }
        }
    }
}
