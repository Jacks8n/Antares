using UnityEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sirenix.OdinInspector;
using Sirenix.OdinInspector.Editor;
using UnityEngine;
using System.IO;

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

            string path = Path.Combine(SaveTo, nameof(SDFTestObject));
            string indexedPath = path;
            for (int i = 0; File.Exists(indexedPath); i++)
                indexedPath = path + i;

            AssetDatabase.CreateAsset(sdf, SaveTo);
            EditorUtility.SetDirty(sdf);
        }
    }
}
