using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Antares.Physics
{
    [StructLayout(LayoutKind.Sequential, Size = 0)]
    public struct NullFluidEmitterParameter { }

    public abstract class FluidEmitterBase<TEmitterParam, TParticleParam> : IFluidEmitter
        where TEmitterParam : unmanaged where TParticleParam : unmanaged
    {
        protected interface IFluidEmitterDataBuilder
        {
            public void SetEmitterParameters(TEmitterParam emitterParam);

            public void SetParticleParameters(int index, TParticleParam particleParam);
        }

        private struct FluidEmitterDataBuilder : IFluidEmitterDataBuilder
        {
            private NativeSlice<TEmitterParam> _emitterParameters;

            private NativeSlice<TParticleParam> _particleParameters;

            public FluidEmitterDataBuilder(NativeSlice<TEmitterParam> emitterParameters, NativeSlice<TParticleParam> particleParameters)
            {
                _emitterParameters = emitterParameters;
                _particleParameters = particleParameters;
            }

            public void SetEmitterParameters(TEmitterParam emitterParam)
            {
                _emitterParameters[0] = emitterParam;
            }

            public void SetEmitterParameters(NullFluidEmitterParameter _)
            {
                throw new Exception($"No {nameof(TEmitterParam)} is Provided for This Emitter");
            }

            public void SetParticleParameters(int index, TParticleParam particleParam)
            {
                _particleParameters[index] = particleParam;
            }

            public void SetParticleParameters(NullFluidEmitterParameter _)
            {
                throw new Exception($"No {nameof(TParticleParam)} is Provided for this Emitter");
            }
        }

        public abstract FluidEmitterType EmitterType { get; }

        public abstract int ParticleCount { get; }

        public unsafe int ParameterByteCount => sizeof(TEmitterParam) + ParticleCount * sizeof(TParticleParam);

        public unsafe FluidEmitterBase()
        {
            Debug.Assert(sizeof(TEmitterParam) % 4 == 0);
            Debug.Assert(sizeof(TParticleParam) % 4 == 0);
        }

        public unsafe void GetParameters(NativeSlice<byte> buffer)
        {
            Debug.Assert(buffer.Length == ParameterByteCount);

            int emitterParamSize = sizeof(TEmitterParam);
            NativeSlice<TEmitterParam> emitterParamSlice = buffer.Slice(0, emitterParamSize).SliceConvert<TEmitterParam>();
            NativeSlice<TParticleParam> particleParamSlice = buffer.Slice(emitterParamSize).SliceConvert<TParticleParam>();

            FluidEmitterDataBuilder builder = new FluidEmitterDataBuilder(emitterParamSlice, particleParamSlice);
            GetParameters(builder);
        }

        public virtual void ClearEmitter() { ClearParticles(); }

        public virtual void ClearParticles() { }

        protected abstract void GetParameters<T>(T builder) where T : IFluidEmitterDataBuilder;
    }
}
