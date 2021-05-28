using System;
using System.Runtime.InteropServices;
using Antares.Physics;
using Antares.SDF;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    public partial class AShaderSpecs
    {
        [Serializable]
        public class FluidSolverCompute : IComputeShaderSpec
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct PhysicsSceneParameters
            {
                private readonly Matrix3x4 WorldToSceneTex;

                private readonly Vector4 CellVolumeTransform;

                private readonly Vector3 SceneTexel;

                private readonly float SDFSupremum;

                private readonly float Stiffness;

                private readonly Vector3 FluidBoundMin;
                private readonly Vector3 FluidBoundMax;

                private readonly int CellVolumeSize;

                private readonly float ParticleKillZ;

                public PhysicsSceneParameters(SDFPhysicsScene physicsScene, SDFScene scene)
                {
                    Matrix4x4 worldToScene = scene.WorldToScene;
                    Vector3 texel = scene.SizeInv;
                    WorldToSceneTex = new Matrix3x4(
                        worldToScene.GetRow(0) * texel.x,
                        worldToScene.GetRow(1) * texel.y,
                        worldToScene.GetRow(2) * texel.z);

                    Vector3 translation = physicsScene.CellVolumeTranslation;
                    CellVolumeTransform = new Vector4(physicsScene.CellVolumeWorldGridInv.x, translation.x, translation.y, translation.z);

                    SceneTexel = scene.SizeInv;

                    SDFSupremum = scene.WorldSpaceSupremum;

                    Stiffness = FluidSolverCompute.Stiffness;

                    FluidBoundMin = scene.WorldSpaceBoundMin;
                    FluidBoundMax = scene.WorldSpaceBoundMax;

                    CellVolumeSize = physicsScene.CellVolumeResolution.x;

                    ParticleKillZ = physicsScene.ParticleKillZ;
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct PhysicsFrameParameters
            {
                private readonly Vector3 TimeStep;

                private readonly float FluidGravity;

                public PhysicsFrameParameters(SDFPhysicsScene physics, float timeStep, float timeStepPrev)
                {
                    TimeStep = new Vector3(timeStep, 1f / timeStep, timeStepPrev);

                    FluidGravity = physics.Gravity;
                }
            }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct CreateParticleParameters
            {
                private readonly uint CreateParticleCount;

                public CreateParticleParameters(uint particleCount)
                {
                    CreateParticleCount = particleCount;
                }
            }

            private const float Stiffness = 1f;

            private const float SPHParticleRadius = 0.2f;

            /// <summary>
            /// must be aligned to size of kernels
            /// </summary>
            public const int MaxParticleCount = 65536;

            /// <summary>
            /// must not be greater than <see cref="MaxParticleCount"/>
            /// </summary>
            public const int MaxParticleCreateCount = 4096;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; private set; }

            public int SetupCLLKernel { get; private set; }

            public int SolveConstraintsKernel { get; private set; }

            public int FillParticlePoolKernel { get; private set; }

            public int CreateParticlesKernel { get; private set; }

            public ConstantBufferSegment<PhysicsSceneParameters> PhysicsSceneParamsCBSegment { get; private set; }

            public ConstantBufferSegment<PhysicsFrameParameters> PhysicsFrameParamCBSegment { get; private set; }

            public ConstantBufferSegment<CreateParticleParameters> CreateParticleParamsCBSegment { get; private set; }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                SetupCLLKernel = Shader.FindKernel("SetupCLL");
                SolveConstraintsKernel = Shader.FindKernel("SolveConstraints");
                FillParticlePoolKernel = Shader.FindKernel("FillParticlePool");
                CreateParticlesKernel = Shader.FindKernel("CreateParticles");

                PhysicsSceneParamsCBSegment = specs.RegisterConstantBuffer<PhysicsSceneParameters>();
                PhysicsFrameParamCBSegment = specs.RegisterConstantBuffer<PhysicsFrameParameters>();
                CreateParticleParamsCBSegment = specs.RegisterConstantBuffer<CreateParticleParameters>();
            }
        }
    }
}
