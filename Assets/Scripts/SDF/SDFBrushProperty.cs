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
        [ReadOnly]
        public Vector3 TranslationInv;

        [ReadOnly]
        public float ScaleInv;

        [ReadOnly]
        public Quaternion RotationInv;

        public Matrix4x4 WorldToLocal {
            get {
                Matrix4x4 matrix = Matrix4x4.Rotate(RotationInv);

                matrix.m03 = TranslationInv.x;
                matrix.m13 = TranslationInv.y;
                matrix.m23 = TranslationInv.z;

                matrix.m00 *= ScaleInv;
                matrix.m01 *= ScaleInv;
                matrix.m02 *= ScaleInv;
                matrix.m10 *= ScaleInv;
                matrix.m11 *= ScaleInv;
                matrix.m12 *= ScaleInv;
                matrix.m20 *= ScaleInv;
                matrix.m21 *= ScaleInv;
                matrix.m22 *= ScaleInv;

                return matrix;
            }
        }

        public SDFBrushTransform(Transform transform)
        {
            TranslationInv = -transform.position;
            ScaleInv = 1f / transform.localScale.x;
            RotationInv = Quaternion.Inverse(transform.rotation);
        }

        public static implicit operator SDFBrushTransform(Transform transform) => new SDFBrushTransform(transform);
    }

    public interface ISDFShape
    {
        SDFBrushType BrushType { get; }

        int ParameterCount { get; }

        void GetParameters(NativeSlice<float> dest);
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
