using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Antares.Physics;
using Sirenix.OdinInspector;
using Unity.Collections;
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
            public struct FluidEmitter
            {
                private readonly uint Type;

                private readonly uint ParamterOffset;

                private readonly uint ParticleCountInclusiveSum;

                public FluidEmitter(FluidEmitterType type, int paramterOffset, int particleCountInclusiveSum)
                {
                    Type = (uint)type;
                    ParamterOffset = (uint)paramterOffset;
                    ParticleCountInclusiveSum = (uint)particleCountInclusiveSum;
                }
            }

            public struct EmitterParameterBufferBuilder
            {
                private int _parameterBufferSize;

                private NativeArray<byte> _parameterBuffer;

                public void Reserve<T>(List<T> emitters) where T : IFluidEmitter
                {
                    for (int i = 0; i < emitters.Count; i++)
                        _parameterBufferSize += emitters[i].ParameterByteCount;
                }

                public void Allocate()
                {
                    
                }

                public static void GetEmitterParameterBuffer<T>(List<T> emitters, NativeSlice<byte> buffer) where T : IFluidEmitter
                {
                    int offset = 0;
                    for (int i = 0; i < emitters.Count; i++)
                    {
                        T emitter = emitters[i];

                        int byteCount = emitter.ParameterByteCount;
                        emitter.GetParameters(buffer.Slice(offset, byteCount));

                        offset += emitter.ParameterByteCount;
                    }
                }
            }

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