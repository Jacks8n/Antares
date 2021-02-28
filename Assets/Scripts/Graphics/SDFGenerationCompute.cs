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

                private readonly int SDFBrushCount;

                public SDFGenerationParameters(SDFScene scene)
                {
                    Matrix4x4 sceneToWorld = scene.SceneToWorld;
                    SceneToWorldRow0 = sceneToWorld.GetRow(0);
                    SceneToWorldRow1 = sceneToWorld.GetRow(1);
                    SceneToWorldRow2 = sceneToWorld.GetRow(2);

                    const float sqrt3Half = 0.8660254037844386f;
                    const int tileSizeInt = MatVolumeScale * MatVolumeTileSize;

                    // the width of one grid of scene volume is subtracted to get tighter bound
                    //const float tileRadiusFactor = tileSizeInt * sqrt3Half + 0.48f;
                    const float tileRadiusFactor = tileSizeInt * sqrt3Half + 0.1f;
                    const float gridRadiusFactor = (MatVolumeScale - 1) * sqrt3Half + 0.1f;

                    float gridSize = scene.GridWorldSize;
                    float sdfBand = gridSize * SDFSupremum;
                    BrushCullRadius = new Vector2(
                        gridSize * tileRadiusFactor + sdfBand,
                        gridSize * gridRadiusFactor + sdfBand);

                    SDFBrushCount = scene.BrusheCollection.Brushes.Length;
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct MipGenerationParameters
            {
                private readonly Vector2 SceneGridSize;

                private readonly Vector2 SDFSupremum;

                private readonly Vector3Int SceneVolumeSize;

                public MipGenerationParameters(SDFScene scene, int mip)
                {
                    Debug.Assert(mip >= 0 && mip < SceneMipCount);

                    float gridSize = scene.GridWorldSize;
                    if (mip == 0)
                    {
                        float supremum = AShaderSpecs.SDFSupremum * gridSize;
                        SceneGridSize = new Vector2(0f, 0f);
                        SDFSupremum = new Vector2(0f, 1f / supremum);
                    }
                    else
                    {
                        gridSize *= 1 << mip;
                        float supremum = .5f * AShaderSpecs.SDFSupremum * gridSize;
                        SceneGridSize = new Vector2(gridSize, gridSize * gridSize);
                        SDFSupremum = new Vector2(supremum, .5f / supremum);
                    }

                    int scale = mip - 1;
                    SceneVolumeSize = scene.Size;
                    SceneVolumeSize.x >>= scale;
                    SceneVolumeSize.y >>= scale;
                    SceneVolumeSize.z >>= scale;
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

            public ConstantBufferSegment<MipGenerationParameters>[] MipGenerationCBuffers { get; private set; }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                GenerateMatVolumeKernel = Shader.FindKernel("GenerateMatVolume");
                GenerateMipDispatchKernel = Shader.FindKernel("GenerateMipDispatch");
                GenerateSceneVolumeKernel = Shader.FindKernel("GenerateSceneVolume");
                GenerateMipMapKernel = Shader.FindKernel("GenerateMipMap");

                SDFGenerationCBuffer = specs.RegisterConstantBuffer<SDFGenerationParameters>();
                MipGenerationCBuffers = new ConstantBufferSegment<MipGenerationParameters>[SceneMipCount];
                for (int i = 0; i < SceneMipCount; i++)
                    MipGenerationCBuffers[i] = specs.RegisterConstantBuffer<MipGenerationParameters>();
            }
        }
    }
}
