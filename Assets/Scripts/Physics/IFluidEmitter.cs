using Unity.Collections;

namespace Antares.Physics
{
    public interface IFluidEmitter
    {
        FluidEmitterType EmitterType { get; }

        int ParticleCount { get; }

        int PropertyByteCount { get; }

        void GetProperties(NativeSlice<byte> buffer);

        void ClearEmitter();

        void ClearParticles();
    }
}
