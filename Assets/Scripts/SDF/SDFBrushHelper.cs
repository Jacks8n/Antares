#if UNITY_EDITOR

using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Antares.SDF
{
    [ExecuteAlways, RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class SDFBrushHelper : MonoBehaviour
    {
        [SerializeField]
        private ISDFShape _shape;

        [SerializeField, ShowIf("@_shape is " + nameof(SDFShape.Numerical))]
        private Texture3D _bakedTexture;

        [SerializeField]
        private int _materialID;

        /// <returns>whether the brush is numerical</returns>
        public bool GetBrush(ref SDFBrush brush, ref Texture3D atlas)
        {
            if (_shape is SDFShape.Numerical)
            {
                brush = SDFBrush.FromShape(transform, _shape, _materialID);
                atlas = _bakedTexture;
                return true;
            }
            else
            {
                brush = SDFBrush.FromShape(transform, _shape, _materialID);
                return false;
            }
        }

        private static void UpdatePreview()
        {
            // TODO
        }

        private void Awake()
        {
            const string editorOnly = "EditorOnly";
            if (!CompareTag(editorOnly))
            {
                tag = editorOnly;
                EditorUtility.SetDirty(gameObject);
            }
        }
    }
}

#endif
