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
            public struct SPHParameters
            {
                private readonly Matrix3x4 WorldToSceneTex;

                private readonly uint FluidParticleCount;

                private readonly Vector2 TimeStep;

                private readonly float Stiffness;

                private readonly float FluidGravity;

                private readonly Vector3 SceneTexel;

                private readonly float CellVolumeTexelInv;

                private readonly Vector3 CellVolumeTranslation;

                private readonly float SDFCollisionThres;

                private readonly uint CellVolumeSize;

                public SPHParameters(float timeStep, SDFPhysics physics, SDFScene scene)
                {
                    Matrix4x4 worldToScene = scene.WorldToScene;
                    Vector3 texel = scene.SizeInv;
                    WorldToSceneTex = new Matrix3x4(
                        worldToScene.GetRow(0) * texel.x,
                        worldToScene.GetRow(1) * texel.y,
                        worldToScene.GetRow(2) * texel.z);

                    FluidParticleCount = physics.FluidParticleCount;

                    TimeStep = new Vector2(timeStep, 1f / timeStep);

                    const float third2 = 2f / 3f;
                    Stiffness = third2 * FluidSolverCompute.Stiffness * timeStep;

                    FluidGravity = third2 * physics.Gravity * timeStep;

                    SceneTexel = scene.SizeInv;

                    CellVolumeTexelInv = physics.CellVolumeWorldGridInv;

                    CellVolumeTranslation = physics.CellVolumeTranslation;

                    SDFCollisionThres = SPHParticleRadius / scene.WorldSpaceSupremum;

                    CellVolumeSize = physics.CellVolumeResolution;
                }
            }

            private const float Stiffness = 1f;

            private const float SPHParticleRadius = 0.2f;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; private set; }

            public int SetupCLLKernel { get; private set; }

            public int SolveConstraintsKernel { get; private set; }

            public ConstantBufferSegment<SPHParameters> SPHParamsCBSegment { get; private set; }

            void IShaderSpec.OnAfterDeserialize<T>(T specs)
            {
                SetupCLLKernel = Shader.FindKernel("SetupCLL");
                SolveConstraintsKernel = Shader.FindKernel("SolveConstraints");

                SPHParamsCBSegment = specs.RegisterConstantBuffer<SPHParameters>();
            }
        }
    }
}
