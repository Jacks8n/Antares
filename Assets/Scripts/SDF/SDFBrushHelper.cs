#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace Antares.SDF
{
    [ExecuteAlways, RequireComponent(typeof(MeshRenderer), typeof(MeshFilter))]
    public class SDFBrushHelper : MonoBehaviour
    {
        public static IEnumerable<SDFBrushHelper> EnabledSDFBrushes => _enabledSDFBrushes;

        private static LinkedList<SDFBrushHelper> _enabledSDFBrushes = new LinkedList<SDFBrushHelper>();

        private LinkedListNode<SDFBrushHelper> _listNode;

        [SerializeField]
        private bool _useAnalytic;

        [SerializeField, ShowIf(nameof(_useAnalytic))]
        private SDFBrushAnalyticalType _type;

        [SerializeField, HideIf(nameof(_useAnalytic))]
        private Texture3D _bakedTexture;

        [SerializeField, ShowIf("@_useAnalytic && _type == SDFBrushAnalyticalType.Sphere")]
        private float _radius;

        [SerializeField, ShowIf("@_useAnalytic && _type == SDFBrushAnalyticalType.Cube")]
        private Vector3 _cubeSize;

        public bool GetBrush(out SDFBrushNumericalWrapper brushNumerical, out SDFBrushAnalytical brushAnalytical)
        {
            if (_useAnalytic)
            {
                brushNumerical = default;

                Vector4 parameters;
                switch (_type)
                {
                    case SDFBrushAnalyticalType.Sphere:
                        parameters = new Vector4(_radius, 0f);
                        break;
                    case SDFBrushAnalyticalType.Cube:
                        parameters = _cubeSize;
                        break;
                    default:
                        throw new NotImplementedException();
                }
                brushAnalytical = new SDFBrushAnalytical(transform, _type, parameters);

                return false;
            }
            else
            {
                brushNumerical = new SDFBrushNumericalWrapper(transform, _bakedTexture);
                brushAnalytical = default;
                return true;
            }
        }

        private static void UpdatePreview()
        {
            // TODO
            Debug.LogWarning("todo");
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

        private void OnEnable()
        {
            _listNode = _enabledSDFBrushes.AddLast(this);
        }

        private void OnDisable()
        {
            _enabledSDFBrushes.Remove(_listNode);
        }
    }
}

#endif
