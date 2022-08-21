using UnityEngine;

namespace Antares.Physics
{
    [ExecuteInEditMode]
    [DefaultExecutionOrder(APhysicsScene.ExecutionOrder - 1)]
    public class ParticleFluidEmitterComponent : FluidEmitterComponent<FluidEmitter.Particle>
    {
    }
}
