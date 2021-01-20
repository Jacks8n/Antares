using UnityEngine;

namespace Antares.SDF
{
    [ExecuteAlways]
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

        public Vector3Int Size { get; private set; }

        public Vector3 SizeInv { get; private set; }

        [SerializeField]
        private Texture3D _scene;

        public Vector3 WorldToSceneVector(Vector3 vec) => transform.worldToLocalMatrix.MultiplyVector(vec);

        public Vector3 WorldToScenePoint(Vector3 pos) => transform.worldToLocalMatrix.MultiplyPoint(pos);

        private void OnEnable()
        {
            if (_instance && _instance != this)
                _instance.enabled = false;
            _instance = this;

            if (_scene)
            {
                Size = new Vector3Int(_scene.width, _scene.height, _scene.depth);
                SizeInv = new Vector3(1f / Size.x, 1f / Size.y, 1f / Size.z);
            }

            Debug.Log($"scene size: {Size}");
        }

        private void Update()
        {
#if UNITY_EDITOR
            Vector3 scale = transform.localScale;
            if (scale.x != scale.y || scale.y != scale.z || scale.z != scale.x)
            {
                Debug.LogWarning("Only the affine transfrom is permitted");
                transform.localScale = new Vector3(scale.x, scale.x, scale.x);
            }
#endif
        }
    }
}
