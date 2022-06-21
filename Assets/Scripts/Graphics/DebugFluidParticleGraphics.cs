using System;
using System.Runtime.InteropServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Graphics
{
    public partial class AShaderSpecifications
    {
        [Serializable]
        public class DebugFluidParticleGraphics : IShaderSpec
        {
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public struct DebugFluidParticleParameters
            {
                private readonly Vector3 CameraPosition;

                private readonly float Padding;

                private readonly Vector3 ParticleUp;

                public DebugFluidParticleParameters(Vector3 cameraPosition, Vector3 particleUp)
                {
                    CameraPosition = cameraPosition;
                    Padding = 0f;
                    ParticleUp = particleUp;
                }
            }

            [field: SerializeField, LabelText(nameof(Material))]
            public Material Material { get; private set; }

            void IShaderSpec.Initialize()
            {
            }
        }
    }
}
