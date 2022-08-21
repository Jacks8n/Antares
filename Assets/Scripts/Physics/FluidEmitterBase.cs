using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;

namespace Antares.Physics
{
    [StructLayout(LayoutKind.Sequential, Size = 0)]
    public struct NullFluidEmitterProperty { }

    public abstract class FluidEmitterBase<TEmitterProperty, TParticleProperty> : IFluidEmitter
        where TEmitterProperty : unmanaged where TParticleProperty : unmanaged
    {
        protected interface IFluidEmitterDataBuilder<T> where T : unmanaged
        {
            public void AddProperty(T property);
        }

        private struct FluidEmitterDataBuilder<T> : IFluidEmitterDataBuilder<T> where T : unmanaged
        {
            private readonly List<byte> _buffer;

            public FluidEmitterDataBuilder(List<byte> buffer)
            {
                _buffer = buffer;
            }

            public unsafe void AddProperty(T property)
            {
                byte* ptr = stackalloc byte[sizeof(T)];
                UnsafeUtility.WriteArrayElement(ptr, 0, property);

                for (int i = 0; i < sizeof(T); i++)
                    _buffer.Add(ptr[i]);
            }

            public void AddProperty(NullFluidEmitterProperty _)
            {
                throw new Exception($"No {nameof(TEmitterProperty)} is Provided for This Emitter");
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

        public unsafe void GetProperties(List<byte> buffer)
        {
            FluidEmitterDataBuilder<TEmitterProperty> emitterBuilder = new FluidEmitterDataBuilder<TEmitterProperty>(buffer);
            GetEmitterProperties(emitterBuilder);

            FluidEmitterDataBuilder<TParticleProperty> particleBuilder = new FluidEmitterDataBuilder<TParticleProperty>(buffer);
            GetParticleProperties(particleBuilder);
        }

        public virtual void ClearEmitter() { ClearParticles(); }

        public virtual void ClearParticles() { }

        protected virtual void GetEmitterProperties<T>(T builder) where T : IFluidEmitterDataBuilder<TEmitterProperty> { }

        protected virtual void GetParticleProperties<T>(T builder) where T : IFluidEmitterDataBuilder<TParticleProperty> { }
    }
}
