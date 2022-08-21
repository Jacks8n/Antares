using System.Collections.Generic;

namespace Antares.Physics
{
    public interface IFluidEmitter
    {
        FluidEmitterType EmitterType { get; }

        int ParticleCount { get; }

        int PropertyByteCount { get; }

        void GetProperties(List<byte> buffer);

        void ClearEmitter();

        void ClearParticles();
    }
}
