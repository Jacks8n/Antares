using Unity.Collections;

namespace Antares.Physics
{
    public interface IFluidEmitter
    {
        FluidEmitterType EmitterType { get; }

        int ParticleCount { get; }

        int ParameterByteCount { get; }

        void GetParameters(NativeSlice<byte> buffer);

        void ClearEmitter();

        void ClearParticles();
    }
}
