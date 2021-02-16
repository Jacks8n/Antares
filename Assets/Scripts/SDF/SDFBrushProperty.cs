using System;
using System.Runtime.InteropServices;
using Antares.Utility;
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

    public static class SDFShape
    {
        /// <summary>
        /// all the shapes are guaranteed to use less than this number of paramters
        /// </summary>
        public const int MaxParameterCount = 8;

        public static unsafe int GetParameterCount<T>() where T: unmanaged, ISDFShape
        {
            return sizeof(T) / sizeof(float);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
        public struct Numerical : ISDFShape
        {
            public Vector3 Offset;

            public Vector3 Size;

            public SDFBrushType BrushType => SDFBrushType.Numerical;

            public int ParameterCount => 6;

            void ISDFShape.GetParameters(NativeSlice<float> dest) => dest.ReinterpretStore(0, this);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
        public struct Sphere : ISDFShape
        {
            [Min(0.01f)]
            public float Radius;

            public SDFBrushType BrushType => SDFBrushType.Sphere;

            public int ParameterCount => 1;

            void ISDFShape.GetParameters(NativeSlice<float> dest) => dest.ReinterpretStore(0, this);
        }
    }
}
