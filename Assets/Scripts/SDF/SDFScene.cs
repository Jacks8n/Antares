using UnityEngine;
using UnityEngine.Rendering;

namespace Antares.SDF
{
    class SDFScene : MonoBehaviour
    {
        public static SDFScene Instance { get; private set; }

        public RenderTexture SDF { get; private set; }

        public Vector3Int Size { get; private set; }

        static SDFScene()
        {
            Instance = null;
        }

        public SDFScene(int sizeX, int sizeY, int sizeZ)
        {
            Debug.Assert(sizeX > 0 && sizeY > 0 && sizeZ > 0);

            SDF = new RenderTexture(sizeX, sizeY, sizeZ, RenderTextureFormat.R8, RenderTextureReadWrite.Linear) {
                useMipMap = true,
                enableRandomWrite = true,
                filterMode = FilterMode.Point
            };
            SDF.Create();

            Debug.Assert(SDF.IsCreated());

            Size = new Vector3Int(sizeX, sizeY, sizeZ);
        }

        ~SDFScene()
        {
            SDF.Release();
        }

        private void OnEnable()
        {
            Instance?.OnDisable();

            Instance = this;
        }

        private void OnDisable()
        {
            Debug.Assert(Instance == this);

            Instance = null;
        }
    }
}
