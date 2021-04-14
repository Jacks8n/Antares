using System;
using System.Runtime.InteropServices;
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

                private readonly float SDFCollisionThres;

                private readonly Vector3 CellVolumeSize;

                private readonly Vector3 CellVolumeTe;
                private readonly Vector3 CellVolumeTranslation;

                public SPHParameters(uint particleCount, float timeStep, float gravity)
                {
                    FluidParticleCount = particleCount;

                    TimeStep = new Vector2(timeStep, 1f / timeStep);

                    const float third2 = 2f / 3f;
                    Stiffness = third2 * FluidSolverCompute.Stiffness * timeStep;

                    FluidGravity = third2 * gravity * timeStep;

                    SceneTexel = ;

                    SDFCollisionThres = ;

                    CellVolumeSize = ;

                    SceneTranslation = ;
                }
            }

            private const float Stiffness = 1f;



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
