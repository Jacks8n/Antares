using System;
using System.Runtime.InteropServices;
using Sirenix.OdinInspector;
using UnityEngine;
using Antares.Physics;
using Antares.SDF;

namespace Antares.Physics
{
    public enum FluidEmitterType { Particle, Cube }
}

namespace Antares.Graphics
{
    public partial class AShaderSpecifications
    {
        [Serializable]
        public class FluidSolverCompute : IComputeShaderSpec
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct PhysicsSceneParameters
            {
                private readonly Vector3 SceneVolumeTexel;

                private readonly float Padding0;

                private readonly Vector2 FluidGridSpacing;

                public PhysicsSceneParameters(SDFScene scene, APhysicsScene physicsScene)
                {
                    SceneVolumeTexel = scene.SizeInv;
                    Padding0 = 0f;
                    FluidGridSpacing = new Vector2(physicsScene.GridSpacing, 1f / physicsScene.GridSpacing);
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct PhysicsFrameParameters
            {
                private readonly Vector2 TimeStep;

                private readonly uint CurrentFrameAddParticleCount;

                private readonly float Padding0;

                private readonly Vector3 FluidGravity;

                private readonly float Padding1;

                private readonly Vector3 FluidGridTranslation;

                private readonly float Padding2;

                private readonly Vector3 SDFSceneTranslation;

                public PhysicsFrameParameters(SDFScene scene, APhysicsScene physicsScene, float timeStep,
                    int currentFrameAddParticleCount)
                {
                    TimeStep = new Vector3(timeStep, 1f / timeStep);
                    CurrentFrameAddParticleCount = (uint)currentFrameAddParticleCount;
                    Padding0 = 0f;
                    FluidGravity = physicsScene.Gravity;
                    Padding1 = 0f;
                    FluidGridTranslation = physicsScene.transform.position;
                    Padding2 = 0f;
                    SDFSceneTranslation = scene.transform.position;
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct AddParticlesParameters
            {
                private readonly float Mass;

                private readonly uint RandomSeed;

                public AddParticlesParameters(float mass, int randomSeed = 0)
                {
                    Mass = mass;
                    RandomSeed = (uint)randomSeed;
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct FluidEmitterDispatch
            {
                private readonly uint Type;

                private readonly uint ParamterOffset;

                private readonly uint ParticleCountInclusiveSum;

                public FluidEmitterDispatch(FluidEmitterType type, int paramterOffset, int particleCountInclusiveSum)
                {
                    Type = (uint)type;
                    ParamterOffset = (uint)paramterOffset;
                    ParticleCountInclusiveSum = (uint)particleCountInclusiveSum;
                }
            }

            public const int MaxParticleCount = 1 << 20;

            public const int BlockSizeLevel0 = 4;
            public const int BlockSizeLevel1 = 4;

            public const int GridPerBlockLevel0 = BlockSizeLevel0 * BlockSizeLevel0 * BlockSizeLevel0;
            public const int GridPerBlockLevel1 = BlockSizeLevel1 * BlockSizeLevel1 * BlockSizeLevel1;

            public const int BlockCountLevel0 = 64 * 32 * 32;
            public const int BlockCountLevel1 = 64 * 32 * 32;

            public const int GridCountLevel0 = BlockCountLevel0 * GridPerBlockLevel0;
            public const int GridCountLevel1 = BlockCountLevel1 * GridPerBlockLevel1;
            public const int GridCountLevel2 = 256 * 256 * 256;

            public const int GridChannelCount = 5;

            public const int PrefixSumPartitionSize = 1024;
            public const int PrefixSumPartitionCount = (BlockCountLevel0 + PrefixSumPartitionSize - 1) / PrefixSumPartitionSize;

            public const int ClearPartitionSumsKernelSize = 128;

            public const int ClearFluidGridDispatchAlignment = 2;

            public const int MaxEmitterParticleCountPerGroup = 1024;

            public const int MaxFluidEmitterPartitionCount = 1024;

            public const int MaxFluidEmitterDispatchCount = 65536;

            public const int MaxFluidEmitterPropertyCount = MaxFluidEmitterDispatchCount * 4;

            public static Vector3Int GridSizeLevel0 { get => new Vector3Int(64 * GridChannelCount, 32, 32) * BlockSizeLevel0; }
            public static Vector3Int GridSizeLevel1 { get => new Vector3Int(64 * GridChannelCount, 32, 32) * BlockSizeLevel1; }
            public static Vector3Int GridSizeLevel2 { get => new Vector3Int(256, 256, 256); }

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; private set; }

            public int GenerateIndirectArgs0Kernel { get; private set; }
            public int GenerateIndirectArgs1Kernel { get; private set; }
            public int GenerateIndirectArgs2Kernel { get; private set; }

            public int ClearPartitionSumsKernel { get; private set; }

            public int GenerateParticleHistogramKernel { get; private set; }
            public int GenerateParticleOffsetsKernel { get; private set; }
            public int SortParticlesKernel { get; private set; }

            public int ParticleToGrid0Kernel { get; private set; }
            public int ParticleToGrid1Kernel { get; private set; }

            public int SolveGridLevel0Kernel { get; private set; }
            public int SolveGridLevel1Kernel { get; private set; }

            public int GridToParticleKernel { get; private set; }

            public int ClearFluidGridLevel0 { get; private set; }
            public int ClearFluidGridLevel1 { get; private set; }

            public int AddParticlesKernel { get; private set; }

            void IShaderSpec.Initialize()
            {
                GenerateIndirectArgs0Kernel = Shader.FindKernel("GenerateIndirectArgs0");
                GenerateIndirectArgs1Kernel = Shader.FindKernel("GenerateIndirectArgs1");
                GenerateIndirectArgs2Kernel = Shader.FindKernel("GenerateIndirectArgs2");
                ClearPartitionSumsKernel = Shader.FindKernel("ClearPartitionSums");
                GenerateParticleHistogramKernel = Shader.FindKernel("GenerateParticleHistogram");
                GenerateParticleOffsetsKernel = Shader.FindKernel("GenerateParticleOffsets");
                SortParticlesKernel = Shader.FindKernel("SortParticles");
                ParticleToGrid0Kernel = Shader.FindKernel("ParticleToGrid0");
                ParticleToGrid1Kernel = Shader.FindKernel("ParticleToGrid1");
                SolveGridLevel0Kernel = Shader.FindKernel("SolveGridLevel0");
                SolveGridLevel1Kernel = Shader.FindKernel("SolveGridLevel1");
                GridToParticleKernel = Shader.FindKernel("GridToParticle");
                ClearFluidGridLevel0 = Shader.FindKernel("ClearFluidGridLevel0");
                ClearFluidGridLevel1 = Shader.FindKernel("ClearFluidGridLevel1");
                AddParticlesKernel = Shader.FindKernel("AddParticles");
            }
        }
    }
}
