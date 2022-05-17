using Unity.Collections;
using UnityEngine;

namespace Antares.Utility
{
    public static class NativeContainerExtension
    {
        public static TTo ReinterpretLoad<TTo, TFrom>(this NativeSlice<TFrom> slice, int index) where TTo : struct where TFrom : struct
        {
            return slice.Slice(index).SliceConvert<TTo>()[0];
        }

        public static void ReinterpretStore<TTo, TFrom>(this NativeSlice<TFrom> slice, int index, TTo value) where TTo : struct where TFrom : struct
        {
            var reinterpretSlice = slice.SliceConvert<TTo>();
            reinterpretSlice[index] = value;
        }
    }
}
