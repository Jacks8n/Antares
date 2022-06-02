using System;
using System.Runtime.InteropServices;
using Antares.Physics;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Rendering;

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

                public PhysicsSceneParameters(SDFPhysicsScene physicsScene)
                {
                    FluidGridResolution = new Vector2(
                        physicsScene.GridResolution,
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

                public PhysicsFrameParameters(SDFPhysicsScene physicsScene, float timeStep)
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

            public const int BlockCountLevel0 = 16 * 16 * 16;

            public const int BlockParticleStride = 64;

            public const int AddParticlesKernelSize = 128;

            public static int MaxAddParticleCount { get => AddParticlesKernelSize * SystemInfo.maxComputeWorkGroupSizeX; }

            public static Vector3Int GridSizeLevel0 { get => new Vector3Int(64, 64, 64); }
            public static Vector3Int GridSizeLevel1 { get => new Vector3Int(64, 64, 64); }
            public static Vector3Int GridSizeLevel2 { get => new Vector3Int(16, 16, 16); }

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; private set; }

            public int SortParticleKernel { get; private set; }

            public int ParticleToGridKernel { get; private set; }

            public int SolveGridKernel { get; private set; }

            public int GridToParticleKernel { get; private set; }

            public int GenerateIndirectArgsKernel { get; private set; }

            public int AddParticlesKernel { get; private set; }

            public ConstantBufferSpans<PhysicsSceneParameters> PhysicsSceneParamsCBSpan { get; private set; }

            public ConstantBufferSpans<PhysicsFrameParameters> PhysicsFrameParamsCBSpan { get; private set; }

            public ConstantBufferSpans<AddParticlesParameters> AddParticlesParamsCBSpan { get; private set; }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                SortParticleKernel = Shader.FindKernel("SortParticle");
                ParticleToGridKernel = Shader.FindKernel("ParticleToGrid");
                SolveGridKernel = Shader.FindKernel("SolveGrid");
                GridToParticleKernel = Shader.FindKernel("GridToParticle");
                GenerateIndirectArgsKernel = Shader.FindKernel("GenerateIndirectArgs");
                AddParticlesKernel = Shader.FindKernel("AddParticlesKernel");

                PhysicsSceneParamsCBSpan = specs.RegisterConstantBuffer<PhysicsSceneParameters>();
                PhysicsFrameParamsCBSpan = specs.RegisterConstantBuffer<PhysicsFrameParameters>();
                AddParticlesParamsCBSpan = specs.RegisterConstantBuffer<AddParticlesParameters>();
            }
        }
    }
}
