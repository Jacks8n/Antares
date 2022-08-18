using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Antares.Physics;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor.Validation;
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
                private int _emitterDispatchCount;

                private int _parameterByteCount;

                private int _partitionCount;

                private int _groupParticleCount;

                private int _groupParticleCountInclusiveSum;

                private NativeArray<FluidEmitter> _emitterDispatchBuffer;

                private NativeArray<int> _partitionBuffer;

                private NativeArray<byte> _parameterBuffer;

                public unsafe void Reserve<T>(List<T> emitters) where T : IFluidEmitter
                {
                    _emitterDispatchCount += emitters.Count;

                    for (int i = 0; i < emitters.Count; i++)
                    {
                        T emitter = emitters[i];

                        _groupParticleCount += emitter.ParticleCount;
                        if (_groupParticleCount > MaxEmitterParticleCountPerGroup)
                        {
                            int groupCount = _groupParticleCount / MaxEmitterParticleCountPerGroup;
                            _emitterDispatchCount += groupCount;
                            _partitionCount += groupCount;
                            _groupParticleCount = _groupParticleCount % MaxEmitterParticleCountPerGroup;
                        }

                        _parameterByteCount += emitter.ParameterByteCount;
                    }
                }

                public void Allocate()
                {
                    _emitterDispatchBuffer = new NativeArray<FluidEmitter>(_emitterDispatchCount, Allocator.Temp);
                    _partitionBuffer = new NativeArray<int>(_partitionCount, Allocator.Temp);
                    _parameterBuffer = new NativeArray<byte>(_parameterByteCount, Allocator.Temp);

                    _emitterDispatchCount = 0;
                    _parameterByteCount = 0;
                    _partitionCount = 0;
                    _groupParticleCount = 0;
                }

                public void AddEmitters<T>(List<T> emitters) where T : IFluidEmitter
                {
                    for (int i = 0; i < emitters.Count; i++)
                    {
                        T emitter = emitters[i];

                        int parameterByteCount = emitter.ParameterByteCount;
                        emitter.GetParameters(_parameterBuffer.Slice(_parameterByteCount, parameterByteCount));

                        int particleCount = emitter.ParticleCount;
                        _groupParticleCount += particleCount;

                        FluidEmitterType emitterType = emitter.EmitterType;
                        while (_groupParticleCount > MaxEmitterParticleCountPerGroup)
                        {
                            _emitterDispatchBuffer[_emitterDispatchCount++] =
                                new FluidEmitter(emitterType, parameterByteCount, _groupParticleCountInclusiveSum);

                            _groupParticleCount -= MaxEmitterParticleCountPerGroup;
                            particleCount = _groupParticleCount;
                        }

                        _groupParticleCountInclusiveSum += particleCount;

                        _emitterDispatchBuffer[_emitterDispatchCount] =
                            new FluidEmitter(emitter.EmitterType, parameterByteCount, _groupParticleCountInclusiveSum);

                        _parameterByteCount += emitter.ParameterByteCount;
                    }

                    _emitterDispatchCount += emitters.Count;
                }
            }

            private const int MaxEmitterParticleCountPerGroup = 1024;

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