using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Physics
{
    [ExecuteAlways]
    public class SDFPhysicsScene : MonoBehaviour
    {
        public static SDFPhysicsScene Instance { get; private set; }

        [field: SerializeField, LabelText(nameof(Gravity))]
        public float Gravity { get; set; } = -9.8f;

        [field: VerticalGroup("Specification"), SerializeField, LabelText(nameof(CellVolumeResolution))]
#if UNITY_EDITOR
        [field: OnValueChanged(nameof(GetUniformVector3))]
#endif
        public Vector3Int CellVolumeResolution { get; private set; }

        [field: VerticalGroup("Specification"), SerializeField, LabelText(nameof(ParticleKillZ))]
        public float ParticleKillZ { get; private set; }

        public Vector3 CellVolumeWorldGridInv => (Vector3)CellVolumeResolution / transform.localScale.x;

        public Vector3 CellVolumeTranslation => transform.position;

#if UNITY_EDITOR
        private void GetUniformVector3()
        {
            CellVolumeResolution = new Vector3Int(CellVolumeResolution.x, CellVolumeResolution.y, CellVolumeResolution.z);
        }
#endif

        private void OnEnable()
        {
            if (Instance)
                Instance.enabled = false;
            Instance = this;
        }

        private void OnDisable()
        {
            Instance = null;
        }
    }
}
