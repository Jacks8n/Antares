using System;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Antares.SDF
{
    [Serializable]
    public struct SDFBrushTransform
    {
        [field: SerializeField, LabelText(nameof(Position))]
        public Vector3 Position { get; }

        [field: SerializeField, LabelText(nameof(Rotation))]
        public Quaternion Rotation { get; }

        [field: SerializeField, LabelText(nameof(Scale))]
        public Vector3 Scale { get; }

        public SDFBrushTransform(Transform transform)
        {
            Position = transform.position;
            Rotation = transform.rotation;
            Scale = transform.localScale;
        }
    }

    [Serializable]
    public struct SDFBrushNumerical
    {
        [field: SerializeField, LabelText(nameof(Transform))]
        public SDFBrushTransform Transform { get; }

        [field: SerializeField, LabelText(nameof(Volume))]
        public Texture3D Volume { get; }

        public SDFBrushNumerical(Transform transform, Texture3D volume)
        {
            Transform = new SDFBrushTransform(transform);
            Volume = volume;
        }
    }

    public enum SDFBrushAnalyticalType { Sphere, Cube }

    [Serializable]
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
