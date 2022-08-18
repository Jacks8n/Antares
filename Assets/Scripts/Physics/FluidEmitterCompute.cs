using System;
using System.Runtime.InteropServices;
using Antares.Physics;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Physics
{
    public enum FluidEmitterType { Particle, Cube }
}

namespace Antares.Graphics
{
    public partial class AShaderSpecifications
    {
        [Serializable]
        public class FluidEmitterCompute : IComputeShaderSpec
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct AddParticlesParameters
            {
                private readonly uint FluidEmitterCount;

                private readonly float Mass;

                private readonly uint RandomSeed;

                public AddParticlesParameters(int fluidEmitterCount, float mass, int randomSeed = 0)
                {
                    FluidEmitterCount = (uint)fluidEmitterCount;
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

            public const int MaxEmitterParticleCountPerGroup = 1024;

            [field: SerializeField, LabelText(nameof(Shader))]
            public ComputeShader Shader { get; private set; }

            public int AddParticlesKernel { get; private set; }

            void IShaderSpec.Initialize()
            {
                AddParticlesKernel = Shader.FindKernel("AddParticles");
            }
        }
    }
}