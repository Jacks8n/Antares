using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Antares.SDF
{
#if UNITY_EDITOR
    [ExecuteAlways, RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class SDFBrushHelper : MonoBehaviour
    {
        public static IEnumerable<SDFBrushHelper> EnabledSDFBrushes => _enabledSDFBrushes;

        private static LinkedList<SDFBrushHelper> _enabledSDFBrushes = new LinkedList<SDFBrushHelper>();

        public SDFBrush Brush {
            get {
                SDFPresentation presentation;
                switch (_type)
                {
                    case SDFPresentationType.Numerical:
                        presentation = new SDFPresentation(_bakedTexture);
                        break;
                    case SDFPresentationType.Sphere:
                        presentation = new SDFPresentation(_type, new Vector4(_radius, 0f));
                        break;
                    default:
                        throw new NotImplementedException();
                }
                return new SDFBrush(transform, presentation);
            }
        }

        private LinkedListNode<SDFBrushHelper> _listNode;

        [SerializeField, OnValueChanged("UpdatePreview")]
        private SDFPresentationType _type;

        [SerializeField, ShowIf("_type", SDFPresentationType.Numerical)]
        private Texture3D _bakedTexture;

        [SerializeField, ShowIf("_type", SDFPresentationType.Sphere)]
        private float _radius;

        private static void UpdatePreview()
        {
        }

        private void Awake()
        {
            const string editorOnly = "EditorOnly";
            if (CompareTag(editorOnly))
            {
                tag = editorOnly;
                EditorUtility.SetDirty(gameObject);
            }
        }

        private void OnEnable()
        {
            _listNode = _enabledSDFBrushes.AddLast(this);
        }

        private void OnDisable()
        {
            _enabledSDFBrushes.Remove(_listNode);
        }
    }
#endif
}
