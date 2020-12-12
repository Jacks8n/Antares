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

        public float Scale {
            get {
#if UNITY_EDITOR
                Vector3 scale = transform.localScale;
                if (scale.x != scale.y || scale.y != scale.z || scale.z != scale.x)
                {
                    Debug.LogWarning("scale of sdf scene should be uniform");
                    transform.localScale = new Vector3(scale.x, scale.x, scale.x);
                }
#endif
                return transform.localScale.x;
            }
        }

        public Vector3Int Size { get; private set; }

        public Vector3 SizeInv { get; private set; }

        [SerializeField]
        private Texture3D _scene;

        public void GetBound(out Vector3 min, out Vector3 max)
        {
            Vector3 halfSize = (Vector3)Size * .5f;
            min = transform.position - halfSize;
            max = transform.position + halfSize;
        }

        public Vector3 WorldToSceneVector(Vector3 vec) => transform.worldToLocalMatrix.MultiplyVector(Vector3.Scale(vec, SizeInv));

        public Vector3 WorldToScenePoint(Vector3 pos) => transform.worldToLocalMatrix.MultiplyPoint(Vector3.Scale(pos, SizeInv));

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
    }
}
