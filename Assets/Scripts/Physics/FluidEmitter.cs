using Unity.Collections;
using UnityEngine.Rendering;

namespace Antares.Physics
{
    public static partial class FluidEmitter
    {
        public static Particle ParticleEmitter { get; } = new Particle();

        public static Cube CubeEmitter { get; } = new Cube();
    }
}
