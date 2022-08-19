using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Antares.Physics
{
    public static partial class FluidEmitter
    {
        public class Particle : FluidEmitterBase<NullFluidEmitterProperty, Particle.Parameters>
        {
            [Serializable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Parameters
            {
                [SerializeField]
                private Vector3 Position;

                [SerializeField]
                private Vector3 Velocity;

                public Parameters(Vector3 position, Vector3 velocity)
                {
                    Position = position;
                    Velocity = velocity;
                }
            }

            public override FluidEmitterType EmitterType => FluidEmitterType.Particle;

            public override int ParticleCount => _particles.Count;

            [SerializeField]
            private List<Parameters> _particles;

            public Particle() : base()
            {
                _particles = new List<Parameters>();
            }

            public void AddParticle(Parameters parameters) => _particles.Add(parameters);

            public void AddParticle(Vector3 position, Vector3 velocity) => AddParticle(new Parameters(position, velocity));

            public override void ClearParticles() => _particles.Clear();

            protected override void GetProperties<T>(T builder)
            {
                for (int i = 0; i < _particles.Count; i++)
                    builder.SetParticleProperty(i, _particles[i]);
            }
        }
    }
}
