using System;
using System.Collections.Generic;
using System.IO;
using Antares.Graphics;
using Antares.Utility;
using Sirenix.OdinInspector;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using ReadOnlyAttribute = Sirenix.OdinInspector.ReadOnlyAttribute;

namespace Antares.SDF
{
    [CreateAssetMenu(menuName = "SDF/BrushCollection")]
    public class SDFBrushCollection : ScriptableObject
    {
        [Serializable]
        public struct Brush
        {
            public SDFBrushProperty Property;

            [ReadOnly]
            public int ParameterCount;

            [ReadOnly]
            public int ParameterOffset;

            public Brush(SDFBrushProperty property, int count, int offset)
            {
                Property = property;
                ParameterCount = count;
                ParameterOffset = offset;
            }
        }

        [field: SerializeField, LabelText(nameof(NumericalBrushAtlas))]
        public RenderTexture NumericalBrushAtlas { get; private set; }

        [field: SerializeField, LabelText(nameof(Brushes)), ListDrawerSettings(HideAddButton = true, HideRemoveButton = true)]
        public Brush[] Brushes { get; private set; }

        [field: SerializeField, LabelText(nameof(BrushParameters)), ReadOnly]
        public float[] BrushParameters { get; private set; }

#if UNITY_EDITOR
        [Button]
        private void GatherBrushes()
        {
            SDFBrushHelper[] brushHelpers = FindObjectsOfType<SDFBrushHelper>();

            var numericalBrushes = new List<Texture3D>();
            var numericalBrushIndices = new List<int>();
            var paramBuffer = new NativeArray<float>(16, Allocator.Temp);

            // store brushes
            {
                int brushCount = brushHelpers.Length;
                int parameterCount, totalParameterCount = 0;

                Brushes = new Brush[brushCount];

                // store sizes and offsets
                {
                    SDFBrushProperty brush = default;
                    Texture3D brushTexture = null;
                    for (int i = 0; i < brushHelpers.Length; i++)
                    {
                        if (brushHelpers[i].GetBrushProperty(ref brush, ref brushTexture))
                        {
                            numericalBrushes.Add(brushTexture);
                            numericalBrushIndices.Add(i);
                        }

                        parameterCount = brushHelpers[i].ShapeParameterCount;
                        Brushes[i] = new Brush(brush, parameterCount, totalParameterCount);
                        totalParameterCount += parameterCount;
                    }
                }

                BrushParameters = new float[totalParameterCount];

                // store parameters
                int parameterOffset;
                for (int i = 0; i < brushCount; i++)
                {
                    parameterCount = Brushes[i].ParameterCount;
                    parameterOffset = Brushes[i].ParameterOffset;

                    if (paramBuffer.Length < parameterCount)
                    {
                        paramBuffer.Dispose();
                        paramBuffer = new NativeArray<float>(parameterCount, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    }

                    brushHelpers[i].GetShapeParameters(paramBuffer.Slice(0, parameterCount));

                    for (int j = 0; j < parameterCount; j++)
                        BrushParameters[parameterOffset + j] = paramBuffer[j];
                }
            }

            // generate brush atlas
            if (numericalBrushes.Count > 0)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                Vector3Int[] brushOffsets = new Vector3Int[numericalBrushes.Count];
                NumericalBrushAtlas = Texture3DAtlas.GetAtlas(cmd, numericalBrushes, brushOffsets);
                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

                for (int i = 0; i < numericalBrushIndices.Count; i++)
                {
                    int offset = Brushes[numericalBrushIndices[i]].ParameterOffset;
                    for (int j = 0; j < SDFShape.GetParameterCount<SDFShape.Numerical>(); j++)
                        BrushParameters[offset + j] = paramBuffer[j];
                    var shape = paramBuffer.ReinterpretLoad<SDFShape.Numerical>(0);
                    shape.Offset = brushOffsets[i];
                }
            }
            else
                NumericalBrushAtlas = ARenderUtilities.CreateRWVolumeRT(GraphicsFormat.R8_SNorm, new Vector3Int(4, 4, 4));

            string path = AssetDatabase.GetAssetPath(this);
            path = Path.ChangeExtension(path, "Atlas.asset");
            AssetDatabase.CreateAsset(NumericalBrushAtlas, path);
        }
#endif
    }
}
