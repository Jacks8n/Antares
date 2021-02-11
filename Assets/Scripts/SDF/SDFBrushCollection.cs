using System.Collections.Generic;
using System.IO;
using Antares.Utility;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

using FilePathAttribute = Sirenix.OdinInspector.FilePathAttribute;

namespace Antares.SDF
{
    [CreateAssetMenu(menuName = "SDF/BrushCollection")]
    public class SDFBrushCollection : ScriptableObject, ISerializationCallbackReceiver
    {
        [field: SerializeField, LabelText(nameof(NumericalBrushAtlas))]
        public RenderTexture NumericalBrushAtlas { get; private set; }

        public NativeArray<(SDFBrushProperty Brush, int Offset)>.ReadOnly Brushes => _brushes.AsReadOnly();

        public NativeArray<float>.ReadOnly BrushParameters => _brushParameters.AsReadOnly();

        [SerializeField, FilePath, Required]
        private string _serializationPath;

        private NativeArray<(SDFBrushProperty Brush, int Offset)> _brushes;

        private NativeArray<float> _brushParameters;

        void ISerializationCallbackReceiver.OnBeforeSerialize()
        {
            using FileStream stream = new FileStream(_serializationPath, FileMode.OpenOrCreate);

            int parameterCount = _brushParameters.Length;
            SerializationUtility.SerializeValue(parameterCount, stream, DataFormat.Binary);

            for (int i = 0; i < _brushParameters.Length; i++)
                SerializationUtility.SerializeValue(_brushParameters[i], stream, DataFormat.Binary);
        }

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            using FileStream stream = new FileStream(_serializationPath, FileMode.Open);

            int parameterCount = SerializationUtility.DeserializeValue<int>(stream, DataFormat.Binary);
            _brushParameters = new NativeArray<float>(parameterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < parameterCount; i++)
                _brushParameters[i] = SerializationUtility.DeserializeValue<float>(stream, DataFormat.Binary);
        }

#if UNITY_EDITOR
        [Button]
        private void GatherBrushes()
        {
            var numericalBrushes = new List<Texture3D>();
            var numericalBrushIndices = new List<int>();

            {
                SDFBrushHelper[] brushHelpers = FindObjectsOfType<SDFBrushHelper>();
                int brushCount = brushHelpers.Length;

                _brushes = new NativeArray<(SDFBrushProperty, int)>(brushCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                SDFBrushProperty brush = default;
                Texture3D brushTexture = null;
                int parameterCount = 0;
                for (int i = 0; i < brushHelpers.Length; i++)
                {
                    if (brushHelpers[i].GetBrushProperty(ref brush, ref brushTexture))
                    {
                        numericalBrushes.Add(brushTexture);
                        numericalBrushIndices.Add(i);
                    }

                    _brushes[i] = (brush, parameterCount);
                    parameterCount += brushHelpers[i].ShapeParameterCount;
                }

                _brushParameters = new NativeArray<float>(parameterCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                for (int i = 0; i < brushCount; i++)
                    brushHelpers[i].GetShapeParameters(_brushParameters.Slice(_brushes[i].Offset));
            }

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
                    int index = _brushes[numericalBrushIndices[i]].Offset;
                    var shape = _brushParameters.ReinterpretLoad<SDFShape.Numerical>(index);
                    shape.Offset = brushOffsets[i];
                }
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
#endif
    }
}
