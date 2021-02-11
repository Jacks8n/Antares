using System;
using System.Runtime.InteropServices;
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

        // todo: default implementation of interface functions are not supported by unity currently
        void GetParameters(NativeArray<float> dest);
    }

    [Serializable]
    public struct SDFBrush
    {
        public SDFBrushTransform Transform;

        [field: SerializeField, LabelText(nameof(BrushType))]
        public SDFBrushType BrushType { get; private set; }

        public int MaterialID;

        [field: SerializeField, LabelText(nameof(ParameterCount))]
        public int ParameterCount { get; private set; }

        public SDFBrush(SDFBrushTransform transform, SDFBrushType brushType, int materialID, int parameterCount)
        {
            Transform = transform;
            BrushType = brushType;
            MaterialID = materialID;
            ParameterCount = parameterCount;
        }

        public static SDFBrush FromShape<T>(Transform transform, T shape, int materialID) where T : ISDFShape
        {
            return new SDFBrush(transform, shape.BrushType, materialID, shape.ParameterCount);
        }
    }

    public static class SDFShape
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
        public struct Numerical : ISDFShape
        {
            public Vector3 Offset;

            public Vector3 Size;

            public SDFBrushType BrushType => SDFBrushType.Numerical;

            public int ParameterCount => 6;

            void ISDFShape.GetParameters(NativeArray<float> dest) => dest.ReinterpretStore(0, this);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
        public struct Sphere : ISDFShape
        {
            [Min(0.01f)]
            public float Radius;

            public SDFBrushType BrushType => SDFBrushType.Sphere;

            public int ParameterCount => 1;

            void ISDFShape.GetParameters(NativeArray<float> dest) => dest.ReinterpretStore(0, this);
        }
    }
}
