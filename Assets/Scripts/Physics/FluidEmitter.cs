using UnityEngine.Rendering;

namespace Antares.Physics
{
    public static partial class FluidEmitter
    {
        public static ObjectPool<Particle> ParticleEmitter { get; } = GetEmitterPool<Particle>();

        public static ObjectPool<Cube> CubeEmitterPool { get; } = GetEmitterPool<Cube>();

        private static ObjectPool<T> GetEmitterPool<T>() where T : IFluidEmitter, new()
        {
            return new ObjectPool<T>(null, emitter => emitter.ClearEmitter());
        }
    }
}
