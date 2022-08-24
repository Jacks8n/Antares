#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Antares.Utility
{
    public class ComputeShaderPostprocessor : AssetPostprocessor
    {
        private static readonly Dictionary<ComputeShader, Action<ComputeShader>> Callbacks = new Dictionary<ComputeShader, Action<ComputeShader>>();

        public static void SetImportHandler(ComputeShader shader, Action<ComputeShader> callback)
        {
            Callbacks[shader] = callback;
        }

        public static void RemoveImportHandler(ComputeShader shader)
        {
            Callbacks.Remove(shader);
        }

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            foreach (string str in importedAssets)
            {
                if (str.EndsWith(".compute"))
                {
                    var shader = AssetDatabase.LoadAssetAtPath(str, typeof(ComputeShader)) as ComputeShader;
                    if (shader != null && shader && Callbacks.TryGetValue(shader, out var callback))
                    {
                        if (callback != null)
                            callback(shader);
                        else
                            Callbacks.Remove(shader);
                    }
                }
            }
        }
    }
}

#endif
