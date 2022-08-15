using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;
using UnityEngine;

namespace Antares.Physics
{
    public static partial class FluidEmitter
    {
        public class Particle : IFluidEmitter<Particle.EmitterParams, Particle.ParticleParams>
        {
            [StructLayout(LayoutKind.Sequential, Size = 0)]
            public struct EmitterParams { }

            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct ParticleParams
            {
                private readonly Vector3 Position;

                private readonly Vector3 Velocity;

                public ParticleParams(Vector3 position, Vector3 velocity)
                {
                    Position = position;
                    Velocity = velocity;
                }
            }

            public FluidEmitterType EmitterType => FluidEmitterType.Particle;

            public EmitterParams EmitterParam => throw new System.NotImplementedException();

            private readonly List<ParticleParams> _descriptors = new List<ParticleParams>();

            public void GetDescriptors(NativeArray<ParticleParams> buffer)
            {
                for (int i = 0; i < _descriptors.Count; i++)
                    buffer[i] = _descriptors[i];
            }

            public void AddParticle(ParticleParams particle) => _descriptors.Add(particle);

            public void AddParticle(Vector3 position, Vector3 velocity) => AddParticle(new ParticleParams(position, velocity));

            public unsafe int GetDescriptorCount() => _descriptors.Count;

            public void ClearDescriptors() => _descriptors.Clear();
        }
    }
}
