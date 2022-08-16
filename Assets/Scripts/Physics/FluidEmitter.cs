using UnityEngine.Rendering;

namespace Antares.Physics
{
    public static partial class FluidEmitter
    {
        public static Particle ParticleEmitter { get; } = new Particle();

        public static ObjectPool<Cube> CubeEmitterPool { get; } = new ObjectPool<Cube>(null, emitter => emitter.ClearEmitter());
    }
}
