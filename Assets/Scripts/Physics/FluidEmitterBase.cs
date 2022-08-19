using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Antares.Physics
{
    [StructLayout(LayoutKind.Sequential, Size = 0)]
    public struct NullFluidEmitterProperty { }

    public abstract class FluidEmitterBase<TEmitterProperty, TParticleProperty> : IFluidEmitter
        where TEmitterProperty : unmanaged where TParticleProperty : unmanaged
    {
        protected interface IFluidEmitterDataBuilder
        {
            public void SetEmitterProperty(TEmitterProperty emitterProperty);

            public void SetParticleProperty(int index, TParticleProperty particleProperty);
        }

        private struct FluidEmitterDataBuilder : IFluidEmitterDataBuilder
        {
            private NativeSlice<TEmitterProperty> _emitterProperty;

            private NativeSlice<TParticleProperty> _particleProperties;

            public FluidEmitterDataBuilder(NativeSlice<TEmitterProperty> emitterProperty, NativeSlice<TParticleProperty> particleProperties)
            {
                _emitterProperty = emitterProperty;
                _particleProperties = particleProperties;
            }

            public void SetEmitterProperty(TEmitterProperty emitterProperty)
            {
                _emitterProperty[0] = emitterProperty;
            }

            public void SetEmitterProperty(NullFluidEmitterProperty _)
            {
                throw new Exception($"No {nameof(TEmitterProperty)} is Provided for This Emitter");
            }

            public void SetParticleProperty(int index, TParticleProperty particleParam)
            {
                _particleProperties[index] = particleParam;
            }

            public void SetParticleProperty(NullFluidEmitterProperty _)
            {
                throw new Exception($"No {nameof(TParticleProperty)} is Provided for this Emitter");
            }
        }

        public abstract FluidEmitterType EmitterType { get; }

        public abstract int ParticleCount { get; }

        public unsafe int PropertyByteCount => sizeof(TEmitterProperty) + ParticleCount * sizeof(TParticleProperty);

        public unsafe FluidEmitterBase()
        {
            Debug.Assert(sizeof(TEmitterProperty) % 4 == 0);
            Debug.Assert(sizeof(TParticleProperty) % 4 == 0);
        }

        public unsafe void GetProperties(NativeSlice<byte> buffer)
        {
            Debug.Assert(buffer.Length == PropertyByteCount);

            int emitterPropertySize = sizeof(TEmitterProperty);
            NativeSlice<TEmitterProperty> emitterPropertySlice = buffer.Slice(0, emitterPropertySize).SliceConvert<TEmitterProperty>();
            NativeSlice<TParticleProperty> particlePropertySlice = buffer.Slice(emitterPropertySize).SliceConvert<TParticleProperty>();

            FluidEmitterDataBuilder builder = new FluidEmitterDataBuilder(emitterPropertySlice, particlePropertySlice);
            GetProperties(builder);
        }

        public virtual void ClearEmitter() { ClearParticles(); }

        public virtual void ClearParticles() { }

        protected abstract void GetProperties<T>(T builder) where T : IFluidEmitterDataBuilder;
    }
}
