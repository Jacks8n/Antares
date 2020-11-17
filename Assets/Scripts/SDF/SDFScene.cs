using UnityEngine;

namespace Antares.SDF
{
    class SDFScene : MonoBehaviour
    {
        public static SDFScene Instance {
            get {
#if UNITY_EDITOR
                SDFScene[] instances = FindObjectsOfType<SDFScene>();
                for (int i = 0; i < instances.Length; i++)
                    if (instances[i].enabled)
                        return instances[i];
                return null;
#else
                return _instance;
#endif
            }
            private set {
                _instance = value;
            }
        }

        private static SDFScene _instance;

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
