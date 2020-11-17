using UnityEngine;

namespace Antares.SDF
{
    class SDFScene : MonoBehaviour
    {
        public static SDFScene Instance { get; private set; }

        public Texture3D Scene => _scene;

        public Vector3Int Size => new Vector3Int(Scene.width, Scene.height, Scene.depth);

        [SerializeField]
        private Texture3D _scene;

        public void GetBound(out Vector3 min, out Vector3 max)
        {
            Vector3 halfSize = (Vector3)Size * .5f;
            min = transform.position - halfSize;
            max = transform.position + halfSize;
        }

        private void OnEnable()
        {
            if (Instance)
                Instance.enabled = false;
            Instance = this;
        }
    }
}
