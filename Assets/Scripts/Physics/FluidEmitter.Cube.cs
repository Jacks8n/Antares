using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Antares.Physics
{
    public static partial class FluidEmitter
    {
        public class Cube : IFluidEmitter<Cube.Descriptor>
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct Descriptor
            {
                private readonly Vector3 Min;

                private readonly Vector3 Max;

                private readonly Vector3 LinearVelocity;

                private readonly Vector3 AngularVelocity;

                public Descriptor(Vector3 min, Vector3 max, Vector3 linearVelocity, Vector3 angularVelocity)
                {
                    Min = min;
                    Max = max;
                    LinearVelocity = linearVelocity;
                    AngularVelocity = angularVelocity;
                }

                public Descriptor(Vector3 min, Vector3 max, Vector3 linearVelocity)
                    : this(min, max, linearVelocity, Vector3.zero)
                {
                }

                public Descriptor(Vector3 min, Vector3 max) : this(min, max, Vector3.zero)
                {
                }
            }

            public FluidEmitterType EmitterType => FluidEmitterType.Cube;

            private readonly LinkedList<Descriptor> _descriptors = new LinkedList<Descriptor>();

            public void GetDescriptors(NativeArray<Descriptor> buffer)
            {
                int i = 0;
                var node = _descriptors.First;
                while (node != null)
                    buffer[i++] = node.Value;
            }

            public LinkedListNode<Descriptor> AddEmitter(Descriptor descriptor) => _descriptors.AddLast(descriptor);

            public void RemoveEmitter(LinkedListNode<Descriptor> descriptor) => _descriptors.Remove(descriptor);

            public unsafe int GetDescriptorCount() => _descriptors.Count;

            public void ClearDescriptors() => _descriptors.Clear();
        }
    }
}
