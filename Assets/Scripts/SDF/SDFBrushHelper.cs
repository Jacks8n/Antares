#if UNITY_EDITOR

using System;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Antares.SDF
{
    [ExecuteAlways, RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class SDFBrushHelper : SerializedMonoBehaviour
    {
        [OdinSerialize, NonSerialized]
        private ISDFShape _shape;

        [SerializeField, ShowIf("@_shape != null && _shape.BrushType == SDFBrushType.Numerical"), Required, OnValueChanged("OnSetBrushTexture")]
        private Texture3D _brushTexture;

        [SerializeField]
        private uint _materialID;

        public int ShapeParameterCount => _shape.ParameterCount;

        public void GetShapeParameters(NativeSlice<float> dest) => _shape.GetParameters(dest);

        /// <returns>whether the brush is numerical</returns>
        public bool GetBrushProperty(ref SDFBrushProperty brushProperty, ref Texture3D brushTexture)
        {
            if (_shape is SDFShapes.Numerical)
            {
                brushProperty = SDFBrushProperty.FromShape(transform, _shape, _materialID);
                brushTexture = _brushTexture;
                return true;
            }
            else
            {
                brushProperty = SDFBrushProperty.FromShape(transform, _shape, _materialID);
                return false;
            }
        }

        private void OnSetBrushTexture()
        {
            if (_brushTexture != null)
                _shape = new SDFShapes.Numerical() {
                    Size = new Vector3(_brushTexture.width, _brushTexture.height, _brushTexture.depth)
                };
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
