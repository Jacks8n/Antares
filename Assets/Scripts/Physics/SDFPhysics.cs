using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Physics
{
    public class SDFPhysics : MonoBehaviour
    {
        [field: SerializeField, LabelText(nameof(Gravity))]
        public float Gravity { get; set; } = -9.8f;

        [field: VerticalGroup("Specification"), SerializeField, LabelText(nameof(CellVolumeResolution))]
        public uint CellVolumeResolution { get; private set; }

        [field: VerticalGroup("Specification"), SerializeField, LabelText(nameof(FluidParticleCount))]
        public uint FluidParticleCount { get; private set; }

        public float CellVolumeWorldGridInv => CellVolumeResolution / transform.localScale.x;

        public Vector3 CellVolumeTranslation => transform.position;
    }
}
