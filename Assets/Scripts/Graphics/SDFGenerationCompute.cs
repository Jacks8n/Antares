using System;
using System.Runtime.InteropServices;
using Antares.SDF;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    public partial class AShaderSpecifications
    {
        [Serializable]
        public partial class SDFGenerationCompute : IComputeShaderSpec
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct SDFBrush
            {
                private readonly Matrix3x4 WorldToLocal;

                private readonly uint BrushType;

                private readonly uint MaterialID;

                private readonly uint ParameterCountAndOffset;

                private readonly float ScaleInv;

                public SDFBrush(SDFBrushProperty brushProperty, int parameterCount, int parameterOffset)
                {
                    WorldToLocal = brushProperty.Transform.WorldToLocal;

                    BrushType = (uint)brushProperty.BrushType;

                    MaterialID = brushProperty.MaterialID + 1;

                    ParameterCountAndOffset = (uint)(parameterCount << 24 | parameterOffset);

                    ScaleInv = brushProperty.Transform.ScaleInv;
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
                    const float tileRadiusFactor = (tileSizeInt - 1) * sqrt3Half + SDFSupremum;
                    const float gridRadiusFactor = (MatVolumeScale - 1) * sqrt3Half + SDFSupremum;

                    float gridSize = scene.GridSizeWorld;
                    BrushCullRadius = new Vector2(gridSize * tileRadiusFactor, gridSize * gridRadiusFactor);

                    SDFBrushCount = scene.BrusheCollection.Brushes.Length;
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct MipGenerationParameters
            {
                private readonly Vector3Int SceneVolumeSize;

                public MipGenerationParameters(Vector3Int sceneVolumeSize, int mip)
                {
                    Debug.Assert(mip >= 0 && mip < SceneMipCount);

                    int scale = mip - 1;
                    SceneVolumeSize = sceneVolumeSize;
                    SceneVolumeSize.x >>= scale;
                    SceneVolumeSize.y >>= scale;
                    SceneVolumeSize.z >>= scale;
                }
            }

            /// <summary>
            /// how many times scene volume is big as material volume
            /// </summary>
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

            public ConstantBufferSpans<SDFGenerationParameters> SDFGenerationParamsCBSpan { get; private set; }

            public ConstantBufferSpans<MipGenerationParameters>[] MipGenerationParamsCBSpan { get; private set; }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                GenerateMatVolumeKernel = Shader.FindKernel("GenerateMatVolume");
                GenerateMipDispatchKernel = Shader.FindKernel("GenerateMipDispatch");
                GenerateSceneVolumeKernel = Shader.FindKernel("GenerateSceneVolume");
                GenerateMipMapKernel = Shader.FindKernel("GenerateMipMap");

                SDFGenerationParamsCBSpan = specs.RegisterConstantBuffer<SDFGenerationParameters>();
                MipGenerationParamsCBSpan = new ConstantBufferSpans<MipGenerationParameters>[SceneMipCount];
                for (int i = 0; i < SceneMipCount; i++)
                    MipGenerationParamsCBSpan[i] = specs.RegisterConstantBuffer<MipGenerationParameters>();
            }
        }
    }
}
