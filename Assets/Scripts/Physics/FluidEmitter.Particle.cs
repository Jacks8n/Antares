using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Antares.Physics
{
    public static partial class FluidEmitter
    {
        public class Particle : FluidEmitterBase<NullFluidEmitterParameter, Particle.Parameters>
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Parameters
            {
                private readonly Vector3 Position;

                private readonly Vector3 Velocity;

                public Parameters(Vector3 position, Vector3 velocity)
                {
                    Position = position;
                    Velocity = velocity;
                }
            }

            public override FluidEmitterType EmitterType => FluidEmitterType.Particle;

            public override int ParticleCount => _particles.Count;

            private List<Parameters> _particles;

            public Particle() : base()
            {
                _particles = new List<Parameters>();
            }

            public void AddParticle(Parameters parameters) => _particles.Add(parameters);

            public void AddParticle(Vector3 position, Vector3 velocity) => AddParticle(new Parameters(position, velocity));

            public override void ClearParticles() => _particles.Clear();

            protected override void GetParameters<T>(T builder)
            {
                for (int i = 0; i < _particles.Count; i++)
                    builder.SetParticleParameters(i, _particles[i]);
            }
        }
    }
}
