#if UNITY_EDITOR

using System.IO;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace Antares.SDF
{
    public enum SDFTestObjectType { Sphere }

    public class SDFTestObject : OdinEditorWindow
    {
        public SDFTestObjectType Type;

        public Vector3Int Size;

        [ShowIf("@Type==SDFTestObjectType.Sphere")]
        public float Radius;

        [FolderPath]
        public string SaveTo;

        [MenuItem("SDF/Test Object")]
        private static void OpenWindow()
        {
            GetWindow<SDFTestObject>().Show();
        }

        [Button]
        private void Create()
        {
            Texture3D sdf = null;

            switch (Type)
            {
                case SDFTestObjectType.Sphere:
                    sdf = SDFGenerator.CreateSDFTexture3D(Size.x, Size.y, Size.z, (Vector3 v) => v.magnitude - Radius);
                    break;
                default:
                    Debug.LogError($"not supported type: {Type}");
                    break;
            }
            Debug.Assert(sdf);

            string path = SaveTo + Path.DirectorySeparatorChar + nameof(SDFTestObject);
            string indexedPath = path + ".asset";
            for (int i = 0; File.Exists(indexedPath); i++)
                indexedPath = path + i + ".asset";

            AssetDatabase.CreateAsset(sdf, indexedPath);
            EditorUtility.SetDirty(sdf);
        }
    }
}

#endif
