using System;
using System.Runtime.InteropServices;
using Sirenix.OdinInspector;
using UnityEngine;

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
                private readonly Vector2 FluidGridResolution;

                public PhysicsSceneParameters(Physics.APhysicsScene physicsScene)
                {
                    FluidGridResolution = new Vector2(
                        physicsScene.GridSpacing,
                        1f / physicsScene.GridSpacing);
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct PhysicsFrameParameters
            {
                private readonly Vector2 TimeStep;

                private readonly Vector2 Padding;

                private readonly Vector3 FluidGravity;

                private readonly float Padding2;

                private readonly Vector3 FluidGridTranslation;

                public PhysicsFrameParameters(Physics.APhysicsScene physicsScene, float timeStep)
                {
                    TimeStep = new Vector3(timeStep, 1f / timeStep);
                    Padding = Vector2.zero;
                    FluidGravity = physicsScene.Gravity;
                    Padding2 = 0f;
                    FluidGridTranslation = physicsScene.transform.position;
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct AddParticlesParameters
            {
                private readonly uint ParticleCount;

                private readonly float Mass;

                /// <summary>
                /// create parameters to add particles from an array
                /// </summary>
                public AddParticlesParameters(uint particleCount, float mass)
                {
                    ParticleCount = particleCount;
                    Mass = mass;
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct ParticleToAdd
            {
                private readonly Vector3 Position;

                private readonly Vector3 Velocity;

                public ParticleToAdd(Vector3 position, Vector3 velocity)
                {
                    Position = position;
                    Velocity = velocity;
                }
            }

            public const int MaxParticleCount = 1 << 20;

            public const int BlockCountLevel0 = 64 * 32 * 32;

            public const int BlockCountLevel1 = 64 * 32 * 32;

            public const int BlockSizeLevel0 = 4;

            public const int BlockSizeLevel1 = 4;

            public const int GridChannelCount = 5;

            public const int BlockParticleStride = 64;

            public const int AddParticlesKernelSize = 128;

            public static int MaxAddParticleCount { get => AddParticlesKernelSize * SystemInfo.maxComputeWorkGroupSizeX; }

            public static Vector3Int GridSizeLevel0 { get => new Vector3Int(64 * GridChannelCount, 32, 32) * BlockSizeLevel0; }
            public static Vector3Int GridSizeLevel1 { get => new Vector3Int(64 * GridChannelCount, 32, 32) * BlockSizeLevel1; }
            public static Vector3Int GridSizeLevel2 { get => new Vector3Int(256, 256, 256); }

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; private set; }

            public int SortParticleKernel { get; private set; }

            public int ParticleToGridKernel { get; private set; }

            public int SolveGridLevel0Kernel { get; private set; }

            public int SolveGridLevel1Kernel { get; private set; }

            public int GridToParticleKernel { get; private set; }

            public int GenerateIndirectArgsKernel { get; private set; }

            public int ClearFluidGridLevel0 { get; private set; }

            public int ClearFluidGridLevel1 { get; private set; }

            public int AddParticlesKernel { get; private set; }

            void IShaderSpec.Initialize()
            {
                GenerateIndirectArgsKernel = Shader.FindKernel("GenerateIndirectArgs");
                SortParticleKernel = Shader.FindKernel("SortParticle");
                ParticleToGridKernel = Shader.FindKernel("ParticleToGrid");
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
