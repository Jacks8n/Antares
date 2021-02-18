using System;
using Antares.Graphics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Antares.SDF
{
    public static class SDFGenerator
    {
        private const float UnitDistance = AShaderSpecs.SDFSupremum * 2f / (byte.MaxValue - byte.MinValue);

        private const float InvUnitDistance = 1f / UnitDistance;

        public static Texture3D CreateSDFTexture3D(int sizeX, int sizeY, int sizeZ, Func<Vector3, float> sdf)
        {
            Texture3D result = new Texture3D(sizeX, sizeY, sizeZ, GraphicsFormat.R8_SNorm, TextureCreationFlags.None, mipCount: 0);
            NativeArray<byte> values = new NativeArray<byte>(sizeX * sizeY * sizeZ, Allocator.Temp);
            for (int i = 0, index = 0; i < sizeX; i++)
                for (int j = 0; j < sizeY; j++)
                    for (int k = 0; k < sizeZ; k++)
                    {
                        float value = sdf(new Vector3(i, j, k)) * InvUnitDistance;
                        value = value >= 0 ? Mathf.Floor(value) : Mathf.Ceil(value);
                        values[index++] = (byte)Mathf.Clamp((int)value, byte.MinValue, byte.MaxValue);
                    }
            result.SetPixelData(values, mipLevel: 0);
            values.Dispose();

            result.Apply(updateMipmaps: false, makeNoLongerReadable: false);

            return result;
        }
    }
}
