using System;
using System.Runtime.InteropServices;
using Sirenix.OdinInspector;
using Unity.Collections;
using UnityEngine;

namespace Antares.SDF
{
    public enum SDFBrushType { Numerical, Sphere, Cube }

    /// <typeparam name="T"></typeparam>
    public interface ISDFBrush<T> where T : struct
    {
        SDFBrushTransform Transform { get; }

        SDFBrushType BrushType { get; }

        void MapParameters(NativeArray<T> buffer);
    }

    [Serializable]
    public struct SDFBrushTransform
    {
        [field: SerializeField, LabelText(nameof(Translation))]
        public Vector3 Translation { get; }

        [field: SerializeField, LabelText(nameof(Rotation))]
        public Quaternion Rotation { get; }

        [field: SerializeField, LabelText(nameof(Scale))]
        public float Scale { get; }

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
            Rotation = Quaternion.Inverse(transform.rotation);
            Scale = transform.localScale.x;
        }
    }

    [Serializable]
    public struct SDFBrushNumerical
    {
        [field: SerializeField, LabelText(nameof(Transform))]
        public SDFBrushTransform Transform { get; }

        [field: SerializeField, LabelText(nameof(AtlasOffset))]
        public Vector3Int AtlasOffset { get; }

        [field: SerializeField, LabelText(nameof(Size))]
        public Vector3Int Size { get; }

        public SDFBrushNumerical(Transform transform, Vector3Int atlasOffset, Vector3Int size)
        {
            Transform = new SDFBrushTransform(transform);
            AtlasOffset = atlasOffset;
            Size = size;
        }
    }

#if UNITY_EDITOR
    public struct SDFBrushNumericalWrapper
    {
        public SDFBrushTransform Transform { get; }

        public Texture3D Volume { get; }

        public SDFBrushNumericalWrapper(Transform transform, Texture3D volume)
        {
            Transform = new SDFBrushTransform(transform);
            Volume = volume;
        }
    }
#endif

    [Serializable]
    public struct SDFBrushAnalytical
    {
        [field: SerializeField, LabelText(nameof(Transform))]
        public SDFBrushTransform Transform { get; }

        [field: SerializeField, LabelText(nameof(Type))]
        public SDFBrushType Type { get; }

        [field: SerializeField, LabelText(nameof(Parameters))]
        public Vector4 Parameters { get; }

        public SDFBrushAnalytical(Transform transform, SDFBrushType type, Vector4 parameters)
        {
            Debug.Assert(type != SDFBrushType.Numerical);

            Transform = new SDFBrushTransform(transform);
            Type = type;
            Parameters = parameters;
        }
    }

    /// <summary>
    /// unified brush presentation passed to compute kernel
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SDFBrushUnion
    {
        Vector3 WorldToLocalCol0;
        Vector3 WorldToLocalCol1;
        Vector3 WorldToLocalCol2;
        Vector3 WorldToLocalCol3;

        uint BrushType;

        uint MaterialID;

        uint ParameterInt0, ParameterInt1;

        float Scale;

        Vector3 Parameter0;
        Vector4 Parameter1;
        Vector4 Parameter2;
        Vector4 Parameter3;

        public static SDFBrushUnion From(SDFBrushNumerical brush)
        {
            SDFBrushUnion brushUnion = new SDFBrushUnion();

            SDFBrushTransform transform = brush.Transform;
            brushUnion.SetTransform(transform);

            brushUnion.BrushType = (uint)SDFBrushType.Numerical;
            brushUnion.MaterialID = 0;

            brushUnion.Parameter0 = brush.Size;
            brushUnion.Parameter1 = (Vector3)brush.AtlasOffset;

            return brushUnion;
        }

        public static SDFBrushUnion From(SDFBrushAnalytical brush)
        {
            SDFBrushUnion brushUnion = new SDFBrushUnion();

            SDFBrushTransform transform = brush.Transform;
            brushUnion.SetTransform(transform);

            brushUnion.BrushType = (uint)brush.Type;
            brushUnion.MaterialID = 0;

            return brushUnion;
        }

        private void SetTransform(SDFBrushTransform transform)
        {
            Matrix4x4 worldToLocal = transform.WorldToLocal;
            WorldToLocalCol0 = worldToLocal.GetColumn(0);
            WorldToLocalCol1 = worldToLocal.GetColumn(1);
            WorldToLocalCol2 = worldToLocal.GetColumn(2);
            WorldToLocalCol3 = worldToLocal.GetColumn(3);

            Scale = transform.Scale;
        }
    }
}
