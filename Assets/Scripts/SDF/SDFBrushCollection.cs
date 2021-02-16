using System;
using System.Collections.Generic;
using System.IO;
using Antares.Utility;
using Sirenix.OdinInspector;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
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
            public int ParameterOffset;

            public Brush(SDFBrushProperty property, int offset)
            {
                Property = property;
                ParameterOffset = offset;
            }
        }

        [field: SerializeField, LabelText(nameof(NumericalBrushAtlas))]
        public RenderTexture NumericalBrushAtlas { get; private set; }

        [field: SerializeField, LabelText(nameof(Brushes))]
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
            using var paramBuffer = new NativeArray<float>(SDFShape.MaxParameterCount, Allocator.Temp);

            {
                int brushCount = brushHelpers.Length;
                int parameterCount, totalParameterCount = 0;

                Brushes = new Brush[brushCount];

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
                        Brushes[i] = new Brush(brush, parameterCount);
                        totalParameterCount += parameterCount;
                    }
                }

                BrushParameters = new float[totalParameterCount];

                int parameterOffset = 0;
                for (int i = 0; i < brushCount; i++)
                {
                    parameterCount = Brushes[i].ParameterOffset;
                    Brushes[i].ParameterOffset = parameterOffset;

                    brushHelpers[i].GetShapeParameters(paramBuffer);
                    for (int j = 0; j < parameterCount; j++)
                        BrushParameters[parameterOffset + j] = paramBuffer[j];
                    parameterOffset += parameterCount;
                }
            }

            if (numericalBrushes.Count > 0)
            {
                CommandBuffer cmd = CommandBufferPool.Get();
                Vector3Int[] brushOffsets = new Vector3Int[numericalBrushes.Count];
                NumericalBrushAtlas = Texture3DAtlas.GetAtlas(cmd, numericalBrushes, brushOffsets);
                UnityEngine.Graphics.ExecuteCommandBuffer(cmd);

                string path = AssetDatabase.GetAssetPath(this);
                path = Path.GetDirectoryName(path) + "_Atlas.assets";
                AssetDatabase.AddObjectToAsset(NumericalBrushAtlas, path);

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
                NumericalBrushAtlas = null;
        }
#endif
    }
}
