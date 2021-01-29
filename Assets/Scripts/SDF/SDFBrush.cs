using System;
using System.Runtime.InteropServices;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.SDF
{
    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SDFBrushTransform
    {
        [field: SerializeField, LabelText(nameof(Position))]
        public Vector3 Position { get; }

        [field: SerializeField, LabelText(nameof(Scale))]
        public float Scale { get; }

        [field: SerializeField, LabelText(nameof(Rotation))]
        public Quaternion Rotation { get; }

        public SDFBrushTransform(Transform transform)
        {
            Position = transform.position;
            Scale = transform.localScale.x;
            Rotation = transform.rotation;
        }
    }

    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 1)]
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

    public enum SDFBrushAnalyticalType { Sphere, Cube }

    [Serializable, StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct SDFBrushAnalytical
    {
        [field: SerializeField, LabelText(nameof(Transform))]
        public SDFBrushTransform Transform { get; }

        [field: SerializeField, LabelText(nameof(Type))]
        public SDFBrushAnalyticalType Type { get; }

        [field: SerializeField, LabelText(nameof(Parameters))]
        public Vector4 Parameters { get; }

        public SDFBrushAnalytical(Transform transform, SDFBrushAnalyticalType type, Vector4 parameters)
        {
            Transform = new SDFBrushTransform(transform);
            Type = type;
            Parameters = parameters;
        }
    }
}
