using System;
using Sirenix.OdinInspector;
using Unity.Collections;
using UnityEngine;

using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;

namespace Antares.SDF
{
    public enum SDFBrushType { Numerical, Sphere, Cube }

    [Serializable]
    public struct SDFBrushTransform
    {
        public Vector3 Translation;

        public float Scale;

        public Quaternion Rotation;

        // order of matrix multiplication doesn't matter since scale is uniform
        public Matrix4x4 WorldToLocal {
            get {
                float scaleInv = 1f / Scale;
                return Matrix4x4.TRS(Translation, Rotation, new Vector3(scaleInv, scaleInv, scaleInv));
            }
        }

        public SDFBrushTransform(Transform transform)
        {
            Translation = -transform.position;
            Scale = transform.localScale.x;
            Rotation = Quaternion.Inverse(transform.rotation);
        }

        public static implicit operator SDFBrushTransform(Transform transform) => new SDFBrushTransform(transform);
    }

    public interface ISDFShape
    {
#if UNITY_EDITOR
        SDFBrushType BrushType { get; }

        int ParameterCount { get; }

        void GetParameters(NativeSlice<float> dest);
#endif
    }

    [Serializable]
    public struct SDFBrushProperty
    {
        public SDFBrushTransform Transform;

        [field: SerializeField, LabelText(nameof(BrushType)), ReadOnly]
        public SDFBrushType BrushType { get; private set; }

        public uint MaterialID;

        public SDFBrushProperty(SDFBrushTransform transform, SDFBrushType brushType, uint materialID)
        {
            Transform = transform;
            BrushType = brushType;
            MaterialID = materialID;
        }

        public static SDFBrushProperty FromShape<T>(Transform transform, T shape, uint materialID) where T : ISDFShape
        {
            return new SDFBrushProperty(transform, shape.BrushType, materialID);
        }
    }
}
