using System;
using System.Runtime.InteropServices;
using Antares.Utility;
using Sirenix.OdinInspector;
using Unity.Collections;
using UnityEngine;

namespace Antares.SDF
{
    public static class SDFShapes
    {
        public static unsafe int GetParameterCount<T>() where T : unmanaged, ISDFShape
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

            public void GetParameters(NativeSlice<float> dest) => dest.ReinterpretStore(0, this);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
        public struct Sphere : ISDFShape
        {
            [Min(0.1f)]
            public float Radius;

            public SDFBrushType BrushType => SDFBrushType.Sphere;

            public int ParameterCount => 1;

            public void GetParameters(NativeSlice<float> dest) => dest.ReinterpretStore(0, this);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1), Serializable]
        public struct Cube : ISDFShape
        {
            [MinValue(0.1f)]
            public Vector3 Size;

            public SDFBrushType BrushType => SDFBrushType.Cube;

            public int ParameterCount => 3;

            public void GetParameters(NativeSlice<float> dest) => dest.ReinterpretStore(0, this);
        }
    }
}
