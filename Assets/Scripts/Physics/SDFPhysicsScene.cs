using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.Physics
{
    [ExecuteAlways]
    public class SDFPhysicsScene : MonoBehaviour
    {
        public static SDFPhysicsScene Instance { get; private set; }

        [field: SerializeField, LabelText(nameof(Gravity))]
        public Vector3 Gravity { get; set; } = new Vector3(0f, 0f, -9.8f);

        [field: VerticalGroup("Specification"), SerializeField, LabelText(nameof(GridResolution))]
        public int GridResolution { get; private set; }

        public float GridSpacing { get => 1f / GridResolution; }

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
