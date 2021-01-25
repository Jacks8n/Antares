using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Antares.SDF
{
    public enum SDFPresentationType { Numerical, Sphere }

    [StructLayout(LayoutKind.Explicit), Serializable]
    public struct SDFPresentation
    {
        [field: FieldOffset(0)]
        public SDFPresentationType Type { get; }

        [field: FieldOffset(sizeof(SDFPresentationType))]
        public Vector4 AnalyticalSDFParams { get; }

        [field: FieldOffset(sizeof(SDFPresentationType))]
        public Texture3D NumericalSDF { get; }

        public SDFPresentation(SDFPresentationType type, Vector4 analyticalSDFParams)
        {
            Type = type;
            NumericalSDF = default;
            AnalyticalSDFParams = analyticalSDFParams;
        }

        public SDFPresentation(Texture3D numericalSDF)
        {
            Type = SDFPresentationType.Numerical;
            AnalyticalSDFParams = default;
            NumericalSDF = numericalSDF;
        }
    }
}
