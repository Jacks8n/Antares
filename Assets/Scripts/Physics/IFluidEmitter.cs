using Unity.Collections;

namespace Antares.Physics
{
    public enum FluidEmitterType { Particle, Cube }

    public abstract class FluidEmitterBase<TEmitterParam, TParticleParam>
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

            public void SetParticleParameters(int index, TParticleParam particleParam)
            {
                _particleParameters[index] = particleParam;
            }
        }

        public abstract FluidEmitterType EmitterType { get; }

        public abstract int ParticleCount { get; }

        public unsafe void GetParameters(NativeSlice<byte> buffer)
        {
            int emitterParamSize = sizeof(TEmitterParam);
            NativeSlice<TEmitterParam> emitterParamSlice = buffer.Slice(0, emitterParamSize).SliceConvert<TEmitterParam>();
            NativeSlice<TParticleParam> particleParamSlice = buffer.Slice(emitterParamSize).SliceConvert<TParticleParam>();

            FluidEmitterDataBuilder builder = new FluidEmitterDataBuilder(emitterParamSlice, particleParamSlice);
            GetParameters(builder);
        }

        public abstract void ClearParticles();

        protected abstract void GetParameters<T>(T builder) where T : IFluidEmitterDataBuilder;
    }
}
