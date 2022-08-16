using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Antares.Physics
{
    public static partial class FluidEmitter
    {
        [Serializable]
        public class Cube : FluidEmitterBase<Cube.Parameters, NullFluidEmitterParameter>
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Parameters
            {
                private readonly Vector3 Min;

                private readonly Vector3 Max;

                private readonly Vector3 LinearVelocity;

                private readonly Vector3 AngularVelocity;

                public Parameters(Vector3 min, Vector3 max, Vector3 linearVelocity, Vector3 angularVelocity)
                {
                    Min = min;
                    Max = max;
                    LinearVelocity = linearVelocity;
                    AngularVelocity = angularVelocity;
                }
            }

            [field: SerializeField]
            public Vector3 Min { get; set; }

            [field: SerializeField]
            public Vector3 Max { get; set; }

            [field: SerializeField]
            public Vector3 LinearVelocity { get; set; }

            [field: SerializeField]
            public Vector3 AngularVelocity { get; set; }

            public override FluidEmitterType EmitterType => FluidEmitterType.Cube;

            public override int ParticleCount => _particleCount;

            [SerializeField, Min(0)]
            private int _particleCount;

            public Cube(Vector3 min, Vector3 max, Vector3 linearVelocity, Vector3 angularVelocity, int particleCount = 0) : base()
            {
                Min = min;
                Max = max;
                LinearVelocity = linearVelocity;
                AngularVelocity = angularVelocity;
                _particleCount = particleCount;
            }

            public Cube(Vector3 min, Vector3 max, Vector3 linearVelocity, int particleCount = 0)
                : this(min, max, linearVelocity, Vector3.zero, particleCount)
            {
            }

            public Cube(Vector3 min, Vector3 max, int particleCount = 0)
                : this(min, max, Vector3.zero, particleCount)
            {
            }

            public Cube() : this(Vector3.zero, Vector3.zero)
            {
            }

            public void AddParticles(int count)
            {
                Debug.Assert(count >= 0);

                _particleCount += count;
            }

            public override void ClearEmitter()
            {
                Min = Vector3.zero;
                Max = Vector3.zero;
                LinearVelocity = Vector3.zero;
                AngularVelocity = Vector3.zero;

                ClearParticles();
            }

            public override void ClearParticles() => _particleCount = 0;

            protected override void GetParameters<T>(T builder)
            {
                builder.SetEmitterParameters(new Parameters(Min, Max, LinearVelocity, AngularVelocity));
            }
        }
    }
}
